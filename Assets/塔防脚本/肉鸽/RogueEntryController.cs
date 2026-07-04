using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RogueEntry 场景入口控制：
/// - 显示 Available / Run / Permanent
/// - 开始本局
/// - 5:1 点数兑换
/// - 进入收藏页
/// </summary>
public class RogueEntryController : MonoBehaviour
{
    [Header("可选手动绑定（不绑会自动创建简易UI）")]
    [SerializeField] private TMP_Text availablePointText;
    [SerializeField] private TMP_Text runPointText;
    [SerializeField] private TMP_Text permanentPointText;
    [SerializeField] private Button startRunButton;
    [SerializeField] private Button exchangeButton;
    [SerializeField] private Button collectionButton;

    private RogueFlowRouter _flow;

    private void Awake()
    {
        RogueRuntimeState.InitIfNeeded();
        _flow = FindFirstObjectByType<RogueFlowRouter>();
        TryBindByName();
        EnsureSimpleUiIfMissing();
        BindButtons();
        RogueUIUtil.EnsureButtonLabelTmp(startRunButton);
        RogueUIUtil.EnsureButtonLabelTmp(exchangeButton);
        RogueUIUtil.EnsureButtonLabelTmp(collectionButton);
        RogueUIUtil.EnsureButtonsVisible(startRunButton, exchangeButton, collectionButton);
    }

    private void Start()
    {
        RefreshTexts();
        if (RogueRuntimeState.AutoStartBattleOnEntry)
        {
            RogueRuntimeState.AutoStartBattleOnEntry = false;
            StartRun();
        }
    }

    private void BindButtons()
    {
        if (startRunButton != null)
        {
            startRunButton.onClick.RemoveListener(StartRun);
            startRunButton.onClick.AddListener(StartRun);
        }

        if (exchangeButton != null)
        {
            exchangeButton.onClick.RemoveListener(ExchangePoint);
            exchangeButton.onClick.AddListener(ExchangePoint);
        }

        if (collectionButton != null)
        {
            collectionButton.onClick.RemoveListener(OpenCollection);
            collectionButton.onClick.AddListener(OpenCollection);
        }
    }

    public void StartRun()
    {
        RogueRuntimeState.StartRunIfNeeded();
        RefreshTexts();

        if (_flow != null) _flow.EnterBattleFromEntry();
    }

    public void ExchangePoint()
    {
        RogueRuntimeState.TryExchangeAvailableToPermanent();
        RefreshTexts();
    }

    public void OpenCollection()
    {
        if (_flow != null) _flow.EnterCollectionFromEntry();
    }

    private void RefreshTexts()
    {
        if (availablePointText != null)
            availablePointText.text = $"当前可用点数: {RogueRuntimeState.AvailablePoint}";
        if (runPointText != null)
            runPointText.text = $"本局点数: {RogueRuntimeState.RunPoint}";
        if (permanentPointText != null)
            permanentPointText.text = $"永久点数: {RogueRuntimeState.PermanentPoint}";
    }

    private void EnsureSimpleUiIfMissing()
    {
        if (availablePointText != null && runPointText != null && permanentPointText != null
            && startRunButton != null && exchangeButton != null && collectionButton != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("AutoCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }

        var panel = new GameObject("RogueEntry_AutoPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);
        panelRect.sizeDelta = new Vector2(520f, 360f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        availablePointText = CreateText(panel.transform, "可用点文本", new Vector2(20f, -30f));
        runPointText = CreateText(panel.transform, "本局点文本", new Vector2(20f, -80f));
        permanentPointText = CreateText(panel.transform, "永久点文本", new Vector2(20f, -130f));

        startRunButton = CreateButton(panel.transform, "开始本局", new Vector2(20f, -200f), out _);
        exchangeButton = CreateButton(panel.transform, "点数兑换(5:1)", new Vector2(20f, -255f), out _);
        collectionButton = CreateButton(panel.transform, "进入收藏页", new Vector2(20f, -310f), out _);
    }

    private void TryBindByName()
    {
        if (availablePointText == null)
            availablePointText = FindTmpInScene("可用点文本") ?? FindTmpInScene("AvailablePointText");
        if (runPointText == null)
            runPointText = FindTmpInScene("本局点文本") ?? FindTmpInScene("RunPointText");
        if (permanentPointText == null)
            permanentPointText = FindTmpInScene("永久点文本") ?? FindTmpInScene("PermanentPointText");

        if (startRunButton == null)
            startRunButton = FindInScene<Button>("开始本局按钮") ?? FindInScene<Button>("StartRunButton");
        if (exchangeButton == null)
            exchangeButton = FindInScene<Button>("点数兑换按钮") ?? FindInScene<Button>("ExchangeButton");
        if (collectionButton == null)
            collectionButton = FindInScene<Button>("进入收藏页按钮") ?? FindInScene<Button>("CollectionButton");
    }

    private static TMP_Text FindTmpInScene(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) return null;
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp != null) return tmp;

        var legacy = go.GetComponent<Text>();
        if (legacy == null) return null;

        // 自动把旧 Text 升级为 TMP_Text，避免你换新文本后再手动修脚本引用。
        var t = legacy.text;
        var c = legacy.color;
        var a = legacy.alignment;
        var fs = legacy.fontSize;
        DestroyImmediate(legacy, true);
        tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = t;
        tmp.color = c;
        tmp.fontSize = fs;
        tmp.alignment = ConvertAlignment(a);
        return tmp;
    }

    private static T FindInScene<T>(string goName) where T : Component
    {
        var go = GameObject.Find(goName);
        if (go == null) return null;
        return go.GetComponent<T>();
    }

    private static void SetRect(string goName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var go = GameObject.Find(goName);
        if (go == null) return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(460f, 40f);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.text = name;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos, out TMP_Text labelText)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(260f, 42f);
        go.GetComponent<Image>().color = Color.white;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        labelText = textGo.GetComponent<TextMeshProUGUI>();
        labelText.fontSize = 24;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.black;
        labelText.text = label;

        return go.GetComponent<Button>();
    }
}
