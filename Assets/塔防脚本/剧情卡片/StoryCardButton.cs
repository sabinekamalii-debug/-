using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Naninovel;

[RequireComponent(typeof(Button))]
public class StoryCardButton : MonoBehaviour
{
    public StoryCardData data;

    /// <summary>
    /// V2: 首次观看碎片发放天赋点奖励时触发（data=卡片数据, reward=天赋点数量）。
    /// StoryCardPanel 或 StoryMirrorPanel 可以订阅此事件弹出浮动提示。
    /// </summary>
    public static System.Action<StoryCardData, int> OnRewardGranted;

    [Header("可选绑定")]
    public Image iconImage;
    public TMP_Text nameText;

    [Header("视觉状态")]
    [Tooltip("锁定覆盖层（自动创建）")]
    public GameObject lockOverlay;
    [Tooltip("NEW 角标（自动创建）")]
    public GameObject newBadge;
    [Tooltip("高亮边框（自动创建）")]
    public GameObject highlightBorder;

    Button _button;
    Image _bgImage;
    Color _originalBgColor = Color.white;

    string _runtimeScriptName;
    string _runtimeLabelName;
    static string PlotSceneName => "Title";

    // ── 脉冲动画 ──
    float _pulseTimer;
    static readonly Color HighlightColor = new Color(1f, 0.85f, 0f, 0.6f);
    static readonly Color HighlightPulseMin = new Color(1f, 0.85f, 0f, 0.25f);

    void Awake()
    {
        TryAutoBind();
        _button = GetComponent<Button>();
        _bgImage = GetComponent<Image>();
        if (_bgImage != null) _originalBgColor = _bgImage.color;

        if (_button != null)
            _button.onClick.AddListener(OnClick);

        EnsureOverlayChildren();
    }

    void Update()
    {
        // 高亮边框脉冲动画
        if (highlightBorder != null && highlightBorder.activeSelf)
        {
            _pulseTimer += Time.unscaledDeltaTime * 1.5f;
            float t = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            var img = highlightBorder.GetComponent<Image>();
            if (img != null)
                img.color = Color.Lerp(HighlightPulseMin, HighlightColor, t);
        }
    }

    void OnValidate() => TryAutoBind();

    void EnsureOverlayChildren()
    {
        // 锁定覆盖层
        if (lockOverlay == null)
        {
            var existing = transform.Find("LockOverlay");
            if (existing != null)
                lockOverlay = existing.gameObject;
            else
                lockOverlay = CreateOverlay("LockOverlay", new Color(0f, 0f, 0f, 0.55f));
        }

        // NEW 角标
        if (newBadge == null)
        {
            var existing = transform.Find("NewBadge");
            if (existing != null)
                newBadge = existing.gameObject;
            else
                newBadge = CreateNewBadge();
        }

        // 高亮边框
        if (highlightBorder == null)
        {
            var existing = transform.Find("HighlightBorder");
            if (existing != null)
                highlightBorder = existing.gameObject;
            else
                highlightBorder = CreateHighlightBorder();
        }
    }

    GameObject CreateOverlay(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        // 锁图标
        var lockTextGo = new GameObject("LockIcon");
        lockTextGo.transform.SetParent(go.transform, false);
        var lockRt = lockTextGo.AddComponent<RectTransform>();
        lockRt.anchorMin = new Vector2(0.5f, 0.5f);
        lockRt.anchorMax = new Vector2(0.5f, 0.5f);
        lockRt.sizeDelta = new Vector2(60f, 60f);
        lockRt.anchoredPosition = Vector2.zero;
        var lockTmp = lockTextGo.AddComponent<TextMeshProUGUI>();
        lockTmp.text = "🔒";
        lockTmp.fontSize = 28f;
        lockTmp.alignment = TextAlignmentOptions.Center;
        lockTmp.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    GameObject CreateNewBadge()
    {
        var go = new GameObject("NewBadge");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-6f, -6f);
        rt.sizeDelta = new Vector2(40f, 24f);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.2f, 0.2f, 1f);
        img.raycastTarget = false;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "NEW";
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    GameObject CreateHighlightBorder()
    {
        var go = new GameObject("HighlightBorder");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-4f, -4f);
        rt.offsetMax = new Vector2(4f, 4f);

        var img = go.AddComponent<Image>();
        img.color = HighlightPulseMin;
        img.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    /// <summary> 设置卡片的外观状态（由 Panel 调用）</summary>
    public void SetCardState(StoryCardData cardData, bool isUnlocked, bool isViewed, int fragmentIndex)
    {
        TryAutoBind();
        EnsureOverlayChildren();

        data = isUnlocked ? cardData : null;
        _runtimeScriptName = null;
        _runtimeLabelName = null;

        if (!isUnlocked)
        {
            ApplyLockedAppearance(cardData);
            return;
        }

        ApplyUnlockedAppearance(fragmentIndex);

        if (!isViewed)
        {
            if (newBadge != null) newBadge.SetActive(true);
            if (highlightBorder != null) highlightBorder.SetActive(true);
            _pulseTimer = 0f;
        }
        else
        {
            if (newBadge != null) newBadge.SetActive(false);
            if (highlightBorder != null) highlightBorder.SetActive(false);
        }

        if (_button != null) _button.interactable = true;
    }

    void ApplyLockedAppearance(StoryCardData cardData)
    {
        if (nameText != null)
        {
            nameText.text = cardData != null ? "???" : "???";
            nameText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        }
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }
        if (_bgImage != null)
            _bgImage.color = new Color(0.25f, 0.25f, 0.25f, 0.5f);

        if (lockOverlay != null) lockOverlay.SetActive(true);
        if (newBadge != null) newBadge.SetActive(false);
        if (highlightBorder != null) highlightBorder.SetActive(false);
        if (_button != null) _button.interactable = false;
    }

    void ApplyUnlockedAppearance(int fragmentIndex)
    {
        if (_bgImage != null) _bgImage.color = _originalBgColor;

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            if (data != null && data.icon != null)
                iconImage.sprite = data.icon;
            iconImage.enabled = true;
        }

        if (nameText != null)
        {
            nameText.text = data != null ? data.displayName : "剧情碎片" + fragmentIndex;
            nameText.color = Color.white;
        }

        if (lockOverlay != null) lockOverlay.SetActive(false);
    }

    // 兼容旧 API
    public void SetData(StoryCardData cardData, int fragmentIndex = 1)
    {
        SetCardState(cardData, true, true, fragmentIndex);
    }

    void OnClick()
    {
        if (data == null) return;

        string script = "";
        string label = "";
        if (data != null)
        {
            script = string.IsNullOrEmpty(_runtimeScriptName) ? data.scriptName : _runtimeScriptName;
            script = ResolveScriptName(script, data);
            label = string.IsNullOrEmpty(_runtimeLabelName) ? data.labelName : _runtimeLabelName;
        }

        if (string.IsNullOrEmpty(script)) return;

        // V2: 标记已观看，获取天赋点奖励（仅首次观看有效）
        int reward = StoryCardUnlockState.MarkViewed(data.cardId);
        if (reward > 0)
        {
            OnRewardGranted?.Invoke(data, reward);
        }

        if (SceneManager.GetActiveScene().name == PlotSceneName && Engine.Initialized)
        {
            var player = Engine.GetService<IScriptPlayer>();
            if (player != null)
            {
                if (string.IsNullOrEmpty(label))
                    player.LoadAndPlay(script).Forget();
                else
                    player.LoadAndPlayAtLabel(script, label).Forget();
            }
        }
        else
        {
            NaninovelReturnRequest.Set(script, label ?? "");
            NaninovelReturnAutoPlayer.Ensure();
            VideoSceneLoader.LoadScene(PlotSceneName);
        }
    }

    string ResolveScriptFromNameText()
    {
        string title = nameText != null ? nameText.text : "";
        if (string.IsNullOrEmpty(title)) return "";
        int value = 0;
        for (int i = 0; i < title.Length; i++)
        {
            char c = title[i];
            if (c < '0' || c > '9') continue;
            value = value * 10 + (c - '0');
        }
        if (value <= 0) return "";
        return $"魔王 {value}";
    }

    static string ResolveScriptName(string script, StoryCardData cardData)
    {
        string s = (script ?? "").Trim();
        string id = cardData != null ? (cardData.cardId ?? "").Trim() : "";
        bool idIsNum = int.TryParse(id, out int idNum);

        if (string.IsNullOrEmpty(s) || s == "plot1")
        {
            if (idIsNum && idNum > 0)
                s = $"魔王 {idNum}";
        }

        if (s.StartsWith("魔王") && !s.StartsWith("魔王 "))
        {
            var suffix = s.Substring(2).Trim();
            if (!string.IsNullOrEmpty(suffix))
                s = $"魔王 {suffix}";
        }

        return s;
    }

    public void SetRuntimeTarget(string scriptName, string labelName = "")
    {
        _runtimeScriptName = scriptName;
        _runtimeLabelName = labelName;
    }

    void TryAutoBind()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
            if (iconImage == null)
            {
                var icon = transform.Find("Icon");
                if (icon != null) iconImage = icon.GetComponent<Image>();
            }
        }

        if (nameText == null)
        {
            var name = transform.Find("Name");
            if (name != null) nameText = name.GetComponent<TMP_Text>();
        }
    }
}
