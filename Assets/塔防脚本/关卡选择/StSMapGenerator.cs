using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slay the Spire 风格地图生成器。
///
/// 算法：
/// 1. 创建起点（floor 0，单节点）
/// 2. 生成 N 条路径，每条从起点到 Boss
/// 3. 路径在每层选择 -1/0/+1 的列偏移，可以分叉和合并
/// 4. 按楼层规则分配节点类型（战斗/精英/商店/休息/事件）
/// 5. 应用约束：不连续休息/商店、第一层必战斗、Boss前有休息等
/// </summary>
public static class StSMapGenerator
{
    public const int DefaultFloorCount = 15;
    public const int DefaultMaxColumns = 5;
    public const int DefaultPathCount = 6;

    public static MapGraph Generate(int seed, ActConfig actConfig = null)
    {
        int floorCount = actConfig != null && actConfig.mapFloorCount > 0
            ? actConfig.mapFloorCount : DefaultFloorCount;
        int maxColumns = actConfig != null && actConfig.mapMaxColumns > 0
            ? actConfig.mapMaxColumns : DefaultMaxColumns;
        int pathCount = actConfig != null && actConfig.mapPathCount > 0
            ? actConfig.mapPathCount : DefaultPathCount;

        var rng = new RunRng(seed);
        var graph = new MapGraph
        {
            seed = seed,
            floorCount = floorCount,
            maxColumns = maxColumns,
            allNodes = new List<MapNodeData>()
        };

        // Floor 0: Start node (center column) — acts as first battle node
        int startCol = maxColumns / 2;
        var startNode = new MapNodeData(0, 0, startCol, LevelType.Start);
        graph.allNodes.Add(startNode);

        int nextNodeId = 1;

        // Generate paths from start to boss floor
        for (int p = 0; p < pathCount; p++)
        {
            int currentCol = startCol;
            MapNodeData prevNode = startNode;

            for (int floor = 1; floor < floorCount; floor++)
            {
                // Column delta: -1, 0, or +1
                int delta = rng.NextInt(-1, 1);
                int newCol = Mathf.Clamp(currentCol + delta, 0, maxColumns - 1);
                currentCol = newCol;

                // Find or create node at this floor/column
                var node = graph.FindNode(floor, newCol);
                if (node == null)
                {
                    node = new MapNodeData(nextNodeId++, floor, newCol, LevelType.NormalBattle);
                    graph.allNodes.Add(node);
                }

                // Connect previous node → this node (avoid duplicate connections)
                if (prevNode != null && !prevNode.nextNodeIds.Contains(node.nodeId))
                {
                    prevNode.nextNodeIds.Add(node.nodeId);
                }

                prevNode = node;
            }
        }

        // Boss floor: single boss node (center column)
        var bossNode = new MapNodeData(nextNodeId, floorCount, maxColumns / 2, LevelType.Boss);
        graph.allNodes.Add(bossNode);

        // Connect all last-floor nodes to boss (search allNodes directly, lookups not built yet)
        foreach (var node in graph.allNodes)
        {
            if (node.floor == floorCount - 1 && !node.nextNodeIds.Contains(bossNode.nodeId))
                node.nextNodeIds.Add(bossNode.nodeId);
        }

        // Rebuild lookups after all nodes + connections are finalized
        graph.BuildLookups();

        // Assign node types (floor-based rules + constraints)
        AssignNodeTypes(graph, rng, actConfig);

        // Assign LevelConfig IDs from ActConfig pools
        AssignLevelConfigIds(graph, actConfig);

        return graph;
    }

    // ─────────────────────────────────────────────
    //  节点类型分配
    // ─────────────────────────────────────────────

    private static void AssignNodeTypes(MapGraph graph, RunRng rng, ActConfig actConfig)
    {
        // Floor 1: always NormalBattle
        foreach (var node in graph.GetFloor(1))
        {
            if (node.nodeType != LevelType.Start && node.nodeType != LevelType.Boss)
                node.nodeType = LevelType.NormalBattle;
        }

        // Assign remaining nodes with weighted random + constraints
        for (int floor = 2; floor < graph.floorCount; floor++)
        {
            var floorNodes = graph.GetFloor(floor);
            var info = GetFloorInfo(floor, graph.floorCount);

            // If this floor has a forced Rest, ensure at least one Rest
            bool needsForcedRest = info.forceRest;
            bool restAssigned = false;

            foreach (var node in floorNodes)
            {
                // Skip already-assigned nodes (fixed types, Start, Boss)
                if (node.nodeType != LevelType.NormalBattle) continue;

                var predTypes = GetPredecessorTypes(graph, node);
                var type = GenerateNodeType(info, predTypes, rng, needsForcedRest && !restAssigned);
                node.nodeType = type;
                if (type == LevelType.Rest) restAssigned = true;
            }

            // If forced Rest wasn't assigned naturally, force it on first node
            if (needsForcedRest && !restAssigned && floorNodes.Count > 0)
            {
                foreach (var node in floorNodes)
                {
                    if (node.nodeType == LevelType.NormalBattle)
                    {
                        node.nodeType = LevelType.Rest;
                        break;
                    }
                }
            }
        }
    }

    private struct FloorInfo
    {
        public bool noElite;
        public bool noRest;
        public bool noShop;
        public bool forceRest;
        public float monsterWeight;
        public float eliteWeight;
        public float restWeight;
        public float shopWeight;
        public float eventWeight;
    }

    private static FloorInfo GetFloorInfo(int floor, int totalFloors)
    {
        var info = new FloorInfo
        {
            monsterWeight = 40f,
            eliteWeight = 10f,
            restWeight = 10f,
            shopWeight = 10f,
            eventWeight = 30f,
        };

        // Floor 1: always monster (handled separately, but set for safety)
        if (floor == 1)
        {
            info.monsterWeight = 100f;
            info.eliteWeight = 0f;
            info.restWeight = 0f;
            info.shopWeight = 0f;
            info.eventWeight = 0f;
            return info;
        }

        // Early floors (2-3): no elite, no rest
        if (floor <= 3)
        {
            info.noElite = true;
            info.noRest = true;
            info.eliteWeight = 0f;
            info.restWeight = 0f;
            info.monsterWeight = 50f;
            info.eventWeight = 35f;
            info.shopWeight = 15f;
            return info;
        }

        // Mid floors (4-8): elites start, rest possible
        if (floor <= 8)
        {
            info.monsterWeight = 40f;
            info.eliteWeight = 15f;
            info.eventWeight = 20f;
            info.shopWeight = 10f;
            info.restWeight = 15f;
            return info;
        }

        // Rest-guaranteed floor (roughly 2/3 through)
        int restFloor1 = Mathf.Max(4, totalFloors - 6);
        if (floor == restFloor1)
        {
            info.forceRest = true;
            info.restWeight = 30f;
            info.monsterWeight = 30f;
            info.eliteWeight = 15f;
            info.eventWeight = 10f;
            info.shopWeight = 15f;
            return info;
        }

        // Late floors
        info.monsterWeight = 30f;
        info.eliteWeight = 20f;
        info.eventWeight = 15f;
        info.shopWeight = 10f;
        info.restWeight = 25f;

        // Pre-boss floor: guaranteed rest
        if (floor == totalFloors - 1)
        {
            info.forceRest = true;
            info.restWeight = 40f;
            info.monsterWeight = 25f;
            info.eliteWeight = 10f;
            info.eventWeight = 10f;
            info.shopWeight = 15f;
        }

        return info;
    }

    private static HashSet<LevelType> GetPredecessorTypes(MapGraph graph, MapNodeData node)
    {
        var types = new HashSet<LevelType>();
        foreach (var predId in graph.GetPredecessors(node.nodeId))
        {
            var pred = graph.GetNode(predId);
            if (pred != null) types.Add(pred.nodeType);
        }
        return types;
    }

    private static LevelType GenerateNodeType(FloorInfo info, HashSet<LevelType> predTypes,
        RunRng rng, bool forceRest)
    {
        if (forceRest)
            return LevelType.Rest;

        var candidates = new List<(LevelType type, float weight)>
        {
            (LevelType.NormalBattle, info.monsterWeight),
            (LevelType.RandomEvent, info.eventWeight),
        };

        if (!info.noElite && info.eliteWeight > 0)
            candidates.Add((LevelType.Elite, info.eliteWeight));

        // No consecutive Rest on same path
        if (!info.noRest && info.restWeight > 0 && !predTypes.Contains(LevelType.Rest))
            candidates.Add((LevelType.Rest, info.restWeight));

        // No consecutive Shop on same path
        if (!info.noShop && info.shopWeight > 0 && !predTypes.Contains(LevelType.Shop))
            candidates.Add((LevelType.Shop, info.shopWeight));

        // Weighted random
        float total = 0f;
        foreach (var (_, w) in candidates) total += w;

        float roll = rng.NextFloat(0f, total);
        float cum = 0f;
        foreach (var (type, w) in candidates)
        {
            cum += w;
            if (roll <= cum) return type;
        }

        return LevelType.NormalBattle;
    }

    // ─────────────────────────────────────────────
    //  LevelConfig ID 分配
    // ─────────────────────────────────────────────

    private static void AssignLevelConfigIds(MapGraph graph, ActConfig actConfig)
    {
        if (actConfig == null) return;

        var normalPool = actConfig.normalLevelPool ?? new int[0];
        var elitePool = actConfig.eliteLevelPool ?? new int[0];

        // 全游戏统一混乱：始终洗牌普通/精英池，关卡顺序完全随机（流派倾向只影响抽卡分布）。
        int[] shuffledNormal = (int[])normalPool.Clone();
        int[] shuffledElite = (int[])elitePool.Clone();
        var rng = RogueRuntimeState.RunRng;
        if (rng != null)
        {
            rng.Shuffle(shuffledNormal);
            rng.Shuffle(shuffledElite);
        }

        int shNormalIdx = 0, shEliteIdx = 0;

        for (int floor = 1; floor <= graph.floorCount; floor++)
        {
            foreach (var node in graph.GetFloor(floor))
            {
                if (node.nodeType == LevelType.Boss)
                {
                    node.levelConfigId = actConfig.bossLevelConfigId > 0
                        ? actConfig.bossLevelConfigId : floor;
                }
                else if (node.nodeType == LevelType.NormalBattle)
                {
                    node.levelConfigId = shuffledNormal != null && shuffledNormal.Length > 0
                        ? shuffledNormal[shNormalIdx++ % shuffledNormal.Length] : floor;
                }
                else if (node.nodeType == LevelType.Elite)
                {
                    node.levelConfigId = shuffledElite != null && shuffledElite.Length > 0
                        ? shuffledElite[shEliteIdx++ % shuffledElite.Length] : floor;
                }
                else if (node.nodeType == LevelType.Start)
                {
                    node.levelConfigId = normalPool.Length > 0
                        ? normalPool[0] : 1;
                }
            }
        }
    }
}
