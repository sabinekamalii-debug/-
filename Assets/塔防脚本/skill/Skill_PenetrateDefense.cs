using UnityEngine;

/// <summary>
/// 破防近卫技能：技能期间干员攻击无视敌人防御（挂 IgnoreDefenseAttacker 标记）。
/// 适合近卫对付重甲兵等高防敌人。
/// </summary>
public class Skill_PenetrateDefense : OperatorSkill
{
    [Header("技能参数")]
    [Tooltip("刚部署时已积累的技力")]
    public float initialSPOnDeploy = 25f;

    [Tooltip("技能期间干员显示的颜色")]
    public Color skillColor = new Color(0.6f, 0.9f, 1f);

    private SpriteRenderer[] _spriteRenderers;
    private Color[] _originalColors;
    private IgnoreDefenseAttacker _penMarker;

    void Awake()
    {
        if (maxSP <= 0f) maxSP = 30f;
        if (duration <= 0f) duration = 10f;
    }

    public override void Initialize(OperatorUnit unit)
    {
        base.Initialize(unit);
        if (owner != null)
        {
            owner.currentSP = Mathf.Clamp(initialSPOnDeploy, 0f, maxSP);
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
        if (owner == null) return;

        // 挂上破防标记，AttackBlockedEnemies 里的 GetComponent<IgnoreDefenseAttacker>() 会取到它
        _penMarker = owner.GetComponent<IgnoreDefenseAttacker>();
        if (_penMarker == null)
            _penMarker = owner.gameObject.AddComponent<IgnoreDefenseAttacker>();

        // 变色反馈
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
        // 移除破防标记
        if (_penMarker != null)
            Destroy(_penMarker);

        // 恢复原色
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
