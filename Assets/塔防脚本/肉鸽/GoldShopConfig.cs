using System.Collections.Generic;
using UnityEngine;

/// ═══════════════════════════════════════════════════════════
///  商店 — 用 RunGold（局内金币）购买天赋卡（加入本局牌组）
///  从 Resources/TalentCards/ 加载所有卡，提供商店信息
///  S1：随机货位化 — 不再列出全部未拥有卡，改为按稀有度加权随机抽取
/// ═══════════════════════════════════════════════════════════
public static class GoldShopConfig
{
    private static List<TalentCardData> _allCards;
    private static bool _loaded;

    /// <summary> 稀有度权重表（Common 60 / Advanced 28 / Rare 10 / Legendary 2）。 </summary>
    private static readonly Dictionary<TalentCardRarity, float> RarityWeights = new Dictionary<TalentCardRarity, float>
    {
        { TalentCardRarity.Common,    60f },
        { TalentCardRarity.Advanced,  28f },
        { TalentCardRarity.Rare,      10f },
        { TalentCardRarity.Legendary,  2f },
    };

    /// <summary> 商店中可购买的所有天赋卡（包含已被购买的）。 </summary>
    public static IReadOnlyList<TalentCardData> GetAllCards()
    {
        EnsureLoaded();
        return _allCards;
    }

    /// <summary> 商店中本局未拥有的天赋卡（可购买的）。 </summary>
    public static List<TalentCardData> GetAvailableCards()
    {
        EnsureLoaded();
        RogueRuntimeState.InitIfNeeded();
        var available = new List<TalentCardData>();
        foreach (var card in _allCards)
        {
            if (!RogueRuntimeState.IsCardOwned(card.cardId))
                available.Add(card);
        }
        return available;
    }

    /// <summary>
    /// S1：从全部未拥有卡中按稀有度加权随机抽取货位卡。
    /// 货位数量随机 4~6 张。spc_fortune 提升稀有度权重。
    /// 每张卡有概率获得货位折扣（S2）。
    /// </summary>
    public static List<TalentCardData> RollRandomShopSlots()
    {
        EnsureLoaded();
        RogueRuntimeState.InitIfNeeded();

        // 候选池：未拥有 + 非诅咒
        var pool = new List<TalentCardData>();
        foreach (var card in _allCards)
        {
            if (card.isCurse) continue;
            if (RogueRuntimeState.IsCardOwned(card.cardId)) continue;
            pool.Add(card);
        }

        int slotCount = Random.Range(BalanceConfig.ShopSlotCountMin, BalanceConfig.ShopSlotCountMax + 1);
        slotCount = Mathf.Min(slotCount, pool.Count);

        var result = new List<TalentCardData>();
        var used = new HashSet<string>();

        for (int i = 0; i < slotCount && pool.Count > 0; i++)
        {
            // 构建加权列表（已选过的跳过）
            float totalWeight = 0f;
            var candidates = new List<(TalentCardData card, float weight)>();
            foreach (var card in pool)
            {
                if (used.Contains(card.cardId)) continue;
                float w = GetRarityWeight(card);
                candidates.Add((card, w));
                totalWeight += w;
            }

            if (candidates.Count == 0 || totalWeight <= 0f) break;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            TalentCardData picked = null;
            foreach (var (card, weight) in candidates)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    picked = card;
                    break;
                }
            }
            if (picked == null) picked = candidates[candidates.Count - 1].card;

            used.Add(picked.cardId);
            result.Add(picked);

            // S2：货位折扣
            if (Random.Range(0f, 1f) < BalanceConfig.ShopSlotDiscountChance)
            {
                int idx = Random.Range(0, BalanceConfig.ShopSlotDiscountValues.Length);
                picked.slotDiscount = BalanceConfig.ShopSlotDiscountValues[idx];
            }
            else
            {
                picked.slotDiscount = 1f;
            }
        }

        // 清除未选中卡的货位折扣
        foreach (var card in _allCards)
        {
            if (!used.Contains(card.cardId))
                card.slotDiscount = 0f;
        }

        return result;
    }

    /// <summary> 获取稀有度权重（spc_fortune 提升稀有度，流派倾向调整类型权重）。 </summary>
    private static float GetRarityWeight(TalentCardData card)
    {
        if (!RarityWeights.TryGetValue(card.rarity, out float w)) w = 1f;
        if (RogueRuntimeState.RareRateUpActive)
        {
            // spc_fortune：稀有/传奇权重翻倍
            if (card.rarity == TalentCardRarity.Rare || card.rarity == TalentCardRarity.Legendary)
                w *= 2f;
        }
        // 流派倾向：按天赋卡类型叠加权重乘子（盛怒攻 / 苟道防+守护）。
        w *= RogueRuntimeState.GetCardTypeWeightMultiplier(card.cardType);
        return w;
    }

    /// <summary>
    /// 公开：某张卡的抽卡综合权重（稀有度权重 × 流派倾向类型权重）。
    /// 供奖励选卡 / 事件选卡等所有随机抽卡入口复用，保证「盛怒/苟道」倾向在全游戏统一生效。
    /// </summary>
    public static float GetCardWeight(TalentCardData card)
    {
        return GetRarityWeight(card);
    }

    /// <summary> 获取稀有度显示名。 </summary>
    public static string GetRarityDisplayName(TalentCardRarity rarity)
    {
        return rarity switch
        {
            TalentCardRarity.Common => "普通",
            TalentCardRarity.Advanced => "进阶",
            TalentCardRarity.Rare => "稀有",
            TalentCardRarity.Legendary => "传奇",
            _ => "?",
        };
    }

    /// <summary> 获取类型显示名。 </summary>
    public static string GetTypeDisplayName(TalentCardType type)
    {
        return type switch
        {
            TalentCardType.Special => "特殊",
            TalentCardType.Attack => "攻击",
            TalentCardType.Defense => "防御",
            TalentCardType.Guardian => "守护",
            TalentCardType.Rare => "稀有",
            TalentCardType.Skill => "技能",
            _ => "?",
        };
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _allCards = new List<TalentCardData>();

        var cards = Resources.LoadAll<TalentCardData>("TalentCards");
        foreach (var card in cards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardId)) continue;
            _allCards.Add(card);
        }
    }
}
