using UnityEngine;

/// <summary>
/// 光波等范围群攻：攻击范围、攻速、攻击力均来自干员数据（OperatorData.attackRange / attackInterval / attackDamage）。
/// 仅敌人 Layer 需在 Inspector 配置。
/// </summary>
public class AoEAttacker : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("敌人所在的 LayerMask，与 RangedAttacker 的 Enemy Layer 一致即可。")]
    public LayerMask enemyLayer;

    private OperatorUnit unit;
    private float timer = 0f;
    private float _rangeOverride = -1f;

    /// <summary> 攻击范围：来自干员数据 attackRange；技能可临时改写，设为 ≤0 恢复用数据。 </summary>
    public float range
    {
        get => _rangeOverride > 0 ? _rangeOverride : (unit != null && unit.data != null ? unit.data.attackRange : 0f);
        set => _rangeOverride = value;
    }

    public void ClearRangeOverride() { _rangeOverride = -1f; }

    void Awake()
    {
        unit = GetComponent<OperatorUnit>();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (unit == null) return;
        if (unit.skillPreventAttack) return;

        float interval = unit.runtimeAttackInterval;
        if (interval <= 0f) return;

        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;

        PerformAoEAttack();
    }

    private void PerformAoEAttack()
    {
        float r = range;
        if (r <= 0f) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, r, enemyLayer);
        if (hits == null || hits.Length == 0) return;

        int damage = unit.runtimeAttackDamage;
        bool ignoreDefense = unit.GetComponent<IgnoreDefenseAttacker>() != null;
        for (int i = 0; i < hits.Length; i++)
        {
            Enemy2 enemy = hits[i].GetComponent<Enemy2>();
            if (enemy != null) enemy.TakeDamage(damage, ignoreDefense);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float r = range;
        if (r <= 0f)
        {
            OperatorUnit op = GetComponent<OperatorUnit>();
            if (op != null && op.data != null) r = op.data.attackRange;
        }
        if (r > 0f) Gizmos.DrawWireSphere(transform.position, r);
    }
}
