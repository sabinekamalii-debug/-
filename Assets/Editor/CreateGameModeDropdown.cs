using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public static class CreateGameModeDropdown
{
    [MenuItem("Tools/创建关卡模式下拉")]
    public static void Create()
    {
        var canvas = GameObject.Find("UIRoot");
        if (canvas == null) { Debug.LogError("UIRoot not found"); return; }

        // Delete old objects
        var oldLabel = GameObject.Find("GameModeLabel");
        if (oldLabel != null) GameObject.DestroyImmediate(oldLabel);
        var oldDropdown = GameObject.Find("GameModeDropdown");
        if (oldDropdown != null) GameObject.DestroyImmediate(oldDropdown);

        // ===== 1. 标签 "关卡模式" =====
        var labelGo = new GameObject("GameModeLabel", typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvas.transform, false);
        var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
        labelTmp.text = "关卡模式";
        labelTmp.fontSize = 28;
        labelTmp.alignment = TextAlignmentOptions.MidlineRight;
        labelTmp.color = new Color(0.85f, 0.75f, 0.55f); // 金色
        labelTmp.fontStyle = FontStyles.Normal;
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(1f, 0.5f);
        labelRt.anchoredPosition = new Vector2(-830, 250);
        labelRt.sizeDelta = new Vector2(120, 32);

        // ===== 2. 下拉框容器 =====
        var go = new GameObject("GameModeDropdown", typeof(Image), typeof(TMP_Dropdown));
        go.transform.SetParent(canvas.transform, false);
        var dropdown = go.GetComponent<TMP_Dropdown>();
        var dropdownRt = go.GetComponent<RectTransform>();
        dropdownRt.anchorMin = new Vector2(0.5f, 0.5f);
        dropdownRt.anchorMax = new Vector2(0.5f, 0.5f);
        dropdownRt.pivot = new Vector2(0f, 0.5f);
        dropdownRt.anchoredPosition = new Vector2(-710, 250);
        dropdownRt.sizeDelta = new Vector2(220, 38);

        // 背景：半透明深蓝 + 金色边框效果
        var bgImage = go.GetComponent<Image>();
        bgImage.color = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        bgImage.type = Image.Type.Sliced;
        // 用一张简单的白色贴图做边框，通过 Outline 组件实现

        // 金色边框用 Outline 组件
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.55f, 0.2f, 0.6f);
        outline.effectDistance = new Vector2(1, 1);

        // 内发光效果用 Shadow
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.3f, 0.2f, 0.05f, 0.3f);
        shadow.effectDistance = new Vector2(0, -1);

        // ===== 3. Caption 文字 =====
        var captionGo = new GameObject("Label", typeof(TextMeshProUGUI));
        captionGo.transform.SetParent(go.transform, false);
        var captionText = captionGo.GetComponent<TextMeshProUGUI>();
        captionText.text = "混合模式";
        captionText.fontSize = 24;
        captionText.alignment = TextAlignmentOptions.MidlineLeft;
        captionText.color = new Color(0.9f, 0.85f, 0.7f);
        var captionRt = captionGo.GetComponent<RectTransform>();
        captionRt.anchorMin = new Vector2(0, 0);
        captionRt.anchorMax = new Vector2(1, 1);
        captionRt.pivot = new Vector2(0.5f, 0.5f);
        captionRt.sizeDelta = new Vector2(-30, 0);
        captionRt.offsetMin = new Vector2(10, 0);
        captionRt.offsetMax = new Vector2(-30, 0);

        // ===== 4. 下拉箭头指示器 =====
        var arrowGo = new GameObject("Arrow", typeof(TextMeshProUGUI));
        arrowGo.transform.SetParent(go.transform, false);
        var arrowTmp = arrowGo.GetComponent<TextMeshProUGUI>();
        arrowTmp.text = "▼";
        arrowTmp.fontSize = 16;
        arrowTmp.alignment = TextAlignmentOptions.MidlineRight;
        arrowTmp.color = new Color(0.7f, 0.55f, 0.2f);
        var arrowRt = arrowGo.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1, 0);
        arrowRt.anchorMax = new Vector2(1, 1);
        arrowRt.pivot = new Vector2(1, 0.5f);
        arrowRt.sizeDelta = new Vector2(25, 0);
        arrowRt.offsetMin = new Vector2(-25, 0);
        arrowRt.offsetMax = new Vector2(0, 0);

        // ===== 5. 下拉列表模板 =====
        var templateGo = new GameObject("Template", typeof(Image), typeof(ScrollRect), typeof(Mask));
        templateGo.transform.SetParent(go.transform, false);
        templateGo.SetActive(false);
        var templateRt = templateGo.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0);
        templateRt.anchorMax = new Vector2(1, 0);
        templateRt.pivot = new Vector2(0.5f, 1);
        templateRt.anchoredPosition = new Vector2(0, -2);
        templateRt.sizeDelta = new Vector2(0, 132);

        var templateImg = templateGo.GetComponent<Image>();
        templateImg.color = new Color(0.06f, 0.1f, 0.2f, 0.95f);
        templateImg.type = Image.Type.Sliced;

        // Template 边框
        var templateOutline = templateGo.AddComponent<Outline>();
        templateOutline.effectColor = new Color(0.5f, 0.4f, 0.15f, 0.5f);
        templateOutline.effectDistance = new Vector2(1, 1);

        var scrollRect = templateGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20;

        var mask = templateGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        // ===== 6. Viewport =====
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask));
        viewport.transform.SetParent(templateGo.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0, 0);
        viewportRt.anchorMax = new Vector2(1, 1);
        viewportRt.pivot = new Vector2(0, 1);
        viewportRt.sizeDelta = new Vector2(0, 0);
        viewportRt.offsetMin = new Vector2(2, 2);
        viewportRt.offsetMax = new Vector2(-2, -2);
        var viewportMask = viewport.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        scrollRect.viewport = viewportRt;

        // ===== 7. Content =====
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = new Vector2(0, 0);
        contentRt.sizeDelta = new Vector2(0, 42);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.spacing = 0;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        var contentCsf = content.GetComponent<ContentSizeFitter>();
        contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        // ===== 8. Item 模板 =====
        var item = new GameObject("Item", typeof(Image), typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        var itemToggle = item.GetComponent<Toggle>();
        var itemImg = item.GetComponent<Image>();
        itemImg.color = new Color(0.1f, 0.15f, 0.28f, 0.9f);
        itemImg.type = Image.Type.Sliced;
        var itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.pivot = new Vector2(0.5f, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 42);

        // 分隔线
        var separator = new GameObject("Separator", typeof(Image));
        separator.transform.SetParent(item.transform, false);
        var sepImg = separator.GetComponent<Image>();
        sepImg.color = new Color(0.4f, 0.3f, 0.1f, 0.3f);
        var sepRt = separator.GetComponent<RectTransform>();
        sepRt.anchorMin = new Vector2(0, 0);
        sepRt.anchorMax = new Vector2(1, 0);
        sepRt.pivot = new Vector2(0.5f, 0);
        sepRt.sizeDelta = new Vector2(0, 1);
        sepRt.offsetMin = new Vector2(5, 0);
        sepRt.offsetMax = new Vector2(-5, 0);

        // Item 文本
        var itemLabel = new GameObject("ItemLabel", typeof(TextMeshProUGUI));
        itemLabel.transform.SetParent(item.transform, false);
        var itemLabelTmp = itemLabel.GetComponent<TextMeshProUGUI>();
        itemLabelTmp.text = "选项";
        itemLabelTmp.fontSize = 22;
        itemLabelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        itemLabelTmp.color = new Color(0.8f, 0.75f, 0.6f);
        var itemLabelRt = itemLabel.GetComponent<RectTransform>();
        itemLabelRt.anchorMin = new Vector2(0, 0);
        itemLabelRt.anchorMax = new Vector2(1, 1);
        itemLabelRt.pivot = new Vector2(0.5f, 0.5f);
        itemLabelRt.sizeDelta = new Vector2(0, 0);
        itemLabelRt.offsetMin = new Vector2(15, 0);
        itemLabelRt.offsetMax = new Vector2(-10, 0);

        // Item 选中高亮
        var highlight = new GameObject("Highlight", typeof(Image));
        highlight.transform.SetParent(item.transform, false);
        var hlImg = highlight.GetComponent<Image>();
        hlImg.color = new Color(0.3f, 0.5f, 0.7f, 0.2f);
        var hlRt = highlight.GetComponent<RectTransform>();
        hlRt.anchorMin = new Vector2(0, 0);
        hlRt.anchorMax = new Vector2(1, 1);
        hlRt.pivot = new Vector2(0.5f, 0.5f);
        hlRt.sizeDelta = new Vector2(0, 0);
        hlRt.offsetMin = new Vector2(0, 0);
        hlRt.offsetMax = new Vector2(0, 0);
        hlRt.SetAsFirstSibling(); // 放在最底层

        // Toggle 配置
        itemToggle.targetGraphic = hlImg;
        var colors = itemToggle.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color(1, 1, 1, 0.1f);
        colors.pressedColor = new Color(1, 1, 1, 0.2f);
        colors.selectedColor = new Color(1, 1, 1, 0.15f);
        colors.disabledColor = new Color(1, 1, 1, 0);
        colors.colorMultiplier = 1;
        itemToggle.colors = colors;
        itemToggle.graphic = hlImg;
        itemToggle.transition = Selectable.Transition.ColorTint;
        itemToggle.isOn = false;

        // ===== 9. 连接引用 =====
        dropdown.captionText = captionText;
        dropdown.itemText = itemLabelTmp;
        dropdown.template = templateRt;

        // 设置选项
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("固定模式", null),
            new TMP_Dropdown.OptionData("混合模式", null),
            new TMP_Dropdown.OptionData("随机模式", null),
        });
        dropdown.value = 1;

        // 同步到 EntryController
        var entryGo = GameObject.Find("EntryController");
        if (entryGo != null)
        {
            var entryController = entryGo.GetComponent("RogueEntryController");
            if (entryController != null)
            {
                var so = new SerializedObject(entryController);
                var field = so.FindProperty("_gameModeDropdown");
                if (field != null)
                {
                    field.objectReferenceValue = dropdown;
                    so.ApplyModifiedProperties();
                }
            }
        }

        Selection.activeGameObject = go;
        // 标记场景脏
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("✅ 关卡模式下拉 UI 创建完成！深蓝背景 + 金色边框，已自动绑定到 EntryController。");
    }
}