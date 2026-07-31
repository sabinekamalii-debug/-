using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 剧情卡片呈现状态筛选
/// </summary>
public enum StoryCardStatusFilter
{
    All,        // 全部
    Unlocked,   // 已解锁
    Unviewed    // 未观看
}

/// <summary>
/// 剧情卡片面板：根据「卡片库」「解锁状态」「筛选条件」显示卡片。
/// 未解锁卡片显示灰色 + 🔒 图标，
/// 已解锁未观看显示高亮边框 + NEW 角标，
/// 已观看正常显示。
/// 用法：在场景里做一个 Canvas，挂本脚本，把 cardDatabase 填好；
/// 关卡结束时调用 UnlockAndShow(cardId) 或只 Unlock。
/// </summary>
public class StoryCardPanel : MonoBehaviour
{
    public static StoryCardPanel Instance { get; private set; }

    [Header("卡片库（所有可能发放的卡片，顺序即显示顺序）")]
    public List<StoryCardData> cardDatabase = new List<StoryCardData>();

    [Header("卡片按钮预制体（需带 StoryCardButton；不填则用代码生成简单按钮）")]
    public GameObject cardButtonPrefab;

    [Header("卡片容器（子物体将作为卡片挂载点）")]
    public Transform cardContainer;

    [Header("无卡片时是否隐藏整块面板")]
    public bool hidePanelWhenEmpty = true;

    [Header("分页")]
    [Min(1)] public int cardsPerPage = 8;
    public Button prevPageButton;
    public Button nextPageButton;
    public TMP_Text pageText;

    [Header("分类筛选")]
    [Tooltip("分类筛选按钮容器下的 Toggle；未赋值时自动查找")]
    public ToggleGroup categoryToggleGroup;
    public Toggle toggleAllCategory;
    public Toggle toggleMain;
    public Toggle toggleSide;
    public Toggle toggleCharacter;
    public Toggle toggleEvent;

    [Header("状态筛选")]
    public ToggleGroup statusToggleGroup;
    public Toggle toggleStatusAll;
    public Toggle toggleStatusUnlocked;
    public Toggle toggleStatusUnviewed;

    [Header("卡片尺寸")]
    public Vector2 cardSize = new Vector2(180f, 320f);

    [Header("点击脚本映射")]
    [Tooltip("按卡片序号映射脚本名，例如剧情碎片3 -> 魔王3")]
    public bool useIndexToScriptName = true;
    [Tooltip("脚本名格式，{0} 为卡片序号")]
    public string scriptNameFormat = "魔王{0}";

    [Header("套系（V2 新增）")]
    [Tooltip("剧情套系数据库（用于套系筛选和进度展示）")]
    public List<StorySetData> setDatabase = new List<StorySetData>();

    [Header("返回按钮")]
    [Tooltip("返回天赋卡片面板的按钮")]
    public Button returnButton;
    [Tooltip("返回时显示的面板（天赋卡片面板根节点）")]
    public GameObject heldCardsPanelRoot;

    [Header("奖励提示（V2 新增）")]
    [Tooltip("首次观看碎片时弹出天赋点奖励文本（可选）")]
    public TMP_Text rewardToastText;
    [Tooltip("奖励提示显示持续时间（秒）")]
    public float rewardToastDuration = 2f;
    [Tooltip("奖励提示格式：{0}=天赋点数, {1}=碎片名")]
    public string rewardToastFormat = "+{0} 天赋点（{1}）";

    // ── 筛选状态 ──
    StoryCardCategory? _categoryFilter = null; // null = 全部
    StoryCardStatusFilter _statusFilter = StoryCardStatusFilter.All;
    string _setFilter = ""; // V2: 非空=按套系筛选

    readonly List<StoryCardButton> _buttons = new List<StoryCardButton>();
    readonly List<StoryCardData> _visibleCards = new List<StoryCardData>();
    int _currentPage = 0;
    float _rewardToastTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindReturnButton()
    {
        if (returnButton == null)
        {
            var t = FindChildRecursive(transform, "BackButton");
            if (t != null) returnButton = t.GetComponent<Button>();
        }
        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToHeldCards);
    }

    void ReturnToHeldCards()
    {
        gameObject.SetActive(false);
        if (heldCardsPanelRoot != null)
            heldCardsPanelRoot.SetActive(true);
    }

    void Start()
    {
        TryBindPageControls();
        BindPageButtonEvents();
        TryBindFilterToggles();
        BindReturnButton();
        Refresh();
    }

    void OnEnable()
    {
        StoryCardButton.OnRewardGranted += OnCardRewardGranted;
        Refresh();
    }

    void OnDisable()
    {
        StoryCardButton.OnRewardGranted -= OnCardRewardGranted;
    }

    void Update()
    {
        if (_rewardToastTimer > 0f)
        {
            _rewardToastTimer -= Time.unscaledDeltaTime;
            if (_rewardToastTimer <= 0f && rewardToastText != null)
                rewardToastText.gameObject.SetActive(false);
        }
    }

    void OnCardRewardGranted(StoryCardData cardData, int reward)
    {
        if (rewardToastText != null && cardData != null)
        {
            rewardToastText.text = string.Format(rewardToastFormat, reward, cardData.displayName);
            rewardToastText.gameObject.SetActive(true);
            _rewardToastTimer = rewardToastDuration;
        }
    }

    /// <summary> 刷新列表：按当前筛选条件显示卡片。 </summary>
    public void Refresh()
    {
        Transform root = cardContainer != null ? cardContainer : transform;
        if (root == null) return;
        root.SetAsLastSibling();
        EnsureCanvasScale(root);
        DisableAutoLayout(root);
        CollectExistingButtons(root);
        RebuildVisibleCards();

        if (hidePanelWhenEmpty)
            gameObject.SetActive(_visibleCards.Count > 0);

        EnsureButtonPoolFixed(root);
        NormalizeAllButtons();
        RenderPage();
    }

    /// <summary> 解锁一张卡片并刷新、显示面板。 </summary>
    public void UnlockAndShow(string cardId)
    {
        StoryCardUnlockState.Unlock(cardId);
        Refresh();
        gameObject.SetActive(true);
    }

    /// <summary> 仅解锁，不强制显示。 </summary>
    public void Unlock(string cardId)
    {
        StoryCardUnlockState.Unlock(cardId);
        Refresh();
    }

    public void NextPage()
    {
        int pageCount = GetPageCount();
        if (pageCount <= 1) return;
        _currentPage = Mathf.Min(_currentPage + 1, pageCount - 1);
        RenderPage();
    }

    public void PrevPage()
    {
        int pageCount = GetPageCount();
        if (pageCount <= 1) return;
        _currentPage = Mathf.Max(_currentPage - 1, 0);
        RenderPage();
    }

    // ── 筛选器 ──

    /// <summary> 设置分类筛选 </summary>
    public void SetCategoryFilter(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName) || categoryName == "All")
            _categoryFilter = null;
        else if (Enum.TryParse<StoryCardCategory>(categoryName, out var cat))
            _categoryFilter = cat;
        else
            _categoryFilter = null;

        _currentPage = 0;
        Refresh();
    }

    /// <summary> 设置状态筛选 </summary>
    public void SetStatusFilter(string statusName)
    {
        if (string.IsNullOrEmpty(statusName) || statusName == "All")
            _statusFilter = StoryCardStatusFilter.All;
        else if (statusName == "Unlocked")
            _statusFilter = StoryCardStatusFilter.Unlocked;
        else if (statusName == "Unviewed")
            _statusFilter = StoryCardStatusFilter.Unviewed;

        _currentPage = 0;
        Refresh();
    }

    /// <summary> V2: 设置套系筛选（setId 或 ""=全部） </summary>
    public void SetSetFilter(string setId)
    {
        _setFilter = setId ?? "";
        _currentPage = 0;
        Refresh();
    }

    void OnCategoryToggleChanged(string categoryName)
    {
        SetCategoryFilter(categoryName);
    }

    void OnStatusToggleChanged(string statusName)
    {
        SetStatusFilter(statusName);
    }

    // ── 私有方法 ──

    void CollectExistingButtons(Transform root)
    {
        _buttons.Clear();
        for (int i = 0; i < root.childCount; i++)
        {
            var btn = root.GetChild(i).GetComponent<StoryCardButton>();
            if (btn != null) _buttons.Add(btn);
        }
    }

    void RebuildVisibleCards()
    {
        // V2: 每次刷新时检查是否有因 flag/链条/套系变化而解锁的新碎片
        StoryCardUnlockState.CheckAllPending();

        _visibleCards.Clear();
        int totalCards = cardDatabase.Count;

        for (int i = 0; i < totalCards; i++)
        {
            var data = cardDatabase[i];
            if (data == null) continue;

            // 分类筛选
            if (_categoryFilter != null && data.category != _categoryFilter.Value)
                continue;

            // V2: 套系筛选
            if (!string.IsNullOrEmpty(_setFilter) && data.fragmentSetId != _setFilter)
                continue;

            bool isUnlocked = StoryCardUnlockState.IsUnlocked(data.cardId);

            // 状态筛选
            switch (_statusFilter)
            {
                case StoryCardStatusFilter.Unlocked:
                    if (!isUnlocked) continue;
                    break;
                case StoryCardStatusFilter.Unviewed:
                    if (!isUnlocked) continue;
                    if (StoryCardUnlockState.IsViewed(data.cardId)) continue;
                    break;
                case StoryCardStatusFilter.All:
                default:
                    break;
            }

            _visibleCards.Add(data);
        }

        int pageCount = GetPageCount();
        _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, pageCount - 1));
    }

    void RenderPage()
    {
        for (int i = 0; i < _buttons.Count; i++)
            _buttons[i].gameObject.SetActive(false);

        int pageCount = GetPageCount();
        UpdatePageUi(pageCount);
        if (_visibleCards.Count == 0) return;

        int start = _currentPage * cardsPerPage;
        int end = Mathf.Min(start + cardsPerPage, _visibleCards.Count);
        int count = end - start;
        if (count <= 0) return;

        var rootRect = (cardContainer != null ? cardContainer : transform) as RectTransform;
        var slots = BuildPageSlots(rootRect, count);

        for (int i = 0; i < count && i < _buttons.Count; i++)
        {
            int globalIndex = start + i + 1;
            var btn = _buttons[i];
            var data = _visibleCards[start + i];
            btn.gameObject.SetActive(true);

            bool isUnlocked = StoryCardUnlockState.IsUnlocked(data.cardId);
            bool isViewed = isUnlocked && StoryCardUnlockState.IsViewed(data.cardId);

            btn.SetCardState(data, isUnlocked, isViewed, globalIndex);

            if (isUnlocked && useIndexToScriptName)
                btn.SetRuntimeTarget(string.Format(scriptNameFormat, globalIndex), "");

            var rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = cardSize;
                rt.anchoredPosition = slots[i];
                rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
                rt.localScale = Vector3.one;
            }
        }
    }

    void NormalizeAllButtons()
    {
        foreach (var b in _buttons)
        {
            if (b == null) continue;
            var rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;
            if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;
            if (rt.sizeDelta.x < 1f || rt.sizeDelta.y < 1f) rt.sizeDelta = cardSize;
            var p = rt.localPosition;
            if (Mathf.Abs(p.z) > 0.01f) rt.localPosition = new Vector3(p.x, p.y, 0f);
        }
    }

    int GetPageCount()
    {
        if (cardsPerPage <= 0) cardsPerPage = 8;
        return Mathf.Max(1, Mathf.CeilToInt(_visibleCards.Count / (float)cardsPerPage));
    }

    void UpdatePageUi(int pageCount)
    {
        if (pageText != null)
            pageText.text = $"{_currentPage + 1}/{pageCount}";
        if (prevPageButton != null)
            prevPageButton.interactable = _currentPage > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = _currentPage < pageCount - 1;
    }

    void EnsureButtonPoolFixed(Transform root)
    {
        int target = Mathf.Max(1, cardsPerPage);
        while (_buttons.Count < target)
        {
            var go = CreateOneButton(root);
            var btn = go.GetComponent<StoryCardButton>();
            if (btn != null) _buttons.Add(btn);
        }

        while (_buttons.Count > target)
        {
            int last = _buttons.Count - 1;
            var btn = _buttons[last];
            _buttons.RemoveAt(last);
            if (btn != null) Destroy(btn.gameObject);
        }
    }

    List<Vector2> BuildPageSlots(RectTransform rootRect, int count)
    {
        var result = new List<Vector2>(count);
        float cardW = Mathf.Max(120f, cardSize.x);
        float cardH = Mathf.Max(180f, cardSize.y);

        const float spacingX = 24f;
        const float spacingY = 36f;
        int cols = Mathf.Min(4, Mathf.Max(1, count));
        int rows = count > 4 ? 2 : 1;
        float totalW = cols * cardW + (cols - 1) * spacingX;
        float startX = -totalW * 0.5f + cardW * 0.5f;

        float startY = rows == 2 ? (cardH * 0.5f + spacingY * 0.5f) : 0f;
        for (int i = 0; i < count; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float x = startX + col * (cardW + spacingX);
            float y = startY - row * (cardH + spacingY);
            result.Add(new Vector2(x, y));
        }
        return result;
    }

    void EnsureCanvasScale(Transform root)
    {
        var canvas = root.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRt = canvas.GetComponent<RectTransform>();
        if (canvasRt == null) return;
        if (canvasRt.localScale.x < 0.5f || canvasRt.localScale.y < 0.5f)
            canvasRt.localScale = Vector3.one;
    }

    void DisableAutoLayout(Transform root)
    {
        var grid = root.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.enabled = false;
        var h = root.GetComponent<HorizontalLayoutGroup>();
        if (h != null) h.enabled = false;
        var v = root.GetComponent<VerticalLayoutGroup>();
        if (v != null) v.enabled = false;
        var fitter = root.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
    }

    void TryBindPageControls()
    {
        if (prevPageButton == null)
            prevPageButton = FindButtonByName("上一页按钮") ?? FindButtonByName("PrevPageButton");
        if (nextPageButton == null)
            nextPageButton = FindButtonByName("下一页按钮") ?? FindButtonByName("NextPageButton");
        if (pageText == null)
            pageText = FindTextByName("页码文本") ?? FindTextByName("PageText");
    }

    void BindPageButtonEvents()
    {
        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveListener(PrevPage);
            prevPageButton.onClick.AddListener(PrevPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(NextPage);
            nextPageButton.onClick.AddListener(NextPage);
        }
    }

    void TryBindFilterToggles()
    {
        // 按名称自动查找分类筛选 Toggle
        if (toggleAllCategory == null) toggleAllCategory = FindToggleByName("FilterAllCategory");
        if (toggleAllCategory != null) BindToggle(toggleAllCategory, () => OnCategoryToggleChanged("All"));
        if (toggleMain == null) toggleMain = FindToggleByName("FilterMain");
        if (toggleMain != null) BindToggle(toggleMain, () => OnCategoryToggleChanged("Main"));
        if (toggleSide == null) toggleSide = FindToggleByName("FilterSide");
        if (toggleSide != null) BindToggle(toggleSide, () => OnCategoryToggleChanged("Side"));
        if (toggleCharacter == null) toggleCharacter = FindToggleByName("FilterCharacter");
        if (toggleCharacter != null) BindToggle(toggleCharacter, () => OnCategoryToggleChanged("Character"));
        if (toggleEvent == null) toggleEvent = FindToggleByName("FilterEvent");
        if (toggleEvent != null) BindToggle(toggleEvent, () => OnCategoryToggleChanged("Event"));

        if (categoryToggleGroup == null && toggleAllCategory != null)
            categoryToggleGroup = toggleAllCategory.GetComponentInParent<ToggleGroup>();

        // 状态筛选 Toggle
        if (toggleStatusAll == null) toggleStatusAll = FindToggleByName("FilterStatusAll");
        if (toggleStatusAll != null) BindToggle(toggleStatusAll, () => OnStatusToggleChanged("All"));
        if (toggleStatusUnlocked == null) toggleStatusUnlocked = FindToggleByName("FilterStatusUnlocked");
        if (toggleStatusUnlocked != null) BindToggle(toggleStatusUnlocked, () => OnStatusToggleChanged("Unlocked"));
        if (toggleStatusUnviewed == null) toggleStatusUnviewed = FindToggleByName("FilterStatusUnviewed");
        if (toggleStatusUnviewed != null) BindToggle(toggleStatusUnviewed, () => OnStatusToggleChanged("Unviewed"));

        if (statusToggleGroup == null && toggleStatusAll != null)
            statusToggleGroup = toggleStatusAll.GetComponentInParent<ToggleGroup>();
    }

    void BindToggle(Toggle toggle, UnityEngine.Events.UnityAction action)
    {
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener((isOn) =>
        {
            if (isOn) action();
        });
    }

    static Button FindButtonByName(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return null;
        return go.GetComponent<Button>();
    }

    static TMP_Text FindTextByName(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return null;
        return go.GetComponent<TMP_Text>();
    }

    static Toggle FindToggleByName(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return null;
        return go.GetComponent<Toggle>();
    }

    GameObject CreateOneButton(Transform parent)
    {
        if (cardButtonPrefab != null)
        {
            var go = Instantiate(cardButtonPrefab, parent, false);
            if (go.GetComponent<StoryCardButton>() == null)
                go.AddComponent<StoryCardButton>();
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localPosition = Vector3.zero;
                rt.localScale = Vector3.one;
            }
            return go;
        }

        var g = new GameObject("StoryCardButton");
        g.transform.SetParent(parent, false);

        var rect = g.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 80f);

        var btn = g.AddComponent<Button>();
        var img = g.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);

        var textGo = new GameObject("Name");
        textGo.transform.SetParent(g.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 4f);
        textRect.offsetMax = new Vector2(-4f, -4f);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        var cardBtn = g.AddComponent<StoryCardButton>();
        cardBtn.nameText = tmp;
        return g;
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
