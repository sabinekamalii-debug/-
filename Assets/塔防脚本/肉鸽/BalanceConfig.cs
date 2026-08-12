using UnityEngine;

/// ═══════════════════════════════════════════════════════════
///  魔王肉鸽 — 数值平衡总表（代码即文档）
///  本文件是所有金币/抽卡/天赋点数值的唯一真值源。
///  改数值只改这里，全游戏自动生效。
/// ═══════════════════════════════════════════════════════════
public static class BalanceConfig
{
    // ─────────────────────────────────────────────
    //  一、货币体系
    // ─────────────────────────────────────────────
    //  货币          字段              作用域  用途
    //  天赋点        TalentPoints      永久    天赋树加点（TalentTreeState管理）
    //  金币          RunGold           局内    局内重抽消费，通关后清零
    //  抽卡次数      CardDrawCount     局内    抽取天赋卡（免费选卡，战斗奖励获得）

    /// 战场维修卡（spc_repair）每场胜利后守护点回复量。
    /// 设计：跨场血量继承是基础机制（上一场剩几血，这一场就几血开打），
    /// 战场维修提供定量恢复而非回满，保留资源管理紧张感。
    public const int GuardianRepairAmount = 3;

    // ─────────────────────────────────────────────
    //  二、死亡安慰天赋点
    // ─────────────────────────────────────────────
    //  死亡/主动结束时根据通关层数返还天赋点：Base + 通关层数 × PerStage
    public const int DeathConsolationBase = 1;
    public const int DeathConsolationPerStage = 1;

    //     ─────────────────────────────────────────────
    //  三、战斗奖励表（固定奖励）
    //  ── 每场战斗只有「通关」与「失败」两种结果，再做细分 ──
    //  精英/Boss 的奖励重点在抽卡质量（更高稀有度），而非数量。
    //
    //  战斗类型   金币  抽卡  天赋点
    //  普通       30    1    2
    //  精英       45    2    3
    //  Boss       70    3    4
    //  任意      失败   0    0    0
    //  首通加成  胜利  +20    0    0
    // ─────────────────────────────────────────────

    /// 失败时金币。
    public const int LossGoldGain = 0;
    /// 失败时抽卡次数。
    public const int LossCardDrawGain = 0;
    /// 失败时天赋点。
    public const int LossTalentPointGain = 0;

    // — 普通战斗 —
    public const int NormalWinGold = 30;
    public const int NormalWinCardDraw = 1;
    public const int NormalWinTalentPoint = 2;

    // — 精英战斗 —
    public const int EliteWinGold = 45;
    public const int EliteWinCardDraw = 2;
    public const int EliteWinTalentPoint = 3;

    // — Boss 战斗 —
    public const int BossWinGold = 70;
    public const int BossWinCardDraw = 3;
    public const int BossWinTalentPoint = 4;

    // — 首通加成 —
    public const int FirstClearBonusGold = 20;

    // ─────────────────────────────────────────────
    //  四、押注系统（阶段 5 预留）
    // ─────────────────────────────────────────────
    public const float BetNoHitReturnBonusChance = 0.50f;
    public const float BetNoHitReturnOnlyChance = 0.95f;
    public const float BetHitReturnDoubleChance = 0.40f;
    public const float BetHitReturnOnlyChance = 0.90f;

    // ─────────────────────────────────────────────
    //  五、局内消费
    // ─────────────────────────────────────────────
    /// 选卡界面重抽一次的金币消耗（天赋树可减免）。
    public const int RerollCost = 10;

    // ─────────────────────────────────────────────
    //  五·五、干员升星养成（局内金币体系）
    //  ── 干员 ★1 起步，花局内 RunGold 升到 ★5 ──
    //  ── 5 星属性 = 1 星 × 3（见 03-干员玩法设计 升星养成）──
    //  满星(=maxStarRating)解锁职业专属被动。
    // ─────────────────────────────────────────────
    /// 升星档位所需局内金币（索引=目标星级-1，即升到★2/★3/★4/★5的花费）。
    /// 文档 §6.1：★1→★2:20, ★2→★3:50, ★3→★4:100, ★4→★5:200（满星共需 370 局内金币）。
    public static readonly int[] StarUpgradeCost = { 20, 50, 100, 200 };

    // ─────────────────────────────────────────────
    //  升星属性倍率（修订版见下方 §6.2 对齐定义）
    // ─────────────────────────────────────────────
    /// 单局允许的最高星级（养成上限，也是解锁职业被动的阈值）。
    public const int MaxStarLimit = 5;
    /// 选人阵容最低人数（RogueEntry 必须至少选这么多干员才能开战）。
    /// 设为 3 以支持「1 个 ★5 核心 + 2 个 ★1 辅助」这类小阵容，配合 7 点星数预算更灵活。
    public const int RosterMinCount = 3;
    /// 选人阵容最高人数（RogueEntry 最多可选这么多干员进局）。
    public const int RosterMaxCount = 8;
    /// <summary>
    /// 开局自选干员的「星数预算」。每个进阵容的干员按其当前星级消耗预算：
    /// 带 7 个 ★1 炮灰 = 7 点，带 1 个 ★5 核心 + 2 个 ★1 辅助 = 7 点（刚好满），
    /// 带 1 个 ★5 + 3 个 ★1 = 8 点则超预算。选人阶段升星也会占用预算。
    /// </summary>
    public const int StarBudget = 7;

    /// <summary>
    /// 升到"目标星级"所需的局内金币（目标星级需 ∈ [2, maxStar]）。
    /// 返回 <see cref="StarUpgradeCost"/> 中对应档位的花费。
    /// </summary>
    public static int GetStarUpgradeCost(int targetStar)
    {
        int idx = targetStar - 2; // ★2 → 索引0
        if (idx < 0 || idx >= StarUpgradeCost.Length) return int.MaxValue;
        return StarUpgradeCost[idx];
    }

    // ─────────────────────────────────────────────
    //  升星属性倍率（与 03-干员玩法设计 §6.2 对齐）
    //  最终属性 = 原始值 × BaseStatMultiplier[maxStar] × StarGrowth[star]
    //  ── 满星(=maxStar=5)时 = 1.6 × 3.0 = 4.8 倍于"未乘base的1星"，
    //     相对自身★1(=1.6×1.0=1.6倍基准) 翻 3 倍 ──
    // ─────────────────────────────────────────────
    /// 按干员星级上限(maxStar)的基础倍率基数（索引 = maxStar，0 占位，1~5 对应 1~5 星上限）。
    public static readonly float[] BaseStatMultiplier = { 0f, 0.6f, 0.8f, 1.0f, 1.3f, 1.6f };
    /// 按当前星级(star)的成长倍率（索引 = star，0 占位，1~5 对应 ★1~★5；★5 = 3.0）。
    public static readonly float[] StarGrowth = { 0f, 1.0f, 1.3f, 1.7f, 2.2f, 3.0f };

    // ─────────────────────────────────────────────
    //  六、商店（局内金币体系）
    // ─────────────────────────────────────────────
    /// 商店各稀有度价格。
    public const int CardShopPriceCommon = 15;
    public const int CardShopPriceAdvanced = 30;
    public const int CardShopPriceRare = 60;
    public const int CardShopPriceLegendary = 120;

    // ─────────────────────────────────────────────
    //  七、经济膨胀（奖励随深度温和递增 / 商店随深度温和涨价）
    //  ── 设计意图见下方 GetRewardDepthMultiplier / GetShopPriceDepthMultiplier ──
    //  前几层系数≈1，几乎无感；越深越明显，但不会失控。
    //  奖励增速略高于价格增速，保证深层 run 仍"差一点点"而非绝望。
    // ─────────────────────────────────────────────
    /// 每层奖励金币的递增比例（相对基础值）。2.5% 意味着：1层×1.00、8层×1.175、16层×1.375。
    public const float RewardDepthGrowthPerStage = 0.025f;
    /// 每层商店价格的递增比例（相对基础价）。略低于奖励增速，维持稀缺但不绝望。
    public const float ShopPriceDepthGrowthPerStage = 0.020f;

    // — 随机货位（S1）—
    /// 商店默认货位数量。
    public const int ShopSlotCountDefault = 5;
    /// 商店货位随机下限。
    public const int ShopSlotCountMin = 4;
    /// 商店货位随机上限。
    public const int ShopSlotCountMax = 6;

    // — 货位折扣（S2）—
    /// 每个货位有折扣的概率。
    public const float ShopSlotDiscountChance = 0.3f;
    /// 折扣可选倍率（随机取一个）。
    public static readonly float[] ShopSlotDiscountValues = { 0.7f, 0.75f, 0.8f, 0.85f, 0.9f };

    // — 付费刷新（S3）—
    /// 商店刷新基础费用（第一次刷新）。
    public const int ShopRefreshBaseCost = 10;
    /// 每次刷新后费用递增量。
    public const int ShopRefreshCostIncrement = 5;

    // — 持有上限 —
    /// 每种类型天赋卡的最大持有数量。
    public const int CardTypeLimit = 3;

    // — 删卡服务（S4）—
    /// 删卡服务基础费用（第一次删卡）。
    public const int CardRemovalBaseCost = 25;
    /// 每次删卡后费用递增量。
    public const int CardRemovalCostIncrement = 15;
    /// 每次进入商店可删卡的次数上限。
    public const int CardRemovalLimitPerVisit = 1;

    // ─────────────────────────────────────────────
    //  八、混合模式（Hybrid Mode）默认参数
    //  ── 前 N 关固定，后段受控随机修饰 ──
    //  倍率区间由 03-数值配置与平衡参考 收敛。
    // ─────────────────────────────────────────────
    public const int HybridFixedCutoff = 5;
    public const float HybridEnemyHpMin = 1.0f;
    public const float HybridEnemyHpMax = 1.3f;
    public const float HybridEnemySpeedMin = 1.0f;
    public const float HybridEnemySpeedMax = 1.2f;
    public const int HybridStartDPOffsetMin = -2;
    public const int HybridStartDPOffsetMax = 3;
    public const int HybridMaxLifePointOffsetMin = -1;
    public const int HybridMaxLifePointOffsetMax = 2;
    public const float HybridEnemySwapChance = 0.3f;
    public const float HybridHpGrowthPerStage = 0.02f;
    public const float HybridSpeedGrowthPerStage = 0.01f;

    // ═══════════════════════════════════════════════════════════
    /// <summary>
    /// 根据战斗类型和胜利等级获取基础奖励 (金币, 抽卡次数, 天赋点)。
    /// </summary>
    public static (int gold, int cardDraw, int talentPoint) GetBaseReward(BattleType battleType, VictoryGrade grade)
    {
        if (grade == VictoryGrade.Loss)
            return (LossGoldGain, LossCardDrawGain, LossTalentPointGain);

        return battleType switch
        {
            BattleType.Normal => (NormalWinGold, NormalWinCardDraw, NormalWinTalentPoint),
            BattleType.Elite => (EliteWinGold, EliteWinCardDraw, EliteWinTalentPoint),
            BattleType.Boss => (BossWinGold, BossWinCardDraw, BossWinTalentPoint),
            _ => (NormalWinGold, NormalWinCardDraw, NormalWinTalentPoint),
        };
    }

    /// <summary> 获取首通加成金币。 </summary>
    public static int GetFirstClearBonus()
    {
        return FirstClearBonusGold;
    }

    // ─────────────────────────────────────────────
    //  经济膨胀系数（依赖 RogueRuntimeState.CurrentStage）
    // ─────────────────────────────────────────────

    /// <summary>
    /// 奖励金币深度系数：随当前层数温和放大。
    /// 公式 1 + (stage-1) × RewardDepthGrowthPerStage，stage≤1 时返回 1。
    /// 意图：让"水龙头"随难度曲线一起涨，避免深层奖励相对需求过小而失衡。
    /// 前几层系数≈1（几乎无感），深层层数才拉开差距。
    /// </summary>
    public static float GetRewardDepthMultiplier()
    {
        int stage = RogueRuntimeState.CurrentStage;
        return 1f + Mathf.Max(0, stage - 1) * RewardDepthGrowthPerStage;
    }

    /// <summary>
    /// 商店价格深度系数：随当前层数温和放大。
    /// 公式 1 + (stage-1) × ShopPriceDepthGrowthPerStage，stage≤1 时返回 1。
    /// 意图：让"下水道"随深度微通胀，维持金币稀缺性与取舍价值，
    /// 但增速低于奖励增速，深层 run 仍可持续变强而非绝望。
    /// </summary>
    public static float GetShopPriceDepthMultiplier()
    {
        int stage = RogueRuntimeState.CurrentStage;
        return 1f + Mathf.Max(0, stage - 1) * ShopPriceDepthGrowthPerStage;
    }
}
