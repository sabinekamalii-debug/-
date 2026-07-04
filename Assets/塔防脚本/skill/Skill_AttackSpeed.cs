using UnityEngine;

/// <summary>
/// 攻速提升技能：持续时间内减少攻击间隔并加快动画播放；近战干员释放时角色变红（参考 Skill_GoldenDefense 变色）。
/// - 通用：所有有 attackInterval 的干员都兼容（近战、远程、光波、牧师等）。
/// - 牧师：会缩短治疗间隔（AreaHealer 使用 runtimeAttackInterval），技能期间治疗更频繁。
/// </summary>
public class Skill_AttackSpeed : OperatorSkill
{
    [Header("技能参数")]
    [Tooltip("攻速倍率，例如 2 表示攻击间隔变为原来的 1/2，即攻速 2 倍")]
    public float speedMultiplier = 2f;

    [Tooltip("刚部署时已积累的技力（相当于冷却已过的秒数），例如 35 表示开局约 35 秒后即可释放")]
    public float initialSPOnDeploy = 35f;

    [Tooltip("技能期间近战干员显示的颜色")]
    public Color skillColor = new Color(1f, 0.3f, 0.3f);

    private Animator cachedAnimator;
    private float originalAnimatorSpeed = 1f;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    void Awake()
    {
        // 如果在 Inspector 中没有手动配置（仍为 0），则使用默认值；
        // 一旦你在面板里填了数值，就不会被这里覆盖，避免“改了没效果”的情况。
        if (duration <= 0f) duration = 8f;   // 默认持续 8 秒
        if (maxSP   <= 0f) maxSP   = 40f;   // 默认需要 40 点技力才能释放（冷却 40 秒）
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);
        if (owner != null)
        {
            cachedAnimator = owner.GetComponentInChildren<Animator>();
            owner.currentSP = Mathf.Clamp(initialSPOnDeploy, 0f, maxSP);
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
        if (owner != null && owner.data != null)
            owner.runtimeAttackInterval = owner.data.attackInterval / speedMultiplier;

        if (cachedAnimator != null)
        {
            originalAnimatorSpeed = cachedAnimator.speed;
            cachedAnimator.speed = speedMultiplier;
        }

        // 近战干员：全身变红
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
        if (owner != null && owner.data != null)
            owner.runtimeAttackInterval = owner.data.attackInterval;

        if (cachedAnimator != null)
            cachedAnimator.speed = originalAnimatorSpeed;

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
