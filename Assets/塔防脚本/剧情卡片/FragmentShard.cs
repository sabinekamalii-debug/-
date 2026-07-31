using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「碎裂之镜」中的单片碎片。
/// 由 StoryMirrorPanel 创建和管理。
/// </summary>
public class FragmentShard : MonoBehaviour
{
    [Header("视觉元素")]
    public Image backgroundImage;
    public Image iconImage;
    public TMP_Text nameText;
    public GameObject lockOverlay;
    public GameObject newBadge;
    public GameObject highlightBorder;
    public TMP_Text hintText; // 锁定状态显示解锁条件提示

    [Header("锁定外观")]
    public Color lockedBgColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
    public Color unlockedBgColor = new Color(0.3f, 0.3f, 0.45f, 0.9f);
    public Color viewedBgColor = new Color(0.25f, 0.25f, 0.35f, 0.7f);

    // ── 数据 ──
    public StoryCardData CardData { get; private set; }
    public StorySetData SetData { get; private set; }
    public Vector2 MirrorPosition { get; private set; }
    public int SetIndex { get; private set; }

    bool _isUnlocked;
    bool _isViewed;
    bool _isZoomed;
    System.Action<FragmentShard> _onClick;

    // ── 脉冲 ──
    float _pulseTimer;
    static readonly Color PulseColorHigh = new Color(1f, 0.85f, 0f, 0.7f);
    static readonly Color PulseColorLow = new Color(1f, 0.85f, 0f, 0.2f);

    void Awake()
    {
        // 自动查找组件（如果没有在 prefab 里绑定）
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        TryAutoBind();

        var btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        btn.onClick.AddListener(HandleClick);
    }

    void TryAutoBind()
    {
        if (lockOverlay == null) lockOverlay = FindChild("LockOverlay");
        if (newBadge == null) newBadge = FindChild("NewBadge");
        if (highlightBorder == null) highlightBorder = FindChild("HighlightBorder");
        if (hintText == null) hintText = FindChildTMP("HintText");
        if (iconImage == null) iconImage = FindChildImage("Icon");
        if (nameText == null) nameText = FindChildTMP("Name");
    }

    void Update()
    {
        // 未观看脉冲高亮
        if (_isUnlocked && !_isViewed && highlightBorder != null && highlightBorder.activeSelf)
        {
            _pulseTimer += Time.unscaledDeltaTime * 1.5f;
            float t = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            var img = highlightBorder.GetComponent<Image>();
            if (img != null)
                img.color = Color.Lerp(PulseColorLow, PulseColorHigh, t);
        }
    }

    /// <summary> 由 StoryMirrorPanel 调用初始化 </summary>
    public void Init(StoryCardData cardData, StorySetData setData, Vector2 mirrorPos, int setIndex, System.Action<FragmentShard> onClick)
    {
        CardData = cardData;
        SetData = setData;
        MirrorPosition = mirrorPos;
        SetIndex = setIndex;
        _onClick = onClick;

        TryAutoBind();

        // 设置镜面位置
        var rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = mirrorPos;
            rt.localScale = Vector3.one;
        }

        RefreshState(
            StoryCardUnlockState.IsUnlocked(cardData.cardId),
            StoryCardUnlockState.IsViewed(cardData.cardId)
        );
    }

    /// <summary> 刷新碎片状态 </summary>
    public void RefreshState(bool unlocked, bool viewed)
    {
        _isUnlocked = unlocked;
        _isViewed = viewed;

        if (!unlocked)
            ApplyLockedState();
        else if (!viewed)
            ApplyNewState();
        else
            ApplyViewedState();
    }

    void ApplyLockedState()
    {
        if (backgroundImage != null)
            backgroundImage.color = lockedBgColor;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            iconImage.gameObject.SetActive(false);
        }

        if (nameText != null)
        {
            nameText.text = "???";
            nameText.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }

        if (lockOverlay != null) lockOverlay.SetActive(true);
        if (newBadge != null) newBadge.SetActive(false);
        if (highlightBorder != null) highlightBorder.SetActive(false);

        // 显示解锁条件提示
        if (hintText != null && CardData != null)
        {
            hintText.text = GetUnlockHint();
            hintText.gameObject.SetActive(true);
        }
    }

    void ApplyNewState()
    {
        if (backgroundImage != null)
            backgroundImage.color = unlockedBgColor;

        if (iconImage != null && CardData != null && CardData.icon != null)
        {
            iconImage.sprite = CardData.icon;
            iconImage.color = Color.white;
            iconImage.gameObject.SetActive(true);
        }

        if (nameText != null && CardData != null)
        {
            nameText.text = CardData.displayName;
            nameText.color = Color.white;
        }

        if (lockOverlay != null) lockOverlay.SetActive(false);
        if (newBadge != null) newBadge.SetActive(true);
        if (highlightBorder != null) highlightBorder.SetActive(true);
        if (hintText != null) hintText.gameObject.SetActive(false);

        _pulseTimer = 0f;
    }

    void ApplyViewedState()
    {
        if (backgroundImage != null)
            backgroundImage.color = viewedBgColor;

        if (iconImage != null && CardData != null && CardData.icon != null)
        {
            iconImage.sprite = CardData.icon;
            iconImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            iconImage.gameObject.SetActive(true);
        }

        if (nameText != null && CardData != null)
        {
            nameText.text = CardData.displayName;
            nameText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        if (lockOverlay != null) lockOverlay.SetActive(false);
        if (newBadge != null) newBadge.SetActive(false);
        if (highlightBorder != null) highlightBorder.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    public void SetZoomed(bool zoomed)
    {
        _isZoomed = zoomed;
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // 简单缩放（实际项目建议用 DOTween 做平滑动画）
        rt.localScale = zoomed ? Vector3.one * 2f : Vector3.one;
    }

    void HandleClick()
    {
        _onClick?.Invoke(this);
    }

    // ── 提示文本 ──

    string GetUnlockHint()
    {
        if (CardData == null) return "未知条件";

        switch (CardData.unlockConditionType)
        {
            case UnlockConditionType.LevelClear:
                return $"通关第{CardData.unlockParam}关后解锁";
            case UnlockConditionType.EliteDefeated:
                return string.IsNullOrEmpty(CardData.unlockParam)
                    ? "击败精英怪物后解锁"
                    : $"击败第{CardData.unlockParam}关精英后解锁";
            case UnlockConditionType.BossDefeated:
                return string.IsNullOrEmpty(CardData.unlockParam)
                    ? "击败Boss后解锁"
                    : $"击败第{CardData.unlockParam}关Boss后解锁";
            case UnlockConditionType.OperatorRecruit:
                return $"招募「{CardData.unlockParam}」后解锁";
            case UnlockConditionType.AdventureChoice:
                return "特定奇遇选择后解锁";
            case UnlockConditionType.FragmentChain:
                return "需要先完成前置碎片";
            case UnlockConditionType.SetComplete:
                return "集齐本套系所有碎片后解锁";
            case UnlockConditionType.FragmentViewed:
                return "需要先观看指定碎片";
            case UnlockConditionType.TotalRuns:
                return $"通关{CardData.unlockParam}局后解锁";
            case UnlockConditionType.GoldReached:
                return $"单局金币≥{CardData.unlockParam}后解锁";
            case UnlockConditionType.NoHitCleared:
                return "无伤通关后解锁";
            case UnlockConditionType.Manual:
            default:
                return "???"; // 隐藏或特殊条件
        }
    }

    // ── 辅助 ──

    GameObject FindChild(string name)
    {
        var t = transform.Find(name);
        return t != null ? t.gameObject : null;
    }

    Image FindChildImage(string name)
    {
        var child = FindChild(name);
        return child != null ? child.GetComponent<Image>() : null;
    }

    TMP_Text FindChildTMP(string name)
    {
        var child = FindChild(name);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
