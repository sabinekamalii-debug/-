using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 干员升星养成状态（局内）。
/// 一局之内：开局所有已选干员为 ★1，玩家用局内 RunGold 把阵容里干员养到 ★maxStar。
/// 养成以 operatorName 为 key，跨同一干员的多个部署实例共享。
/// 死亡/通关时本局状态随 RogueRuntimeState 一起清零。
/// </summary>
public static class OperatorStarRegistry
{
    /// <summary> 本局选中阵容的干员 key → 当前星级。 </summary>
    private static readonly Dictionary<string, int> _starByKey = new Dictionary<string, int>();

    /// <summary> 本局是否已完成阵容登记（RogueEntry 选人后调用 BeginRun）。 </summary>
    public static bool IsRunActive { get; private set; }

    /// <summary>
    /// 开战前登记本局阵容：将所有选中干员的星级初始化为 1（或数据允许的下限）。
    /// </summary>
    public static void BeginRun(IEnumerable<string> selectedKeys, IEnumerable<OperatorData> allData)
    {
        _starByKey.Clear();
        IsRunActive = true;
        var maxStarByKey = new Dictionary<string, int>();
        foreach (var d in allData)
            if (!string.IsNullOrEmpty(d.RegistryKey))
                maxStarByKey[d.RegistryKey] = d.maxStarRating;

        foreach (var key in selectedKeys)
        {
            if (_starByKey.ContainsKey(key)) continue;
            int maxStar = maxStarByKey.TryGetValue(key, out var ms) ? ms : 1;
            _starByKey[key] = Mathf.Clamp(1, 1, Mathf.Max(1, maxStar));
        }
    }

    /// <summary> 结束一局时清空养成状态。 </summary>
    public static void EndRun()
    {
        _starByKey.Clear();
        IsRunActive = false;
    }

    /// <summary> 获取某干员当前星级（未登记返回 1）。 </summary>
    public static int GetStar(string key)
    {
        return _starByKey.TryGetValue(key, out var s) ? s : 1;
    }

    /// <summary> 获取某干员满星上限（默认 1）。 </summary>
    public static int GetMaxStar(string key, IEnumerable<OperatorData> allData)
    {
        foreach (var d in allData)
            if (d.RegistryKey == key)
                return d.maxStarRating;
        return 1;
    }

    /// <summary> 是否已到满星。 </summary>
    public static bool IsMaxStar(string key, IEnumerable<OperatorData> allData)
    {
        return GetStar(key) >= GetMaxStar(key, allData);
    }

    /// <summary> 直接设置某干员星级（用于选人阶段预升星后的确认写入）。 </summary>
    public static void SetStar(string key, int star)
    {
        if (!_starByKey.ContainsKey(key)) return;
        _starByKey[key] = Mathf.Max(1, star);
    }

    /// <summary>
    /// 尝试为某干员升 1 星，消耗局内 RunGold（通过 RogueRuntimeState）。
    /// 成功返回 true；金币不足 / 已满星 / 未开战 则返回 false。
    /// </summary>
    public static bool TryUpgradeStar(string key, IEnumerable<OperatorData> allData, out int cost)
    {
        cost = 0;
        if (!IsRunActive) return false;
        int cur = GetStar(key);
        int maxStar = GetMaxStar(key, allData);
        if (cur >= maxStar) return false;
        int target = cur + 1;
        cost = BalanceConfig.GetStarUpgradeCost(target);
        if (!RogueRuntimeState.TryConsumeRunGold(cost)) return false;
        _starByKey[key] = target;
        return true;
    }

    /// <summary> 升星到目标星级所需金币（用于 UI 预览）。已满星/异常返回 int.MaxValue。 </summary>
    public static int PreviewUpgradeCost(string key, IEnumerable<OperatorData> allData)
    {
        if (!IsRunActive) return int.MaxValue;
        int cur = GetStar(key);
        int maxStar = GetMaxStar(key, allData);
        if (cur >= maxStar) return int.MaxValue;
        return BalanceConfig.GetStarUpgradeCost(cur + 1);
    }
}
