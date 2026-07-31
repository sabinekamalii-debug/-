using UnityEngine;

/// <summary>
/// 剧情卡片分类：主线、支线、角色、活动
/// </summary>
public enum StoryCardCategory
{
    Main,       // 主线
    Side,       // 支线
    Character,  // 角色
    Event       // 活动
}

/// <summary>
/// 剧情碎片的解锁条件类型
/// </summary>
public enum UnlockConditionType
{
    /// <summary> 无需条件，代码/编辑器手动解锁（调试用） </summary>
    Manual = 0,

    /// <summary> 通关指定关卡（参数=关卡号，如 "3"） </summary>
    LevelClear = 1,

    /// <summary> 击败精英怪（参数=精英怪ID或关卡号；留空=任意精英） </summary>
    EliteDefeated = 2,

    /// <summary> 击败Boss（参数=BossID或关卡号；留空=任意Boss） </summary>
    BossDefeated = 3,

    /// <summary> 招募指定干员（参数=OperatorData 的 operatorId 名称） </summary>
    OperatorRecruit = 4,

    /// <summary> 满足指定奇遇选择 flag（参数=flag名，如 "sympathy_demon"） </summary>
    AdventureChoice = 5,

    /// <summary> 已拥有同套系前序碎片（参数=依赖的 cardId） </summary>
    FragmentChain = 6,

    /// <summary> 同套系其余碎片已集齐（用于关键碎片D的解锁判断，无参数） </summary>
    SetComplete = 7,

    /// <summary> 观看过指定碎片（参数=碎片 cardId） </summary>
    FragmentViewed = 8,

    /// <summary> 本局金币达到指定数值（参数=金币数量） </summary>
    GoldReached = 9,

    /// <summary> 累计游戏局数达到指定值（参数=局数） </summary>
    TotalRuns = 10,

    /// <summary> 无伤通关任意关卡（参数=关卡号；留空=任意关卡） </summary>
    NoHitCleared = 11,
}

/// <summary>
/// 单张剧情卡片的数据：点击后播放的 Naninovel 脚本与标签，以及显示用名称/图标。
/// 在 Project 里右键 Create → 剧情卡片 → Story Card Data 创建，再在面板里引用。
///
/// V2 新增：解锁条件系统、套系归属、天赋点奖励
/// </summary>
[CreateAssetMenu(fileName = "StoryCard", menuName = "剧情卡片/Story Card Data", order = 0)]
public class StoryCardData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("唯一 ID，用于解锁状态存档，例如 lonely_century_01")]
    public string cardId;

    [Tooltip("卡片上显示的名称")]
    public string displayName = "剧情片段";

    [Tooltip("剧情简介")]
    [TextArea(2, 4)]
    public string description = "暂无描述";

    [Tooltip("剧情分类")]
    public StoryCardCategory category = StoryCardCategory.Main;

    [Tooltip("可选：卡片图标")]
    public Sprite icon;

    [Header("Naninovel 播放")]
    [Tooltip("Naninovel 脚本路径（不含扩展名），如 plot1")]
    public string scriptName = "plot1";

    [Tooltip("脚本内标签，如 AfterLevel1；留空则从脚本开头播放")]
    public string labelName;

    [Header("解锁条件（V2 新增）")]
    [Tooltip("触发解锁的条件类型")]
    public UnlockConditionType unlockConditionType = UnlockConditionType.Manual;

    [Tooltip("解锁参数（关卡号/干员名/flag名/金币数等，视条件类型而定）")]
    public string unlockParam;

    [Header("套系归属（V2 新增）")]
    [Tooltip("所属剧情套系 ID（如 lonely_century），留空=独立碎片")]
    public string fragmentSetId;

    [Tooltip("在套系中的序号（0=第一片，1=第二片...）")]
    [Range(0, 10)]
    public int setIndex;

    [Tooltip("是否为关键碎片：集齐套系时居中的揭示碎片")]
    public bool isKeyFragment;

    [Header("奖励（V2 新增）")]
    [Tooltip("首次观看后给予的天赋点数量")]
    [Range(0, 10)]
    public int rewardTalentPoint = 1;
}
