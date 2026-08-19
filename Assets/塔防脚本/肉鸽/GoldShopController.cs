using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 商店场景控制器（重制版）：
/// 左侧：可滚动的卡牌网格（2列），大字体 + 卡牌类型背景色 + 折扣标签
/// 右侧：商人立绘 + 对话框，进入商店和购买时切换台词
/// - S1：随机货位 — 每次进入商店随机抽取 4~6 张未拥有卡（按稀有度加权）
/// - S2：货位折扣 — 部分卡牌随机打折
/// - S3：付费刷新 — 花费递增金币重摇货位
/// - S4：删卡服务 — 花费金币删除已拥有卡（每店限1次，逐次涨价）
/// - 每种类型最多持有 3 张，超出不可购买
/// - 返回选关场景
/// </summary>
public class GoldShopController : MonoBehaviour
{
    [Header("商人立绘")]
    [Tooltip("商人角色立绘 Sprite")]
    public Sprite merchantSprite;
    [Tooltip("商人立绘 Image 组件名（场景中查找）")]
    private string _merchantImageName = "MerchantImage";

    [Header("卡牌类型图标")]
    public Sprite attackCardIcon;
    public Sprite defenseCardIcon;
    public Sprite guardianCardIcon;
    public Sprite skillCardIcon;
    public Sprite rareCardIcon;
    public Sprite specialCardIcon;

    [Header("UI 色彩配置")]
    public Color rarityCommonColor   = new Color(0.25f, 0.28f, 0.32f, 1f);
    public Color rarityAdvancedColor = new Color(0.18f, 0.22f, 0.38f, 1f);
    public Color rarityRareColor    = new Color(0.28f, 0.16f, 0.32f, 1f);
    public Color rarityLegendaryColor = new Color(0.35f, 0.22f, 0.08f, 1f);
    public Color purchasedColor     = new Color(0.12f, 0.12f, 0.14f, 1f);
    public Color typeFullColor      = new Color(0.35f, 0.14f, 0.14f, 1f);
    public Color cantAffordColor    = new Color(0.10f, 0.10f, 0.12f, 1f);
    public Color removeColor        = new Color(0.38f, 0.14f, 0.16f, 1f);
    public Color convertColor       = new Color(0.38f, 0.30f, 0.10f, 1f);
    public Color refreshColor       = new Color(0.12f, 0.28f, 0.48f, 1f);

    // ── 商人台词 ──
    private static readonly string[] GreetLines =
    {
        "欢迎光临！今天的货品都是精品哦~",
        "看看有没有中意的卡牌吧！",
        "嘿，又来了？随便看看~",
    };
    private static readonly string[] PurchaseLines =
    {
        "好眼光！这张卡很值！",
        "嘿嘿，又赚了一笔~",
        "明智的选择！",
        "这可是限量版哦，别处买不到！",
    };
    private static readonly string[] CantAffordLines =
    {
        "哎呀，金币不够呢...",
        "再多攒攒钱再来吧~",
        "这张卡有点贵，量力而行哦~",
    };
    private static readonly string[] RefreshLines =
    {
        "好嘞，新货品上架！",
        "刚进的新货，看看？",
        "刷新一下心情也好了呢~",
    };
    private static readonly string[] RemoveLines =
    {
        "要舍弃哪张呢？",
        "嗯...删卡服务有限，选好哦~",
    };
    private static readonly string[] EmptyLines =
    {
        "今天的货位空了，要不要刷新一下？",
        "卖完了卖完了！刷新看看新货~",
    };

    // ── 场景对象 ──
    private TMP_Text _goldCountText;
    private GameObject _itemListRoot;
    private Button _backBtn;
    private TMP_Text _titleText;
    private TMP_Text _dialogueText;
    private ScrollRect _scrollRect;
    private Image _merchantImage;

    // ── 删卡服务面板 ──
    private GameObject _removePanelRoot;
    private GameObject _removePanelContent;

    // ── 布局常量 ──
    private const float CardWidth = 560f;
    private const float CardHeight = 200f;
    private const float CardSpacingX = 20f;
    private const float CardSpacingY = 24f;
    private const float HeaderHeight = 50f;
    private const int CardsPerRow = 2;
    private const float LeftPanelPadding = 240f;

    private void Awake()
    {
        RogueRuntimeState.InitIfNeeded();
        BindSceneObjects();
    }

    private void Start()
    {
        // 编辑器下强制输入模块激活，避免 Game 窗口未点击时滚轮无效
        ForceInputModuleActive();

        BindButtons();
        if (RogueRuntimeState.ShopSlotCardIds.Count == 0)
            RogueRuntimeState.RollShopSlots();
        RogueRuntimeState.ResetShopVisitState();
        PopulateShop();
        SetDialogue(GreetLines);
    }

    // ════════════════════════════════════════
    //  场景绑定
    // ════════════════════════════════════════

    private void BindSceneObjects()
    {
        _goldCountText = FindTmp("GoldCount");
        _titleText = FindTmp("ShopTitle");
        if (_titleText != null) _titleText.text = "商店";
        _backBtn = FindButton("Btn_返回入口");
        _dialogueText = FindTmp("DialogueText");
        _merchantImage = FindImage(_merchantImageName);

        var scrollGo = GameObject.Find("ShopScroll");
        if (scrollGo != null)
        {
            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            if (_scrollRect != null)
                _scrollRect.scrollSensitivity = 35f;
            var content = scrollGo.transform.Find("Viewport/Content");
            if (content != null)
            {
                ClearChildren(content);
                _itemListRoot = content.gameObject;
            }
        }

        // fallback: 直接查找 ItemList
        if (_itemListRoot == null)
        {
            var listGo = GameObject.Find("ItemList");
            if (listGo != null)
            {
                ClearChildren(listGo.transform);
                _itemListRoot = listGo;
            }
        }
    }

    private void BindButtons()
    {
        if (_backBtn != null)
        {
            _backBtn.onClick.RemoveAllListeners();
            _backBtn.onClick.AddListener(OnBackClicked);
        }
    }

    // ════════════════════════════════════════
    //  商人对话
    // ════════════════════════════════════════

    private void SetDialogue(string[] lines)
    {
        if (lines == null || lines.Length == 0) return;
        SetDialogue(lines[Random.Range(0, lines.Length)]);
    }

    private void SetDialogue(string text)
    {
        if (_dialogueText != null)
            _dialogueText.text = text;
    }

    // ════════════════════════════════════════
    //  填充商店
    // ════════════════════════════════════════

    private void PopulateShop()
    {
        if (_itemListRoot != null)
            ClearChildren(_itemListRoot.transform);

        RefreshGoldText();
        if (_itemListRoot == null) return;

        float yPos = 0f;

        // S3：刷新按钮（占满一行）
        int refreshCost = RogueRuntimeState.ShopRefreshCost;
        bool canRefresh = RogueRuntimeState.RunGold >= refreshCost;
        var refreshCard = CreateShopCard(_itemListRoot.transform, "Btn_Refresh",
            new Vector2(-CardWidth / 2f - CardSpacingX / 2f, yPos),
            "刷新货位",
            $"花费 {refreshCost} 金币重新随机货位",
            refreshCost, "刷新",
            canRefresh ? refreshColor : cantAffordColor,
            canRefresh, CardAction.Refresh, null);
        yPos -= CardHeight + CardSpacingY;

        if (canRefresh)
        {
            refreshCard.GetComponent<Button>().onClick.RemoveAllListeners();
            refreshCard.GetComponent<Button>().onClick.AddListener(OnRefreshClicked);
        }

        // S1：随机货位卡牌
        var shopCards = RogueRuntimeState.GetShopSlotCards();

        // 货位标题
        yPos = CreateSectionHeader(_itemListRoot.transform, "Header_Slots",
            yPos, $"── 随机货位（{shopCards.Count} 张） ──");

        int col = 0;
        foreach (var card in shopCards)
        {
            float xPos = (col == 0)
                ? -CardWidth / 2f - CardSpacingX / 2f
                : CardWidth / 2f + CardSpacingX / 2f;

            bool alreadyOwned = RogueRuntimeState.IsCardOwned(card.cardId);
            bool typeFull = !RogueRuntimeState.CanAcquireCard(card);
            int price = RogueRuntimeState.GetCardShopPrice(card);
            bool canAfford = RogueRuntimeState.RunGold >= price;

            // 折扣信息
            string discountTag = "";
            if (card.slotDiscount > 0f && card.slotDiscount < 1f)
            {
                int zhe = Mathf.RoundToInt(card.slotDiscount * 10f);
                discountTag = $"  [{zhe}折!]";
            }

            // 类型持有信息
            int ownedCount = RogueRuntimeState.GetOwnedCardCountByType(card.cardType);
            string typeInfo = $"{GoldShopConfig.GetTypeDisplayName(card.cardType)} {ownedCount}/{BalanceConfig.CardTypeLimit}";

            string label = $"{card.displayName}{discountTag}";
            string desc = $"{card.description}";
            string meta = $"{GoldShopConfig.GetRarityDisplayName(card.rarity)} | {typeInfo} | {price} 金币";

            Color cardColor;
            bool interactable;
            string btnText;
            CardAction action = CardAction.Buy;

            if (alreadyOwned)
            {
                cardColor = purchasedColor;
                interactable = false;
                btnText = "已购买";
            }
            else if (typeFull)
            {
                cardColor = typeFullColor;
                interactable = false;
                btnText = "已达上限";
            }
            else if (canAfford)
            {
                cardColor = GetRarityColor(card.rarity);
                interactable = true;
                btnText = "购买";
            }
            else
            {
                cardColor = cantAffordColor;
                interactable = false;
                btnText = "金币不足";
            }

            var cardGo = CreateShopCard(_itemListRoot.transform, $"Card_{card.cardId}",
                new Vector2(xPos, yPos), label, desc, price, btnText,
                cardColor, interactable, action, card);
            var metaTmp = cardGo.transform.Find("TextArea/MetaText")?.GetComponent<TextMeshProUGUI>();
            if (metaTmp != null) metaTmp.text = meta;

            if (interactable)
            {
                var btn = cardGo.GetComponent<Button>();
                TalentCardData captured = card;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnPurchaseCard(captured));
            }

            col = (col + 1) % CardsPerRow;
            if (col == 0) yPos -= CardHeight + CardSpacingY;
        }

        // 如果奇数张卡，补齐行
        if (col != 0)
        {
            yPos -= CardHeight + CardSpacingY;
        }

        // S4：删卡服务按钮（点击打开独立面板）—— 仅当拥有可删除的星卡时显示
        bool hasRemovableCards = false;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var c = TalentEffectApplier.GetCardById(id);
            if (c != null && c.cardTier != CardTier.Dream) { hasRemovableCards = true; break; }
        }
        if (hasRemovableCards)
        {
            int removalCost = RogueRuntimeState.CardRemovalCost;
            bool canRemove = RogueRuntimeState.CanRemoveCard;

            string removalHeader = canRemove
                ? $"── 删卡服务（本次 {removalCost} 金币，限 {BalanceConfig.CardRemovalLimitPerVisit} 次） ──"
                : "── 删卡服务（本店已用完） ──";
            yPos = CreateSectionHeader(_itemListRoot.transform, "Header_Remove", yPos, removalHeader);

            var removeBtnGo = CreateShopCard(_itemListRoot.transform, "Btn_RemoveService",
                new Vector2(-CardWidth / 2f - CardSpacingX / 2f, yPos),
                "进入删卡服务",
                "点击查看已拥有卡牌，选择要删除的卡",
                removalCost, canRemove ? "进入" : "已用完",
                canRemove ? removeColor : cantAffordColor,
                canRemove, CardAction.Remove, null);
            yPos -= CardHeight + CardSpacingY;

            if (canRemove)
            {
                removeBtnGo.GetComponent<Button>().onClick.RemoveAllListeners();
                removeBtnGo.GetComponent<Button>().onClick.AddListener(ShowRemovePanel);
            }
        }

        // spc_convert：卡牌转化
        if (RogueRuntimeState.CanConvertCard)
        {
            var owned = new List<TalentCardData>();
            foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
            {
                var c = TalentEffectApplier.GetCardById(id);
                if (c != null) owned.Add(c);
            }
            if (owned.Count > 0)
            {
                yPos = CreateSectionHeader(_itemListRoot.transform, "Header_Convert",
                    yPos, "── 卡牌转化（点击转化为金币） ──");

                col = 0;
                foreach (var card in owned)
                {
                    // 梦卡不可转化
                    if (card.cardTier == CardTier.Dream) continue;
                    int value = RogueRuntimeState.GetCardShopPrice(card);
                    float xPos = (col == 0)
                        ? -CardWidth / 2f - CardSpacingX / 2f
                        : CardWidth / 2f + CardSpacingX / 2f;

                    var cardGo = CreateShopCard(_itemListRoot.transform, $"Convert_{card.cardId}",
                        new Vector2(xPos, yPos), card.displayName, card.description, value, "转化",
                        convertColor, true, CardAction.Convert, card);
                    var metaTmp3 = cardGo.transform.Find("TextArea/MetaText")?.GetComponent<TextMeshProUGUI>();
                    if (metaTmp3 != null) metaTmp3.text = $"转化为 {value} 金币";

                    var btn = cardGo.GetComponent<Button>();
                    TalentCardData captured = card;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnConvertCard(captured));

                    col = (col + 1) % CardsPerRow;
                    if (col == 0) yPos -= CardHeight + CardSpacingY;
                }

                if (col != 0) yPos -= CardHeight + CardSpacingY;
            }
        }

        // 空货位提示
        if (shopCards.Count == 0)
        {
            yPos = CreateSectionHeader(_itemListRoot.transform, "EmptyHint", yPos,
                "货位为空，点击刷新获取新卡！");
            SetDialogue(EmptyLines);
        }

        // 设置 Content 高度
        var contentRect = _itemListRoot.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            float totalHeight = -yPos + LeftPanelPadding;
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        }
    }

    // ════════════════════════════════════════
    //  删卡服务面板
    // ════════════════════════════════════════

    private void ShowRemovePanel()
    {
        if (_removePanelRoot != null)
        {
            _removePanelRoot.SetActive(true);
            PopulateRemovePanel();
            return;
        }

        var canvas = GameObject.Find("GoldShopCanvas");
        if (canvas == null) return;

        _removePanelRoot = CreateRemovePanelRoot(canvas.transform);
        PopulateRemovePanel();
        SetDialogue(RemoveLines);
    }

    private void HideRemovePanel()
    {
        if (_removePanelRoot != null)
            _removePanelRoot.SetActive(false);
        SetDialogue(GreetLines);
    }

    private void PopulateRemovePanel()
    {
        if (_removePanelContent == null) return;
        ClearChildren(_removePanelContent.transform);

        int removalCost = RogueRuntimeState.CardRemovalCost;
        bool canRemove = RogueRuntimeState.CanRemoveCard;
        bool canAffordRemove = RogueRuntimeState.RunGold >= removalCost;

        var owned = new List<TalentCardData>();
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var c = TalentEffectApplier.GetCardById(id);
            // 梦卡不可删除，不显示在删卡面板
            if (c != null && c.cardTier != CardTier.Dream) owned.Add(c);
        }

        float yPos = 0f;
        int col = 0;

        foreach (var card in owned)
        {
            float xPos = (col == 0)
                ? -CardWidth / 2f - CardSpacingX / 2f
                : CardWidth / 2f + CardSpacingX / 2f;

            int typeCount = RogueRuntimeState.GetOwnedCardCountByType(card.cardType);
            string typeInfo = $"{GoldShopConfig.GetTypeDisplayName(card.cardType)} {typeCount}/{BalanceConfig.CardTypeLimit}";
            string meta = canRemove
                ? $"{GoldShopConfig.GetRarityDisplayName(card.rarity)} | {typeInfo} | 删除 {removalCost} 金币"
                : $"{GoldShopConfig.GetRarityDisplayName(card.rarity)} | {typeInfo}";

            Color cardColor;
            bool interactable;
            string btnText;

            if (!canRemove)
            {
                cardColor = cantAffordColor;
                interactable = false;
                btnText = "已用完";
            }
            else if (!canAffordRemove)
            {
                cardColor = cantAffordColor;
                interactable = false;
                btnText = "金币不足";
            }
            else
            {
                cardColor = removeColor;
                interactable = true;
                btnText = "删除";
            }

            var cardGo = CreateShopCard(_removePanelContent.transform, $"Remove_{card.cardId}",
                new Vector2(xPos, yPos), card.displayName, card.description, removalCost, btnText,
                cardColor, interactable, CardAction.Remove, card);
            var metaTmp = cardGo.transform.Find("TextArea/MetaText")?.GetComponent<TextMeshProUGUI>();
            if (metaTmp != null) metaTmp.text = meta;

            if (interactable)
            {
                var btn = cardGo.GetComponent<Button>();
                TalentCardData captured = card;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnRemoveCard(captured));
            }

            col = (col + 1) % CardsPerRow;
            if (col == 0) yPos -= CardHeight + CardSpacingY;
        }

        if (col != 0) yPos -= CardHeight + CardSpacingY;

        var contentRect = _removePanelContent.GetComponent<RectTransform>();
        if (contentRect != null)
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, -yPos + 120f);
    }

    private GameObject CreateRemovePanelRoot(Transform parent)
    {
        // ── 全屏覆盖层 ──
        var panel = new GameObject("RemovePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        panelImage.raycastTarget = true;

        // ── 顶部栏 ──
        var topBar = new GameObject("TopBar", typeof(RectTransform));
        topBar.transform.SetParent(panel.transform, false);
        var topBarRect = topBar.GetComponent<RectTransform>();
        topBarRect.anchorMin = new Vector2(0.5f, 1f);
        topBarRect.anchorMax = new Vector2(0.5f, 1f);
        topBarRect.pivot = new Vector2(0.5f, 1f);
        topBarRect.anchoredPosition = new Vector2(0f, -60f);
        topBarRect.sizeDelta = new Vector2(1200f, 80f);

        // 标题
        var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(topBar.transform, false);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(400f, 60f);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "删卡服务";
        titleTmp.fontSize = 36;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.95f, 0.7f, 1f);

        // 返回按钮
        var backGo = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
        backGo.transform.SetParent(topBar.transform, false);
        var backRect = backGo.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0.5f);
        backRect.anchorMax = new Vector2(0f, 0.5f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = new Vector2(20f, 0f);
        backRect.sizeDelta = new Vector2(160f, 60f);
        var backImage = backGo.GetComponent<Image>();
        backImage.color = new Color(0.5f, 0.2f, 0.2f, 1f);
        backImage.type = Image.Type.Sliced;
        var backBtn = backGo.GetComponent<Button>();
        backBtn.onClick.AddListener(HideRemovePanel);

        var backTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        backTextGo.transform.SetParent(backGo.transform, false);
        var backTextRect = backTextGo.GetComponent<RectTransform>();
        backTextRect.anchorMin = Vector2.zero;
        backTextRect.anchorMax = Vector2.one;
        backTextRect.offsetMin = Vector2.zero;
        backTextRect.offsetMax = Vector2.zero;
        var backText = backTextGo.GetComponent<TextMeshProUGUI>();
        backText.text = "← 返回商店";
        backText.fontSize = 26;
        backText.fontStyle = FontStyles.Bold;
        backText.alignment = TextAlignmentOptions.Center;
        backText.color = Color.white;

        // 金币显示
        var goldGo = new GameObject("GoldText", typeof(RectTransform), typeof(TextMeshProUGUI));
        goldGo.transform.SetParent(topBar.transform, false);
        var goldRect = goldGo.GetComponent<RectTransform>();
        goldRect.anchorMin = new Vector2(1f, 0.5f);
        goldRect.anchorMax = new Vector2(1f, 0.5f);
        goldRect.pivot = new Vector2(1f, 0.5f);
        goldRect.anchoredPosition = new Vector2(-20f, 0f);
        goldRect.sizeDelta = new Vector2(300f, 60f);
        var goldTmp = goldGo.GetComponent<TextMeshProUGUI>();
        goldTmp.text = $"金币: {RogueRuntimeState.RunGold}";
        goldTmp.fontSize = 28;
        goldTmp.fontStyle = FontStyles.Bold;
        goldTmp.alignment = TextAlignmentOptions.Right;
        goldTmp.color = new Color(1f, 0.85f, 0.3f, 1f);

        // ── 滚动区域 ──
        var scrollGo = new GameObject("RemoveScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGo.transform.SetParent(panel.transform, false);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(0f, 0f);
        scrollRect.offsetMax = new Vector2(0f, -140f);
        var scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.vertical = true;
        sr.horizontal = false;
        sr.scrollSensitivity = 35f;

        // Viewport
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
        viewport.transform.SetParent(scrollGo.transform, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        var viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        var viewportMask = viewport.AddComponent<RectMask2D>();

        sr.viewport = viewportRect;

        // Content
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(CardWidth * 2f + CardSpacingX + 40f, 0f);

        sr.content = contentRect;

        _removePanelContent = content.gameObject;

        return panel;
    }

    // ════════════════════════════════════════
    //  事件回调
    // ════════════════════════════════════════

    private void OnRefreshClicked()
    {
        if (RogueRuntimeState.TryRefreshShop())
        {
            PopulateShop();
            SetDialogue(RefreshLines);
        }
    }

    private void OnPurchaseCard(TalentCardData card)
    {
        if (RogueRuntimeState.TryPurchaseCard(card))
        {
            PopulateShop();
            SetDialogue(PurchaseLines);
        }
        else
        {
            SetDialogue(CantAffordLines);
        }
    }

    private void OnConvertCard(TalentCardData card)
    {
        if (RogueRuntimeState.ConvertOwnedCardToGold(card))
        {
            PopulateShop();
            SetDialogue(PurchaseLines);
        }
    }

    private void OnRemoveCard(TalentCardData card)
    {
        if (RogueRuntimeState.TryRemoveCard(card))
        {
            PopulateShop();
            // 如果删卡面板开着，刷新它
            if (_removePanelRoot != null && _removePanelRoot.activeSelf)
                PopulateRemovePanel();
            SetDialogue(RemoveLines);
        }
    }

    private void RefreshGoldText()
    {
        if (_goldCountText != null)
            _goldCountText.text = $"金币: {RogueRuntimeState.RunGold}";
    }

    private void OnBackClicked()
    {
        int shopLevel = ShopReturnContext.GetAndClear();
        if (shopLevel > 0)
        {
            LevelProgress.MarkCompleted("level " + shopLevel);
        }
        LevelSceneLoadContext.SetFromVictory();
        VideoSceneLoader.LoadScene(SceneNames.Plot);
    }

    // ════════════════════════════════════════
    //  UI 辅助方法
    // ════════════════════════════════════════

    private enum CardAction { Buy, Remove, Convert, Refresh }

    private Color GetRarityColor(TalentCardRarity rarity)
    {
        switch (rarity)
        {
            case TalentCardRarity.Common:    return rarityCommonColor;
            case TalentCardRarity.Advanced:  return rarityAdvancedColor;
            case TalentCardRarity.Rare:      return rarityRareColor;
            case TalentCardRarity.Legendary: return rarityLegendaryColor;
            default: return rarityCommonColor;
        }
    }

    private Sprite GetTypeIcon(TalentCardType cardType)
    {
        switch (cardType)
        {
            case TalentCardType.Attack:   return attackCardIcon;
            case TalentCardType.Defense:  return defenseCardIcon;
            case TalentCardType.Guardian: return guardianCardIcon;
            case TalentCardType.Skill:    return skillCardIcon;
            case TalentCardType.Rare:     return rareCardIcon;
            case TalentCardType.Special:  return specialCardIcon;
            default: return null;
        }
    }

    /// <summary>
    /// 创建一张商店卡牌（宽 700 × 高 180）。
    /// 布局：
    ///   [图标 120×120]  [名称 (fontSize 28 Bold)]              [价格按钮]
    ///                   [描述 (fontSize 18)]                   [按钮文字]
    ///                   [MetaText (fontSize 16, 稀有度|类型|价格)]
    /// </summary>
    private GameObject CreateShopCard(Transform parent, string name, Vector2 anchoredPos,
        string title, string description, int price, string btnText,
        Color bgColor, bool interactable, CardAction action, TalentCardData cardData)
    {
        // ── 卡牌根 ──
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);

        var bgImage = go.GetComponent<Image>();
        bgImage.color = bgColor;
        bgImage.type = Image.Type.Sliced;

        var btn = go.GetComponent<Button>();
        btn.interactable = interactable;

        // ── 卡牌类型图标（左侧 100×100） ──
        var iconGo = new GameObject("CardIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(20f, 0f);
        iconRect.sizeDelta = new Vector2(120f, 120f);
        var iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;
        if (cardData != null)
        {
            var typeIcon = GetTypeIcon(cardData.cardType);
            if (typeIcon != null)
            {
                iconImage.sprite = typeIcon;
                iconImage.color = Color.white;
            }
            else if (cardData.icon != null)
            {
                iconImage.sprite = cardData.icon;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.color = new Color(1f, 1f, 1f, 0.15f);
            }
        }
        else
        {
            iconImage.color = new Color(1f, 1f, 1f, 0.15f);
        }

        // ── 文字区域（中间） ──
        var textAreaGo = new GameObject("TextArea", typeof(RectTransform));
        textAreaGo.transform.SetParent(go.transform, false);
        var textAreaRect = textAreaGo.GetComponent<RectTransform>();
        textAreaRect.anchorMin = new Vector2(0f, 0f);
        textAreaRect.anchorMax = new Vector2(1f, 1f);
        textAreaRect.offsetMin = new Vector2(160f, 12f);
        textAreaRect.offsetMax = new Vector2(-165f, -12f);

        // 卡牌名称（顶部）
        var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(textAreaGo.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(0f, 42f);
        var nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = title;
        nameText.fontSize = 28;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.color = Color.white;
        nameText.enableWordWrapping = true;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        // 卡牌描述（中间）
        var descGo = new GameObject("DescText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGo.transform.SetParent(textAreaGo.transform, false);
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.offsetMin = new Vector2(0f, 28f);
        descRect.offsetMax = new Vector2(0f, -48f);
        var descText = descGo.GetComponent<TextMeshProUGUI>();
        descText.text = description;
        descText.fontSize = 20;
        descText.alignment = TextAlignmentOptions.TopLeft;
        descText.color = new Color(0.85f, 0.85f, 0.92f, 1f);
        descText.enableWordWrapping = true;
        descText.overflowMode = TextOverflowModes.Ellipsis;

        // Meta 信息（底部）
        var metaGo = new GameObject("MetaText", typeof(RectTransform), typeof(TextMeshProUGUI));
        metaGo.transform.SetParent(textAreaGo.transform, false);
        var metaRect = metaGo.GetComponent<RectTransform>();
        metaRect.anchorMin = new Vector2(0f, 0f);
        metaRect.anchorMax = new Vector2(1f, 0f);
        metaRect.pivot = new Vector2(0f, 0f);
        metaRect.anchoredPosition = Vector2.zero;
        metaRect.sizeDelta = new Vector2(0f, 24f);
        var metaText = metaGo.GetComponent<TextMeshProUGUI>();
        metaText.fontSize = 17;
        metaText.alignment = TextAlignmentOptions.BottomLeft;
        metaText.color = new Color(0.95f, 0.85f, 0.45f, 1f);

        // ── 购买/操作按钮（右侧） ──
        var buyGo = new GameObject("BuyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        buyGo.transform.SetParent(go.transform, false);
        var buyRect = buyGo.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(1f, 0.5f);
        buyRect.anchorMax = new Vector2(1f, 0.5f);
        buyRect.pivot = new Vector2(1f, 0.5f);
        buyRect.anchoredPosition = new Vector2(-15f, 0f);
        buyRect.sizeDelta = new Vector2(140f, 90f);
        var buyImage = buyGo.GetComponent<Image>();
        buyImage.color = interactable
            ? new Color(0.20f, 0.55f, 0.25f, 1f)
            : new Color(0.15f, 0.15f, 0.18f, 0.8f);

        var buyBtn = buyGo.GetComponent<Button>();
        buyBtn.interactable = interactable;
        buyBtn.onClick.AddListener(() => btn.onClick.Invoke());

        var buyTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        buyTextGo.transform.SetParent(buyGo.transform, false);
        var buyTextRect = buyTextGo.GetComponent<RectTransform>();
        buyTextRect.anchorMin = Vector2.zero;
        buyTextRect.anchorMax = Vector2.one;
        buyTextRect.offsetMin = Vector2.zero;
        buyTextRect.offsetMax = Vector2.zero;
        var buyText = buyTextGo.GetComponent<TextMeshProUGUI>();
        buyText.text = btnText;
        buyText.fontSize = 24;
        buyText.fontStyle = FontStyles.Bold;
        buyText.alignment = TextAlignmentOptions.Center;
        buyText.color = Color.white;

        return go;
    }

    private float CreateSectionHeader(Transform parent, string name, float yPos, string text)
    {
        // 背景条
        var bgGo = new GameObject(name + "_BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(parent, false);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 1f);
        bgRect.anchorMax = new Vector2(0.5f, 1f);
        bgRect.pivot = new Vector2(0.5f, 1f);
        bgRect.anchoredPosition = new Vector2(0f, yPos - HeaderHeight / 2f);
        bgRect.sizeDelta = new Vector2(CardWidth * 2f + CardSpacingX + 40f, HeaderHeight + 12f);
        var bgImage = bgGo.GetComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        bgImage.type = Image.Type.Sliced;

        // 文字
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, yPos - HeaderHeight / 2f);
        rect.sizeDelta = new Vector2(CardWidth * 2f + CardSpacingX, HeaderHeight);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.95f, 0.7f, 1f);
        return yPos - HeaderHeight - 16f;
    }

    private static void SetBuyButtonText(Button parentBtn, string text)
    {
        var buyBtn = parentBtn.transform.Find("BuyBtn");
        if (buyBtn == null) return;
        var tmp = buyBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        var children = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
            children.Add(parent.GetChild(i).gameObject);
        foreach (var child in children)
            Destroy(child);
    }

    private void ClearItemListChildren(Transform parent) => ClearChildren(parent);

    private TMP_Text FindTmp(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private Image FindImage(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<Image>() : null;
    }

    /// <summary>
    /// 编辑器下强制 StandaloneInputModule 激活，避免 Game 窗口未点击时滚轮无效。
    /// 打包后不影响（构建窗口自带焦点，不需要这个）。
    /// </summary>
    private static void ForceInputModuleActive()
    {
        if (EventSystem.current == null) return;
        var module = EventSystem.current.currentInputModule as StandaloneInputModule;
        if (module != null)
            module.forceModuleActive = true;
    }
}
