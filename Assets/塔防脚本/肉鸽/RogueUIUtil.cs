using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 肉鸽场景 UI 通用：按钮子物体统一用 TextMeshProUGUI，保证按钮可见。
/// </summary>
public static class RogueUIUtil
{
    /// <summary>
    /// 若按钮下有名为 "Text" 的子物体且挂的是旧版 Text，则替换为 TextMeshProUGUI（保留文字、颜色、字号、对齐）。
    /// </summary>
    public static void EnsureButtonLabelTmp(Button button)
    {
        if (button == null) return;
        var textTrans = button.transform.Find("Text");
        if (textTrans == null) return;

        var legacy = textTrans.GetComponent<Text>();
        if (legacy == null) return;

        var t = legacy.text;
        var c = legacy.color;
        var fs = legacy.fontSize;
        var a = legacy.alignment;
        Object.DestroyImmediate(legacy, true);
        var tmp = textTrans.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = t;
        tmp.color = c;
        tmp.fontSize = fs;
        tmp.alignment = ConvertAlignment(a);
    }

    /// <summary>
    /// 保证这些按钮的 GameObject 为 active，避免一个可见一个不可见。
    /// </summary>
    public static void EnsureButtonsVisible(params Button[] buttons)
    {
        if (buttons == null) return;
        foreach (var b in buttons)
        {
            if (b != null && !b.gameObject.activeSelf)
                b.gameObject.SetActive(true);
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };
    }
}
