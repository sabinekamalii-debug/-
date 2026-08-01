using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大局注册表：加载、查询、管理所有 ActConfig 资产。
/// ActConfig 资产放在 Resources/ActConfigs/ 目录下，命名如 ActConfig_1.asset。
/// </summary>
public static class ActRegistry
{
    private const string ResourcesPath = "ActConfigs";
    private const string CompletedPrefsKey = "Act.Completed";

    private static ActConfig[] _allActs;
    private static Dictionary<int, ActConfig> _actById;
    private static HashSet<int> _completedActs;
    private static bool _loaded;

    #region 加载

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        var assets = Resources.LoadAll<ActConfig>(ResourcesPath);
        _allActs = assets ?? new ActConfig[0];
        _actById = new Dictionary<int, ActConfig>();
        foreach (var act in _allActs)
        {
            if (act != null && !_actById.ContainsKey(act.actId))
                _actById[act.actId] = act;
        }

        // 加载已完成的大局
        _completedActs = new HashSet<int>();
        string completedStr = PlayerPrefs.GetString(CompletedPrefsKey, "");
        if (!string.IsNullOrEmpty(completedStr))
        {
            foreach (var s in completedStr.Split('|'))
            {
                if (int.TryParse(s, out int id))
                    _completedActs.Add(id);
            }
        }

        var names = new System.Text.StringBuilder();
        for (int i = 0; i < _allActs.Length; i++)
        {
            if (i > 0) names.Append(", ");
            names.Append($"Act{_allActs[i].actId}:{_allActs[i].actName}");
        }
    }

    #endregion

    #region 查询

    /// <summary> 获取所有大局配置（按 actId 排序）。 </summary>
    public static ActConfig[] GetAllActs()
    {
        EnsureLoaded();
        return _allActs;
    }

    /// <summary> 根据 actId 获取大局配置。 </summary>
    public static ActConfig GetActConfig(int actId)
    {
        EnsureLoaded();
        return _actById != null && _actById.TryGetValue(actId, out var config) ? config : null;
    }

    /// <summary> 获取第一个大局（actId 最小的）。 </summary>
    public static ActConfig GetFirstAct()
    {
        EnsureLoaded();
        if (_allActs == null || _allActs.Length == 0) return null;

        ActConfig first = _allActs[0];
        foreach (var act in _allActs)
        {
            if (act != null && act.actId < first.actId)
                first = act;
        }
        return first;
    }

    /// <summary> 获取所有已解锁的大局。 </summary>
    public static ActConfig[] GetUnlockedActs()
    {
        EnsureLoaded();
        var list = new List<ActConfig>();
        foreach (var act in _allActs)
        {
            if (act != null && act.IsUnlocked())
                list.Add(act);
        }
        return list.ToArray();
    }

    /// <summary> 获取所有未完成的大局（已解锁但未通关）。 </summary>
    public static ActConfig[] GetAvailableActs()
    {
        EnsureLoaded();
        var list = new List<ActConfig>();
        foreach (var act in _allActs)
        {
            if (act != null && act.IsUnlocked() && !IsActCompleted(act.actId))
                list.Add(act);
        }
        return list.ToArray();
    }

    #endregion

    #region 完成状态

    /// <summary> 指定大局是否已通关。 </summary>
    public static bool IsActCompleted(int actId)
    {
        EnsureLoaded();
        return _completedActs != null && _completedActs.Contains(actId);
    }

    /// <summary> 标记大局已通关。 </summary>
    public static void MarkActCompleted(int actId)
    {
        EnsureLoaded();
        if (_completedActs == null) _completedActs = new HashSet<int>();
        if (_completedActs.Add(actId))
        {
            PlayerPrefs.SetString(CompletedPrefsKey, string.Join("|", _completedActs));
            PrefsSaver.Save();
        }
    }

    #endregion

    #region 调试

    /// <summary> 清除所有大局完成记录（调试用）。 </summary>
    public static void ClearAllCompletion()
    {
        _completedActs?.Clear();
        PlayerPrefs.DeleteKey(CompletedPrefsKey);
        PrefsSaver.Save();
    }

    /// <summary> 强制重新加载（编辑器调试用）。 </summary>
    public static void Reload()
    {
        _loaded = false;
        _allActs = null;
        _actById = null;
        _completedActs = null;
    }

    #endregion
}


