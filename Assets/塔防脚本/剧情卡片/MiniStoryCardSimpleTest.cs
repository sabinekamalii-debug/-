using UnityEngine;
using UnityEngine.UI;

public class MiniStoryCardSimpleTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== 开始测试 ===");

        var panel = FindObjectOfType<MiniStoryCardPanel>();
        if (panel == null)
        {
            Debug.LogError("找不到 MiniStoryCardPanel！");
            return;
        }

        RectTransform cardContainer = panel.cardContainer;
        if (cardContainer == null)
        {
            Debug.LogError("MiniStoryCardPanel 的 cardContainer 是空的！");
            return;
        }

        Debug.Log("找到 cardContainer: " + cardContainer.name);
        Debug.Log("cardContainer parent: " + cardContainer.parent.name);

        for (int i = 0; i < 2; i++)
        {
            CreateTestCard(cardContainer, i);
        }

        Debug.Log("=== 测试完成 ===");
    }

    void CreateTestCard(RectTransform cardContainer, int index)
    {
        GameObject card = new GameObject("TestCard_" + index);
        card.layer = LayerMask.NameToLayer("UI");
        card.transform.SetParent(cardContainer, false);
        card.transform.SetAsLastSibling();

        RectTransform rect = card.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 300);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        if (index == 0)
        {
            rect.anchoredPosition = new Vector2(-150, 0);
        }
        else
        {
            rect.anchoredPosition = new Vector2(150, 0);
        }

        rect.localScale = Vector3.one;
        rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);

        Image img = card.AddComponent<Image>();
        img.color = index == 0 ? Color.red : Color.blue;

        Debug.Log("创建测试卡片: " + card.name + " 位置: " + rect.anchoredPosition);
    }
}
