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

    // ── 升星养成（局内）──
    /// <summary>是否处于满星（=data.maxStarRating）并因此激活职业被动。</summary>
    public bool starPassiveActive { get; private set; }
    /// <summary>满星数值型被动提供的额外攻击百分比（近卫专注强化等）。</summary>
    private int _starPassiveAtkPercent = 0;
    /// <summary>满星数值型被动提供的额外防御百分比（重装额外减伤等）。</summary>
    private int _starPassiveDefPercent = 0;
    /// <summary>重装满星「致命伤保 1 血」本实例是否已触发过（每场限一次）。</summary>
    private bool _defenderLastStandUsed = false;

    [HideInInspector] public bool skillPreventAttack = false;

    [HideInInspector] public bool skillAttackAllBlocked = false;

    [HideInInspector] public int deployCost = 0;

    private bool isEncountering = false;
    private bool chooseToFight = false;
    private bool _suppressEncounterUntilExit = false;
    private bool _pendingEvadeContactDamage = false;
    private bool _isVanguardReturning = false; // 先锋专属「返回」：正在返回守护点途中
    private bool _avoidAllEncounters = false;  // 「一路避让」：后续遇到敌人直接避让，不再弹菜单/不暂停

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
            ApplyStarPassive();
            ResetStats();
            currentHealth = runtimeMaxHealth;
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

    // ═══════════════════════════════════════════════════════════
    //  升星养成：星级属性倍率 + 满星职业被动
    // ═══════════════════════════════════════════════════════════

    /// <summary>当前干员星级（从 OperatorStarRegistry 查询，未登记返回 1）。</summary>
    public int CurrentStar =>
        (data != null && OperatorStarRegistry.IsRunActive) ? OperatorStarRegistry.GetStar(data.RegistryKey) : 1;

    /// <summary>是否满星（=data.maxStarRating）。满星解锁职业专属被动。</summary>
    public bool IsAtMaxStar()
    {
        if (data == null) return false;
        int star = CurrentStar;
        return star >= data.maxStarRating && data.maxStarRating > 0;
    }

    /// <summary>
    /// 局内升星后调用：重算属性并按新星级刷新满星被动。
    /// 已部署在场上的干员即时变强（最大生命提升时按比例补当前血量，
    /// 避免升星瞬间「血条变长但血量没跟上」的观感落差）。
    /// </summary>
    public void RefreshStarState()
    {
        if (data == null) return;
        int oldMax = runtimeMaxHealth;
        int oldCur = currentHealth;

        ApplyStarPassive();
        ResetStats();

        if (runtimeMaxHealth > oldMax && oldMax > 0)
        {
            // 按原血量比例放大，保持「受伤程度」不变
            float ratio = Mathf.Clamp01((float)oldCur / oldMax);
            currentHealth = Mathf.Max(1, Mathf.RoundToInt(runtimeMaxHealth * ratio));
        }
        if (currentHealth > runtimeMaxHealth) currentHealth = runtimeMaxHealth;

        UpdateUIState();
    }

    /// <summary>
    /// 星级属性倍率 = BaseStatMultiplier[maxStar] × StarGrowth[star]（对齐 03-干员玩法设计 §6.2）。
    /// 例：近卫 maxStar=5, ★5 → 1.6 × 3.0 = 4.8（相对自身★1=1.6×1.0，翻 3 倍）。
    /// 1 星时倍率=BaseStatMultiplier[maxStar]×1.0（与未接入养成的历史数值同级，不破坏平衡）。
    /// </summary>
    private float StarStatMultiplier()
    {
        if (data == null) return 1f;
        int star = CurrentStar;
        int maxStar = data.maxStarRating > 0 ? data.maxStarRating : 1;
        int idxMax = Mathf.Clamp(maxStar, 1, BalanceConfig.BaseStatMultiplier.Length - 1);
        int idxStar = Mathf.Clamp(star, 1, BalanceConfig.StarGrowth.Length - 1);
        return BalanceConfig.BaseStatMultiplier[idxMax] * BalanceConfig.StarGrowth[idxStar];
    }

    /// <summary>
    /// 部署时调用：根据是否满星激活职业专属被动（7 个）。
    /// 触发型被动（先锋回费 / 重装致命伤保 1 血 / 狙击暴击等）在对应逻辑点读取对应标志位生效；
    /// 数值型被动（近卫专注强化 / 重装额外减伤）在此写入 _starPassiveAtkPercent/_starPassiveDefPercent，
    /// 由 SyncRuntimeFromData/ResetStats 在属性重算时叠加。
    /// </summary>
    private void ApplyStarPassive()
    {
        _starPassiveAtkPercent = 0;
        _starPassiveDefPercent = 0;
        starPassiveActive = IsAtMaxStar();
        if (!starPassiveActive || data == null) return;

        switch (data.opType)
        {
            case OperatorData.OperatorType.Vanguard:    // ① 先锋：部署即返还部署费用 + 技力回复加速（均为触发型）
                break;                                  // 见 OnDeployed() 与 StarPassiveVanguardSPRate
            case OperatorData.OperatorType.Guard:       // ② 近卫：专注强化（攻击+30%）
                _starPassiveAtkPercent += 30;
                break;
            case OperatorData.OperatorType.Defender:    // ③ 重装：额外减伤（防御+40%）+ 致命伤保 1 血（触发型）
                _starPassiveDefPercent += 40;
                break;
            case OperatorData.OperatorType.Sniper:     // ④ 狙击：暴击率+25%（触发型，CalculateDamage 读取）
                break;
            case OperatorData.OperatorType.Caster:      // ⑤ 术师：法术穿透（无视 50% 防御，见 GetPenetrationPercent）+ 攻击+20%
                _starPassiveAtkPercent += 20;
                break;
            case OperatorData.OperatorType.Medic:      // ⑥ 医疗：治疗量+30%（触发型，Heal 读取）
                break;
            case OperatorData.OperatorType.Specialist: // ⑦ 特种：再部署时间-50%（触发型，撤退读取）
                break;
        }
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
        float starMul = StarStatMultiplier();
        runtimeMaxHealth = Mathf.RoundToInt(data.maxHealth * starMul);
        currentHealth = runtimeMaxHealth;
        runtimeAttackDamage = Mathf.RoundToInt(data.attackDamage * starMul);
        // 攻击间隔不随星级缩放：星级成长体现在攻击力/生命/防御上。
        // （若乘 starMul 会让升星后攻击变慢，与「越养越强」相悖）
        runtimeAttackInterval = data.attackInterval;
        runtimeDefense = Mathf.RoundToInt(data.defense * starMul);

        runtimeAttackDamage += TalentEffectApplier.GetAttackBonus(data);
        runtimeDefense += TalentEffectApplier.GetDefenseBonus(data);
        runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * TalentEffectApplier.GetAttackPercent(data) / 100f);
        runtimeDefense += Mathf.RoundToInt(runtimeDefense * TalentEffectApplier.GetDefensePercent(data) / 100f);
        runtimeMaxHealth += Mathf.RoundToInt(runtimeMaxHealth * TalentEffectApplier.GetMaxHpPercentBonus(data) / 100f);
        runtimeAttackInterval /= TalentEffectApplier.GetAttackSpeedMultiplier(data);

        // 满星数值型被动（如近卫专注强化、重装额外减伤）在属性重算时叠加
        if (IsAtMaxStar())
        {
            runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * _starPassiveAtkPercent / 100f);
            runtimeDefense += Mathf.RoundToInt(runtimeDefense * _starPassiveDefPercent / 100f);
        }

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

        // 先锋「返回」：到达守护点，发放大量部署点奖励
        if (_isVanguardReturning)
        {
            if (DeploymentManager.Instance != null)
                DeploymentManager.Instance.AddDP(VanguardReturnDPBonus);
            // 先锋「收集完情报就走」：返回后，当前已标记的敌人受到额外 +10% 增伤（全局叠加，可多先锋累计）
            Enemy2.AddVanguardReturnReconBonus();
            // 释放阻挡 + 撤退（销毁）
            if (blocker != null) blocker.ReleaseAllEnemies();
            Destroy(gameObject);
            return;
        }

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
        if (chooseToFight) return; // 已选「战斗」：后续敌人自动战斗，不再弹窗
        if (_isVanguardReturning) return; // 先锋正在返回守护点，不再触发遭遇
        if (!other.CompareTag("Enemy")) return;

        Enemy2 enemy = other.GetComponent<Enemy2>();
        if (enemy == null) return;
        if (IsCellOccupiedByStandingOperator(transform.position, this))
            return;

        // 「一路避让」：直接默认避让，不弹遭遇菜单、不暂停游戏，干员继续移动不停下
        if (_avoidAllEncounters)
        {
            ResolveEncounter(false);
            return;
        }

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

    /// <summary>
    /// 开启「一路避让」：后续遇到敌人直接默认避让，不再弹出遭遇战菜单、不暂停游戏。
    /// 生效到干员撤退/阵亡（重新部署新实例时自然重置）。
    /// </summary>
    public void EnableAvoidAllEncounters()
    {
        _avoidAllEncounters = true;
    }

    /// <summary>
    /// 通用遭遇选项「返回守护点」：干员开始返回守护点，到达后获得部署点奖励。
    /// 返回途中不阻挡/攻击敌人、不再触发遭遇菜单。此前为先锋专属，现对所有干员开放。
    /// </summary>
    public void ReturnToGuardPoint()
    {
        _isVanguardReturning = true;
        chooseToFight = false;
        isEncountering = false;
        _suppressEncounterUntilExit = true;

        // 释放当前阻挡的敌人，返回途中不阻挡
        if (blocker != null)
        {
            blocker.ReleaseAllEnemies();
            currentBlockCount = 0;
        }

        // 开始向守护点移动
        if (DeploymentManager.Instance != null && DeploymentManager.Instance.basePoint != null)
        {
            MoveToDestination(DeploymentManager.Instance.basePoint.position);
        }
        else
        {
            // 找不到守护点则直接撤退（兜底）
            if (DeploymentManager.Instance != null)
                DeploymentManager.Instance.AddDP(VanguardReturnDPBonus);
            Destroy(gameObject);
        }
    }

    /// <summary>先锋「返回」到达守护点后奖励的部署点数量</summary>
    private const int VanguardReturnDPBonus = 30;

    /// <summary>当前是否正在执行先锋返回（供外部查询）</summary>
    public bool IsVanguardReturning => _isVanguardReturning;

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
        if (data == null) return;
        float starMul = StarStatMultiplier();
        runtimeAttackDamage = Mathf.RoundToInt(data.attackDamage * starMul);
        // 攻击间隔不随星级缩放（见 SyncRuntimeFromData 说明）
        runtimeAttackInterval = data.attackInterval;
        runtimeMaxHealth = Mathf.RoundToInt(data.maxHealth * starMul);
        runtimeDefense = Mathf.RoundToInt(data.defense * starMul);

        runtimeAttackDamage += TalentEffectApplier.GetAttackBonus(data);
        runtimeDefense += TalentEffectApplier.GetDefenseBonus(data);
        runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * TalentEffectApplier.GetAttackPercent(data) / 100f);
        runtimeDefense += Mathf.RoundToInt(runtimeDefense * TalentEffectApplier.GetDefensePercent(data) / 100f);
        runtimeMaxHealth += Mathf.RoundToInt(runtimeMaxHealth * TalentEffectApplier.GetMaxHpPercentBonus(data) / 100f);
        runtimeAttackInterval /= TalentEffectApplier.GetAttackSpeedMultiplier(data);

        // 满星数值型被动叠加
        if (IsAtMaxStar())
        {
            runtimeAttackDamage += Mathf.RoundToInt(runtimeAttackDamage * _starPassiveAtkPercent / 100f);
            runtimeDefense += Mathf.RoundToInt(runtimeDefense * _starPassiveDefPercent / 100f);
        }

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

        UpdateUIState();
    }

    public int GetDeployCost()
    {
        int cost = data != null ? data.cost : 0;
        var bonus = GetComponent<OperatorStatBonus>();
        if (bonus != null) cost += bonus.GetDeployCostBonus();
        return cost < 0 ? 0 : cost;
    }

    // ── 满星触发型被动的外部接口（供 DeploymentManager 等调用）──

    /// <summary>⑦ 特种满星被动：再部署时间减免比例（0.5 表示 -50%）。非特种满星返回 0。</summary>
    public float StarPassiveSpecialistRedeployReduction =>
        (starPassiveActive && data != null && data.opType == OperatorData.OperatorType.Specialist) ? 0.5f : 0f;

    /// <summary>
    /// ① 先锋满星被动（后半）：技力回复速率倍率（1.3 表示技力回复 +30%）。
    /// 非先锋满星返回 1（不影响原速率）。由 UpdateSkillState 的技力累积读取。
    /// </summary>
    public float StarPassiveVanguardSPRate =>
        (starPassiveActive && data != null && data.opType == OperatorData.OperatorType.Vanguard) ? 1.3f : 1f;

    /// <summary>
    /// 部署到战场时由 DeploymentManager 调用。处理满星触发型被动：
    /// ① 先锋满星「部署即返还部署费用」（鼓励高频轮换先锋）。
    /// </summary>
    public void OnDeployed()
    {
        if (!starPassiveActive || data == null) return;
        if (data.opType == OperatorData.OperatorType.Vanguard && DeploymentManager.Instance != null)
        {
            DeploymentManager.Instance.AddDP(deployCost > 0 ? deployCost : data.cost);
        }
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
                // 先锋满星被动：技力回复 +30%
                currentSP += Time.deltaTime * StarPassiveVanguardSPRate;
                
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
        // ④ 狙击满星被动：暴击率 +25%
        if (starPassiveActive && data != null &&
            data.opType == OperatorData.OperatorType.Sniper)
            critChance += 25;
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
        int pen = TalentEffectApplier.GetDefensePenetrationPercent(data);

        // ⑤ 术师满星被动「法术穿透」：无视目标 50% 防御。
        // 所有出手路径（近战/远程/AoE/接触）都经由本方法取穿透值，故只需在此叠加。
        if (starPassiveActive && data != null &&
            data.opType == OperatorData.OperatorType.Caster)
            pen += 50;

        return Mathf.Clamp(pen, 0, 100);
    }

    public void TakeDamage(int damage, bool ignoreDefense = false)
    {
        int finalDamage = ignoreDefense ? damage : ApplyDefense(damage, runtimeDefense);
        int oldHealth = currentHealth;
        currentHealth -= finalDamage;

        // ③ 重装满星被动「致命伤保 1 血」：本实例每场仅触发一次，避免无限续命破坏平衡。
        if (currentHealth <= 0 && starPassiveActive && data != null &&
            data.opType == OperatorData.OperatorType.Defender && !_defenderLastStandUsed)
        {
            _defenderLastStandUsed = true;
            currentHealth = 1;
        }

        UpdateUIState();
        if (currentHealth <= 0) Die();
    }
    
    public void Heal(int amount) {
        // ⑥ 医疗满星被动：治疗量 +30%
        if (starPassiveActive && data != null && data.opType == OperatorData.OperatorType.Medic)
            amount = Mathf.RoundToInt(amount * 1.3f);
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
