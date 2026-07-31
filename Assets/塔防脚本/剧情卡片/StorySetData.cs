using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 剧情套系数据：一组有叙事关联的剧情碎片，集齐后揭示关键碎片。
/// 在 Project 里右键 Create → 剧情卡片 → Story Set Data 创建。
/// </summary>
[CreateAssetMenu(fileName = "StorySet", menuName = "剧情卡片/Story Set Data", order = 1)]
public class StorySetData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("套系唯一 ID（如 lonely_century）")]
    public string setId;

    [Tooltip("套系显示名称")]
    public string displayName = "未命名的套系";

    [Tooltip("套系描述（在碎裂之镜中显示）")]
    [TextArea(2, 4)]
    public string description;

    [Header("碎片列表")]
    [Tooltip("该套系包含的碎片 cardId（按 setIndex 排序，最后一个为关键碎片）")]
    public List<string> fragmentCardIds = new List<string>();

    [Tooltip("套系图标（用于碎裂之镜区域标识）")]
    public Sprite setIcon;

    [Header("碎裂之镜布局")]
    [Tooltip("套系在镜面上的中心位置（归一化坐标，0~1）")]
    public Vector2 mirrorCenter = new Vector2(0.5f, 0.5f);

    [Tooltip("套系在镜面上占用的扇形角度范围（度）")]
    [Range(30f, 180f)]
    public float mirrorArcDegrees = 60f;

    [Tooltip("套系区域的半径偏移（相对于镜面中心）")]
    [Range(0.3f, 0.9f)]
    public float mirrorRadius = 0.65f;

    [Header("集齐奖励（暂留，日后设计）")]
    [Tooltip("集齐奖励类型（待定）")]
    public string completeRewardType;

    [Tooltip("集齐奖励数值（待定）")]
    public int completeRewardValue;
}
