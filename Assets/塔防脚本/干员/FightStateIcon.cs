using System.Collections;
using UnityEngine;

/// <summary>
/// 近战/任意干员：根据 Animator 的 IsFight 显示或隐藏“战斗图标”。
/// 开局图标强制失活，只有 IsFight 为 true 时才激活。
/// Icon Root 可不填，只填 Icon Renderer 即可；Animator 不填会从本物体/父物体/子物体自动查找。
/// </summary>
public class FightStateIcon : MonoBehaviour
{
    [Header("图标")]
    [Tooltip("要随 IsFight 显隐的图标物体（整个 GameObject 的 active 会随 IsFight 切换）。不填没关系，只填下面 Icon Renderer 即可。")]
    public GameObject iconRoot;

    [Tooltip("指定要显隐的 SpriteRenderer，IsFight 时显示否则隐藏。二选一填一个即可。")]
    public SpriteRenderer iconRenderer;

    [Header("IsFight 来源")]
    [Tooltip("读取 IsFight 的 Animator。不填会从本物体/父物体/子物体自动查找；若图标不随战斗变化，请手动拖入和 OperatorAttackAnimator 同一个 Animator。")]
    public Animator animator;

    [Tooltip("Animator 里表示“战斗”的 Bool 参数名。")]
    public string isFightParamName = "IsFight";

    private bool _lastIsFight = false;
    private bool _loggedNoAnimator;
    private int _resolveFramesLeft = 5;
    private int _startupFramesLeft = 10;
    private int _graceFramesLeft = 600; // 冷静期后再约 600 帧（约 10 秒 @60fps）内强制隐藏，之后才响应 IsFight
    private bool _allowShowIcon;
    private int _consecutiveFightFrames;

    void Awake()
    {
        ResolveAnimator();
        ForceIconInactive();
    }

    /// <summary> 必须和 OperatorAttackAnimator 用同一个 Animator 和参数名，否则 IsFight 读不到。 </summary>
    private void ResolveAnimator()
    {
        var attackAnim = GetComponent<OperatorAttackAnimator>();
        if (attackAnim != null && attackAnim.animator != null)
        {
            animator = attackAnim.animator;
            if (!string.IsNullOrEmpty(attackAnim.isFightParam))
                isFightParamName = attackAnim.isFightParam;
            return;
        }
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInParent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
    }

    void OnEnable()
    {
        ForceIconInactive();
    }

    void Start()
    {
        ResolveAnimator();
        ForceIconInactive();
        _lastIsFight = animator != null && animator.GetBool(isFightParamName);
        StartCoroutine(ForceInactiveAfterFrames());
    }

    IEnumerator ForceInactiveAfterFrames()
    {
        yield return null;
        ForceIconInactive();
        yield return null;
        ForceIconInactive();
    }

    void LateUpdate()
    {
        if (animator == null)
        {
            if (_resolveFramesLeft > 0)
            {
                _resolveFramesLeft--;
                ResolveAnimator();
            }
            if (animator == null)
            {
                return;
            }
        }
        if (_startupFramesLeft > 0)
        {
            _startupFramesLeft--;
            ForceIconInactive();
            return;
        }

        // 2) 冷静期结束后 50 帧内：只做“允许显示”的倒计时，不在这段时间内因为看到 IsFight=false 就允许显示（避免首帧抖动）
        if (_graceFramesLeft > 0)
        {
            _graceFramesLeft--;
            ForceIconInactive();
            return;
        }

        bool isFight = animator.GetBool(isFightParamName);
        if (isFight == false)
        {
            _allowShowIcon = true;
            _consecutiveFightFrames = 0;
        }
        else
        {
            _consecutiveFightFrames++;
            if (!_allowShowIcon && _consecutiveFightFrames >= 30)
                _allowShowIcon = true;
        }

        if (isFight != _lastIsFight)
            _lastIsFight = isFight;
        if (isFight && _allowShowIcon)
            ApplyVisible(true);
        else
            ForceIconInactive();
    }

    /// <summary> 强制图标失活（开局用），不依赖 _lastIsFight。 </summary>
    private void ForceIconInactive()
    {
        if (iconRoot != null)
            iconRoot.SetActive(false);
        if (iconRenderer != null)
        {
            iconRenderer.enabled = false;
            iconRenderer.gameObject.SetActive(false);
        }
    }

    void ApplyVisible(bool visible)
    {
        if (iconRoot != null)
            iconRoot.SetActive(visible);
        if (iconRenderer != null)
        {
            iconRenderer.enabled = visible;
            iconRenderer.gameObject.SetActive(visible);
        }
    }
}
