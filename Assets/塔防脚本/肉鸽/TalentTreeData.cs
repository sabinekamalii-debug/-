/// ═══════════════════════════════════════════════════════════
///  魔王肉鸽 — 天赋树数据定义（代码即配置）
///
///  五向放射结构，从中心起点向外，5 条分支每条 4 个节点。
///  每条分支必须按顺序解锁（从中心往外），末端为大天赋。
///
///  天赋树 = 永久被动加成（局外加点，永久生效）
///  天赋卡 = 局内临时效果（战斗中选卡获得，本局有效）
///  两套系统完全独立。
/// ═══════════════════════════════════════════════════════════
public static class TalentTreeData
{
    // ─────────────────────────────────────────────
    //  天赋分支
    // ─────────────────────────────────────────────
    //  Attack  = 攻击线（干员攻击力/攻速）
    //  Defense = 防御线（干员防御/守护HP/免死）
    //  Economy = 经济线（初始金币/重抽折扣/通关金币/抽卡）
    //  Deploy  = 部署线（DP回复/初始DP/部署范围/DP上限）
    //  Tactics = 战术线（攻击范围/减伤/金币II/DP回复II）

    public const int NodesPerBranch = 4;
    public const int BranchCount = 5;

    /// <summary> 每个位置的天赋点消耗（0=最近中心，3=末端大天赋）。已按需求上调 4 倍。 </summary>
    public static readonly int[] CostByOrder = { 4, 8, 12, 20 };

    // ─────────────────────────────────────────────
    //  节点定义
    // ─────────────────────────────────────────────

    public readonly struct NodeDef
    {
        public readonly string nodeId;
        public readonly string displayName;
        public readonly string description;
        public readonly TalentBranch branch;
        public readonly int order;
        public readonly TalentTreeEffect effect;

        public NodeDef(string nodeId, string displayName, string description,
                       TalentBranch branch, int order, TalentTreeEffect effect)
        {
            this.nodeId = nodeId;
            this.displayName = displayName;
            this.description = description;
            this.branch = branch;
            this.order = order;
            this.effect = effect;
        }

        public int Cost => CostByOrder[order];
        public bool IsBig => order == NodesPerBranch - 1;
    }

    /// <summary> 全部 20 个节点（5 分支 × 4 节点）。 </summary>
    public static readonly NodeDef[] Nodes =
    {
        // ── 攻击线 ──
        new NodeDef("atk_0", "攻击强化 I",  "全干员攻击力 +10",
            TalentBranch.Attack, 0, new TalentTreeEffect(TreeEffectType.AttackFlat, 10)),
        new NodeDef("atk_1", "攻速强化",    "全干员攻速 +8%",
            TalentBranch.Attack, 1, new TalentTreeEffect(TreeEffectType.AttackSpeed, 8)),
        new NodeDef("atk_2", "攻击强化 II", "全干员攻击力 +15",
            TalentBranch.Attack, 2, new TalentTreeEffect(TreeEffectType.AttackFlat, 15)),
        new NodeDef("atk_3", "战争领主",    "全干员攻击力 +25%",
            TalentBranch.Attack, 3, new TalentTreeEffect(TreeEffectType.AttackPercent, 25)),

        // ── 防御线 ──
        new NodeDef("def_0", "防御强化 I",  "全干员防御力 +10",
            TalentBranch.Defense, 0, new TalentTreeEffect(TreeEffectType.DefenseFlat, 10)),
        new NodeDef("def_1", "守护壁垒",    "守护点最大生命 +1",
            TalentBranch.Defense, 1, new TalentTreeEffect(TreeEffectType.GuardianHp, 1)),
        new NodeDef("def_2", "防御强化 II", "全干员防御力 +15",
            TalentBranch.Defense, 2, new TalentTreeEffect(TreeEffectType.DefenseFlat, 15)),
        new NodeDef("def_3", "不灭之盾",    "每局免死 1 次：致命伤害时伤害归零",
            TalentBranch.Defense, 3, new TalentTreeEffect(TreeEffectType.GuardianSave, 1)),

        // ── 经济线 ──
        new NodeDef("eco_0", "初始金币 I",  "每局初始金币 +50",
            TalentBranch.Economy, 0, new TalentTreeEffect(TreeEffectType.InitialGold, 50)),
        new NodeDef("eco_1", "重抽折扣",    "选卡重抽费用 -5",
            TalentBranch.Economy, 1, new TalentTreeEffect(TreeEffectType.RerollDiscount, 5)),
        new NodeDef("eco_2", "通关金币",    "通关结算金币 +20%",
            TalentBranch.Economy, 2, new TalentTreeEffect(TreeEffectType.GoldGainPercent, 20)),
        new NodeDef("eco_3", "幸运之手",    "每局开局多 1 次抽卡",
            TalentBranch.Economy, 3, new TalentTreeEffect(TreeEffectType.ExtraDraw, 1)),

        // ── 部署线 ──
        new NodeDef("dep_0", "DP回复 I",   "DP 回复速度 +1/秒",
            TalentBranch.Deploy, 0, new TalentTreeEffect(TreeEffectType.DpRegen, 1)),
        new NodeDef("dep_1", "初始DP",      "每局初始 DP +10",
            TalentBranch.Deploy, 1, new TalentTreeEffect(TreeEffectType.InitialDp, 10)),
        new NodeDef("dep_2", "部署范围",    "干员部署半径 +0.5 格",
            TalentBranch.Deploy, 2, new TalentTreeEffect(TreeEffectType.DeployRange, 1)),
        new NodeDef("dep_3", "调度大师",    "DP 上限 +200",
            TalentBranch.Deploy, 3, new TalentTreeEffect(TreeEffectType.DpCap, 200)),

        // ── 战术线 ──
        new NodeDef("tac_0", "范围扩展",    "全干员攻击范围 +0.5",
            TalentBranch.Tactics, 0, new TalentTreeEffect(TreeEffectType.AttackRange, 1)),
        new NodeDef("tac_1", "铁壁",        "全干员减伤 +5%",
            TalentBranch.Tactics, 1, new TalentTreeEffect(TreeEffectType.DefensePercent, 5)),
        new NodeDef("tac_2", "初始金币 II", "每局初始金币 +100",
            TalentBranch.Tactics, 2, new TalentTreeEffect(TreeEffectType.InitialGold, 100)),
        new NodeDef("tac_3", "DP回复 II",  "DP 回复速度 +1/秒",
            TalentBranch.Tactics, 3, new TalentTreeEffect(TreeEffectType.DpRegen, 1)),
    };

    // ─────────────────────────────────────────────
    //  查询方法
    // ─────────────────────────────────────────────

    public static NodeDef? GetNode(string nodeId)
    {
        for (int i = 0; i < Nodes.Length; i++)
        {
            if (Nodes[i].nodeId == nodeId) return Nodes[i];
        }
        return null;
    }

    public static NodeDef[] GetBranchNodes(TalentBranch branch)
    {
        var list = new System.Collections.Generic.List<NodeDef>();
        foreach (var n in Nodes)
        {
            if (n.branch == branch) list.Add(n);
        }
        list.Sort((a, b) => a.order.CompareTo(b.order));
        return list.ToArray();
    }

    public static string GetPrerequisiteId(string nodeId)
    {
        var node = GetNode(nodeId);
        if (!node.HasValue || node.Value.order == 0) return null;
        return $"{BranchPrefix(node.Value.branch)}_{node.Value.order - 1}";
    }

    public static string BranchPrefix(TalentBranch branch)
    {
        return branch switch
        {
            TalentBranch.Attack  => "atk",
            TalentBranch.Defense  => "def",
            TalentBranch.Economy => "eco",
            TalentBranch.Deploy   => "dep",
            TalentBranch.Tactics  => "tac",
            _ => "atk",
        };
    }

    public static string BranchDisplayName(TalentBranch branch)
    {
        return branch switch
        {
            TalentBranch.Attack  => "攻击",
            TalentBranch.Defense  => "防御",
            TalentBranch.Economy => "经济",
            TalentBranch.Deploy   => "部署",
            TalentBranch.Tactics  => "战术",
            _ => "?",
        };
    }
}

public enum TalentBranch
{
    Attack = 0,
    Defense,
    Economy,
    Deploy,
    Tactics,
}

public enum TreeEffectType
{
    AttackFlat,
    AttackPercent,
    AttackSpeed,
    AttackRange,
    DefenseFlat,
    DefensePercent,
    GuardianHp,
    GuardianSave,
    InitialGold,
    RerollDiscount,
    GoldGainPercent,
    ExtraDraw,
    DpRegen,
    InitialDp,
    DeployRange,
    DpCap,
}

public readonly struct TalentTreeEffect
{
    public readonly TreeEffectType type;
    public readonly int value;
    public TalentTreeEffect(TreeEffectType type, int value) { this.type = type; this.value = value; }
}
