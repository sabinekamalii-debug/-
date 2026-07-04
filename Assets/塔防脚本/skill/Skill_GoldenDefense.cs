using UnityEngine;

/// <summary>
/// 金色防御技能：持续时间内角色停止一切攻击/治疗、全身变金色、血量上限变为 3 倍、阻挡数 +2。
/// - 停止攻击：近战/远程/光波/牧师在技能期间均不攻击、不治疗。
/// - 金色外观：子物体上的 SpriteRenderer 颜色改为金色，结束时还原。
/// - 血量 3 倍：runtimeMaxHealth 与 currentHealth 按倍率放大，结束时还原上限并钳制当前血量。
/// - 阻挡 +2：UnitBlocker.maxBlockCount 临时 +2，OperatorUnit.currentBlockCount 同步，结束时还原。
/// - 持续 60 秒，需 30 点技力释放（约 30 秒冷却）。
/// </summary>
public class Skill_GoldenDefense : OperatorSkill
{
    [Header("技能参数")]
    [Tooltip("血量上限倍率，例如 3 表示最大生命变为原来的 3 倍")]
    public float healthMultiplier = 3f;

    [Tooltip("阻挡数增加量，例如 2 表示技能期间可多挡 2 个敌人")]
    public int blockCountBonus = 2;

    [Tooltip("技能期间角色显示的金色")]
    public Color goldenColor = new Color(1f, 0.85f, 0f);

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private int cachedMaxBlockCount;

    void Awake()
    {
        // Inspector 未手动配置（值为 0）时使用默认值；填了就尊重面板设置。
        if (duration <= 0f) duration = 60f; // 默认持续 60 秒
        if (maxSP   <= 0f) maxSP   = 30f;   // 默认需要 30 点技力（约 30 秒冷却）
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);
        if (owner == null) return;
        spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    public override void OnSkillStart()
    {
        if (owner == null) return;

        // 1. 停止攻击（近战、远程、光波、牧师都会检测此标志）
        owner.skillPreventAttack = true;

        // 若正处于攻击状态则立即退出，直到技能结束
        OperatorAttackAnimator attackAnim = owner.GetComponent<OperatorAttackAnimator>();
        if (attackAnim != null) attackAnim.ForceExitFightState();

        // 2. 全身变金色
        if (spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    originalColors[i] = spriteRenderers[i].color;
                    spriteRenderers[i].color = goldenColor;
                }
            }
        }

        // 3. 血量上限变为原来的 3 倍，当前血量同比例放大
        owner.runtimeMaxHealth = (int)(owner.data.maxHealth * healthMultiplier);
        owner.currentHealth = (int)(owner.currentHealth * healthMultiplier);
        owner.UpdateUIState();

        // 4. 阻挡数 +2
        if (owner.blocker != null)
        {
            cachedMaxBlockCount = owner.blocker.maxBlockCount;
            owner.blocker.maxBlockCount = cachedMaxBlockCount + blockCountBonus;
            owner.currentBlockCount = owner.blocker.maxBlockCount;
        }
    }

    public override void OnSkillEnd()
    {
        if (owner == null) return;

        // 1. 恢复攻击
        owner.skillPreventAttack = false;

        // 2. 恢复原色
        if (spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length && i < originalColors.Length; i++)
            {
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].color = originalColors[i];
            }
        }

        // 3. 还原血量上限，当前血量超过上限则压到上限
        owner.runtimeMaxHealth = (int)owner.data.maxHealth;
        if (owner.currentHealth > owner.runtimeMaxHealth)
            owner.currentHealth = owner.runtimeMaxHealth;
        owner.UpdateUIState();

        // 4. 还原阻挡数
        if (owner.blocker != null)
        {
            owner.blocker.maxBlockCount = cachedMaxBlockCount;
            owner.currentBlockCount = owner.blocker.maxBlockCount;
        }

        // 5. 若仍阻挡着敌人（或范围内有敌人/队友），恢复战斗动画 IsFight=true
        OperatorAttackAnimator attackAnim = owner.GetComponent<OperatorAttackAnimator>();
        if (attackAnim != null) attackAnim.RefreshFightState();
    }
}
