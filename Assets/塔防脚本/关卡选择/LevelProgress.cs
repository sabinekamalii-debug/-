using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 关卡进度：记录已通关关卡，用于地图上解锁下一关。
/// 支持线性解锁和自定义连线解锁。
/// </summary>
public static class LevelProgress
{
    const string PrefsKey = "LevelSelect_Completed";
    const string PrefsKeyLastEntered = "LevelSelect_LastEntered";
#if UNITY_EDITOR
    const string EditorPrefsKeyTestUnlock = "LevelProgress.TestUnlockAll";
#endif

    static string[] _levelOrder;
    static HashSet<string> _completed;
    static LevelConnectionConfig _connectionConfig;

    // ===== StS 分叉路径图：节点完成追踪 =====
    const string NodePrefsKey = "Map.CompletedNodes";
    static HashSet<int> _completedNodes;
    static MapGraph _mapGraph;

    // Domain Reload 禁用时，每次 Enter Play Mode 清空缓存。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlaymodeEnter()
    {
        _levelOrder = null;
        _completed = null;
        _connectionConfig = null;
        _completedNodes = null;
        _mapGraph = null;
    }

    // ===== StS 分叉路径图 API =====

    public static void SetMapGraph(MapGraph graph)
    {
        _mapGraph = graph;
    }

    public static void MarkNodeCompleted(int nodeId)
    {
        var set = GetCompletedNodes();
        set.Add(nodeId);
        SaveNodes(set);
    }

    public static bool IsNodeCompleted(int nodeId)
    {
        return GetCompletedNodes().Contains(nodeId);
    }

    public static bool IsNodeUnlocked(int nodeId)
    {
        // 开发阶段：编辑器下默认全部解锁
        if (ShouldUnlockAllForTesting())
            return true;

        if (_mapGraph == null) return false;
        var node = _mapGraph.GetNode(nodeId);
        if (node == null) return false;

        // Start node always unlocked
        if (node.nodeType == LevelType.Start) return true;

        // Already completed → unlocked (for re-entry in testing)
        if (IsNodeCompleted(nodeId)) return true;

        // Unlocked if any predecessor is completed
        foreach (var predId in _mapGraph.GetPredecessors(nodeId))
        {
            if (IsNodeCompleted(predId)) return true;
        }
        return false;
    }

    public static void ClearNodeProgress()
    {
        _completedNodes = new HashSet<int>();
        PlayerPrefs.DeleteKey(NodePrefsKey);
        PrefsSaver.Save();
    }

    static HashSet<int> GetCompletedNodes()
    {
        if (_completedNodes != null) return _completedNodes;
        _completedNodes = new HashSet<int>();
        string raw = PlayerPrefs.GetString(NodePrefsKey, "");
        if (string.IsNullOrEmpty(raw)) return _completedNodes;
        foreach (var s in raw.Split(','))
        {
            if (int.TryParse(s.Trim(), out int id))
                _completedNodes.Add(id);
        }
        return _completedNodes;
    }

    static void SaveNodes(HashSet<int> set)
    {
        _completedNodes = set;
        PlayerPrefs.SetString(NodePrefsKey, string.Join(",", set));
        PrefsSaver.Save();
    }

    /// <summary> 由 LevelMapController 在 Awake 时设置连线配置。 </summary>
    public static void SetConnectionConfig(LevelConnectionConfig config)
    {
        _connectionConfig = config;
    }

    /// <summary> 统一关卡场景名：[旧版备用] level 1 / Level 1 / level1 都变成 level1。 </summary>
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
    /// 正式流程：按顺序解锁或按连线配置解锁；测试阶段可一键开放全部关卡。
    /// </summary>
    public static bool IsUnlocked(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        if (ShouldUnlockAllForTesting())
            return true;

        string key = NormalizeSceneName(sceneName);
        if (string.IsNullOrEmpty(key)) return false;

        if (IsCompleted(key)) return true;

        // 第一关默认开放。
        if (IsFirstLevel(key)) return true;

        // 先看是否存在显式连线配置；有则按前置关卡完成解锁。
        if (_connectionConfig != null)
        {
            int levelNumber = ParseLevelNumber(key);
            if (levelNumber > 0)
            {
                foreach (var conn in _connectionConfig.connections)
                {
                    if (conn.to != levelNumber) continue;
                    string prerequisiteKey = NormalizeSceneName($"level {conn.from}");
                    if (IsCompleted(prerequisiteKey)) return true;
                }
            }
        }

        // 没有连线配置时，按顺序解锁：前一关通关即可解锁当前关。
        if (_levelOrder != null && _levelOrder.Length > 0)
        {
            int index = GetLevelIndex(key);
            if (index > 0)
                return IsCompleted(_levelOrder[index - 1]);
        }

        return false;
    }

    /// <summary> 玩家点击进入某关时调用。 </summary>
    public static void OnEnterLevel(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        string key = NormalizeSceneName(sceneName);
        if (string.IsNullOrEmpty(key)) return;

        PlayerPrefs.SetString(PrefsKeyLastEntered, key);
        PrefsSaver.Save();
    }

#if UNITY_EDITOR
    public static bool IsTestUnlockEnabled()
    {
        return EditorPrefs.GetBool(EditorPrefsKeyTestUnlock, true);
    }

    public static void SetTestUnlockEnabled(bool enabled)
    {
        EditorPrefs.SetBool(EditorPrefsKeyTestUnlock, enabled);
    }
#else
    public static bool IsTestUnlockEnabled() => false;

    public static void SetTestUnlockEnabled(bool enabled) { }
#endif

    public static void UnlockAllForTesting()
    {
        _completed = new HashSet<string>();
        if (_levelOrder != null && _levelOrder.Length > 0)
        {
            foreach (var level in _levelOrder)
                if (!string.IsNullOrEmpty(level)) _completed.Add(level);
        }
        else
        {
            for (int i = 1; i <= 16; i++)
                _completed.Add(NormalizeSceneName($"level {i}"));
        }
        Save(_completed);
    }

    static bool ShouldUnlockAllForTesting()
    {
#if UNITY_EDITOR
        return EditorPrefs.GetBool(EditorPrefsKeyTestUnlock, true);
#else
        return false;
#endif
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
        PrefsSaver.Save();
    }

    /// <summary> 清空通关记录。 </summary>
    public static void ClearAll()
    {
        _completed = new HashSet<string>();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.DeleteKey(PrefsKeyLastEntered);
        PrefsSaver.Save();
    }

    /// <summary> 当前设定的关卡顺序。 </summary>
    public static string[] GetLevelOrder()
    {
        if (_levelOrder == null || _levelOrder.Length == 0) return null;
        return (string[])_levelOrder.Clone();
    }

    static bool IsFirstLevel(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (_levelOrder != null && _levelOrder.Length > 0)
            return key == _levelOrder[0];
        return key == NormalizeSceneName("[旧版备用] level 1");
    }

    static int GetLevelIndex(string key)
    {
        if (_levelOrder == null || _levelOrder.Length == 0) return -1;
        for (int i = 0; i < _levelOrder.Length; i++)
        {
            if (_levelOrder[i] == key) return i;
        }
        return -1;
    }

    static int ParseLevelNumber(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return 0;
        string key = NormalizeSceneName(sceneName);
        if (string.IsNullOrEmpty(key) || !key.StartsWith("level")) return 0;

        int start = "level".Length;
        int end = start;
        while (end < key.Length && char.IsDigit(key[end])) end++;
        if (end <= start) return 0;
        return int.Parse(key.Substring(start, end - start));
    }
}
