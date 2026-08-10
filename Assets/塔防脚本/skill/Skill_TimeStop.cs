using UnityEngine;

/// <summary>
/// 时之魔王专属技能：暂停全图敌人。
/// 技力充满后自动释放，眩晕全图敌人 stunDuration 秒。
/// </summary>
public class Skill_TimeStop : OperatorSkill
{
    [Header("时停技能参数")]
    [Tooltip("眩晕全图敌人的持续时间（秒）")]
    public float stunDuration = 5f;

    [Tooltip("技能激活时的颜色变化")]
    public Color skillColor = new Color(0.4f, 0.6f, 1f);

    [Header("初始技力")]
    [Tooltip("开局时技能的初始技力")]
    public float initialSP = 20f;

    private SpriteRenderer[] _spriteRenderers;
    private Color[] _originalColors;

    void Awake()
    {
        if (duration <= 0f) duration = stunDuration;
        if (maxSP <= 0f) maxSP = 35f;
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
        Enemy2.StunAllEnemies(stunDuration);

        if (_spriteRenderers != null && _originalColors != null)
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
