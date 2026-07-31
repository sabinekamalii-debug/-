using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 随机事件选项的结果类型。
/// 每个选项可以包含多个结果，按顺序执行。
/// </summary>
public enum RandomEventOutcomeType
{
    None = 0,
    GainGold,              // 获得金币
    LoseGold,              // 失去金币
    GainTalentPoints,      // 获得天赋点
    GainCardDraw,          // 获得抽卡次数
    AddRandomCard,         // 随机获得一张指定稀有度的卡
    AddSpecificCard,       // 获得指定 cardId 的卡
    RemoveRandomCardToGold,// 随机移除一张已有卡，转化为金币
    HealGuardian,          // 回复守护点 HP
    DamageGuardian,       // 守护点受到伤害
    AddCurse,             // 施加一张诅咒卡
    RemoveRandomCurse,    // 移除一张随机诅咒
    EnemyBuffPercent,     // 下场战斗敌人全属性 +X%
    EnemyDebuffPercent,   // 下场战斗敌人全属性 -X%
    NextBattleGoldBonus,  // 下场战斗金币 +X%
    GainReroll,           // 获得 1 次免费重抽机会
    RevealMap,            // 揭示地图上所有节点的类型
    SkipNextBattle,       // 跳过下一场普通战斗（不消耗 spc_skip）
    DuplicateRandomCard,  // 复制一张已有卡（效果翻倍等）
    GainGuardianMaxHp,    // 守护点最大 HP +X（本局永久）
    LoseGuardianMaxHp,    // 守护点最大 HP -X
}

/// <summary>
/// 单个随机事件结果。
/// </summary>
[Serializable]
public class RandomEventOutcome
{
    [Tooltip("结果类型")]
    public RandomEventOutcomeType outcomeType = RandomEventOutcomeType.None;

    [Tooltip("数值参数（金币数、HP量、百分比等）")]
    public int value = 0;

    [Tooltip("字符串参数（如特定卡ID、稀有度等）")]
    public string stringParam = "";

    [Tooltip("成功率（0~1），1=必定触发")]
    [Range(0f, 1f)]
    public float successChance = 1f;

    [Tooltip("失败时的替代结果（成功率<1时生效，为空则无事发生）")]
    public RandomEventOutcomeType failureOutcome = RandomEventOutcomeType.None;

    [Tooltip("失败替代结果的数值")]
    public int failureValue = 0;
}

/// <summary>
/// 随机事件的一个选项。
/// </summary>
[Serializable]
public class RandomEventOption
{
    [Tooltip("选项按钮文本")]
    public string buttonText = "选项";

    [Tooltip("选项描述（选前预览）")]
    [TextArea(2, 3)]
    public string previewText = "";

    [Tooltip("选项详细描述（选后展示）")]
    [TextArea(2, 4)]
    public string resultText = "";

    [Tooltip("该选项的结果列表（按顺序执行）")]
    public List<RandomEventOutcome> outcomes = new List<RandomEventOutcome>();

    [Tooltip("是否需要消耗金币才能选")]
    public int goldCost = 0;

    [Tooltip("是否需要满足条件（如拥有某卡、守护点≥X等）")]
    public string requiredCardId = "";

    [Tooltip("所需最小守护点HP")]
    public int requiredGuardianHp = 0;

    [Header("Naninovel 剧情（可选）")]
    [Tooltip("选了该选项后跳转播放的 Naninovel 剧本名（如 \"魔王 1\"），播完自动返回事件场景显示结果")]
    public string naniScriptName = "";

    [Tooltip("剧本内标签名（可选，不填则从剧本开头播放）")]
    public string naniLabel = "";
}

/// <summary>
/// 随机事件 ScriptableObject。
/// 在 Project 里右键 Create → 肉鸽 → Random Event Data 创建。
/// </summary>
[CreateAssetMenu(fileName = "RandomEvent", menuName = "肉鸽/随机事件数据", order = 50)]
public class RandomEventData : ScriptableObject
{
    [Tooltip("事件唯一ID")]
    public string eventId = "evt_001";

    [Tooltip("事件标题")]
    public string title = "未知事件";

    [Header("叙事")]
    [Tooltip("事件描述文本（进入时显示）")]
    [TextArea(3, 6)]
    public string description = "你遇到了一个未知的事件...";

    [Tooltip("事件背景图片（可选）")]
    public Sprite backgroundImage;

    [Header("选项")]
    [Tooltip("选项列表（2~4个）")]
    public List<RandomEventOption> options = new List<RandomEventOption>();

    [Header("出现条件")]
    [Tooltip("最小关卡层数（0=不限）")]
    public int minStage = 0;

    [Tooltip("最大关卡层数（0=不限）")]
    public int maxStage = 0;

    [Tooltip("出现权重（越高越容易遇到）")]
    [Range(1, 10)]
    public int weight = 5;

    [Tooltip("是否为一次性事件（本局遇到后不再出现）")]
    public bool oneShot = false;

    [Tooltip("是否可重复遇到")]
    public bool repeatable = true;

    // ⚠️ 进场触发剧情已弃用：曾用于"进入事件时跳 Naninovel 剧本"，现已移除逻辑。
    //   字段定义保留是为了让旧 .asset 文件仍能被 Unity 正常反序列化（避免破坏资源），
    //   但代码不会再读取这些字段——剧情触发统一改为选项的 naniScriptName。
    [Obsolete("进场触发剧情已移除，不再使用。保留仅为兼容旧 .asset 反序列化。")]
    [Header("Naninovel 进场剧情（已弃用）")]
    [Tooltip("(已弃用) 进入事件时是否跳转播放 Naninovel 剧本——逻辑已移除，仅保留字段兼容旧资产。")]
    public bool playNaniOnEnter = false;

    [Obsolete("进场触发剧情已移除，不再使用。保留仅为兼容旧 .asset 反序列化。")]
    [Tooltip("(已弃用) 进入事件时播放的 Naninovel 剧本名——逻辑已移除。")]
    public string naniEntryScriptName = "";

    [Obsolete("进场触发剧情已移除，不再使用。保留仅为兼容旧 .asset 反序列化。")]
    [Tooltip("(已弃用) 剧本内标签名——逻辑已移除。")]
    public string naniEntryLabel = "";
}
