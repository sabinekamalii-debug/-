using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using TMPro;
using System;

/// <summary>
/// RogueResult 结算控制：
/// - 统一结算失败/胜利/无伤/押注
/// - 若有战斗结果且配置了天赋卡池：先在本场景内弹出「3 张选 1」选卡（由本脚本在 Start 里创建 RogueResult_CardSelectPanel 挂到 Canvas 下），选完或跳过后再显示结算
/// - 按钮：回入口 / 下一战
/// </summary>
public class RogueResultController : MonoBehaviour
{
    [Header("可选手动绑定（不绑会自动创建简易UI）")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text gainText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private Button backEntryButton;
    [SerializeField] private Button nextBattleButton;
    [SerializeField] private Button mainMenuButton;

    [Header("选卡（3选1）")]
    [Tooltip("天赋卡池：在 Inspector 拖入 TalentCardData；不填则从 Resources/TalentCards 加载。每张卡需填 Card Id 否则不会参与 3 选 1。选卡界面就在本场景内弹出，不切场景。")]
    [SerializeField] private TalentCardData[] cardPool;
    [Tooltip("卡槽预制体：拖入后选卡用预制体生成 3 个槽，否则用代码生成。预制体需含子物体名 Icon(Image)、Desc(TMP)、Cost(TMP)。菜单 塔防→创建肉鸽选卡槽预制体 可生成模板。")]
    [SerializeField] private GameObject cardSlotPrefab;
    [Tooltip("是否允许不选直接跳过")]
    [SerializeField] private bool allowSkipPick = true;


    [Header("选卡 按类型翻开视频（视频放 Assets/视频，拖到对应槽位）")]
    [Tooltip("点击某张卡时先播该卡类型对应视频，播完再显示正面。")]
    [SerializeField] private VideoClip attackCardRevealVideo;
    [SerializeField] private VideoClip defenseCardRevealVideo;
    [SerializeField] private VideoClip guardianCardRevealVideo;
    [SerializeField] private VideoClip rareCardRevealVideo;
    [SerializeField] private VideoClip skillCardRevealVideo;
    [SerializeField] private VideoClip specialCardRevealVideo;
    [Tooltip("播放上述视频的 VideoPlayer。不填则运行时自动创建全屏播视频，无需手动拖。")]
    [SerializeField] private VideoPlayer cardRevealVideoPlayer;
    [Tooltip("播视频时显示的物体。不填则用自动创建的面板或 VideoPlayer 所在物体。")]
    [SerializeField] private GameObject cardRevealVideoPanel;
    [Tooltip("无该类型视频或无 VideoPlayer 时，可播 Animator 状态名。不填则用下方时长。")]
    [SerializeField] private string revealAnimatorStateName = "Reveal";
    [Tooltip("无视频/无动画时等待多久（秒）后翻开。")]
    [SerializeField] private float revealAnimDuration = 0.6f;
    [Tooltip("翻开后卡片正面停留多久（秒）再关选卡面板，让玩家看到正面。")]
    [SerializeField] private float revealShowDuration = 3f;
    [Tooltip("卡槽内播视频时整体缩放比例（1=铺满卡槽，0.7=完整视频缩小到 70% 显示，不裁切边角）。")]
    [Range(0.3f, 1f)]
    [SerializeField] private float cardRevealVideoScale = 0.85f;
    [Tooltip("翻开视频播放速度（1=原速，最大 10 倍速）。")]
    [Range(0.5f, 10f)]
    [SerializeField] private float cardRevealVideoSpeed = 1.5f;

    [Header("直接运行本场景时")]
    [Tooltip("勾选后，直接以 RogueResult 场景按 Play 时，会模拟一场战斗胜利结果，方便测试选卡与结算界面。从战斗跳转过来时不会重复模拟。")]
    [SerializeField] private bool simulateBattleResultWhenDirectRun = true;

    // 是否为局内掉落抽卡模式。如果是，加载场景后只保留抽卡界面，结束后自动卸载该场景并恢复时间。
    public static bool IsMidGameDrop = false;

    private RogueFlowRouter _flow;
    private bool _settled;
    private bool _needCardPick;
    private GameObject _resultPanel;
    private GameObject _cardSelectPanel;
    private GameObject _cardTooltipPanel;
    private TMP_Text _cardTooltipText;
    private RectTransform _cardTooltipRect;
    private TalentCardData[] _currentOffers = new TalentCardData[3];
    private List<Button> _cardButtons = new List<Button>();
    private bool _revealingInProgress;
    private bool _revealClickToClose;
    private GameObject _revealClickOverlay;
    private bool[] _slotRevealed = new bool[3];
    private GameObject _clickToReturnToTitleOverlay;
    private VideoPlayer _fallbackRevealVideoPlayer;
    private GameObject _fallbackRevealVideoPanel;
    private RenderTexture _fallbackRevealRenderTexture;
    private static Sprite _whitePlaceholderSprite;
    private static Sprite _blackSquareSprite;
    
    // 战斗结果，用于后续判断
    private bool _isBattleWin = true;
    private bool _useSimplifiedSettlementView = true;
    [Header("简化结算显示")]
    [SerializeField] private int fullGuardianHpForGreatVictory = 10;
    [Header("结算文字出现特效")]
    [SerializeField] private float settlementTextFadeDuration = 0.26f;
    [SerializeField] private float settlementTextStaggerDelay = 0.06f;
    [SerializeField] private float settlementTextStartScale = 0.88f;
    [SerializeField] private bool useOutlinePulseEffect = true;
    [SerializeField] private float outlinePulsePeakWidth = 0.22f;
    [SerializeField] private Color outlinePulseColor = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private int outlinePulseCount = 1;
    [SerializeField] private float greatVictoryOutlinePulsePeakWidth = 0.22f;
    [SerializeField] private Color greatVictoryOutlinePulseColor = new Color(1f, 0.35f, 0.2f, 1f);
    [SerializeField] private Color victoryOutlinePulseColor = new Color(0.62f, 0.34f, 1f, 1f);
    [SerializeField] private bool useGreatVictoryFlash = false;
    [SerializeField] private float greatVictoryFlashDuration = 0.07f;
    [SerializeField] private float greatVictoryScalePulseMultiplier = 1.0f;
    [SerializeField] private bool useImpactShakeEffect = true;
    [SerializeField] private float impactScaleMultiplier = 1.12f;
    [SerializeField] private float impactScaleDuration = 0.1f;
    [SerializeField] private float impactShakeDuration = 0.16f;
    [SerializeField] private float impactShakeStrength = 22f;

    private void Awake()
    {
        if (IsMidGameDrop)
        {
            // 作为叠加场景时，销毁自身多余的相机和事件系统避免冲突和遮挡主场景
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras) { if (cam.gameObject.scene.name == "RogueResult") Destroy(cam.gameObject); }
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var es in eventSystems) { if (es.gameObject.scene.name == "RogueResult") Destroy(es.gameObject); }
            // 设置抽卡标志
            _needCardPick = true;
        }
        else
        {
            // 普通运行场景时，确保有相机
            EnsureCameraExists();
        }

        RogueRuntimeState.InitIfNeeded();
        EnsureResultSceneUIRootActive();
        _flow = FindFirstObjectByType<RogueFlowRouter>();
        TryBindByName();
        EnsureSimpleUiIfMissing();
        BindButtons();
        RogueUIUtil.EnsureButtonLabelTmp(backEntryButton);
        RogueUIUtil.EnsureButtonLabelTmp(nextBattleButton);
        RogueUIUtil.EnsureButtonsVisible(backEntryButton, nextBattleButton);
        CacheResultPanel();
        if (cardPool == null || cardPool.Length == 0)
            cardPool = Resources.LoadAll<TalentCardData>("TalentCards");
    }

    private void Start()
    {
        EnsureResultSceneUIRootActive();

        if (IsMidGameDrop)
        {
            GameObject panel = GameObject.Find("结算面板");
            if (panel != null) panel.SetActive(false);
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (detailText != null) detailText.gameObject.SetActive(false);
            if (gainText != null) gainText.gameObject.SetActive(false);
            if (totalText != null) totalText.gameObject.SetActive(false);
            HideBackgroundElements(); // 隐藏背景元素，只显示卡片
            _needCardPick = true;
        }
        else if (simulateBattleResultWhenDirectRun && !RogueRuntimeState.HasPendingBattleResult)
        {
            RogueRuntimeState.ClearSelectedTalentCardsForTesting();
            RogueRuntimeState.StartRunIfNeeded();
            RogueRuntimeState.PublishBattleResult(new RogueBattleResult
            {
                stage = 1,
                isWin = true,
                noHit = true,
                guardianHpEnd = 10,
                firstClear = true,
                betPlaced = false
            });
        }

        if (!IsMidGameDrop) SettleIfNeeded();
        if (_needCardPick)
        {
            EnsureCardSelectPanel();
            PickRandomOffers();
            bool hasAnyOffer = _currentOffers[0] != null || _currentOffers[1] != null || _currentOffers[2] != null;
            if (hasAnyOffer && _cardSelectPanel != null)
            {
                BringCardPanelToFront();
                ShowCardSelectPanel();
            }
            else
            {
                SetResultPanelVisible(true);
            }
        }
        else
        {
            SetResultPanelVisible(true);
        }
    }

    /// <summary>
    /// 把选卡面板移到同层级最后，保证绘制在最上层。
    /// </summary>
    private void BringCardPanelToFront()
    {
        if (_cardSelectPanel == null) return;
        _cardSelectPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 场景里若把 UIRoot / RogueResultRoot 或结算面板在编辑器中设为未激活，一运行会全不显示。
    /// 先激活本场景根节点，再按名字激活 RogueResultRoot / UIRoot / 结算面板 及其父链、两个按钮。
    /// </summary>
    private void EnsureResultSceneUIRootActive()
    {
        if (transform != null && transform.root != null && !transform.root.gameObject.activeSelf)
            transform.root.gameObject.SetActive(true);
        var resultRoot = GameObject.Find("RogueResultRoot");
        if (resultRoot != null && !resultRoot.activeSelf) resultRoot.SetActive(true);
        var uiRoot = GameObject.Find("UIRoot");
        if (uiRoot != null)
        {
            if (!uiRoot.activeSelf) uiRoot.SetActive(true);
            // 场景里 UIRoot 的 scale 常被做成 (0,0,0)，不恢复为 1 则结算文字全部不显示
            uiRoot.transform.localScale = Vector3.one;
        }
        var panel = GameObject.Find("结算面板");
        if (panel != null)
        {
            for (var t = panel.transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }
        var backBtn = GameObject.Find("回入口按钮");
        if (backBtn != null && !backBtn.activeSelf) backBtn.SetActive(true);
        var nextBtn = GameObject.Find("下一战按钮");
        if (nextBtn != null && !nextBtn.activeSelf) nextBtn.SetActive(true);
    }

    private void CacheResultPanel()
    {
        if (titleText != null && titleText.transform.parent != null)
            _resultPanel = titleText.transform.parent.gameObject;
    }

    private void SetResultPanelVisible(bool visible)
    {
        if (_resultPanel != null)
        {
            _resultPanel.SetActive(visible);
            if (visible)
            {
                // 场景里 UIRoot 的 localScale 可能为 (0,0,0)，会导致子物体（标题/详情/收益/总点）全部不可见，尽管 TMP 里已有正确文字
                _resultPanel.transform.localScale = Vector3.one;
                RefreshResultPanelTexts();
                
                // 只在失败时显示点击屏幕返回Title的覆盖层
                if (!_isBattleWin)
                {
                    EnsureClickToReturnToTitleOverlay();
                }
                else
                {
                    // 胜利时确保覆盖层被隐藏
                    if (_clickToReturnToTitleOverlay != null)
                        _clickToReturnToTitleOverlay.SetActive(false);
                }
            }
            else
            {
                // 隐藏点击屏幕返回Title的覆盖层
                if (_clickToReturnToTitleOverlay != null)
                    _clickToReturnToTitleOverlay.SetActive(false);
            }
        }
    }
    
    private void EnsureClickToReturnToTitleOverlay()
    {
        if (_clickToReturnToTitleOverlay != null)
        {
            _clickToReturnToTitleOverlay.SetActive(true);
            _clickToReturnToTitleOverlay.transform.SetAsLastSibling();
            return;
        }
        
        Canvas canvas = null;
        if (titleText != null)
            canvas = titleText.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;
        
        // 创建全屏点击覆盖层
        var go = new GameObject("ClickToReturnToTitle", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.01f);
        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnClickReturnToTitle);
        
        // 添加提示文字
        var textGo = new GameObject("ReturnToTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.15f);
        textRt.anchorMax = new Vector2(0.5f, 0.15f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(400f, 60f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "点击屏幕返回Title";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        
        _clickToReturnToTitleOverlay = go;
    }
    
    private void OnClickReturnToTitle()
    {
        if (_clickToReturnToTitleOverlay != null)
            _clickToReturnToTitleOverlay.SetActive(false);
        
        // 失败时跳转到死亡剧情，胜利时返回Title
        if (!_isBattleWin)
        {
            StartCoroutine(FadeToBlackThenLoadDeathScene());
        }
        else
        {
            // 返回Title场景
            VideoSceneLoader.LoadScene("Title");
        }
    }
    
    private IEnumerator FadeToBlackThenLoadDeathScene()
    {
        // 创建全屏黑色覆盖层
        Canvas canvas = null;
        if (titleText != null)
            canvas = titleText.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        
        if (canvas != null)
        {
            GameObject fadeOverlay = new GameObject("FadeToBlack", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fadeOverlay.transform.SetParent(canvas.transform, false);
            
            RectTransform fadeRt = fadeOverlay.GetComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.offsetMin = Vector2.zero;
            fadeRt.offsetMax = Vector2.zero;
            
            UnityEngine.UI.Image fadeImage = fadeOverlay.GetComponent<UnityEngine.UI.Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = true;
            
            // 1秒内渐变到黑色
            float duration = 1f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeImage.color = new Color(0f, 0f, 0f, t);
                yield return null;
            }
            
            // 跳转前销毁覆盖层
            Destroy(fadeOverlay);
        }
        
        // 跳转到死亡 1剧情
        LoadDeathScene();
    }
    
    private void LoadDeathScene()
    {
        // 使用NaninovelReturnRequest加载死亡 1剧情
        // NaninovelReturnRequest.Set("死亡 1", ""); // TODO: Naninovel包缺失
        VideoSceneLoader.LoadScene("Title");
    }

    private void EnsureCardSelectPanel()
    {
        if (_cardSelectPanel != null) return;

        // 必须优先使用本场景内的 UIRoot Canvas，防止错误挂载到怪物的 World Space Canvas 上
        Canvas canvas = null;
        var rRoot = transform.parent;
        if (rRoot != null)
        {
            var uiRoot = rRoot.Find("UIRoot");
            if (uiRoot != null) canvas = uiRoot.GetComponent<Canvas>();
        }

        if (canvas == null && titleText != null)
            canvas = titleText.GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        // 如果是局内掉落，必须确保 Canvas 是全屏 Overlap 模式且渲染层级最高
        if (IsMidGameDrop && canvas != null)
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

        // 无背景：仅作为布局容器，不显示整块遮罩
        _cardSelectPanel = new GameObject("RogueResult_CardSelectPanel", typeof(RectTransform), typeof(Image));
        _cardSelectPanel.transform.SetParent(canvas.transform, false);
        var panelRect = _cardSelectPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImg = _cardSelectPanel.GetComponent<Image>();
        if (IsMidGameDrop)
        {
            panelImg.color = new Color(0f, 0f, 0f, 0.75f);
            panelImg.raycastTarget = true;
        }
        else
        {
            panelImg.color = new Color(0f, 0f, 0f, 0f);
            panelImg.raycastTarget = false;
        }

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

        const float cardW = 540f;
        const float cardH = 960f;
        const float gap = 80f;
        const float cardSlotY = -100f;
        float startX = -(cardW + gap);

        if (cardSlotPrefab != null)
        {
            for (int i = 0; i < 3; i++)
            {
                var slotGo = UnityEngine.Object.Instantiate(cardSlotPrefab, _cardSelectPanel.transform);
                slotGo.name = $"CardSlot_{i}";
                
                // 【根源修复】彻底清空预制体上所有 Image 的默认 Sprite，确保完全由数据驱动
                var allImages = slotGo.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in allImages)
                {
                    img.sprite = null;
                    img.color = Color.white;
                }
                
                var slotRect = slotGo.GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    slotRect.anchorMin = new Vector2(0.5f, 1f);
                    slotRect.anchorMax = new Vector2(0.5f, 1f);
                    slotRect.pivot = new Vector2(0.5f, 1f);
                    slotRect.anchoredPosition = new Vector2(startX + i * (cardW + gap), cardSlotY);
                    slotRect.sizeDelta = new Vector2(cardW, cardH);
                }
                var btn = slotGo.GetComponent<Button>();
                if (btn != null)
                {
                    int capture = i;
                    btn.onClick.AddListener(() => OnPickCard(capture));
                    _cardButtons.Add(btn);
                    AddCardSlotHover(slotGo, capture);
                }
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                var slot = CreateCardSlot(_cardSelectPanel.transform, i, new Vector2(startX + i * (cardW + gap), cardSlotY), new Vector2(cardW, cardH));
                _cardButtons.Add(slot);
            }
        }

        EnsureCardTooltipPanel();

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
            skipBtn.onClick.AddListener(OnSkipCardPick);
        }

        _cardSelectPanel.SetActive(false);
        _cardSelectPanel.transform.SetAsLastSibling();
    }

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

        // 让 Icon 完全覆盖整个卡片
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.zero;
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = false;

        // 描述文本和消耗文本现在不需要了，因为 Icon 完全覆盖卡片
        // float nameBottom = size.y - iconH - 4f;
        // var descGo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        // descGo.transform.SetParent(go.transform, false);
        // SetFullRect(descGo.GetComponent<RectTransform>(), 10f, 44f, 10f, size.y - nameBottom);
        // var descTmp = descGo.GetComponent<TextMeshProUGUI>();
        // descTmp.fontSize = 16;
        // descTmp.alignment = TextAlignmentOptions.TopLeft;
        // descTmp.color = new Color(0.9f, 0.9f, 0.9f);
        // descTmp.text = "";
        // descTmp.enableWordWrapping = true;

        // var costGo = new GameObject("Cost", typeof(RectTransform), typeof(TextMeshProUGUI));
        // costGo.transform.SetParent(go.transform, false);
        // var costRect = costGo.GetComponent<RectTransform>();
        // costRect.anchorMin = new Vector2(0f, 0f);
        // costRect.anchorMax = new Vector2(1f, 0f);
        // costRect.pivot = new Vector2(0.5f, 0f);
        // costRect.anchoredPosition = new Vector2(0f, 10f);
        // costRect.sizeDelta = new Vector2(-20f, 32f);
        // var costTmp = costGo.GetComponent<TextMeshProUGUI>();
        // costTmp.fontSize = 20;
        // costTmp.alignment = TextAlignmentOptions.Center;
        // costTmp.color = Color.yellow;
        // costTmp.text = "0点";

        var btn = go.GetComponent<Button>();
        int capture = index;
        btn.onClick.AddListener(() => OnPickCard(capture));
        AddCardSlotHover(go, capture);
        return btn;
    }

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

    private void ShowCardTooltip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3 || _currentOffers[slotIndex] == null) { HideCardTooltip(); return; }
        var c = _currentOffers[slotIndex];
        _cardTooltipText.text = $"{c.displayName}\n类型:{c.cardType}\n稀有度:{c.rarity}\n消耗:{c.costRunPoint}点\n\n{c.description}";
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

    private static void SetFullRect(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private void PickRandomOffers()
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

        // 卡池不足 3 张时允许重复抽取，保证三槽都有卡显示
        for (int i = 0; i < 3; i++)
        {
            int idx = UnityEngine.Random.Range(0, available.Count);
            _currentOffers[i] = available[idx];
            if (available.Count >= 3)
                available.RemoveAt(idx);
        }

        RefreshCardSlotVisuals();
    }

    private void RefreshCardSlotVisuals()
    {
        int runPoint = RogueRuntimeState.RunPoint;
        
        for (int i = 0; i < 3; i++)
        {
            if (i >= _cardButtons.Count) break;
            var btn = _cardButtons[i];
            var card = _currentOffers[i];
            
            if (btn == null) continue;

            var root = btn.transform;
            var rootImg = root.GetComponent<Image>();
            var backImg = FindChildImage(root, "Back", "背面", "CardBack");
            var iconImg = FindChildImageRecursive(root, "Icon", "卡图", "CardImage", "Image");

            if (card != null)
            {
                if (_slotRevealed[i])
                {
                    // 已翻开状态：显示正面和icon
                    if (rootImg != null)
                    {
                        if (card.cardFront != null)
                        {
                            rootImg.sprite = card.cardFront;
                            rootImg.color = Color.white;
                            rootImg.enabled = true;
                        }
                    }
                    
                    if (backImg != null)
                    {
                        backImg.gameObject.SetActive(false);
                    }

                    // 显示 icon 作为16:9小图（仅在翻开后）
                    if (iconImg != null)
                    {
                        if (card.icon != null)
                        {
                            iconImg.sprite = card.icon;
                            iconImg.enabled = true;
                            iconImg.gameObject.SetActive(true);
                            iconImg.color = Color.white;
                            iconImg.preserveAspect = false;
                            
                            // 调整 icon 为16:9比例（上半部分）
                            var iconRect = iconImg.GetComponent<RectTransform>();
                            if (iconRect != null)
                            {
                                var slotRect = root.GetComponent<RectTransform>();
                                if (slotRect != null)
                                {
                                    float iconWidth = slotRect.sizeDelta.x * 0.5f;
                                    float iconHeight = iconWidth * 9f / 16f;
                                    iconRect.anchorMin = new Vector2(0.5f - 0.25f, 1f - (0.25f * 9f / 16f));
                                    iconRect.anchorMax = new Vector2(0.5f + 0.25f, 1f);
                                    iconRect.pivot = new Vector2(0.5f, 1f);
                                    iconRect.anchoredPosition = Vector2.zero;
                                    iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
                                }
                            }
                        }
                        else
                        {
                            iconImg.enabled = false;
                            iconImg.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    // 未翻开状态：只显示背面
                    if (rootImg != null)
                    {
                        if (card.cardBack != null)
                        {
                            rootImg.sprite = card.cardBack;
                            rootImg.color = Color.white;
                            rootImg.enabled = true;
                        }
                    }
                    
                    if (backImg != null)
                    {
                        if (card.cardBack != null)
                        {
                            backImg.sprite = card.cardBack;
                            backImg.gameObject.SetActive(true);
                            backImg.enabled = true;
                        }
                    }

                    // 隐藏icon
                    if (iconImg != null)
                    {
                        iconImg.enabled = false;
                        iconImg.gameObject.SetActive(false);
                    }
                }

                bool canAfford = IsMidGameDrop || runPoint >= card.costRunPoint;
                btn.interactable = canAfford;
            }
            else
            {
                if (rootImg != null) rootImg.enabled = false;
                if (backImg != null) backImg.gameObject.SetActive(false);
                if (iconImg != null)
                {
                    iconImg.enabled = false;
                    iconImg.gameObject.SetActive(false);
                }
                btn.interactable = false;
            }
        }
    }

    /// <summary> 兼容不同预制体：按多个名字找子物体上的 Image（仅直接子物体）。 </summary>
    private static Image FindChildImage(Transform root, params string[] names)
    {
        foreach (var name in names)
        {
            var t = root.Find(name);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    /// <summary> 在整棵子树里找用于显示卡图的 Image（支持嵌套、任意命名）。 </summary>
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
        var allImages = root.GetComponentsInChildren<Image>(true);
        Image fallback = null;
        Image firstNonRoot = null;
        foreach (var img in allImages)
        {
            if (img == rootImg) continue;
            if (firstNonRoot == null) firstNonRoot = img;
            var n = (img.gameObject.name ?? "").Trim();
            if (n.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.Contains("卡图") || n.IndexOf("CardImage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return img;
            if ((fallback == null) && (n == "Image" || n == "卡图" || n == "Icon"))
                fallback = img;
        }
        return fallback ?? firstNonRoot;
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

    /// <summary> 兼容不同预制体：按多个名字找子物体上的 TMP_Text（仅直接子物体）。 </summary>
    private static TMP_Text FindChildTMP(Transform root, params string[] names)
    {
        foreach (var name in names)
        {
            var t = root.Find(name);
            if (t != null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
        }
        return null;
    }

    /// <summary> 在整棵子树里按名字找带 TMP_Text 的节点。 </summary>
    private static TMP_Text FindChildTMPRecursive(Transform root, params string[] names)
    {
        foreach (var name in names)
        {
            var t = FindInDescendants(root, name);
            if (t != null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
        }
        return null;
    }

    private void ShowCardSelectPanel()
    {
        SetSettlementTextsActive(false);
        if (_cardSelectPanel != null) _cardSelectPanel.SetActive(true);
    }

    private void HideCardSelectPanel()
    {
        HideCardTooltip();
        if (_cardSelectPanel != null) _cardSelectPanel.SetActive(false);
    }

    private void OnPickCard(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        if (_revealingInProgress) return;
        var card = _currentOffers[slotIndex];
        if (card == null) return;
        StartCoroutine(RevealThenPick(slotIndex));
    }

    private IEnumerator RevealThenPick(int slotIndex)
    {
        _revealingInProgress = true;
        var btn = slotIndex < _cardButtons.Count ? _cardButtons[slotIndex] : null;
        Transform slotRoot = btn != null ? btn.transform : null;

        var card = slotIndex < _currentOffers.Length ? _currentOffers[slotIndex] : null;
        var clip = GetRevealVideoForType(card != null ? card.cardType : TalentCardType.Special);
        var player = cardRevealVideoPlayer != null ? cardRevealVideoPlayer : EnsureFallbackRevealVideoPlayer();
        if (clip != null && player != null)
        {
            var panel = cardRevealVideoPanel != null ? cardRevealVideoPanel : (player.gameObject);
            if (_fallbackRevealVideoPanel != null && player == _fallbackRevealVideoPlayer)
            {
                panel = _fallbackRevealVideoPanel;
                EnsureRenderTextureForClip(clip);
                if (_fallbackRevealRenderTexture != null) player.targetTexture = _fallbackRevealRenderTexture;
                if (slotRoot != null)
                {
                    panel.transform.SetParent(slotRoot, false);
                    var rt = panel.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    float scale = Mathf.Clamp(cardRevealVideoScale, 0.3f, 1f);
                    panel.transform.localScale = new Vector3(scale, scale, 1f);
                    panel.transform.SetAsLastSibling();
                }
            }
            panel.SetActive(true);
            player.clip = clip;
            float speed = Mathf.Clamp(cardRevealVideoSpeed, 0.5f, 10f);
            player.playbackSpeed = speed;
            player.Prepare();
            player.Play();
            float waitTime = (float)clip.length / speed;
            if (waitTime <= 0f) waitTime = revealAnimDuration;
            yield return new WaitForSecondsRealtime(waitTime);
            player.Stop();
            panel.SetActive(false);
        }
        else if (slotRoot != null)
        {
            var anim = slotRoot.GetComponent<Animator>();
            if (anim != null && !string.IsNullOrEmpty(revealAnimatorStateName))
            {
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                anim.Play(revealAnimatorStateName, 0, 0f);
                float elapsed = 0f;
                while (elapsed < revealAnimDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(revealAnimDuration);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(revealAnimDuration);
        }

        SetSlotRevealedAndFill(slotRoot, slotIndex);
        if (slotRoot != null)
        {
            var front = GetFrontContainer(slotRoot);
            if (front != null) front.SetAsLastSibling();
            var iconT = FindInDescendants(slotRoot, "Icon");
            if (iconT != null) iconT.SetAsLastSibling();
            var slotRect = slotRoot as RectTransform;
            if (slotRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(slotRect);
        }
        if (card != null)
        {
            if (IsMidGameDrop)
                RogueRuntimeState.AddFreeTalentCard(card);
            else
                RogueRuntimeState.TryPickTalentCard(card);
        }
        yield return null;
        EnsureRevealClickOverlay();
        _revealClickToClose = false;
        if (_revealClickOverlay != null)
        {
            _revealClickOverlay.SetActive(true);
            _revealClickOverlay.transform.SetAsLastSibling();
            var overlayBtn = _revealClickOverlay.GetComponent<Button>();
            if (overlayBtn != null)
            {
                overlayBtn.onClick.RemoveAllListeners();
                overlayBtn.onClick.AddListener(OnRevealClickToClose);
            }
        }
        yield return new WaitUntil(() => _revealClickToClose);
        if (_revealClickOverlay != null) _revealClickOverlay.SetActive(false);
        HideCardSelectPanel();

        if (IsMidGameDrop)
        {
            IsMidGameDrop = false;
            // 仅在真正完成选择后恢复时间，而不是在跳过时立即恢复
            if (GameManager.Instance != null) GameManager.Instance.ResetMidGameDropFlag();
            var unloadOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("RogueResult");
            if (unloadOperation != null)
            {
                yield return unloadOperation;
                // 场景卸载完成后恢复游戏时间
                Time.timeScale = 1f;
            }
            yield break;
        }

        SetResultPanelVisible(true);
        yield return PlaySettlementRevealEffectIfNeeded();
        RefreshTotalText();
        _revealingInProgress = false;
    }

    private void OnRevealClickToClose()
    {
        _revealClickToClose = true;
    }

    private void EnsureRevealClickOverlay()
    {
        if (_revealClickOverlay != null || _cardSelectPanel == null) return;
        var go = new GameObject("RevealClickToContinue", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_cardSelectPanel.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.01f);
        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.15f);
        textRect.anchorMax = new Vector2(0.5f, 0.15f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400f, 60f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "点击屏幕继续";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        go.SetActive(false);
        _revealClickOverlay = go;
    }

    /// <summary> 按视频尺寸创建/更新 RenderTexture，不裁切视频。 </summary>
    private void EnsureRenderTextureForClip(VideoClip clip)
    {
        if (clip == null) return;
        uint w = clip.width;
        uint h = clip.height;
        if (w < 1) w = 1280;
        if (h < 1) h = 720;
        int iw = (int)w;
        int ih = (int)h;
        if (_fallbackRevealRenderTexture != null && _fallbackRevealRenderTexture.width == iw && _fallbackRevealRenderTexture.height == ih)
            return;
        if (_fallbackRevealRenderTexture != null)
        {
            _fallbackRevealRenderTexture.Release();
            _fallbackRevealRenderTexture = null;
        }
        _fallbackRevealRenderTexture = new RenderTexture(iw, ih, 0);
        _fallbackRevealRenderTexture.name = "RogueRevealVideoRT";
        if (_fallbackRevealVideoPanel != null)
        {
            var raw = _fallbackRevealVideoPanel.GetComponent<RawImage>();
            if (raw != null) raw.texture = _fallbackRevealRenderTexture;
        }
    }

    /// <summary> 未在 Inspector 拖 VideoPlayer 时，自动创建播视频用的 VideoPlayer + RawImage。 </summary>
    private VideoPlayer EnsureFallbackRevealVideoPlayer()
    {
        if (_fallbackRevealVideoPlayer != null) return _fallbackRevealVideoPlayer;
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return null;
        _fallbackRevealRenderTexture = new RenderTexture(1280, 720, 0);
        _fallbackRevealRenderTexture.name = "RogueRevealVideoRT";
        var panelGo = new GameObject("RogueResult_RevealVideoPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var rawImg = panelGo.AddComponent<RawImage>();
        rawImg.color = Color.white;
        rawImg.texture = _fallbackRevealRenderTexture;
        var videoGo = new GameObject("RogueResult_RevealVideoPlayer");
        videoGo.transform.SetParent(panelGo.transform, false);
        var vp = videoGo.AddComponent<VideoPlayer>();
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = _fallbackRevealRenderTexture;
        vp.isLooping = false;
        vp.playOnAwake = false;
        panelGo.SetActive(false);
        _fallbackRevealVideoPlayer = vp;
        _fallbackRevealVideoPanel = panelGo;
        return _fallbackRevealVideoPlayer;
    }

    private VideoClip GetRevealVideoForType(TalentCardType type)
    {
        switch (type)
        {
            case TalentCardType.Attack: return attackCardRevealVideo;
            case TalentCardType.Defense: return defenseCardRevealVideo;
            case TalentCardType.Guardian: return guardianCardRevealVideo;
            case TalentCardType.Rare: return rareCardRevealVideo;
            case TalentCardType.Skill: return skillCardRevealVideo;
            case TalentCardType.Special: return specialCardRevealVideo;
            default: return attackCardRevealVideo;
        }
    }

    private void SetAllSlotsClosed()
    {
        for (int i = 0; i < _cardButtons.Count && i < 3; i++)
        {
            if (_cardButtons[i] == null) continue;
            SetSlotClosed(_cardButtons[i].transform, i);
        }
    }

    private void SetSlotClosed(Transform slotRoot, int slotIndex)
    {
        if (slotRoot == null) return;
        
        if (slotIndex >= 0 && slotIndex < _currentOffers.Length && _currentOffers[slotIndex] != null)
        {
            var card = _currentOffers[slotIndex];
            var rootImg = slotRoot.GetComponent<Image>();
            
            if (card.cardBack != null && rootImg != null)
            {
                rootImg.sprite = card.cardBack;
                rootImg.enabled = true;
            }
        }
    }

    private void SetSlotRevealedAndFill(Transform slotRoot, int slotIndex)
    {
        if (slotRoot == null) return;
        
        if (slotIndex >= 0 && slotIndex < _currentOffers.Length && _currentOffers[slotIndex] != null)
        {
            // 标记卡槽已翻开
            _slotRevealed[slotIndex] = true;
            // 刷新卡槽视觉，显示正面和icon
            RefreshCardSlotVisuals();
        }
    }

    private void SetSlotIconToWhite(Transform slotRoot)
    {
        if (slotRoot == null) return;
        var iconImg = FindChildImageRecursive(slotRoot, "Icon", "卡图", "CardImage", "Image");
        if (iconImg != null)
        {
            iconImg.sprite = GetOrCreateWhitePlaceholderSprite();
            iconImg.enabled = true;
            iconImg.color = Color.white;
            iconImg.gameObject.SetActive(true);
            SetActiveUpTo(iconImg.transform, slotRoot);
        }
    }

    private void ForceEnsureIconVisible(Transform slotRoot, int slotIndex)
    {
        var iconImg = FindChildImageRecursive(slotRoot, "Icon", "卡图", "CardImage", "Image");
        if (iconImg == null) return;
        
        iconImg.enabled = true;
        iconImg.color = Color.white;
        iconImg.gameObject.SetActive(true);
        SetActiveUpTo(iconImg.transform, slotRoot);
        EnsureRectChainMinimumSize(iconImg.transform, slotRoot, 80f);
        var card = slotIndex < _currentOffers.Length ? _currentOffers[slotIndex] : null;
        
        if (card != null && card.icon != null)
        {
            iconImg.sprite = card.icon;
        }
        var rt = iconImg.GetComponent<RectTransform>();
        if (rt != null && (rt.sizeDelta.x < 10f || rt.sizeDelta.y < 10f))
        {
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 100f), Mathf.Max(rt.sizeDelta.y, 100f));
        }
    }

    private static void EnsureRectChainMinimumSize(Transform from, Transform root, float minSize)
    {
        var t = from;
        while (t != null && t != root)
        {
            var rt = t.GetComponent<RectTransform>();
            if (rt != null && (rt.sizeDelta.x < 5f || rt.sizeDelta.y < 5f))
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, minSize), Mathf.Max(rt.sizeDelta.y, minSize));
            }
            t = t.parent;
        }
    }

    private static Transform GetFrontContainer(Transform root)
    {
        if (root == null) return null;
        var t = FindInDescendants(root, "Front");
        if (t != null) return t;
        return FindInDescendants(root, "正面");
    }

    /// <summary> 背面朝上时只隐藏这些，不隐藏 Icon，避免一运行 Icon 就失活。 </summary>
    private static void SetFrontChildrenActiveWhenClosed(Transform root)
    {
        if (root == null) return;
        string[] hideOnlyNames = { "正面图", "FrontImage", "Desc", "描述", "Cost", "消耗", "Name", "标题", "卡名" };
        foreach (var name in hideOnlyNames)
        {
            var t = FindInDescendants(root, name);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    private static void SetFrontChildrenActive(Transform root, bool active)
    {
        if (root == null) return;
        string[] frontNames = { "正面图", "FrontImage", "Icon", "卡图", "CardImage", "Desc", "描述", "Cost", "消耗", "Name", "标题", "卡名" };
        foreach (var name in frontNames)
        {
            var t = FindInDescendants(root, name);
            if (t != null)
            {
                t.gameObject.SetActive(active);
                if (active) SetActiveUpTo(t, root);
            }
        }
    }

    private void RefreshSingleSlotVisuals(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _cardButtons.Count) return;
        var btn = _cardButtons[slotIndex];
        var card = _currentOffers[slotIndex];
        if (btn == null) return;
        var root = btn.transform;
        var iconImg = FindChildImageRecursive(root, "Icon", "卡图", "CardImage", "Image");
        var nameTmp = FindChildTMPRecursive(root, "Name", "Title", "标题", "卡名");
        var descTmp = FindChildTMPRecursive(root, "Desc", "描述", "Description");
        var costTmp = FindChildTMPRecursive(root, "Cost", "消耗", "点数", "CostText");
        
        int runPoint = RogueRuntimeState.RunPoint;
        if (card != null)
        {
            if (iconImg != null)
            {
                if (card.icon != null)
                {
                    iconImg.sprite = card.icon;
                }
                iconImg.enabled = true;
                iconImg.color = Color.white;
                iconImg.gameObject.SetActive(true);
                SetActiveUpTo(iconImg.transform, root);
                var iconRt = iconImg.GetComponent<RectTransform>();
                if (iconRt != null && (iconRt.sizeDelta.x < 10f || iconRt.sizeDelta.y < 10f))
                    iconRt.sizeDelta = new Vector2(Mathf.Max(iconRt.sizeDelta.x, 100f), Mathf.Max(iconRt.sizeDelta.y, 100f));
            }
            if (nameTmp != null) nameTmp.text = card.displayName;
            if (descTmp != null) descTmp.text = string.IsNullOrEmpty(card.description) ? card.cardType.ToString() : card.description;
            if (costTmp != null) costTmp.text = IsMidGameDrop ? "免费" : $"{card.costRunPoint}点";
        }
        else
        {
            if (iconImg != null) { iconImg.sprite = null; iconImg.enabled = false; }
            if (nameTmp != null) nameTmp.text = "—";
            if (descTmp != null) descTmp.text = "";
            if (costTmp != null) costTmp.text = "";
        }
    }

    private static void SetActiveUpTo(Transform from, Transform root)
    {
        while (from != null && from != root)
        {
            from.gameObject.SetActive(true);
            from = from.parent;
        }
    }

    private void OnSkipCardPick()
    {
        HideCardSelectPanel();
        if (IsMidGameDrop)
        {
            IsMidGameDrop = false;
            // 仅在真正完成选择后恢复时间，而不是在跳过时立即恢复
            if (GameManager.Instance != null) GameManager.Instance.ResetMidGameDropFlag();
            var unloadOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("RogueResult");
            if (unloadOperation != null)
            {
                StartCoroutine(ResumeTimeAfterUnload(unloadOperation));
            }
            return;
        }
        StartCoroutine(ShowResultPanelWithEffects());
    }
    
    private IEnumerator ResumeTimeAfterUnload(AsyncOperation unloadOperation)
    {
        yield return unloadOperation;
        // 场景卸载完成后恢复游戏时间
        Time.timeScale = 1f;
    }

    private void BindButtons()
    {
        if (backEntryButton != null)
        {
            backEntryButton.onClick.RemoveListener(BackEntry);
            backEntryButton.onClick.AddListener(BackEntry);
        }

        if (nextBattleButton != null)
        {
            nextBattleButton.onClick.RemoveListener(NextBattle);
            nextBattleButton.onClick.AddListener(NextBattle);
        }
    }

    private void SettleIfNeeded()
    {
        if (_settled) return;
        _settled = true;

        EnsureSettlementTextsBound();
        if (titleText == null) return;

        if (!RogueRuntimeState.TryConsumeBattleResult(out var result))
        {
            SetText(titleText, "未找到结算数据");
            SetText(detailText, "请从入口进入本局，或从关卡/plot 完成战斗后进入本场景。");
            RefreshTotalText();
            _needCardPick = false;
            return;
        }

        var summary = RogueRuntimeState.ApplySettlement(result);
        
        // 保存战斗结果
        _isBattleWin = result.isWin;

        if (_useSimplifiedSettlementView)
        {
            ApplySimplifiedSettlementDisplay(result, summary);
        }
        else
        {
            SetText(titleText, result.isWin ? "战斗胜利" : "战斗失败");
            SetText(detailText,
                $"关卡:{result.stage}\n" +
                $"无伤:{(result.noHit ? "是" : "否")}\n" +
                $"守护点剩余:{result.guardianHpEnd}\n" +
                $"押注结果:{summary.betOutcome}");
            SetText(gainText, $"本次获得RunPoint:+{summary.runPointGain}|永久点:+{summary.permanentPointGain}");
            RefreshTotalText();
        }
        // 失败时跳过抽卡，直接显示结算
        _needCardPick = result.isWin && cardPool != null && cardPool.Length > 0 && RogueRuntimeState.HasActiveRun;
        
        // 失败时隐藏所有按钮
        if (!result.isWin)
        {
            HideAllButtons();
            // 失败时设置背景为深蓝色
            SetFailureBackground();
        }
    }
    
    private void HideAllButtons()
    {
        if (backEntryButton != null)
        {
            backEntryButton.gameObject.SetActive(false);
        }
        
        if (nextBattleButton != null)
        {
            nextBattleButton.gameObject.SetActive(false);
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(false);
        }
    }
    
    private void SetFailureBackground()
    {
        // 找到结算面板的背景
        if (_resultPanel != null)
        {
            // 尝试获取背景Image组件
            var backgroundImage = _resultPanel.GetComponent<UnityEngine.UI.Image>();
            if (backgroundImage != null)
            {
                // 设置为深蓝色
                backgroundImage.color = new Color(0.1f, 0.2f, 0.4f, 0.8f);
            }
            
            // 同时设置覆盖层为深蓝色
            if (_clickToReturnToTitleOverlay != null)
            {
                var overlayImage = _clickToReturnToTitleOverlay.GetComponent<UnityEngine.UI.Image>();
                if (overlayImage != null)
                {
                    overlayImage.color = new Color(0.1f, 0.2f, 0.4f, 0.01f);
                }
            }
        }
    }

    private void EnsureSettlementTextsBound()
    {
        TryBindByName();
        if (titleText == null || detailText == null || gainText == null || totalText == null)
            TryBindSettlementByContent();
    }

    private void TryBindSettlementByContent()
    {
        if (_resultPanel != null)
        {
            var tmps = _resultPanel.GetComponentsInChildren<TMP_Text>(true);
            if (tmps != null && tmps.Length >= 4)
            {
                System.Array.Sort(tmps, (a, b) =>
                {
                    float ay = a != null && a.rectTransform != null ? a.rectTransform.anchoredPosition.y : 0;
                    float by = b != null && b.rectTransform != null ? b.rectTransform.anchoredPosition.y : 0;
                    return by.CompareTo(ay);
                });
                if (titleText == null && tmps.Length > 0) titleText = tmps[0];
                if (detailText == null && tmps.Length > 1) detailText = tmps[1];
                if (gainText == null && tmps.Length > 2) gainText = tmps[2];
                if (totalText == null && tmps.Length > 3) totalText = tmps[3];
            }
        }
        var panels = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels == null) return;
        foreach (var t in panels)
        {
            if (t == null) continue;
            string txt = t.text ?? "";
            if (titleText == null && (txt.Contains("战斗") || txt.Contains("胜利") || txt.Contains("失败"))) titleText = t;
            if (detailText == null && (txt.Contains("详情") || txt.Contains("关卡"))) detailText = t;
            if (gainText == null && (txt.Contains("收益") || txt.Contains("获得"))) gainText = t;
            if (totalText == null && (txt.Contains("总点") || txt.Contains("本局点"))) totalText = t;
        }
    }

    private static void SetText(TMP_Text tmp, string value)
    {
        if (tmp != null) tmp.text = value;
    }

    private void ApplySimplifiedSettlementDisplay(RogueBattleResult result, RogueSettlementSummary summary)
    {
        HideLegacyUnusedSettlementLabels();

        if (!result.isWin)
        {
            SetText(titleText, "止步");
            if (titleText != null) titleText.color = new Color32(255, 50, 50, 255); // 红色
            HideTextNode(detailText);
            HideTextNode(gainText);
            HideTextNode(totalText);
            return;
        }

        bool isGreatVictory = result.noHit && result.guardianHpEnd >= fullGuardianHpForGreatVictory;
        string gradeText = isGreatVictory ? "大胜" : "胜利";
        Color gradeColor = isGreatVictory
            ? new Color32(255, 215, 0, 255)
            : new Color32(0, 220, 120, 255);

        SetText(titleText, gradeText);
        if (titleText != null) titleText.color = gradeColor;

        HideTextNode(detailText);
        ShowGainAndTotalBelowTitle(summary);
    }

    private static void HideTextNode(TMP_Text text)
    {
        if (text == null) return;
        text.text = "";
        text.gameObject.SetActive(false);
    }

    private void ShowGainAndTotalBelowTitle(RogueSettlementSummary summary)
    {
        if (gainText != null)
        {
            gainText.gameObject.SetActive(true);
            gainText.text = $"收益 +{summary.runPointGain}";
        }

        if (totalText != null)
        {
            totalText.gameObject.SetActive(true);
            totalText.text = $"总点数 {RogueRuntimeState.RunPoint}";
        }
    }

    private void HideLegacyUnusedSettlementLabels()
    {
        HideByName("结算详情");
        HideByName("详情");
        HideByName("详情文本");
    }

    private static void HideByName(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go != null) go.SetActive(false);
    }

    private void SetSettlementTextsActive(bool visible)
    {
        SetTextNodeActive(titleText, visible);
        SetTextNodeActive(detailText, visible);
        SetTextNodeActive(gainText, visible);
        SetTextNodeActive(totalText, visible);
    }

    private void RestoreSettlementTextVisibilityForResult()
    {
        if (!_isBattleWin)
        {
            SetTextNodeActive(titleText, true);
            SetTextNodeActive(detailText, false);
            SetTextNodeActive(gainText, false);
            SetTextNodeActive(totalText, false);
            return;
        }

        SetTextNodeActive(titleText, true);
        SetTextNodeActive(detailText, false);
        SetTextNodeActive(gainText, true);
        SetTextNodeActive(totalText, true);
    }

    private static void SetTextNodeActive(TMP_Text text, bool active)
    {
        if (text == null) return;
        text.gameObject.SetActive(active);
    }

    private IEnumerator ShowResultPanelWithEffects()
    {
        SetResultPanelVisible(true);
        yield return PlaySettlementRevealEffectIfNeeded();
        RefreshTotalText();
    }

    private IEnumerator PlaySettlementRevealEffectIfNeeded()
    {
        if (!_isBattleWin || !_useSimplifiedSettlementView) yield break;
        RestoreSettlementTextVisibilityForResult();
        yield return AnimateVisibleSettlementText(titleText);
    }

    private IEnumerator AnimateVisibleSettlementText(TMP_Text text)
    {
        if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text))
            yield break;

        var rect = text.rectTransform;
        if (rect == null) yield break;

        var cg = text.GetComponent<CanvasGroup>();
        if (cg == null) cg = text.gameObject.AddComponent<CanvasGroup>();

        Vector3 originalScale = rect.localScale;
        Vector3 startScale = originalScale * Mathf.Max(0.1f, settlementTextStartScale);
        cg.alpha = 0f;
        rect.localScale = startScale;

        float duration = Mathf.Max(0.05f, settlementTextFadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            cg.alpha = eased;
            rect.localScale = Vector3.LerpUnclamped(startScale, originalScale, eased);
            yield return null;
        }

        cg.alpha = 1f;
        rect.localScale = originalScale;

        if (useOutlinePulseEffect)
            yield return AnimateOutlinePulse(text);

        float delay = Mathf.Max(0f, settlementTextStaggerDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
    }

    private IEnumerator AnimateOutlinePulse(TMP_Text text)
    {
        if (text == null) yield break;
        var mat = text.fontMaterial;
        if (mat == null) yield break;
        if (!mat.HasProperty(ShaderUtilities.ID_OutlineWidth) || !mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            yield break;

        float baseWidth = mat.GetFloat(ShaderUtilities.ID_OutlineWidth);
        Color baseColor = mat.GetColor(ShaderUtilities.ID_OutlineColor);
        bool isGreatVictory = string.Equals(text.text, "大胜", StringComparison.Ordinal);
        bool isVictory = string.Equals(text.text, "胜利", StringComparison.Ordinal);
        float peak = Mathf.Clamp(isGreatVictory ? greatVictoryOutlinePulsePeakWidth : outlinePulsePeakWidth, 0f, 1f);
        Color targetPulseColor = outlinePulseColor;
        if (isGreatVictory) targetPulseColor = greatVictoryOutlinePulseColor;
        else if (isVictory) targetPulseColor = victoryOutlinePulseColor;
        int count = 1;
        Vector3 baseScale = text.rectTransform != null ? text.rectTransform.localScale : Vector3.one;
        float scalePulse = Mathf.Max(1f, greatVictoryScalePulseMultiplier);

        // 保持单次冲击：不再叠加前摇闪框，避免视觉上像两次脉冲

        for (int i = 0; i < count; i++)
        {
            float t = 0f;
            const float half = 0.1f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Lerp(baseWidth, peak, eased));
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(baseColor, targetPulseColor, eased));
                if (isGreatVictory && text.rectTransform != null)
                    text.rectTransform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * scalePulse, eased);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                float eased = p * p * (3f - 2f * p);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Lerp(peak, baseWidth, eased));
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(targetPulseColor, baseColor, eased));
                if (isGreatVictory && text.rectTransform != null)
                    text.rectTransform.localScale = Vector3.LerpUnclamped(baseScale * scalePulse, baseScale, eased);
                yield return null;
            }
        }

        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, baseWidth);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, baseColor);
        if (text.rectTransform != null) text.rectTransform.localScale = baseScale;
        text.UpdateMeshPadding();

        if (useImpactShakeEffect)
            yield return PlayImpactScaleAndShake(text);
    }

    private IEnumerator PlayGreatVictoryFlash(TMP_Text text, float baseWidth, Color baseColor, Vector3 baseScale)
    {
        if (text == null || text.rectTransform == null) yield break;
        var mat = text.fontMaterial;
        if (mat == null) yield break;

        float duration = Mathf.Max(0.02f, greatVictoryFlashDuration);
        float peak = Mathf.Clamp(greatVictoryOutlinePulsePeakWidth, 0f, 1f);
        float scalePulse = Mathf.Max(1f, greatVictoryScalePulseMultiplier);
        Color flashColor = Color.white;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Lerp(baseWidth, peak, eased));
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(baseColor, flashColor, eased));
            text.rectTransform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * scalePulse, eased);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = p * p * (3f - 2f * p);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Lerp(peak, baseWidth, eased));
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(flashColor, baseColor, eased));
            text.rectTransform.localScale = Vector3.LerpUnclamped(baseScale * scalePulse, baseScale, eased);
            yield return null;
        }
    }

    private IEnumerator PlayImpactScaleAndShake(TMP_Text text)
    {
        if (text == null || text.rectTransform == null) yield break;
        var rt = text.rectTransform;
        Vector3 baseScale = rt.localScale;
        float scaleMul = Mathf.Max(1f, impactScaleMultiplier);
        float scaleDur = Mathf.Max(0.04f, impactScaleDuration);

        float t = 0f;
        while (t < scaleDur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / scaleDur);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            rt.localScale = Vector3.LerpUnclamped(baseScale, baseScale * scaleMul, eased);
            yield return null;
        }

        t = 0f;
        while (t < scaleDur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / scaleDur);
            float eased = p * p * (3f - 2f * p);
            rt.localScale = Vector3.LerpUnclamped(baseScale * scaleMul, baseScale, eased);
            yield return null;
        }
        rt.localScale = baseScale;

        Transform shakeTarget = _resultPanel != null ? _resultPanel.transform : rt.parent;
        if (shakeTarget == null) yield break;

        Vector3 basePos = shakeTarget.localPosition;
        float shakeDur = Mathf.Max(0.05f, impactShakeDuration);
        float strength = Mathf.Max(1f, impactShakeStrength);
        t = 0f;
        while (t < shakeDur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / shakeDur);
            float damper = 1f - p;
            Vector2 noise = UnityEngine.Random.insideUnitCircle * strength * damper;
            shakeTarget.localPosition = basePos + new Vector3(noise.x, noise.y, 0f);
            yield return null;
        }
        shakeTarget.localPosition = basePos;
    }

    private void RefreshTotalText()
    {
        if (_useSimplifiedSettlementView) return;
        SetText(totalText,
            $"当前本局点数:{RogueRuntimeState.RunPoint}\n" +
            $"当前可用点数:{RogueRuntimeState.AvailablePoint}\n" +
            $"当前永久点数:{RogueRuntimeState.PermanentPoint}");
    }

    /// <summary> 显示结算面板时调用：确保绑定完整并强制刷新 TMP 显示（避免面板从隐藏变为显示时文字不更新）。</summary>
    private void RefreshResultPanelTexts()
    {
        EnsureSettlementTextsBound();
        RefreshTotalText();
        if (titleText != null) titleText.ForceMeshUpdate(true, true);
        if (detailText != null) detailText.ForceMeshUpdate(true, true);
        if (gainText != null) gainText.ForceMeshUpdate(true, true);
        if (totalText != null) totalText.ForceMeshUpdate(true, true);
    }

    /// <summary> 回主菜单：本局剩余点数加回可用点数并持久化，再跳转入口。从关卡进结算时场景内无 Router，需直接 LoadScene。 </summary>
    public void BackEntry()
    {
        RogueRuntimeState.EndRunAndBackToEntry();
        if (_flow != null)
            _flow.ReturnEntryFromResult();
        else
            VideoSceneLoader.LoadScene("RogueEntry");
    }

    /// <summary> 下一战：关卡数+1，跳转到 plot 选关。 </summary>
    public void NextBattle()
    {
        RogueRuntimeState.ContinueToNextStage();
        
        // 标记为通关后返回，保留通过状态
        LevelSceneLoadContext.SetFromVictory();
        
        VideoSceneLoader.LoadScene("plot");
    }

    private void EnsureSimpleUiIfMissing()
    {
        if (titleText != null && detailText != null && gainText != null && totalText != null
            && backEntryButton != null && nextBattleButton != null)
            return;

        Transform panel = (titleText != null && titleText.transform.parent != null)
            ? titleText.transform.parent
            : null;

        if (panel == null)
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
            var panelGo = new GameObject("RogueResult_AutoPanel", typeof(RectTransform), typeof(Image));
            panel = panelGo.transform;
            panel.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(820f, 520f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        }

        if (titleText == null) titleText = CreateText(panel, "Title", new Vector2(20f, -20f), 42);
        if (detailText == null) detailText = CreateText(panel, "Detail", new Vector2(20f, -90f), 28);
        if (gainText == null) gainText = CreateText(panel, "Gain", new Vector2(20f, -290f), 28);
        if (totalText == null) totalText = CreateText(panel, "Total", new Vector2(20f, -360f), 26);
        if (backEntryButton == null) backEntryButton = CreateButton(panel, "回入口并结算本局", new Vector2(20f, -450f));
        if (nextBattleButton == null) nextBattleButton = CreateButton(panel, "下一战", new Vector2(320f, -450f));
    }

    private void TryBindByName()
    {
        if (titleText == null)
            titleText = FindTmpInScene("标题文本") ?? FindTmpInScene("TitleText");
        if (detailText == null)
            detailText = FindTmpInScene("详情文本") ?? FindTmpInScene("DetailText");
        if (gainText == null)
            gainText = FindTmpInScene("收益文本") ?? FindTmpInScene("GainText");
        if (totalText == null)
            totalText = FindTmpInScene("总点文本") ?? FindTmpInScene("TotalText");

        if (backEntryButton == null)
            backEntryButton = FindInScene<Button>("回入口按钮") ?? FindInScene<Button>("BackEntryButton");
        if (nextBattleButton == null)
            nextBattleButton = FindInScene<Button>("下一战按钮") ?? FindInScene<Button>("NextBattleButton");
        if (mainMenuButton == null)
        {
            mainMenuButton = FindInScene<Button>("返回主菜单按钮") ?? FindInScene<Button>("主菜单按钮") ?? FindInScene<Button>("MainMenuButton") ?? FindInScene<Button>("主菜单");
        }
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

    private static TMP_Text CreateText(Transform parent, string label, Vector2 pos, int size)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(760f, 120f);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.text = label;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 pos)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(260f, 46f);
        go.GetComponent<Image>().color = Color.white;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        var text = txtGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        text.text = label;

        return go.GetComponent<Button>();
    }

    /// <summary>
    /// 在紫色敌人死亡抽卡模式下，隐藏背景元素（按钮、标题等），只显示卡片选择界面
    /// </summary>
    private void HideBackgroundElements()
    {
        // 隐藏返回主菜单按钮
        var backToMenuBtn = GameObject.Find("返回主菜单按钮");
        if (backToMenuBtn != null) backToMenuBtn.SetActive(false);
        
        // 隐藏回入口按钮
        if (backEntryButton != null) backEntryButton.gameObject.SetActive(false);
        
        // 隐藏下一战按钮
        if (nextBattleButton != null) nextBattleButton.gameObject.SetActive(false);
        
        // 隐藏总点数字文本
        var totalTextObj = GameObject.Find("总点数字");
        if (totalTextObj != null) totalTextObj.SetActive(false);
        
        // 隐藏总点文本标签
        var totalTextLabel = GameObject.Find("总点文本");
        if (totalTextLabel != null) totalTextLabel.SetActive(false);
    }

    private void EnsureCameraExists()
    {
        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool hasActiveCamera = false;
        
        foreach (var cam in cameras)
        {
            if (cam.gameObject.scene.name == "RogueResult" && cam.gameObject.activeInHierarchy)
            {
                hasActiveCamera = true;
                break;
            }
        }
        
        if (!hasActiveCamera)
        {
            // 创建一个新相机
            var cameraGo = new GameObject("RogueResultCamera", typeof(Camera));
            cameraGo.transform.position = new Vector3(0, 0, -10);
            var cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.orthographic = false;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
        }
    }

    private static Sprite GetOrCreateWhitePlaceholderSprite()
    {
        if (_whitePlaceholderSprite != null) return _whitePlaceholderSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var pixels = new Color32[4];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        _whitePlaceholderSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        _whitePlaceholderSprite.name = "WhitePlaceholder";
        return _whitePlaceholderSprite;
    }

    private static Sprite GetOrCreateBlackSquareSprite()
    {
        if (_blackSquareSprite != null) return _blackSquareSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var pixels = new Color32[4];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        _blackSquareSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        _blackSquareSprite.name = "BlackSquare";
        return _blackSquareSprite;
    }

    private void SetSlotIconToBlackSquareWithText(Transform slotRoot)
    {
        if (slotRoot == null) return;
        
        // 先找到对应卡片的数据
        int slotIndex = -1;
        for (int i = 0; i < _cardButtons.Count; i++)
        {
            if (_cardButtons[i] != null && _cardButtons[i].transform == slotRoot)
            {
                slotIndex = i;
                break;
            }
        }
        
        // 第一步：设置卡片正面（完全覆盖整个卡片）
        var rootImg = slotRoot.GetComponent<Image>();
        if (rootImg != null)
        {
            Sprite cardFrontToUse = null;
            if (slotIndex >= 0 && slotIndex < _currentOffers.Length && _currentOffers[slotIndex] != null)
            {
                cardFrontToUse = _currentOffers[slotIndex].cardFront;
            }
            
            if (cardFrontToUse != null)
            {
                rootImg.sprite = cardFrontToUse;
                rootImg.enabled = true;
            }
            else
            {
                rootImg.color = Color.white;
                rootImg.enabled = true;
            }
        }
        
        // 第二步：找到 Icon 并设置为 16:9 比例的小图
        var iconImg = FindChildImageRecursive(slotRoot, "Icon", "卡图", "CardImage", "Image");
        if (iconImg != null)
        {
            // 使用卡片数据中的 icon，如果没有就用黑色正方形作为占位符
            Sprite cardIcon = null;
            if (slotIndex >= 0 && slotIndex < _currentOffers.Length && _currentOffers[slotIndex] != null)
            {
                cardIcon = _currentOffers[slotIndex].icon;
            }
            
            iconImg.sprite = cardIcon != null ? cardIcon : GetOrCreateBlackSquareSprite();
            iconImg.enabled = true;
            iconImg.color = Color.white;
            iconImg.gameObject.SetActive(true);
            iconImg.preserveAspect = false;
            SetActiveUpTo(iconImg.transform, slotRoot);
            
            // 调整 Icon 为上半部分的 16:9 比例
            var iconRect = iconImg.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                var slotRect = slotRoot.GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    float iconWidth = slotRect.sizeDelta.x * 0.5f;
                    float iconHeight = iconWidth * 9f / 16f; // 16:9 比例
                    iconRect.anchorMin = new Vector2(0.5f - 0.25f, 1f - (0.25f * 9f / 16f));
                    iconRect.anchorMax = new Vector2(0.5f + 0.25f, 1f);
                    iconRect.pivot = new Vector2(0.5f, 1f);
                    iconRect.anchoredPosition = Vector2.zero;
                    iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
                }
            }
        }
        
        // 第三步：设置描述文字
        var descTmp = FindChildTMPRecursive(slotRoot, "Desc", "描述", "Description");
        if (descTmp != null)
        {
            // 使用卡片数据中的 description，如果没有就用"能力"作为占位符
            string descText = "能力";
            if (slotIndex >= 0 && slotIndex < _currentOffers.Length && _currentOffers[slotIndex] != null)
            {
                if (!string.IsNullOrEmpty(_currentOffers[slotIndex].description))
                {
                    descText = _currentOffers[slotIndex].description;
                }
            }
            
            descTmp.text = descText;
            descTmp.enabled = true;
            descTmp.gameObject.SetActive(true);
            descTmp.color = Color.black;
            descTmp.alignment = TextAlignmentOptions.Center;
            SetActiveUpTo(descTmp.transform, slotRoot);
            
            // 调整 Desc 文本到下半部分
            var descRect = descTmp.GetComponent<RectTransform>();
            if (descRect != null)
            {
                descRect.anchorMin = Vector2.zero;
                descRect.anchorMax = new Vector2(1f, 0.5f);
                descRect.pivot = new Vector2(0.5f, 0.5f);
                descRect.anchoredPosition = Vector2.zero;
                descRect.sizeDelta = new Vector2(-20f, 0f);
            }
        }
    }
}
