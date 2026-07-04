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

    public bool IsEvading() => !chooseToFight && isMoving;

    private SpriteRenderer spriteRenderer;
    private int originalSortingOrder;

    private UnitStatusUI statusUI;
    [HideInInspector] public int currentHealth;
    [HideInInspector] public int runtimeMaxHealth;
    [HideInInspector] public int runtimeAttackDamage;
    [HideInInspector] public int runtimeDefense;
    [HideInInspector] public float runtimeAttackInterval;
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

        if (blocker != null) currentBlockCount = blocker.maxBlockCount;
        else currentBlockCount = 1;

        if (data != null)
        {
            runtimeMaxHealth = (int)data.maxHealth;
            currentHealth = runtimeMaxHealth;
            runtimeAttackDamage = (int)data.attackDamage;
            runtimeAttackInterval = data.attackInterval;
            runtimeDefense = data.defense;

            runtimeAttackDamage += TalentEffectApplier.GetGlobalAttackBonus();
            runtimeDefense += TalentEffectApplier.GetGlobalDefenseBonus();
        }
        else
            runtimeDefense = 0;
        
        if (statusUI != null) statusUI.UpdateHP(currentHealth, runtimeMaxHealth);

        if (currentSkill == null) currentSkill = GetComponent<OperatorSkill>();
        if (currentSkill != null)
        {
            currentSkill.Initialize(this);
            maxSP = currentSkill.maxSP;
            InitUI(maxSP);
        }
        else InitUI(1);

        TryRegisterPrePlacedPosition();
    }

    public void SyncRuntimeFromData()
    {
        if (data == null) return;
        runtimeMaxHealth = (int)data.maxHealth;
        currentHealth = runtimeMaxHealth;
        runtimeAttackDamage = (int)data.attackDamage;
        runtimeAttackInterval = data.attackInterval;
        runtimeDefense = data.defense;

        runtimeAttackDamage += TalentEffectApplier.GetGlobalAttackBonus();
        runtimeDefense += TalentEffectApplier.GetGlobalDefenseBonus();

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
            float moveSpeed = (brain != null) ? brain.moveSpeed : 1f;
            transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, currentWaypoint) < 0.05f)
            {
                targetIndex++;
            }
            yield return null;
        }

        isMoving = false;
        OnArriveDestination();
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
        if (currentBlockCount <= 0) return;
        if (isEncountering) return;
        if (_suppressEncounterUntilExit) return;
        if (!other.CompareTag("Enemy")) return;

        Enemy2 enemy = other.GetComponent<Enemy2>();
        if (enemy == null) return;
        if (IsCellOccupiedByStandingOperator(transform.position, this))
            return;

        if (blocker != null)
        {
            for (int i = blocker.blockedEnemies.Count - 1; i >= 0; i--)
            {
                if (blocker.blockedEnemies[i] == null) blocker.blockedEnemies.RemoveAt(i);
            }

            if (chooseToFight && blocker.blockedEnemies.Count > 0)
                return;

            if (blocker.blockedEnemies.Count >= blocker.maxBlockCount)
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
                if (attackTimer >= runtimeAttackInterval)
                {
                    AttackBlockedEnemies();
                    SpawnerHealth spawner = blocker.GetFirstBlockedSpawner();
                    if (spawner != null)
                        spawner.TakeDamage(runtimeAttackDamage);
                    attackTimer = 0f;
                }
            }
        }
    }

    void AttackBlockedEnemies()
    {
        if (blocker == null) return;
        if (blocker.blockedEnemies.Count == 0) return;

        bool ignoreDefense = GetComponent<IgnoreDefenseAttacker>() != null;
        if (skillAttackAllBlocked)
        {
            for (int i = blocker.blockedEnemies.Count - 1; i >= 0; i--)
            {
                if (i < blocker.blockedEnemies.Count)
                {
                    var enemy = blocker.blockedEnemies[i];
                    if (enemy != null) enemy.TakeDamage(runtimeAttackDamage, ignoreDefense);
                }
            }
        }
        else
        {
            var first = blocker.blockedEnemies[0];
            if (first != null) first.TakeDamage(runtimeAttackDamage, ignoreDefense);
        }
    }

    public void ResetStats() {
        runtimeAttackDamage = (int)data.attackDamage;
        runtimeAttackInterval = data.attackInterval;
        runtimeMaxHealth = (int)data.maxHealth;
        runtimeDefense = data != null ? data.defense : 0;

        runtimeAttackDamage += TalentEffectApplier.GetGlobalAttackBonus();
        runtimeDefense += TalentEffectApplier.GetGlobalDefenseBonus();

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

    public void TakeDamage(int damage, bool ignoreDefense = false)
    {
        int finalDamage = ignoreDefense ? damage : ApplyDefense(damage, runtimeDefense);
        currentHealth -= finalDamage;
        
        UpdateUIState();
        if (currentHealth <= 0) Die();
    }
    
    public void Heal(int amount) {
        currentHealth += amount;
        if (currentHealth > runtimeMaxHealth) currentHealth = runtimeMaxHealth;
        
        UpdateUIState();
    }
    
    void Die() {
        if(blocker != null) blocker.ReleaseAllEnemies();
        Destroy(gameObject); 
    }
    
    public void MaximizeSP() {
        if (currentSkill != null) currentSP = currentSkill.maxSP;
        else currentSP = maxSP;
        UpdateSkillState();
    }
}
