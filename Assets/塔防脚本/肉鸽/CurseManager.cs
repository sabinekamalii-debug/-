using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 诅咒管理器：管理本局中玩家身上的诅咒卡。
/// 诅咒卡是负面效果的 TalentCardData（isCurse=true），
/// 不在选卡池中出现，仅通过随机事件施加。
/// </summary>
public static class CurseManager
{
    private static List<TalentCardData> _activeCurses = new List<TalentCardData>();
    private static List<TalentCardData> _curseDatabase;
    private static bool _databaseLoaded;

    public static IReadOnlyList<TalentCardData> ActiveCurses => _activeCurses;

    /// <summary> 加载诅咒卡数据库。 </summary>
    private static void EnsureDatabase()
    {
        if (_databaseLoaded) return;
        _databaseLoaded = true;
        _curseDatabase = new List<TalentCardData>();

        var allCards = Resources.LoadAll<TalentCardData>("TalentCards");
        foreach (var card in allCards)
        {
            if (card != null && card.isCurse)
                _curseDatabase.Add(card);
        }

        Debug.Log($"[CurseManager] 加载了 {_curseDatabase.Count} 张诅咒卡");
    }

    /// <summary> 随机获取一张未激活的诅咒卡。 </summary>
    public static TalentCardData GetRandomCurse()
    {
        EnsureDatabase();
        var available = new List<TalentCardData>();
        foreach (var curse in _curseDatabase)
        {
            if (!_activeCurses.Contains(curse))
                available.Add(curse);
        }
        if (available.Count == 0) return _curseDatabase.Count > 0 ? _curseDatabase[0] : null;
        return available[Random.Range(0, available.Count)];
    }

    /// <summary> 施加一张诅咒卡。 </summary>
    public static void ApplyCurse(TalentCardData curse)
    {
        if (curse == null || !curse.isCurse) return;
        if (_activeCurses.Contains(curse)) return;
        _activeCurses.Add(curse);
        Debug.Log($"[CurseManager] 施加诅咒: {curse.displayName}");
    }

    /// <summary> 移除指定的诅咒。 </summary>
    public static bool RemoveCurse(TalentCardData curse)
    {
        if (curse == null) return false;
        bool removed = _activeCurses.Remove(curse);
        if (removed)
            Debug.Log($"[CurseManager] 解除诅咒: {curse.displayName}");
        return removed;
    }

    /// <summary> 随机移除一个可移除的诅咒，返回被移除的诅咒名。 </summary>
    public static string RemoveRandomCurse()
    {
        var removable = new List<TalentCardData>();
        foreach (var c in _activeCurses)
        {
            if (c.curseRemovable)
                removable.Add(c);
        }
        if (removable.Count == 0) return null;

        int idx = Random.Range(0, removable.Count);
        string name = removable[idx].displayName;
        _activeCurses.Remove(removable[idx]);
        Debug.Log($"[CurseManager] 随机解除诅咒: {name}");
        return name;
    }

    /// <summary> 是否有活跃诅咒。 </summary>
    public static bool HasCurses => _activeCurses.Count > 0;

    /// <summary> 清空所有诅咒（用于新局开始）。 </summary>
    public static void ClearCurses()
    {
        _activeCurses.Clear();
    }

    // ─────────────────────────────────────────────
    //  诅咒效果计算（供 TalentEffectApplier 调用）
    // ─────────────────────────────────────────────

    /// <summary> 获取活跃诅咒对指定效果类型的总惩罚值（正值，由 TalentEffectApplier 转为负）。 </summary>
    public static int GetCurseEffectTotal(TalentEffectType effectType)
    {
        EnsureDatabase();
        int total = 0;
        foreach (var curse in _activeCurses)
        {
            if (curse.curseEffectType == effectType)
                total += curse.curseEffectValue;
            if (curse.curseSecondaryEffectType == effectType)
                total += curse.curseSecondaryEffectValue;
        }
        return total;
    }

    /// <summary> 获取所有活跃诅咒对某一类效果的累计值（用于特定战斗系统读取）。 </summary>
    public static int GetCursePenalty(TalentEffectType effectType)
    {
        return GetCurseEffectTotal(effectType);
    }
}
