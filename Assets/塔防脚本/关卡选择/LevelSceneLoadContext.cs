using UnityEngine;

/// <summary>
/// 关卡场景加载上下文：记录两个不同入口间如何进入关卡。
/// 
/// 作用：区分以下三种情况，便于 LevelMapController/GameManager 采取不同的初始化策略
/// 1. 从存档/选关进入（LoadScene("level 1")）→ FromSelection
/// 2. 重新挑战/死亡重进（LoadScene(sceneName)）→ FromRetry
/// 3. 通关后经过结算返回（从LevelEndMenu/RogueResult）→ FromVictory
/// 
/// 使用方式：
/// 进入关卡时：LevelSceneLoadContext.SetFromSelection();
/// 进入关卡时：LevelSceneLoadContext.SetFromRetry();  
/// 返回选关：LevelSceneLoadContext.SetFromVictory();
/// 在选关场景（plot）检查：var context = LevelSceneLoadContext.GetAndClear();
/// 
/// 【新增】新架构下，进入 BattleScene 时同时传递 LevelConfig：
/// LevelSceneLoadContext.SetLevelConfig(config);
/// VideoSceneLoader.LoadScene("BattleScene");
/// BattleScene 加载后通过 GetCurrentLevelConfig() 获取配置。
/// </summary>
public enum LevelSceneLoadType
{
    None = 0,
    FromSelection = 1,  // 从选关界面点击进入
    FromRetry = 2,      // 重新挑战/死亡重新进入
    FromVictory = 3     // 通关后经过结算界面回来
}

public class LevelSceneLoadContext
{
    public LevelSceneLoadType loadType;
    public string fromScene;  // 从哪个场景来的
    
    private static LevelSceneLoadContext _instance;

    // ===== 新增：关卡配置传递（新架构） =====
    
    /// <summary> 当前要加载的关卡配置（ScriptableObject，跨场景保持）。 </summary>
    private static LevelConfig _currentLevelConfig;

    /// <summary> 当前关卡 ID（用于进度标记、重试等）。 </summary>
    private static int _currentLevelId;

    /// <summary> 当前关卡场景名（兼容旧模式）。 </summary>
    private static string _currentLevelSceneName;

    /// <summary> 当前大局 ID（跨场景传递）。 </summary>
    private static int _currentActId;

    /// <summary>
    /// 设置即将进入的关卡配置（新架构入口）。
    /// 调用后 BattleScene 会读取此配置来构建地图和波次。
    /// </summary>
    public static void SetLevelConfig(LevelConfig config, int levelId, string sceneName = null)
    {
        _currentLevelConfig = config;
        _currentLevelId = levelId;
        _currentLevelSceneName = sceneName ?? $"level{levelId}";
        _currentActId = RogueRuntimeState.CurrentActId;
        SetFromSelection();
    }

    /// <summary> 获取当前关卡配置（不清空，由 BattleSceneBootstrap 消费后调用 ClearLevelConfig）。 </summary>
    public static LevelConfig GetCurrentLevelConfig()
    {
        return _currentLevelConfig;
    }

    /// <summary> 获取当前关卡 ID。 </summary>
    public static int GetCurrentLevelId()
    {
        return _currentLevelId > 0 ? _currentLevelId : 0;
    }

    /// <summary> 获取当前关卡场景名。 </summary>
    public static string GetCurrentLevelSceneName()
    {
        return _currentLevelSceneName;
    }

    /// <summary> 获取当前大局 ID。 </summary>
    public static int GetCurrentActId()
    {
        return _currentActId;
    }

    /// <summary> 清空关卡配置（BattleScene 初始化完成后调用）。 </summary>
    public static void ClearLevelConfig()
    {
        _currentLevelConfig = null;
        _currentLevelId = 0;
        _currentLevelSceneName = null;
        _currentActId = 0;
    }

    // ===== 原有方法（保持兼容） =====

    /// <summary> 设置为"从选关界面进入"的上下文。 </summary>
    public static void SetFromSelection()
    {
        _instance = new LevelSceneLoadContext { loadType = LevelSceneLoadType.FromSelection };
    }

    /// <summary> 设置为"重新挑战/死亡重进"的上下文。 </summary>
    public static void SetFromRetry()
    {
        _instance = new LevelSceneLoadContext { loadType = LevelSceneLoadType.FromRetry };
    }

    /// <summary> 设置为"通关后经过结算返回"的上下文。 </summary>
    public static void SetFromVictory()
    {
        _instance = new LevelSceneLoadContext { loadType = LevelSceneLoadType.FromVictory };
    }

    /// <summary> 获取当前上下文并清空。 </summary>
    public static LevelSceneLoadContext GetAndClear()
    {
        var tmp = _instance;
        _instance = null;
        return tmp;
    }

    /// <summary> 仅获取不清空（调试用）。 </summary>
    public static LevelSceneLoadContext Peek()
    {
        return _instance;
    }

    /// <summary> 清空上下文。 </summary>
    public static void Clear()
    {
        _instance = null;
    }
}
