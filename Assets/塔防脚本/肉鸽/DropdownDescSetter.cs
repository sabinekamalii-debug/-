using UnityEngine;
using TMPro;

/// <summary>
/// 挂在下拉 Content 上，展开后自动为每项设置描述文字（旁白）。
/// </summary>
public class DropdownDescSetter : MonoBehaviour
{
    private static readonly string[] Descriptions = {
        "（每关固定关卡）",
        "（固定与随机关卡相结合）",
        "（每关随机关卡）"
    };

    private bool _done;

    private void LateUpdate()
    {
        if (_done) return;
        if (transform.childCount < 4) return;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == "Item") continue;

            var titleTr = child.Find("Title");
            if (titleTr == null) continue;
            var titleTmp = titleTr.GetComponent<TextMeshProUGUI>();
            if (titleTmp == null) continue;

            int descIdx = -1;
            if (titleTmp.text == "固定模式") descIdx = 0;
            else if (titleTmp.text == "混合模式") descIdx = 1;
            else if (titleTmp.text == "随机模式") descIdx = 2;

            if (descIdx >= 0)
            {
                var descTr = child.Find("Description");
                if (descTr != null)
                {
                    var descTmp = descTr.GetComponent<TextMeshProUGUI>();
                    if (descTmp != null)
                        descTmp.text = Descriptions[descIdx];
                }
            }
        }

        _done = true;
    }
}