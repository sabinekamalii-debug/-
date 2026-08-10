using UnityEngine;

/// <summary>
/// 熔岩虫专属技能：吞噬低血量敌人。
/// 技力充满后自动释放，将被阻挡且血量低于阈值的非精英敌人强制消灭。
/// 消化冷却 = maxSP 充能时间。
/// </summary>
public class Skill_Devour : OperatorSkill
{
    [Header("吞噬技能参数")]
    [Tooltip("血量低于此比例的非精英敌人将被吞噬（0.4 = 40%）")]
    public float executeThreshold = 0.4f;

    [Tooltip("技能激活时的颜色变化")]
    public Color skillColor = new Color(1f, 0.3f, 0.1f);

    [Header("初始技力")]
    [Tooltip("开局时技能的初始技力")]
    public float initialSP = 10f;

    private SpriteRenderer[] _spriteRenderers;
    private Color[] _originalColors;

    void Awake()
    {
        if (duration <= 0f) duration = 1f;
        if (maxSP <= 0f) maxSP = 20f;
        autoActivate = true;
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);

        if (unit != null)
            unit.currentSP = Mathf.Clamp(initialSP, 0f, maxSP);

        if (owner != null)
        {
            _spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
            if (_spriteRenderers != null && _spriteRenderers.Length > 0)
            {
                _originalColors = new Color[_spriteRenderers.Length];
                for (int i = 0; i < _spriteRenderers.Length; i++)
                    _originalColors[i] = _spriteRenderers[i].color;
            }
        }
    }

    public override void OnSkillStart()
    {
        if (owner == null || owner.blocker == null) return;

        bool devoured = false;

        for (int i = owner.blocker.blockedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy2 enemy = owner.blocker.blockedEnemies[i];
            if (enemy == null) continue;
            if (enemy.isElite) continue;

            if (enemy.GetHealthRatio() <= executeThreshold)
            {
                enemy.TakeDamage(99999, true);
                devoured = true;
            }
        }

        if (devoured && _spriteRenderers != null && _originalColors != null)
        {
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    _originalColors[i] = _spriteRenderers[i].color;
                    _spriteRenderers[i].color = skillColor;
                }
            }
        }
    }

    public override void OnSkillEnd()
    {
        if (_spriteRenderers != null && _originalColors != null)
        {
            for (int i = 0; i < _spriteRenderers.Length && i < _originalColors.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                    _spriteRenderers[i].color = _originalColors[i];
            }
        }
    }
}
