using UnityEngine;

/// <summary>
/// 剧情碎片的解锁条件类型
/// </summary>
public enum UnlockConditionType
{
    Manual = 0,
    LevelClear = 1,
    EliteDefeated = 2,
    BossDefeated = 3,
    OperatorRecruit = 4,
    AdventureChoice = 5,
    GoldReached = 9,
    TotalRuns = 10,
    NoHitCleared = 11,
}

/// <summary>
/// 单张剧情卡片数据：点击播放 Naninovel 脚本。
/// </summary>
[CreateAssetMenu(fileName = "StoryCard", menuName = "剧情卡片/Story Card Data", order = 0)]
public class StoryCardData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("唯一 ID")]
    public string cardId;

    [Tooltip("卡片上显示的名称")]
    public string displayName = "剧情片段";

    [Tooltip("剧情简介")]
    [TextArea(2, 4)]
    public string description = "暂无描述";

    [Tooltip("可选：卡片图标")]
    public Sprite icon;

    [Header("Naninovel 播放")]
    [Tooltip("Naninovel 脚本路径（不含扩展名）")]
    public string scriptName = "plot1";

    [Tooltip("脚本内标签；留空则从脚本开头播放")]
    public string labelName;

    [Header("解锁条件")]
    [Tooltip("触发解锁的条件类型")]
    public UnlockConditionType unlockConditionType = UnlockConditionType.Manual;

    [Tooltip("解锁参数（关卡号/干员名/flag名等）")]
    public string unlockParam;

    [Header("奖励")]
    [Tooltip("首次观看后给予的天赋点数量")]
    [Range(0, 10)]
    public int rewardTalentPoint = 1;
}
