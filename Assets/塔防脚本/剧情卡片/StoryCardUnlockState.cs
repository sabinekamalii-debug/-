using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 剧情碎片解锁状态：PlayerPrefs 持久化，条件判定解锁。
/// </summary>
public static class StoryCardUnlockState
{
    const string UnlockPrefsKey = "StoryCard_UnlockedIds";
    const string ViewedPrefsKey = "StoryCard_ViewedIds";
    const string FlagPrefsKey = "StoryCard_AdventureFlags";
    const string RunCountKey = "StoryCard_TotalRuns";

    public enum GameEvent
    {
        LevelCleared,
        EliteDefeated,
        BossDefeated,
        OperatorRecruited,
        GoldReached,
        NoHitCleared,
        RunStarted,
    }

    static HashSet<string> _unlockedCache;
    static HashSet<string> _viewedCache;
    static HashSet<string> _flagsCache;
    static int _runCountCache = -1;
    static Dictionary<string, StoryCardData> _cardDataCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlaymodeEnter()
    {
        _unlockedCache = null;
        _viewedCache = null;
        _flagsCache = null;
        _runCountCache = -1;
        _cardDataCache = null;
    }

    static void EnsureCache()
    {
        if (_unlockedCache == null)
            _unlockedCache = new HashSet<string>(LoadIds(UnlockPrefsKey));
        if (_viewedCache == null)
            _viewedCache = new HashSet<string>(LoadIds(ViewedPrefsKey));
        if (_flagsCache == null)
            _flagsCache = new HashSet<string>(LoadIds(FlagPrefsKey));
        if (_runCountCache < 0)
            _runCountCache = PlayerPrefs.GetInt(RunCountKey, 0);
        if (_cardDataCache == null)
        {
            _cardDataCache = new Dictionary<string, StoryCardData>();
            foreach (var c in Resources.LoadAll<StoryCardData>(""))
            {
                if (c != null && !string.IsNullOrEmpty(c.cardId))
                    _cardDataCache[c.cardId] = c;
            }
        }
    }

    static List<string> LoadIds(string key)
    {
        var list = new List<string>();
        string raw = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var id in raw.Split(','))
        {
            var t = id.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(t);
        }
        return list;
    }

    static void SaveSet(HashSet<string> set, string key)
    {
        PlayerPrefs.SetString(key, string.Join(",", set));
        PrefsSaver.Save();
    }

    static StoryCardData GetCardData(string cardId)
    {
        EnsureCache();
        _cardDataCache.TryGetValue(cardId, out var data);
        return data;
    }

    // ═══════════════════════════════════════════════
    //  解锁
    // ═══════════════════════════════════════════════

    public static bool IsUnlocked(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;
        EnsureCache();
        return _unlockedCache.Contains(cardId);
    }

    public static void Unlock(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        EnsureCache();
        if (_unlockedCache.Contains(cardId)) return;
        _unlockedCache.Add(cardId);
        SaveSet(_unlockedCache, UnlockPrefsKey);
    }

    public static List<string> GetUnlockedCardIds()
    {
        EnsureCache();
        return new List<string>(_unlockedCache);
    }

    // ═══════════════════════════════════════════════
    //  条件判定
    // ═══════════════════════════════════════════════

    public static List<string> CheckAndUnlockByEvent(GameEvent gameEvent, string param = "")
    {
        EnsureCache();
        var newlyUnlocked = new List<string>();
        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null || _unlockedCache.Contains(data.cardId)) continue;
            if (CheckSingleCondition(data, gameEvent, param))
            {
                Unlock(data.cardId);
                newlyUnlocked.Add(data.cardId);
            }
        }
        return newlyUnlocked;
    }

    static bool CheckSingleCondition(StoryCardData data, GameEvent gameEvent, string param)
    {
        switch (data.unlockConditionType)
        {
            case UnlockConditionType.Manual:
                return false;
            case UnlockConditionType.LevelClear:
                return gameEvent == GameEvent.LevelCleared && MatchParam(data.unlockParam, param);
            case UnlockConditionType.EliteDefeated:
                return gameEvent == GameEvent.EliteDefeated && MatchParamOrEmpty(data.unlockParam, param);
            case UnlockConditionType.BossDefeated:
                return gameEvent == GameEvent.BossDefeated && MatchParamOrEmpty(data.unlockParam, param);
            case UnlockConditionType.OperatorRecruit:
                return gameEvent == GameEvent.OperatorRecruited && MatchParam(data.unlockParam, param);
            case UnlockConditionType.GoldReached:
                if (gameEvent != GameEvent.GoldReached) return false;
                if (!int.TryParse(param, out int currentGold)) return false;
                if (!int.TryParse(data.unlockParam, out int requiredGold)) return false;
                return currentGold >= requiredGold;
            case UnlockConditionType.NoHitCleared:
                return gameEvent == GameEvent.NoHitCleared && MatchParamOrEmpty(data.unlockParam, param);
            case UnlockConditionType.AdventureChoice:
                return (gameEvent == GameEvent.LevelCleared || gameEvent == GameEvent.RunStarted)
                    && CheckAdventureFlag(data.unlockParam);
            case UnlockConditionType.TotalRuns:
                if (gameEvent != GameEvent.RunStarted) return false;
                if (!int.TryParse(data.unlockParam, out int requiredRuns)) return false;
                return _runCountCache >= requiredRuns;
            default:
                return false;
        }
    }

    public static List<string> CheckAllPending()
    {
        EnsureCache();
        var newlyUnlocked = new List<string>();
        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null || _unlockedCache.Contains(data.cardId)) continue;

            if (data.unlockConditionType == UnlockConditionType.AdventureChoice)
            {
                if (CheckAdventureFlag(data.unlockParam))
                {
                    Unlock(data.cardId);
                    newlyUnlocked.Add(data.cardId);
                }
            }
            else if (data.unlockConditionType == UnlockConditionType.TotalRuns)
            {
                if (!int.TryParse(data.unlockParam, out int required)) continue;
                if (_runCountCache >= required)
                {
                    Unlock(data.cardId);
                    newlyUnlocked.Add(data.cardId);
                }
            }
        }
        return newlyUnlocked;
    }

    static bool MatchParam(string requiredParam, string actualParam)
    {
        if (string.IsNullOrEmpty(requiredParam)) return true;
        return string.Equals(requiredParam.Trim(), (actualParam ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    static bool MatchParamOrEmpty(string requiredParam, string actualParam)
    {
        if (string.IsNullOrEmpty(requiredParam)) return true;
        return string.Equals(requiredParam.Trim(), (actualParam ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════
    //  AdventureChoice Flag
    // ═══════════════════════════════════════════════

    public static void SetAdventureFlag(string flagName)
    {
        if (string.IsNullOrEmpty(flagName)) return;
        EnsureCache();
        if (_flagsCache.Contains(flagName)) return;
        _flagsCache.Add(flagName);
        SaveSet(_flagsCache, FlagPrefsKey);
        CheckAllPending();
    }

    public static bool HasAdventureFlag(string flagName)
    {
        if (string.IsNullOrEmpty(flagName)) return false;
        EnsureCache();
        return _flagsCache.Contains(flagName);
    }

    static bool CheckAdventureFlag(string flagName)
    {
        if (string.IsNullOrEmpty(flagName)) return false;
        EnsureCache();
        return _flagsCache.Contains(flagName);
    }

    // ═══════════════════════════════════════════════
    //  已观看 + 天赋点
    // ═══════════════════════════════════════════════

    public static bool IsViewed(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;
        EnsureCache();
        return _viewedCache.Contains(cardId);
    }

    public static int MarkViewed(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return 0;
        EnsureCache();
        if (_viewedCache.Contains(cardId)) return 0;
        _viewedCache.Add(cardId);
        SaveSet(_viewedCache, ViewedPrefsKey);

        int reward = 0;
        var data = GetCardData(cardId);
        if (data != null && data.rewardTalentPoint > 0)
        {
            reward = data.rewardTalentPoint;
            TalentTreeState.AddTalentPoints(reward);
        }
        CheckAllPending();
        return reward;
    }

    // ═══════════════════════════════════════════════
    //  局数
    // ═══════════════════════════════════════════════

    public static int TotalRuns
    {
        get { EnsureCache(); return _runCountCache; }
    }

    public static List<string> IncrementRunAndCheck()
    {
        EnsureCache();
        _runCountCache++;
        PlayerPrefs.SetInt(RunCountKey, _runCountCache);
        PrefsSaver.Save();
        return CheckAndUnlockByEvent(GameEvent.RunStarted);
    }

    // ═══════════════════════════════════════════════
    //  调试
    // ═══════════════════════════════════════════════

    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(UnlockPrefsKey);
        PlayerPrefs.DeleteKey(ViewedPrefsKey);
        PlayerPrefs.DeleteKey(FlagPrefsKey);
        PlayerPrefs.DeleteKey(RunCountKey);
        PrefsSaver.Save();
        _unlockedCache = null;
        _viewedCache = null;
        _flagsCache = null;
        _runCountCache = -1;
        _cardDataCache = null;
    }

    public static void RefreshCache()
    {
        _cardDataCache = null;
        _unlockedCache = null;
        _viewedCache = null;
        _flagsCache = null;
        _runCountCache = -1;
        EnsureCache();
    }
}
