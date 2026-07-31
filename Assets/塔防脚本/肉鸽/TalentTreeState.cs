using System.Collections.Generic;
using UnityEngine;

/// ═══════════════════════════════════════════════════════════
///  天赋树持久化状态：
///  - 天赋点余额（战斗结算获得，永久）
///  - 已解锁节点列表（PlayerPrefs 持久化）
///  - 所有加成的 getter 方法（供 TalentEffectApplier / RogueRuntimeState 调用）
///
///  天赋树是局外系统，加点永久生效。
///  天赋卡是局内系统，选卡后本局有效。两套系统独立。
/// ═══════════════════════════════════════════════════════════
public static class TalentTreeState
{
    private const string KeyTalentPoints = "Rogue.TalentPoints";
    private const string KeyUnlockedNodes = "Rogue.UnlockedNodes";
    private const string KeyFirstInit = "Rogue.Tree.FirstInit.Done";
    private const char Separator = ',';

    private static bool _initialized;
    private static int _talentPoints;
    private static HashSet<string> _unlockedNodes;

    // ─────────────────────────────────────────────
    //  初始化
    // ─────────────────────────────────────────────

    public static void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        if (PlayerPrefs.GetInt(KeyFirstInit, 0) == 0)
        {
            PlayerPrefs.SetInt(KeyTalentPoints, 0);
            PlayerPrefs.SetString(KeyUnlockedNodes, "");
            PlayerPrefs.SetInt(KeyFirstInit, 1);
            PrefsSaver.Save();
        }

        _talentPoints = Mathf.Max(0, PlayerPrefs.GetInt(KeyTalentPoints, 0));
        _unlockedNodes = new HashSet<string>();

        string saved = PlayerPrefs.GetString(KeyUnlockedNodes, "");
        if (!string.IsNullOrEmpty(saved))
        {
            var parts = saved.Split(Separator, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                _unlockedNodes.Add(p.Trim());
            }
        }
    }

    // ─────────────────────────────────────────────
    //  天赋点
    // ─────────────────────────────────────────────

    public static int TalentPoints
    {
        get
        {
            InitIfNeeded();
            return _talentPoints;
        }
    }

    public static void AddTalentPoints(int amount)
    {
        InitIfNeeded();
        _talentPoints += Mathf.Max(0, amount);
        SavePersistent();
    }

    public static bool TrySpendTalentPoints(int amount)
    {
        InitIfNeeded();
        if (_talentPoints < amount) return false;
        _talentPoints -= amount;
        SavePersistent();
        return true;
    }

    // ─────────────────────────────────────────────
    //  节点解锁
    // ─────────────────────────────────────────────

    public static bool IsNodeUnlocked(string nodeId)
    {
        InitIfNeeded();
        return _unlockedNodes.Contains(nodeId);
    }

    /// <summary> 检查前置节点是否已解锁（order=0 的节点无前置）。 </summary>
    public static bool CanUnlock(string nodeId)
    {
        InitIfNeeded();
        if (_unlockedNodes.Contains(nodeId)) return false;

        var node = TalentTreeData.GetNode(nodeId);
        if (!node.HasValue) return false;

        if (node.Value.order == 0) return true;

        string prereq = TalentTreeData.GetPrerequisiteId(nodeId);
        return !string.IsNullOrEmpty(prereq) && _unlockedNodes.Contains(prereq);
    }

    /// <summary> 解锁节点（消耗天赋点）。 </summary>
    public static bool TryUnlockNode(string nodeId)
    {
        InitIfNeeded();
        if (!CanUnlock(nodeId)) return false;

        var node = TalentTreeData.GetNode(nodeId);
        if (!node.HasValue) return false;

        int cost = node.Value.Cost;
        if (_talentPoints < cost) return false;

        _talentPoints -= cost;
        _unlockedNodes.Add(nodeId);
        SavePersistent();
        return true;
    }

    public static IReadOnlyCollection<string> UnlockedNodeIds
    {
        get
        {
            InitIfNeeded();
            return _unlockedNodes;
        }
    }

    /// <summary> 获取分支已解锁的层数（0~5）。 </summary>
    public static int GetBranchDepth(TalentBranch branch)
    {
        InitIfNeeded();
        int depth = 0;
        var nodes = TalentTreeData.GetBranchNodes(branch);
        foreach (var n in nodes)
        {
            if (_unlockedNodes.Contains(n.nodeId))
                depth = Mathf.Max(depth, n.order + 1);
        }
        return depth;
    }

    // ─────────────────────────────────────────────
    //  加成 Getter（供战斗系统调用）
    // ─────────────────────────────────────────────

    private static int SumEffect(TreeEffectType type)
    {
        InitIfNeeded();
        int sum = 0;
        foreach (var nodeId in _unlockedNodes)
        {
            var node = TalentTreeData.GetNode(nodeId);
            if (!node.HasValue) continue;
            if (node.Value.effect.type == type)
                sum += node.Value.effect.value;
        }
        return sum;
    }

    // — 攻击线 —
    public static int GetAttackFlat() => SumEffect(TreeEffectType.AttackFlat);
    public static int GetAttackPercent() => SumEffect(TreeEffectType.AttackPercent);
    public static int GetAttackSpeedPercent() => SumEffect(TreeEffectType.AttackSpeed);
    public static float GetAttackRangeBonus() => SumEffect(TreeEffectType.AttackRange) * 0.5f;

    // — 防御线 —
    public static int GetDefenseFlat() => SumEffect(TreeEffectType.DefenseFlat);
    public static int GetDefensePercent() => SumEffect(TreeEffectType.DefensePercent);
    public static int GetGuardianHpBonus() => SumEffect(TreeEffectType.GuardianHp);
    public static bool HasGuardianSave() => SumEffect(TreeEffectType.GuardianSave) > 0;

    // — 经济线 —
    public static int GetInitialGoldBonus() => SumEffect(TreeEffectType.InitialGold);
    public static int GetRerollDiscount() => SumEffect(TreeEffectType.RerollDiscount);
    public static int GetGoldGainPercent() => SumEffect(TreeEffectType.GoldGainPercent);
    public static int GetExtraDraws() => SumEffect(TreeEffectType.ExtraDraw);

    // — 部署线 —
    public static int GetDpRegenBonus() => SumEffect(TreeEffectType.DpRegen);
    public static int GetInitialDpBonus() => SumEffect(TreeEffectType.InitialDp);
    public static float GetDeployRangeBonus() => SumEffect(TreeEffectType.DeployRange) * 0.5f;
    public static int GetDpCapBonus() => SumEffect(TreeEffectType.DpCap);

    // ─────────────────────────────────────────────
    //  持久化
    // ─────────────────────────────────────────────

    private static void SavePersistent()
    {
        PlayerPrefs.SetInt(KeyTalentPoints, Mathf.Max(0, _talentPoints));

        var sb = new System.Text.StringBuilder();
        bool first = true;
        foreach (var id in _unlockedNodes)
        {
            if (!first) sb.Append(Separator);
            sb.Append(id);
            first = false;
        }
        PlayerPrefs.SetString(KeyUnlockedNodes, sb.ToString());
        PrefsSaver.Save();
    }

    /// <summary> 仅测试用：重置所有天赋树进度。 </summary>
    public static void ResetForTesting()
    {
        _talentPoints = 0;
        _unlockedNodes?.Clear();
        if (_unlockedNodes == null) _unlockedNodes = new HashSet<string>();
        SavePersistent();
    }
}
