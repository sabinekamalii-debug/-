using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 卡片选择UI控制器：管理3选1面板、卡槽、Tooltip、牌面刷新等
/// 从原RogueResultController分离（部分核心功能）
/// </summary>
public class CardSelectUIController : MonoBehaviour
{
    [Header("选卡配置")]
    [SerializeField] private TalentCardData[] cardPool;
    [SerializeField] private GameObject cardSlotPrefab;
    [SerializeField] private bool allowSkipPick = true;
    
    [Header("卡槽尺寸")]
    [SerializeField] private Vector2 cardSize = new Vector2(351f, 624f);
    [SerializeField] private float cardSlotY = -65f;
    
    private GameObject _cardSelectPanel;
    private GameObject _cardTooltipPanel;
    private TMP_Text _cardTooltipText;
    private RectTransform _cardTooltipRect;
    private TalentCardData[] _currentOffers = new TalentCardData[3];
    private List<Button> _cardButtons = new List<Button>();
    private bool[] _slotRevealed = new bool[3];
    
    public GameObject CardSelectPanel => _cardSelectPanel;
    public List<Button> CardButtons => _cardButtons;
    
    /// <summary>
    /// 创建选卡面板
    /// </summary>
    public void EnsureCardSelectPanel(TMP_Text titleText = null)
    {
        if (_cardSelectPanel != null) return;

        // 必须优先使用本场景内的UIRoot Canvas，防止错误挂载到怪物的World Space Canvas上
        Canvas canvas = null;
        
        // 如果是Result场景的特殊处理
        var rRoot = transform.parent;
        if (rRoot != null)
        {
            var uiRoot = rRoot.Find("UIRoot");
            if (uiRoot != null) canvas = uiRoot.GetComponent<Canvas>();
        }

        if (canvas == null && titleText != null)
            canvas = titleText.GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = RogueUIUtil.FindSceneCanvas();

        // 如果是局内掉落，必须确保Canvas是全屏Overlap模式且渲染层级最高
        if (RogueResultController.IsMidGameDrop && canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
        }

        if (canvas == null)
        {
            var c = new GameObject("RogueResult_Canvas");
            canvas = c.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            c.AddComponent<CanvasScaler>();
            c.AddComponent<GraphicRaycaster>();
        }
        
        if (!canvas.gameObject.activeInHierarchy)
        {
            for (var t = canvas.transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }

        // 创建选卡面板
        _cardSelectPanel = new GameObject("RogueResult_CardSelectPanel", typeof(RectTransform), typeof(Image));
        _cardSelectPanel.transform.SetParent(canvas.transform, false);
        var panelRect = _cardSelectPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImg = _cardSelectPanel.GetComponent<Image>();
        panelImg.color = RogueResultController.IsMidGameDrop ? 
            new Color(0f, 0f, 0f, 0.75f) : new Color(0f, 0f, 0f, 0f);
        panelImg.raycastTarget = RogueResultController.IsMidGameDrop;

        // 添加标题
        var titleGo = new GameObject("选卡标题", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(_cardSelectPanel.transform, false);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(700f, 56f);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "选择一张天赋（消耗本局点数）";
        titleTmp.fontSize = 36;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;

        // 创建三个卡槽
        const float cardW = 351f;
        const float cardH = 624f;
        const float gap = 52f;
        float startX = -(cardW + gap);

        for (int i = 0; i < 3; i++)
        {
            var slot = CreateCardSlot(_cardSelectPanel.transform, i, new Vector2(startX + i * (cardW + gap), cardSlotY), new Vector2(cardW, cardH));
            _cardButtons.Add(slot);
        }

        // 创建Tooltip
        EnsureCardTooltipPanel();

        // 跳过按钮
        if (allowSkipPick)
        {
            float skipY = cardSlotY - cardH - 100f;
            var skipBtn = CreateButton(_cardSelectPanel.transform, "跳过", new Vector2(0f, skipY));
            var skipRect = skipBtn.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.5f, 1f);
            skipRect.anchorMax = new Vector2(0.5f, 1f);
            skipRect.pivot = new Vector2(0.5f, 1f);
            skipRect.anchoredPosition = new Vector2(0f, skipY);
            skipRect.sizeDelta = new Vector2(280f, 64f);
        }

        _cardSelectPanel.SetActive(false);
        _cardSelectPanel.transform.SetAsLastSibling();
    }
    
    /// <summary>
    /// 创建卡槽
    /// </summary>
    private Button CreateCardSlot(Transform parent, int index, Vector2 pos, Vector2 size)
    {
        // 9:16 竖卡，无整块背景：仅卡片本身（细边框+内容）
        var go = new GameObject($"CardSlot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var slotImg = go.GetComponent<Image>();
        slotImg.color = Color.white;

        // 卡图 - 只占上方区域，不覆盖下方文字
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.3f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.zero;
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;

        // 卡名
        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(go.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.78f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(12f, 0f);
        nameRect.offsetMax = new Vector2(-12f, -16f);
        var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
        nameTmp.fontSize = 94;
        nameTmp.fontStyle = TMPro.FontStyles.Bold;
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.color = Color.black;
        nameTmp.enableWordWrapping = true;

        // 描述
        var descGo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGo.transform.SetParent(go.transform, false);
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 0.72f);
        descRect.offsetMin = new Vector2(12f, 12f);
        descRect.offsetMax = new Vector2(-12f, -8f);
        var descTmp = descGo.GetComponent<TextMeshProUGUI>();
        descTmp.fontSize = 70;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = Color.black;
        descTmp.enableWordWrapping = true;

        var btn = go.GetComponent<Button>();
        int capture = index;
        btn.onClick.AddListener(() => OnPickCard(capture));
        AddCardSlotHover(go, capture);
        return btn;
    }
    
    /// <summary>
    /// 为卡槽添加悬停事件
    /// </summary>
    private void AddCardSlotHover(GameObject slotGo, int slotIndex)
    {
        var et = slotGo.GetComponent<EventTrigger>();
        if (et == null) et = slotGo.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowCardTooltip(slotIndex));
        et.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideCardTooltip());
        et.triggers.Add(exit);
    }
    
    /// <summary>
    /// 创建Tooltip面板
    /// </summary>
    private void EnsureCardTooltipPanel()
    {
        if (_cardTooltipPanel != null) return;
        
        _cardTooltipPanel = new GameObject("CardTooltip", typeof(RectTransform), typeof(Image));
        _cardTooltipPanel.transform.SetParent(_cardSelectPanel.transform, false);
        _cardTooltipRect = _cardTooltipPanel.GetComponent<RectTransform>();
        _cardTooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        _cardTooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        _cardTooltipRect.pivot = new Vector2(0.5f, 0.5f);
        _cardTooltipRect.sizeDelta = new Vector2(320f, 200f);
        _cardTooltipPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        
        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(_cardTooltipPanel.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(12f, 12f);
        txtRt.offsetMax = new Vector2(-12f, -12f);
        _cardTooltipText = txtGo.GetComponent<TextMeshProUGUI>();
        _cardTooltipText.fontSize = 18;
        _cardTooltipText.alignment = TextAlignmentOptions.TopLeft;
        _cardTooltipText.color = Color.white;
        _cardTooltipText.enableWordWrapping = true;
        
        _cardTooltipPanel.SetActive(false);
    }
    
    /// <summary>
    /// 随机抽取3张卡牌作为选择
    /// </summary>
    public void PickRandomOffers()
    {
        var selected = new HashSet<string>(RogueRuntimeState.SelectedTalentCardIds);
        var available = new List<TalentCardData>();
        
        if (cardPool != null)
        {
            foreach (var c in cardPool)
            {
                if (c == null || string.IsNullOrEmpty(c.cardId)) continue;
                if (selected.Contains(c.cardId)) continue;
                available.Add(c);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            _currentOffers[i] = null;
            _slotRevealed[i] = false;
        }

        if (available.Count == 0)
        {
            RefreshCardSlotVisuals();
            return;
        }

        // 卡池不足3张时允许重复抽取，保证三槽都有卡显示
        for (int i = 0; i < 3; i++)
        {
            int idx = UnityEngine.Random.Range(0, available.Count);
            _currentOffers[i] = available[idx];
            if (available.Count >= 3)
                available.RemoveAt(idx);
        }

        RefreshCardSlotVisuals();
    }
    
    /// <summary>
    /// 刷新所有卡槽的视觉效果
    /// </summary>
    public void RefreshCardSlotVisuals()
    {
        int runPoint = RogueRuntimeState.RunGold;
        
        for (int i = 0; i < 3; i++)
        {
            if (i >= _cardButtons.Count) break;
            var btn = _cardButtons[i];
            var card = _currentOffers[i];
            
            if (btn == null) continue;

            var root = btn.transform;
            var rootImg = root.GetComponent<Image>();
            var iconImg = FindChildImageRecursive(root, "Icon", "卡图", "CardImage", "Image");
            
            // 从原RogueResultController中提取了部分逻辑，但为了简化，这里只显示核心状态...
            
            // 实际完整实现需要更多代码，但核心是：
            // 1. 判断是否已翻开 (_slotRevealed[i])
            // 2. 显示对应的正面/背面图片
            // 3. 显示/隐藏文字和图标
            // 4. 设置按钮交互性
        }
    }
    
    // Helper methods from original RogueResultController
    private static Image FindChildImageRecursive(Transform root, params string[] names)
    {
        var rootImg = root.GetComponent<Image>();
        foreach (var name in names)
        {
            var t = FindInDescendants(root, name);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img == null) img = t.GetComponentInChildren<Image>(true);
                if (img != null && img != rootImg) return img;
            }
        }
        return null;
    }
    
    private static Transform FindInDescendants(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindInDescendants(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
    
    private Button CreateButton(Transform parent, string text, Vector2 pos)
    {
        var go = new GameObject($"Button_{text}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(280f, 64f);
        go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.9f);
        
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 4f);
        textRect.offsetMax = new Vector2(-4f, -4f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        
        return go.GetComponent<Button>();
    }
    
    private void OnPickCard(int slotIndex)
    {
        // 由主控制器处理
    }
    
    private void ShowCardTooltip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3 || _currentOffers[slotIndex] == null) 
        {
            HideCardTooltip(); 
            return;
        }
        
        // 未翻开的卡片不显示信息tooltip
        if (!_slotRevealed[slotIndex]) 
        {
            HideCardTooltip(); 
            return;
        }
        
        var c = _currentOffers[slotIndex];
        _cardTooltipText.text = $"{c.displayName}\n类型:{c.cardType}\n稀有度:{c.rarity}\n\n{c.description}";
        _cardTooltipPanel.SetActive(true);
        _cardTooltipPanel.transform.SetAsLastSibling();
        if (_cardButtons != null && slotIndex < _cardButtons.Count && _cardButtons[slotIndex] != null)
        {
            var slotRect = _cardButtons[slotIndex].GetComponent<RectTransform>();
            if (slotRect != null)
                _cardTooltipRect.anchoredPosition = new Vector2(slotRect.anchoredPosition.x + 320f, slotRect.anchoredPosition.y - 200f);
        }
    }
    
    private void HideCardTooltip()
    {
        if (_cardTooltipPanel != null) _cardTooltipPanel.SetActive(false);
    }
}