using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情卡收藏页控制（仅展示）：
/// - 读取已解锁卡片 ID
/// - 在 CardGridRoot 下生成简易文本列表
/// - 返回入口
/// </summary>
public class RogueCollectionController : MonoBehaviour
{
    [Header("可选绑定")]
    [SerializeField] private Transform cardGridRoot;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text titleText;

    private RogueFlowRouter _flow;
    private readonly List<TMP_Text> _rows = new List<TMP_Text>();

    private void Awake()
    {
        _flow = FindFirstObjectByType<RogueFlowRouter>();
        TryBindByName();
        EnsureSimpleUiIfMissing();
        ApplyDefaultLayout();
        RogueUIUtil.EnsureButtonLabelTmp(backButton);
        RogueUIUtil.EnsureButtonsVisible(backButton);
        if (backButton != null)
            BindBackButtonOnce(backButton);
    }

    private void Start()
    {
        TryBindBackButtonAgain();
        Refresh();
    }

    /// <summary> Start 时再绑一次返回按钮，避免 Awake 时 UI 未就绪或执行顺序导致没绑上。 </summary>
    private void TryBindBackButtonAgain()
    {
        if (backButton != null)
        {
            if (!backButton.gameObject.activeInHierarchy)
                backButton = null;
            else
            {
                BindBackButtonOnce(backButton);
                return;
            }
        }
        TryBindByName();
        if (backButton != null)
            BindBackButtonOnce(backButton);
    }

    private void BindBackButtonOnce(Button btn)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(BackEntry);
        btn.onClick.AddListener(BackEntry);
        btn.interactable = true;
        if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
    }

    public void Refresh()
    {
        if (cardGridRoot == null) return;
        if (FindFirstObjectByType<StoryCardPanel>() != null) return;

        var unlocked = StoryCardUnlockState.GetUnlockedCardIds();
        EnsureRowCount(unlocked.Count > 0 ? unlocked.Count : 1);

        if (unlocked.Count == 0)
        {
            _rows[0].text = "暂无已解锁剧情卡";
            _rows[0].gameObject.SetActive(true);
            for (int i = 1; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < unlocked.Count; i++)
        {
            _rows[i].text = $"#{i + 1}  剧情碎片{i + 1}  {unlocked[i]}";
            _rows[i].gameObject.SetActive(true);
            var btn = _rows[i].GetComponent<Button>();
            if (btn != null)
            {
                int index = i + 1;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => PlayScriptByFragmentIndex(index));
            }
        }

        for (int i = unlocked.Count; i < _rows.Count; i++)
        {
            _rows[i].gameObject.SetActive(false);
            var btn = _rows[i].GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveAllListeners();
        }
    }

    public void BackEntry()
    {
        if (_flow != null)
            _flow.ReturnEntryFromCollection();
        else
            RogueFlowRouter.ReturnFromCollectionStatic("RogueEntry", "StoryCardCollection");
    }

    private void EnsureRowCount(int count)
    {
        while (_rows.Count < count)
        {
            var rowGo = new GameObject($"CardRow_{_rows.Count + 1}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(TextMeshProUGUI));
            rowGo.transform.SetParent(cardGridRoot, false);
            var img = rowGo.GetComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
            img.raycastTarget = true;
            var text = rowGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(12f, -12f - _rows.Count * 36f);
            rt.sizeDelta = new Vector2(760f, 32f);
            _rows.Add(text);
        }
    }

    /// <summary> 收藏页中点击「剧情碎片N」时跳转 Title 并播放对应 nani 脚本（魔王 N）。 </summary>
    private void PlayScriptByFragmentIndex(int oneBasedIndex)
    {
        if (oneBasedIndex < 1) return;
        string script = "魔王 " + oneBasedIndex;
        // NaninovelReturnRequest.Set(script, ""); // TODO: Naninovel包缺失
        // NaninovelReturnAutoPlayer.Ensure();
        VideoSceneLoader.LoadScene("Title");
    }

    private void EnsureSimpleUiIfMissing()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var c = new GameObject("AutoCanvas");
            canvas = c.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            c.AddComponent<CanvasScaler>();
            c.AddComponent<GraphicRaycaster>();
        }

        if (cardGridRoot == null)
        {
            var panel = new GameObject("CollectionPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 600f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            cardGridRoot = panel.transform;

            if (titleText == null)
            {
                var t = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                t.transform.SetParent(panel.transform, false);
                var text = t.GetComponent<TextMeshProUGUI>();
                text.fontSize = 36;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.text = "剧情卡收藏";
                var tr = t.GetComponent<RectTransform>();
                tr.anchorMin = new Vector2(0f, 1f);
                tr.anchorMax = new Vector2(0f, 1f);
                tr.pivot = new Vector2(0f, 1f);
                tr.anchoredPosition = new Vector2(12f, -12f);
                tr.sizeDelta = new Vector2(420f, 42f);
                titleText = text;
            }
        }

        if (backButton == null)
            backButton = CreateBackButton(canvas.transform);
    }

    private void TryBindByName()
    {
        if (cardGridRoot == null)
        {
            var go = GameObject.Find("CardGridRoot") ?? GameObject.Find("卡片容器");
            if (go != null) cardGridRoot = go.transform;
        }

        if (backButton == null)
        {
            backButton = FindBackButtonUnderUIRoot();
            if (backButton == null)
            {
                backButton = FindInScene<Button>("返回入口按钮") ?? FindInScene<Button>("BackButton")
                    ?? FindInScene<Button>("返回") ?? FindInScene<Button>("返回按钮");
            }
            if (backButton == null)
                backButton = FindBackButtonByTextOrName();
        }

        if (titleText == null)
            titleText = FindTmpInScene("收藏页标题文本") ?? FindTmpInScene("CollectionTitleText");
    }

    /// <summary> 从 UIRoot 下找「返回」按钮，确保绑到画面上那个。 </summary>
    private static Button FindBackButtonUnderUIRoot()
    {
        var uiRoot = GameObject.Find("UIRoot");
        if (uiRoot == null) return null;
        var buttons = uiRoot.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0) return null;
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            if (btn.gameObject.name.Contains("返回") || btn.gameObject.name.Contains("Back"))
                return btn;
            var tmp = btn.GetComponentInChildren<TMP_Text>(true);
            var leg = btn.GetComponentInChildren<Text>(true);
            if (tmp != null && (tmp.text == "返回" || tmp.text == "返回入口")) return btn;
            if (leg != null && (leg.text == "返回" || leg.text == "返回入口")) return btn;
        }
        return null;
    }

    /// <summary> 按名称或子物体文本找「返回」按钮；优先返回在界面里实际显示、可点击的那个。 </summary>
    private static Button FindBackButtonByTextOrName()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (buttons == null || buttons.Length == 0) return null;
        Button activeMatch = null;
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            bool nameMatch = btn.gameObject.name.Contains("返回") || btn.gameObject.name.Contains("Back");
            var tmp = btn.GetComponentInChildren<TMP_Text>(true);
            var leg = btn.GetComponentInChildren<Text>(true);
            bool textMatch = (tmp != null && (tmp.text == "返回" || tmp.text == "返回入口"))
                || (leg != null && (leg.text == "返回" || leg.text == "返回入口"));
            if (!nameMatch && !textMatch) continue;
            if (btn.gameObject.activeInHierarchy)
                return btn;
            if (activeMatch == null)
                activeMatch = btn;
        }
        return activeMatch;
    }

    private void ApplyDefaultLayout()
    {
        // 修复：场景里 UIRoot 可能被存成 scale (0,0,0)，导致 Game 视图整块 UI 不显示
        var uiRoot = GameObject.Find("UIRoot");
        if (uiRoot != null)
        {
            var rt = uiRoot.GetComponent<RectTransform>();
            if (rt != null && rt.localScale == Vector3.zero) rt.localScale = Vector3.one;
        }

        // 保证卡片容器最后渲染，避免被收藏页背景/标题挡住，Game 视图才能看到剧情碎片卡片
        if (cardGridRoot != null) cardGridRoot.SetAsLastSibling();
    }

    private static TMP_Text FindTmpInScene(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) return null;
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp != null) return tmp;
        var legacy = go.GetComponent<Text>();
        if (legacy == null) return null;
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

    private static Button CreateBackButton(Transform parent)
    {
        var go = new GameObject("返回入口", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(20f, 20f);
        rect.sizeDelta = new Vector2(200f, 44f);
        go.GetComponent<Image>().color = Color.white;

        var txt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var txtRect = txt.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        var text = txt.GetComponent<TextMeshProUGUI>();
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        text.text = "返回入口";
        return go.GetComponent<Button>();
    }
}
