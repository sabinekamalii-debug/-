using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CardBatchGenerator
{
    // 效果类型枚举值
    private const int EffectType_None = 0;
    private const int EffectType_AttackPercent = 4;
    private const int EffectType_DefensePercent = 5;
    private const int EffectType_GuardianHpBonus = 3;
    private const int EffectType_AttackSpeedPercent = 8;
    private const int EffectType_CritChanceBonus = 11;
    private const int EffectType_CritDamageBonus = 12;
    private const int EffectType_AttackRangeBonus = 9;
    private const int EffectType_DefensePenetration = 10;
    private const int EffectType_MaxHpPercent = 15;
    private const int EffectType_GoldBonus = 6;
    private const int EffectType_ScoreBonus = 7;
    private const int EffectType_LifeStealPercent = 13;
    private const int EffectType_KillStackAttack = 16;
    private const int EffectType_AoeRangePercent = 19;
    private const int EffectType_GuardianDamageBonus = 21;
    private const int EffectType_GuardianRangeBonus = 22;
    private const int EffectType_GuardianMultiTarget = 23;
    private const int EffectType_GuardianAttackSpeedPercent = 24;
    private const int EffectType_GuardianRegenInterval = 20;
    private const int EffectType_TeleportCooldownReduction = 27;
    private const int EffectType_GuardianRewindExtraTime = 25;
    private const int EffectType_GuardianShieldCount = 28;
    private const int EffectType_EliteDamageBonus = 14;
    private const int EffectType_MoveSpeedPercent = 35;

    // 卡牌类型枚举值
    private const int CardType_Special = 0;
    private const int CardType_Attack = 1;
    private const int CardType_Defense = 2;
    private const int CardType_Guardian = 3;
    private const int CardType_Rare = 4;
    private const int CardType_Skill = 5;

    // 效果目标枚举值
    private const int EffectTarget_Global = 0;
    private const int EffectTarget_ByClass = 1;

    // 职业类型枚举值
    private const int OperatorType_Vanguard = 0;
    private const int OperatorType_Guard = 1;
    private const int OperatorType_Defender = 2;
    private const int OperatorType_Sniper = 3;
    private const int OperatorType_Caster = 4;
    private const int OperatorType_Medic = 5;
    private const int OperatorType_Specialist = 6;

    // 作用域
    private const int CardScope_PerRun = 0;

    [MenuItem("Tools/生成星卡/Attack攻击卡")]
    public static void GenerateAttackCards()
    {
        var cards = new List<CardInfo>
        {
            new CardInfo("atk_power", "力量强化", "攻击+20%", CardType_Attack, EffectTarget_Global, 0, EffectType_AttackPercent, 20, 0, EffectType_None, 0, 0),
            new CardInfo("atk_speed", "疾风步", "攻速+25%", CardType_Attack, EffectTarget_Global, 0, EffectType_AttackSpeedPercent, 25, 0, EffectType_None, 0, 0),
            new CardInfo("atk_crit", "精准打击", "暴击率+20%，暴击伤害+40%", CardType_Attack, EffectTarget_Global, 0, EffectType_CritChanceBonus, 20, 0, EffectType_CritDamageBonus, 40, 0),
            new CardInfo("atk_range", "远程射击", "射程+15%", CardType_Attack, EffectTarget_Global, 0, EffectType_AttackRangeBonus, 15, 0, EffectType_None, 0, 0),
            new CardInfo("atk_pierce", "破甲穿击", "无视30%防御", CardType_Attack, EffectTarget_Global, 0, EffectType_DefensePenetration, 30, 0, EffectType_None, 0, 0),
            new CardInfo("atk_vanguard", "先锋荣耀", "先锋攻击力+30%，侦察伤害+50%", CardType_Attack, EffectTarget_ByClass, OperatorType_Vanguard, EffectType_AttackPercent, 30, 0, EffectType_EliteDamageBonus, 50, 0),
            new CardInfo("atk_guard", "近卫荣耀", "近卫攻击+25%，攻速+15%", CardType_Attack, EffectTarget_ByClass, OperatorType_Guard, EffectType_AttackPercent, 25, 0, EffectType_AttackSpeedPercent, 15, 0),
            new CardInfo("atk_sniper", "狙击强化", "狙击攻速+30%", CardType_Attack, EffectTarget_ByClass, OperatorType_Sniper, EffectType_AttackSpeedPercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("atk_caster", "术师精通", "术师AoE范围+30%", CardType_Attack, EffectTarget_ByClass, OperatorType_Caster, EffectType_AoeRangePercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("atk_ramp", "战意觉醒", "每击杀+2%攻击(上限+50%)", CardType_Attack, EffectTarget_Global, 0, EffectType_KillStackAttack, 2, 50, EffectType_None, 0, 0),
        };
        GenerateCards(cards, "Assets/Resources/TalentCards/Attack");
    }

    [MenuItem("Tools/生成星卡/Defense防御卡")]
    public static void GenerateDefenseCards()
    {
        var cards = new List<CardInfo>
        {
            new CardInfo("def_hp", "生命强化", "生命+25%", CardType_Defense, EffectTarget_Global, 0, EffectType_MaxHpPercent, 25, 0, EffectType_None, 0, 0),
            new CardInfo("def_armor", "铁壁防御", "防御+30%", CardType_Defense, EffectTarget_Global, 0, EffectType_DefensePercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("def_dodge", "灵活闪避", "攻速+20%", CardType_Defense, EffectTarget_Global, 0, EffectType_AttackSpeedPercent, 20, 0, EffectType_None, 0, 0),
            new CardInfo("def_vanguard_block", "先锋壁垒", "先锋阻挡数+1", CardType_Defense, EffectTarget_ByClass, OperatorType_Vanguard, EffectType_AttackSpeedPercent, 20, 0, EffectType_None, 0, 0),
            new CardInfo("def_defender_block", "重装壁垒", "重装阻挡数+1", CardType_Defense, EffectTarget_ByClass, OperatorType_Defender, EffectType_DefensePercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("def_guard", "近卫防御", "近卫防御+40%，生命+15%", CardType_Defense, EffectTarget_ByClass, OperatorType_Guard, EffectType_DefensePercent, 40, 0, EffectType_MaxHpPercent, 15, 0),
            new CardInfo("def_ranged_hp", "远程生存", "远程生命+30%", CardType_Defense, EffectTarget_ByClass, OperatorType_Sniper, EffectType_MaxHpPercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("def_medic", "医疗守护", "医疗治疗量+30%", CardType_Defense, EffectTarget_ByClass, OperatorType_Medic, EffectType_MaxHpPercent, 25, 0, EffectType_None, 0, 0),
            new CardInfo("def_specialist", "特种机动", "特种移速+50%", CardType_Defense, EffectTarget_ByClass, OperatorType_Specialist, EffectType_MoveSpeedPercent, 50, 0, EffectType_None, 0, 0),
            new CardInfo("def_armor_regen", "自然恢复", "生命+20%，防御+15%", CardType_Defense, EffectTarget_Global, 0, EffectType_MaxHpPercent, 20, 0, EffectType_DefensePercent, 15, 0),
        };
        GenerateCards(cards, "Assets/Resources/TalentCards/Defense");
    }

    [MenuItem("Tools/生成星卡/Guardian守护卡")]
    public static void GenerateGuardianCards()
    {
        var cards = new List<CardInfo>
        {
            new CardInfo("grd_hp", "守护之心", "守护点+3生命", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianHpBonus, 3, 0, EffectType_None, 0, 0),
            new CardInfo("grd_damage", "守护之矛", "守护点伤害+150", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianDamageBonus, 150, 0, EffectType_None, 0, 0),
            new CardInfo("grd_speed", "守护加速", "守护点射速+50%", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianAttackSpeedPercent, 50, 0, EffectType_None, 0, 0),
            new CardInfo("grd_range", "守护射程", "守护点范围+2", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianRangeBonus, 2, 0, EffectType_None, 0, 0),
            new CardInfo("grd_pierce", "守护穿透", "守护点无视防御", CardType_Guardian, EffectTarget_Global, 0, EffectType_DefensePenetration, 100, 0, EffectType_None, 0, 0),
            new CardInfo("grd_multi", "守护风暴", "守护点同时攻击+2目标", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianMultiTarget, 2, 0, EffectType_None, 0, 0),
            new CardInfo("grd_regen", "守护回复", "守护点每10秒回1血", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianRegenInterval, 10, 0, EffectType_None, 0, 0),
            new CardInfo("grd_teleport", "瞬移精通", "传送冷却-40%", CardType_Guardian, EffectTarget_Global, 0, EffectType_TeleportCooldownReduction, 40, 0, EffectType_None, 0, 0),
            new CardInfo("grd_rewind", "时光延长", "回溯时间+3秒", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianRewindExtraTime, 3, 0, EffectType_None, 0, 0),
            new CardInfo("grd_shield", "能量护盾", "守护点获得2层护盾", CardType_Guardian, EffectTarget_Global, 0, EffectType_GuardianShieldCount, 2, 0, EffectType_None, 0, 0),
        };
        GenerateCards(cards, "Assets/Resources/TalentCards/guardian");
    }

    [MenuItem("Tools/生成星卡/Skill技能卡")]
    public static void GenerateSkillCards()
    {
        var cards = new List<CardInfo>
        {
            new CardInfo("skl_charge", "技能充能", "攻速+20%", CardType_Skill, EffectTarget_Global, 0, EffectType_AttackSpeedPercent, 20, 0, EffectType_None, 0, 0),
            new CardInfo("skl_duration", "技能延续", "技能持续+40%", CardType_Skill, EffectTarget_Global, 0, EffectType_AttackSpeedPercent, 20, 0, EffectType_AttackPercent, 10, 0),
            new CardInfo("skl_guard", "卫士之怒", "近卫技能伤害+50%", CardType_Skill, EffectTarget_ByClass, OperatorType_Guard, EffectType_AttackPercent, 50, 0, EffectType_None, 0, 0),
            new CardInfo("skl_sniper_chain", "狙击连锁", "狙击攻击弹射1次", CardType_Skill, EffectTarget_ByClass, OperatorType_Sniper, EffectType_AoeRangePercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("skl_caster_invincible", "法师无敌", "法师技能期间无敌", CardType_Skill, EffectTarget_ByClass, OperatorType_Caster, EffectType_AttackPercent, 30, 0, EffectType_None, 0, 0),
            new CardInfo("skl_defender_heal", "重装复苏", "重装技能回10%血", CardType_Skill, EffectTarget_ByClass, OperatorType_Defender, EffectType_MaxHpPercent, 10, 0, EffectType_DefensePercent, 15, 0),
            new CardInfo("skl_medic_attack", "医者仁心", "医疗技能攻击敌人", CardType_Skill, EffectTarget_ByClass, OperatorType_Medic, EffectType_AttackPercent, 15, 0, EffectType_MaxHpPercent, 20, 0),
            new CardInfo("skl_auto", "自动技能", "技能冷却好自动释放(仅3秒技能)", CardType_Skill, EffectTarget_Global, 0, EffectType_AttackSpeedPercent, 15, 0, EffectType_CritChanceBonus, 10, 0),
            new CardInfo("skl_specialist_tp", "特种传送", "特种可免费传送1次", CardType_Skill, EffectTarget_ByClass, OperatorType_Specialist, EffectType_MoveSpeedPercent, 40, 0, EffectType_None, 0, 0),
            new CardInfo("skl_vanguard_dp", "先锋征召", "先锋部署返30%费", CardType_Skill, EffectTarget_ByClass, OperatorType_Vanguard, EffectType_AttackPercent, 20, 0, EffectType_AttackSpeedPercent, 15, 0),
        };
        GenerateCards(cards, "Assets/Resources/TalentCards/Skill");
    }

    [MenuItem("Tools/生成星卡/Rare稀有卡")]
    public static void GenerateRareCards()
    {
        var cards = new List<CardInfo>
        {
            new CardInfo("rar_attack_all", "全军突击", "攻击+15%，攻速+15%", CardType_Rare, EffectTarget_Global, 0, EffectType_AttackPercent, 15, 0, EffectType_AttackSpeedPercent, 15, 0),
            new CardInfo("rar_defense_all", "全军防御", "防御+25%，生命+15%", CardType_Rare, EffectTarget_Global, 0, EffectType_DefensePercent, 25, 0, EffectType_MaxHpPercent, 15, 0),
            new CardInfo("rar_vanguard_speed", "先锋共鸣", "每先锋+1，所有人移速+5%", CardType_Rare, EffectTarget_Global, 0, EffectType_MoveSpeedPercent, 5, 0, EffectType_AttackSpeedPercent, 10, 0),
            new CardInfo("rar_guard_passive", "战阵联动", "近战多阻挡时被动也触发", CardType_Rare, EffectTarget_ByClass, OperatorType_Guard, EffectType_AttackPercent, 20, 0, EffectType_AttackSpeedPercent, 20, 0),
            new CardInfo("rar_boss_slayer", "精英杀手", "对精英/Boss伤害+40%", CardType_Rare, EffectTarget_Global, 0, EffectType_EliteDamageBonus, 40, 0, EffectType_None, 0, 0),
            new CardInfo("rar_dp_burst", "部署爆发", "部署后前3次攻击伤害翻倍", CardType_Rare, EffectTarget_Global, 0, EffectType_AttackPercent, 25, 0, EffectType_AttackSpeedPercent, 20, 0),
            new CardInfo("rar_kill_reinforce", "击杀强化", "每击杀+1%全属性(本场，上限30%)", CardType_Rare, EffectTarget_Global, 0, EffectType_KillStackAttack, 1, 30, EffectType_None, 0, 0),
            new CardInfo("rar_first_strike", "先发制人", "战斗前5秒攻击+80%", CardType_Rare, EffectTarget_Global, 0, EffectType_AttackPercent, 30, 0, EffectType_AttackSpeedPercent, 25, 0),
            new CardInfo("rar_dp_cap", "调度大师", "DP上限+100", CardType_Rare, EffectTarget_Global, 0, EffectType_AttackPercent, 15, 0, EffectType_DefensePercent, 15, 0),
            new CardInfo("rar_starting_gold", "初始财富", "开局+80金币", CardType_Rare, EffectTarget_Global, 0, EffectType_GoldBonus, 25, 0, EffectType_None, 0, 0),
        };
        GenerateCards(cards, "Assets/Resources/TalentCards/Rare");
    }

    [MenuItem("Tools/生成星卡/Special特殊卡")]
    public static void GenerateSpecialCards()
    {
        var cards = new List<CardInfo>
        {
            new CardInfo("spc_gold", "财源滚滚", "金币+35%", CardType_Special, EffectTarget_Global, 0, EffectType_GoldBonus, 35, 0, EffectType_None, 0, 0),
            new CardInfo("spc_draw", "命运抉择", "每场+1抽卡", CardType_Special, EffectTarget_Global, 0, EffectType_ScoreBonus, 20, 0, EffectType_GoldBonus, 15, 0),
            new CardInfo("spc_reroll", "免费重抽", "本局免费重抽2次", CardType_Special, EffectTarget_Global, 0, EffectType_GoldBonus, 20, 0, EffectType_DefensePercent, 10, 0),
            new CardInfo("spc_extra_shop", "免费购物", "本局免费买1张卡", CardType_Special, EffectTarget_Global, 0, EffectType_GoldBonus, 30, 0, EffectType_None, 0, 0),
            new CardInfo("spc_card_capacity", "星卡容量", "持有上限+1", CardType_Special, EffectTarget_Global, 0, EffectType_DefensePercent, 15, 0, EffectType_MaxHpPercent, 10, 0),
            new CardInfo("spc_guard_range", "先锋扩张", "先锋部署范围+2", CardType_Special, EffectTarget_ByClass, OperatorType_Vanguard, EffectType_AttackRangeBonus, 2, 0, EffectType_MoveSpeedPercent, 20, 0),
            new CardInfo("spc_sniper_range", "狙击射程", "狙击攻击范围+3", CardType_Special, EffectTarget_ByClass, OperatorType_Sniper, EffectType_AttackRangeBonus, 3, 0, EffectType_CritChanceBonus, 15, 0),
            new CardInfo("spc_guard_attack", "卫士横扫", "近卫同时攻击阻挡敌", CardType_Special, EffectTarget_ByClass, OperatorType_Guard, EffectType_AttackPercent, 25, 0, EffectType_AoeRangePercent, 30, 0),
            new CardInfo("spc_repair", "战场维修", "每场守护点回满血", CardType_Special, EffectTarget_Global, 0, EffectType_GuardianHpBonus, 2, 0, EffectType_MaxHpPercent, 8, 0),
            new CardInfo("spc_revive", "复活契约", "30%概率复活死亡干员", CardType_Special, EffectTarget_Global, 0, EffectType_MaxHpPercent, 15, 0, EffectType_LifeStealPercent, 10, 0),
        };
        GenerateCards(cards, "Assets/Resources/TalentCards/Special");
    }

    [MenuItem("Tools/生成星卡/全部生成60张")]
    public static void GenerateAllCards()
    {
        GenerateAttackCards();
        GenerateDefenseCards();
        GenerateGuardianCards();
        GenerateSkillCards();
        GenerateRareCards();
        GenerateSpecialCards();
        AssetDatabase.Refresh();
        Debug.Log("[CardBatchGenerator] 全部60张星卡生成完成!");
    }

    private static void GenerateCards(List<CardInfo> cards, string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentPath = "Assets/Resources/TalentCards";
            string folderName = folderPath.Replace(parentPath + "/", "");
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        foreach (var card in cards)
        {
            CreateCardAsset(card, folderPath);
        }
    }

    private static void CreateCardAsset(CardInfo card, string folderPath)
    {
        var asset = ScriptableObject.CreateInstance<TalentCardData>();
        asset.cardId = card.cardId;
        asset.displayName = card.displayName;
        asset.description = card.description;
        asset.cardType = (TalentCardType)card.cardType;
        asset.rarity = TalentCardRarity.Common;
        asset.effectType = (TalentEffectType)card.effectType;
        asset.effectValue = card.effectValue;
        asset.effectValue2 = card.effectValue2;
        asset.secondaryEffectType = (TalentEffectType)card.secondaryEffectType;
        asset.secondaryEffectValue = card.secondaryEffectValue;
        asset.secondaryEffectValue2 = card.secondaryEffectValue2;
        asset.effectTarget = (CardEffectTarget)card.effectTarget;
        asset.targetOperatorType = (OperatorData.OperatorType)card.targetOperatorType;
        asset.targetOperatorDataId = -1;
        asset.purchaseCooldownPenalty = card.effectTarget == EffectTarget_ByClass ? 15 : 0;
        asset.cardScope = (CardScope)CardScope_PerRun;
        asset.isGuardianRewindCard = false;
        asset.triggerType = GuardianRewindTriggerType.None;
        asset.triggerValue = 0;
        asset.isCurse = false;

        string assetPath = $"{folderPath}/{card.cardId}.asset";
        AssetDatabase.CreateAsset(asset, assetPath);
        Debug.Log($"[CardBatchGenerator] 生成: {assetPath}");
    }

    private class CardInfo
    {
        public string cardId;
        public string displayName;
        public string description;
        public int cardType;
        public int effectTarget;
        public int targetOperatorType;
        public int effectType;
        public int effectValue;
        public int effectValue2;
        public int secondaryEffectType;
        public int secondaryEffectValue;
        public int secondaryEffectValue2;

        public CardInfo(string cardId, string displayName, string description, int cardType,
            int effectTarget, int targetOperatorType, int effectType, int effectValue, int effectValue2,
            int secondaryEffectType, int secondaryEffectValue, int secondaryEffectValue2)
        {
            this.cardId = cardId;
            this.displayName = displayName;
            this.description = description;
            this.cardType = cardType;
            this.effectTarget = effectTarget;
            this.targetOperatorType = targetOperatorType;
            this.effectType = effectType;
            this.effectValue = effectValue;
            this.effectValue2 = effectValue2;
            this.secondaryEffectType = secondaryEffectType;
            this.secondaryEffectValue = secondaryEffectValue;
            this.secondaryEffectValue2 = secondaryEffectValue2;
        }
    }
}
