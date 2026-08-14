using UnityEngine;
using TMPro;

/// <summary>
/// 挂在下拉 Content 上，展开后为每项的 Description 设置文字。
/// 每帧检查（开销极小），确保多次展开都能正确设置。
/// </summary>
public class DropdownDescSetter : MonoBehaviour
{
    private static readonly string[] Titles = { "守心模式", "盛怒模式", "苟道模式" };
    private static readonly string[] Descs = {
        "攻防均衡（模式影响剧情）",
        "攻击类卡更多",
        "防御守护类卡更多"
    };

    private void LateUpdate()
    {
        if (!gameObject.activeInHierarchy) return;
        if (transform.childCount < 2) return;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == "Item") continue; // 跳过模板原型

            var titleTr = child.Find("Title");
            if (titleTr == null) continue;
            var titleTmp = titleTr.GetComponent<TextMeshProUGUI>();
            if (titleTmp == null) continue;

            for (int j = 0; j < Titles.Length; j++)
            {
                if (titleTmp.text == Titles[j])
                {
                    var descTr = child.Find("Description");
                    if (descTr != null)
                    {
                        var descTmp = descTr.GetComponent<TextMeshProUGUI>();
                        if (descTmp != null)
                            descTmp.text = Descs[j];
                    }
                    break;
                }
            }
        }
    }
}
