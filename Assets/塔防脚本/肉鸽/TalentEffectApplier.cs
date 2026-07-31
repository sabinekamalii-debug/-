using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 负责将 RogueRuntimeState 中挑选的卡牌 ID + TalentTreeState 的天赋树加成
/// 转化为实际的战斗数值加成。
///
/// 两套加成叠加：
/// - 天赋卡（局内临时）：从 Resources/TalentCards/ 加载，按 cardId 索引
/// - 天赋树（永久被动）：从 TalentTreeState 获取，局外加点点亮
///
/// 混合型方案：
/// - Global 卡：对所有干员生效
/// - ByClass 卡：只对对应职业干员生效
/// - ByOperator 卡：只对对应干员生效
/// </summary>
public static class TalentEffectApplier
{
    private static Dictionary<string, TalentCardData> _cardDatabase;
    private static bool _databaseLoaded;

    private static void EnsureDatabase()
    {
        if (_databaseLoaded) return;
        _databaseLoaded = true;
        _cardDatabase = new Dictionary<string, TalentCardData>();

        var cards = Resources.LoadAll<TalentCardData>("TalentCards");
        foreach (var card in cards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardId)) continue;
            if (!_cardDatabase.ContainsKey(card.cardId))
                _cardDatabase[card.cardId] = card;
        }
    }

    private static TalentCardData GetCard(string id)
    {
        EnsureDatabase();
        _cardDatabase.TryGetValue(id, out var card);
        return card;
    }

    /// <summary>
    /// 判断一张卡对指定干员是否生效。
    /// - Global：总是生效
    /// - ByClass：需要匹配职业
    /// - ByOperator：需要匹配干员名称哈希
    /// 如果 opData 为 null，则只算全局卡。
    /// </summary>
    private static bool IsCardEffectiveFor(TalentCardData card, OperatorData opData)
    {
        if (card == null) return false;
        switch (card.effectTarget)
        {
            case CardEffectTarget.Global:
                return true;
            case CardEffectTarget.ByClass:
                return opData != null && opData.opType == card.targetOperatorType;
            case CardEffectTarget.ByOperator:
                return opData != null && !string.IsNullOrEmpty(opData.operatorName)
                    && card.targetOperatorDataId >= 0
                    && opData.operatorName.GetHashCode() == card.targetOperatorDataId;
            default:
                return false;
        }
    }

    private delegate bool EffectMatcher(TalentCardData card);
    private delegate int ValueExtractor(TalentCardData card);

    private static int SumEffectValues(OperatorData opData, TalentEffectType effectType,
        ValueExtractor primaryValue, ValueExtractor secondaryValue)
    {
        int total = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var card = GetCard(id);
            if (card == null || !IsCardEffectiveFor(card, opData)) continue;
            float mult = RogueRuntimeState.GetCardMultiplier(id);
            if (card.effectType == effectType) total += Mathf.RoundToInt(primaryValue(card) * mult);
            if (card.secondaryEffectType == effectType) total += Mathf.RoundToInt(secondaryValue(card) * mult);
        }
        return total;
    }

    // ─────────────────────────────────────────────
    //  攻击力
    // ─────────────────────────────────────────────

    /// <summary> 攻击力固定加成（卡牌 + 天赋树）。opData=null 时只算全局卡。 </summary>
    public static int GetAttackBonus(OperatorData opData = null)
    {
        int bonus = SumEffectValues(opData, TalentEffectType.AttackBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
        bonus += TalentTreeState.GetAttackFlat();
        return bonus;
    }

    /// <summary> 攻击力百分比加成（卡牌 + 天赋树 + 一次性战斗buff）。opData=null 时只算全局卡。 </summary>
    public static int GetAttackPercent(OperatorData opData = null)
    {
        int percent = SumEffectValues(opData, TalentEffectType.AttackPercent,
            c => c.effectValue, c => c.secondaryEffectValue);
        percent += TalentTreeState.GetAttackPercent();
        percent += RogueRuntimeState.BattleTempAttackPercent;
        return percent;
    }

    // ─────────────────────────────────────────────
    //  防御力
    // ─────────────────────────────────────────────

    /// <summary> 防御力固定加成（卡牌 + 天赋树）。opData=null 时只算全局卡。 </summary>
    public static int GetDefenseBonus(OperatorData opData = null)
    {
        int bonus = SumEffectValues(opData, TalentEffectType.DefenseBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
        bonus += TalentTreeState.GetDefenseFlat();
        return bonus;
    }

    /// <summary> 防御力百分比加成（卡牌 + 天赋树）。opData=null 时只算全局卡。 </summary>
    public static int GetDefensePercent(OperatorData opData = null)
    {
        int percent = SumEffectValues(opData, TalentEffectType.DefensePercent,
            c => c.effectValue, c => c.secondaryEffectValue);
        percent += TalentTreeState.GetDefensePercent();
        return percent;
    }

    // ─────────────────────────────────────────────
    //  守护点生命
    // ─────────────────────────────────────────────

    /// <summary> 守护点生命值固定加成（卡牌 + 天赋树）。 </summary>
    public static int GetGuardianHpBonus()
    {
        int bonus = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var card = GetCard(id);
            if (card == null) continue;
            float mult = RogueRuntimeState.GetCardMultiplier(id);
            if (card.effectType == TalentEffectType.GuardianHpBonus) bonus += Mathf.RoundToInt(card.effectValue * mult);
            if (card.secondaryEffectType == TalentEffectType.GuardianHpBonus) bonus += Mathf.RoundToInt(card.secondaryEffectValue * mult);
        }
        bonus += TalentTreeState.GetGuardianHpBonus();
        return bonus;
    }

    // ─────────────────────────────────────────────
    //  金币 / 分数
    // ─────────────────────────────────────────────

    /// <summary> 击杀金币百分比加成（卡牌）。 </summary>
    public static int GetGoldBonusPercent()
    {
        return SumEffectValues(null, TalentEffectType.GoldBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 击杀分数百分比加成（卡牌）。 </summary>
    public static int GetScoreBonusPercent()
    {
        return SumEffectValues(null, TalentEffectType.ScoreBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    // ─────────────────────────────────────────────
    //  攻速 / 攻击范围
    // ─────────────────────────────────────────────

    /// <summary> 攻速加成百分比（卡牌 + 天赋树 + 一次性战斗buff）。opData=null 时只算全局卡。 </summary>
    public static int GetAttackSpeedPercent(OperatorData opData = null)
    {
        int percent = SumEffectValues(opData, TalentEffectType.AttackSpeedPercent,
            c => c.effectValue, c => c.secondaryEffectValue);
        percent += TalentTreeState.GetAttackSpeedPercent();
        percent += RogueRuntimeState.BattleTempAttackSpeedPercent;
        return percent;
    }

    /// <summary> 攻速倍率（1.0 = 无加成）。 </summary>
    public static float GetAttackSpeedMultiplier(OperatorData opData = null)
    {
        return 1f + GetAttackSpeedPercent(opData) / 100f;
    }

    /// <summary> 攻击范围加成（卡牌 + 天赋树，格数）。opData=null 时只算全局卡。 </summary>
    public static float GetAttackRangeBonus(OperatorData opData = null)
    {
        float bonus = 0f;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var card = GetCard(id);
            if (card == null || !IsCardEffectiveFor(card, opData)) continue;
            float mult = RogueRuntimeState.GetCardMultiplier(id);
            if (card.effectType == TalentEffectType.AttackRangeBonus) bonus += card.effectValue * mult;
            if (card.secondaryEffectType == TalentEffectType.AttackRangeBonus) bonus += card.secondaryEffectValue * mult;
        }
        bonus += TalentTreeState.GetAttackRangeBonus();
        return bonus;
    }

    /// <summary> 部署半径加成（天赋树，格数）。 </summary>
    public static float GetDeployRangeBonus()
    {
        return TalentTreeState.GetDeployRangeBonus();
    }

    /// <summary> DP 回复速度加成（天赋树，每秒额外回复量）。 </summary>
    public static int GetDpRegenBonus()
    {
        return TalentTreeState.GetDpRegenBonus();
    }

    /// <summary> 初始 DP 加成（天赋树）。 </summary>
    public static int GetInitialDpBonus()
    {
        return TalentTreeState.GetInitialDpBonus();
    }

    /// <summary> DP 上限加成（天赋树）。 </summary>
    public static int GetDpCapBonus()
    {
        return TalentTreeState.GetDpCapBonus();
    }

    /// <summary> 是否拥有守护点免死（天赋树防御线大天赋）。 </summary>
    public static bool HasGuardianSave()
    {
        return TalentTreeState.HasGuardianSave();
    }

    // ─────────────────────────────────────────────
    //  暴击 / 穿透 / 吸血 / 精英伤害 / 等
    // ─────────────────────────────────────────────

    /// <summary> 无视防御百分比（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetDefensePenetrationPercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.DefensePenetration,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 暴击率加成（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetCritChancePercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.CritChanceBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 暴击伤害加成（卡牌）。基础150%，返回如 30 = +30% 暴伤。opData=null 时只算全局卡。 </summary>
    public static int GetCritDamagePercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.CritDamageBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 攻击吸血百分比（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetLifeStealPercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.LifeStealPercent,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 对精英怪伤害加成（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetEliteDamageBonusPercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.EliteDamageBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 最大生命值百分比加成（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetMaxHpPercentBonus(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.MaxHpPercent,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 低血时攻击力加成百分比（卡牌，HP<50%时触发）。opData=null 时只算全局卡。 </summary>
    public static int GetLowHpAttackBonusPercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.LowHpAttackBonus,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> AoE 范围百分比加成（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetAoeRangePercentBonus(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.AoeRangePercent,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    // ─────────────────────────────────────────────
    //  移动速度
    // ─────────────────────────────────────────────

    /// <summary> 移动速度倍率（MoveSpeedPercent，每1=+1%）。先锋职业卡按 ByClass 生效。opData=null 时只算全局卡。 </summary>
    public static float GetMoveSpeedMultiplier(OperatorData opData = null)
    {
        int percent = SumEffectValues(opData, TalentEffectType.MoveSpeedPercent,
            c => c.effectValue, c => c.secondaryEffectValue);
        return 1f + percent / 100f;
    }

    /// <summary> 击杀叠加攻击力（卡牌）。返回每层加的攻击力。opData=null 时只算全局卡。 </summary>
    public static int GetKillStackAttackPerStack(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.KillStackAttack,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 击杀叠加攻击力上限（卡牌）。0 表示无上限。opData=null 时只算全局卡。 </summary>
    public static int GetKillStackAttackCap(OperatorData opData = null)
    {
        int cap = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var card = GetCard(id);
            if (card == null || !IsCardEffectiveFor(card, opData)) continue;
            if (card.effectType == TalentEffectType.KillStackAttack)
            {
                if (card.effectValue2 == 0) return 0;
                cap += Mathf.RoundToInt(card.effectValue2 * RogueRuntimeState.GetCardMultiplier(id));
            }
            if (card.secondaryEffectType == TalentEffectType.KillStackAttack)
            {
                if (card.secondaryEffectValue2 == 0) return 0;
                cap += Mathf.RoundToInt(card.secondaryEffectValue2 * RogueRuntimeState.GetCardMultiplier(id));
            }
        }
        return cap;
    }

    /// <summary> 击杀后攻速加成百分比（卡牌）。opData=null 时只算全局卡。 </summary>
    public static int GetKillAttackSpeedBuffPercent(OperatorData opData = null)
    {
        return SumEffectValues(opData, TalentEffectType.KillAttackSpeedBuff,
            c => c.effectValue, c => c.secondaryEffectValue);
    }

    /// <summary> 击杀后攻速加成持续时间（秒）。opData=null 时只算全局卡。 </summary>
    public static int GetKillAttackSpeedBuffDuration(OperatorData opData = null)
    {
        int duration = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            var card = GetCard(id);
            if (card == null || !IsCardEffectiveFor(card, opData)) continue;
            float mult = RogueRuntimeState.GetCardMultiplier(id);
            if (card.effectType == TalentEffectType.KillAttackSpeedBuff) duration += Mathf.RoundToInt(card.effectValue2 * mult);
            if (card.secondaryEffectType == TalentEffectType.KillAttackSpeedBuff) duration += Mathf.RoundToInt(card.secondaryEffectValue2 * mult);
        }
        return duration;
    }

    // ─────────────────────────────────────────────
    //  兼容旧 API（标记为过时，提醒使用新方法）
    // ─────────────────────────────────────────────

    [System.Obsolete("Use GetAttackBonus() instead")]
    public static int GetGlobalAttackBonus() => GetAttackBonus(null);

    /// <summary> 按 cardId 获取卡牌数据（供商店等读取已拥有卡）。 </summary>
    public static TalentCardData GetCardById(string id) => GetCard(id);

    [System.Obsolete("Use GetAttackPercent() instead")]
    public static int GetGlobalAttackPercent() => GetAttackPercent(null);

    [System.Obsolete("Use GetDefenseBonus() instead")]
    public static int GetGlobalDefenseBonus() => GetDefenseBonus(null);

    [System.Obsolete("Use GetDefensePercent() instead")]
    public static int GetGlobalDefensePercent() => GetDefensePercent(null);
}
