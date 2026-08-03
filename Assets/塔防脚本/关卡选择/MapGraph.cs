using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 地图节点数据：StS风格分叉路径图中的一个节点。
/// </summary>
[Serializable]
public class MapNodeData
{
    public int nodeId;
    public int floor;          // 0=起点, floorCount=Boss层
    public int column;         // 0 ~ maxColumns-1
    public LevelType nodeType;
    public int levelConfigId;  // 战斗节点：加载哪个 LevelConfig
    public List<int> nextNodeIds = new List<int>();

    public MapNodeData(int id, int flr, int col, LevelType type)
    {
        nodeId = id;
        floor = flr;
        column = col;
        nodeType = type;
        levelConfigId = 0;
    }
}

/// <summary>
/// 地图图：StS风格分叉路径。由 StSMapGenerator 生成。
/// 使用种子保证同一局生成相同的地图。
/// </summary>
[Serializable]
public class MapGraph
{
    public int seed;
    public int floorCount;
    public int maxColumns;
    public List<MapNodeData> allNodes = new List<MapNodeData>();

    private Dictionary<int, MapNodeData> _nodeLookup;
    private Dictionary<int, List<int>> _floorLookup;
    private Dictionary<int, List<int>> _predecessorLookup;
    private bool _lookupBuilt;

    public void BuildLookups()
    {
        _nodeLookup = new Dictionary<int, MapNodeData>();
        _floorLookup = new Dictionary<int, List<int>>();
        _predecessorLookup = new Dictionary<int, List<int>>();

        foreach (var node in allNodes)
        {
            _nodeLookup[node.nodeId] = node;

            if (!_floorLookup.ContainsKey(node.floor))
                _floorLookup[node.floor] = new List<int>();
            _floorLookup[node.floor].Add(node.nodeId);

            if (!_predecessorLookup.ContainsKey(node.nodeId))
                _predecessorLookup[node.nodeId] = new List<int>();
        }

        foreach (var node in allNodes)
        {
            foreach (var nextId in node.nextNodeIds)
            {
                if (!_predecessorLookup.ContainsKey(nextId))
                    _predecessorLookup[nextId] = new List<int>();
                _predecessorLookup[nextId].Add(node.nodeId);
            }
        }

        _lookupBuilt = true;
    }

    public MapNodeData GetNode(int nodeId)
    {
        if (!_lookupBuilt) BuildLookups();
        return _nodeLookup.TryGetValue(nodeId, out var n) ? n : null;
    }

    public List<MapNodeData> GetFloor(int floor)
    {
        if (!_lookupBuilt) BuildLookups();
        if (!_floorLookup.TryGetValue(floor, out var ids)) return new List<MapNodeData>();
        return ids.Select(id => _nodeLookup[id]).ToList();
    }

    public List<int> GetPredecessors(int nodeId)
    {
        if (!_lookupBuilt) BuildLookups();
        return _predecessorLookup.TryGetValue(nodeId, out var preds) ? preds : new List<int>();
    }

    public MapNodeData GetStartNode()
    {
        return GetFloor(0).FirstOrDefault();
    }

    public MapNodeData GetBossNode()
    {
        return GetFloor(floorCount).FirstOrDefault();
    }

    public MapNodeData FindNode(int floor, int column)
    {
        if (!_lookupBuilt) BuildLookups();
        foreach (var node in allNodes)
        {
            if (node.floor == floor && node.column == column)
                return node;
        }
        return null;
    }
}
