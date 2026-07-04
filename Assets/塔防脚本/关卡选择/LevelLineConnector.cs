using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据连线配置在关卡按钮之间自动生成炫酷连线。
/// 挂在 Lines 物体上，Lines 应与按钮同父级（Content）。
/// 每帧实时跟踪按钮位置，按钮移动时连线自动跟随。
/// </summary>
[ExecuteAlways]
public class LevelLineConnector : MonoBehaviour
{
    [Header("━━━━ 外观 ━━━━")]
    [SerializeField] float lineWidth = 4f;
    [SerializeField] float glowWidth = 14f;
    [SerializeField][Range(0.01f, 0.5f)] float glowAlpha = 0.12f;

    [Header("━━━━ 渐变色 ━━━━")]
    [SerializeField] Color earlyColor = new Color(0.25f, 0.55f, 1f, 1f);
    [SerializeField] Color midColor   = new Color(0.55f, 0.25f, 1f, 1f);
    [SerializeField] Color lateColor  = new Color(1f, 0.7f, 0.15f, 1f);

    [Header("━━━━ 箭头 ━━━━")]
    [SerializeField] bool showArrows = true;
    [SerializeField] float arrowSize = 10f;
    [SerializeField] Color arrowColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("━━━━ 流动动画 ━━━━")]
    [SerializeField] bool enableFlowAnimation = true;
    [SerializeField] float flowSpeed = 80f;
    [SerializeField] float flowDotSize = 6f;
    [SerializeField] float flowDotSpacing = 40f;

    [Header("━━━━ 配置 ━━━━")]
    [Tooltip("拖入连线配置（不填则线性1→2→...→16）")]
    [SerializeField] LevelConnectionConfig connectionConfig;

    RectTransform _rt;
    static Sprite _whiteSprite;

    struct LineInfo
    {
        public RectTransform fromBtn;
        public RectTransform toBtn;
        public RectTransform root;
        public RectTransform core;
        public RectTransform glow;
        public RectTransform arrowA;
        public RectTransform arrowB;
        public List<LineFlowAnimator> flowDots;
    }

    readonly List<LineInfo> _lines = new List<LineInfo>();

    static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                var tex = new Texture2D(4, 4);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            return _whiteSprite;
        }
    }

    void OnEnable()
    {
        GenerateLines();
    }

    [ContextMenu("重新生成连线")]
    public void GenerateLines()
    {
        _rt = GetComponent<RectTransform>();
        if (_rt == null) return;

        var parent = _rt.parent;
        if (parent == null) return;

        for (int i = _rt.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(_rt.GetChild(i).gameObject);
            else
                DestroyImmediate(_rt.GetChild(i).gameObject);
        }

        _lines.Clear();
        _rt.anchoredPosition = Vector2.zero;

        var pairs = new List<(int from, int to)>();

        if (connectionConfig != null && connectionConfig.connections.Count > 0)
        {
            foreach (var conn in connectionConfig.connections)
                pairs.Add((conn.from, conn.to));
        }
        else
        {
            for (int i = 1; i <= 15; i++)
                pairs.Add((i, i + 1));
        }

        foreach (var (from, to) in pairs)
        {
            var b1 = parent.Find("按钮" + from);
            var b2 = parent.Find("按钮" + to);
            if (b1 == null || b2 == null) continue;

            var r1 = b1.GetComponent<RectTransform>();
            var r2 = b2.GetComponent<RectTransform>();
            if (r1 == null || r2 == null) continue;

            CreateStyledLine(r1, r2, from, to);
        }

        // 首帧立刻刷新一次
        UpdateAllLines();
    }

    Color GetGradientColor(int from, int to)
    {
        float t = (from + to) / 2f / 16f;
        if (t < 0.5f)
            return Color.Lerp(earlyColor, midColor, t * 2f);
        else
            return Color.Lerp(midColor, lateColor, (t - 0.5f) * 2f);
    }

    void CreateStyledLine(RectTransform fromBtn, RectTransform toBtn, int from, int to)
    {
        Vector2 p1 = fromBtn.anchoredPosition;
        Vector2 p2 = toBtn.anchoredPosition;
        Vector2 dir = p2 - p1;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Color gradColor = GetGradientColor(from, to);

        var rootGo = new GameObject("Line_" + from + "to" + to);
        rootGo.transform.SetParent(_rt, false);
        var rootRt = rootGo.AddComponent<RectTransform>();
        rootRt.anchoredPosition = (p1 + p2) * 0.5f;
        rootRt.sizeDelta = new Vector2(Mathf.Max(dist, 1f), glowWidth);
        rootRt.localEulerAngles = new Vector3(0, 0, angle);

        // ── 辉光底层 ──
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(rootRt, false);
        var glowRt = glowGo.AddComponent<RectTransform>();
        var glowImg = glowGo.AddComponent<Image>();
        glowImg.sprite = WhiteSprite;
        glowImg.color = new Color(gradColor.r, gradColor.g, gradColor.b, glowAlpha);
        glowImg.raycastTarget = false;
        glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.sizeDelta = new Vector2(Mathf.Max(dist, 1f), glowWidth);
        glowRt.anchoredPosition = Vector2.zero;

        // ── 主线 ──
        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(rootRt, false);
        var coreRt = coreGo.AddComponent<RectTransform>();
        var coreImg = coreGo.AddComponent<Image>();
        coreImg.sprite = WhiteSprite;
        coreImg.color = gradColor;
        coreImg.raycastTarget = false;
        coreRt.anchorMin = coreRt.anchorMax = new Vector2(0.5f, 0.5f);
        coreRt.sizeDelta = new Vector2(Mathf.Max(dist, 1f), lineWidth);
        coreRt.anchoredPosition = Vector2.zero;

        // ── 箭头 ──
        RectTransform arrowA = null, arrowB = null;
        if (showArrows)
        {
            float halfS = arrowSize * 0.5f;
            arrowA = CreateArrowWing(rootRt, halfS, arrowColor);
            arrowB = CreateArrowWing(rootRt, halfS, arrowColor);
        }

        // ── 流动光点（仅运行时） ──
        var flowDots = new List<LineFlowAnimator>();
        if (enableFlowAnimation && Application.isPlaying)
        {
            int dotCount = Mathf.Max(1, Mathf.FloorToInt((dist - arrowSize) / flowDotSpacing));
            for (int i = 0; i < dotCount; i++)
            {
                var dotGo = new GameObject("FlowDot_" + i);
                dotGo.transform.SetParent(rootRt, false);
                var dotRt = dotGo.AddComponent<RectTransform>();
                var dotImg = dotGo.AddComponent<Image>();
                dotImg.sprite = WhiteSprite;
                dotImg.color = new Color(1f, 1f, 1f, 0.7f);
                dotImg.raycastTarget = false;
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
                dotRt.sizeDelta = new Vector2(flowDotSize, flowDotSize);
                dotRt.anchoredPosition = new Vector2(-dist * 0.5f + i * flowDotSpacing, 0);

                var anim = dotGo.AddComponent<LineFlowAnimator>();
                anim.speed = flowSpeed;
                anim.startX = -dist * 0.5f;
                anim.endX = dist * 0.5f - arrowSize * 0.5f;
                flowDots.Add(anim);
            }
        }

        _lines.Add(new LineInfo
        {
            fromBtn = fromBtn,
            toBtn = toBtn,
            root = rootRt,
            core = coreRt,
            glow = glowRt,
            arrowA = arrowA,
            arrowB = arrowB,
            flowDots = flowDots,
        });
    }

    RectTransform CreateArrowWing(Transform parent, float halfSize, Color color)
    {
        var go = new GameObject("ArrowWing");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        img.raycastTarget = false;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(halfSize, lineWidth + 1f);
        return rt;
    }

    /// <summary>
    /// 每帧根据按钮实时位置更新所有连线的位置/长度/角度/箭头/光点范围。
    /// 这就是「跟随按钮」的核心。
    /// </summary>
    void UpdateAllLines()
    {
        foreach (var line in _lines)
        {
            if (line.fromBtn == null || line.toBtn == null || line.root == null) continue;

            Vector2 p1 = line.fromBtn.anchoredPosition;
            Vector2 p2 = line.toBtn.anchoredPosition;
            Vector2 dir = p2 - p1;
            float dist = dir.magnitude;
            if (dist < 1f) dist = 1f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 根节点：中点 + 旋转
            line.root.anchoredPosition = (p1 + p2) * 0.5f;
            line.root.localEulerAngles = new Vector3(0, 0, angle);

            // 主线 + 辉光：长度跟随
            if (line.core != null)
                line.core.sizeDelta = new Vector2(dist, lineWidth);
            if (line.glow != null)
                line.glow.sizeDelta = new Vector2(dist, glowWidth);

            // 箭头：定位到终点端
            float tipX = dist * 0.5f;
            float halfS = arrowSize * 0.5f;
            if (line.arrowA != null)
            {
                line.arrowA.localEulerAngles = new Vector3(0, 0, -55f);
                line.arrowA.anchoredPosition = new Vector2(tipX - halfS * 0.4f, 0);
            }
            if (line.arrowB != null)
            {
                line.arrowB.localEulerAngles = new Vector3(0, 0, 55f);
                line.arrowB.anchoredPosition = new Vector2(tipX - halfS * 0.4f, 0);
            }

            // 流动光点：更新滚动范围
            if (line.flowDots != null)
            {
                float newStartX = -dist * 0.5f;
                float newEndX = dist * 0.5f - arrowSize * 0.5f;
                foreach (var anim in line.flowDots)
                {
                    anim.startX = newStartX;
                    anim.endX = newEndX;
                }
            }
        }
    }

    void LateUpdate()
    {
        if (_lines.Count == 0) return;
        UpdateAllLines();
    }
}
