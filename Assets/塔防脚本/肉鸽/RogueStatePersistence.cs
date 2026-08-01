using UnityEngine;

/// <summary>
/// 退出游戏时保存爬塔中途存档，确保下次可「继续」。
/// 自动创建 DontDestroyOnLoad 实例，无需手动挂载。
/// </summary>
public class RogueStatePersistence : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        // 延迟一帧创建，避免与其他 RuntimeInitializeOnLoad 脚本冲突
        var go = new GameObject("RogueStatePersistence");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<RogueStatePersistence>();
    }

    void Awake()
    {
        // 确保单例模式，避免重复创建
        if (FindObjectsByType<RogueStatePersistence>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationQuit()
    {
        Save();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            Save(); // 切到后台 → 存档（防杀进程丢进度）
        else
            RogueRuntimeState.ClearPersistedRunFlag(); // 回来了 → 删除这个保护存档
    }

    private static void Save()
    {
        try
        {
            RogueRuntimeState.SaveRunStateIfNeeded();
        }
        catch (System.Exception)
        {
        }
    }
}
