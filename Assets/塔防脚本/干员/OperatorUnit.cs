using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OperatorUnit : MonoBehaviour
{
    public static readonly List<OperatorUnit> AllOperators = new List<OperatorUnit>();
    [Header("数据引用")]
    [Tooltip("干员数据，可不拖拽：从卡片部署时会自动注入；场景里直接放的prefab需在预制体上指定")]
    public OperatorData data;
    [Tooltip("阻挡组件，可不拖拽：未填时自动从本物体读取UnitBlocker")]
    public UnitBlocker blocker;

    [Header("状态")]
    public bool isMoving = false;
    public int currentBlockCount = 1;

    [HideInInspector] public bool skillPreventAttack = false;

    [HideInInspector] public bool skillAttackAllBlocked = false;

    [HideInInspector] public int deployCost = 0;

    private bool isEncountering = false;
    private bool chooseToFight = false;
    private bool _suppressEncounterUntilExit = false;
    private bool _pendingEvadeContactDamage = false;

    // 移动中的干员不阻挡敌人（部署/换位途中），供 UnitBlocker 判断
    public bool IsEvading() => !chooseToFight && isMoving;

    /// <summary>启用 Animator 播放走路/攻击动画，由 OperatorAttackAnimator 和 MoveRoutine 调用。</summary>
    public void EnableAnimator()
    {
        if (animator != null) animator.enabled = true;
    }

    /// <summary>禁用 Animator 并恢复预制体默认 sprite，用于待机状态。</summary>
    public void DisableAnimator()
    {
        if (animator != null) animator.enabled = false;
        if (spriteRenderer != null && originalSprite != null) spriteRenderer.sprite = originalSprite;
    }

    private SpriteRenderer spriteRenderer;
    private int originalSortingOrder;
    private UnitStatusUI statusUI;
    [HideInInspector] public int currentHealth;
    [HideInInspector] public int runtimeMaxHealth;
    [HideInInspector] public int runtimeAttackDamage;
    [HideInInspector] public int runtimeDefense;
    [HideInInspector] public float runtimeAttackInterval;
    [HideInInspector] public float traitAttackSpeedMultiplier = 1f;
    /// <summary>实际攻击间隔 = 基础间隔 ÷ 攻速倍率。技能改 runtimeAttackInterval，特质倍率改这里，二者互不打架。</summary>
    public float EffectiveAttackInterval => runtimeAttackInterval / Mathf.Max(0.01f, traitAttackSpeedMultiplier);
    private float attackTimer = 0f;

    [Tooltip("当前技能，可不拖拽：未填时自动从本物体读取OperatorSkill子类组件")]
    public OperatorSkill currentSkill;
    public float maxSP = 10f;
    public float currentSP = 0f;
    public bool isSkillActive = false;
    public bool isSkillReady = false;
    private float currentSkillTime = 0f;

    private Vector3 originalTargetWorldPos;
    private bool isTargetingHighGround = false;
    private Vector3 occupiedPosition;
    private bool hasOccupied = false;

    private OperatorBrain brain;
    private Animator animator;
    private Sprite originalSprite;

    void Awake()
    {
        if (!AllOperators.Contains(this)) AllOperators.Add(this);
        if (blocker == null) blocker = GetComponent<UnitBlocker>();
        if (currentSkill == null) currentSkill = GetComponent<OperatorSkill>();
    }

    void Start()
    {
        if (blocker == null) blocker = GetComponent<UnitBlocker>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        statusUI = GetComponentInChildren<UnitStatusUI>();
        brain = GetComponent<OperatorBrain>();
        animator = GetComponentInChildren<Animator>();
        if (spriteRenderer != null) originalSprite = spriteRenderer.sprite;
        if (animator != null) animator.enabled = false;

        if (blocker != null) currentBlockCount = blocker.maxBlockCount;
        else currentBlockCount = 1;

        if (data != null)
        {
            runtimeMaxHealth = (int)data.maxHealth;
            currentHealth = runtimeMaxHealth;
            runtimeAttackDamage = (int)data.attackDamage;
            runtimeAttackInterval = data.attackInterval;
            runtimeDefense = data.defense;

            runtimeAttackDamage += TalentEffectApplier.GetAttackBonus(data);
            runtimeDefense += TalentEffectApplier.GetDefenseBonus(data);
            runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * TalentEffectApplier.GetAttackPercent(data) / 100f);
            runtimeDefense += Mathf.RoundToInt(runtimeDefense * TalentEffectApplier.GetDefensePercent(data) / 100f);
            runtimeMaxHealth += Mathf.RoundToInt(runtimeMaxHealth * TalentEffectApplier.GetMaxHpPercentBonus(data) / 100f);
            runtimeAttackInterval /= TalentEffectApplier.GetAttackSpeedMultiplier(data);
            currentHealth = runtimeMaxHealth;

            string opId = data != null ? data.operatorName : name;
            
            
            
        }
        else
            runtimeDefense = 0;
        
        if (statusUI != null) statusUI.UpdateHP(currentHealth, runtimeMaxHealth);

        if (currentSkill == null) currentSkill = GetComponent<OperatorSkill>();
        if (currentSkill == null) TryAttachDefaultSkill();
        if (currentSkill != null)
        {
            currentSkill.Initialize(this);
            maxSP = currentSkill.maxSP;
            InitUI(maxSP);
        }
        else InitUI(1);

        TryRegisterPrePlacedPosition();
    }

    /// <summary>
    /// 当预制体上没有挂 OperatorSkill 组件时，按职业类型(opType)自动附加一个默认技能。
    /// 这样新干员只要设置 OperatorType 就能自动获得对应技能，无需每个预制体手动接线
    /// （契合「未来干员自动接入」的设计约束）。已手动挂了技能组件的干员不会被覆盖。
    /// </summary>
    private void TryAttachDefaultSkill()
    {
        if (data == null) return;
        System.Type skillType = GetDefaultSkillType(data.opType);
        if (skillType == null) return;

        currentSkill = (OperatorSkill)gameObject.AddComponent(skillType);
        ApplyDefaultSkillConfig(currentSkill, data.opType);
    }

    private static System.Type GetDefaultSkillType(OperatorData.OperatorType opType)
    {
        switch (opType)
        {
            case OperatorData.OperatorType.Vanguard:   return typeof(Skill_DPBurst);
            case OperatorData.OperatorType.Guard:      return typeof(Skill_PenetrateDefense);
            case OperatorData.OperatorType.Defender:   return typeof(Skill_GoldenDefense);
            case OperatorData.OperatorType.Sniper:     return typeof(Skill_PowerUp);
            case OperatorData.OperatorType.Caster:     return typeof(Skill_RangeExpand);
            case OperatorData.OperatorType.Medic:      return typeof(Skill_RangeExpand);          // 暂用范围扩大兜底，待专属治疗技能接入
            case OperatorData.OperatorType.Specialist: return typeof(Skill_BlockAndStrikeAll);
            default:                      return null;
        }
    }

    private static void ApplyDefaultSkillConfig(OperatorSkill skill, OperatorData.OperatorType opType)
    {
        if (skill == null) return;
        switch (opType)
        {
            case OperatorData.OperatorType.Vanguard:
            {
                var s = skill as Skill_DPBurst;
                if (s != null)
                {
                    s.autoActivate = true;   // 自动回费先锋：技力满自动触发
                    s.maxSP = 18f;
                    s.duration = 0.5f;
                    s.dpBurst = 12;
                    s.initialSPOnDeploy = 6f;
                    s.enableFlash = true;
                }
                break;
            }
            case OperatorData.OperatorType.Guard:
            {
                var s = skill as Skill_PenetrateDefense;
                if (s != null)
                {
                    s.autoActivate = false;
                    s.maxSP = 30f;
                    s.duration = 10f;
                    s.initialSPOnDeploy = 25f;
                }
                break;
            }
            case OperatorData.OperatorType.Defender:
            {
                var s = skill as Skill_GoldenDefense;
                if (s != null)
                {
                    s.autoActivate = false;
                    s.maxSP = 30f;
                    s.duration = 60f;
                    s.healthMultiplier = 3f;
                    s.blockCountBonus = 2;
                }
                break;
            }
            case OperatorData.OperatorType.Sniper:
            {
                var s = skill as Skill_PowerUp;
                if (s != null)
                {
                    s.autoActivate = false;
                    s.maxSP = 25f;
                    s.duration = 12f;
                    s.damageMultiplier = 2f;
                }
                break;
            }
            case OperatorData.OperatorType.Caster:
            case OperatorData.OperatorType.Medic:
            {
                var s = skill as Skill_RangeExpand;
                if (s != null)
                {
                    s.autoActivate = false;
                    s.maxSP = 20f;
                    s.duration = 30f;
                    s.rangeMultiplier = 1.5f;
                }
                break;
            }
            case OperatorData.OperatorType.Specialist:
            {
                var s = skill as Skill_BlockAndStrikeAll;
                if (s != null)
                {
                    s.autoActivate = false;
                    s.maxSP = 40f;
                    s.duration = 60f;
                    s.skillBlockCount = 10;
                    s.damageMultiplier = 4f / 3f;
                }
                break;
            }
        }
    }

    public void SyncRuntimeFromData()
    {
        if (data == null) return;
        runtimeMaxHealth = (int)data.maxHealth;
        currentHealth = runtimeMaxHealth;
        runtimeAttackDamage = (int)data.attackDamage;
        runtimeAttackInterval = data.attackInterval;
        runtimeDefense = data.defense;

        runtimeAttackDamage += TalentEffectApplier.GetAttackBonus(data);
        runtimeDefense += TalentEffectApplier.GetDefenseBonus(data);
        runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * TalentEffectApplier.GetAttackPercent(data) / 100f);
        runtimeDefense += Mathf.RoundToInt(runtimeDefense * TalentEffectApplier.GetDefensePercent(data) / 100f);
        runtimeMaxHealth += Mathf.RoundToInt(runtimeMaxHealth * TalentEffectApplier.GetMaxHpPercentBonus(data) / 100f);
        runtimeAttackInterval /= TalentEffectApplier.GetAttackSpeedMultiplier(data);
        currentHealth = runtimeMaxHealth;

        if (statusUI != null) statusUI.UpdateHP(currentHealth, runtimeMaxHealth);
    }

    private void TryRegisterPrePlacedPosition()
    {
        if (hasOccupied || GridSystem.Instance == null) return;
        Vector3 pos = transform.position;
        Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(pos);
        bool hasGround = GridSystem.Instance.groundTilemap.HasTile(cellPos);
        bool hasWall = GridSystem.Instance.wallTilemap.HasTile(cellPos);
        bool hasHigh = GridSystem.Instance.highGroundTilemap.HasTile(cellPos);
        if (hasWall) return;
        Vector3 cellCenter;
        if (hasHigh)
        {
            cellCenter = GridSystem.Instance.highGroundTilemap.GetCellCenterWorld(cellPos);
            currentBlockCount = 0;
        }
        else if (hasGround)
            cellCenter = GridSystem.Instance.groundTilemap.GetCellCenterWorld(cellPos);
        else
            return;
        occupiedPosition = cellCenter;
        GridSystem.Instance.SetCellOccupied(cellCenter, true);
        hasOccupied = true;
    }

    void OnDestroy()
    {
        AllOperators.Remove(this);
        if (hasOccupied && GridSystem.Instance != null)
            GridSystem.Instance.SetCellOccupied(occupiedPosition, false);
    }

    public bool IsStandingOnCell()
    {
        bool isBlocking = blocker != null && blocker.blockedEnemies.Count > 0;
        return !isMoving || isBlocking;
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (spriteRenderer != null)
        {
            if (isHighlighted)
            {
                originalSortingOrder = spriteRenderer.sortingOrder;
                spriteRenderer.sortingOrder = 999;
            }
            else
            {
                spriteRenderer.sortingOrder = originalSortingOrder;
            }
        }
    }

    public void TeleportTo(Vector3 newWorldPos)
    {
        if (GridSystem.Instance == null) return;
        if (hasOccupied) GridSystem.Instance.SetCellOccupied(occupiedPosition, false);

        Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(newWorldPos);
        bool hasHigh = GridSystem.Instance.highGroundTilemap.HasTile(cellPos);
        bool hasGround = GridSystem.Instance.groundTilemap.HasTile(cellPos);
        bool hasWall = GridSystem.Instance.wallTilemap.HasTile(cellPos);
        Vector3 cellCenter;
        if (hasHigh)
        {
            cellCenter = GridSystem.Instance.highGroundTilemap.GetCellCenterWorld(cellPos);
            currentBlockCount = 0;
        }
        else if (hasGround && !hasWall)
            cellCenter = GridSystem.Instance.groundTilemap.GetCellCenterWorld(cellPos);
        else
            cellCenter = newWorldPos;

        transform.position = cellCenter;
        occupiedPosition = cellCenter;
        hasOccupied = true;
        isMoving = false;
        GridSystem.Instance.SetCellOccupied(cellCenter, true);
        if (hasHigh) currentBlockCount = 0;
        else if (blocker != null) currentBlockCount = blocker.maxBlockCount;
        else currentBlockCount = 1;
    }

    public bool IsStandingOnHighGround()
    {
        if (GridSystem.Instance == null || GridSystem.Instance.highGroundTilemap == null) return false;
        Vector3Int cell = GridSystem.Instance.highGroundTilemap.WorldToCell(transform.position);
        return GridSystem.Instance.highGroundTilemap.HasTile(cell);
    }

    public static bool IsCellOccupiedByStandingOperator(Vector3 worldPos, OperatorUnit self)
    {
        if (GridSystem.Instance == null) return false;
        var myNode = GridSystem.Instance.NodeFromWorldPoint(worldPos);
        if (myNode == null) return false;

        foreach (var op in AllOperators)
        {
            if (op == null || op == self) continue;
            if (!op.IsStandingOnCell()) continue;
            var theirNode = GridSystem.Instance.NodeFromWorldPoint(op.transform.position);
            if (theirNode != null && theirNode.gridX == myNode.gridX && theirNode.gridY == myNode.gridY)
                return true;
        }
        return false;
    }

    public void MoveToDestination(Vector3 destination)
    {
        if (GridSystem.Instance == null) return;

        isEncountering = false;
        chooseToFight = false;

        if (blocker != null) currentBlockCount = blocker.maxBlockCount;
        else currentBlockCount = 1;

        if (hasOccupied) GridSystem.Instance.SetCellOccupied(occupiedPosition, false);
        occupiedPosition = destination;
        GridSystem.Instance.SetCellOccupied(occupiedPosition, true);
        hasOccupied = true;

        originalTargetWorldPos = destination;
        Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(destination);
        isTargetingHighGround = GridSystem.Instance.highGroundTilemap.HasTile(cellPos);

        List<Vector3> path = GridSystem.Instance.FindPath(transform.position, destination);
        if (path != null && path.Count > 0)
        {
            StopAllCoroutines();
            isMoving = true;
            StartCoroutine(MoveRoutine(path));
        }
        else
        {
            if (isTargetingHighGround && Vector3.Distance(transform.position, destination) < 2.0f)
            {
                OnArriveDestination();
            }
        }
    }

    IEnumerator MoveRoutine(List<Vector3> path)
    {
        isMoving = true;
        int targetIndex = 0;
        EnableAnimator();

        while (targetIndex < path.Count)
        {
            if (isEncountering)
            {
                yield return null;
                continue;
            }

            if (chooseToFight && blocker != null && blocker.blockedEnemies.Count > 0)
            {
                yield return null;
                continue;
            }

            Vector3 currentWaypoint = path[targetIndex];
            float moveSpeed = ((brain != null) ? brain.moveSpeed : 1f) * TalentEffectApplier.GetMoveSpeedMultiplier(data);
            transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, currentWaypoint) < 0.05f)
            {
                targetIndex++;
            }
            yield return null;
        }

        isMoving = false;
        DisableAnimator();
        OnArriveDestination();
    }

    void OnArriveDestination()
    {
        _suppressEncounterUntilExit = false;
        if (GridSystem.Instance == null) return;

        if (isTargetingHighGround)
        {
            Vector3Int cellPos = GridSystem.Instance.highGroundTilemap.WorldToCell(originalTargetWorldPos);
            Vector3 highGroundCenter = GridSystem.Instance.highGroundTilemap.GetCellCenterWorld(cellPos);
            transform.position = highGroundCenter;
            currentBlockCount = 0;
        }
        else
        {
            if (blocker != null) currentBlockCount = blocker.maxBlockCount;
            else currentBlockCount = 1;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartEncounter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartEncounter(other);
    }

    private void TryStartEncounter(Collider2D other)
    {
        if (!isMoving) return;
        if (isEncountering) return;
        if (_suppressEncounterUntilExit) return;
        if (!other.CompareTag("Enemy")) return;

        Enemy2 enemy = other.GetComponent<Enemy2>();
        if (enemy == null) return;
        if (IsCellOccupiedByStandingOperator(transform.position, this))
            return;

        isEncountering = true;
        if (EncounterManager.Instance != null)
            EncounterManager.Instance.TriggerEncounter(this);
        else
            isEncountering = false;
    }

    public void ResolveEncounter(bool fight)
    {
        chooseToFight = fight;
        if (fight)
        {
            if (blocker != null) currentBlockCount = blocker.maxBlockCount;
            else currentBlockCount = 1;
            _pendingEvadeContactDamage = false;
        }
        else
        {
            if (blocker != null) blocker.ReleaseAllEnemies();
            currentBlockCount = 0;
            _pendingEvadeContactDamage = true;
        }

        _suppressEncounterUntilExit = true;
        isEncountering = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (_pendingEvadeContactDamage)
            {
                Enemy2 enemy = other.GetComponentInParent<Enemy2>();
                if (enemy != null)
                {
                    int contactDamage = enemy.GetContactDamage();
                    if (contactDamage > 0)
                    {
                        bool ignoreDef = enemy.GetComponent<IgnoreDefenseAttacker>() != null;
                        TakeDamage(contactDamage, ignoreDef);
                    }
                }
                _pendingEvadeContactDamage = false;
            }
            _suppressEncounterUntilExit = false;
        }
    }

    void Update()
    {
        UpdateSkillState();

        if (blocker != null)
        {
            for (int i = blocker.blockedEnemies.Count - 1; i >= 0; i--)
            {
                if (blocker.blockedEnemies[i] == null)
                {
                    blocker.blockedEnemies.RemoveAt(i);
                }
            }

            bool hasMeleeTarget = blocker.blockedEnemies.Count > 0 || blocker.HasBlockedSpawner();
            if (!skillPreventAttack && hasMeleeTarget)
            {
                attackTimer += Time.deltaTime;
                if (attackTimer >= EffectiveAttackInterval)
                {
                    AttackBlockedEnemies();
                    SpawnerHealth spawner = blocker.GetFirstBlockedSpawner();
                    if (spawner != null)
                    {
                        var (dmg, _) = CalculateDamage(null);
                        spawner.TakeDamage(dmg);
                        OnDamageDealt(dmg);
                    }
                    attackTimer = 0f;
                }
            }
        }
    }

    void AttackBlockedEnemies()
    {
        if (blocker == null) return;
        if (blocker.blockedEnemies.Count == 0) return;

        bool ignoreDefense = false;
        int penetration = GetPenetrationPercent();
        if (skillAttackAllBlocked)
        {
            for (int i = blocker.blockedEnemies.Count - 1; i >= 0; i--)
            {
                if (i < blocker.blockedEnemies.Count)
                {
                    var enemy = blocker.blockedEnemies[i];
                    if (enemy != null)
                    {
                        var (dmg, _) = CalculateDamage(enemy);
                        enemy.TakeDamage(dmg, ignoreDefense, penetration);
                        OnDamageDealt(dmg);
                    }
                }
            }
        }
        else
        {
            var first = blocker.blockedEnemies[0];
            if (first != null)
            {
                var (dmg, _) = CalculateDamage(first);
                first.TakeDamage(dmg, ignoreDefense, penetration);
                OnDamageDealt(dmg);
            }
        }
    }

    public void ResetStats() {
        int oldAtk = runtimeAttackDamage, oldDef = runtimeDefense, oldMaxHp = runtimeMaxHealth;
        runtimeAttackDamage = (int)data.attackDamage;
        runtimeAttackInterval = data.attackInterval;
        runtimeMaxHealth = (int)data.maxHealth;
        runtimeDefense = data != null ? data.defense : 0;

        runtimeAttackDamage += TalentEffectApplier.GetAttackBonus(data);
        runtimeDefense += TalentEffectApplier.GetDefenseBonus(data);
        runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * TalentEffectApplier.GetAttackPercent(data) / 100f);
        runtimeDefense += Mathf.RoundToInt(runtimeDefense * TalentEffectApplier.GetDefensePercent(data) / 100f);
        runtimeMaxHealth += Mathf.RoundToInt(runtimeMaxHealth * TalentEffectApplier.GetMaxHpPercentBonus(data) / 100f);
        runtimeAttackInterval /= TalentEffectApplier.GetAttackSpeedMultiplier(data);

        var bonus = GetComponent<OperatorStatBonus>();
        if (bonus != null)
        {
            runtimeAttackDamage += bonus.attackBonus;
            runtimeDefense += bonus.defenseBonus;
            runtimeMaxHealth += bonus.healthBonus;
            if (runtimeMaxHealth < 1) runtimeMaxHealth = 1;
            if (currentHealth > runtimeMaxHealth) currentHealth = runtimeMaxHealth;
        }
        if (currentHealth > runtimeMaxHealth) currentHealth = runtimeMaxHealth;
        
        string opId = data != null ? data.operatorName : name;
        
        
        
        
        UpdateUIState();
    }

    public int GetDeployCost()
    {
        int cost = data != null ? data.cost : 0;
        var bonus = GetComponent<OperatorStatBonus>();
        if (bonus != null) cost += bonus.GetDeployCostBonus();
        return cost < 0 ? 0 : cost;
    }
    
    void UpdateSkillState() {
        if (currentSkill == null) return;
        if (isSkillActive) {
            currentSkillTime -= Time.deltaTime;
            currentSkill.OnSkillUpdate();
            if (statusUI != null) {
                statusUI.UpdateMP(currentSkillTime, currentSkill.duration);
                statusUI.SetMPColor(Color.green);
            }
            if (currentSkillTime <= 0) EndSkill();
        } else {
            if (currentSP < currentSkill.maxSP) {
                float oldSP = currentSP;
                currentSP += Time.deltaTime;
                
                isSkillReady = false;
                if (statusUI != null) {
                    statusUI.UpdateMP(currentSP, currentSkill.maxSP);
                    statusUI.SetMPColor(Color.blue);
                }
            } else {
                currentSP = currentSkill.maxSP;
                isSkillReady = true;
                if (statusUI != null) statusUI.UpdateMP(1, 1);
                // 自动释放技能（明日方舟「自动回复」类）：技力充满立即触发，无需玩家点击
                if (currentSkill.autoActivate) StartSkill();
            }
        }
    }
    
    void InitUI(float maxSP) {
        if (statusUI != null) {
            UpdateUIState();
            statusUI.UpdateMP(currentSP, maxSP);
            statusUI.SetMPColor(Color.blue);
        }
    }
    
    public void UpdateUIState() {
        if (statusUI != null) statusUI.UpdateHP(currentHealth, runtimeMaxHealth);
    }
    
    public void OnClickedForSkill() {
        if (isSkillReady && !isSkillActive && currentSkill != null) StartSkill();
    }
    
    private void OnMouseDown() {
        OnClickedForSkill();
    }
    
    void StartSkill() {
        isSkillActive = true;
        isSkillReady = false;
        currentSkillTime = currentSkill.duration;
        currentSkill.OnSkillStart();
    }
    
    void EndSkill() {
        isSkillActive = false;
        float oldSP = currentSP;
        currentSP = 0f;
        
        currentSkill.OnSkillEnd();
        ResetStats();
        if (statusUI != null) {
            statusUI.SetMPColor(Color.blue);
            statusUI.UpdateMP(0, currentSkill.maxSP);
        }
    }
    
    public static int ApplyDefense(int rawDamage, int defense)
    {
        if (rawDamage <= 0) return 0;
        float reduction = Mathf.Min(0.99f, defense / 10000f);
        int final = Mathf.RoundToInt(rawDamage * (1f - reduction));
        return Mathf.Max(1, final);
    }

    /// <summary> 计算对目标敌人的实际伤害（含暴击、精英伤害加成、低血增伤等）。返回 (伤害值, 是否暴击)。 </summary>
    public (int damage, bool isCrit) CalculateDamage(Enemy2 enemy)
    {
        int damage = runtimeAttackDamage;

        // 低血增伤（HP < 50%）
        int lowHpBonus = TalentEffectApplier.GetLowHpAttackBonusPercent(data);
        if (lowHpBonus > 0 && currentHealth < runtimeMaxHealth * 0.5f)
        {
            damage += Mathf.RoundToInt(damage * lowHpBonus / 100f);
        }

        // 精英伤害加成
        if (enemy != null && enemy.isElite)
        {
            int eliteBonus = TalentEffectApplier.GetEliteDamageBonusPercent(data);
            if (eliteBonus > 0)
            {
                damage += Mathf.RoundToInt(damage * eliteBonus / 100f);
            }
        }

        // 暴击
        bool isCrit = false;
        int critChance = TalentEffectApplier.GetCritChancePercent(data);
        if (data != null) critChance += data.baseCritChance;
        if (critChance > 0 && Random.Range(0, 100) < critChance)
        {
            isCrit = true;
            int critDmgBonus = TalentEffectApplier.GetCritDamagePercent(data);
            float critMultiplier = 1.5f + critDmgBonus / 100f;
            damage = Mathf.RoundToInt(damage * critMultiplier);
        }

        return (damage, isCrit);
    }

    /// <summary> 造成伤害后处理吸血。 </summary>
    public void OnDamageDealt(int damageDealt)
    {
        int lifesteal = TalentEffectApplier.GetLifeStealPercent(data);
        if (lifesteal > 0 && damageDealt > 0)
        {
            int healAmount = Mathf.RoundToInt(damageDealt * lifesteal / 100f);
            if (healAmount > 0)
            {
                Heal(healAmount);
            }
        }
    }

    /// <summary> 获取当前穿透百分比。 </summary>
    public int GetPenetrationPercent()
    {
        return TalentEffectApplier.GetDefensePenetrationPercent(data);
    }

    public void TakeDamage(int damage, bool ignoreDefense = false)
    {
        int finalDamage = ignoreDefense ? damage : ApplyDefense(damage, runtimeDefense);
        int oldHealth = currentHealth;
        currentHealth -= finalDamage;
        
        
        UpdateUIState();
        if (currentHealth <= 0) Die();
    }
    
    public void Heal(int amount) {
        int oldHealth = currentHealth;
        currentHealth += amount;
        if (currentHealth > runtimeMaxHealth) currentHealth = runtimeMaxHealth;
        
        
        UpdateUIState();
    }
    
    void Die() {
        if(blocker != null) blocker.ReleaseAllEnemies();

        // skl_vanguard_laststand：先锋干员阵亡时自动回费
        if (RogueRuntimeState.HasVanguardDeathDPRefund &&
            data != null && data.opType == OperatorData.OperatorType.Vanguard)
        {
            if (currentSkill != null)
            {
                // 有技能：自动触发一次技能（如 Skill_DPBurst 即返还 dpBurst 部署点）
                StartSkill();
            }
            else
            {
                // 无技能：返还其撤退至守护点时的部署费用，与 DeploymentManager.RetreatOperator 一致
                if (DeploymentManager.Instance != null)
                {
                    int refund = deployCost > 0 ? deployCost : data.cost;
                    DeploymentManager.Instance.AddDP(refund);
                }
            }
        }

        Destroy(gameObject); 
    }
    
    public void MaximizeSP() {
        float oldSP = currentSP;
        if (currentSkill != null) currentSP = currentSkill.maxSP;
        else currentSP = maxSP;
        
        UpdateSkillState();
    }

    // ── 时光回溯：血量历史记录 ──
    private struct OperatorHealthSnapshot
    {
        public float timestamp;
        public int health;
    }

    private const float RewindHistoryDuration = 5f;
    private List<OperatorHealthSnapshot> _healthHistory = new List<OperatorHealthSnapshot>();

    private void LateUpdate()
    {
        float now = Time.time;
        _healthHistory.Add(new OperatorHealthSnapshot
        {
            timestamp = now,
            health = currentHealth
        });

        while (_healthHistory.Count > 0 && now - _healthHistory[0].timestamp > RewindHistoryDuration)
        {
            _healthHistory.RemoveAt(0);
        }
    }

    public void RewindHealthToSecondsAgo(float seconds)
    {
        if (_healthHistory.Count == 0) return;

        float targetTime = Time.time - seconds;
        int targetHealth = currentHealth;
        bool found = false;

        for (int i = 0; i < _healthHistory.Count; i++)
        {
            if (_healthHistory[i].timestamp >= targetTime)
            {
                if (i > 0)
                {
                    float t = (targetTime - _healthHistory[i - 1].timestamp) /
                              (_healthHistory[i].timestamp - _healthHistory[i - 1].timestamp);
                    if (float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
                    t = Mathf.Clamp01(t);
                    targetHealth = Mathf.RoundToInt(Mathf.Lerp(
                        _healthHistory[i - 1].health, _healthHistory[i].health, t));
                }
                else
                {
                    targetHealth = _healthHistory[i].health;
                }
                found = true;
                break;
            }
            targetHealth = _healthHistory[i].health;
        }

        if (!found && _healthHistory.Count > 0)
            targetHealth = _healthHistory[_healthHistory.Count - 1].health;

        currentHealth = Mathf.Clamp(targetHealth, 1, runtimeMaxHealth);
        UpdateUIState();

        _healthHistory.Clear();
    }

    public static void RewindAllOperatorsHealth(float seconds)
    {
        foreach (var op in AllOperators)
        {
            if (op != null && op.gameObject.activeInHierarchy)
            {
                op.RewindHealthToSecondsAgo(seconds);
            }
        }
    }
}
