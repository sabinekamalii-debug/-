using UnityEngine;

/// <summary>
/// 单张剧情卡片的数据：点击后播放的 Naninovel 脚本与标签，以及显示用名称/图标。
/// 在 Project 里右键 Create → 剧情卡片 → Story Card Data 创建，再在面板里引用。
/// </summary>
[CreateAssetMenu(fileName = "StoryCard", menuName = "剧情卡片/Story Card Data", order = 0)]
public class StoryCardData : ScriptableObject
{
    [Tooltip("唯一 ID，用于解锁状态存档，例如 Level1_After")]
    public string cardId;

    [Tooltip("卡片上显示的名称")]
    public string displayName = "剧情片段";

    [Tooltip("Naninovel 脚本路径（不含扩展名），如 plot1")]
    public string scriptName = "plot1";

    [Tooltip("脚本内标签，如 AfterLevel1；留空则从脚本开头播放")]
    public string labelName;

    [Tooltip("可选：卡片图标")]
    public Sprite icon;
}
