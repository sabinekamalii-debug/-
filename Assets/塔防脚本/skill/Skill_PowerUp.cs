using UnityEngine;

/// <summary>
/// 攻击力提升技能：持续时间内提高干员攻击力（runtimeAttackDamage）；释放时角色变蓝。
/// - 通用：近战/远程/光波等只影响「攻击伤害」数值。
/// </summary>
public class Skill_PowerUp : OperatorSkill
{
    [Header("技能参数")]
    [Tooltip("攻击力倍率，例如 2.5 表示攻击力变为原来的 2.5 倍")]
    public float damageMultiplier = 2f;

    [Tooltip("技能期间角色显示的蓝色")]
    public Color skillColor = new Color(0.4f, 0.6f, 1f);

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);
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
        if (owner != null && owner.data != null)
            owner.runtimeAttackDamage = (int)(owner.data.attackDamage * damageMultiplier);

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
            owner.runtimeAttackDamage = (int)owner.data.attackDamage;

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
