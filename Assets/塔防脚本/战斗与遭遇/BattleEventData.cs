using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局内随机事件类型。
/// 在战斗中途触发，打断战斗流程，给玩家一个选择。
/// </summary>
public enum BattleEventType
{
    None = 0,
    NeutralNpc,         // 中立NPC出现（救援/交易/无视）
    EnvironmentalHazard, // 环境灾害（毒雾/落石/暴风雪）
    Reinforcements,     // 增援选择（花钱叫援军/免费但弱/不叫）
    TreasureChest,      // 宝箱（打开有风险/安全打开/无视）
    EnemySurrender,     // 敌人投降（接受/屠杀/勒索）
    MysteriousPortal,   // 神秘传送门（进入/引爆/研究）
    Betrayal,          // 内鬼背叛（处决/原谅/收买）
    SupplyDrop,        // 空投补给（争抢/观察/设伏）
    AncientMachine,    // 古代机械（启动/拆解/绕过）
    WanderingMerchant,  // 游商（交易/抢劫/无视）
}

/// <summary>
/// 局内随机事件的单个选项。
/// </summary>
[Serializable]
public class BattleEventOption
{
    public string buttonText = "选项";
    [TextArea(2, 3)]
    public string description = "";
    /// <summary> 选择后的即时效果。 </summary>
    public BattleEventOptionEffect effect;
}

/// <summary>
/// 局内事件选项的效果。
/// </summary>
[Serializable]
public class BattleEventOptionEffect
{
    [Tooltip("立即获得 DP")]
    public int dpGain = 0;
    [Tooltip("消耗 DP")]
    public int dpCost = 0;
    [Tooltip("消耗金币")]
    public int goldCost = 0;
    [Tooltip("守护点回复 HP")]
    public int guardianHeal = 0;
    [Tooltip("守护点受伤")]
    public int guardianDamage = 0;
    [Tooltip("全体干员回复 HP 百分比")]
    public int allOperatorsHealPercent = 0;
    [Tooltip("冻结全场敌人（秒）")]
    public float freezeAllEnemies = 0;
    [Tooltip("对全场敌人造成伤害")]
    public int damageAllEnemies = 0;
    [Tooltip("击杀随机 N 个敌人")]
    public int killRandomEnemies = 0;
    [Tooltip("本场战斗临时攻击力 +X%")]
    public int tempAttackPercent = 0;
    [Tooltip("本场战斗临时攻速 +X%")]
    public int tempAttackSpeedPercent = 0;
    [Tooltip("召唤友方 NPC 协助（1=是）")]
    public int spawnAlly = 0;
    [Tooltip("额外刷出一波敌人（数量）")]
    public int spawnExtraEnemies = 0;
    [Tooltip("获得一张随机天赋卡（稀有度过滤）")]
    public string gainRandomCardRarity = "";
    [Tooltip("获得一张指定天赋卡")]
    public string gainSpecificCardId = "";
    [Tooltip("施加诅咒")]
    public bool applyRandomCurse = false;
}

/// <summary>
/// 局内随机事件 ScriptableObject。
/// 战斗中随机触发，给玩家即时选择。
/// </summary>
[CreateAssetMenu(fileName = "BattleEvent", menuName = "肉鸽/局内随机事件数据", order = 51)]
public class BattleEventData : ScriptableObject
{
    [Tooltip("事件ID")]
    public string eventId = "bat_001";

    [Tooltip("事件类型")]
    public BattleEventType eventType = BattleEventType.NeutralNpc;

    [Tooltip("事件标题")]
    public string title = "突发事件";

    [Tooltip("事件描述")]
    [TextArea(3, 6)]
    public string description = "战斗中发生了意外...";

    [Header("触发条件")]
    [Tooltip("最低触发关卡")]
    [Range(1, 16)]
    public int minStage = 1;

    [Tooltip("触发概率（0~1，每关检查时随机判定）")]
    [Range(0f, 1f)]
    public float triggerChance = 0.3f;

    [Tooltip("每局最多触发次数（0=不限）")]
    public int maxPerRun = 0;

    [Tooltip("需要守护点HP低于此值才触发（0=不限）")]
    public int requireGuardianHpBelow = 0;

    [Header("选项")]
    public List<BattleEventOption> options = new List<BattleEventOption>();

    [Header("叙事")]
    [Tooltip("事件背景图（可选）")]
    public Sprite backgroundImage;
}
