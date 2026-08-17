using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 职业徽标组件：挂在「职业徽标」预制体/场景对象上。
/// 它是一个真实可编辑对象——符号文本、职业名文本、背景色、边框全在 Inspector 里可见可改，
/// 双击预制体即可编辑样式，不需要运行游戏。
///
/// 运行时由 OperatorCard 调用 Set(OperatorType) 填充符号与职业名
/// （内容来自 OperatorClassBadge 样式表）；样式（底色/描边/字号/颜色）由预制体决定。
/// </summary>
public class OperatorClassBadgeUI : MonoBehaviour
{
    [Header("引用（在预制体里手动拖，或留空按子物体名自动查找）")]
    public Image background;      // 底色：保证文字不被立绘颜色覆盖
    public TMP_Text symbolText;   // 符号字符（如 » ⚔ ■ ⌖ ✶ ✚ ◈）
    public TMP_Text nameText;     // 职业名（如 先锋 / 近卫 / 重装）

    [Header("自动查找配置")]
    [Tooltip("background 为空时：取本物体上的 Image")]
    public bool autoBindBackground = true;
    public string symbolChildName = "符号";
    public string nameChildName = "职业名";

    [Header("运行时行为")]
    [Tooltip("true=Set() 时自动覆盖文本为职业内容；false=保留你在预制体里手动填的文字")]
    public bool autoFillOnSet = true;

    private void Reset() => TryAutoBind();

    private void OnValidate() => TryAutoBind();

    /// <summary> 按子物体名/自身组件自动绑定缺失引用（编辑器友好）。 </summary>
    private void TryAutoBind()
    {
        if (autoBindBackground && background == null)
            background = GetComponent<Image>();

        if (symbolText == null && !string.IsNullOrEmpty(symbolChildName))
        {
            var t = transform.Find(symbolChildName);
            if (t != null) symbolText = t.GetComponent<TMP_Text>();
        }

        if (nameText == null && !string.IsNullOrEmpty(nameChildName))
        {
            var t = transform.Find(nameChildName);
            if (t != null) nameText = t.GetComponent<TMP_Text>();
        }
    }

    /// <summary> 填充符号与职业名。autoFillOnSet=false 时保留手动填写的文字。 </summary>
    public void Set(OperatorData.OperatorType opType)
    {
        if (!autoFillOnSet) return;
        var style = OperatorClassBadge.Get(opType);
        if (symbolText != null) symbolText.text = style.symbol;
        if (nameText != null) nameText.text = style.className;
    }
}
