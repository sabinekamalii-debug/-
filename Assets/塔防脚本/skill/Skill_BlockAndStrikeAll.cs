using UnityEngine;

/// <summary>
/// 阻挡并攻击所有敌人技能：持续时间内阻挡数变为 10、同时攻击所有被阻挡的敌人、攻击力提升 1/3；近战干员释放时角色变紫。
/// - 阻挡数：UnitBlocker.maxBlockCount 临时设为 10，结束时还原。
/// - 攻击方式：OperatorUnit.skillAttackAllBlocked = true，近战普攻对所有阻挡单位造成伤害；结束时还原为只打第一个。
/// - 攻击力：技能期间为干员数据的 4/3 倍（增加 1/3），结束时还原。
/// - 持续 60 秒，需 40 点技力释放（冷却 40 秒）。
/// </summary>
public class Skill_BlockAndStrikeAll : OperatorSkill
{
    [Header("技能参数")]
    [Tooltip("技能持续时的阻挡数")]
    public int skillBlockCount = 10;

    [Tooltip("攻击力倍率，例如 4/3 表示增加 1/3")]
    public float damageMultiplier = 4f / 3f;

    [Tooltip("技能期间角色显示的紫色")]
    public Color skillColor = new Color(0.7f, 0.4f, 1f);

    [Header("初始技力设置")]
    [Tooltip("开局时技能的初始技力（蓝条起始值），单位同 maxSP，可在 Inspector / 控制台面板中直接调整。")]
    public float initialSP = 30f; // 默认开局 30 点蓝

    private int cachedMaxBlockCount;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    void Awake()
    {
        // 如果在 Inspector 中没有手动配置（仍为 0），则使用默认值；
        // 一旦你在面板里填了数值，就不会被这里覆盖，避免“改了没效果”的情况。
        if (duration <= 0f)      duration = 60f; // 默认持续 60 秒
        if (maxSP   <= 0f)       maxSP   = 40f;  // 默认需要 40 点技力
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);

        // 初始化时把干员当前技力设为 initialSP（夹在 0 ~ maxSP 之间），
        // 这样进关时蓝条就从 initialSP 开始，数值可在 Inspector 面板里直接改。
        if (unit != null)
        {
            unit.currentSP = Mathf.Clamp(initialSP, 0f, maxSP);
        }

        if (owner != null)
        {
            spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers != null && spriteRenderers.Length > 0)
            {
                originalColors = new Color[spriteRenderers.Length];
                for (int i = 0; i < spriteRenderers.Length; i++)
                    originalColors[i] = spriteRenderers[i].color;
            }
        }
    }

    public override void OnSkillStart()
    {
        if (owner == null) return;

        if (owner.blocker != null)
        {
            cachedMaxBlockCount = owner.blocker.maxBlockCount;
            owner.blocker.maxBlockCount = skillBlockCount;
            owner.currentBlockCount = owner.blocker.maxBlockCount;
        }

        owner.skillAttackAllBlocked = true;

        if (owner.data != null)
            owner.runtimeAttackDamage = (int)(owner.data.attackDamage * damageMultiplier);

        // 全身变紫
        if (spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    originalColors[i] = spriteRenderers[i].color;
                    spriteRenderers[i].color = skillColor;
                }
            }
        }
    }

    public override void OnSkillEnd()
    {
        if (owner == null) return;

        owner.skillAttackAllBlocked = false;

        if (owner.blocker != null)
        {
            owner.blocker.maxBlockCount = cachedMaxBlockCount;
            owner.currentBlockCount = owner.blocker.maxBlockCount;
        }

        if (owner.data != null)
            owner.runtimeAttackDamage = (int)owner.data.attackDamage;

        // 恢复原色
        if (spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length && i < originalColors.Length; i++)
            {
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].color = originalColors[i];
            }
        }
    }
}
