using UnityEngine;

/// <summary>
/// 近卫特质（挂到近卫预制体上即可，无需其他配置）。
/// 近卫普遍可阻挡多个敌人（UnitBlocker.maxBlockCount 建议设 2~3）。
/// 当本干员「恰好只阻挡 1 个敌人」时进入「专注」状态：
///   - 全身变色（focusColor）；
///   - 攻击速度 +0.5 倍（攻击间隔 ÷1.5，通过 OperatorUnit.traitAttackSpeedMultiplier 生效，
///     与技能系统互不冲突）。
/// 阻挡 0 个或多个敌人时不触发（保持常态）。
/// 设计意图：近卫 = 近战斩杀核心；单挑时爆发，群挡时转为稳定输出，
/// 与「重装只扛不输出」「先锋铺场回费」形成清晰分工。
/// </summary>
public class GuardTrait : MonoBehaviour
{
    [Header("专注状态外观")]
    [Tooltip("阻挡 1 个敌人时干员显示的颜色，默认亮橙黄")]
    public Color focusColor = new Color(1f, 0.75f, 0.2f);

    [Header("专注加成")]
    [Tooltip("攻击速度加成倍率。0.5 = 攻速 +50%（攻击间隔 ÷1.5）。可在 Inspector 调。")]
    public float attackSpeedBonus = 0.5f;

    private OperatorUnit owner;
    private UnitBlocker blocker;
    private Animator animator;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private bool _focusActive = false;

    void Awake()
    {
        owner = GetComponent<OperatorUnit>();
        blocker = GetComponent<UnitBlocker>();
    }

    void Start()
    {
        if (owner == null) return;
        spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                originalColors[i] = spriteRenderers[i].color;
        }
        animator = owner.GetComponent<Animator>();
    }

    void Update()
    {
        if (owner == null) { SetFocus(false); return; }
        if (blocker == null) { SetFocus(false); return; }

        // 仅清理已销毁引用，不改动 blockedEnemies 的阻挡逻辑
        blocker.blockedEnemies.RemoveAll(e => e == null);
        bool shouldFocus = blocker.blockedEnemies.Count == 1;
        SetFocus(shouldFocus);
    }

    private void SetFocus(bool active)
    {
        if (active == _focusActive) return;
        _focusActive = active;

        if (active)
        {
            // 攻速 +0.5 倍 => 攻击间隔 ÷ (1 + 0.5)
            owner.traitAttackSpeedMultiplier = 1f + attackSpeedBonus;
            // 播放速度同步加快，避免动作与出手节奏脱节
            if (animator != null) animator.speed = 1f + attackSpeedBonus;
            // 变色
            if (spriteRenderers != null && originalColors != null)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                    if (spriteRenderers[i] != null) spriteRenderers[i].color = focusColor;
            }
        }
        else
        {
            owner.traitAttackSpeedMultiplier = 1f;
            if (animator != null) animator.speed = 1f;
            if (spriteRenderers != null && originalColors != null)
            {
                for (int i = 0; i < spriteRenderers.Length && i < originalColors.Length; i++)
                    if (spriteRenderers[i] != null) spriteRenderers[i].color = originalColors[i];
            }
        }
    }

    void OnDestroy()
    {
        if (owner != null) owner.traitAttackSpeedMultiplier = 1f;
    }
}
