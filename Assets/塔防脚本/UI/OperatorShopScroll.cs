using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 干员商店竖向滚动控制器。
/// 支持两种滚动方式：①鼠标滚轮 ②手指/鼠标拖拽（通过 OperatorCard 方向路由触发）。
/// 使用 RectMask2D（不是 Mask）做裁剪，兼容 URP。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class OperatorShopScroll : MonoBehaviour, IScrollHandler
{
    [Header("引用")]
    [Tooltip("视口（带 RectMask2D 的 RectTransform）")]
    public RectTransform viewport;
    [Tooltip("内容容器（卡片放在这里面）")]
    public RectTransform content;

    [Header("滚动设置")]
    [Tooltip("卡片之间的间距（内容坐标单位）")]
    public float cardSpacing = 12f;
    [Tooltip("每张卡片的高度（内容坐标单位）")]
    public float cardHeight = 100f;
    [Tooltip("滚动惯性衰减系数（0=无惯性，越高越滑）")]
    public float deceleration = 0.05f;
    [Tooltip("顶部和底部超出边界后的回弹力度")]
    public float elasticity = 0.1f;
    [Tooltip("鼠标滚轮一次滚动的距离（内容坐标单位）")]
    public float scrollSensitivity = 300f;

    // 滚动状态
    private bool _isScrolling;
    private Vector2 _lastLocalPos;
    private Vector2 _velocity;

    private Canvas _canvas;
    private Camera _cam;
    private RectMask2D _mask;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
            _cam = _canvas.worldCamera;
        if (viewport != null)
            _mask = viewport.GetComponent<RectMask2D>();
    }

    /// <summary> RectMask2D 在 WorldSpace Canvas 中滚动后不自动更新裁剪，需要强制刷新。 </summary>
    private void ForceMaskUpdate()
    {
        if (_mask == null) return;
        _mask.enabled = false;
        _mask.enabled = true;
    }

    private void Update()
    {
        if (_isScrolling) return;

        // 惯性滑动
        if (_velocity.sqrMagnitude > 0.01f)
        {
            Vector2 pos = content.anchoredPosition;
            pos += _velocity * Time.deltaTime;
            content.anchoredPosition = pos;
            _velocity *= (1f - deceleration);
        }
        else
        {
            _velocity = Vector2.zero;
        }

        // 边界回弹
        ClampContent(elasticity);
    }

    // ===== 鼠标滚轮滚动 =====
    public void OnScroll(PointerEventData eventData)
    {
        float scrollDelta = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        Vector2 pos = content.anchoredPosition;
        pos.y += scrollDelta * scrollSensitivity * 0.01f;

        float contentHeight = GetContentHeight();
        float viewportHeight = viewport.rect.height;
        float scrollable = Mathf.Max(0f, contentHeight - viewportHeight);
        pos.y = Mathf.Clamp(pos.y, -scrollable, 0f);
        content.anchoredPosition = pos;

        // RectMask2D 需要强制刷新才能显示新进入视口的卡片
        ForceMaskUpdate();

        // 给一点惯性
        _velocity = new Vector2(0, scrollDelta * scrollSensitivity * 0.01f * 10f);
    }

    // ===== 拖拽滚动（由 OperatorCard 方向路由调用）=====
    public void BeginScroll(Vector2 screenPos)
    {
        _isScrolling = true;
        _velocity = Vector2.zero;
        _lastLocalPos = ScreenToContentLocal(screenPos);
    }

    public void UpdateScroll(Vector2 screenPos)
    {
        if (!_isScrolling) return;
        Vector2 cur = ScreenToContentLocal(screenPos);
        float deltaY = cur.y - _lastLocalPos.y;
        Vector2 pos = content.anchoredPosition;
        pos.y += deltaY;
        float contentHeight = GetContentHeight();
        float viewportHeight = viewport.rect.height;
        float scrollable = Mathf.Max(0f, contentHeight - viewportHeight);
        pos.y = Mathf.Clamp(pos.y, -scrollable, 0f);
        content.anchoredPosition = pos;
        // RectMask2D 需要强制刷新才能显示新进入视口的卡片
        ForceMaskUpdate();
        _lastLocalPos = cur;
        _velocity = new Vector2(0, deltaY / Mathf.Max(Time.deltaTime, 0.001f));
    }

    public void EndScroll()
    {
        _isScrolling = false;
    }

    private void ClampContent(float lerpFactor)
    {
        float contentHeight = GetContentHeight();
        float viewportHeight = viewport.rect.height;
        float scrollable = Mathf.Max(0f, contentHeight - viewportHeight);
        float targetY = Mathf.Clamp(content.anchoredPosition.y, -scrollable, 0f);

        if (lerpFactor >= 1f)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        }
        else
        {
            float currentY = content.anchoredPosition.y;
            float newY = Mathf.Lerp(currentY, targetY, lerpFactor);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);
            if (currentY < -scrollable || currentY > 0f)
                _velocity = Vector2.zero;
        }
    }

    public void ClampImmediate()
    {
        ClampContent(1f);
    }

    private float GetContentHeight()
    {
        if (content == null) return 0f;
        int childCount = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i).gameObject.activeSelf) childCount++;
        }
        return childCount * (cardHeight + cardSpacing) + cardSpacing;
    }

    public void ScrollToCard(int index)
    {
        if (content == null) return;
        float viewportHeight = viewport.rect.height;
        float contentHeight = GetContentHeight();
        float scrollable = Mathf.Max(0f, contentHeight - viewportHeight);
        float targetY = -index * (cardHeight + cardSpacing);
        targetY = Mathf.Clamp(targetY, -scrollable, 0f);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
    }

    private Vector2 ScreenToContentLocal(Vector2 screenPos)
    {
        if (viewport == null) return screenPos;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, _cam, out local);
        return local;
    }
}
