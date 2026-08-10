using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 干员商店竖向滚动控制器 — 整卡吸附模式。
/// 每次滚轮/拖拽只移动一个卡片位置，松手后自动吸附到最近的整卡位置。
/// 使用 RectMask2D（不是 Mask）做裁剪，兼容 URP。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class OperatorShopScroll : MonoBehaviour, IScrollHandler
{
    [Header("引用")]
    public RectTransform viewport;
    public RectTransform content;

    [Header("卡片设置")]
    public float cardSpacing = 0.15f;
    public float cardHeight = 1.2f;

    [Header("吸附动画")]
    [Tooltip("吸附到整卡位置的速度（每秒插值系数，越大越快）")]
    public float snapLerpSpeed = 12f;

    private Canvas _canvas;
    private Camera _cam;
    private RectMask2D _mask;

    // 目标卡片索引（浮点，动画过程中会从当前值插值到整数）
    private float _targetIndex = 0f;
    private bool _isDragging;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
            _cam = _canvas.worldCamera;
        if (viewport != null)
            _mask = viewport.GetComponent<RectMask2D>();
        if (_mask != null)
            _mask.enabled = Application.isPlaying;
    }

    private void ForceMaskUpdate()
    {
        if (_mask == null) return;
        _mask.enabled = false;
        _mask.enabled = true;
    }

    private void Update()
    {
        if (content == null || viewport == null) return;

        if (!_isDragging)
        {
            // 吸附动画：从当前位置插值到目标整卡位置
            float step = cardHeight + cardSpacing;
            float targetY = _targetIndex * step;
            float clampedTarget = Mathf.Clamp(targetY, 0f, GetScrollableRange());

            float currentY = content.anchoredPosition.y;
            if (Mathf.Abs(currentY - clampedTarget) > 0.001f)
            {
                float newY = Mathf.Lerp(currentY, clampedTarget, snapLerpSpeed * Time.deltaTime);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);
                ForceMaskUpdate();
            }
        }
    }

    // ===== 鼠标滚轮：每次只移动一个卡片 =====
    public void OnScroll(PointerEventData eventData)
    {
        float scrollDelta = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        // 滚轮向下 (scrollDelta<0) → 看下一个卡片 → targetIndex+1
        int dir = scrollDelta > 0 ? -1 : 1;
        _targetIndex = Mathf.RoundToInt(_targetIndex) + dir;
        _targetIndex = Mathf.Clamp(_targetIndex, 0f, GetMaxIndex());
    }

    // ===== 拖拽滚动：自由拖动，松手吸附 =====
    public void BeginScroll(Vector2 screenPos)
    {
        _isDragging = true;
        _lastLocalPos = ScreenToContentLocal(screenPos);
    }

    public void UpdateScroll(Vector2 screenPos)
    {
        if (!_isDragging) return;
        Vector2 cur = ScreenToContentLocal(screenPos);
        float deltaY = cur.y - _lastLocalPos.y;
        Vector2 pos = content.anchoredPosition;
        pos.y += deltaY;
        pos.y = Mathf.Clamp(pos.y, 0f, GetScrollableRange());
        content.anchoredPosition = pos;
        ForceMaskUpdate();
        _lastLocalPos = cur;
    }

    public void EndScroll()
    {
        _isDragging = false;
        // 吸附到最近的整卡位置
        float step = cardHeight + cardSpacing;
        _targetIndex = Mathf.RoundToInt(content.anchoredPosition.y / step);
        _targetIndex = Mathf.Clamp(_targetIndex, 0f, GetMaxIndex());
    }

    // ===== 辅助方法 =====

    private float Step => cardHeight + cardSpacing;

    private float GetScrollableRange()
    {
        return Mathf.Max(0f, GetContentHeight() - viewport.rect.height);
    }

    private float GetMaxIndex()
    {
        return Mathf.Max(0f, GetScrollableRange() / Step);
    }

    private float GetContentHeight()
    {
        if (content == null) return 0f;
        int count = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i).gameObject.activeSelf) count++;
        }
        return count * Step + cardSpacing;
    }

    public void ScrollToCard(int index)
    {
        _targetIndex = Mathf.Clamp(index, 0, Mathf.RoundToInt(GetMaxIndex()));
    }

    public void ClampImmediate()
    {
        float step = Step;
        float targetY = Mathf.Clamp(_targetIndex * step, 0f, GetScrollableRange());
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        ForceMaskUpdate();
    }

    private Vector2 _lastLocalPos;
    private Vector2 ScreenToContentLocal(Vector2 screenPos)
    {
        if (viewport == null) return screenPos;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, _cam, out local);
        return local;
    }
}
