using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CreateGameModeDropdown
{
    static readonly string BtnSpritePath = "Assets/杂和卡图案/杂/杂/jimeng-2026-03-12-2146-游戏古风界面的一组游戏风格UI标题框合集，采用闪亮暖暖戏风格，飘逸，精美 的细节....png";

    [MenuItem("Tools/创建关卡模式下拉")]
    public static void Create()
    {
        var canvas = GameObject.Find("UIRoot");
        if (canvas == null) { Debug.LogError("UIRoot not found"); return; }

        var btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BtnSpritePath);
        if (btnSprite == null) { Debug.LogError("按钮贴图未找到: " + BtnSpritePath); return; }

        // 删除旧对象
        var oldLabel = GameObject.Find("GameModeLabel");
        if (oldLabel != null) GameObject.DestroyImmediate(oldLabel);
        var oldDropdown = GameObject.Find("GameModeDropdown");
        if (oldDropdown != null) GameObject.DestroyImmediate(oldDropdown);

        // 与其他按钮相同锚点：左上角 (0,1)
        float width = 260f;
        float height = 54f;

        // ===== 下拉框本体（按钮同款贴图）=====
        var go = new GameObject("GameModeDropdown", typeof(Image), typeof(TMP_Dropdown));
        go.transform.SetParent(canvas.transform, false);
        var dropdown = go.GetComponent<TMP_Dropdown>();
        var dropdownRt = go.GetComponent<RectTransform>();
        dropdownRt.anchorMin = new Vector2(0, 1);
        dropdownRt.anchorMax = new Vector2(0, 1);
        dropdownRt.pivot = new Vector2(0, 0.5f);
        // 60px from left (same as other buttons), -382px from top (between start button and talent tree)
        dropdownRt.anchoredPosition = new Vector2(60, -382);
        dropdownRt.sizeDelta = new Vector2(width, height);
        dropdownRt.localRotation = Quaternion.identity;
        dropdownRt.localScale = Vector3.one;

        var bgImage = go.GetComponent<Image>();
        bgImage.sprite = btnSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = new Color(0.85f, 0.85f, 0.9f, 1f);

        // ===== 静态标签 "模式选择" =====
        var labelGo = new GameObject("ModeLabel", typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelText = labelGo.GetComponent<TextMeshProUGUI>();
        labelText.text = "模式选择";
        labelText.fontSize = 22;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.color = new Color(1f, 0.85f, 0.5f);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0, 0);
        labelRt.anchorMax = new Vector2(1, 1);
        labelRt.pivot = new Vector2(0, 0.5f);
        labelRt.offsetMin = new Vector2(55, 0);
        labelRt.offsetMax = new Vector2(-55, 0);

        // ===== 箭头 =====
        var arrowGo = new GameObject("Arrow", typeof(TextMeshProUGUI));
        arrowGo.transform.SetParent(go.transform, false);
        var arrowText = arrowGo.GetComponent<TextMeshProUGUI>();
        arrowText.text = "▸";
        arrowText.fontSize = 18;
        arrowText.alignment = TextAlignmentOptions.MidlineRight;
        arrowText.color = new Color(1f, 0.85f, 0.5f);
        var arrowRt = arrowGo.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1, 0);
        arrowRt.anchorMax = new Vector2(1, 1);
        arrowRt.pivot = new Vector2(1, 0.5f);
        arrowRt.offsetMin = new Vector2(-45, 0);
        arrowRt.offsetMax = new Vector2(-15, 0);

        // ===== Template（纯色背景 + RectMask2D 裁剪）=====
        var templateGo = new GameObject("Template", typeof(Image));
        templateGo.transform.SetParent(go.transform, false);
        templateGo.SetActive(false);
        var templateRt = templateGo.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0);
        templateRt.anchorMax = new Vector2(1, 0);
        templateRt.pivot = new Vector2(0.5f, 1);
        templateRt.anchoredPosition = new Vector2(0, -3);
        templateRt.sizeDelta = new Vector2(0, 162);

        var templateImg = templateGo.GetComponent<Image>();
        templateImg.color = new Color(0.08f, 0.12f, 0.25f, 0.95f);

        var scrollRect = templateGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var rectMask = templateGo.AddComponent<RectMask2D>();
        rectMask.softness = new Vector2Int(2, 2);

        // ===== Viewport =====
        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(templateGo.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0, 0);
        viewportRt.anchorMax = new Vector2(1, 1);
        viewportRt.pivot = new Vector2(0, 1);
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        scrollRect.viewport = viewportRt;

        // ===== Content =====
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 54);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.spacing = 0;
        var csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.MinSize;
        scrollRect.content = contentRt;

        // ===== Item 模板 =====
        var item = new GameObject("Item", typeof(RectTransform));
        item.transform.SetParent(content.transform, false);
        var itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.pivot = new Vector2(0.5f, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 54);

        // LayoutElement 确保高度
        var le = item.AddComponent<LayoutElement>();
        le.preferredHeight = 54;
        le.minHeight = 54;

        var toggle = item.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;
        var tc = toggle.colors;
        tc.normalColor = new Color(1, 1, 1, 0);
        tc.highlightedColor = new Color(0.3f, 0.5f, 0.9f, 0.3f);
        tc.pressedColor = new Color(0.3f, 0.5f, 0.9f, 0.5f);
        tc.selectedColor = new Color(0.3f, 0.5f, 0.9f, 0.3f);
        tc.disabledColor = new Color(1, 1, 1, 0);
        tc.fadeDuration = 0.12f;
        toggle.colors = tc;

        var itemBg = new GameObject("ItemBackground", typeof(Image));
        itemBg.transform.SetParent(item.transform, false);
        var itemBgImg = itemBg.GetComponent<Image>();
        itemBgImg.color = new Color(1, 1, 1, 0);
        var itemBgRt = itemBg.GetComponent<RectTransform>();
        itemBgRt.anchorMin = new Vector2(0, 0);
        itemBgRt.anchorMax = new Vector2(1, 1);
        itemBgRt.offsetMin = Vector2.zero;
        itemBgRt.offsetMax = Vector2.zero;
        itemBgRt.SetAsFirstSibling();
        toggle.targetGraphic = itemBgImg;

        var itemLabel = new GameObject("ItemLabel", typeof(TextMeshProUGUI));
        itemLabel.transform.SetParent(item.transform, false);
        var itemLabelTmp = itemLabel.GetComponent<TextMeshProUGUI>();
        itemLabelTmp.text = "选项";
        itemLabelTmp.fontSize = 26;
        itemLabelTmp.alignment = TextAlignmentOptions.Center;
        itemLabelTmp.color = new Color(0.9f, 0.85f, 0.7f);
        var itemLabelRt = itemLabel.GetComponent<RectTransform>();
        itemLabelRt.anchorMin = new Vector2(0, 0);
        itemLabelRt.anchorMax = new Vector2(1, 1);
        itemLabelRt.offsetMin = new Vector2(10, 0);
        itemLabelRt.offsetMax = new Vector2(-10, 0);

        // ===== 连接 =====
        dropdown.captionText = null;
        dropdown.itemText = itemLabelTmp;
        dropdown.template = templateRt;

        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("固定模式", null),
            new TMP_Dropdown.OptionData("混合模式", null),
            new TMP_Dropdown.OptionData("随机模式", null),
        });
        dropdown.value = 1;

        // 绑定到 EntryController
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
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ 关卡模式下拉 UI 创建完成！按钮显示'模式选择'，点击展开三个选项。");
    }
}