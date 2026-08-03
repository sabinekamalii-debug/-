using System;
using UnityEngine;

/// <summary>
/// 战前对话配置：指定哪个节点序号在进入战斗前播放对话。
/// 默认为场景内文字对话（NewbieTutorialController），勾选 useNaninovel 后才跳转 Naninovel。
/// </summary>
[Serializable]
public class PreBattleDialogue
{
    [Tooltip("触发对话的节点序号（1-based，如5=第5关前）")]
    public int stageNumber = 1;

    [Tooltip("是否跳转 Naninovel 播放剧情（仅特殊关卡勾选）")]
    public bool useNaninovel = false;

    [Tooltip("Naninovel label 名（useNaninovel=true 时使用，在当前大局的 mainScriptName 脚本中）")]
    public string labelName = "";

    [Tooltip("场景内对话内容（useNaninovel=false 时使用，逐行显示）")]
    [TextArea(2, 4)]
    public string[] inSceneLines = new string[0];
}

/// <summary>
/// 大局配置（ScriptableObject）。
/// 每个大局定义一条独立的剧情线 + 关卡池 + Boss + Naninovel 脚本。
/// 一个 ActConfig.asset = 一个大局。
/// 
/// 设计文档参考：Assets/docs/剧情与游戏融合设计.md
/// </summary>
[CreateAssetMenu(fileName = "ActConfig_", menuName = "魔塔/大局配置", order = 50)]
public class ActConfig : ScriptableObject
{
    #region 大局标识

    [Header("大局标识")]
    [Tooltip("大局唯一编号，从1开始")]
    public int actId = 1;

    [Tooltip("大局名称，如「古城遗梦」")]
    public string actName = "未命名大局";

    [Tooltip("大局简介")]
    [TextArea(2, 4)]
    public string description = "";

    #endregion

    #region 剧情脚本

    [Header("剧情脚本")]
    [Tooltip("Naninovel 主脚本名（不含扩展名），如 plot1")]
    public string mainScriptName = "";

    [Tooltip("大局开场 Naninovel label（进入大局时播放）")]
    public string introLabel = "";

    [Tooltip("大局结局 Naninovel label（击败Boss后播放）")]
    public string outroLabel = "";

    [Header("战前对话")]
    [Tooltip("战斗前的简短对话：在指定节点序号点击进入战斗前，先播放一段 Naninovel 对话再加载战斗")]
    public PreBattleDialogue[] preBattleDialogues = new PreBattleDialogue[0];

    #endregion

    #region 关卡池

    [Header("关卡池")]
    [Tooltip("普通关卡 LevelConfig ID 池（随机/混合模式从中抽取）")]
    public int[] normalLevelPool = new int[0];

    [Tooltip("精英关卡 LevelConfig ID 池")]
    public int[] eliteLevelPool = new int[0];

    [Tooltip("Boss 关的 LevelConfig ID（固定，不参与随机）")]
    public int bossLevelConfigId = 0;

    #endregion

    #region 节点配置

    [Header("节点配置")]
    [Tooltip("本大局总节点数（含战斗/商店/休息/事件节点）。最后一关为Boss关。")]
    public int totalNodes = 21;

    [Tooltip("Boss 在第几个节点（默认为最后一个）")]
    public int bossNodeIndex = 21;

    #endregion

    #region StS地图配置

    [Header("StS地图配置")]
    [Tooltip("地图楼层数（不含Boss层）。Boss在 floorCount 层。")]
    public int mapFloorCount = 15;

    [Tooltip("地图最大列数（路径分叉宽度）")]
    public int mapMaxColumns = 5;

    [Tooltip("从起点到Boss的路径数量")]
    public int mapPathCount = 6;

    #endregion

    #region 主线碎片

    [Header("主线碎片解锁节点")]
    [Tooltip("通关到这些节点序号时解锁主线碎片")]
    public int[] mainLineUnlockStages = new[] { 1, 5, 10, 15, 21 };

    #endregion

    #region 视觉

    [Header("视觉")]
    [Tooltip("地图背景图")]
    public Sprite backgroundSprite;

    [Tooltip("视觉主题")]
    public MapTheme mapTheme = MapTheme.Grassland;

    #endregion

    #region 解锁条件

    [Header("解锁条件")]
    [Tooltip("解锁类型")]
    public ActUnlockType unlockType = ActUnlockType.Default;

    [Tooltip("需要通关的前置大局ID（unlockType=AfterAct 时使用）")]
    public int requiredCompletedActId = 0;

    [Tooltip("需要累计的Run次数（unlockType=AfterRuns 时使用）")]
    public int requiredTotalRuns = 0;

    #endregion

    #region 剧情碎片

    [Header("剧情碎片")]
    [Tooltip("本大局包含的所有剧情碎片")]
    public StoryCardData[] storyFragments = new StoryCardData[0];

    #endregion

    #region 运行时辅助方法

    /// <summary>
    /// 根据关卡类型获取对应的 LevelConfig ID 池。
    /// Boss 返回单个 ID 的数组，非战斗类型返回空数组。
    /// </summary>
    public int[] GetLevelPool(LevelType levelType)
    {
        switch (levelType)
        {
            case LevelType.NormalBattle:
                return normalLevelPool ?? new int[0];
            case LevelType.Elite:
                return eliteLevelPool ?? new int[0];
            case LevelType.Boss:
                return bossLevelConfigId > 0 ? new[] { bossLevelConfigId } : new int[0];
            default:
                return new int[0];
        }
    }

    /// <summary>
    /// 检查此大局是否已解锁。
    /// </summary>
    public bool IsUnlocked()
    {
        switch (unlockType)
        {
            case ActUnlockType.Default:
                return true;
            case ActUnlockType.AfterAct:
                return ActRegistry.IsActCompleted(requiredCompletedActId);
            case ActUnlockType.AfterRuns:
                return StoryCardUnlockState.TotalRuns >= requiredTotalRuns;
            default:
                return false;
        }
    }

    #endregion
}

/// <summary>
/// 大局解锁条件类型。
/// </summary>
public enum ActUnlockType
{
    Default,        // 默认解锁（第一个大局）
    AfterAct,       // 通关指定大局后解锁
    AfterRuns,      // 累计指定Run次数后解锁
}
