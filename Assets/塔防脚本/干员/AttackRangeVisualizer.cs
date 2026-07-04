using UnityEngine;

/// <summary>
/// 攻击范围可视化：
/// - 外圈用 LineRenderer 画圆，颜色/线宽可自定义
/// - 填充：可自动生成实心圆（无需素材），或指定 SpriteRenderer 用自定义图
/// - 可选紫烟等粒子效果（需在子物体上挂 ParticleSystem）
/// 依赖 RangedAttacker 的 range 数值。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AttackRangeVisualizer : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("攻击脚本，读取 range 数值。如果留空则自动在本物体上查找。")]
    public RangedAttacker ranged;

    [Header("外圈设置")]
    [Tooltip("圆边数量，越大越圆，但稍微更耗性能。")]
    public int segments = 64;

    [Tooltip("外圈线宽")]
    public float lineWidth = 0.05f;

    [Tooltip("外圈颜色")]
    public Color outlineColor = new Color(0f, 0.7f, 1f, 0.8f);

    [Header("填充设置")]
    [Tooltip("勾选后：若无填充图则用代码生成实心圆（不需要任何素材）。")]
    public bool useFilledCircle = true;

    [Tooltip("用于填充的自定义 SpriteRenderer；为空且 useFilledCircle 为 true 时自动生成实心圆。拖入自己的图时，不会改它的 Scale 和颜色。")]
    public SpriteRenderer fillRenderer;

    [Tooltip("仅对脚本自动生成的实心圆生效；拖入自定义图时不会覆盖图案颜色。")]
    public Color fillColor = new Color(0f, 0.5f, 1f, 0.35f);

    [Tooltip("仅对脚本自动生成的实心圆生效；拖入自定义图时用 Transform 自己的 Scale。")]
    public float fillScaleMultiplier = 2f;

    [Header("圆内特效（可选）")]
    [Tooltip("圆内的粒子（如紫烟）。需在子物体上挂 ParticleSystem，形状设为 Circle、半径与 range 一致。")]
    public ParticleSystem rangeParticles;

    [Header("可见性")]
    [Tooltip("勾选后：仅在 Animator 的 IsFight 为 true（战斗状态）时显示范围圆，其他时候隐藏。")]
    public bool onlyShowWhenIsFight = true;

    [Tooltip("用于读取 IsFight 的 Animator；留空则自动从本物体或父物体获取。")]
    public Animator animator;

    [Tooltip("Animator 中表示“战斗/攻击”的 Bool 参数名。")]
    public string isFightParamName = "IsFight";

    [Header("Scene 视图预览")]
    [Tooltip("在 Scene 里显示攻击范围圆，方便编辑时调整。")]
    public bool showGizmoInScene = true;

    [Tooltip("Gizmo 圆颜色（仅 Scene 可见）")]
    public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.4f);

    private LineRenderer lr;
    private SpriteRenderer _autoFillRenderer;
    private static Sprite _proceduralCircleSprite;
    /// <summary> 技能期间 Fill 缩放倍数（远程干员放技能时设为 2，结束时恢复 1）。 </summary>
    private float _skillScaleMultiplier = 1f;

    /// <summary> 生成一张白色圆形 Sprite，用于无素材的实心范围。 </summary>
    private static Sprite CreateCircleSprite(int resolution = 128)
    {
        if (_proceduralCircleSprite != null) return _proceduralCircleSprite;
        int size = Mathf.Clamp(resolution, 16, 512);
        var tex = new Texture2D(size, size);
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, d <= radius ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        _proceduralCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _proceduralCircleSprite;
    }

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null) return;
        if (ranged == null) ranged = GetComponent<RangedAttacker>();

        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = segments;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 200;

        // 需要填充且没有指定 Sprite 时，自动建一个子物体用程序生成的圆
        if (useFilledCircle && fillRenderer == null)
        {
            var go = new GameObject("AttackRangeFill");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _autoFillRenderer = go.AddComponent<SpriteRenderer>();
            _autoFillRenderer.sprite = CreateCircleSprite();
            _autoFillRenderer.color = fillColor;
            _autoFillRenderer.sortingLayerName = "Default";
            _autoFillRenderer.sortingOrder = 199;
            fillRenderer = _autoFillRenderer;
        }

        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInParent<Animator>();

        if (onlyShowWhenIsFight)
            SetVisible(false);
    }

    void OnEnable()
    {
        if (onlyShowWhenIsFight)
            SetVisible(false);
    }

    void Start()
    {
        if (onlyShowWhenIsFight)
            SetVisible(false);
    }

    void LateUpdate()
    {
        if (lr == null || ranged == null) return;

        float r = ranged.range;
        Vector3 center = transform.position;

        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle) * r;
            float y = Mathf.Sin(angle) * r;
            lr.SetPosition(i, new Vector3(center.x + x, center.y + y, center.z));
        }

        if (fillRenderer != null)
        {
            bool isAutoFill = (fillRenderer == _autoFillRenderer);
            if (isAutoFill)
            {
                fillRenderer.color = fillColor;
                float scale = r * fillScaleMultiplier * _skillScaleMultiplier;
                fillRenderer.transform.localPosition = Vector3.zero;
                fillRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                fillRenderer.transform.localPosition = Vector3.zero;
            }
        }

        if (rangeParticles != null && r > 0.01f)
        {
            var sh = rangeParticles.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = r * 0.98f;
        }

        // 仅战斗状态显示：根据 Animator 的 IsFight 控制可见性
        if (onlyShowWhenIsFight)
        {
            bool isFight = animator != null && animator.GetBool(isFightParamName);
            bool currentlyVisible = lr != null && lr.enabled;
            if (currentlyVisible != isFight)
                SetVisible(isFight);
        }
    }

    /// <summary> 远程干员释放技能时调用，Fill 圆放大为原来的 multiplier 倍；结束时传 1 恢复。 </summary>
    public void SetSkillScaleMultiplier(float multiplier)
    {
        _skillScaleMultiplier = Mathf.Max(0.01f, multiplier);
    }

    /// <summary>
    /// 动态开关范围显示，比如选中时显示 / 非选中时隐藏。
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (lr != null) lr.enabled = visible;
        if (fillRenderer != null) fillRenderer.enabled = visible;
        if (rangeParticles != null)
        {
            if (visible && !rangeParticles.isPlaying) rangeParticles.Play();
            else if (!visible && rangeParticles.isPlaying) rangeParticles.Stop();
        }
    }

    /// <summary> 获取用于显示的半径（编辑时从 OperatorData 读，运行时用 RangedAttacker.range）。 </summary>
    private float GetRangeForDisplay()
    {
        var ou = GetComponent<OperatorUnit>();
        if (ou != null && ou.data != null && ou.data.attackRange > 0f)
            return ou.data.attackRange;
        if (ranged != null) return ranged.range;
        return 3.5f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmoInScene) return;
        float r = GetRangeForDisplay();
        Vector3 center = transform.position;
        const int segs = 48;
        Gizmos.color = gizmoColor;
        for (int i = 0; i < segs; i++)
        {
            float a0 = 2f * Mathf.PI * i / segs;
            float a1 = 2f * Mathf.PI * (i + 1) / segs;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f);
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
            Gizmos.DrawLine(p0, p1);
        }
    }
}
