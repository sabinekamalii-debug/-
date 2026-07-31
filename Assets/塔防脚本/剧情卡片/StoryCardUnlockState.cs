using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// V2 剧情碎片解锁状态：
/// - 不再默认解锁任何碎片
/// - 根据 UnlockConditionType 条件判定解锁
/// - MarkViewed 时给予天赋点奖励
/// - 支持套系进度查询
/// - 支持 AdventureChoice flag 持久化（配合奇遇 if 线）
///
/// 用法：
///   通关时调用 StoryCardUnlockState.CheckAndUnlockByEvent(GameEvent.LevelCleared, "3")
///   奇遇选择时调用 StoryCardUnlockState.SetAdventureFlag("sympathy_demon")
///   观看完毕后自动在 StoryCardButton 中调用 MarkViewed 并发放天赋点
/// </summary>
public static class StoryCardUnlockState
{
    const string UnlockPrefsKey = "StoryCard_UnlockedIds";
    const string ViewedPrefsKey = "StoryCard_ViewedIds";
    const string FlagPrefsKey = "StoryCard_AdventureFlags";
    const string RunCountKey = "StoryCard_TotalRuns";

    // ── 事件类型（用于 CheckAndUnlockByEvent） ──
    public enum GameEvent
    {
        LevelCleared,       // 通关关卡，param=关卡号
        EliteDefeated,      // 击败精英，param=关卡号或空
        BossDefeated,       // 击败Boss，param=关卡号或空
        OperatorRecruited,  // 招募干员，param=operatorId
        GoldReached,        // 本局金币达标，param=金币数
        NoHitCleared,       // 无伤通关，param=关卡号或空
        RunStarted,         // 新一局开始（累计局数+1）
    }

    // ── 缓存 ──
    static HashSet<string> _unlockedCache;
    static HashSet<string> _viewedCache;
    static HashSet<string> _flagsCache;
    static int _runCountCache = -1;
    static Dictionary<string, StoryCardData> _cardDataCache;

    // Domain Reload 禁用时，每次 Enter Play Mode 强制重建缓存（否则保留的是
    // 上一局的旧 Set 引用，会与新局 PlayerPrefs 实际内容不同步，导致"碎片消失"）。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlaymodeEnter()
    {
        _unlockedCache = null;
        _viewedCache = null;
        _flagsCache = null;
        _runCountCache = -1;
        _cardDataCache = null;
    }

    // ═══════════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════════

    static void EnsureCache()
    {
        if (_unlockedCache == null)
        {
            _unlockedCache = new HashSet<string>(LoadIds(UnlockPrefsKey));
        }
        if (_viewedCache == null)
        {
            _viewedCache = new HashSet<string>(LoadIds(ViewedPrefsKey));
        }
        if (_flagsCache == null)
        {
            _flagsCache = new HashSet<string>(LoadIds(FlagPrefsKey));
        }
        if (_runCountCache < 0)
        {
            _runCountCache = PlayerPrefs.GetInt(RunCountKey, 0);
        }
        if (_cardDataCache == null)
        {
            _cardDataCache = new Dictionary<string, StoryCardData>();
            var all = Resources.LoadAll<StoryCardData>("");
            foreach (var c in all)
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

    /// <summary> 获取已加载的 StoryCardData（用于条件判定时读取 cardId 外的字段）。 </summary>
    static StoryCardData GetCardData(string cardId)
    {
        EnsureCache();
        _cardDataCache.TryGetValue(cardId, out var data);
        return data;
    }

    // ═══════════════════════════════════════════════
    //  解锁 - 基础
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
    //  解锁 - 条件判定（V2 核心）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 根据游戏事件，遍历所有 StoryCardData，检查是否有新碎片满足条件。
    /// 应在完成战斗/招募/通关等操作后调用。
    /// 返回本次新解锁的 cardId 列表（供 UI 弹出提示用）。
    /// </summary>
    public static List<string> CheckAndUnlockByEvent(GameEvent gameEvent, string param = "")
    {
        EnsureCache();
        var newlyUnlocked = new List<string>();

        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null) continue;
            if (_unlockedCache.Contains(data.cardId)) continue; // 已解锁，跳过

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
        // 如果条件类型是 SetComplete，走专门的套系判定
        if (data.unlockConditionType == UnlockConditionType.SetComplete)
            return IsSetReadyForKeyFragment(data);

        // 事件类型必须匹配
        switch (data.unlockConditionType)
        {
            case UnlockConditionType.Manual:
                return false;

            case UnlockConditionType.LevelClear:
                if (gameEvent != GameEvent.LevelCleared) return false;
                return MatchParam(data.unlockParam, param);

            case UnlockConditionType.EliteDefeated:
                if (gameEvent != GameEvent.EliteDefeated) return false;
                return MatchParamOrEmpty(data.unlockParam, param);

            case UnlockConditionType.BossDefeated:
                if (gameEvent != GameEvent.BossDefeated) return false;
                return MatchParamOrEmpty(data.unlockParam, param);

            case UnlockConditionType.OperatorRecruit:
                if (gameEvent != GameEvent.OperatorRecruited) return false;
                return MatchParam(data.unlockParam, param);

            case UnlockConditionType.GoldReached:
                if (gameEvent != GameEvent.GoldReached) return false;
                if (!int.TryParse(param, out int currentGold)) return false;
                if (!int.TryParse(data.unlockParam, out int requiredGold)) return false;
                return currentGold >= requiredGold;

            case UnlockConditionType.NoHitCleared:
                if (gameEvent != GameEvent.NoHitCleared) return false;
                return MatchParamOrEmpty(data.unlockParam, param);

            case UnlockConditionType.AdventureChoice:
                // AdventureChoice 不是被动事件触发，而是 flag 设置时主动判定
                if (gameEvent == GameEvent.LevelCleared || gameEvent == GameEvent.RunStarted)
                    return CheckAdventureFlag(data.unlockParam);
                return false;

            case UnlockConditionType.FragmentChain:
                if (gameEvent == GameEvent.LevelCleared || gameEvent == GameEvent.RunStarted)
                    return CheckFragmentChain(data.unlockParam);
                return false;

            case UnlockConditionType.FragmentViewed:
                if (gameEvent == GameEvent.LevelCleared || gameEvent == GameEvent.RunStarted)
                    return IsViewed(data.unlockParam);
                return false;

            case UnlockConditionType.TotalRuns:
                if (gameEvent != GameEvent.RunStarted) return false;
                if (!int.TryParse(data.unlockParam, out int requiredRuns)) return false;
                return _runCountCache >= requiredRuns;

            case UnlockConditionType.SetComplete:
                if (gameEvent == GameEvent.LevelCleared || gameEvent == GameEvent.RunStarted)
                    return IsSetReadyForKeyFragment(data, false);
                return false;

            default:
                return false;
        }
    }

    /// <summary> 主动检查所有碎片（在 flag 变更或新局开始时调用）。 </summary>
    public static List<string> CheckAllPending()
    {
        EnsureCache();
        var newlyUnlocked = new List<string>();

        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null) continue;
            if (_unlockedCache.Contains(data.cardId)) continue;

            if (data.unlockConditionType == UnlockConditionType.AdventureChoice)
            {
                if (CheckAdventureFlag(data.unlockParam))
                {
                    Unlock(data.cardId);
                    newlyUnlocked.Add(data.cardId);
                }
            }
            else if (data.unlockConditionType == UnlockConditionType.FragmentChain)
            {
                if (CheckFragmentChain(data.unlockParam))
                {
                    Unlock(data.cardId);
                    newlyUnlocked.Add(data.cardId);
                }
            }
            else if (data.unlockConditionType == UnlockConditionType.FragmentViewed)
            {
                if (IsViewed(data.unlockParam))
                {
                    Unlock(data.cardId);
                    newlyUnlocked.Add(data.cardId);
                }
            }
            else if (data.unlockConditionType == UnlockConditionType.SetComplete)
            {
                if (IsSetReadyForKeyFragment(data, false))
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

    // ── 条件子判定 ──

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
    //  AdventureChoice Flag（奇遇 if 线配合）
    // ═══════════════════════════════════════════════

    public static void SetAdventureFlag(string flagName)
    {
        if (string.IsNullOrEmpty(flagName)) return;
        EnsureCache();
        if (_flagsCache.Contains(flagName)) return;
        _flagsCache.Add(flagName);
        SaveSet(_flagsCache, FlagPrefsKey);

        // flag 变更后立刻检查依赖该 flag 的碎片
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
    //  FragmentChain 判定
    // ═══════════════════════════════════════════════

    static bool CheckFragmentChain(string prerequisiteCardId)
    {
        if (string.IsNullOrEmpty(prerequisiteCardId)) return false;
        return IsUnlocked(prerequisiteCardId) && IsViewed(prerequisiteCardId);
    }

    // ═══════════════════════════════════════════════
    //  SetComplete 判定（关键碎片D）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 检查指定碎片对应的套系是否已全部集齐（除关键碎片自身外）。
    /// recheckAfterView=true 表示在刚观看了一个碎片后重检（用于触发关键碎片）。
    /// </summary>
    public static bool IsSetReadyForKeyFragment(StoryCardData keyFrag, bool checkViewed = false)
    {
        if (keyFrag == null || !keyFrag.isKeyFragment) return false;
        if (string.IsNullOrEmpty(keyFrag.fragmentSetId)) return false;

        // 找到同套系所有非关键碎片
        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null) continue;
            if (data.fragmentSetId != keyFrag.fragmentSetId) continue;
            if (data.isKeyFragment) continue;

            if (!IsUnlocked(data.cardId)) return false;
            if (checkViewed && !IsViewed(data.cardId)) return false;
        }

        return true;
    }

    // ═══════════════════════════════════════════════
    //  已观看 + 天赋点奖励（V2 核心）
    // ═══════════════════════════════════════════════

    public static bool IsViewed(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;
        EnsureCache();
        return _viewedCache.Contains(cardId);
    }

    /// <summary>
    /// 标记卡片为已观看，并发放天赋点奖励（仅首次观看有效）。
    /// 返回实际发放的天赋点数量（0=已看过，不发）。
    /// 可在 StoryCardButton.OnClick 中调用。
    /// </summary>
    public static int MarkViewed(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return 0;
        EnsureCache();

        if (_viewedCache.Contains(cardId))
            return 0; // 已看过，不重复给奖励

        _viewedCache.Add(cardId);
        SaveSet(_viewedCache, ViewedPrefsKey);

        // 发放天赋点奖励
        int reward = 0;
        var data = GetCardData(cardId);
        if (data != null && data.rewardTalentPoint > 0)
        {
            reward = data.rewardTalentPoint;
            TalentTreeState.AddTalentPoints(reward);
        }

        // 标记后立刻检查：是否有套系因此集齐、或有依赖本片的 FragmentChain/FragmentViewed 碎片
        CheckAllPending();

        return reward;
    }

    public static List<string> GetViewedCardIds()
    {
        EnsureCache();
        return new List<string>(_viewedCache);
    }

    // ═══════════════════════════════════════════════
    //  套系进度（供碎裂之镜 UI 用）
    // ═══════════════════════════════════════════════

    /// <summary> 获取指定套系的进度：(已解锁数, 非关键碎片总数, 关键碎片是否已解锁) </summary>
    public static (int unlocked, int total, bool keyUnlocked) GetSetProgress(string setId)
    {
        EnsureCache();
        int unlocked = 0;
        int total = 0;
        bool keyUnlocked = false;

        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null) continue;
            if (data.fragmentSetId != setId) continue;

            if (data.isKeyFragment)
            {
                keyUnlocked = IsUnlocked(data.cardId);
            }
            else
            {
                total++;
                if (IsUnlocked(data.cardId))
                    unlocked++;
            }
        }

        return (unlocked, Mathf.Max(1, total), keyUnlocked);
    }

    /// <summary>
    /// 获取指定套系的关键碎片 cardId（用于 UI 定位）
    /// </summary>
    public static string GetKeyFragmentCardId(string setId)
    {
        EnsureCache();
        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null) continue;
            if (data.fragmentSetId == setId && data.isKeyFragment)
                return data.cardId;
        }
        return "";
    }

    /// <summary>
    /// 获取指定套系的所有碎片 cardId（按 setIndex 排序）
    /// </summary>
    public static List<string> GetSetFragmentIds(string setId)
    {
        EnsureCache();
        var list = new List<(int index, string id)>();

        foreach (var kvp in _cardDataCache)
        {
            var data = kvp.Value;
            if (data == null) continue;
            if (data.fragmentSetId == setId)
                list.Add((data.setIndex, data.cardId));
        }

        list.Sort((a, b) => a.index.CompareTo(b.index));
        var result = new List<string>();
        foreach (var item in list)
            result.Add(item.id);
        return result;
    }

    // ═══════════════════════════════════════════════
    //  累计游戏局数（配合 TotalRuns 条件）
    // ═══════════════════════════════════════════════

    public static int TotalRuns
    {
        get
        {
            EnsureCache();
            return _runCountCache;
        }
    }

    /// <summary> 新一局开始时调用，局数+1，同时检查所有条件。 </summary>
    public static List<string> IncrementRunAndCheck()
    {
        EnsureCache();
        _runCountCache++;
        PlayerPrefs.SetInt(RunCountKey, _runCountCache);
        PrefsSaver.Save();

        return CheckAndUnlockByEvent(GameEvent.RunStarted);
    }

    // ═══════════════════════════════════════════════
    //  调试 & 重置
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

    /// <summary> 强制刷新缓存（编辑器修改.asset后调用）。 </summary>
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
