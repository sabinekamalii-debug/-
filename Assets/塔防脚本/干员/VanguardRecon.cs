using UnityEngine;

/// <summary>
/// 先锋侦察标记被动：挂在先锋干员身上即可。
/// 干员移动/战斗/避让过程中，会持续侦察「行走方向前方」的一块长方形区域；
/// 长方形会跟随行走方向实时旋转（停步/战斗时保持上一次朝向）。
/// 进入该区域的敌人不会立刻被标记，而是开始累计「暴露时间」，待满 fullMarkTime（默认 2 秒）才完全标记；
/// 标记进度越高、受到的增伤越高（未满时按比例，完全标记时达到最大增伤）。
/// 配合高移动速度(OperatorBrain.moveSpeed)，实现「高速穿插、持续扫描、他人集火」的先锋定位。
/// </summary>
public class VanguardRecon : MonoBehaviour
{
    [Header("侦察范围（长方形，随行走方向旋转）")]
    [Tooltip("侦察范围长度：沿行走方向向「前方」延伸的距离")]
    public float reconLength = 5f;

    [Tooltip("侦察范围宽度：垂直于行走方向的横向宽度")]
    public float reconWidth = 3f;

    [Tooltip("侦察范围起点相对干员中心的前置偏移（0 = 紧贴干员身前）")]
    public float reconOffset = 0f;

    [Tooltip("检测节流间隔（秒），越小越灵敏但更耗性能")]
    public float scanInterval = 0.2f;

    [Header("范围可视化")]
    [Tooltip("运行时是否显示侦察范围框")]
    public bool showRange = true;

    [Tooltip("范围框外框颜色（青色侦察风）")]
    public Color outlineColor = new Color(0.3f, 0.9f, 1f, 0.85f);

    [Tooltip("范围框填充颜色（半透明）")]
    public Color fillColor = new Color(0.3f, 0.9f, 1f, 0.22f);

    [Tooltip("范围框外框线宽")]
    public float lineWidth = 0.05f;

    private float _scanTimer;
    private readonly Collider2D[] _hits = new Collider2D[64];

    // 行走方向（由实时位移推算，停止/战斗时保持上一次朝向）
    private Vector3 _facingDir = Vector3.right;
    private Vector3 _lastPosition;

    // 运行时范围可视化
    private LineRenderer _lr;
    private SpriteRenderer _autoFill;
    private static Sprite _proceduralSquareSprite;

    void Awake()
    {
        _lastPosition = transform.position;
        if (!showRange) return;
        SetupRangeVisual();
    }

    private void SetupRangeVisual()
    {
        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace = true;
        _lr.loop = true;
        _lr.positionCount = 4;
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
        _autoFill.sprite = GetSquareSprite();
        _autoFill.color = fillColor;
        _autoFill.sortingLayerName = "Default";
        _autoFill.sortingOrder = 199;
    }

    /// <summary> 生成一张 1x1 白色方块 Sprite，用于无素材的实心长方形范围（靠 localScale 拉伸）。 </summary>
    private static Sprite GetSquareSprite()
    {
        if (_proceduralSquareSprite != null) return _proceduralSquareSprite;
        const int size = 4;
        var tex = new Texture2D(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        _proceduralSquareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _proceduralSquareSprite;
    }

    void Update()
    {
        UpdateFacingDirection();

        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f)
        {
            UpdateRangeVisual();
            return;
        }
        _scanTimer = scanInterval;

        ScanEnemies();
        UpdateRangeVisual();
    }

    /// <summary> 用位移推算行走方向；停止/战斗时保持上一次朝向。 </summary>
    private void UpdateFacingDirection()
    {
        Vector3 delta = transform.position - _lastPosition;
        _lastPosition = transform.position;
        if (delta.sqrMagnitude > 0.0001f)
            _facingDir = delta.normalized;
    }

    /// <summary> 检测行走方向前方的长方形区域，把进入的敌人登记为「正在被侦察观察」。 </summary>
    private void ScanEnemies()
    {
        Vector3 center = transform.position + _facingDir * (reconOffset + reconLength * 0.5f);
        float angle = Mathf.Atan2(_facingDir.y, _facingDir.x) * Mathf.Rad2Deg;
        // OverlapBox 的 size 约定：x = 长度（沿前进方向），y = 宽度（垂直于前进方向）
        var size = new Vector2(reconLength, reconWidth);

        int count = Physics2D.OverlapBoxNonAlloc(center, size, angle, _hits);
        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null) continue;
            var enemy = _hits[i].GetComponentInParent<Enemy2>();
            if (enemy != null)
                enemy.NotifyReconObserved();
        }
    }

    /// <summary> 跟随先锋移动与朝向实时刷新长方形范围框。 </summary>
    private void UpdateRangeVisual()
    {
        if (!showRange) return;

        float angle = Mathf.Atan2(_facingDir.y, _facingDir.x) * Mathf.Rad2Deg;
        Vector3 center = transform.position + _facingDir * (reconOffset + reconLength * 0.5f);
        float hl = reconLength * 0.5f; // 半长（沿前进方向）
        float hw = reconWidth * 0.5f;  // 半宽（垂直于前进方向）

        if (_lr != null)
        {
            _lr.SetPosition(0, LocalToWorld(center, new Vector3(-hl, -hw, 0f)));
            _lr.SetPosition(1, LocalToWorld(center, new Vector3(hl, -hw, 0f)));
            _lr.SetPosition(2, LocalToWorld(center, new Vector3(hl, hw, 0f)));
            _lr.SetPosition(3, LocalToWorld(center, new Vector3(-hl, hw, 0f)));
        }

        if (_autoFill != null)
        {
            _autoFill.color = fillColor;
            _autoFill.transform.localPosition = _facingDir * (reconOffset + reconLength * 0.5f);
            _autoFill.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            _autoFill.transform.localScale = new Vector3(reconLength, reconWidth, 1f);
        }
    }

    /// <summary> 把以「中心为原点、x 沿前进方向 / y 沿垂直方向」为基准的局部偏移映射到世界坐标。 </summary>
    private Vector3 LocalToWorld(Vector3 center, Vector3 local)
    {
        Vector3 right = new Vector3(_facingDir.y, -_facingDir.x, 0f); // 前进方向顺时针 90° 的垂直方向
        return center + _facingDir * local.x + right * local.y;
    }

    // 在编辑器里可视化侦察范围，方便调参
    void OnDrawGizmosSelected()
    {
        Vector3 dir = Application.isPlaying ? _facingDir : (Vector3)transform.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector3 center = transform.position + dir * (reconOffset + reconLength * 0.5f);

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(reconLength, reconWidth, 0f));
        Gizmos.matrix = old;
    }
}
