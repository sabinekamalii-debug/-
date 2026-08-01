using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡总配置（ScriptableObject）。
/// 每关一份 .asset，BattleScene 运行时读取它来构建地图、配置波次、施加限制条件。
/// 
/// 设计理念：一个关卡 = 一份数据表（地图网格 + 路线 + 波次 + 限制），
///           不再为每个关卡建独立 Unity 场景。
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "魔塔/关卡配置", order = 100)]
public class LevelConfig : ScriptableObject
{
    #region 基本信息

    [Header("基本信息")]
    [Tooltip("关卡唯一 ID，如 3, 4, 5... 对应原 level 3/4/5")]
    public int levelId;

    [Tooltip("所属大局 ID，0=未分配。用于 ActConfig 关卡池分类。")]
    public int actId = 0;

    [Tooltip("关卡显示名，如「荒原遭遇战」")]
    public string displayName = "未命名关卡";

    [Tooltip("关卡类型：普通/精英/Boss/商店/休息/随机事件")]
    public LevelType levelType = LevelType.NormalBattle;

    #endregion

    #region 地图数据

    [Header("地图数据")]
    [Tooltip("网格列数（X 方向）")]
    [Range(1, 50)]
    public int gridWidth = 20;

    [Tooltip("网格行数（Y 方向）")]
    [Range(1, 30)]
    public int gridHeight = 12;

    /// <summary>
    /// 网格数据：一维数组，索引 = y * gridWidth + x。
    /// 值含义：0=空地, 1=Ground（地面，近战可部署/行走）,
    ///         2=Wall（墙，不可通行/部署）, 3=HighGround（高台，远程可部署）
    /// </summary>
    [Tooltip("网格数据（一维展平），索引 = y * gridWidth + x。0=空 1=地面 2=墙 3=高台")]
    public int[] gridData;

    /// <summary>
    /// 视觉主题：决定 MapBuilder 用哪套 Tile 贴图。
    /// </summary>
    [Tooltip("视觉主题（草原/森林/沙漠/Boss/精英）")]
    public MapTheme mapTheme = MapTheme.Grassland;

    #endregion

    #region 敌人路线

    [Header("敌人路线")]
    [Tooltip("路点坐标数组。最多 4 条路线。每条路线是一组世界坐标。")]
    public Vector3[] path0Waypoints;
    public Vector3[] path1Waypoints;
    public Vector3[] path2Waypoints;
    public Vector3[] path3Waypoints;

    /// <summary> 运行时拿全部非空路线 </summary>
    public Vector3[][] GetAllPaths()
    {
        // 保留原始路径索引位置；若某条路径为空，则保持对应位置为 null，避免 pathIndex 映射错乱。
        return new Vector3[][]
        {
            path0Waypoints,
            path1Waypoints,
            path2Waypoints,
            path3Waypoints
        };
    }

    #endregion

    #region 波次配置

    [Header("波次配置")]
    [Tooltip("本关所有波次。每个 WaveEntry 定义一个波次组（支持同波次多路同时出怪）。")]
    public WaveGroup[] waveGroups;

    [Tooltip("特殊波次索引：该波次第一个敌人染紫色（天赋怪），0 表示不染色")]
    public int specialWaveIndex = 0;

    [Tooltip("特殊敌人染色颜色")]
    public Color specialEnemyColor = new Color(0.6f, 0f, 1f, 1f);

    /// <summary> 把 WaveGroup[] 展平成单个 WaveData[] 列表，供 Spawner 使用。 </summary>
    public List<WaveData> FlattenToWaveDataList()
    {
        var list = new List<WaveData>();
        if (waveGroups == null) return list;

        int waveDisplayNum = 1;
        foreach (var group in waveGroups)
        {
            if (group == null || group.entries == null) continue;
            foreach (var entry in group.entries)
            {
                if (entry == null || entry.count <= 0) continue;
                // 为每个 entry 创建临时 WaveData（不存盘，仅运行时使用）
                var wd = ScriptableObject.CreateInstance<WaveData>();
                wd.waveNumberDisplay = waveDisplayNum;
                wd.enemyType = (EnemyType)entry.enemyTypeInt;
                wd.spawnInterval = entry.spawnInterval;
                wd.enemiesPerWave = entry.count;
                wd.delayBeforeWave = group.delayBeforeGroup;
                wd.pathIndex = entry.pathIndex;
                list.Add(wd);
            }
            waveDisplayNum++;
        }
        return list;
    }

    #endregion

    #region 限制条件（未来使用）

    [Header("限制条件（可留空=不限）")]
    [Tooltip("本关允许出现的敌人类型。留空=不限制。")]
    public EnemyType[] availableEnemyTypes;

    [Tooltip("本关禁用的干员职业")]
    public OperatorData.OperatorType[] bannedOperatorTypes;

    [Tooltip("最大部署干员数，0=不限")]
    public int maxDeployCount = 0;

    [Tooltip("初始 DP 费用")]
    public int startDP = 20;

    [Tooltip("守护点最大血量")]
    public int maxLifePoint = 5;

    [Tooltip("敌人血量全局倍率")]
    public float enemyHpMultiplier = 1.0f;

    [Tooltip("敌人移速全局倍率")]
    public float enemySpeedMultiplier = 1.0f;

    #endregion

    #region 标签（兼容旧 LevelEndMenu）

    [Header("标签（剧情跳转用）")]
    [Tooltip("通关后 Naninovel 跳转标签，如 AfterLevel3。留空自动生成。")]
    public string afterLevelLabel = "";

    [Tooltip("通关后发放的剧情卡片（可选）")]
    public StoryCardData cardToUnlockOnWin;

    #endregion

    #region 辅助方法

    /// <summary>
    /// 创建本关卡配置的运行时副本，并在副本上叠加受控随机修饰。
    /// 永不写回源 .asset（三道保险之三）。
    /// 随机只作用在修饰层，不动结构层（地图/路线/波次节奏不变）。
    /// </summary>
    public LevelConfig ApplyRunModifiers(RunRng rng, int levelNumber, RunModifierConfig modifierConfig)
    {
        if (rng == null || modifierConfig == null) return this;

        var copy = Instantiate(this);
        copy.name = $"{name}_Modified_L{levelNumber}_S{rng.Seed}";

        int stage = RogueRuntimeState.CurrentStage;

        copy.enemyHpMultiplier = rng.NextFloat(
            modifierConfig.enemyHpMin,
            modifierConfig.GetHpMax(stage));

        copy.enemySpeedMultiplier = rng.NextFloat(
            modifierConfig.enemySpeedMin,
            modifierConfig.GetSpeedMax(stage));

        copy.startDP = Mathf.Max(0, startDP + rng.NextInt(
            modifierConfig.startDPOffsetMin,
            modifierConfig.startDPOffsetMax));

        copy.maxLifePoint = Mathf.Max(1, maxLifePoint + rng.NextInt(
            modifierConfig.maxLifePointOffsetMin,
            modifierConfig.maxLifePointOffsetMax));

        if (modifierConfig.enableEnemyPoolSwap && availableEnemyTypes != null && availableEnemyTypes.Length > 0)
        {
            copy.waveGroups = ApplyEnemyPoolSwap(rng, modifierConfig);
        }

        return copy;
    }

    private WaveGroup[] ApplyEnemyPoolSwap(RunRng rng, RunModifierConfig modifierConfig)
    {
        if (waveGroups == null) return null;

        var newGroups = new WaveGroup[waveGroups.Length];
        for (int g = 0; g < waveGroups.Length; g++)
        {
            var srcGroup = waveGroups[g];
            if (srcGroup == null || srcGroup.entries == null)
            {
                newGroups[g] = srcGroup;
                continue;
            }

            var newGroup = new WaveGroup
            {
                delayBeforeGroup = srcGroup.delayBeforeGroup,
                entries = new WaveEntry[srcGroup.entries.Length]
            };

            for (int e = 0; e < srcGroup.entries.Length; e++)
            {
                var srcEntry = srcGroup.entries[e];
                if (srcEntry == null)
                {
                    newGroup.entries[e] = srcEntry;
                    continue;
                }

                var newEntry = new WaveEntry
                {
                    enemyTypeInt = srcEntry.enemyTypeInt,
                    spawnInterval = srcEntry.spawnInterval,
                    count = srcEntry.count,
                    pathIndex = srcEntry.pathIndex,
                };

                if (rng.NextBool(modifierConfig.enemySwapChance))
                {
                    int poolIdx = rng.NextInt(0, availableEnemyTypes.Length - 1);
                    newEntry.enemyTypeInt = (int)availableEnemyTypes[poolIdx];
                }

                newGroup.entries[e] = newEntry;
            }

            newGroups[g] = newGroup;
        }

        return newGroups;
    }

    /// <summary> 获取网格中指定坐标的类型。越界返回 0（空地）。 </summary>
    public int GetCellType(int x, int y)
    {
        if (gridData == null) return 0;
        int idx = y * gridWidth + x;
        if (idx < 0 || idx >= gridData.Length) return 0;
        return gridData[idx];
    }

    /// <summary> 设置网格中指定坐标的类型。 </summary>
    public void SetCellType(int x, int y, int value)
    {
        if (gridData == null)
            gridData = new int[gridWidth * gridHeight];
        int idx = y * gridWidth + x;
        if (idx >= 0 && idx < gridData.Length)
            gridData[idx] = value;
    }

    /// <summary> 初始化网格数组（全部填 0）。 </summary>
    public void InitGridData()
    {
        gridData = new int[gridWidth * gridHeight];
    }

#if UNITY_EDITOR
    [ContextMenu("初始化网格数组")]
    void EditorInitGridData() => InitGridData();
#endif

    #endregion
}

#region 子数据结构

/// <summary>
/// 地图视觉主题。
/// </summary>
public enum MapTheme
{
    Grassland,  // 草原
    Forest,     // 森林
    Desert,     // 沙漠
    Boss,       // Boss 专用
    Elite,      // 精英专用
}

/// <summary>
/// 一个波次组：同时（或按序）在一或多条路线出怪。
/// 取代旧的离散 WaveData 文件，让一个 LevelConfig 包含完整波次。
/// </summary>
[Serializable]
public class WaveGroup
{
    [Tooltip("该波次组开始前延迟（秒）")]
    public float delayBeforeGroup = 3f;

    [Tooltip("该波次组内的出怪条目。多个条目可同时在不同路线出怪。")]
    public WaveEntry[] entries;
}

/// <summary>
/// 单个出怪条目：在某条路线上以固定间隔出一组敌人。
/// </summary>
[Serializable]
public class WaveEntry
{
    [Tooltip("敌人类型（整数，对应 EnemyType 枚举）")]
    public int enemyTypeInt = 0;

    [Tooltip("生成间隔（秒）")]
    public float spawnInterval = 3f;

    [Tooltip("本条目敌人数")]
    public int count = 3;

    [Tooltip("路线索引（0-3，对应 Path0-Path3）")]
    public int pathIndex = 0;

    // ===== 方便代码引用的属性 =====
    public EnemyType enemyType
    {
        get => (EnemyType)enemyTypeInt;
        set => enemyTypeInt = (int)value;
    }
}

/// <summary>
/// 网格单元格类型。
/// </summary>
public enum CellType
{
    Empty = 0,      // 无 Tile
    Ground = 1,     // 地面（近战行走/部署）
    Wall = 2,       // 墙壁（不可通行/部署）
    HighGround = 3, // 高台（远程部署，近战不可行走）
}

#endregion
