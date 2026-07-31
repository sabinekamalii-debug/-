using UnityEngine;
using TMPro;

/// <summary> 挂在下拉 Content 上，每次激活时自动刷新各选项的描述文字。 </summary>
public class DropdownDescSetter : MonoBehaviour
{
    private static readonly string[] Descriptions = {
        "全部关卡使用设计师手调的固定配置",
        "前5关固定，后段叠加受控随机修饰",
        "全部关卡叠加随机修饰，每局不同体验"
    };

    private void OnEnable()
    {
        StartCoroutine(SetDescriptionsNextFrame());
    }

    private System.Collections.IEnumerator SetDescriptionsNextFrame()
    {
        yield return new WaitForEndOfFrame();
        Apply();
    }

    private void Apply()
    {        for (int i = 0; i < transform.childCount && i < Descriptions.Length; i++)
        {
            var descTr = transform.GetChild(i).Find("Description");
            if (descTr != null)
            {
                var descTmp = descTr.GetComponent<TextMeshProUGUI>();
                if (descTmp != null)
                    descTmp.text = Descriptions[i];
            }
        }
    }
}