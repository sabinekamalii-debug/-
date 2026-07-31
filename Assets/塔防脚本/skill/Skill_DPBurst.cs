using UnityEngine;

/// <summary>
/// 部署费用爆发返还技能（明日方舟「部署费用回复」类，例：桃金娘二技能 / 极境冲锋号令）。
/// 技力充满时（自动或手动）瞬间返还一大笔部署费用（DP）。
/// - autoActivate = true：技力满自动释放，做成「自动回费」先锋。
/// - autoActivate = false：需玩家点击干员释放，可作为「回撤触碰守护点」时手动触发的费用返还技能。
/// 注意：本技能只走 DeploymentManager.AddDP，与敌人击杀返费(dpOnKill) 完全独立，互不影响。
/// </summary>
public class Skill_DPBurst : OperatorSkill
{
    [Header("费用返还参数")]
    [Tooltip("技能触发时瞬间返还的部署费用数量")]
    public int dpBurst = 15;

    [Tooltip("刚部署时已积累的技力（相当于已冷却的秒数），用于控制第一次触发的快慢")]
    public float initialSPOnDeploy = 0f;

    [Header("触发时的视觉反馈")]
    [Tooltip("触发瞬间干员闪烁的颜色（费用返还给人金色/青色反馈较好）")]
    public Color flashColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("闪烁颜色是否启用")]
    public bool enableFlash = true;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    void Awake()
    {
        // 面板没填时给默认值，避免「改了没效果 / 忘了填」
        if (maxSP <= 0f) maxSP = 20f;      // 默认 20 点技力（约 20 秒）触发一次
        if (duration <= 0f) duration = 0.5f; // 瞬发型：极短持续，仅用于播放闪烁反馈
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);
        if (owner != null)
        {
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
        // 核心：瞬间返还部署费用
        if (DeploymentManager.Instance != null)
            DeploymentManager.Instance.AddDP(dpBurst);

        // 视觉反馈：短暂变色
        if (enableFlash && spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    originalColors[i] = spriteRenderers[i].color;
                    spriteRenderers[i].color = flashColor;
                }
            }
        }
    }

    public override void OnSkillEnd()
    {
        // 还原颜色
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
