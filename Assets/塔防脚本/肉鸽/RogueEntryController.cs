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
        // 先把选人面板选中的阵容 + 各自升星等级写入本局养成状态
        var panel = FindFirstObjectByType<OperatorSelectionPanel>();
        if (panel != null) panel.ConfirmRoster();

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
            config = null;
        }
        if (config != null)
        {
            RogueRuntimeState.SetRunModifierConfig(config);
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
    /// 确保 GameMode 下拉选择 UI 已绑定。
    /// 场景中必须存在名为 "GameModeDropdown" 的 TMP_Dropdown 并拖拽到 _gameModeDropdown 字段。
    /// 如果未手动绑定，会尝试按名称查找。
    /// </summary>
    private void EnsureGameModeUI()
    {
        // 尝试查找已有 dropdown（按名称自动绑定）
        if (_gameModeDropdown == null)
        {
            var existing = GameObject.Find("GameModeDropdown");
            if (existing != null)
                _gameModeDropdown = existing.GetComponent<TMP_Dropdown>();
        }

        if (_gameModeDropdown == null)
        {
            return;
        }

        // 设置选项
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

        // 更新按钮上的文字
        string modeName = mode switch
        {
            GameMode.Fixed => "固定模式",
            GameMode.Hybrid => "混合模式",
            GameMode.Random => "随机模式",
            _ => "未知"
        };
        var captionTr = _gameModeDropdown.transform.Find("Caption");
        if (captionTr != null)
        {
            var caption = captionTr.GetComponent<TMPro.TextMeshProUGUI>();
            if (caption != null)
                caption.text = "模式选择：" + modeName;
        }

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
