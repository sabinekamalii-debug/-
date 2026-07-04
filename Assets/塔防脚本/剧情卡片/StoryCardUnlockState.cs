using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 剧情卡片解锁状态：用 PlayerPrefs 持久化，关卡结束时调用 Unlock(cardId)，左侧面板只显示已解锁的卡片。
/// </summary>
public static class StoryCardUnlockState
{
    const string PrefsKey = "StoryCard_UnlockedIds";

    /// <summary> 解锁指定卡片（关卡通关后调用）。 </summary>
    public static void Unlock(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        var set = new HashSet<string>(GetUnlockedCardIds());
        set.Add(cardId);
        Save(set);
    }

    /// <summary> 是否已解锁。 </summary>
    public static bool IsUnlocked(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;
        return GetUnlockedCardIds().Contains(cardId);
    }

    /// <summary> 已解锁的 cardId 列表。 </summary>
    public static List<string> GetUnlockedCardIds()
    {
        var list = new List<string>();
        string raw = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var id in raw.Split(','))
        {
            var t = id.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(t);
        }
        return list;
    }

    static void Save(HashSet<string> ids)
    {
        PlayerPrefs.SetString(PrefsKey, string.Join(",", ids));
        PlayerPrefs.Save();
    }

    /// <summary> 仅调试用：清空所有解锁。 </summary>
    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }
}
