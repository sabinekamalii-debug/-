using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 干员升星养成状态（局内）。
/// 一局之内：开局所有已选干员为 ★1，玩家用局内 RunGold 把阵容里干员养到 ★maxStar。
/// 养成以 operatorName 为 key，跨同一干员的多个部署实例共享。
/// 死亡/通关时本局状态随 RogueRuntimeState 一起清零。
///
/// 【持久化】星级必须跨场景保留：肉鸽流程里每场战斗、商店、选卡都是独立场景，
/// 静态字段会随域重载/场景切换丢失，因此每次变更都写入 PlayerPrefs，
/// 并在读取前 LoadIfNeeded()，保证「上一场升到 ★3，下一场进场仍是 ★3」。
/// </summary>
public static class OperatorStarRegistry
{
    /// <summary> 本局选中阵容的干员 key → 当前星级。 </summary>
    private static readonly Dictionary<string, int> _starByKey = new Dictionary<string, int>();

    /// <summary> 干员 key → 本局记录的满星上限（用于脱离 allData 也能判断满星）。 </summary>
    private static readonly Dictionary<string, int> _maxStarByKey = new Dictionary<string, int>();

    private static bool _loaded;

    // ── PlayerPrefs 键 ──
    private const string KeyRunActive = "Rogue.StarRegistry.Active";
    private const string KeyStarData  = "Rogue.StarRegistry.Stars";
    private const char EntrySep = '|';
    private const char FieldSep = '#';

    /// <summary> 本局是否已完成阵容登记（RogueEntry 选人后调用 BeginRun）。 </summary>
    public static bool IsRunActive
    {
        get { LoadIfNeeded(); return _isRunActive; }
        private set { _isRunActive = value; }
    }
    private static bool _isRunActive;

    /// <summary>
    /// 星级变更事件：任何升星/重置后触发，UI 与已部署干员据此刷新。
    /// 参数为发生变化的干员 key（重置类操作传 null 表示整体刷新）。
    /// </summary>
    public static event System.Action<string> OnStarChanged;

    /// <summary>
    /// 招募结果：用于战斗胜利/事件「获得干员」反馈。
    /// NewRecruit = 新干员加入阵容（★1）；
    /// Upgraded  = 已在阵容，重复获得自动升 1 星；
    /// Maxed     = 已在阵容且已满星，无法再升；
    /// Failed    = 未开战等异常。
    /// </summary>
    public enum RecruitOutcome { NewRecruit, Upgraded, Maxed, Failed }

    /// <summary>
    /// 开战前登记本局阵容。
    /// forceReset=true（选人阶段确认）时，所有选中干员强制初始化为 ★1，
    /// 不保留任何历史养成——选人阶段不允许预升星，星级只能靠局内「获得」解锁。
    /// forceReset=false（兼容旧调用）时，新加入阵容的干员补 ★1，已登记的保留星级。
    /// </summary>
    public static void BeginRun(IEnumerable<string> selectedKeys, IEnumerable<OperatorData> allData, bool forceReset = false)
    {
        LoadIfNeeded();

        // 记录满星上限（后续无需 allData 即可判断满星）
        if (allData != null)
        {
            foreach (var d in allData)
                if (d != null && !string.IsNullOrEmpty(d.RegistryKey))
                    _maxStarByKey[d.RegistryKey] = Mathf.Max(1, d.maxStarRating);
        }

        // 保留已有星级：只为新加入阵容的干员补 ★1（forceReset 时跳过此逻辑，强制全 1★）
        var keep = new Dictionary<string, int>();
        if (selectedKeys != null)
        {
            foreach (var key in selectedKeys)
            {
                if (string.IsNullOrEmpty(key) || keep.ContainsKey(key)) continue;
                int maxStar = GetMaxStarCached(key);
                int star = 1;
                if (!forceReset)
                {
                    int prev = _starByKey.TryGetValue(key, out var s) ? s : 1;
                    star = Mathf.Clamp(prev, 1, Mathf.Max(1, maxStar));
                }
                keep[key] = star;
            }
        }

        _starByKey.Clear();
        foreach (var kv in keep) _starByKey[kv.Key] = kv.Value;

        _isRunActive = true;
        Save();
        OnStarChanged?.Invoke(null);
    }

    /// <summary>
    /// 「获得干员」：集成战略式——重复获得同名干员自动升 1 星。
    /// - 已在阵容：未满星则升 1 星（Upgraded），已满星则 Maxed；
    /// - 不在阵容：作为新干员加入并设 ★1（NewRecruit）。
    /// 仅登记星级，阵容名单的增删由调用方（RogueRuntimeState）负责。
    /// </summary>
    public static RecruitOutcome Recruit(string key, IEnumerable<OperatorData> allData)
    {
        LoadIfNeeded();
        if (!_isRunActive) return RecruitOutcome.Failed;
        if (string.IsNullOrEmpty(key)) return RecruitOutcome.Failed;

        // 补登记满星上限（新招募的干员可能未在 BeginRun 覆盖到）
        if (allData != null)
        {
            foreach (var d in allData)
            {
                if (d != null && d.RegistryKey == key)
                    _maxStarByKey[key] = Mathf.Max(1, d.maxStarRating);
            }
        }

        int maxStar = GetMaxStarCached(key);
        if (IsInRoster(key))
        {
            int cur = GetStar(key);
            if (cur >= maxStar) return RecruitOutcome.Maxed;
            _starByKey[key] = cur + 1;
            Save();
            OnStarChanged?.Invoke(key);
            return RecruitOutcome.Upgraded;
        }
        else
        {
            _starByKey[key] = 1;
            Save();
            OnStarChanged?.Invoke(key);
            return RecruitOutcome.NewRecruit;
        }
    }

    /// <summary> 结束一局时清空养成状态。 </summary>
    public static void EndRun()
    {
        _loaded = true;
        _starByKey.Clear();
        _maxStarByKey.Clear();
        _isRunActive = false;
        PlayerPrefs.DeleteKey(KeyStarData);
        PlayerPrefs.SetInt(KeyRunActive, 0);
        PrefsSaver.Save();
        OnStarChanged?.Invoke(null);
    }

    /// <summary> 获取某干员当前星级（未登记返回 1）。 </summary>
    public static int GetStar(string key)
    {
        LoadIfNeeded();
        if (string.IsNullOrEmpty(key)) return 1;
        return _starByKey.TryGetValue(key, out var s) ? s : 1;
    }

    /// <summary> 本局阵容内的所有干员 key（按登记顺序）。 </summary>
    public static List<string> GetRosterKeys()
    {
        LoadIfNeeded();
        return new List<string>(_starByKey.Keys);
    }

    /// <summary> 该干员是否在本局阵容中（决定能否升星）。 </summary>
    public static bool IsInRoster(string key)
    {
        LoadIfNeeded();
        return !string.IsNullOrEmpty(key) && _starByKey.ContainsKey(key);
    }

    /// <summary> 获取某干员满星上限（默认 1）。优先用缓存，缺失时回落到 allData。 </summary>
    public static int GetMaxStar(string key, IEnumerable<OperatorData> allData)
    {
        LoadIfNeeded();
        int cached = GetMaxStarCached(key);
        if (cached > 1) return cached;

        if (allData != null)
        {
            foreach (var d in allData)
            {
                if (d != null && d.RegistryKey == key)
                {
                    int ms = Mathf.Max(1, d.maxStarRating);
                    _maxStarByKey[key] = ms;
                    return ms;
                }
            }
        }
        return cached;
    }

    /// <summary> 只读缓存里的满星上限，缺失返回 1。 </summary>
    public static int GetMaxStarCached(string key)
    {
        LoadIfNeeded();
        if (string.IsNullOrEmpty(key)) return 1;
        return _maxStarByKey.TryGetValue(key, out var ms) ? Mathf.Max(1, ms) : 1;
    }

    /// <summary> 登记某干员的满星上限（OperatorData 已知时调用，便于脱离 allData 判断）。 </summary>
    public static void RegisterMaxStar(string key, int maxStar)
    {
        LoadIfNeeded();
        if (string.IsNullOrEmpty(key)) return;
        int ms = Mathf.Max(1, maxStar);
        if (_maxStarByKey.TryGetValue(key, out var old) && old == ms) return;
        _maxStarByKey[key] = ms;
        Save();
    }

    /// <summary> 是否已到满星。 </summary>
    public static bool IsMaxStar(string key, IEnumerable<OperatorData> allData)
    {
        return GetStar(key) >= GetMaxStar(key, allData);
    }

    /// <summary> 直接设置某干员星级（用于选人阶段预升星后的确认写入）。 </summary>
    public static void SetStar(string key, int star)
    {
        LoadIfNeeded();
        if (string.IsNullOrEmpty(key) || !_starByKey.ContainsKey(key)) return;
        int maxStar = GetMaxStarCached(key);
        _starByKey[key] = Mathf.Clamp(star, 1, Mathf.Max(1, maxStar));
        Save();
        OnStarChanged?.Invoke(key);
    }

    // ─────────────────────────────────────────────
    //  持久化
    // ─────────────────────────────────────────────

    /// <summary> 首次访问时从 PlayerPrefs 恢复本局养成状态。 </summary>
    private static void LoadIfNeeded()
    {
        if (_loaded) return;
        _loaded = true;

        _isRunActive = PlayerPrefs.GetInt(KeyRunActive, 0) != 0;
        _starByKey.Clear();
        _maxStarByKey.Clear();

        string raw = PlayerPrefs.GetString(KeyStarData, "");
        if (string.IsNullOrEmpty(raw)) return;

        // 格式：key#star#maxStar|key#star#maxStar|...
        var entries = raw.Split(EntrySep);
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            var parts = entry.Split(FieldSep);
            if (parts.Length < 2) continue;
            string key = parts[0];
            if (string.IsNullOrEmpty(key)) continue;
            if (!int.TryParse(parts[1], out int star)) continue;
            int maxStar = 1;
            if (parts.Length >= 3) int.TryParse(parts[2], out maxStar);

            _maxStarByKey[key] = Mathf.Max(1, maxStar);
            _starByKey[key] = Mathf.Clamp(star, 1, Mathf.Max(1, maxStar));
        }
    }

    /// <summary> 把当前养成状态写入 PlayerPrefs。 </summary>
    private static void Save()
    {
        var sb = new StringBuilder();
        foreach (var kv in _starByKey)
        {
            if (sb.Length > 0) sb.Append(EntrySep);
            sb.Append(kv.Key).Append(FieldSep)
              .Append(kv.Value).Append(FieldSep)
              .Append(GetMaxStarCached(kv.Key));
        }
        PlayerPrefs.SetString(KeyStarData, sb.ToString());
        PlayerPrefs.SetInt(KeyRunActive, _isRunActive ? 1 : 0);
        PrefsSaver.Save();
    }
}
