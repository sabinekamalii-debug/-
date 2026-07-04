using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridSystem : MonoBehaviour
{
    
    public static GridSystem Instance;

    [Header("网格基础设置")]
    public float nodeRadius = 0.5f;
    public Vector2 gridWorldSize = new Vector2(20, 12); // 确保是偶数对齐地砖

    [Header("图层引用")]
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public Tilemap highGroundTilemap;
    // ================== 新增：地图染色设置 ==================
    [Header("地图染色")]
    public Color normalColor = Color.white; // 默认原色（纯白代表不染色）
    public Color goldColor = new Color(1f, 0.84f, 0f, 0.8f); // 金色（可自己调透明度）
    [Header("部署中心设置")]
    public Transform defensePoint; // 【新增】必须把守护点拖进这里，作为部署的中心！

    Node[,] grid;
    float nodeDiameter;
    int gridSizeX, gridSizeY;

    void Awake()
    {
        Instance = this;
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }
// 【全新功能】把整块地图直接染成金色！
    // =======================================================
    public void HighlightEntireLayer(OperatorData.OperatorType opType)
    {
        
        ResetMapHighlight(); // 先恢复原色

        if (opType == OperatorData.OperatorType.Melee && groundTilemap != null)
        {
            groundTilemap.color = goldColor;
        }
        else if (opType == OperatorData.OperatorType.Ranged && highGroundTilemap != null)
        {
            highGroundTilemap.color = goldColor;
        }
        else
        {
        }
    }

    public void ResetMapHighlight()
    {
        if (groundTilemap != null) groundTilemap.color = normalColor;
        if (highGroundTilemap != null) highGroundTilemap.color = normalColor;
    }
    // 【部署高光逻辑】只返回：在部署范围内 + 可以合法部署 的格子
    // =======================================================
    public List<Vector3> GetAllDeployablePositions(OperatorData opData)
    {
        List<Vector3> positions = new List<Vector3>();
        
        // 防错：没拖守护点就不亮
        if (grid == null || defensePoint == null) return positions;

        // 获取守护点所在的网格节点，作为部署的圆心
        Node centerNode = NodeFromWorldPoint(defensePoint.position);
        int radius = (int)opData.deployRadius; 

        foreach (Node n in grid)
        {
            // 规则 1：格子上有其他人了？不亮！
            if (n.isOccupied) continue;

            // 规则 2：计算这个格子到守护点的距离。如果超出了干员的部署半径？不亮！
            // 这里用的是曼哈顿距离（菱形十字范围），如果你要正方形范围，可以自己改。
            int distanceToCenter = GetDistance(centerNode, n);
            if (distanceToCenter > radius) continue;

            // 获取该网格对应的图层信息
            Vector3Int cellPos = groundTilemap.WorldToCell(n.worldPosition);
            bool hasGround = groundTilemap.HasTile(cellPos);
            bool hasWall = wallTilemap.HasTile(cellPos);
            bool hasHighGround = highGroundTilemap.HasTile(cellPos);

            // 规则 3：墙壁绝对不能部署？不亮！
            if (hasWall) continue;

            // 规则 4：区分职业验证地形
            if (opData.opType == OperatorData.OperatorType.Melee && hasGround && !hasHighGround)
            {
                // 近战：必须是平地，加入高亮名单
                positions.Add(n.worldPosition);
            }
            else if (opData.opType == OperatorData.OperatorType.Ranged && hasHighGround)
            {
                // 远程：必须是高台，加入高亮名单，并精确对齐高台中心
                positions.Add(highGroundTilemap.GetCellCenterWorld(cellPos));
            }
        }
        return positions;
    }

    // =======================================================
    // 【移动高光逻辑】只返回：在移动步数范围内 + 可以合法行走的格子
    // =======================================================
    public List<Vector3> GetMovablePositions(Vector3 startPos, int moveRange)
    {
        List<Vector3> positions = new List<Vector3>();
        Node startNode = NodeFromWorldPoint(startPos);
        if (startNode == null) return positions;

        foreach (Node n in grid)
        {
            // 规则 1：不可行走（比如是墙、高台）或者被别人占领了？不亮！
            if (!n.walkable || n.isOccupied) continue;

            // 规则 2：如果在最大步数范围内，且不是自己脚下这个格子，加入高亮名单！
            int distance = GetDistance(startNode, n);
            if (distance <= moveRange && distance > 0) 
            {
                positions.Add(n.worldPosition);
            }
        }
        return positions;
    }

    // =======================================================
    // 以下全部是你原版最完美的 A* 寻路和网格生成逻辑，绝对一字不改！
    // =======================================================

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Vector3Int startCell = groundTilemap.WorldToCell(startPos);
        Vector3Int targetCell = groundTilemap.WorldToCell(targetPos);

        if (highGroundTilemap.HasTile(targetCell) || wallTilemap.HasTile(targetCell) || !groundTilemap.HasTile(targetCell))
        {
            targetCell = GetClosestGroundNeighbor(targetCell, startCell);
        }

        Vector3 finalTargetWorld = groundTilemap.GetCellCenterWorld(targetCell);
        return CalculatePath(startPos, finalTargetWorld);
    }

    private Vector3Int GetClosestGroundNeighbor(Vector3Int target, Vector3Int start)
    {
        Vector3Int bestCell = target;
        float minDst = float.MaxValue;
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        Vector3 startWorld = groundTilemap.GetCellCenterWorld(start);

        foreach (var dir in directions)
        {
            Vector3Int neighbor = target + dir;
            bool isGround = groundTilemap.HasTile(neighbor);
            bool isWall = wallTilemap.HasTile(neighbor);
            bool isHigh = highGroundTilemap.HasTile(neighbor);

            if (isGround && !isWall && !isHigh)
            {
                float dst = Vector3.Distance(groundTilemap.GetCellCenterWorld(neighbor), startWorld);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestCell = neighbor;
                }
            }
        }
        return bestCell;
    }

    List<Vector3> CalculatePath(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = NodeFromWorldPoint(startPos);
        Node targetNode = NodeFromWorldPoint(targetPos);

        if (startNode == null || targetNode == null || !targetNode.walkable) return null;

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost)
                    currentNode = openSet[i];
            }
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode) return RetracePath(startNode, targetNode);

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.walkable || closedSet.Contains(neighbour)) continue;
                int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newMovementCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = currentNode;
                    if (!openSet.Contains(neighbour)) openSet.Add(neighbour);
                }
            }
        }
        return null;
    }

    List<Vector3> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        List<Vector3> waypoints = new List<Vector3>();
        foreach (Node n in path) waypoints.Add(n.worldPosition);
        return waypoints;
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.up * (y * nodeDiameter + nodeRadius);
                Vector3Int cellPos = groundTilemap.WorldToCell(worldPoint);

                bool hasGround = groundTilemap.HasTile(cellPos);
                bool hasWall = wallTilemap.HasTile(cellPos);
                bool hasHighGround = highGroundTilemap.HasTile(cellPos);
                
                bool walkable = hasGround && !hasWall && !hasHighGround;

                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.y + gridWorldSize.y / 2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);
        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        if (x >= gridSizeX) x = gridSizeX - 1;
        if (y >= gridSizeY) y = gridSizeY - 1;
        return grid[x, y];
    }

    List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();
        int[] xDirs = { 0, 0, 1, -1 };
        int[] yDirs = { 1, -1, 0, 0 };
        for (int i = 0; i < 4; i++)
        {
            int checkX = node.gridX + xDirs[i];
            int checkY = node.gridY + yDirs[i];
            if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                neighbours.Add(grid[checkX, checkY]);
        }
        return neighbours;
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);
        return dstX + dstY;
    }

    public void ShowHighGroundHighlights() { }
    public bool IsCellOccupied(Vector3 worldPos) { Node node = NodeFromWorldPoint(worldPos); return node != null && node.isOccupied; }
    public void SetCellOccupied(Vector3 worldPos, bool occupied) { Node node = NodeFromWorldPoint(worldPos); if (node != null) node.isOccupied = occupied; }
    public Vector3 GetCellCenterWorld(Vector3 worldPos) { if (groundTilemap == null) return worldPos; return groundTilemap.GetCellCenterWorld(groundTilemap.WorldToCell(worldPos)); }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));
        if (grid != null)
        {
            foreach (Node n in grid)
            {
                if (n.isOccupied) Gizmos.color = new Color(1, 0, 0, 0.5f);
                else Gizmos.color = (n.walkable) ? new Color(1, 1, 1, 0.3f) : new Color(0, 0, 0, 0.3f);
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - .1f));
            }
        }
    }
}

public class Node
{
    public bool walkable;
    public bool isOccupied; 
    public Vector3 worldPosition;
    public int gridX, gridY;
    public int gCost, hCost;
    public Node parent;

    public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY)
    {
        walkable = _walkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridY = _gridY;
        isOccupied = false; 
    }
    public int fCost { get { return gCost + hCost; } }
}