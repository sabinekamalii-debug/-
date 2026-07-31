using UnityEngine;
using TMPro;

/// <summary>
/// 挂在下拉 Content 上，当模板被激活创建 Item 后，自动为每项设置描述文字。
/// 用 LateUpdate 轮询检测，设置完成后自毁。
/// </summary>
public class DropdownDescSetter : MonoBehaviour
{
    private static readonly string[] Descriptions = {
        "全部关卡使用设计师手调的固定配置",
        "前5关固定，后段叠加受控随机修饰",
        "全部关卡叠加随机修饰，每局不同体验"
    };

    private bool _done;

    private void LateUpdate()
    {
        if (_done) return;
        // 等至少有 4 个子对象（原型模板 + 3个选项）
        if (transform.childCount < 4) return;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            // 跳过原型模板（Item 名，不是"Item(Clone)"）
            if (child.name == "Item") continue;

            // 找到实际的 Item，通过 Title 文字匹配描述索引
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