using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BattleScene 启动协调器。
/// 挂载在 BattleScene 的根物体上（如 Managers），负责：
/// 1. 从 LevelSceneLoadContext 读取当关 LevelConfig
/// 2. 调用 MapBuilder 铺地图 + 创建路线
/// 3. 注入波次/路线到 Spawner
/// 4. 应用关卡限制条件到 GameManager / DeploymentManager
/// 5. 配置 LevelEndMenu（标签、剧情卡片等）
///
/// 如果 LevelSceneLoadContext 中没有 LevelConfig（兼容旧场景），则跳过初始化，
/// 走旧有的 Inspector 拖拽模式。
/// </summary>
[DefaultExecutionOrder(-100)] // 在 GridSystem(-50?) 之后，Spawner/LevelEndMenu Start 之前
public class BattleSceneBootstrap : MonoBehaviour
{
    [Header("依赖引用")]
    [Tooltip("MapBuilder 组件（在同一场景中）")]
    public MapBuilder mapBuilder;

    [Tooltip("Spawner 组件")]
    public Spawner spawner;

    [Tooltip("GameManager 组件")]
    public GameManager gameManager;

    [Tooltip("DeploymentManager 组件（如果有关卡限制需要应用）")]
    public DeploymentManager deploymentManager;

    [Tooltip("LevelEndMenu 组件")]
    public LevelEndMenu levelEndMenu;

    [Header("调试")]
    [Tooltip("勾选后，没有 LevelConfig 时使用默认测试配置")]
    public bool useDefaultConfigForTesting = false;

    [Tooltip("测试用默认关卡配置（不填则默认加载 LevelConfigs/Level_01_Battle）。" +
        "在编辑器里直接打开 BattleScene 按 Play（没有通过 LevelSceneLoadContext 传入关卡）时使用。")]
    public LevelConfig defaultTestConfig;

    void Start()
    {
        // 1. 尝试从上下文获取关卡配置
        var config = LevelSceneLoadContext.GetCurrentLevelConfig();

        if (config == null)
        {
            // 直接打开 BattleScene 测试（未经过 plot/选关）时没有传入 LevelConfig，
            // 自动回退到默认测试关卡（level 1），让“打开场景即跑”和老方案一样方便。
            bool allowFallback = useDefaultConfigForTesting;
            if (allowFallback)
            {
                var fallback = ResolveFallbackConfig();
                if (fallback != null)
                {
                    config = fallback;
                    // 直接运行场景测试时，重置可能残留的静态状态，避免上一局的暂停标志卡住游戏
                    ResetStaleStaticState();
                }
                else
                {
                    return;
                }
            }
            else
            {
                return; // 兼容旧场景：没有配置就不注入
            }
        }

        // 2. 解析依赖（未手动拖入则自动查找）
        ResolveDependencies();

        // 3. 构建地图
        if (mapBuilder != null)
        {
            mapBuilder.BuildFromConfig(config);
        }
        else
        {
        }

        // 4. 注入波次和路线到 Spawner
        if (spawner != null && mapBuilder != null)
        {
            mapBuilder.InjectWavesToSpawner(spawner, config);
        }

        // 5. 应用关卡限制条件
        ApplyLevelRestrictions(config);

        // 6. 配置 LevelEndMenu
        ConfigureLevelEndMenu(config);

        // 7. 不清空上下文：BattleScene 运行期间仍可能需要关卡ID/场景名用于重试、结算等逻辑。
        //      下一次进入新关卡时会被新的 SetLevelConfig 覆盖。
    }

    /// <summary>
    /// 解析“直接打开场景测试”时使用的默认关卡配置。
    /// 优先用 Inspector 指定的 defaultTestConfig，否则回退到 Level_01_Battle。
    /// </summary>
    LevelConfig ResolveFallbackConfig()
    {
        if (defaultTestConfig != null) return defaultTestConfig;
        var c = Resources.Load<LevelConfig>("LevelConfigs/Level_01_Battle");
        if (c == null) c = Resources.Load<LevelConfig>("LevelConfigs/Level_01");
        return c;
    }

    void ResolveDependencies()
    {
        if (mapBuilder == null)
            mapBuilder = FindFirstObjectByType<MapBuilder>();

        if (spawner == null)
            spawner = FindFirstObjectByType<Spawner>();

        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (deploymentManager == null)
            deploymentManager = FindFirstObjectByType<DeploymentManager>();

        if (levelEndMenu == null)
            levelEndMenu = LevelEndMenu.Instance ?? FindFirstObjectByType<LevelEndMenu>();
    }

    /// <summary>
    /// 将 LevelConfig 的限制条件应用到游戏运行时。
    /// </summary>
    void ApplyLevelRestrictions(LevelConfig config)
    {
        // 守护点血量
        if (gameManager != null && config.maxLifePoint > 0)
        {
            gameManager.maxPlayerHealth = config.maxLifePoint;
            gameManager.playerHealth = config.maxLifePoint;
        }

        // 初始 DP
        if (deploymentManager != null && config.startDP >= 0)
        {
            deploymentManager.currentDP = config.startDP;
        }

        // 最大部署数
        if (deploymentManager != null && config.maxDeployCount > 0)
        {
            deploymentManager.maxDP = Mathf.Min(deploymentManager.maxDP, config.maxDeployCount);
        }

        // 混合模式：应用敌人倍率修饰到全局运行时状态
        LevelRunModifiers.Apply(config);
    }

    /// <summary>
    /// 根据 LevelConfig 配置 LevelEndMenu 的标签和卡片。
    /// </summary>
    void ConfigureLevelEndMenu(LevelConfig config)
    {
        if (levelEndMenu == null) return;

        // 设置肉鸽结算标记（所有 battle 场景都走肉鸽结算）
        levelEndMenu.goToRogueResultOnContinue = true;

        // 设置关卡类型
        levelEndMenu.battleType = ConvertLevelType(config.levelType);

        // 设置标签
        int currentLevelId = LevelSceneLoadContext.GetCurrentLevelId();
        if (!string.IsNullOrEmpty(config.afterLevelLabel) && currentLevelId == config.levelId)
        {
            levelEndMenu.labelName = config.afterLevelLabel;
        }
        else if (!string.IsNullOrEmpty(config.afterLevelLabel) && currentLevelId > 0 && currentLevelId != config.levelId)
        {
            levelEndMenu.labelName = $"AfterLevel{currentLevelId}";
        }
        else
        {
            // 自动生成标签：AfterLevel + 关卡ID
            int labelId = currentLevelId > 0 ? currentLevelId : config.levelId;
            levelEndMenu.labelName = $"AfterLevel{labelId}";
        }

        // 设置剧情卡片
        if (config.cardToUnlockOnWin != null)
        {
            levelEndMenu.cardToUnlockOnWin = config.cardToUnlockOnWin;
        }
    }

    static BattleType ConvertLevelType(LevelType levelType)
    {
        switch (levelType)
        {
            case LevelType.Elite: return BattleType.Elite;
            case LevelType.Boss: return BattleType.Boss;
            case LevelType.NormalBattle:
            default: return BattleType.Normal;
        }
    }

    /// <summary>
    /// 直接运行场景测试时重置可能残留的静态状态，防止上一局的暂停标志卡住游戏。
    /// </summary>
    static void ResetStaleStaticState()
    {
        // RogueResultController 的静态标志在正常流程中由结算界面设置/清除，
        // 但直接运行场景时可能残留为 true，导致 NewbieTutorialController 结束后不恢复 timeScale。
        var rrcType = System.Type.GetType("RogueResultController");
        if (rrcType == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                rrcType = asm.GetType("RogueResultController");
                if (rrcType != null) break;
            }
        }
        if (rrcType != null)
        {
            var isFirst = rrcType.GetField("IsFirstStageDrop",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var isMid = rrcType.GetField("IsMidGameDrop",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (isFirst != null) isFirst.SetValue(null, false);
            if (isMid != null) isMid.SetValue(null, false);
        }

        // 确保 timeScale 恢复正常（NewbieTutorialController 会在 Start 时重新设为 0）
        Time.timeScale = 1f;
    }
}
