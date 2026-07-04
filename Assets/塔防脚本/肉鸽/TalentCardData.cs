using UnityEngine;

/// <summary>
/// 单张天赋卡的数据（设计文档 M3 六大类）。
/// 在 Project 里右键 Create → 天赋卡 → Talent Card Data 创建，用于选卡界面显示与扣费。
/// 首版只做数据与显示，效果（攻防数值等）可后续在战斗里按 cardId 或 type 分支实现。
/// </summary>
[CreateAssetMenu(fileName = "TalentCard", menuName = "天赋卡/Talent Card Data", order = 0)]
public class TalentCardData : ScriptableObject
{
    [Tooltip("唯一 ID，用于本局已选记录与去重")]
    public string cardId;

    [Tooltip("卡片上显示的名称")]
    public string displayName = "天赋";

    [Tooltip("详细描述（选卡界面展示）")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("类型：特殊/攻击/防御/守护/稀有/技能")]
    public TalentCardType cardType = TalentCardType.Attack;

    [Tooltip("稀有度，影响出现概率与选卡池")]
    public TalentCardRarity rarity = TalentCardRarity.Common;

    [Tooltip("购买消耗的 RunPoint（1~4）")]
    [Range(1, 4)]
    public int costRunPoint = 1;

    [Tooltip("可选：卡片图标（16:9）")]
    public Sprite icon;
    
    [Tooltip("可选：卡片背面（未点击时显示）")]
    public Sprite cardBack;
    
    [Tooltip("可选：卡片正面（视频播放完毕后显示）")]
    public Sprite cardFront;
}

/// <summary> 设计文档 3.1 六类天赋。 </summary>
public enum TalentCardType
{
    Special = 0,   // 特殊卡
    Attack,        // 攻击卡
    Defense,      // 防御卡
    Guardian,     // 守护卡
    Rare,         // 稀有卡
    Skill         // 技能卡
}

/// <summary> 设计文档 3.2 稀有度与出现概率（首版可不做传奇特效）。 </summary>
public enum TalentCardRarity
{
    Common = 0,   // 普通 60%
    Advanced,    // 进阶 28%
    Rare,        // 稀有 10%
    Legendary   // 传奇 2%
}
