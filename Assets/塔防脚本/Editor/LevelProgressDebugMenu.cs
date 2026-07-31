#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class LevelProgressDebugMenu
{
    [MenuItem("魔塔/关卡进度/切换测试全解锁", false, 10)]
    private static void ToggleTestUnlock()
    {
        bool enabled = LevelProgress.IsTestUnlockEnabled();
        LevelProgress.SetTestUnlockEnabled(!enabled);
        Debug.Log($"[LevelProgress] 测试全解锁已 {(enabled ? "关闭" : "开启")}");
    }

    [MenuItem("魔塔/关卡进度/切换测试全解锁", true)]
    private static bool ToggleTestUnlockValidate()
    {
        return true;
    }

    [MenuItem("魔塔/关卡进度/强制解锁全部关卡", false, 20)]
    private static void UnlockAllLevels()
    {
        LevelProgress.UnlockAllForTesting();
        Debug.Log("[LevelProgress] 已强制解锁全部关卡");
    }

    [MenuItem("魔塔/关卡进度/清空关卡进度", false, 30)]
    private static void ClearProgress()
    {
        LevelProgress.ClearAll();
        Debug.Log("[LevelProgress] 已清空关卡进度");
    }
}
#endif
