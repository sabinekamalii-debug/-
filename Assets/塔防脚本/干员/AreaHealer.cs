using UnityEngine;

/// <summary>
/// 牧师等治疗干员用的范围治疗脚本：
/// - 治疗量 = 干员数据里的攻击伤害（OperatorData.attackDamage → runtimeAttackDamage），技能加攻会同步加治疗量。
/// - 治疗间隔 = 干员数据的 attackInterval（runtimeAttackInterval）。
/// - 治疗范围 = 干员数据的 attackRange。
/// </summary>
public class AreaHealer : MonoBehaviour
{
    [Header("目标筛选")]
    [Tooltip("友军所在的 Layer，请设为 My（与友方干员图层一致），未设置时脚本会尝试自动使用 My 层。")]
    public LayerMask allyLayer;

    private OperatorUnit unit;
    private float timer = 0f;
    private float _rangeOverride = -1f;

    /// <summary> 治疗范围：技能可临时改写；否则来自干员数据 attackRange。设为 ≤0 可恢复为用数据。 </summary>
    public float range
    {
        get => _rangeOverride > 0 ? _rangeOverride : ((unit != null && unit.data != null) ? unit.data.attackRange : 0f);
        set => _rangeOverride = value;
    }

    public void ClearRangeOverride() { _rangeOverride = -1f; }

    void Awake()
    {
        unit = GetComponent<OperatorUnit>();

        if (allyLayer == 0)
        {
            int myLayer = LayerMask.NameToLayer("My");
            if (myLayer >= 0) allyLayer = 1 << myLayer;
        }
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

        PerformHeal();
    }

    private void PerformHeal()
    {
        float r = range;
        if (r <= 0f) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, r, allyLayer);
        if (hits == null || hits.Length == 0) return;

        // 治疗量 = 干员数据里的攻击伤害（转为治疗量）
        int healAmount = unit.runtimeAttackDamage;
        for (int i = 0; i < hits.Length; i++)
        {
            OperatorUnit ally = hits[i].GetComponent<OperatorUnit>();
            if (ally != null)
                ally.Heal(healAmount);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        float r = range;
        if (r > 0f) Gizmos.DrawWireSphere(transform.position, r);
    }
}
