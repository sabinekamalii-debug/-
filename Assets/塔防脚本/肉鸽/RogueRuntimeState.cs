using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 肉鸽全局运行时状态：
/// - RunGold：当前 run 金币（局内），用于商店购买、重抽等，通关后清零
/// - CardDrawCount：本局剩余抽卡次数（战斗奖励）
/// - SelectedTalentCardIds：本局已选天赋卡 ID（选卡免费 + 商店购买）
///
/// 天赋点（TalentTreeState.TalentPoints）和天赋树加点由 TalentTreeState 管理（局外永久）。
/// 天赋卡效果由 TalentEffectApplier 管理（叠加卡牌+天赋树加成）。
/// 所有数值常量定义在 BalanceConfig.cs 中。
/// </summary>
public static class RogueRuntimeState
{
    private const string KeyFirstInit = "Rogue.FirstInit.Done";
    private const string KeyHasActiveRun = "Rogue.HasActiveRun";
    private const string KeyCurrentStage = "Rogue.CurrentStage";
    private const string KeyRunGold = "Rogue.RunGold";
    private const string KeyGuardianCurrentHp = "Rogue.Guardian.CurrentHp";
    private const string KeyGuardianMaxHp = "Rogue.Guardian.MaxHp";
    private const string KeySelectedTalentIds = "Rogue.SelectedTalentIds";
    private const string KeySelectedRoster = "Rogue.SelectedRoster";
    private const string KeyGameMode = "Rogue.GameMode";
    private const string KeyRunSeed = "Rogue.RunSeed";
    private const string KeyCurrentActId = "Rogue.CurrentActId";
    private const char KeyTalentIdSep = '|';

    private static bool _initialized;

    public static int RunGold { get; private set; }
    public static int CardDrawCount { get; private set; }

    public static bool HasActiveRun { get; private set; }
    public static int CurrentStage { get; private set; } = 1;

    /// <summary> 守护点跨场当前血量（0 表示无跨场状态）。 </summary>
    public static int GuardianCurrentHp { get; private set; }
    /// <summary> 守护点跨场最大血量。 </summary>
    public static int GuardianMaxHp { get; private set; }
    public static bool AutoStartBattleOnEntry { get; set; }

    public static GameMode CurrentGameMode { get; private set; } = GameMode.Normal;
    public static int RunSeed { get; private set; }

    /// <summary> 当前大局 ID（0=未选择大局）。 </summary>
    public static int CurrentActId { get; private set; }

    /// <summary> 当前大局配置（运行时从 ActRegistry 加载，可能为 null）。 </summary>
    public static ActConfig CurrentActConfig => ActRegistry.GetActConfig(CurrentActId);
    private static RunRng _runRng;
    public static RunRng RunRng => _runRng;
    private static RunModifierConfig _modifierConfig;
    public static RunModifierConfig ModifierConfig => _modifierConfig;

    /// <summary> 当前 StS 分叉路径地图图。 </summary>
    private static MapGraph _currentMapGraph;
    public static MapGraph CurrentMapGraph => _currentMapGraph;

    /// <summary> 按关卡类型分组的打乱 LevelConfig ID 池。 </summary>
    private static Dictionary<LevelType, int[]> _typeShuffledPools;
    private const int ShufflePoolScanRange = 50;

    public static void SetGameMode(GameMode mode)
    {
        CurrentGameMode = mode;
        SavePersistent();
    }

    /// <summary>
    /// 选择大局并持久化。在玩家选择大局或自动选择第一个大局时调用。
    /// </summary>
    public static void StartAct(int actId)
    {
        CurrentActId = actId;
        SavePersistent();
    }

    /// <summary>
    /// 标记当前大局已通关。击败 Boss 后调用。
    /// </summary>
    public static void CompleteCurrentAct()
    {
        if (CurrentActId <= 0) return;
        ActRegistry.MarkActCompleted(CurrentActId);
    }

    public static void SetRunModifierConfig(RunModifierConfig config)
    {
        _modifierConfig = config;
    }

    /// <summary> 设置当前地图图（由 LevelMapController 生成后调用）。 </summary>
    public static void SetMapGraph(MapGraph graph)
    {
        _currentMapGraph = graph;
    }

    public static int GetFixedCutoff()
    {
        // 全游戏统一混乱：无前段固定，返回 0 表示没有任何关卡走按序。
        return 0;
    }

    public static bool ShouldApplyModifiers(int levelNumber)
    {
        // 流派倾向不修改任何战斗数值，只影响抽卡分布。地图也只打乱顺序。
        return false;
    }

    /// <summary>
    /// 流派倾向对「天赋卡类型」的抽取权重乘子。
    /// 普通模式所有类型乘 1（均衡）；盛怒放大攻击卡；苟道放大防御卡与守护卡。
    /// 返回的值会作为额外乘子叠加到原有稀有度权重上（见 GoldShopConfig.GetRarityWeight）。
    /// </summary>
    public static float GetCardTypeWeightMultiplier(TalentCardType type)
    {
        switch (CurrentGameMode)
        {
            case GameMode.Fury:
                // 盛怒模式：攻击卡明显更易出，其它类型略降，保证分布仍健康。
                return type switch
                {
                    TalentCardType.Attack => 2.2f,
                    TalentCardType.Defense => 0.7f,
                    TalentCardType.Guardian => 0.7f,
                    _ => 1f,
                };
            case GameMode.Turtle:
                // 苟道模式：防御卡与守护卡明显更易出，攻击卡略降。
                return type switch
                {
                    TalentCardType.Defense => 2.0f,
                    TalentCardType.Guardian => 2.0f,
                    TalentCardType.Attack => 0.7f,
                    _ => 1f,
                };
            default: // Normal
                return 1f;
        }
    }

    /// <summary>
    /// 根据当前模式返回第 stageNumber 关实际应加载的 LevelConfig ID。
    /// 全游戏统一混乱：所有战斗关卡都从池中打乱抽取（Boss 固定）。流派倾向（GameMode）只影响抽卡，
    /// 不影响关卡顺序，因此这里不再按 Fixed/Hybrid/Random 分支。
    /// 无 ActConfig 时：回退旧逻辑（stageNumber 直接作为 ID）。
    /// </summary>
    public static int GetLevelConfigIdForStage(int stageNumber, LevelType levelType = LevelType.NormalBattle)
    {
        InitIfNeeded();
        if (stageNumber <= 0) return stageNumber;

        // 非战斗类型不参与随机
        if (levelType != LevelType.NormalBattle && levelType != LevelType.Elite && levelType != LevelType.Boss)
            return stageNumber;

        var actConfig = CurrentActConfig;

        // 无 ActConfig 时回退旧逻辑
        if (actConfig == null)
        {
            return stageNumber;
        }

        // 有 ActConfig：Boss 固定
        if (levelType == LevelType.Boss)
            return actConfig.bossLevelConfigId > 0 ? actConfig.bossLevelConfigId : stageNumber;

        int[] pool = actConfig.GetLevelPool(levelType);
        if (pool == null || pool.Length == 0)
            return stageNumber;

        return GetShuffledLevelIdForType(stageNumber, levelType);
    }

    private static int GetShuffledLevelIdForType(int stageNumber, LevelType levelType)
    {
        if (_typeShuffledPools == null)
            GenerateShuffledOrder();

        // 非战斗类型（商店/休息/事件）不参与随机，直接返回原关卡号
        if (levelType != LevelType.NormalBattle && levelType != LevelType.Elite && levelType != LevelType.Boss)
            return stageNumber;

        if (_typeShuffledPools == null || !_typeShuffledPools.TryGetValue(levelType, out var pool) || pool == null || pool.Length == 0)
            return stageNumber;

        int idx = (stageNumber - 1) % pool.Length;
        return pool[idx];
    }

    private static void GenerateShuffledOrder()
    {
        _typeShuffledPools = new Dictionary<LevelType, int[]>();

        var actConfig = CurrentActConfig;

        // 有 ActConfig：直接使用其关卡池
        if (actConfig != null)
        {
            var normalArr = (int[])actConfig.normalLevelPool?.Clone() ?? new int[0];
            var eliteArr = (int[])actConfig.eliteLevelPool?.Clone() ?? new int[0];
            var bossArr = actConfig.bossLevelConfigId > 0
                ? new[] { actConfig.bossLevelConfigId }
                : new int[0];

            if (_runRng != null)
            {
                _runRng.Shuffle(normalArr);
                _runRng.Shuffle(eliteArr);
            }

            _typeShuffledPools[LevelType.NormalBattle] = normalArr;
            _typeShuffledPools[LevelType.Elite] = eliteArr;
            _typeShuffledPools[LevelType.Boss] = bossArr;

            return;
        }

        // 回退：扫描 Resources/LevelConfigs/ 目录
        var normalIds = new List<int>();
        var eliteIds = new List<int>();
        var bossIds = new List<int>();

        for (int id = 1; id <= ShufflePoolScanRange; id++)
        {
            string[] names = {
                $"Level_{id:D2}_Battle",
                $"Level_{id}_Battle",
            };
            LevelConfig config = null;
            foreach (var name in names)
            {
                config = Resources.Load<LevelConfig>($"LevelConfigs/{name}");
                if (config != null) break;
            }
            if (config == null) continue;

            switch (config.levelType)
            {
                case LevelType.Elite:
                    eliteIds.Add(id);
                    break;
                case LevelType.Boss:
                    bossIds.Add(id);
                    break;
                default:
                    normalIds.Add(id);
                    break;
            }
        }

        if (_runRng != null)
        {
            var normalArr = normalIds.ToArray();
            var eliteArr = eliteIds.ToArray();
            var bossArr = bossIds.ToArray();
            _runRng.Shuffle(normalArr);
            _runRng.Shuffle(eliteArr);
            _runRng.Shuffle(bossArr);
            _typeShuffledPools[LevelType.NormalBattle] = normalArr;
            _typeShuffledPools[LevelType.Elite] = eliteArr;
            _typeShuffledPools[LevelType.Boss] = bossArr;
        }
    }

    private static void ClearShuffledOrder()
    {
        _typeShuffledPools = null;
    }

    /// <summary>
    /// 测试模式：直接在 Unity 中以 RogueResult 场景运行（simulateBattleResultWhenDirectRun）时置 true。
    /// 开启后：结算不永久提交天赋点、本局状态不持久化到 PlayerPrefs，
    /// 便于反复进入场景调试而不累积 meta 进度（金币/卡片会在每次进入时归零）。
    /// 真实战斗流程不会触发此模式。
    /// </summary>
    public static bool TestMode { get; set; }

    public static RogueBattleResult LastBattleResult { get; private set; }
    public static bool HasPendingBattleResult { get; private set; }

    /// <summary> 本局已选天赋卡 ID 列表，用于选卡去重与后续效果。 </summary>
    private static List<string> _selectedTalentIds = new List<string>();
    public static IReadOnlyList<string> SelectedTalentCardIds => _selectedTalentIds;

    /// <summary> 清空已选天赋卡（内存 + 存档），用于开新局 / 结束本局，避免存档串台。 </summary>
    private static void ClearSelectedTalentIds()
    {
        _selectedTalentIds.Clear();
        PlayerPrefs.DeleteKey(KeySelectedTalentIds);
    }

    /// <summary> 仅本次战斗生效的卡 ID 列表，战斗结束后清空。 </summary>
    private static List<string> _battleOnlyCardIds = new List<string>();
    public static IReadOnlyList<string> BattleOnlyCardIds => _battleOnlyCardIds;

    /// <summary> 本次战斗临时攻击力百分比加成（一次性战斗卡效果）。 </summary>
    public static int BattleTempAttackPercent { get; private set; }
    /// <summary> 本次战斗临时攻速百分比加成（一次性战斗卡效果）。 </summary>
    public static int BattleTempAttackSpeedPercent { get; private set; }

    /// <summary>
    /// 仅测试用：清空本局所有已获得的卡片及其派生效果（效果倍数、spc 一次性标记、
    /// 翻牌/守护点奖励等），让测试时一键回到「未抽任何卡」的状态。
    /// 不改动金币/层数/本次战斗结算结果。
    /// </summary>
    public static void ClearAllAcquiredCardsForTesting()
    {
        ClearSelectedTalentIds();
        _cardEffectMultiplier.Clear();
        _pendingNextCardBonusPercent = 0;
        _battleRerollUsed = false;
        _skipBattleUsed = false;
        _shopSlotCardIds.Clear();
        _shopRefreshCount = 0;
        _cardRemovalCount = 0;
        _cardRemovalsThisVisit = 0;
    }

    // ─────────────────────────────────────────────
    //  守护点跨场血量
    // ─────────────────────────────────────────────

    /// <summary>
    /// 写入守护点跨场血量并持久化。战后调用，使下一场战斗承接此血量。
    /// </summary>
    public static void SetGuardianHp(int current, int max)
    {
        GuardianCurrentHp = Mathf.Clamp(current, 0, max);
        GuardianMaxHp = max;
        SavePersistent();
    }

    /// <summary>
    /// 增减守护点最大生命（随机事件调用）。同步增减当前生命，使增减立即生效。
    /// </summary>
    public static void AddGuardianMaxHp(int delta)
    {
        int newMax = Mathf.Max(0, GuardianMaxHp + delta);
        int newCurrent = Mathf.Clamp(GuardianCurrentHp + delta, 0, newMax);
        SetGuardianHp(newCurrent, newMax);
    }

    public static void ClearGuardianHp()
    {
        GuardianCurrentHp = 0;
        GuardianMaxHp = 0;
        PlayerPrefs.DeleteKey(KeyGuardianCurrentHp);
        PlayerPrefs.DeleteKey(KeyGuardianMaxHp);
    }

    /// <summary>
    /// V2 剧情碎片：通知有干员被招募（用于触发 OperatorRecruit 碎片解锁条件）。
    /// 在干员加入玩家队伍时调用，传入 operatorId。
    /// </summary>
    public static void NotifyOperatorRecruited(string operatorId)
    {
        if (string.IsNullOrEmpty(operatorId)) return;
        StoryCardUnlockState.CheckAndUnlockByEvent(
            StoryCardUnlockState.GameEvent.OperatorRecruited, operatorId);
    }

    /// <summary>
    /// V2 剧情碎片：设置奇遇选择 flag（供 Naninovel 脚本在 @choice 后调用）。
    /// flag 设置后自动检查依赖该 flag 的碎片是否满足解锁条件。
    /// 用法：RogueRuntimeState.SetStoryAdventureFlag("sympathy_demon");
    /// </summary>
    public static void SetStoryAdventureFlag(string flagName)
    {
        StoryCardUnlockState.SetAdventureFlag(flagName);
    }

    // ─────────────────────────────────────────────
    //  初始化
    // ─────────────────────────────────────────────

    public static void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        TalentTreeState.InitIfNeeded();

        if (PlayerPrefs.GetInt(KeyFirstInit, 0) == 0)
        {
            PlayerPrefs.SetInt(KeyFirstInit, 1);
            PrefsSaver.Save();
        }

        // 始终加载已保存的卡片 ID（测试阶段卡片永久保留）
        string savedIds = PlayerPrefs.GetString(KeySelectedTalentIds, "");
        if (!string.IsNullOrEmpty(savedIds))
            _selectedTalentIds = new List<string>(savedIds.Split(KeyTalentIdSep, System.StringSplitOptions.RemoveEmptyEntries));

        // 恢复本局选人阵容（升星养成与阵容部署都依赖它跨场景存在）
        string savedRoster = PlayerPrefs.GetString(KeySelectedRoster, "");
        _selectedRoster = string.IsNullOrEmpty(savedRoster)
            ? new List<string>()
            : new List<string>(savedRoster.Split(KeyTalentIdSep, System.StringSplitOptions.RemoveEmptyEntries));

        if (PlayerPrefs.GetInt(KeyHasActiveRun, 0) != 0)
        {
            HasActiveRun = true;
            CurrentStage = Mathf.Max(1, PlayerPrefs.GetInt(KeyCurrentStage, 1));
            RunGold = Mathf.Max(0, PlayerPrefs.GetInt(KeyRunGold, 0));
            GuardianCurrentHp = PlayerPrefs.GetInt(KeyGuardianCurrentHp, 0);
            GuardianMaxHp     = PlayerPrefs.GetInt(KeyGuardianMaxHp, 0);
            CurrentGameMode = (GameMode)PlayerPrefs.GetInt(KeyGameMode, (int)GameMode.Normal);
            RunSeed = PlayerPrefs.GetInt(KeyRunSeed, 0);
            CurrentActId = PlayerPrefs.GetInt(KeyCurrentActId, 0);

            // 迁移：持久化的 CurrentActId 可能残留旧测试写入的非法/未解锁值
            // （如 Act2 在未通关 Act1 前被写入）。若该大局不存在或未解锁，
            // 回退到第一个大局，否则首战会取到该大局 normalLevelPool[0]
            // （Act2 池 [0]=23），玩家点击「开始本局」会进入莫名奇妙的关卡。
            if (CurrentActId > 0)
            {
                var actCfg = ActRegistry.GetActConfig(CurrentActId);
                if (actCfg == null || !actCfg.IsUnlocked())
                {
                    var firstAct = ActRegistry.GetFirstAct();
                    if (firstAct != null)
                        CurrentActId = firstAct.actId;
                }
            }

            if (RunSeed != 0)
            {
                _runRng = new RunRng(RunSeed);
                GenerateShuffledOrder();
            }
        }
    }

    // ─────────────────────────────────────────────
    //  商店购买（局内金币买局内卡）
    // ─────────────────────────────────────────────

    /// <summary> 用 RunGold 购买天赋卡。买到的卡加入 SelectedTalentCardIds（本局生效）。 </summary>
    public static bool TryPurchaseCard(TalentCardData card)
    {
        InitIfNeeded();
        if (card == null || string.IsNullOrEmpty(card.cardId)) return false;
        if (_selectedTalentIds.Contains(card.cardId)) return false; // 本局已拥有
        if (!CanAcquireCard(card)) return false; // 该类型已达持有上限

        int price = GetCardShopPrice(card);
        if (RunGold < price) return false;

        int goldBefore = RunGold;
        RunGold -= price;
        
        _selectedTalentIds.Add(card.cardId);
        _shopSlotCardIds.Remove(card.cardId); // 从货位移除已购卡
        SavePersistent();
        return true;
    }

    /// <summary> 卡片商店价格：基于稀有度，货位折扣先乘，再乘 spc_shop 全局折扣。 </summary>
    public static int GetCardShopPrice(TalentCardData card)
    {
        if (card == null) return 999;
        int basePrice = card.rarity switch
        {
            TalentCardRarity.Common    => BalanceConfig.CardShopPriceCommon,
            TalentCardRarity.Advanced  => BalanceConfig.CardShopPriceAdvanced,
            TalentCardRarity.Rare      => BalanceConfig.CardShopPriceRare,
            TalentCardRarity.Legendary => BalanceConfig.CardShopPriceLegendary,
            _ => BalanceConfig.CardShopPriceCommon,
        };
        // S2：先取货位折扣
        float slotMult = card.slotDiscount > 0f ? card.slotDiscount : 1f;
        // 经济膨胀：商店价格随当前层数温和上涨（微通胀，维持稀缺性）
        float depthMult = BalanceConfig.GetShopPriceDepthMultiplier();
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * slotMult * ShopPriceMultiplier * depthMult));
    }

    /// <summary> 判断本局是否已拥有该卡。 </summary>
    public static bool IsCardOwned(string cardId)
    {
        InitIfNeeded();
        return _selectedTalentIds.Contains(cardId);
    }

    // ─────────────────────────────────────────────
    //  特殊卡（spc_*）运行时状态
    //  仅依据 _selectedTalentIds 是否拥有即可判定，
    //  不依赖任何具体干员（干员仍在陆续制作中）。
    // ─────────────────────────────────────────────

    /// <summary> 商店价格折扣（spc_shop）：拥有时价格 ×0.75。 </summary>
    public static float ShopPriceMultiplier => IsCardOwned("spc_shop") ? 0.75f : 1f;

    /// <summary> 稀有卡出现率提升（spc_fortune）。 </summary>
    public static bool RareRateUpActive => IsCardOwned("spc_fortune");

    /// <summary> 选卡时多 1 个选项（spc_draw）：3 → 4。 </summary>
    public static int CardPickSlotCount => IsCardOwned("spc_draw") ? 4 : 3;

    /// <summary> 战后守护点回复量（spc_repair）：每场胜利后回复固定量，不再回满。0 表示无此卡。 </summary>
    public static int RepairAfterBattleAmount => IsCardOwned("spc_repair") ? BalanceConfig.GuardianRepairAmount : 0;

    /// <summary> 可把已拥有天赋卡转化为金币（spc_convert）。 </summary>
    public static bool CanConvertCard => IsCardOwned("spc_convert");

    // 下一张卡效果 +50%（spc_double）
    private static int _pendingNextCardBonusPercent;
    private static Dictionary<string, float> _cardEffectMultiplier = new Dictionary<string, float>();

    public static int PendingNextCardBonusPercent => _pendingNextCardBonusPercent;
    public static void SetPendingNextCardBonus(int percent) { _pendingNextCardBonusPercent = percent; }
    public static void ClearPendingNextCardBonus() { _pendingNextCardBonusPercent = 0; }
    public static float GetCardMultiplier(string cardId)
        => _cardEffectMultiplier.TryGetValue(cardId, out var m) ? m : 1f;
    public static void ApplyCardMultiplier(string cardId, float multiplier)
    {
        if (!string.IsNullOrEmpty(cardId)) _cardEffectMultiplier[cardId] = multiplier;
    }

    // 每场可重抽 1 次（spc_reroll）
    private static bool _battleRerollUsed;
    public static bool HasBattleReroll => IsCardOwned("spc_reroll") && !_battleRerollUsed;
    public static void ConsumeBattleReroll() { _battleRerollUsed = true; }

    // 可跳过 1 场普通战斗（spc_skip）
    private static bool _skipBattleUsed;

    public static bool CanSkipBattle => IsCardOwned("spc_skip") && !_skipBattleUsed;
    public static void ConsumeSkipBattle() { _skipBattleUsed = true; }

    // ─────────────────────────────────────────────
    //  商店随机货位（S1/S2/S3）
    // ─────────────────────────────────────────────
    /// 当前商店货位上的卡 ID 列表。 </summary>
    private static List<string> _shopSlotCardIds = new List<string>();
    public static IReadOnlyList<string> ShopSlotCardIds => _shopSlotCardIds;

    /// 当前商店已刷新次数（用于递增刷新费用）。 </summary>
    private static int _shopRefreshCount;
    public static int ShopRefreshCount => _shopRefreshCount;

    /// 当前商店刷新费用（10/15/20... 递增）。 </summary>
    public static int ShopRefreshCost
        => BalanceConfig.ShopRefreshBaseCost + _shopRefreshCount * BalanceConfig.ShopRefreshCostIncrement;

    /// <summary> S1：重摇商店货位，从未拥有卡中按稀有度加权随机抽取。 </summary>
    public static void RollShopSlots()
    {
        _shopSlotCardIds.Clear();
        var slots = GoldShopConfig.RollRandomShopSlots();
        foreach (var card in slots)
            _shopSlotCardIds.Add(card.cardId);
    }

    /// <summary> 获取当前货位上的卡数据列表（已购买的卡仍保留在列表中，UI 负责标记已购）。 </summary>
    public static List<TalentCardData> GetShopSlotCards()
    {
        var result = new List<TalentCardData>();
        foreach (var id in _shopSlotCardIds)
        {
            var card = TalentEffectApplier.GetCardById(id);
            if (card != null) result.Add(card);
        }
        return result;
    }

    /// <summary> S3：花费金币刷新商店货位。 </summary>
    public static bool TryRefreshShop()
    {
        InitIfNeeded();
        int cost = ShopRefreshCost;
        if (RunGold < cost) return false;
        RunGold -= cost;
        _shopRefreshCount++;
        RollShopSlots();
        SavePersistent();
        return true;
    }

    // ─────────────────────────────────────────────
    //  持有上限
    // ─────────────────────────────────────────────
    /// <summary> 已拥有某类型卡的数量。 </summary>
    public static int GetOwnedCardCountByType(TalentCardType type)
    {
        int count = 0;
        foreach (var id in _selectedTalentIds)
        {
            var card = TalentEffectApplier.GetCardById(id);
            if (card != null && card.cardType == type) count++;
        }
        return count;
    }

    /// <summary> 是否还能获取该类型的卡（每种最多 BalanceConfig.CardTypeLimit 张）。 </summary>
    public static bool CanAcquireCard(TalentCardData card)
    {
        if (card == null) return false;
        if (_selectedTalentIds.Contains(card.cardId)) return false;
        return GetOwnedCardCountByType(card.cardType) < BalanceConfig.CardTypeLimit;
    }

    // ─────────────────────────────────────────────
    //  删卡服务（S4）
    // ─────────────────────────────────────────────
    /// 本局累计删卡次数（用于递增删卡费用）。 </summary>
    private static int _cardRemovalCount;
    public static int CardRemovalCount => _cardRemovalCount;

    /// 本次商店已删卡次数。 </summary>
    private static int _cardRemovalsThisVisit;
    public static int CardRemovalsThisVisit => _cardRemovalsThisVisit;

    /// 当前删卡费用（25/40/55... 递增）。 </summary>
    public static int CardRemovalCost
        => BalanceConfig.CardRemovalBaseCost + _cardRemovalCount * BalanceConfig.CardRemovalCostIncrement;

    /// 本次商店是否还能删卡（次数未用完且拥有卡牌）。 </summary>
    public static bool CanRemoveCard
        => _cardRemovalsThisVisit < BalanceConfig.CardRemovalLimitPerVisit && _selectedTalentIds.Count > 0;

    /// <summary> 进入商店时重置本次访问的删卡计数。 </summary>
    public static void ResetShopVisitState()
    {
        _cardRemovalsThisVisit = 0;
    }

    /// <summary> S4：花费金币删除一张已拥有的天赋卡（不返还金币，纯金币出口）。 </summary>
    public static bool TryRemoveCard(TalentCardData card)
    {
        InitIfNeeded();
        if (card == null) return false;
        if (!CanRemoveCard) return false;
        if (!_selectedTalentIds.Contains(card.cardId)) return false;

        int cost = CardRemovalCost;
        if (RunGold < cost) return false;

        RunGold -= cost;
        _selectedTalentIds.Remove(card.cardId);
        _cardEffectMultiplier.Remove(card.cardId);
        _cardRemovalCount++;
        _cardRemovalsThisVisit++;
        SavePersistent();
        return true;
    }

    // 避让接触伤害免疫 + 移速暴涨（spc_evade）
    public static bool EvadeContactImmune => IsCardOwned("spc_evade");
    public const float EvadeSpeedBoostMultiplier = 10f;
    public const float EvadeSpeedBoostDuration = 0.4f;

    // 先锋阵亡自动回费（skl_vanguard_laststand）
    public static bool HasVanguardDeathDPRefund => IsCardOwned("skl_vanguard_laststand");

    /// <summary> 把一张已拥有天赋卡转化为金币（spc_convert）。</summary>
    public static bool ConvertOwnedCardToGold(TalentCardData card)
    {
        if (card == null || !CanConvertCard) return false;
        if (!_selectedTalentIds.Contains(card.cardId)) return false;
        _selectedTalentIds.Remove(card.cardId);
        _cardEffectMultiplier.Remove(card.cardId);
        int goldBefore = RunGold;
        RunGold += Mathf.Max(1, GetCardShopPrice(card));
        SavePersistent();
        return true;
    }

    // ─────────────────────────────────────────────
    //  选人阵容 + 升星养成（局内）
    // ─────────────────────────────────────────────

    /// <summary> 本局选中的干员 key（operatorName）列表，开战由 RogueEntry 写入。 </summary>
    private static List<string> _selectedRoster = new List<string>();
    public static IReadOnlyList<string> SelectedRoster => _selectedRoster;

    /// <summary> 写入本局选人阵容，并据此初始化升星养成状态（全员强制 ★1）。
    /// 选人阶段不允许预升星，星级只能靠局内「获得」解锁。需在 StartRunIfNeeded 之后、进战斗前调用。 </summary>
    public static void SetSelectedRoster(List<string> keys, IEnumerable<OperatorData> allData)
    {
        InitIfNeeded();
        _selectedRoster = new List<string>(keys ?? new List<string>());
        SaveSelectedRoster();
        OperatorStarRegistry.BeginRun(_selectedRoster, allData, forceReset: true);
    }

    /// <summary>
    /// 选人阶段实时写入「预览阵容」：每次勾选/取消后立即把当前选择落盘到 SelectedRoster。
    /// 这样中途重进 RogueEntry 场景（或编辑器重跑）已选干员不会丢——
    /// 面板 Start() 会从这里恢复，SelectedRoster 成为选人唯一真相源。
    /// 不触发 StarRegistry.BeginRun（那是点「开始本局」时的正式确认动作）。
    /// </summary>
    public static void SetPreviewRoster(List<string> keys)
    {
        _selectedRoster = new List<string>(keys ?? new List<string>());
        SaveSelectedRoster();
    }

    /// <summary> 加载全量干员数据（用于局内招募随机池）。编辑器下用 AssetDatabase，运行时用 Resources，与选人面板一致。 </summary>
    public static List<OperatorData> LoadAllOperatorData()
    {
        var result = new List<OperatorData>();
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:OperatorData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>(path);
            if (data != null && !string.IsNullOrEmpty(data.operatorName))
                result.Add(data);
        }
#else
        var list = Resources.LoadAll<OperatorData>("");
        foreach (var d in list)
            if (d != null && !string.IsNullOrEmpty(d.operatorName))
                result.Add(d);
#endif
        return result;
    }

    /// <summary> 把某干员加入本局阵容名单（招募新干员时调用），并同步升星养成状态。 </summary>
    public static void AddOperatorToRoster(string key, IEnumerable<OperatorData> allData)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!_selectedRoster.Contains(key))
        {
            _selectedRoster.Add(key);
            SaveSelectedRoster();
        }
        OperatorStarRegistry.Recruit(key, allData);
    }

    /// <summary>
    /// 战斗胜利「获得干员」：从全量干员池随机招募一个。
    /// - 已在阵容 → 重复获得，自动升 1 星；
    /// - 不在阵容 → 新干员加入阵容（★1）。
    /// 返回招募结果的友好文案；测试模式不进行（避免反复测试累积）。
    /// </summary>
    public static bool RecruitOperatorOnVictory(out string label)
    {
        label = "";
        InitIfNeeded();
        if (TestMode) return false;
        if (!HasActiveRun) return false;

        var pool = LoadAllOperatorData();
        if (pool == null || pool.Count == 0) return false;

        var data = pool[UnityEngine.Random.Range(0, pool.Count)];
        if (data == null || string.IsNullOrEmpty(data.RegistryKey)) return false;

        string key = data.RegistryKey;
        bool alreadyInRoster = _selectedRoster.Contains(key);

        if (alreadyInRoster)
            OperatorStarRegistry.Recruit(key, pool);
        else
            AddOperatorToRoster(key, pool);

        int star = OperatorStarRegistry.GetStar(key);
        label = alreadyInRoster
            ? $"获得重复干员 · {data.operatorName} → ★{star}"
            : $"招募新干员 · {data.operatorName} ★{star}";
        return true;
    }

    /// <summary> 清空选人阵容与升星养成状态（结束本局时调用）。 </summary>
    public static void ClearSelectedRoster()
    {
        _selectedRoster.Clear();
        PlayerPrefs.DeleteKey(KeySelectedRoster);
        PrefsSaver.Save();
        OperatorStarRegistry.EndRun();
    }

    /// <summary>
    /// 持久化选人阵容。肉鸽每场战斗/商店都是独立场景，静态字段会随场景切换丢失，
    /// 不落盘会导致「第二场开始阵容为空 → 升星面板与阵容部署全部失效」。
    /// </summary>
    private static void SaveSelectedRoster()
    {
        PlayerPrefs.SetString(KeySelectedRoster, string.Join(KeyTalentIdSep.ToString(), _selectedRoster));
        PrefsSaver.Save();
    }

    // ─────────────────────────────────────────────
    //  Run 生命周期
    // ─────────────────────────────────────────────

    /// <summary> 强制重置当前 run 状态，不给予任何奖励。用于「开始游戏」按钮。测试阶段：不清除卡片和关卡进度。 </summary>
    public static void ForceResetRun()
    {
        InitIfNeeded();
        // 「开始全新一局」：无论存档残留的 CurrentActId 是多少，一律回到第一个大局
        // （actId 最小 = ActConfig_1 魔王的试炼）。目前没有大局选择 UI，新局固定从 Act1 开始。
        // 之前写死 GetActConfig(2) / 仅当 CurrentActId<=0 才重置，会让首战取到
        // Act2 的 normalLevelPool[0]=23（第 23 关），玩家点击「开始本局」进入莫名奇妙的关卡。
        var defaultAct = ActRegistry.GetFirstAct();
        if (defaultAct != null)
            CurrentActId = defaultAct.actId;
        RunGold = 0;
        CardDrawCount = 0;
        HasActiveRun = false;
        CurrentStage = 1;
        // 测试阶段：保留已获得的卡片，不清除
        _pendingNextCardBonusPercent = 0;
        _battleRerollUsed = false;
        _skipBattleUsed = false;
        ClearEventState();
        CurseManager.ClearCurses();
        ClearGuardianHp();
        ClearShuffledOrder();
        ClearSelectedRoster();   // 新局重新选人，清掉上一局的阵容与星级
        _currentMapGraph = null;
        // 测试阶段：不清除节点进度，保留已通关记录方便反复测试
        if (!LevelProgress.IsTestUnlockEnabled())
            LevelProgress.ClearNodeProgress();
        SavePersistent();
    }

    public static void StartRunIfNeeded()
    {
        InitIfNeeded();
        if (HasActiveRun) return;

        // 新局必须有一个有效大局：无存档残留时 CurrentActId 可能为 0，
        // 直接回第一个大局，保证首战走 Act1 的 normalLevelPool[0]=1，
        // 而不是退化成 null ActConfig 的扫描兜底或旧 level 1 场景。
        if (CurrentActId <= 0)
        {
            var defaultAct = ActRegistry.GetFirstAct();
            if (defaultAct != null)
                CurrentActId = defaultAct.actId;
        }

        HasActiveRun = true;
        CurrentStage = 1;
        // 测试阶段：保留已获得的卡片，不清除
        ClearEventState();
        CurseManager.ClearCurses();

        RunSeed = System.Environment.TickCount ^ (int)System.DateTime.Now.Ticks;
        _runRng = new RunRng(RunSeed);
        GenerateShuffledOrder();

        // 生成 StS 分叉路径地图
        _currentMapGraph = StSMapGenerator.Generate(RunSeed, CurrentActConfig);
        LevelProgress.SetMapGraph(_currentMapGraph);
        // 测试阶段：不清除节点进度，保留已通关记录方便反复测试
        if (!LevelProgress.IsTestUnlockEnabled())
            LevelProgress.ClearNodeProgress();
        var startNode = _currentMapGraph?.GetStartNode();
        if (startNode != null)
            LevelProgress.MarkNodeCompleted(startNode.nodeId);

        // V2 剧情碎片：累计局数 +1，并检查 TotalRuns 条件碎片
        StoryCardUnlockState.IncrementRunAndCheck();

        // 清除旧局守护点血量，防止新局读到上局残留 → GameManager.Start() 走满血初始化
        ClearGuardianHp();

        // 天赋树经济线：初始金币 + 开局额外抽卡
        int goldBefore = RunGold;
        RunGold = TalentTreeState.GetInitialGoldBonus();
        
        CardDrawCount = TalentTreeState.GetExtraDraws();

        SavePersistent();
    }

    public static void ContinueToNextStage()
    {
        if (!HasActiveRun) return;
        CurrentStage = Mathf.Max(1, CurrentStage + 1);
        SavePersistent();
    }

    /// <summary> 主动结束本局回入口：根据通关层数获得天赋点。 </summary>
    public static int EndRunAndBackToEntry()
    {
        InitIfNeeded();
        int stagesCleared = Mathf.Max(0, CurrentStage - 1);
        int talentGain = BalanceConfig.DeathConsolationBase + stagesCleared * BalanceConfig.DeathConsolationPerStage;
        if (!TestMode) TalentTreeState.AddTalentPoints(talentGain);

        int goldBeforeEnd = RunGold;
        RunGold = 0;
        
        CardDrawCount = 0;
        HasActiveRun = false;
        CurrentStage = 1;
        // 测试阶段：保留已获得的卡片
        ClearEventState();
        CurseManager.ClearCurses();
        ClearGuardianHp();
        ClearShuffledOrder();
        ClearSelectedRoster();   // 升星养成是局内成长，回入口即清零
        _currentMapGraph = null;
        SavePersistent();
        return talentGain;
    }

    /// <summary>
    /// 死亡结算：失败时调用。RunGold 清零，根据通关层数返还天赋点。
    /// </summary>
    public static int FailRun()
    {
        InitIfNeeded();
        int stagesCleared = Mathf.Max(0, CurrentStage - 1);
        int consolation = BalanceConfig.DeathConsolationBase + stagesCleared * BalanceConfig.DeathConsolationPerStage;
        if (!TestMode) TalentTreeState.AddTalentPoints(consolation);

        int goldBeforeFail = RunGold;
        RunGold = 0;
        
        CardDrawCount = 0;
        HasActiveRun = false;
        CurrentStage = 1;
        // 测试阶段：保留已获得的卡片
        ClearEventState();
        CurseManager.ClearCurses();
        ClearGuardianHp();
        ClearShuffledOrder();
        ClearSelectedRoster();   // 升星养成是局内成长，失败即清零
        _currentMapGraph = null;
        SavePersistent();
        return consolation;
    }

    // ─────────────────────────────────────────────
    //  天赋卡
    // ─────────────────────────────────────────────

    /// <summary> 选卡时调用：免费选卡，仅记录本局已选。 </summary>
    public static bool TryPickTalentCard(TalentCardData card)
    {
        if (card == null || !HasActiveRun) return false;
        if (_selectedTalentIds.Contains(card.cardId)) return false;
        if (!CanAcquireCard(card)) return false; // 该类型已达持有上限

        _selectedTalentIds.Add(card.cardId);
        SavePersistent();
        return true;
    }

    public static void AddFreeTalentCard(TalentCardData card)
    {
        if (card != null && !_selectedTalentIds.Contains(card.cardId) && CanAcquireCard(card))
        {
            _selectedTalentIds.Add(card.cardId);
            SavePersistent();
        }
    }

    // ─────────────────────────────────────────────
    //  一次性战斗卡（守护点回溯救场专用）
    // ─────────────────────────────────────────────

    /// <summary> 添加一张仅本次战斗生效的卡，战斗结束后清除。 </summary>
    public static void AddBattleOnlyCard(TalentCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.cardId)) return;
        if (!_battleOnlyCardIds.Contains(card.cardId))
            _battleOnlyCardIds.Add(card.cardId);
    }

    /// <summary> 增加本次战斗临时攻击力百分比加成。 </summary>
    public static void AddBattleTempAttackPercent(int percent)
    {
        BattleTempAttackPercent += percent;
    }

    /// <summary> 增加本次战斗临时攻速百分比加成。 </summary>
    public static void AddBattleTempAttackSpeedPercent(int percent)
    {
        BattleTempAttackSpeedPercent += percent;
    }

    /// <summary> 战斗结束时调用：清除所有一次性战斗buff和卡。 </summary>
    public static void ClearBattleOnlyEffects()
    {
        _battleOnlyCardIds.Clear();
        BattleTempAttackPercent = 0;
        BattleTempAttackSpeedPercent = 0;
    }

    // ─────────────────────────────────────────────
    //  抽卡
    // ─────────────────────────────────────────────

    public static bool TryConsumeCardDraw()
    {
        if (CardDrawCount <= 0) return false;
        CardDrawCount--;
        return true;
    }

    /// <summary> 增加抽卡次数（休息/事件等非战斗节点获得）。 </summary>
    public static void AddCardDraws(int amount)
    {
        CardDrawCount += Mathf.Max(0, amount);
    }

    /// <summary> 消费局内金币（用于重抽等局内消费）。 </summary>
    public static bool TryConsumeRunGold(int amount)
    {
        InitIfNeeded();
        if (RunGold < amount) return false;
        int goldBefore = RunGold;
        RunGold -= amount;
        
        SavePersistent();
        return true;
    }

    /// <summary> 实际重抽费用（扣除天赋树重抽折扣）。 </summary>
    public static int GetRerollCost()
    {
        int cost = BalanceConfig.RerollCost - TalentTreeState.GetRerollDiscount();
        return Mathf.Max(0, cost);
    }

    // ─────────────────────────────────────────────
    //  战斗结算
    // ─────────────────────────────────────────────

    public static void PublishBattleResult(RogueBattleResult result)
    {
        LastBattleResult = result;
        HasPendingBattleResult = true;
    }

    public static bool TryConsumeBattleResult(out RogueBattleResult result)
    {
        result = LastBattleResult;
        if (!HasPendingBattleResult) return false;
        HasPendingBattleResult = false;
        return true;
    }

    /// <summary>
    /// 战斗结算：根据战斗类型、胜利等级计算金币/抽卡/天赋点奖励。
    /// 金币和抽卡次数是局内奖励；天赋点是永久奖励。
    /// </summary>
    public static RogueSettlementSummary ApplySettlement(RogueBattleResult result)
    {
        InitIfNeeded();

        var grade = EvaluateVictoryGrade(result);
        var (goldGain, cardDrawGain, talentPointGain) = BalanceConfig.GetBaseReward(result.battleType, grade);

        // 经济膨胀：基础奖励金币随当前层数温和递增（前几层几乎无感，深层才拉开差距）
        goldGain = Mathf.RoundToInt(goldGain * BalanceConfig.GetRewardDepthMultiplier());

        if (result.firstClear && result.isWin)
        {
            goldGain += BalanceConfig.GetFirstClearBonus();
        }

        // 休息点"打劫路人"：战斗胜利后追加赏金
        if (_ambushModePending && result.isWin && TryConsumeAmbushBonus(out int ambushBonus))
        {
            goldGain += Mathf.Max(0, ambushBonus);
        }

        // 天赋树经济线：通关金币百分比加成
        int goldPercent = TalentTreeState.GetGoldGainPercent();
        if (goldPercent > 0)
            goldGain = Mathf.RoundToInt(goldGain * (1f + goldPercent / 100f));

        // 赌博卡（spc_gamble）：按本关掷骰结果调整奖励与抽卡机会
        string gambleOutcome = "无押注";
        if (IsCardOwned("spc_gamble") && LevelRunModifiers.GambleResult != GambleMode.None)
        {
            if (LevelRunModifiers.GambleResult == GambleMode.Buff)
            {
                goldGain = Mathf.RoundToInt(goldGain * 1.5f);
                cardDrawGain += 1;
                gambleOutcome = "赌博·强化：金币×1.5，+1抽卡";
            }
            else // Debuff
            {
                goldGain = Mathf.RoundToInt(goldGain * 0.5f);
                cardDrawGain -= 1; // 允许降到 0（本关无抽卡）
                gambleOutcome = "赌博·削弱：金币×0.5，-1抽卡";
            }
        }

        // 付天赋点（测试模式不永久提交，仅用于界面显示）
        if (talentPointGain > 0 && !TestMode)
            TalentTreeState.AddTalentPoints(talentPointGain);

        int goldBeforeSettle = RunGold;
        RunGold += Mathf.Max(0, goldGain);
        
        CardDrawCount += Mathf.Max(0, cardDrawGain);
        SavePersistent();

        // V2 剧情碎片：战斗结算后检查碎片解锁条件
        if (result.isWin)
        {
            string stageStr = result.stage.ToString();
            switch (result.battleType)
            {
                case BattleType.Normal:
                    StoryCardUnlockState.CheckAndUnlockByEvent(
                        StoryCardUnlockState.GameEvent.LevelCleared, stageStr);
                    break;
                case BattleType.Elite:
                    StoryCardUnlockState.CheckAndUnlockByEvent(
                        StoryCardUnlockState.GameEvent.EliteDefeated, stageStr);
                    break;
                case BattleType.Boss:
                    StoryCardUnlockState.CheckAndUnlockByEvent(
                        StoryCardUnlockState.GameEvent.BossDefeated, stageStr);
                    break;
            }
            // 战斗胜利「获得干员」：重复获得自动升 1 星，新干员加入阵容（★1）
            RecruitOperatorOnVictory(out _);
        }
        // 金币达标检查（无论输赢）
        StoryCardUnlockState.CheckAndUnlockByEvent(
            StoryCardUnlockState.GameEvent.GoldReached, RunGold.ToString());

        return new RogueSettlementSummary
        {
            goldGain = goldGain,
            cardDrawGain = cardDrawGain,
            talentPointGain = talentPointGain,
            betOutcome = gambleOutcome
        };
    }

    public static VictoryGrade EvaluateVictoryGrade(RogueBattleResult result)
    {
        return result.isWin ? VictoryGrade.Normal : VictoryGrade.Loss;
    }

    private static void SavePersistent()
    {
        // 测试模式：不把本局状态持久化，避免反复进入场景累积进度
        if (TestMode) return;
        PlayerPrefs.SetInt(KeyHasActiveRun, HasActiveRun ? 1 : 0);
        PlayerPrefs.SetInt(KeyCurrentStage, Mathf.Max(1, CurrentStage));
        PlayerPrefs.SetInt(KeyRunGold, Mathf.Max(0, RunGold));
        PlayerPrefs.SetInt(KeyGuardianCurrentHp, GuardianCurrentHp);
        PlayerPrefs.SetInt(KeyGuardianMaxHp, GuardianMaxHp);
        PlayerPrefs.SetString(KeySelectedTalentIds, string.Join(KeyTalentIdSep.ToString(), _selectedTalentIds));
        PlayerPrefs.SetInt(KeyGameMode, (int)CurrentGameMode);
        PlayerPrefs.SetInt(KeyRunSeed, RunSeed);
        PlayerPrefs.SetInt(KeyCurrentActId, CurrentActId);
        PrefsSaver.Save();
    }

    public static void SaveRunStateIfNeeded()
    {
        if (!_initialized) return;
        SavePersistent();
    }

    /// <summary>
    /// 仅清除持久化的 HasActiveRun 标记，不影响内存中的当前局数据。
    /// 从后台切回时调用，删除切后台时生成的保护存档。
    /// </summary>
    public static void ClearPersistedRunFlag()
    {
        PlayerPrefs.SetInt(KeyHasActiveRun, 0);
        PrefsSaver.Save();
    }

    // ─────────────────────────────────────────────
    //  随机事件追踪
    // ─────────────────────────────────────────────

    private static HashSet<string> _encounteredEvents = new HashSet<string>();

    public static bool HasEncounteredEvent(string eventId)
    {
        return _encounteredEvents.Contains(eventId);
    }

    public static void MarkEventEncountered(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
            _encounteredEvents.Add(eventId);
    }

    // ─────────────────────────────────────────────
    //  随机事件快捷操作
    // ─────────────────────────────────────────────

    /// <summary> 直接增加金币（用于事件奖励）。 </summary>
    public static void AddRunGold(int amount)
    {
        InitIfNeeded();
        RunGold += Mathf.Max(0, amount);
        SavePersistent();
    }

    /// <summary> 直接增加抽卡次数。 </summary>
    public static void AddCardDraw(int amount)
    {
        CardDrawCount += Mathf.Max(0, amount);
    }

    /// <summary> 守护点回复（正值为回复，事件调用）。 </summary>
    private static int _pendingGuardianHeal;
    public static void HealGuardian(int amount)
    {
        _pendingGuardianHeal += Mathf.Max(0, amount);
    }
    public static int ConsumePendingGuardianHeal()
    {
        int v = _pendingGuardianHeal;
        _pendingGuardianHeal = 0;
        return v;
    }

    /// <summary> 守护点受到伤害（事件惩罚）。 </summary>
    private static int _pendingGuardianDamage;
    public static void DamageGuardian(int amount)
    {
        _pendingGuardianDamage += Mathf.Max(0, amount);
    }
    public static int ConsumePendingGuardianDamage()
    {
        int v = _pendingGuardianDamage;
        _pendingGuardianDamage = 0;
        return v;
    }

    // ─────────────────────────────────────────────
    //  卡牌操作（事件用）
    // ─────────────────────────────────────────────

    /// <summary> 随机将一张已有卡转化为金币，返回获得的金币数。0表示无可出售。 </summary>
    public static int ConvertRandomOwnedCardToGold()
    {
        if (_selectedTalentIds.Count == 0) return 0;
        int idx = Random.Range(0, _selectedTalentIds.Count);
        string cardId = _selectedTalentIds[idx];
        var card = TalentEffectApplier.GetCardById(cardId);
        int gold = card != null ? Mathf.Max(1, GetCardShopPrice(card)) : 10;
        _selectedTalentIds.RemoveAt(idx);
        _cardEffectMultiplier.Remove(cardId);
        RunGold += gold;
        SavePersistent();
        return gold;
    }

    /// <summary> 复制一张已有的随机卡（将已拥有卡的效果乘以2或重新添加）。 </summary>
    public static string DuplicateRandomOwnedCard()
    {
        if (_selectedTalentIds.Count == 0) return null;
        int idx = Random.Range(0, _selectedTalentIds.Count);
        string cardId = _selectedTalentIds[idx];
        // 效果翻倍
        if (_cardEffectMultiplier.TryGetValue(cardId, out float current))
            _cardEffectMultiplier[cardId] = current * 2f;
        else
            _cardEffectMultiplier[cardId] = 2f;
        SavePersistent();
        return cardId;
    }

    // ─────────────────────────────────────────────
    //  下场战斗修正（事件用）
    // ─────────────────────────────────────────────

    private static int _nextBattleEnemyModifier;
    private static int _nextBattleGoldBonusPercent;
    // ── 休息点"打劫路人"奖励 ──
    private static bool _ambushModePending;
    private static int _ambushGoldBonus;
    private static bool _mapRevealed;

    public static void SetNextBattleEnemyModifier(int percent)
    {
        _nextBattleEnemyModifier = percent;
    }
    public static int GetNextBattleEnemyModifier() => _nextBattleEnemyModifier;
    public static void ClearNextBattleEnemyModifier() { _nextBattleEnemyModifier = 0; }

    public static void SetNextBattleGoldBonus(int percent)
    {
        _nextBattleGoldBonusPercent = percent;
    }
    public static int GetNextBattleGoldBonusPercent() => _nextBattleGoldBonusPercent;
    public static void ClearNextBattleGoldBonus() { _nextBattleGoldBonusPercent = 0; }

    /// <summary> 休息点"打劫路人"：进入战斗前设置赏金。 </summary>
    public static void SetAmbushMode(int goldBonus)
    {
        _ambushModePending = true;
        _ambushGoldBonus = goldBonus;
    }
    public static bool TryConsumeAmbushBonus(out int bonus)
    {
        bonus = _ambushGoldBonus;
        if (!_ambushModePending || bonus <= 0) { bonus = 0; return false; }
        _ambushModePending = false;
        _ambushGoldBonus = 0;
        return true;
    }

    public static void SetMapRevealed(bool revealed) { _mapRevealed = revealed; }
    public static bool IsMapRevealed() => _mapRevealed;

    // ─────────────────────────────────────────────
    //  免费重抽 / 免费跳过（事件用）
    // ─────────────────────────────────────────────

    private static int _freeRerollsGranted;
    private static bool _freeSkipGranted;

    /// <summary> 事件给予免费重抽次数。 </summary>
    public static void GrantFreeReroll()
    {
        _freeRerollsGranted++;
    }

    /// <summary> 消耗 1 次免费重抽。返回是否消耗成功。 </summary>
    public static bool TryConsumeFreeReroll()
    {
        if (_freeRerollsGranted <= 0) return false;
        _freeRerollsGranted--;
        return true;
    }
    public static int FreeRerollsRemaining => _freeRerollsGranted;

    /// <summary> 事件给予免费跳过普通战斗。 </summary>
    public static void GrantSkipBattle()
    {
        _freeSkipGranted = true;
    }

    /// <summary> 消耗免费跳过。 </summary>
    public static bool TryConsumeFreeSkip()
    {
        if (!_freeSkipGranted) return false;
        _freeSkipGranted = false;
        return true;
    }

    /// <summary> 是否有免费跳过可用（独立于 spc_skip）。 </summary>
    public static bool HasFreeSkip => _freeSkipGranted;

    // ─────────────────────────────────────────────
    //  Run 重置时清理
    // ─────────────────────────────────────────────

    /// <summary> 重置所有事件相关临时状态。ForceResetRun 和 StartRunIfNeeded 中调用。 </summary>
    private static void ClearEventState()
    {
        _encounteredEvents.Clear();
        _pendingGuardianHeal = 0;
        _pendingGuardianDamage = 0;
        _nextBattleEnemyModifier = 0;
        _nextBattleGoldBonusPercent = 0;
        _ambushModePending = false;
        _ambushGoldBonus = 0;
        _mapRevealed = false;
        _freeRerollsGranted = 0;
        _freeSkipGranted = false;
        _shopSlotCardIds.Clear();
        _shopRefreshCount = 0;
        _cardRemovalCount = 0;
        _cardRemovalsThisVisit = 0;
    }
}

public struct RogueBattleResult
{
    public int stage;
    public bool isWin;
    public bool noHit;
    public int guardianHpEnd;
    public int guardianHpMax;
    public bool firstClear;
    public bool betPlaced;
    public BattleType battleType;
    public bool usedEmergencyProtocol;
}

public struct RogueSettlementSummary
{
    public int goldGain;
    public int cardDrawGain;
    public int talentPointGain;
    public string betOutcome;
}

public enum BattleType
{
    Normal = 0,
    Elite,
    Boss,
}

public enum VictoryGrade
{
    Loss = 0,
    Normal,
}
