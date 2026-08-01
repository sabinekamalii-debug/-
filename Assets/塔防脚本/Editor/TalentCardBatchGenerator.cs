using UnityEngine;
using UnityEditor;
using System.IO;

public class TalentCardBatchGenerator
{
    private const string BasePath = "Assets/Resources/TalentCards";

    [MenuItem("天赋卡/批量生成所有卡片")]
    public static void GenerateAllCards()
    {
        if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);

        GenerateAttackCards();
        GenerateDefenseCards();
        GenerateGuardianCards();
        GenerateSkillCards();
        GenerateRareCards();
        GenerateSpecialCards();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateCard(string folder, string cardId, string displayName,
        TalentCardType cardType, CardEffectTarget effectTarget,
        TalentEffectType eff1, int val1, int val1b,
        TalentEffectType eff2, int val2, int val2b,
        int cooldownPenalty, OperatorData.OperatorType opType, int opDataId,
        string desc)
    {
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        string path = folder + "/" + cardId + ".asset";
        TalentCardData card = ScriptableObject.CreateInstance<TalentCardData>();
        card.cardId = cardId;
        card.displayName = displayName;
        card.description = desc;
        card.cardType = cardType;
        card.effectType = eff1;
        card.effectValue = val1;
        card.effectValue2 = val1b;
        card.secondaryEffectType = eff2;
        card.secondaryEffectValue = val2;
        card.secondaryEffectValue2 = val2b;
        card.effectTarget = effectTarget;
        card.targetOperatorType = opType;
        card.targetOperatorDataId = opDataId;
        card.purchaseCooldownPenalty = cooldownPenalty;
        AssetDatabase.CreateAsset(card, path);
        EditorUtility.SetDirty(card);
    }

    // ════════════════════════════════════════════════════════════
    // 1. 攻击卡 16 张
    // ════════════════════════════════════════════════════════════
    private static void GenerateAttackCards()
    {
        string p = BasePath + "/Attack";

        // ── 攻击强化方向（3张） ──
        CreateCard(p, "atk_power1", "攻击强化Ⅰ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 10, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +10%");

        CreateCard(p, "atk_power2", "攻击强化Ⅱ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 20, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +20%");

        CreateCard(p, "atk_power3", "攻击强化Ⅲ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 35, 0,
            TalentEffectType.EliteDamageBonus, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +35%\n对精英怪伤害 +15%");

        // ── 攻速强化方向（3张） ──
        CreateCard(p, "atk_speed1", "攻速提升Ⅰ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 12, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击速度 +12%");

        CreateCard(p, "atk_speed2", "攻速提升Ⅱ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 25, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击速度 +25%");

        CreateCard(p, "atk_speed3", "攻速提升Ⅲ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 40, 0,
            TalentEffectType.AttackPercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击速度 +40%\n攻击力 +10%");

        // ── 暴击方向（3张） ──
        CreateCard(p, "atk_crit1", "暴击强化Ⅰ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.CritChanceBonus, 10, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "暴击率 +10%\n（基础暴击伤害 150%）");

        CreateCard(p, "atk_crit2", "暴击强化Ⅱ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.CritChanceBonus, 20, 0,
            TalentEffectType.CritDamageBonus, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "暴击率 +20%\n暴击伤害 +20%");

        CreateCard(p, "atk_crit3", "暴击强化Ⅲ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.CritChanceBonus, 30, 0,
            TalentEffectType.CritDamageBonus, 50, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "暴击率 +30%\n暴击伤害 +50%");

        // ── 穿透方向（2张） ──
        CreateCard(p, "atk_pierce1", "破甲Ⅰ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.DefensePenetration, 15, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "无视 15% 敌人防御");

        CreateCard(p, "atk_pierce2", "破甲Ⅱ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.DefensePenetration, 30, 0,
            TalentEffectType.AttackPercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "无视 30% 敌人防御\n攻击力 +8%");

        // ── 吸血方向（2张） ──
        CreateCard(p, "atk_lifesteal1", "吸血Ⅰ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.LifeStealPercent, 8, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击吸血 8%");

        CreateCard(p, "atk_lifesteal2", "吸血Ⅱ", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.LifeStealPercent, 18, 0,
            TalentEffectType.MaxHpPercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击吸血 18%\n最大 HP +10%");

        // ── 特殊机制方向（3张） ──
        CreateCard(p, "atk_killstack", "战意觉醒", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.KillStackAttack, 1, 20,
            TalentEffectType.LifeStealPercent, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "每击杀1个敌人+1%攻击（上限+20%）\n攻击吸血 5%");

        CreateCard(p, "atk_berserk", "狂战之怒", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.LowHpAttackBonus, 50, 0,
            TalentEffectType.AttackSpeedPercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "HP低于50%时，攻击力 +50%\nHP低于50%时，攻速 +20%");

        CreateCard(p, "atk_killbuff", "杀戮律动", TalentCardType.Attack, CardEffectTarget.Global,
            TalentEffectType.KillAttackSpeedBuff, 30, 3,
            TalentEffectType.LifeStealPercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "击杀后攻速 +30% 持续3秒\n攻击吸血 8%");

    }

    // ════════════════════════════════════════════════════════════
    // 2. 防御卡 15 张
    // ════════════════════════════════════════════════════════════
    private static void GenerateDefenseCards()
    {
        string p = BasePath + "/Defense";

        // ── HP强化方向（3张） ──
        CreateCard(p, "def_hp1", "生命强化Ⅰ", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.MaxHpPercent, 15, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "最大 HP +15%");

        CreateCard(p, "def_hp2", "生命强化Ⅱ", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.MaxHpPercent, 30, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "最大 HP +30%");

        CreateCard(p, "def_hp3", "生命强化Ⅲ", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.MaxHpPercent, 50, 0,
            TalentEffectType.DefensePercent, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "最大 HP +50%\n防御 +15%");

        // ── 防御强化方向（3张） ──
        CreateCard(p, "def_def1", "防御强化Ⅰ", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 20, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御力 +20%");

        CreateCard(p, "def_def2", "防御强化Ⅱ", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 40, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御力 +40%");

        CreateCard(p, "def_def3", "防御强化Ⅲ", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 60, 0,
            TalentEffectType.MaxHpPercent, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御力 +60%\n最大 HP +15%");

        // ── 再生方向（2张） ──
        CreateCard(p, "def_regen1", "快速恢复", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.MaxHpPercent, 15, 0,
            TalentEffectType.LifeStealPercent, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "最大 HP +15%\n攻击吸血 5%（转化为持续续航）");

        CreateCard(p, "def_regen2", "不灭再生", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.MaxHpPercent, 25, 0,
            TalentEffectType.LifeStealPercent, 12, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "最大 HP +25%\n攻击吸血 12%");

        // ── 闪避/灵活方向（2张） ──
        CreateCard(p, "def_dodge1", "灵活身法", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 15, 0,
            TalentEffectType.DefensePercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +15%\n防御 +10%");

        CreateCard(p, "def_dodge2", "疾风步", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 25, 0,
            TalentEffectType.AttackRangeBonus, 1, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +25%\n攻击范围 +1 格");

        // ── 反伤方向（2张） ──
        CreateCard(p, "def_thorns1", "荆棘护甲", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 25, 0,
            TalentEffectType.AttackPercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御 +25%\n攻击 +8%");

        CreateCard(p, "def_thorns2", "复仇反甲", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 35, 0,
            TalentEffectType.LifeStealPercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御 +35%\n吸血 8%");

        // ── 特殊机制方向（3张） ──
        CreateCard(p, "def_fortify", "坚守阵地", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 30, 0,
            TalentEffectType.EliteDamageBonus, -15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御 +30%\n受到精英怪伤害 -15%");

        CreateCard(p, "def_undying", "不屈意志", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.MaxHpPercent, 25, 0,
            TalentEffectType.DefensePercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "最大 HP +25%\n防御 +20%\n低血时防御额外+50%（待实现）");

        CreateCard(p, "def_block", "格挡大师", TalentCardType.Defense, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 30, 0,
            TalentEffectType.MaxHpPercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御 +30%\n最大 HP +20%\n格挡率+25%（待实现）");

    }

    // ════════════════════════════════════════════════════════════
    // 3. 守护卡 18 张（按用户设计）
    // ════════════════════════════════════════════════════════════
    private static void GenerateGuardianCards()
    {
        string p = BasePath + "/Guardian";

        // ── 回血方向（3张） ──
        CreateCard(p, "grd_regen1", "自然恢复", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianRegenInterval, 15, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点每 15 秒回复 1HP");

        CreateCard(p, "grd_regen2", "守护回春", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianRegenInterval, 8, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点每 8 秒回复 1HP");

        CreateCard(p, "grd_eternal1", "永恒守护", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianHpBonus, 5, 0,
            TalentEffectType.GuardianBattleEndHeal, 1, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点 HP +5\n每场战斗结束回满 HP");

        // ── HP加成方向（3张） ──
        CreateCard(p, "grd_hp1", "守护之心", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianHpBonus, 3, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点 HP +3");

        CreateCard(p, "grd_hp2", "加固壁垒", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianHpBonus, 5, 0,
            TalentEffectType.GuardianDamageReductionMax, 2, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点 HP +5\n每次受伤最多扣 2HP");

        CreateCard(p, "grd_fortress1", "不灭要塞", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianHpBonus, 8, 0,
            TalentEffectType.GuardianDamageReductionMax, 1, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点 HP +8\n每次受伤只扣 1HP");

        // ── 射击强化方向（4张） ──
        CreateCard(p, "grd_dmg1", "锐利防御", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianDamageBonus, 100, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点射击伤害 +100");

        CreateCard(p, "grd_dmg2", "守护增幅", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianDamageBonus, 200, 0,
            TalentEffectType.GuardianRangeBonus, 1, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点射击伤害 +200\n射程 +1.0");

        CreateCard(p, "grd_multi1", "守护风暴", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianMultiTarget, 2, 0,
            TalentEffectType.GuardianDamageBonus, 50, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点同时攻击 2 个目标\n射击伤害 +50");

        CreateCard(p, "grd_avatar1", "守护神降临", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianDamageBonus, 1000, 0,
            TalentEffectType.GuardianRangeBonus, 3, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点射击伤害 ×3\n射程 +3\n攻速 ×2（待攻速实现）");

        // ── 时光回溯方向（3张） ──
        CreateCard(p, "grd_rewind1", "时光延长", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianRewindExtraTime, 3, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "时光回溯回退时间 +3 秒");

        CreateCard(p, "grd_rewind2", "二次回溯", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianRewindExtraCount, 1, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "时光回溯可用次数 +1");

        CreateCard(p, "grd_timelord1", "时间领主", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianRewindExtraCount, 2, 0,
            TalentEffectType.GuardianRewindExtraTime, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "时光回溯可用 2 次\n回退 +5 秒\n回溯后全体攻速×2持续10秒（待实现）");

        // ── 传送方向（2张） ──
        CreateCard(p, "grd_teleport1", "传送加速", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.TeleportCooldownReduction, 15, 0,
            TalentEffectType.None, 0, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "干员传送冷却 -15 秒");

        CreateCard(p, "grd_teleport2", "战术转移", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.TeleportCooldownReduction, 20, 0,
            TalentEffectType.TeleportAttackSpeedBuff, 50, 5,
            0, OperatorData.OperatorType.Guard, -1,
            "传送冷却 -20 秒\n传送后干员攻速 +50% 持续 5 秒");

        // ── 特殊机制方向（3张） ──
        CreateCard(p, "grd_rage1", "守护者之怒", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianLowHpDamageMultiplier, 200, 0,
            TalentEffectType.GuardianHpBonus, 2, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点 HP ≤ 3 时，射击伤害 ×2\n守护点 HP +2");

        CreateCard(p, "grd_shield1", "绝对防御", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianShieldCount, 2, 0,
            TalentEffectType.GuardianHpBonus, 3, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点获得护盾，抵消 2 次伤害\n守护点 HP +3");

        CreateCard(p, "grd_resonance1", "守护共鸣", TalentCardType.Guardian, CardEffectTarget.Global,
            TalentEffectType.GuardianResonancePerOp, 2, 0,
            TalentEffectType.GuardianHpBonus, 2, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "每个存活干员为守护点 +2HP\n守护点 HP +2");

    }

    // ════════════════════════════════════════════════════════════
    // 4. 技能卡 12 张
    // ════════════════════════════════════════════════════════════
    private static void GenerateSkillCards()
    {
        string p = BasePath + "/Skill";

        // ── SP获取方向（3张） ──
        CreateCard(p, "skl_charge1", "SP充能Ⅰ", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 8, 0,
            TalentEffectType.AttackPercent, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +8%（加快SP获取）\n攻击 +5%\nSP获取速度 +20%（待实现）");

        CreateCard(p, "skl_charge2", "SP充能Ⅱ", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 15, 0,
            TalentEffectType.AttackPercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +15%\n攻击 +10%\nSP获取速度 +40%（待实现）");

        CreateCard(p, "skl_charge3", "SP充能Ⅲ", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 25, 0,
            TalentEffectType.CritChanceBonus, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +25%\n暴击率 +10%\nSP获取速度 +60%（待实现）");

        // ── 技能效果方向（3张） ──
        CreateCard(p, "skl_power1", "技能强化Ⅰ", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 10, 0,
            TalentEffectType.CritDamageBonus, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击 +10%\n暴击伤害 +15%\n技能效果 +25%（待实现）");

        CreateCard(p, "skl_power2", "技能强化Ⅱ", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 18, 0,
            TalentEffectType.CritDamageBonus, 25, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击 +18%\n暴击伤害 +25%\n技能效果 +50%（待实现）");

        CreateCard(p, "skl_power3", "技能强化Ⅲ", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 25, 0,
            TalentEffectType.AoeRangePercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击 +25%\nAoE范围 +20%\n技能效果 +80%（待实现）");

        // ── 冷却/持续方向（2张） ──
        CreateCard(p, "skl_cd1", "冷却缩减", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 12, 0,
            TalentEffectType.DefensePercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +12%\n防御 +8%\n技能冷却 -25%（待实现）");

        CreateCard(p, "skl_duration1", "永恒持续", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 10, 0,
            TalentEffectType.MaxHpPercent, 12, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +10%\n最大 HP +12%\n技能持续时间 +50%（待实现）");

        // ── 特殊机制方向（4张） ──
        CreateCard(p, "skl_auto", "自动施放", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.CritChanceBonus, 10, 0,
            TalentEffectType.AttackPercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "暴击率 +10%\n攻击 +8%\nSP满自动释放技能（待实现）");

        CreateCard(p, "skl_reset", "技能重置", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 15, 0,
            TalentEffectType.LifeStealPercent, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +15%\n吸血 5%\n技能结束后立即回30%SP（待实现）");

        CreateCard(p, "skl_chain", "技能连锁", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.AoeRangePercent, 20, 0,
            TalentEffectType.AttackSpeedPercent, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "AoE范围 +20%\n放技能后攻速+30%持续5秒（待实现）");

        CreateCard(p, "skl_berserk", "狂战技能", TalentCardType.Skill, CardEffectTarget.Global,
            TalentEffectType.LowHpAttackBonus, 40, 0,
            TalentEffectType.AttackSpeedPercent, 12, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "低血时攻击 +40%\n攻速 +12%\n低血时技能伤害 +50%（待实现）");

    }

    // ════════════════════════════════════════════════════════════
    // 5. 稀有卡（复合效果） 10 张
    // ════════════════════════════════════════════════════════════
    private static void GenerateRareCards()
    {
        string p = BasePath + "/Rare";

        // ── 二元复合（5张） ──
        CreateCard(p, "rar_ad", "攻防一体", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 15, 0,
            TalentEffectType.DefensePercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +15%\n防御力 +20%");

        CreateCard(p, "rar_ah", "攻血兼备", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 15, 0,
            TalentEffectType.MaxHpPercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +15%\n最大 HP +20%");

        CreateCard(p, "rar_dh", "防血双修", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 20, 0,
            TalentEffectType.MaxHpPercent, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御力 +20%\n最大 HP +20%");

        CreateCard(p, "rar_as", "战技合一", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 18, 0,
            TalentEffectType.AttackSpeedPercent, 12, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +18%\n攻速 +12%\n暴击率 +10%（第三效果待扩展）");

        CreateCard(p, "rar_ag", "攻守同盟", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 12, 0,
            TalentEffectType.GuardianHpBonus, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +12%\n守护点 HP +5\n全体防御 +10%（第三效果待扩展）");

        // ── 三元复合（3张） ──
        CreateCard(p, "rar_triple", "三位一体", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 12, 0,
            TalentEffectType.DefensePercent, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击力 +12%\n防御力 +15%\n最大 HP +15%（第三效果待扩展）");

        CreateCard(p, "rar_dga", "坚壁守护", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 18, 0,
            TalentEffectType.GuardianHpBonus, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御 +18%\n守护点 HP +5\n最大 HP +15%（第三效果待扩展）");

        CreateCard(p, "rar_ds", "守技同心", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.DefensePercent, 20, 0,
            TalentEffectType.MaxHpPercent, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "防御 +20%\n最大 HP +15%\n吸血 +8%（第三效果待扩展）");

        // ── 全面复合（2张） ──
        CreateCard(p, "rar_all", "全面发展", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 10, 0,
            TalentEffectType.DefensePercent, 12, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击 +10%\n防御 +12%\nHP +12%\n攻速 +10%（待效果列表扩展）");

        CreateCard(p, "rar_perfect", "完美主义", TalentCardType.Rare, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 15, 0,
            TalentEffectType.DefensePercent, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击 +15%\n防御 +15%\nHP +15%\n攻速 +15%\n暴击率 +10%（待效果列表扩展）");

    }

    // ════════════════════════════════════════════════════════════
    // 6. 特殊卡（元机制） 10 张
    // ════════════════════════════════════════════════════════════
    private static void GenerateSpecialCards()
    {
        string p = BasePath + "/Special";

        // ── 经济方向（3张） ──
        CreateCard(p, "spc_gold1", "金币加成Ⅰ", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.GoldBonus, 25, 0,
            TalentEffectType.ScoreBonus, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "击杀金币 +25%\n击杀分数 +15%");

        CreateCard(p, "spc_gold2", "金币加成Ⅱ", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.GoldBonus, 50, 0,
            TalentEffectType.ScoreBonus, 30, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "击杀金币 +50%\n击杀分数 +30%");

        CreateCard(p, "spc_shop", "讨价还价", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.GoldBonus, 20, 0,
            TalentEffectType.DefensePercent, 5, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "金币 +20%\n防御 +5%\n商店购买天赋卡价格 -25%");

        // ── 抽卡方向（2张） ──
        CreateCard(p, "spc_draw", "命运抉择", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.AttackPercent, 8, 0,
            TalentEffectType.DefensePercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻击 +8%\n防御 +8%\n选卡时额外提供 1 个选项（4 选 1）");

        CreateCard(p, "spc_reroll", "重新洗牌", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 8, 0,
            TalentEffectType.MaxHpPercent, 8, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +8%\nHP +8%\n每场战斗可重抽 1 次卡");

        // ── 强化方向（2张） ──
        CreateCard(p, "spc_double", "双倍效果", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.CritDamageBonus, 20, 0,
            TalentEffectType.AttackPercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "暴击伤害 +20%\n攻击 +10%\n下一张选择的卡效果 +50%");

        CreateCard(p, "spc_repair", "战场维修", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.GuardianHpBonus, 3, 0,
            TalentEffectType.MaxHpPercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "守护点 HP +3\n全体 HP +10%\n每场战斗守护点回满");

        // ── 节奏方向（3张） ──
        CreateCard(p, "spc_skip", "速通模式", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.AttackSpeedPercent, 20, 0,
            TalentEffectType.GoldBonus, 15, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "攻速 +20%\n金币 +15%\n可跳过 1 场普通战斗");

        CreateCard(p, "spc_convert", "卡牌转化", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.LifeStealPercent, 5, 0,
            TalentEffectType.DefensePercent, 10, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "吸血 +5%\n防御 +10%\n可在商店将 1 张已拥有卡转化为金币");

        CreateCard(p, "spc_fortune", "幸运女神", TalentCardType.Special, CardEffectTarget.Global,
            TalentEffectType.CritChanceBonus, 15, 0,
            TalentEffectType.GoldBonus, 20, 0,
            0, OperatorData.OperatorType.Guard, -1,
            "暴击率 +15%\n金币 +20%\n稀有/传说卡出现率提升");

    }
}
