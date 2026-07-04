using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌方角色通用脚本：干员在攻击范围内或与敌人碰撞时 IsFight 为 true，播放攻击动画。
/// 支持两种模式：① 碰撞体接触（近战） ② 攻击范围圆形检测（远程，如火之魔王）。
/// 若某敌人尚未做攻击动画或 Animator 中没有 IsFight 参数，脚本会安全跳过，不会报错。
/// 使用：挂在敌人根物体或带 Animator 的物体上；远程怪勾选「使用攻击范围」并设好干员图层。
/// </summary>
public class EnemyFightAnimator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("敌方角色的 Animator，不填则自动在自身及子物体中查找")]
    public Animator animator;

    [Header("动画参数名（与 Animator Controller 中一致）")]
    [Tooltip("进入/退出战斗时设置的布尔参数")]
    public string isFightParam = "IsFight";

    [Tooltip("可选：攻击方向在左时为 true")]
    public string leftParam = "Left";

    [Tooltip("可选：攻击方向在右时为 true")]
    public string rightParam = "Right";

    [Header("我方干员识别")]
    [Tooltip("我方干员所在图层（请设为 My）；碰撞/范围检测都用到")]
    public LayerMask operatorLayer;

    [Tooltip("可选：用于识别干员的 Tag，不填则用 operatorLayer + OperatorUnit 识别")]
    public string operatorTag = "";

    [Header("可选：攻击范围（远程怪如火之魔王）")]
    [Tooltip("勾选后：用圆形范围检测干员，有干员在范围内则 IsFight=true。不勾则仅靠碰撞体接触。")]
    public bool useAttackRange = false;
    [Tooltip("攻击半径（世界单位）。留 0 则从同物体上的 Enemy2 的角色数据（EnemyData2.attackRange）读取")]
    public float attackRangeOverride = 0f;

    // 当前在碰撞中且「可被攻击」的干员集合（已上高台的干员不加入，敌人不攻击高台）
    private readonly HashSet<OperatorUnit> _countedOperators = new HashSet<OperatorUnit>();
    // 使用攻击范围时，由 Update 每帧写入
    private bool _fightFromRange;

    // 缓存 Animator 是否拥有对应参数，避免每帧查找
    private bool _hasIsFight;
    private bool _hasLeft;
    private bool _hasRight;
    private Transform _cachedTransform;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _cachedTransform = transform;
        CacheAnimatorParameters();
    }

    private void Update()
    {
        if (!useAttackRange) return;

        float range = GetAttackRange();
        if (range <= 0f)
        {
            _fightFromRange = false;
            SetFight(false);
            return;
        }

        if (operatorLayer == 0) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_cachedTransform.position, range, operatorLayer);
        bool anyValid = false;
        Vector3 nearestPos = _cachedTransform.position;
        float nearestSq = float.MaxValue;

        for (int i = 0; i < (hits?.Length ?? 0); i++)
        {
            if (hits[i] == null) continue;
            var op = GetOperator(hits[i]);
            if (op == null || op.IsStandingOnHighGround()) continue;
            anyValid = true;
            float sq = (op.transform.position - _cachedTransform.position).sqrMagnitude;
            if (sq < nearestSq) { nearestSq = sq; nearestPos = op.transform.position; }
        }

        if (anyValid) UpdateDirection(nearestPos);
        _fightFromRange = anyValid;
        SetFight(anyValid);
    }

    private float GetAttackRange()
    {
        if (attackRangeOverride > 0f) return attackRangeOverride;
        var e2 = GetComponent<Enemy2>();
        if (e2 != null)
        {
            float fromData = e2.GetAttackRangeFromData();
            if (fromData > 0f) return fromData;
        }
        var ra = GetComponent<EnemyRangedAttacker>();
        if (ra != null) return ra.GetEffectiveAttackRange();
        return 0f;
    }

    private void CacheAnimatorParameters()
    {
        _hasIsFight = false;
        _hasLeft = false;
        _hasRight = false;

        if (animator == null) return;

        foreach (var p in animator.parameters)
        {
            if (!string.IsNullOrEmpty(isFightParam) && p.name == isFightParam)
                _hasIsFight = true;
            if (!string.IsNullOrEmpty(leftParam) && p.name == leftParam)
                _hasLeft = true;
            if (!string.IsNullOrEmpty(rightParam) && p.name == rightParam)
                _hasRight = true;
        }
    }

    /// <summary> 碰撞体是否为我方干员（My 图层 + OperatorUnit，或符合 operatorTag）。 </summary>
    private bool IsOperator(Collider2D other)
    {
        if (other == null) return false;

        if (operatorLayer != 0 && ((1 << other.gameObject.layer) & operatorLayer) == 0)
            return false;

        if (!string.IsNullOrEmpty(operatorTag) && other.CompareTag(operatorTag))
            return true;

        return other.GetComponent<OperatorUnit>() != null || other.GetComponentInParent<OperatorUnit>() != null;
    }

    /// <summary> 获取碰撞体对应的干员（可能为 null）。 </summary>
    private OperatorUnit GetOperator(Collider2D other)
    {
        if (other == null) return null;
        var op = other.GetComponent<OperatorUnit>();
        if (op != null) return op;
        return other.GetComponentInParent<OperatorUnit>();
    }

    /// <summary> 是否为「可被攻击」的干员：是干员且未站在高台上（高台干员不触发敌人攻击）。 </summary>
    private bool IsValidFightTarget(Collider2D other)
    {
        if (!IsOperator(other)) return false;
        OperatorUnit op = GetOperator(other);
        return op != null && !op.IsStandingOnHighGround();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (useAttackRange) return; // 远程模式由 Update 范围检测驱动，不依赖碰撞
        if (!IsOperator(other)) return;
        OperatorUnit op = GetOperator(other);
        if (op == null || op.IsStandingOnHighGround()) return;

        _countedOperators.Add(op);
        UpdateDirection(other.transform.position);
        SetFight(_countedOperators.Count > 0);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (useAttackRange) return;
        if (!IsOperator(other)) return;
        OperatorUnit op = GetOperator(other);
        if (op == null) return;

        if (op.IsStandingOnHighGround())
            _countedOperators.Remove(op);
        else
        {
            _countedOperators.Add(op);
            UpdateDirection(other.transform.position);
        }
        SetFight(_countedOperators.Count > 0);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (useAttackRange) return;
        if (!IsOperator(other)) return;
        OperatorUnit op = GetOperator(other);
        if (op != null)
            _countedOperators.Remove(op);
        SetFight(_countedOperators.Count > 0);
    }

    private void UpdateDirection(Vector3 operatorWorldPos)
    {
        if (animator == null || _cachedTransform == null) return;
        if (!_hasLeft && !_hasRight) return;

        bool isRight = operatorWorldPos.x > _cachedTransform.position.x;
        if (_hasRight) animator.SetBool(rightParam, isRight);
        if (_hasLeft) animator.SetBool(leftParam, !isRight);
    }

    private void SetFight(bool isFight)
    {
        if (animator == null) return;
        if (!_hasIsFight) return;

        animator.SetBool(isFightParam, isFight);

        if (!isFight && (_hasLeft || _hasRight))
        {
            if (_hasRight) animator.SetBool(rightParam, false);
            if (_hasLeft) animator.SetBool(leftParam, false);
        }
    }

    /// <summary> 当前是否处于战斗状态（干员在攻击范围内或碰撞中）。 </summary>
    public bool IsFight => _countedOperators.Count > 0 || _fightFromRange;
}
