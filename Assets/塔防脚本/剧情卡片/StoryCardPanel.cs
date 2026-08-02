using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 剧情卡片面板：翻页展示所有卡片，未解锁显示锁定。
/// </summary>
public class StoryCardPanel : MonoBehaviour
{
    public static StoryCardPanel Instance { get; private set; }

    [Header("卡片库")]
    public List<StoryCardData> cardDatabase = new List<StoryCardData>();

    [Header("卡片按钮预制体")]
    public GameObject cardButtonPrefab;

    [Header("卡片容器")]
    public Transform cardContainer;

    [Header("分页")]
    [Min(1)] public int cardsPerPage = 8;
    public Button prevPageButton;
    public Button nextPageButton;
    public TMP_Text pageText;

    [Header("卡片尺寸")]
    public Vector2 cardSize = new Vector2(180f, 320f);

    [Header("返回按钮")]
    public Button returnButton;
    public GameObject heldCardsPanelRoot;

    [Header("奖励提示")]
    public TMP_Text rewardToastText;
    public float rewardToastDuration = 2f;
    public string rewardToastFormat = "+{0} 天赋点（{1}）";

    readonly List<StoryCardButton> _buttons = new List<StoryCardButton>();
    readonly List<StoryCardData> _visibleCards = new List<StoryCardData>();
    int _currentPage = 0;
    float _rewardToastTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        TryBindPageControls();
        BindPageButtonEvents();
        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToHeldCards);
        Refresh();
    }

    void OnEnable()
    {
        StoryCardButton.OnRewardGranted += OnCardRewardGranted;
        Refresh();
    }

    void OnDisable() { StoryCardButton.OnRewardGranted -= OnCardRewardGranted; }

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

    public void Refresh()
    {
        Transform root = cardContainer != null ? cardContainer : transform;
        if (root == null) return;
        root.SetAsLastSibling();
        EnsureCanvasScale(root);
        DisableAutoLayout(root);
        CollectExistingButtons(root);

        for (int i = 0; i < _buttons.Count; i++)
            if (_buttons[i] != null)
                _buttons[i].gameObject.SetActive(false);

        RebuildVisibleCards();
        EnsureButtonPoolFixed(root);
        NormalizeAllButtons();
        RenderPage();
    }

    public void UnlockAndShow(string cardId)
    {
        StoryCardUnlockState.Unlock(cardId);
        Refresh();
        gameObject.SetActive(true);
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
        StoryCardUnlockState.CheckAllPending();
        _visibleCards.Clear();
        foreach (var data in cardDatabase)
        {
            if (data != null)
                _visibleCards.Add(data);
        }
        _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, GetPageCount() - 1));
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
            if (btn != null) { btn.gameObject.SetActive(false); Destroy(btn.gameObject); }
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
            prevPageButton = FindButtonByName("上一页按钮");
        if (nextPageButton == null)
            nextPageButton = FindButtonByName("下一页按钮");
        if (pageText == null)
            pageText = FindTextByName("页码文本");
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

    static Button FindButtonByName(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    static TMP_Text FindTextByName(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    GameObject CreateOneButton(Transform parent)
    {
        if (cardButtonPrefab != null)
        {
            var go = Instantiate(cardButtonPrefab, parent, false);
            if (go.GetComponent<StoryCardButton>() == null)
                go.AddComponent<StoryCardButton>();
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) { rt.localPosition = Vector3.zero; rt.localScale = Vector3.one; }
            return go;
        }

        var g = new GameObject("StoryCardButton");
        g.transform.SetParent(parent, false);
        var rect = g.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 80f);
        g.AddComponent<Button>();
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

    void ReturnToHeldCards()
    {
        gameObject.SetActive(false);
        if (heldCardsPanelRoot != null)
            heldCardsPanelRoot.SetActive(true);
    }
}
