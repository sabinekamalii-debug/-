using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡进度：记录已通关关卡，用于地图上解锁下一关。
/// 支持线性解锁和自定义连线解锁。
/// </summary>
public static class LevelProgress
{
    const string PrefsKey = "LevelSelect_Completed";

    static string[] _levelOrder;
    static HashSet<string> _completed;
    static LevelConnectionConfig _connectionConfig;

    /// <summary> 由 LevelMapController 在 Awake 时设置连线配置。 </summary>
    public static void SetConnectionConfig(LevelConnectionConfig config)
    {
        _connectionConfig = config;
    }

    /// <summary> 统一关卡场景名：level 1 / Level 1 / level1 都变成 level1。 </summary>
    public static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "";
        string s = sceneName.Trim().ToLowerInvariant();
        if (s.StartsWith("level") && s.Length > 5)
        {
            string rest = s.Substring(5).Trim();
            return "level" + rest;
        }
        return s;
    }

    /// <summary> 由 LevelMapController 在 Awake 时设置关卡顺序。 </summary>
    public static void SetLevelOrder(string[] orderedSceneNames)
    {
        if (orderedSceneNames == null || orderedSceneNames.Length == 0) 
        { 
            _levelOrder = null; 
            return; 
        }
        _levelOrder = new string[orderedSceneNames.Length];
        for (int i = 0; i < orderedSceneNames.Length; i++)
            _levelOrder[i] = NormalizeSceneName(orderedSceneNames[i]);
    }

    /// <summary> 当前关卡通关时调用。 </summary>
    public static void MarkCompleted(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        string key = NormalizeSceneName(sceneName);
        if (string.IsNullOrEmpty(key)) return;
        var set = GetCompletedSet();
        set.Add(key);
        Save(set);
    }

    public static bool IsCompleted(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return GetCompletedSet().Contains(NormalizeSceneName(sceneName));
    }

    /// <summary>
    /// 该关卡是否已解锁（可点击进入）。
    /// 规则：第一关始终解锁；有连线配置时按连线解锁，否则线性解锁。
    /// </summary>
    public static bool IsUnlocked(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return true;
        string key = NormalizeSceneName(sceneName);
        if (string.IsNullOrEmpty(key)) return true;

        var completed = GetCompletedSet();

        if (_levelOrder == null || _levelOrder.Length == 0) return false;

        int levelIndex = -1;
        for (int i = 0; i < _levelOrder.Length; i++)
        {
            if (_levelOrder[i] == key) { levelIndex = i; break; }
        }
        if (levelIndex == -1) return false;

        // 第一关始终解锁
        if (levelIndex == 0) return true;

        // 有连线配置：检查是否有任何已通关的来源关卡连接到本关
        if (_connectionConfig != null)
        {
            int levelNum = levelIndex + 1;
            if (!_connectionConfig.HasAnyConnectionTo(levelNum))
            {
                return completed.Contains(_levelOrder[levelIndex - 1]);
            }

            var sources = _connectionConfig.GetConnectionsTo(levelNum);
            foreach (var src in sources)
            {
                if (src >= 1 && src <= _levelOrder.Length)
                {
                    if (completed.Contains(_levelOrder[src - 1]))
                        return true;
                }
            }
            return false;
        }

        // 无连线配置：线性解锁
        return completed.Contains(_levelOrder[levelIndex - 1]);
    }

    /// <summary> 玩家点击进入某关时调用。 </summary>
    public static void OnEnterLevel(string sceneName)
    {
    }

    static HashSet<string> GetCompletedSet()
    {
        if (_completed != null) return _completed;
        _completed = new HashSet<string>();
        string raw = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(raw)) return _completed;
        foreach (var s in raw.Split(','))
        {
            var t = NormalizeSceneName(s.Trim());
            if (!string.IsNullOrEmpty(t)) _completed.Add(t);
        }
        return _completed;
    }

    static void Save(HashSet<string> set)
    {
        _completed = set;
        PlayerPrefs.SetString(PrefsKey, string.Join(",", set));
        PlayerPrefs.Save();
    }

    /// <summary> 清空通关记录。 </summary>
    public static void ClearAll()
    {
        _completed = new HashSet<string>();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    /// <summary> 当前设定的关卡顺序。 </summary>
    public static string[] GetLevelOrder()
    {
        if (_levelOrder == null || _levelOrder.Length == 0) return null;
        return (string[])_levelOrder.Clone();
    }
}
