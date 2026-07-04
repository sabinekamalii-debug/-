using UnityEngine;

/// <summary>
/// 远程攻击敌人专用：只攻击 My 图层的干员，且带优先级——
/// 攻击范围内若有「没有挂 AttackRangeVisualizer」的干员，则只能打他们；
/// 只有当他们都被消灭后，才可以攻击「挂了 AttackRangeVisualizer」的干员。
/// 可挂在敌人或刷怪点上，支持子弹或瞬发伤害。
/// 伤害、攻击间隔、攻击范围均从敌人数据（EnemyData2）读取；不填数据时用下方组件默认值。
/// </summary>
public class EnemyRangedAttacker : MonoBehaviour
{
    [Header("敌人数据（攻击间隔/范围/伤害均由此读取）")]
    [Tooltip("拖入敌人数据后，攻击间隔、范围、伤害从数据读取；不填则用下方默认值")]
    [SerializeField] private EnemyData2 enemyData;

    [Header("无敌人数据时的默认值")]
    [Tooltip("攻击半径（世界单位）")]
    public float attackRange = 4f;

    [Tooltip("每次攻击对干员造成的伤害数值")]
    public int attackDamage = 300;

    [Tooltip("是否无视干员防御")]
    public bool ignoreDefense = false;

    [Header("目标设置")]
    [Tooltip("干员所在图层（如 My），只攻击该图层上的物体")]
    public LayerMask operatorLayer;

    [Header("子弹与特效（可选）")]
    [Tooltip("子弹预制体，需挂 Projectile 脚本；不填则瞬发伤害")]
    public GameObject bulletPrefab;

    [Tooltip("子弹发射起点，不填则用本物体位置")]
    public Transform firePoint;

    private float _attackTimer;
    private Transform _currentTarget;
    private UnitStatusUI _statusUI;

    private float EffectiveAttackRange => enemyData != null ? enemyData.attackRange : attackRange;
    /// <summary> 供 EnemyFightAnimator 等读取：当前生效的攻击半径。 </summary>
    public float GetEffectiveAttackRange() => EffectiveAttackRange;
    private float EffectiveAttackInterval => enemyData != null && enemyData.attackInterval > 0f ? enemyData.attackInterval : 1f;
    private int EffectiveDamage => enemyData != null ? enemyData.damage : attackDamage;

    private void Awake()
    {
        _statusUI = GetComponentInChildren<UnitStatusUI>();
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        _attackTimer -= Time.deltaTime;

        _currentTarget = FindTargetWithPriority();

        // 有蓝条时显示攻击间隔冷却：条随冷却填满，满时攻击
        float interval = EffectiveAttackInterval;
        if (interval <= 0f) interval = 0.5f;
        if (_statusUI != null)
        {
            if (_currentTarget != null)
            {
                float current = Mathf.Clamp(interval - _attackTimer, 0f, interval);
                _statusUI.UpdateMP(current, interval);
            }
            else
                _statusUI.UpdateMP(0f, 1f);
        }

        if (_currentTarget == null) return;
        if (_attackTimer > 0f) return;

        _attackTimer = interval;
        PerformAttack(_currentTarget);
    }

    /// <summary>
    /// 在攻击范围内按优先级选目标：优先选「没有 AttackRangeVisualizer」的干员；
    /// 只有范围内没有这类干员时，才可选「有 AttackRangeVisualizer」的干员。
    /// </summary>
    private Transform FindTargetWithPriority()
    {
        float range = EffectiveAttackRange;
        if (range <= 0f) return null;
        if (operatorLayer == 0) return null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, operatorLayer);
        if (hits == null || hits.Length == 0) return null;

        System.Collections.Generic.List<OperatorUnit> withoutVisualizer = new System.Collections.Generic.List<OperatorUnit>();
        System.Collections.Generic.List<OperatorUnit> withVisualizer = new System.Collections.Generic.List<OperatorUnit>();

        foreach (var col in hits)
        {
            if (col == null) continue;
            OperatorUnit op = col.GetComponent<OperatorUnit>();
            if (op == null) op = col.GetComponentInParent<OperatorUnit>();
            if (op == null) continue;

            if (op.GetComponentInChildren<AttackRangeVisualizer>() != null)
                withVisualizer.Add(op);
            else
                withoutVisualizer.Add(op);
        }

        // 优先从「没有 AttackRangeVisualizer」的干员里选最近的
        if (withoutVisualizer.Count > 0)
            return GetNearest(withoutVisualizer);
        // 只有他们都没了，才从「有 AttackRangeVisualizer」的里选
        if (withVisualizer.Count > 0)
            return GetNearest(withVisualizer);

        return null;
    }

    private Transform GetNearest(System.Collections.Generic.List<OperatorUnit> list)
    {
        if (list == null || list.Count == 0) return null;
        Vector2 pos = transform.position;
        OperatorUnit nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var op in list)
        {
            if (op == null) continue;
            float d = Vector2.Distance(pos, op.transform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = op;
            }
        }
        return nearest != null ? nearest.transform : null;
    }

    private void PerformAttack(Transform target)
    {
        if (target == null) return;

        if (bulletPrefab != null)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            GameObject bulletGO = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            Projectile proj = bulletGO.GetComponent<Projectile>();
            if (proj != null)
                proj.Seek(target, EffectiveDamage, ignoreDefense);
            return;
        }

        OperatorUnit op = target.GetComponent<OperatorUnit>();
        if (op == null) op = target.GetComponentInParent<OperatorUnit>();
        if (op != null)
            op.TakeDamage(EffectiveDamage, ignoreDefense);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, EffectiveAttackRange);
    }
}
