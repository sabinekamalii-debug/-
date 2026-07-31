using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 守护点回溯救场卡管理器：
/// - 维护救场专用卡池（扭转大局的特殊卡）
/// - 抽3张卡供玩家选择
/// - 选卡后立即触发效果
/// 
/// 卡分为两类：
/// 1. 本局生效卡（PerRun）：加入本局卡池，持续到本局结束
/// 2. 仅本次战斗卡（PerBattle）：立即触发强力效果，战斗结束后消失
/// </summary>
public static class GuardianRewindCardManager
{
    private static List<TalentCardData> _guardianRewindCards;
    private static bool _initialized;

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        BuildGuardianRewindCardPool();
    }

    private static void BuildGuardianRewindCardPool()
    {
        _guardianRewindCards = new List<TalentCardData>
        {
            // ── 仅本次战斗生效（用完即消）──
            CreateInstantDPCard(),
            CreateInstantGuardianHealCard(),
            CreateInstantAttackBuffCard(),
            CreateInstantAttackSpeedBuffCard(),
            CreateInstantAllOperatorsHealCard(),
            CreateInstantFreezeAllCard(),
            CreateInstantDamageAllCard(),
            CreateInstantKillWeakestCard(),

            // ── 本局生效（持续到本局结束）──
            CreateRunAttackCard(),
            CreateRunDefenseCard(),
            CreateRunGuardianHpCard(),
        };
    }

    // ═══════════════════════════════════════════
    //   公共 API
    // ═══════════════════════════════════════════

    /// <summary> 从救场卡池中随机抽取3张不重复的卡。 </summary>
    public static List<TalentCardData> Draw3Cards()
    {
        EnsureInitialized();

        var result = new List<TalentCardData>();
        var pool = new List<TalentCardData>(_guardianRewindCards);

        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return result;
    }

    /// <summary> 玩家选中一张救场卡后立即触发效果。 </summary>
    public static void ApplySelectedCard(TalentCardData card)
    {
        if (card == null) return;

        if (card.cardScope == CardScope.PerRun)
        {
            RogueRuntimeState.AddFreeTalentCard(card);
            return;
        }

        RogueRuntimeState.AddBattleOnlyCard(card);
        TriggerInstantEffect(card);
    }

    /// <summary> 立即触发一次性战斗卡的效果。 </summary>
    private static void TriggerInstantEffect(TalentCardData card)
    {
        if (card == null || card.triggerType == GuardianRewindTriggerType.None) return;

        switch (card.triggerType)
        {
            case GuardianRewindTriggerType.InstantDP:
                ApplyInstantDP(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantGuardianHeal:
                ApplyInstantGuardianHeal(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantAttackBuff:
                ApplyInstantAttackBuff(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantAttackSpeedBuff:
                ApplyInstantAttackSpeedBuff(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantAllOperatorsHeal:
                ApplyInstantAllOperatorsHeal(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantFreezeAllEnemies:
                ApplyInstantFreezeAll(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantDamageAllEnemies:
                ApplyInstantDamageAll(card.triggerValue);
                break;
            case GuardianRewindTriggerType.InstantKillWeakest:
                ApplyInstantKillWeakest(card.triggerValue);
                break;
        }
    }

    // ═══════════════════════════════════════════
    //   具体效果实现
    // ═══════════════════════════════════════════

    private static void ApplyInstantDP(int amount)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddDeploymentPoints(amount);
        }
    }

    private static void ApplyInstantGuardianHeal(int amount)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HealGuardian(amount);
        }
    }

    private static void ApplyInstantAttackBuff(int percent)
    {
        RogueRuntimeState.AddBattleTempAttackPercent(percent);
    }

    private static void ApplyInstantAttackSpeedBuff(int percent)
    {
        RogueRuntimeState.AddBattleTempAttackSpeedPercent(percent);
    }

    private static void ApplyInstantAllOperatorsHeal(int amount)
    {
        var operators = Object.FindObjectsByType<OperatorUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var op in operators)
        {
            if (op != null && op.gameObject.activeInHierarchy)
            {
                op.Heal(amount);
            }
        }
    }

    private static void ApplyInstantFreezeAll(int seconds)
    {
        var enemies = Object.FindObjectsByType<Enemy2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.ApplyFreeze(seconds);
            }
        }
    }

    private static void ApplyInstantDamageAll(int damage)
    {
        var enemies = Object.FindObjectsByType<Enemy2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }
    }

    private static void ApplyInstantKillWeakest(int count)
    {
        var enemies = new List<Enemy2>(Object.FindObjectsByType<Enemy2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        enemies.RemoveAll(e => e == null || e.IsDead());
        enemies.Sort((a, b) => a.GetCurrentHealth().CompareTo(b.GetCurrentHealth()));

        int killCount = Mathf.Min(count, enemies.Count);
        for (int i = 0; i < killCount; i++)
        {
            enemies[i].TakeDamage(enemies[i].GetCurrentHealth() + 100);
        }
    }

    // ═══════════════════════════════════════════
    //   卡片定义
    // ═══════════════════════════════════════════

    private static TalentCardData CreateInstantDPCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_DP";
        card.displayName = "紧急增援";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantDP;
        card.triggerValue = 50;
        card.description = "立即获得 50 部署点数。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantGuardianHealCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_HEAL";
        card.displayName = "守护修复";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantGuardianHeal;
        card.triggerValue = 3;
        card.description = "守护点立即回复 3 点生命值。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantAttackBuffCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_ATK_BUFF";
        card.displayName = "火力全开";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantAttackBuff;
        card.triggerValue = 50;
        card.description = "本场战斗全体干员攻击力 +50%。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantAttackSpeedBuffCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_ATKSPD_BUFF";
        card.displayName = "疾风骤雨";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantAttackSpeedBuff;
        card.triggerValue = 40;
        card.description = "本场战斗全体干员攻速 +40%。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantAllOperatorsHealCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_OP_HEAL";
        card.displayName = "全员急救";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantAllOperatorsHeal;
        card.triggerValue = 500;
        card.description = "全体干员立即回复 500 生命值。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantFreezeAllCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_FREEZE";
        card.displayName = "时间停滞";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantFreezeAllEnemies;
        card.triggerValue = 5;
        card.description = "全场敌人冻结 5 秒。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantDamageAllCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_DMG_ALL";
        card.displayName = "天罚";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantDamageAllEnemies;
        card.triggerValue = 200;
        card.description = "对全场敌人造成 200 点真实伤害。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    private static TalentCardData CreateInstantKillWeakestCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_INSTANT_KILL";
        card.displayName = "精准狙击";
        card.rarity = TalentCardRarity.Legendary;
        card.cardType = TalentCardType.Special;
        card.cardScope = CardScope.PerBattle;
        card.isGuardianRewindCard = true;
        card.triggerType = GuardianRewindTriggerType.InstantKillWeakest;
        card.triggerValue = 2;
        card.description = "立即击杀 2 个血量最低的敌人。\n<color=#ff4444>【仅限本次战斗】</color>";
        return card;
    }

    // ── 本局生效卡（加入本局卡池）──

    private static TalentCardData CreateRunAttackCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_RUN_ATK";
        card.displayName = "绝境反击";
        card.rarity = TalentCardRarity.Rare;
        card.cardType = TalentCardType.Attack;
        card.cardScope = CardScope.PerRun;
        card.isGuardianRewindCard = true;
        card.effectType = TalentEffectType.AttackPercent;
        card.effectValue = 25;
        card.description = "本局全体干员攻击力 +25%。\n<color=#44ff44>【本局生效】</color>";
        return card;
    }

    private static TalentCardData CreateRunDefenseCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_RUN_DEF";
        card.displayName = "铜墙铁壁";
        card.rarity = TalentCardRarity.Rare;
        card.cardType = TalentCardType.Defense;
        card.cardScope = CardScope.PerRun;
        card.isGuardianRewindCard = true;
        card.effectType = TalentEffectType.DefensePercent;
        card.effectValue = 30;
        card.description = "本局全体干员防御力 +30%。\n<color=#44ff44>【本局生效】</color>";
        return card;
    }

    private static TalentCardData CreateRunGuardianHpCard()
    {
        var card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = "GRD_RUN_HP";
        card.displayName = "守护强化";
        card.rarity = TalentCardRarity.Rare;
        card.cardType = TalentCardType.Guardian;
        card.cardScope = CardScope.PerRun;
        card.isGuardianRewindCard = true;
        card.effectType = TalentEffectType.GuardianHpBonus;
        card.effectValue = 3;
        card.description = "守护点生命值上限 +3。\n<color=#44ff44>【本局生效】</color>";
        return card;
    }
}
