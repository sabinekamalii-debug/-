using UnityEngine;

/// <summary>
/// 攻击范围扩大技能：持续时间内将干员的攻击/治疗范围扩大为原来的 1.5 倍。
/// - 仅对有攻击范围的干员生效（会检测 RangedAttacker、AoEAttacker、AreaHealer 的 range，有则扩大并还原）。
/// - 没有挂上述组件或 range 为 0 的干员不适用（技能可挂但释放时无范围变化）。
/// - 冷却：需 20 点技力才能释放（约 20 秒攒满）；持续 30 秒。
/// </summary>
public class Skill_RangeExpand : OperatorSkill
{
    [Header("技能参数")]
    [Tooltip("范围倍率，例如 1.5 表示攻击/治疗范围变为原来的 1.5 倍")]
    public float rangeMultiplier = 1.5f;

    private RangedAttacker rangedAttacker;
    private AoEAttacker aoeAttacker;
    private AreaHealer areaHealer;

    void Awake()
    {
        // Inspector 未手动配置（值为 0）时使用默认值；填了就尊重面板设置。
        if (duration <= 0f) duration = 30f; // 默认持续 30 秒
        if (maxSP   <= 0f) maxSP   = 20f;   // 默认需要 20 点技力
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);
        if (owner == null) return;
        rangedAttacker = owner.GetComponent<RangedAttacker>();
        aoeAttacker = owner.GetComponent<AoEAttacker>();
        areaHealer = owner.GetComponent<AreaHealer>();
    }

    public override void OnSkillStart()
    {
        if (rangedAttacker != null && rangedAttacker.range > 0f)
            rangedAttacker.range = rangedAttacker.range * rangeMultiplier;
        if (aoeAttacker != null && aoeAttacker.range > 0f)
            aoeAttacker.range = aoeAttacker.range * rangeMultiplier;
        if (areaHealer != null && areaHealer.range > 0f)
            areaHealer.range = areaHealer.range * rangeMultiplier;
    }

    public override void OnSkillEnd()
    {
        if (rangedAttacker != null) rangedAttacker.ClearRangeOverride();
        if (aoeAttacker != null) aoeAttacker.ClearRangeOverride();
        if (areaHealer != null) areaHealer.ClearRangeOverride();
    }
}
