using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RogueEntry 场景入口控制：
/// - 显示天赋点
/// - 开始本局
/// - 天赋树
/// - 进入收藏页
/// </summary>
public class RogueEntryController : MonoBehaviour
{
    [Header("可选手动绑定（不绑会自动查找）")]
    [SerializeField] private TMP_Text availablePointText;
    [SerializeField] private TMP_Text runPointText;
    [SerializeField] private TMP_Text permanentPointText;
    [SerializeField] private Button startRunButton;
    [SerializeField] private Button exchangeButton;
    [SerializeField] private Button collectionButton;

    [Header("GameMode 选择")]
    [Tooltip("不绑定时会在 Canvas 上自动创建")]
    [SerializeField] private TMP_Dropdown _gameModeDropdown;

    private RogueFlowRouter _flow;

    private void Awake()
    {
        RogueRuntimeState.InitIfNeeded();
        _flow = FindFirstObjectByType<RogueFlowRouter>();
        TryBindByName();
        BindButtons();
        EnsureGameModeUI();
        RogueUIUtil.EnsureButtonLabelTmp(startRunButton);
        RogueUIUtil.EnsureButtonLabelTmp(exchangeButton);
        RogueUIUtil.EnsureButtonLabelTmp(collectionButton);
        RogueUIUtil.EnsureButtonsVisible(startRunButton, exchangeButton, collectionButton);
        RogueUIUtil.DisableCrossSceneUIBlockers();
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
            exchangeButton.onClick.RemoveListener(OpenTalentTree);
            exchangeButton.onClick.AddListener(OpenTalentTree);
        }

        if (collectionButton != null)
        {
            collectionButton.onClick.RemoveListener(OpenCollection);
            collectionButton.onClick.AddListener(OpenCollection);
        }
    }

    public void StartRun()
    {
        EnsureRunModifierConfig();
        RogueRuntimeState.StartRunIfNeeded();
        RefreshTexts();
        if (_flow != null) _flow.EnterBattleFromEntry();
    }

    private static void EnsureRunModifierConfig()
    {
        if (RogueRuntimeState.ModifierConfig != null) return;
        RunModifierConfig config = null;
        try
        {
            config = Resources.Load<RunModifierConfig>("LevelConfigs/RunModifierConfig");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RogueEntryController] Resources.Load 异常: {e.Message}");
            config = null;
        }
        if (config != null)
        {
            RogueRuntimeState.SetRunModifierConfig(config);
            Debug.Log("[RogueEntryController] RunModifierConfig 从 .asset 加载成功");
        }
        else
        {
            var fallback = ScriptableObject.CreateInstance<RunModifierConfig>();
            fallback.fixedCutoff = BalanceConfig.HybridFixedCutoff;
            fallback.enemyHpMin = BalanceConfig.HybridEnemyHpMin;
            fallback.enemyHpMax = BalanceConfig.HybridEnemyHpMax;
            fallback.enemySpeedMin = BalanceConfig.HybridEnemySpeedMin;
            fallback.enemySpeedMax = BalanceConfig.HybridEnemySpeedMax;
            fallback.startDPOffsetMin = BalanceConfig.HybridStartDPOffsetMin;
            fallback.startDPOffsetMax = BalanceConfig.HybridStartDPOffsetMax;
            fallback.maxLifePointOffsetMin = BalanceConfig.HybridMaxLifePointOffsetMin;
            fallback.maxLifePointOffsetMax = BalanceConfig.HybridMaxLifePointOffsetMax;
            fallback.enemySwapChance = BalanceConfig.HybridEnemySwapChance;
            fallback.hpGrowthPerStage = BalanceConfig.HybridHpGrowthPerStage;
            fallback.speedGrowthPerStage = BalanceConfig.HybridSpeedGrowthPerStage;
            RogueRuntimeState.SetRunModifierConfig(fallback);
            Debug.Log("[RogueEntryController] RunModifierConfig.asset 缺失，已用 BalanceConfig 常量创建兜底配置");
        }
    }

    public void OpenTalentTree()
    {
        if (Application.isPlaying)
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.SoulShop);
    }

    public void OpenCollection()
    {
        if (_flow != null) _flow.EnterCollectionFromEntry();
    }

    private void RefreshTexts()
    {
        if (availablePointText != null)
            availablePointText.text = $"天赋点: {TalentTreeState.TalentPoints}";
        if (runPointText != null)
            runPointText.text = "";
        if (permanentPointText != null)
            permanentPointText.text = "";
    }

    /// <summary>
    /// 确保 GameMode 下拉选择 UI 存在。
    /// 如果场景中已有名为 "GameModeDropdown" 的 TMP_Dropdown，直接绑定；
    /// 否则在 Canvas 上自动创建。
    /// </summary>
    private void EnsureGameModeUI()
    {
        // 尝试查找已有 dropdown
        if (_gameModeDropdown == null)
        {
            var existing = GameObject.Find("GameModeDropdown");
            if (existing != null)
                _gameModeDropdown = existing.GetComponent<TMP_Dropdown>();
        }

        // 如果场景中没有，在 Canvas 上自动创建
        if (_gameModeDropdown == null)
        {
            var canvas = RogueUIUtil.FindSceneCanvas();
            if (canvas == null) return;

            // 找到开始按钮的位置，把下拉菜单放在其下方
            var startBtn = GameObject.Find("开始本局按钮");
            Vector2 basePos = new Vector2(-900, 230);
            Vector2 startBtnSize = new Vector2(260, 55);
            if (startBtn != null)
            {
                var startRt = startBtn.GetComponent<RectTransform>();
                if (startRt != null)
                {
                    basePos = new Vector2(startRt.anchoredPosition.x, startRt.anchoredPosition.y - 70);
                    startBtnSize = startRt.sizeDelta;
                }
            }

            // 创建标签
            var labelGo = new GameObject("GameModeLabel", typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(canvas.transform, false);
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = "关卡模式";
            labelTmp.fontSize = 24;
            labelTmp.alignment = TextAlignmentOptions.MidlineRight;
            labelTmp.color = Color.white;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(1f, 0.5f);
            labelRt.anchoredPosition = new Vector2(basePos.x - 5, basePos.y);
            labelRt.sizeDelta = new Vector2(100, 30);

            // 创建下拉框
            var go = new GameObject("GameModeDropdown", typeof(TMP_Dropdown));
            go.transform.SetParent(canvas.transform, false);
            _gameModeDropdown = go.GetComponent<TMP_Dropdown>();
            var dropdownRt = go.GetComponent<RectTransform>();
            dropdownRt.anchorMin = new Vector2(0.5f, 0.5f);
            dropdownRt.anchorMax = new Vector2(0.5f, 0.5f);
            dropdownRt.pivot = new Vector2(0f, 0.5f);
            dropdownRt.anchoredPosition = new Vector2(basePos.x + 5, basePos.y);
            dropdownRt.sizeDelta = new Vector2(startBtnSize.x, 30);

            // 添加选项文本（TMP_Dropdown 需要 Template 结构）
            var captionTrans = new GameObject("Label").transform;
            captionTrans.SetParent(go.transform, false);
            var captionText = captionTrans.gameObject.AddComponent<TextMeshProUGUI>();
            captionText.fontSize = 22;
            captionText.alignment = TextAlignmentOptions.Center;
            captionText.color = Color.white;

            var templateGo = new GameObject("Template", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect), typeof(UnityEngine.UI.Mask), typeof(UnityEngine.UI.ContentSizeFitter));
            templateGo.transform.SetParent(go.transform, false);
            templateGo.SetActive(false);
            var templateRt = templateGo.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 0);
            templateRt.anchorMax = new Vector2(1, 0);
            templateRt.pivot = new Vector2(0.5f, 1);
            templateRt.anchoredPosition = new Vector2(0, 0);
            templateRt.sizeDelta = new Vector2(0, 120);

            var viewport = new GameObject("Viewport", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            viewport.transform.SetParent(templateGo.transform, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = new Vector2(0, 0);
            viewportRt.anchorMax = new Vector2(1, 1);
            viewportRt.pivot = new Vector2(0, 1);
            viewportRt.sizeDelta = new Vector2(0, 0);
            viewportRt.offsetMin = new Vector2(0, 0);
            viewportRt.offsetMax = new Vector2(0, 0);

            var content = new GameObject("Content", typeof( RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = new Vector2(0, 0);
            contentRt.sizeDelta = new Vector2(0, 28);

            var item = new GameObject("Item", typeof(UnityEngine.UI.Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 0.5f);
            itemRt.anchorMax = new Vector2(1, 0.5f);
            itemRt.pivot = new Vector2(0.5f, 0.5f);
            itemRt.sizeDelta = new Vector2(0, 30);

            var itemLabel = new GameObject("ItemLabel", typeof(TextMeshProUGUI));
            itemLabel.transform.SetParent(item.transform, false);
            var itemLabelTmp = itemLabel.GetComponent<TextMeshProUGUI>();
            itemLabelTmp.fontSize = 22;
            itemLabelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabelTmp.color = Color.black;
            var itemLabelRt = itemLabel.GetComponent<RectTransform>();
            itemLabelRt.anchorMin = new Vector2(0, 0);
            itemLabelRt.anchorMax = new Vector2(1, 1);
            itemLabelRt.pivot = new Vector2(0.5f, 0.5f);
            itemLabelRt.sizeDelta = new Vector2(0, 0);
            itemLabelRt.offsetMin = new Vector2(10, 0);
            itemLabelRt.offsetMax = new Vector2(-10, 0);

            // 连接 TMP_Dropdown 的引用
            _gameModeDropdown.captionText = captionText;
            _gameModeDropdown.itemText = itemLabelTmp;
            _gameModeDropdown.template = templateGo.GetComponent<RectTransform>();
        }

        if (_gameModeDropdown == null) return;

        // 设置选项 - 使用反射设置 captionText 和 itemTemplate
        _gameModeDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("固定模式", null),
            new TMP_Dropdown.OptionData("混合模式", null),
            new TMP_Dropdown.OptionData("随机模式", null),
        };
        _gameModeDropdown.AddOptions(options);

        // 同步当前模式
        _gameModeDropdown.value = (int)RogueRuntimeState.CurrentGameMode;

        // 监听变化
        _gameModeDropdown.onValueChanged.RemoveAllListeners();
        _gameModeDropdown.onValueChanged.AddListener(OnGameModeChanged);
    }

    private void OnGameModeChanged(int index)
    {
        var mode = (GameMode)index;
        RogueRuntimeState.SetGameMode(mode);
        Debug.Log($"[RogueEntryController] 关卡模式切换为: {mode}");
    }

    private void TryBindByName()
    {
        if (availablePointText == null)
            availablePointText = FindTmpInScene("可用点文本");
        if (runPointText == null)
            runPointText = FindTmpInScene("本局点文本");
        if (permanentPointText == null)
            permanentPointText = FindTmpInScene("永久点文本");

        if (startRunButton == null)
            startRunButton = FindInScene<Button>("开始本局按钮");
        if (exchangeButton == null)
            exchangeButton = FindInScene<Button>("天赋树按钮") ?? FindInScene<Button>("点数兑换按钮");
        if (collectionButton == null)
            collectionButton = FindInScene<Button>("进入收藏页按钮");
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
}
