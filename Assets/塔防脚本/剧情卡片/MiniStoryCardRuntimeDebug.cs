using UnityEngine;
using UnityEngine.UI;

public class MiniStoryCardRuntimeDebug : MonoBehaviour
{
    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 200, 50), "检查卡片问题"))
        {
            DebugAll();
        }
    }

    void DebugAll()
    {
        Debug.Log("===== 开始全面检查 =====");

        var panel = FindObjectOfType<MiniStoryCardPanel>();
        if (panel == null)
        {
            Debug.LogError("找不到 MiniStoryCardPanel！");
            return;
        }
        Debug.Log("找到 MiniStoryCardPanel: " + panel.name);
        Debug.Log("activeSelf: " + panel.gameObject.activeSelf);
        Debug.Log("layer: " + LayerMask.LayerToName(panel.gameObject.layer));

        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("找不到父 Canvas！");
            return;
        }
        Debug.Log("父 Canvas: " + canvas.name);
        Debug.Log("Canvas renderMode: " + canvas.renderMode);
        Debug.Log("Canvas sortingOrder: " + canvas.sortingOrder);
        Debug.Log("Canvas active: " + canvas.gameObject.activeSelf);

        var panelRect = panel.GetComponent<RectTransform>();
        Debug.Log("MiniStoryCardPanel RectTransform:");
        Debug.Log("  anchorMin: " + panelRect.anchorMin);
        Debug.Log("  anchorMax: " + panelRect.anchorMax);
        Debug.Log("  anchoredPosition: " + panelRect.anchoredPosition);
        Debug.Log("  sizeDelta: " + panelRect.sizeDelta);
        Debug.Log("  localPosition: " + panelRect.localPosition);
        Debug.Log("  localScale: " + panelRect.localScale);

        if (panel.cardContainer == null)
        {
            Debug.LogError("cardContainer 是 null！");
            return;
        }
        else
        {
            Debug.Log("cardContainer: " + panel.cardContainer.name);
            Debug.Log("cardContainer active: " + panel.cardContainer.gameObject.activeSelf);
            Debug.Log("cardContainer layer: " + LayerMask.LayerToName(panel.cardContainer.gameObject.layer));
            
            Debug.Log("cardContainer RectTransform:");
            Debug.Log("  anchorMin: " + panel.cardContainer.anchorMin);
            Debug.Log("  anchorMax: " + panel.cardContainer.anchorMax);
            Debug.Log("  anchoredPosition: " + panel.cardContainer.anchoredPosition);
            Debug.Log("  sizeDelta: " + panel.cardContainer.sizeDelta);
            Debug.Log("  localPosition: " + panel.cardContainer.localPosition);
            Debug.Log("  localScale: " + panel.cardContainer.localScale);

            int childCount = panel.cardContainer.childCount;
            Debug.Log("cardContainer 下有 " + childCount + " 个子物体");
            for (int i = 0; i < childCount; i++)
            {
                var child = panel.cardContainer.GetChild(i);
                Debug.Log("  子物体 " + i + ": " + child.name);
                Debug.Log("    active: " + child.gameObject.activeSelf);
                var rect = child.GetComponent<RectTransform>();
                if (rect != null)
                {
                    Debug.Log("    anchoredPosition: " + rect.anchoredPosition);
                    Debug.Log("    sizeDelta: " + rect.sizeDelta);
                    Debug.Log("    localPosition: " + rect.localPosition);
                }
                var img = child.GetComponent<Image>();
                if (img != null)
                {
                    Debug.Log("    Image enabled: " + img.enabled);
                    Debug.Log("    Image sprite: " + (img.sprite != null ? img.sprite.name : "null"));
                    Debug.Log("    Image color: " + img.color);
                }
            }
        }

        Debug.Log("===== 检查完成 =====");
    }
}
