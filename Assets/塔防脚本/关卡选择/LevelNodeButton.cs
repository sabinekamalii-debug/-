using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum LevelType
{
    NormalBattle,
    Shop,
    Elite,
    Boss,
    RandomEvent,
    Rest
}

/// <summary>
/// 地图上的关卡节点：点击进入对应关卡。根据 LevelProgress 显示已解锁/未解锁。
/// 物体名带数字即对应关卡：按钮1→level 1，按钮2→level 2，…，按钮16→level 16（一一对应）。
/// </summary>
[RequireComponent(typeof(Button))]
public class LevelNodeButton : MonoBehaviour
{
    string _sceneName;
    string _displayName;
    Sprite _normalSprite;
    int _levelNumber;

    [Header("未解锁时的表现")]
    [SerializeField] Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("已通关时的表现")]
    [Tooltip("已通关的关卡只变暗、不可再次挑战，不变成封锁样式")]
    [SerializeField] Color completedDimColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("可选绑定")]
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] Image frameImage;

    [Header("关卡类型配置")]
    public LevelTypeConfig levelTypeConfig;

    [Header("新架构")]
    [Tooltip("勾选后优先尝试从 Resources/LevelConfigs/ 加载关卡配置，走新 BattleScene 流程。未找到时回退旧场景。")]
    public bool useNewArchitecture = true;

    /// <summary> 缓存的当前关卡配置（新架构） </summary>
    private LevelConfig _cachedLevelConfig;
    private bool _loadedInvalidConfig;

    private static readonly string[] FallbackBattleSceneNames = new[]
    {
        "level 1","level 2","level 3","level 4","level 5","level 6","level 7","level 8",
        "level 9","level 10","level 11",
        "level elite","level boss"
    };

    private static readonly int[] FallbackBattleConfigIds = new[]
    {
        1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21
    };

    Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnClick);
            // Button 在 interactable=false 时会用 disabledColor 染 Target Graphic，默认 alpha 约 0.5 会发虚，改为完全不透明
            var colors = _button.colors;
            colors.disabledColor = new Color(colors.disabledColor.r, colors.disabledColor.g, colors.disabledColor.b, 1f);
            _button.colors = colors;
        }

        if (nameText == null) nameText = GetComponentInChildren<TMP_Text>(true);
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>(true);
        if (iconImage != null) _normalSprite = iconImage.sprite;

        // 根据物体名决定显示数字和关卡：按钮5 / LevelNode_5 → 显示 "5"，跳转 "level 5"
        string num = GetNumberFromGameObjectName(gameObject.name);
        if (!string.IsNullOrEmpty(num))
        {
            _displayName = num;
            _sceneName = "level " + num;
            if (int.TryParse(num, out int n))
            {
                _levelNumber = n;
            }
        }
        else
        {
            _displayName = "?";
            _sceneName = "";
            _levelNumber = 0;
        }

        // 尝试加载新架构的关卡配置
        TryLoadLevelConfig();
    }

    /// <summary>
    /// 尝试从 Resources/LevelConfigs/ 加载本关的 LevelConfig。
    /// 命名规则：Level_{关卡号}_Battle 或 Level_{关卡号}。
    /// </summary>
    void TryLoadLevelConfig()
    {
        if (!useNewArchitecture || _levelNumber <= 0) return;

        // 随机/混合模式下，按关卡类型从对应池中取打乱后的 LevelConfig ID。
        // 普通关卡只随机普通关卡，精英只随机精英，Boss 只随机 Boss。
        LevelType btnType = GetLevelType(_levelNumber);
        int configId = RogueRuntimeState.GetLevelConfigIdForStage(_levelNumber, btnType);

        // 支持多种命名格式
        string[] possibleNames = {
            $"Level_{configId:D2}_Battle",  // Level_03_Battle
            $"Level_{configId}_Battle",      // Level_3_Battle
            $"LevelConfig_{configId}",
        };

        foreach (var name in possibleNames)
        {
            _cachedLevelConfig = Resources.Load<LevelConfig>($"LevelConfigs/{name}");
            if (_cachedLevelConfig != null)
                break;
        }

        if (_cachedLevelConfig != null && !IsPlayableLevelConfig(_cachedLevelConfig))
        {
            _cachedLevelConfig = GetPlayableFallbackLevelConfig();
            _loadedInvalidConfig = true;

        }
    }

    bool IsPlayableLevelConfig(LevelConfig config)
    {
        if (config == null) return false;
        if (config.gridData == null || config.gridData.Length < config.gridWidth * config.gridHeight) return false;
        if (config.waveGroups == null || config.waveGroups.Length == 0) return false;

        bool hasWave = false;
        foreach (var group in config.waveGroups)
        {
            if (group == null || group.entries == null) continue;
            foreach (var entry in group.entries)
            {
                if (entry != null && entry.count > 0)
                {
                    hasWave = true;
                    break;
                }
            }
            if (hasWave) break;
        }
        if (!hasWave) return false;

        var paths = config.GetAllPaths();
        bool hasPath = false;
        if (paths != null)
        {
            foreach (var path in paths)
            {
                if (path != null && path.Length > 0)
                {
                    hasPath = true;
                    break;
                }
            }
        }
        return hasPath;
    }

    LevelConfig GetPlayableFallbackLevelConfig(LevelType desiredType = LevelType.NormalBattle)
    {
        var exactMatches = new List<LevelConfig>();
        var fallbackMatches = new List<LevelConfig>();

        foreach (var id in FallbackBattleConfigIds)
        {
            var config = LoadLevelConfigById(id);
            if (config == null || !IsPlayableLevelConfig(config))
                continue;

            if (IsConfigAcceptableForLevelType(config, desiredType))
                exactMatches.Add(config);
            else
                fallbackMatches.Add(config);
        }

        if (exactMatches.Count > 0)
            return exactMatches[Random.Range(0, exactMatches.Count)];
        if (fallbackMatches.Count > 0)
            return fallbackMatches[Random.Range(0, fallbackMatches.Count)];
        return null;
    }

    bool IsConfigAcceptableForLevelType(LevelConfig config, LevelType desiredType)
    {
        if (config == null) return false;
        if (desiredType == LevelType.NormalBattle)
            return config.levelType == LevelType.NormalBattle;
        if (desiredType == LevelType.Elite)
            return config.levelType == LevelType.Elite;
        if (desiredType == LevelType.Boss)
            return config.levelType == LevelType.Boss;
        return config.levelType == LevelType.NormalBattle;
    }

    LevelConfig LoadLevelConfigById(int id)
    {
        string[] names = {
            $"Level_{id:D2}_Battle",
            $"Level_{id}_Battle",
            $"LevelConfig_{id}",
        };
        foreach (var name in names)
        {
            var config = Resources.Load<LevelConfig>($"LevelConfigs/{name}");
            if (config != null)
                return config;
        }
        return null;
    }

    bool IsSceneLoadable(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return Application.CanStreamedLevelBeLoaded(sceneName);
    }

    string GetFallbackOldSceneName()
    {
        foreach (var scene in FallbackBattleSceneNames)
        {
            if (IsSceneLoadable(scene))
                return scene;
        }
        return null;
    }

    LevelType GetLevelType(int levelNum)
    {
        return LevelRandomizer.GetLevelType(levelNum);
    }

    void Start()
    {
        RefreshLockState();
    }

    void RefreshLockState()
    {
        bool unlocked = LevelProgress.IsUnlocked(_sceneName);
        bool completed = LevelProgress.IsCompleted(_sceneName);

        // 测试阶段：已通关的关卡仍可重复进入（仅变暗标记）
        bool canEnter = unlocked;
        if (_button != null)
            _button.interactable = canEnter;

        // 强制不透明（alpha=1），避免图标/按钮/文字发虚
        Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        // 获取关卡类型对应的图标
        Sprite typeSprite = GetTypeSprite();

        if (iconImage != null)
        {
            if (completed)
            {
                iconImage.sprite = typeSprite != null ? typeSprite : _normalSprite;
                iconImage.color = Opaque(completedDimColor);
            }
            else if (unlocked)
            {
                iconImage.sprite = typeSprite != null ? typeSprite : _normalSprite;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = typeSprite != null ? typeSprite : _normalSprite;
                iconImage.color = Opaque(lockedColor);
            }
        }

        UpdateFrameAndText(unlocked, completed);
    }

    void LateUpdate()
    {
        if (iconImage != null && levelTypeConfig != null)
        {
            bool unlocked = LevelProgress.IsUnlocked(_sceneName);
            bool completed = LevelProgress.IsCompleted(_sceneName);
            
            // 确保图标正确，防止被Button组件覆盖
            Sprite typeSprite = GetTypeSprite();
            if (typeSprite != null && iconImage.sprite != typeSprite)
            {
                if (completed)
                {
                    iconImage.sprite = typeSprite;
                }
                else if (unlocked)
                {
                    iconImage.sprite = typeSprite;
                }
                else
                {
                    iconImage.sprite = typeSprite;
                }
            }
        }
    }

    Sprite GetTypeSprite()
    {
        if (levelTypeConfig == null) return null;

        LevelType type = GetLevelType(_levelNumber);
        switch (type)
        {
            case LevelType.Shop:
                return levelTypeConfig.shopIcon;
            case LevelType.Elite:
                return levelTypeConfig.eliteIcon;
            case LevelType.Boss:
                return levelTypeConfig.bossIcon;
            case LevelType.RandomEvent:
                return levelTypeConfig.randomEventIcon;
            case LevelType.Rest:
                return levelTypeConfig.restIcon;
            case LevelType.NormalBattle:
            default:
                return levelTypeConfig.normalBattleIcon;
        }
    }

    void UpdateFrameAndText(bool unlocked, bool completed)
    {
        Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
        Color frameColor = completed ? completedDimColor : (unlocked ? Color.white : lockedColor);
        if (frameImage != null) frameImage.color = Opaque(frameColor);
        if (nameText != null)
        {
            nameText.color = Opaque(completed ? completedDimColor : (unlocked ? Color.white : lockedColor));
            if (!string.IsNullOrEmpty(_displayName)) nameText.text = _displayName;
        }
    }

    /// <summary> 从物体名里取出数字，如 "按钮3" -> "3"，"按钮16" -> "16"。 </summary>
    static string GetNumberFromGameObjectName(string goName)
    {
        if (string.IsNullOrEmpty(goName)) return null;
        int i = 0;
        while (i < goName.Length && !char.IsDigit(goName[i])) i++;
        if (i >= goName.Length) return null;
        int start = i;
        while (i < goName.Length && char.IsDigit(goName[i])) i++;
        return goName.Substring(start, i - start);
    }

    void OnClick()
    {
        if (!LevelProgress.IsUnlocked(_sceneName)) return;
        // 测试阶段：已通关的关卡仍可重复进入
        if (string.IsNullOrEmpty(_sceneName)) return;
        LevelProgress.OnEnterLevel(_sceneName);
        LevelSceneLoadContext.SetFromSelection();

        // 商店节点 → 加载 GoldShop 场景
        LevelType levelType = GetLevelType(_levelNumber);

        // spc_skip / 事件免费跳过：可跳过 1 场普通战斗
        if (levelType == LevelType.NormalBattle)
        {
            if (RogueRuntimeState.HasFreeSkip)
            {
                RogueRuntimeState.TryConsumeFreeSkip();
                string levelKey = _cachedLevelConfig != null
                    ? $"level{_levelNumber}" : _sceneName;
                LevelProgress.MarkCompleted(levelKey);
                VideoSceneLoader.LoadScene(SceneNames.Plot);
                return;
            }
            if (RogueRuntimeState.CanSkipBattle)
            {
                RogueRuntimeState.ConsumeSkipBattle();
                string levelKey = _cachedLevelConfig != null
                    ? $"level{_levelNumber}" : _sceneName;
                LevelProgress.MarkCompleted(levelKey);
                VideoSceneLoader.LoadScene(SceneNames.Plot);
                return;
            }
        }

        if (levelType == LevelType.Shop)
        {
            ShopReturnContext.SetShopLevel(_levelNumber);
            // 非战斗节点自动标记完成，解锁下一关；可反复进入
            LevelProgress.MarkCompleted(_sceneName);
            VideoSceneLoader.LoadScene(SceneNames.GoldShop);
            return;
        }

        // 随机事件节点 → 统一加载 RandomEvent 场景（背景图由 RandomEventData.backgroundImage 驱动）
        if (levelType == LevelType.RandomEvent)
        {
            // 非战斗节点自动标记完成，解锁下一关；可反复进入
            LevelProgress.MarkCompleted(_sceneName);
            VideoSceneLoader.LoadScene("RandomEvent");
            return;
        }

        // 休息节点 → 加载 Rest 场景
        if (levelType == LevelType.Rest)
        {
            RestReturnContext.SetRestLevel(_levelNumber);
            // 非战斗节点自动标记完成，解锁下一关；可反复进入
            LevelProgress.MarkCompleted(_sceneName);
            VideoSceneLoader.LoadScene(SceneNames.Rest);
            return;
        }

        // ===== 新架构：使用 LevelConfig 加载 BattleScene =====
        if (_cachedLevelConfig == null && (levelType == LevelType.NormalBattle || levelType == LevelType.Elite || levelType == LevelType.Boss))
        {
            _cachedLevelConfig = GetPlayableFallbackLevelConfig(levelType);
            _loadedInvalidConfig = true;
        }

        if (_cachedLevelConfig != null)
        {
            LevelSceneLoadContext.SetLevelConfig(_cachedLevelConfig, _levelNumber, _sceneName);

            // 检查是否有战前对话
            ActConfig actConfig = RogueRuntimeState.CurrentActConfig;
            if (actConfig != null && actConfig.preBattleDialogues != null)
            {
                PreBattleDialogue preBattleDialogue = null;
                foreach (var dialogue in actConfig.preBattleDialogues)
                {
                    if (dialogue.stageNumber == _levelNumber)
                    {
                        preBattleDialogue = dialogue;
                        break;
                    }
                }
                if (preBattleDialogue != null)
                {
                    if (preBattleDialogue.useNaninovel
                        && !string.IsNullOrEmpty(preBattleDialogue.labelName)
                        && !string.IsNullOrEmpty(actConfig.mainScriptName))
                    {
                        // 跳转 Naninovel 播放剧情，播完后加载 BattleScene
                        NaninovelReturnRequest.Set(actConfig.mainScriptName, preBattleDialogue.labelName, SceneNames.BattleScene);
                        VideoSceneLoader.LoadScene(SceneNames.Title);
                        return;
                    }

                    if (preBattleDialogue.inSceneLines != null && preBattleDialogue.inSceneLines.Length > 0)
                    {
                        // 场景内文字对话：传递给 BattleScene 的 NewbieTutorialController
                        LevelSceneLoadContext.SetPreBattleDialogueLines(preBattleDialogue.inSceneLines);
                    }
                }
            }

            VideoSceneLoader.LoadScene(SceneNames.BattleScene);
            return;
        }

        // ===== 旧架构回退：直接加载 level N 场景 =====
        if (_levelNumber == 1)
        {
            RogueResultController.IsFirstStageDrop = true;
            RogueResultController.IsMidGameDrop = true;
            VideoSceneLoader.LoadScene(_sceneName, () =>
            {
                VideoSceneLoader.Instance.StartCoroutine(WaitForDialogueThenShowDrop());
            });
            return;
        }

        if (IsSceneLoadable(_sceneName))
        {
            VideoSceneLoader.LoadScene(_sceneName);
            return;
        }

        string fallbackScene = GetFallbackOldSceneName();
        if (!string.IsNullOrEmpty(fallbackScene))
        {
            VideoSceneLoader.LoadScene(fallbackScene);
            return;
        }
    }

    static IEnumerator WaitForDialogueThenShowDrop()
    {
        HideNaninovelUIAndCamera();

        // 等待新手教程开始（NewbieTutorialController.IsTutorialActive 变为 true）
        float waitStart = 0f;
        while (!NewbieTutorialController.IsTutorialActive && waitStart < 10f)
        {
            waitStart += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!NewbieTutorialController.IsTutorialActive)
        {
            Time.timeScale = 0f;
            SceneManager.LoadScene(SceneNames.RogueResult, LoadSceneMode.Additive);
            yield break;
        }

        // 等待新手教程结束（IsTutorialActive 变为 false）
        while (NewbieTutorialController.IsTutorialActive)
        {
            yield return null;
        }
        Time.timeScale = 0f;
        SceneManager.LoadScene(SceneNames.RogueResult, LoadSceneMode.Additive);
    }

    static void HideNaninovelUIAndCamera()
    {
        // 隐藏 Naninovel<Runtime>/UI 下的所有 Canvas（TitleUI 等）
        var naninovel = GameObject.Find("Naninovel<Runtime>");
        if (naninovel == null)
        {
            return;
        }

        var ui = naninovel.transform.Find("UI");
        if (ui != null)
        {
            // 隐藏 UI 根物体
            ui.gameObject.SetActive(false);
        }

        // 禁用 Naninovel 的相机
        var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            if (cam == null) continue;
            // Naninovel 相机通常不在当前场景中（在 DontDestroyOnLoad 中）
            if (cam.gameObject.scene.name == "DontDestroyOnLoad")
            {
                cam.gameObject.SetActive(false);
            }
        }
    }
}
