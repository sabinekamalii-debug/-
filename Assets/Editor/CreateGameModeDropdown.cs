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
        if (btnSprite == null) { Debug.LogError("按钮贴图未找到"); return; }

        // 删除旧对象
        var oldLabel = GameObject.Find("GameModeLabel");
        if (oldLabel != null) GameObject.DestroyImmediate(oldLabel);
        var oldDropdown = GameObject.Find("GameModeDropdown");
        if (oldDropdown != null) GameObject.DestroyImmediate(oldDropdown);

        float btnW = 260f;
        float btnH = 54f;

        // ===== 下拉框本体 =====
        var go = new GameObject("GameModeDropdown", typeof(Image), typeof(TMP_Dropdown));
        go.transform.SetParent(canvas.transform, false);
        var dropdown = go.GetComponent<TMP_Dropdown>();
        var dropdownRt = go.GetComponent<RectTransform>();
        dropdownRt.anchorMin = new Vector2(0, 1);
        dropdownRt.anchorMax = new Vector2(0, 1);
        dropdownRt.pivot = new Vector2(0, 0.5f);
        dropdownRt.anchoredPosition = new Vector2(60, -382);
        dropdownRt.sizeDelta = new Vector2(btnW, btnH);

        var bgImage = go.GetComponent<Image>();
        bgImage.sprite = btnSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = new Color(0.85f, 0.85f, 0.9f, 1f);

        // Caption
        var captionGo = new GameObject("Caption", typeof(TextMeshProUGUI));
        captionGo.transform.SetParent(go.transform, false);
        var captionText = captionGo.GetComponent<TextMeshProUGUI>();
        captionText.text = "模式选择：混合模式";
        captionText.fontSize = 22;
        captionText.alignment = TextAlignmentOptions.Center;
        captionText.color = new Color(1f, 0.85f, 0.5f);
        var captionRt = captionGo.GetComponent<RectTransform>();
        captionRt.anchorMin = new Vector2(0, 0);
        captionRt.anchorMax = new Vector2(1, 1);
        captionRt.pivot = new Vector2(0.5f, 0.5f);
        captionRt.offsetMin = new Vector2(50, 0);
        captionRt.offsetMax = new Vector2(-50, 0);

        // Arrow
        var arrowGo = new GameObject("Arrow", typeof(TextMeshProUGUI));
        arrowGo.transform.SetParent(go.transform, false);
        var arrowText = arrowGo.GetComponent<TextMeshProUGUI>();
        arrowText.text = "▸";
        arrowText.fontSize = 16;
        arrowText.alignment = TextAlignmentOptions.MidlineRight;
        arrowText.color = new Color(1f, 0.85f, 0.5f);
        var arrowRt = arrowGo.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1, 0);
        arrowRt.anchorMax = new Vector2(1, 1);
        arrowRt.pivot = new Vector2(1, 0.5f);
        arrowRt.offsetMin = new Vector2(-40, 0);
        arrowRt.offsetMax = new Vector2(-12, 0);

        // ===== Template（透明背景，比按钮宽以容纳旁白描述）=====
        var templateGo = new GameObject("Template", typeof(RectTransform));
        templateGo.transform.SetParent(go.transform, false);
        templateGo.SetActive(false);
        var templateRt = templateGo.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0);
        templateRt.anchorMax = new Vector2(0, 0);
        templateRt.pivot = new Vector2(0, 1);
        templateRt.anchoredPosition = new Vector2(0, -3);
        templateRt.sizeDelta = new Vector2(520, 176);

        var scrollRect = templateGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        templateGo.AddComponent<RectMask2D>();

        // Viewport
        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(templateGo.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0, 0);
        viewportRt.anchorMax = new Vector2(1, 1);
        viewportRt.pivot = new Vector2(0, 1);
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        scrollRect.viewport = viewportRt;

        // Content
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, btnH);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.spacing = 4;
        var csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.MinSize;
        content.AddComponent<DropdownDescSetter>();
        scrollRect.content = contentRt;

        // ===== Item 模板（按钮贴图 + 右侧旁白描述）=====
        var item = new GameObject("Item", typeof(RectTransform));
        item.transform.SetParent(content.transform, false);
        var itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.pivot = new Vector2(0, 0.5f);
        itemRt.sizeDelta = new Vector2(0, btnH);
        var le = item.AddComponent<LayoutElement>();
        le.preferredHeight = btnH;

        // --- 按钮贴图部分（左侧 260px）---
        var btnArea = new GameObject("BtnArea", typeof(Image));
        btnArea.transform.SetParent(item.transform, false);
        var btnAreaImg = btnArea.GetComponent<Image>();
        btnAreaImg.sprite = btnSprite;
        btnAreaImg.type = Image.Type.Simple;
        btnAreaImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        var btnAreaRt = btnArea.GetComponent<RectTransform>();
        btnAreaRt.anchorMin = new Vector2(0, 0);
        btnAreaRt.anchorMax = new Vector2(0, 1);
        btnAreaRt.pivot = new Vector2(0, 0.5f);
        btnAreaRt.sizeDelta = new Vector2(btnW, 0);
        btnAreaRt.offsetMin = new Vector2(0, 0);
        btnAreaRt.offsetMax = new Vector2(btnW, 0);

        // Toggle（点整个 Item 都能选）
        var toggle = item.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;
        var tc = toggle.colors;
        tc.normalColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        tc.highlightedColor = new Color(1f, 1f, 1f, 1f);
        tc.pressedColor = new Color(0.7f, 0.8f, 1f, 1f);
        tc.selectedColor = new Color(0.9f, 0.9f, 1f, 1f);
        tc.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        tc.fadeDuration = 0.1f;
        toggle.colors = tc;
        toggle.targetGraphic = btnAreaImg;

        // 标题（模式名称，在按钮贴图范围内居中）
        var titleGo = new GameObject("Title", typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(item.transform, false);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "固定模式";
        titleTmp.fontSize = 24;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.9f, 0.6f);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0);
        titleRt.anchorMax = new Vector2(0, 1);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(btnW - 80, 0);
        titleRt.anchoredPosition = new Vector2(btnW / 2f, 0);

        // 描述（旁白，在按钮右侧）
        var descGo = new GameObject("Description", typeof(TextMeshProUGUI));
        descGo.transform.SetParent(item.transform, false);
        var descTmp = descGo.GetComponent<TextMeshProUGUI>();
        descTmp.text = "";
        descTmp.fontSize = 20;
        descTmp.alignment = TextAlignmentOptions.MidlineLeft;
        descTmp.color = new Color(0.75f, 0.7f, 0.6f);
        var descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0, 0);
        descRt.anchorMax = new Vector2(1, 1);
        descRt.pivot = new Vector2(0, 0.5f);
        descRt.offsetMin = new Vector2(btnW + 12, 0);
        descRt.offsetMax = new Vector2(0, 0);

        // ===== 连接 =====
        dropdown.captionText = null;
        dropdown.itemText = titleTmp;
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

        Debug.Log("✅ 模式选择下拉重建完成。描述在按钮右侧旁白显示。");
    }
}