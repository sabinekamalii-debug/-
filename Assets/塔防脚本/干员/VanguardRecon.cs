using UnityEngine;

/// <summary>
/// 先锋侦察标记被动：挂在先锋干员身上即可。
/// 干员移动/战斗/避让过程中，只要有敌人进入侦察半径，就把它「标记」（青色侦察环 + 全身变色 + 限时增伤）。
/// 标记独立于「打 / 避让」抉择——无论玩家最后选战斗还是避让，路过的敌人都已被标记。
/// 配合高移动速度(OperatorBrain.moveSpeed)，实现「高速穿插侦察、路过即标记、他人集火」的先锋定位。
/// </summary>
public class VanguardRecon : MonoBehaviour
{
    [Header("侦察标记参数")]
    [Tooltip("侦察半径：敌人进入该范围即被标记")]
    public float markRadius = 2.5f;

    [Tooltip("标记持续时间（秒），先锋再次路过会刷新")]
    public float markDuration = 10f;

    [Tooltip("检测节流间隔（秒），越小越灵敏但更耗性能")]
    public float scanInterval = 0.2f;

    [Header("范围可视化")]
    [Tooltip("运行时是否显示侦察范围圈（与法师/射手攻击圈同款样式）")]
    public bool showRange = true;

    [Tooltip("范围圈外圈颜色（青色侦察风）")]
    public Color outlineColor = new Color(0.3f, 0.9f, 1f, 0.85f);

    [Tooltip("范围圈填充颜色（半透明）")]
    public Color fillColor = new Color(0.3f, 0.9f, 1f, 0.22f);

    [Tooltip("范围圈分段数，越大越圆")]
    public int segments = 64;

    [Tooltip("范围圈外圈线宽")]
    public float lineWidth = 0.05f;

    private float _scanTimer;
    private readonly Collider2D[] _hits = new Collider2D[32];

    // 运行时范围可视化
    private LineRenderer _lr;
    private SpriteRenderer _autoFill;
    private static Sprite _proceduralCircleSprite;

    void Awake()
    {
        if (!showRange) return;
        SetupRangeVisual();
    }

    private void SetupRangeVisual()
    {
        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace = true;
        _lr.loop = true;
        _lr.positionCount = segments;
        _lr.startWidth = lineWidth;
        _lr.endWidth = lineWidth;
        _lr.material = new Material(Shader.Find("Sprites/Default"));
        _lr.startColor = outlineColor;
        _lr.endColor = outlineColor;
        _lr.sortingLayerName = "Default";
        _lr.sortingOrder = 200;

        var go = new GameObject("VanguardReconRangeFill");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        _autoFill = go.AddComponent<SpriteRenderer>();
        _autoFill.sprite = GetCircleSprite();
        _autoFill.color = fillColor;
        _autoFill.sortingLayerName = "Default";
        _autoFill.sortingOrder = 199;
    }

    /// <summary> 生成一张白色圆形 Sprite，用于无素材的实心范围。 </summary>
    private static Sprite GetCircleSprite(int resolution = 128)
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

    void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f)
        {
            UpdateRangeVisual();
            return;
        }
        _scanTimer = scanInterval;

        // 用非分配版本避免每帧 GC
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, markRadius, _hits);
        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null) continue;
            var enemy = _hits[i].GetComponentInParent<Enemy2>();
            if (enemy != null)
                enemy.MarkForRecon(markDuration);
        }

        UpdateRangeVisual();
    }

    /// <summary> 跟随先锋移动实时刷新范围圈位置与半径。 </summary>
    private void UpdateRangeVisual()
    {
        if (showRange && _lr != null)
        {
            var center = transform.position;
            for (int i = 0; i < segments; i++)
            {
                float angle = 2 * Mathf.PI * i / segments;
                _lr.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * markRadius,
                    center.y + Mathf.Sin(angle) * markRadius,
                    center.z));
            }
            if (_autoFill != null)
            {
                _autoFill.color = fillColor;
                float scale = markRadius * 2f;
                _autoFill.transform.localPosition = Vector3.zero;
                _autoFill.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    // 在编辑器里可视化侦察半径，方便调参（未挂运行时圈时也能看到）
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, markRadius);
    }
}
