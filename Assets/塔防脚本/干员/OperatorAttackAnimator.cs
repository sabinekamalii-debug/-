using UnityEngine;

/// <summary>
/// 只负责根据敌人左右方向，给 Animator 设置攻击相关参数。
/// 不改动 OperatorUnit 的任何逻辑，只是额外挂在同一个物体上。
/// 自动识别角色是否有左右攻击：
/// - 只有 IsFight：只控制进入/退出攻击，不区分左右
/// - 有 IsFight + Left/Right：会根据敌人位置自动切左右
/// 支持三种“进入战斗”的判定方式：
/// - 触发器模式：敌人碰撞到干员身上的触发器时 IsFight=true（适合近战）
/// - 攻击范围模式：敌人在攻击范围内即 IsFight=true（适合远程/光波）
/// - 友方范围模式：队友在治疗/攻击范围内即 IsFight=true（适合牧师等治疗）
/// </summary>
public class OperatorAttackAnimator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("角色的 Animator，若不指定会自动在子物体中查找")]
    public Animator animator;

    [Tooltip("角色本体，用于读取 OperatorData 等")]
    public OperatorUnit ownerUnit;

    [Header("设置")]
    [Tooltip("敌人的 Tag")]
    public string enemyTag = "Enemy";

    [Tooltip("勾选后：用攻击范围（圆形）检测敌人，敌人在范围内就播战斗动画；不勾选则用触发器碰撞判定（近战）")]
    public bool useAttackRangeForFight = false;

    [Tooltip("勾选后：用攻击范围检测队友（如牧师），队友在范围内就播 IsFight 治疗动画；友方干员需在 allyLayer（如 My）上")]
    public bool useAllyRangeForFight = false;

    [Tooltip("攻击范围半径。≤0 时自动从 OperatorData/各攻击组件 读取")]
    public float attackRange = 0f;

    [Tooltip("敌人所在 Layer，用于攻击范围检测（仅 useAttackRangeForFight 时使用）")]
    public LayerMask enemyLayer;

    [Tooltip("友方干员所在 Layer，用于治疗动画检测（仅 useAllyRangeForFight 时使用，请设为 My）")]
    public LayerMask allyLayer;

    [Tooltip("攻击/治疗范围检测间隔（秒），避免每帧 OverlapCircle")]
    public float rangeCheckInterval = 0.15f;

    // 当前在攻击范围里的敌人数（触发器模式用）
    private int enemyInRangeCount = 0;

    private Transform cachedTransform;
    private float rangeCheckTimer;
    private RangedAttacker rangedAttacker;
    private AoEAttacker aoeAttacker;
    private AreaHealer areaHealer;
    private readonly Collider2D[] rangeCheckBuffer = new Collider2D[16];

    // 动画参数名（可以在 Inspector 里改）
    [Header("动画参数名")]
    public string isFightParam = "IsFight";
    public string leftParam = "Left";
    public string rightParam = "Right";

    // 自动识别出来的结果
    private bool hasIsFight;
    private bool hasLeft;
    private bool hasRight;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (ownerUnit == null)
        {
            ownerUnit = GetComponent<OperatorUnit>();
        }

        cachedTransform = ownerUnit != null ? ownerUnit.transform : transform;
        rangedAttacker = GetComponent<RangedAttacker>();
        aoeAttacker = GetComponent<AoEAttacker>();
        areaHealer = GetComponent<AreaHealer>();

        // 有远程/光波时用攻击范围判定 IsFight；SpawnerContactAttacker 是纯碰撞近战，用触发器播攻击动画
        if (!useAttackRangeForFight && (rangedAttacker != null || aoeAttacker != null))
            useAttackRangeForFight = true;
        if (useAttackRangeForFight && enemyLayer == 0)
        {
            if (rangedAttacker != null && rangedAttacker.enemyLayer != 0)
                enemyLayer = rangedAttacker.enemyLayer;
            else if (aoeAttacker != null && aoeAttacker.enemyLayer != 0)
                enemyLayer = aoeAttacker.enemyLayer;
        }

        // 有治疗组件时自动用友方范围判定 IsFight（检测队友，如牧师）
        if (!useAllyRangeForFight && areaHealer != null)
            useAllyRangeForFight = true;
        if (useAllyRangeForFight && allyLayer == 0 && areaHealer != null && areaHealer.allyLayer != 0)
            allyLayer = areaHealer.allyLayer;

        CacheAnimatorParameters();
    }

    private float GetEffectiveAttackRange()
    {
        if (attackRange > 0f) return attackRange;
        if (rangedAttacker != null && rangedAttacker.range > 0f) return rangedAttacker.range;
        if (aoeAttacker != null && aoeAttacker.range > 0f) return aoeAttacker.range;
        if (areaHealer != null && areaHealer.range > 0f) return areaHealer.range;
        if (ownerUnit != null && ownerUnit.data != null && ownerUnit.data.attackRange > 0f)
            return ownerUnit.data.attackRange;
        return 0f;
    }

    private void Update()
    {
        bool useRange = useAttackRangeForFight || useAllyRangeForFight;
        if (!useRange) return;

        rangeCheckTimer -= Time.deltaTime;
        if (rangeCheckTimer > 0f) return;
        rangeCheckTimer = rangeCheckInterval;

        float range = GetEffectiveAttackRange();
        if (range <= 0f)
        {
            SetFight(false);
            return;
        }

        // 友方范围模式：牧师等，队友进入范围时播 IsFight
        if (useAllyRangeForFight)
        {
            bool allyInRange = CheckAllyInRange(range);
            SetFight(allyInRange);
            if (allyInRange)
            {
                Transform nearestAlly = GetNearestAllyInRange(range);
                if (nearestAlly != null) UpdateDirection(nearestAlly.position);
            }
            return;
        }

        // 敌人范围模式：远程/光波
        int count;
        if (enemyLayer != 0)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(enemyLayer);
            filter.useLayerMask = true;
            filter.useTriggers = true;
            count = Physics2D.OverlapCircle((Vector2)cachedTransform.position, range, filter, rangeCheckBuffer);
        }
        else
        {
            Collider2D[] allHits = Physics2D.OverlapCircleAll((Vector2)cachedTransform.position, range);
            count = allHits.Length;
            for (int i = 0; i < count && i < rangeCheckBuffer.Length; i++)
                rangeCheckBuffer[i] = allHits[i];
            if (count > rangeCheckBuffer.Length) count = rangeCheckBuffer.Length;
        }

        Transform nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (rangeCheckBuffer[i] == null) continue;
            if (!rangeCheckBuffer[i].CompareTag(enemyTag)) continue;

            float d = Vector2.SqrMagnitude(rangeCheckBuffer[i].transform.position - cachedTransform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = rangeCheckBuffer[i].transform;
            }
        }

        if (nearest != null)
        {
            UpdateDirection(nearest.position);
            SetFight(true);
        }
        else
        {
            SetFight(false);
        }
    }

    /// <summary> 检测攻击/治疗范围内是否有队友（不含自己），用于牧师 IsFight。 </summary>
    private bool CheckAllyInRange(float range)
    {
        if (allyLayer == 0) return false;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(allyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;
        int count = Physics2D.OverlapCircle((Vector2)cachedTransform.position, range, filter, rangeCheckBuffer);
        for (int i = 0; i < count; i++)
        {
            if (rangeCheckBuffer[i] == null) continue;
            OperatorUnit ally = rangeCheckBuffer[i].GetComponent<OperatorUnit>();
            if (ally != null && ally != ownerUnit) return true;
        }
        return false;
    }

    private Transform GetNearestAllyInRange(float range)
    {
        if (allyLayer == 0) return null;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(allyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;
        int count = Physics2D.OverlapCircle((Vector2)cachedTransform.position, range, filter, rangeCheckBuffer);
        Transform nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (rangeCheckBuffer[i] == null) continue;
            OperatorUnit ally = rangeCheckBuffer[i].GetComponent<OperatorUnit>();
            if (ally == null || ally == ownerUnit) continue;
            float d = Vector2.SqrMagnitude(rangeCheckBuffer[i].transform.position - cachedTransform.position);
            if (d < nearestDist) { nearestDist = d; nearest = rangeCheckBuffer[i].transform; }
        }
        return nearest;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (useAttackRangeForFight) return;
        if (!other.CompareTag(enemyTag)) return;

        enemyInRangeCount++;

        UpdateDirection(other.transform.position);
        SetFight(true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (useAttackRangeForFight) return;
        if (!other.CompareTag(enemyTag)) return;

        UpdateDirection(other.transform.position);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (useAttackRangeForFight) return;
        if (!other.CompareTag(enemyTag)) return;

        enemyInRangeCount--;
        if (enemyInRangeCount <= 0)
        {
            enemyInRangeCount = 0;
            SetFight(false);
        }
    }

    /// <summary>
    /// 根据敌人世界坐标判断在左还是右
    /// </summary>
    private void UpdateDirection(Vector3 enemyWorldPos)
    {
        if (animator == null || cachedTransform == null) return;
        if (!hasLeft && !hasRight) return; // 该角色没有左右攻击，直接忽略方向

        bool isRight = enemyWorldPos.x > cachedTransform.position.x;

        if (hasRight) animator.SetBool(rightParam, isRight);
        if (hasLeft) animator.SetBool(leftParam, !isRight);
    }

    /// <summary>
    /// 统一控制进入/退出战斗状态。技能期间（如金色防御）不显示战斗姿态，强制为 false。
    /// </summary>
    private void SetFight(bool isFight)
    {
        if (animator == null) return;
        if (ownerUnit != null && ownerUnit.skillPreventAttack && isFight)
            isFight = false;

        if (hasIsFight)
        {
            animator.SetBool(isFightParam, isFight);
        }

        if (!isFight && (hasLeft || hasRight))
        {
            // 离开战斗时清空左右状态，回到待机
            if (hasRight) animator.SetBool(rightParam, false);
            if (hasLeft) animator.SetBool(leftParam, false);
        }
    }

    /// <summary>
    /// 在 Awake 时缓存一下 Animator 里是否有这些参数，避免每帧查找。
    /// </summary>
    private void CacheAnimatorParameters()
    {
        hasIsFight = false;
        hasLeft = false;
        hasRight = false;

        if (animator == null) return;

        foreach (var p in animator.parameters)
        {
            if (!string.IsNullOrEmpty(isFightParam) && p.name == isFightParam)
                hasIsFight = true;
            if (!string.IsNullOrEmpty(leftParam) && p.name == leftParam)
                hasLeft = true;
            if (!string.IsNullOrEmpty(rightParam) && p.name == rightParam)
                hasRight = true;
        }

        // 如果角色只有一个简单攻击，一般只有 IsFight，没有 Left / Right。
        // 这种情况下，脚本只会控制 IsFight，不会去设左右。
    }

    /// <summary>
    /// 立即退出攻击状态（IsFight=false）。技能开始时调用，使正在攻击的角色立刻切回待机。
    /// </summary>
    public void ForceExitFightState()
    {
        SetFight(false);
    }

    /// <summary>
    /// 技能结束后调用：若仍阻挡敌人或范围内有敌人/队友，恢复 IsFight=true。
    /// 由 Skill_GoldenDefense 等在 OnSkillEnd 时调用。
    /// </summary>
    public void RefreshFightState()
    {
        if (ownerUnit == null || ownerUnit.skillPreventAttack) return;

        if (useAttackRangeForFight || useAllyRangeForFight)
        {
            rangeCheckTimer = 0f;
            return;
        }
        if (enemyInRangeCount > 0)
            SetFight(true);
    }
}
