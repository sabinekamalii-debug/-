using UnityEngine;

/// <summary>
/// 连线流动光点动画：让光点沿连线循环移动。
/// 挂在每条连线的子物体上，由 LevelLineConnector 自动生成。
/// </summary>
public class LineFlowAnimator : MonoBehaviour
{
    public float speed = 80f;
    public float startX = -400f;
    public float endX = 400f;
    [Tooltip("错开相位，避免所有光点同步")]
    public float phaseOffset = 0f;

    RectTransform _rt;
    float _x;

    void Start()
    {
        _rt = GetComponent<RectTransform>();
        _x = startX;
    }

    void Update()
    {
        if (_rt == null) return;

        _x += speed * Time.deltaTime;
        float range = endX - startX;
        if (range <= 0) return;

        // 循环滚动：跑到终点后从起点重新开始
        float t = ((_x - startX) % range) / range;
        if (t < 0) t += 1f;

        // 在起点附近淡入，在终点附近淡出
        float alpha = 1f;
        float fadeIn = 0.1f, fadeOut = 0.9f;
        if (t < fadeIn) alpha = t / fadeIn;
        else if (t > fadeOut) alpha = (1f - t) / (1f - fadeOut);

        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null)
            img.color = new Color(1f, 1f, 1f, alpha * 0.8f);

        _rt.anchoredPosition = new Vector2(
            Mathf.Lerp(startX, endX, t),
            _rt.anchoredPosition.y
        );
    }
}
