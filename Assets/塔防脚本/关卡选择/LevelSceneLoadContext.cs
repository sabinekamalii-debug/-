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
