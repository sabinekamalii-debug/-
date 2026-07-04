using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 负责将 RogueRuntimeState 中挑选的卡牌 ID 转化为实际的战斗数值加成。
/// (核心一：天赋系统与塔防数值的“桥梁”)
/// </summary>
public static class TalentEffectApplier
{
    // 全局攻击力加成（固定值）
    public static int GetGlobalAttackBonus()
    {
        int bonus = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            if (id == "atk_1") bonus += 10;
            if (id == "atk_2") bonus += 25;
            if (id == "test_atk") bonus += 20; // 专属测试攻击卡
        }
        return bonus;
    }

    // 全局防御力加成
    public static int GetGlobalDefenseBonus()
    {
        int bonus = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            if (id == "def_1") bonus += 50;
            if (id == "def_2") bonus += 150;
        }
        return bonus;
    }

    // 守护点生命值加成
    public static int GetGuardianHpBonus()
    {
        int bonus = 0;
        foreach (var id in RogueRuntimeState.SelectedTalentCardIds)
        {
            if (id == "hp_1") bonus += 1;
            if (id == "hp_2") bonus += 2;
            if (id == "test_hp") bonus += 2; // 专属测试守护卡
        }
        return bonus;
    }
}
