using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地图构建器：从 LevelConfig 读取网格数据，运行时向 Tilemap 铺瓦片，
/// 并创建敌人路线（Path GameObject）。
/// 
/// 挂载在 BattleScene 中的 MapBuilder GameObject 上。
/// </summary>
public class MapBuilder : MonoBehaviour
{
    [Header("Tile 资源（拖入各类型的 Tile）")]
    [Tooltip("Ground 地面的 Tile")]
    public TileBase groundTile;

    [Tooltip("Wall 墙壁的 Tile")]
    public TileBase wallTile;

    [Tooltip("HighGround 高台的 Tile")]
    public TileBase highGroundTile;

    [Header("路线预制体")]
    [Tooltip("WayPoint 预制体（含标识用的 Sprite）")]
    public GameObject wayPointPrefab;

    [Tooltip("运行时 WayPoint 用的 Sprite（不填则 WayPoint 不显示图标）")]
    [SerializeField] private Sprite _runtimeWaypointSprite;

    [Tooltip("刷怪点装饰 Sprite（不填则运行时从场景 Spawner 的 SpriteRenderer 获取）")]
    [SerializeField] private Sprite _spawnPointSprite;

    [Tooltip("Path 父物体预制体/模板（挂有 Path 组件）。若不填则自动创建空物体加 Path 组件。")]
    public GameObject pathTemplatePrefab;

    [Header("路线父节点")]
    [Tooltip("所有路线 GameObject 将创建在此 Transform 下。不填则放根层级。")]
    public Transform pathsParent;

    /// <summary> 当前构建使用的关卡配置 </summary>
    public LevelConfig CurrentConfig { get; private set; }

    /// <summary> 构建出的 Path 组件列表（索引对应路线 0-3） </summary>
    public List<Path> BuiltPaths { get; private set; } = new List<Path>();

    private GridSystem _gridSystem;

    void Awake()
    {
        _gridSystem = GridSystem.Instance;
    }

    /// <summary>
    /// 根据 LevelConfig 构建整个地图。
    /// 调用时机：BattleSceneBootstrap 在 Start 中调用。
    /// </summary>
    public void BuildFromConfig(LevelConfig config)
    {
        if (config == null)
        {
            return;
        }

        CurrentConfig = config;
        ClearExistingMap();
        PaintGridFromConfig(config);
        BuildPathsFromConfig(config);

        // 重建 GridSystem 的 Node 网格（因为 Tilemap 变了）
        if (_gridSystem != null)
        {
            _gridSystem.RebuildGrid();
        }
    }

    /// <summary>
    /// 清除当前地图（清空三个 Tilemap + 销毁旧路线）。
    /// </summary>
    public void ClearExistingMap()
    {
        if (_gridSystem == null)
            _gridSystem = GridSystem.Instance;

        // 清空 Tilemap
        if (_gridSystem != null)
        {
            if (_gridSystem.groundTilemap != null)
                _gridSystem.groundTilemap.ClearAllTiles();
            if (_gridSystem.wallTilemap != null)
                _gridSystem.wallTilemap.ClearAllTiles();
            if (_gridSystem.highGroundTilemap != null)
                _gridSystem.highGroundTilemap.ClearAllTiles();
        }

        // 销毁旧路线
        ClearExistingPaths();
    }

    /// <summary>
    /// 销毁旧的 Path GameObject。
    /// </summary>
    private void ClearExistingPaths()
    {
        Transform parent = pathsParent != null ? pathsParent : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith("Path"))
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
        BuiltPaths.Clear();
    }

    /// <summary>
    /// 根据配置的网格数据向 Tilemap 铺瓦片。
    /// </summary>
    private void PaintGridFromConfig(LevelConfig config)
    {
        if (_gridSystem == null)
        {
            return;
        }

        // 如果当前场景里没有挂入可用 Tile，则回退到默认的三种 Tile 资源，避免地图渲染空白。
        if (groundTile == null)
            groundTile = Resources.Load<TileBase>("Tiles/GroundTile");
        if (wallTile == null)
            wallTile = Resources.Load<TileBase>("Tiles/WallTile");
        if (highGroundTile == null)
            highGroundTile = Resources.Load<TileBase>("Tiles/HighGroundTile");

        if (config.gridData == null || config.gridData.Length == 0)
        {
            return;
        }

        var groundTM = _gridSystem.groundTilemap;
        var wallTM = _gridSystem.wallTilemap;
        var highGroundTM = _gridSystem.highGroundTilemap;

        if (groundTM == null && wallTM == null && highGroundTM == null)
        {
            return;
        }

        // GridSystem 的网格从左下角开始计算
        Vector3 worldBottomLeft = _gridSystem.transform.position
            - Vector3.right * _gridSystem.gridWorldSize.x / 2
            - Vector3.up * _gridSystem.gridWorldSize.y / 2;

        float cellSize = _gridSystem.nodeRadius * 2f; // = 1.0

        for (int y = 0; y < config.gridHeight; y++)
        {
            for (int x = 0; x < config.gridWidth; x++)
            {
                int cellType = config.gridData[y * config.gridWidth + x];
                if (cellType == (int)CellType.Empty) continue;

                // 计算世界坐标
                Vector3 worldPos = worldBottomLeft
                    + Vector3.right * (x * cellSize + _gridSystem.nodeRadius)
                    + Vector3.up * (y * cellSize + _gridSystem.nodeRadius);

                switch ((CellType)cellType)
                {
                    case CellType.Ground:
                        if (groundTM != null && groundTile != null)
                        {
                            Vector3Int cellPos = groundTM.WorldToCell(worldPos);
                            groundTM.SetTile(cellPos, groundTile);
                        }
                        break;

                    case CellType.Wall:
                        if (wallTM != null && wallTile != null)
                        {
                            Vector3Int cellPos = wallTM.WorldToCell(worldPos);
                            wallTM.SetTile(cellPos, wallTile);
                        }
                        break;

                    case CellType.HighGround:
                        if (highGroundTM != null && highGroundTile != null)
                        {
                            Vector3Int cellPos = highGroundTM.WorldToCell(worldPos);
                            highGroundTM.SetTile(cellPos, highGroundTile);
                        }
                        // 高台下面也要有地面（保证 GridSystem walkable 判定正确）
                        if (groundTM != null && groundTile != null)
                        {
                            Vector3Int groundCellPos = groundTM.WorldToCell(worldPos);
                            groundTM.SetTile(groundCellPos, groundTile);
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 根据配置的路点数据创建 Path GameObject。
    /// </summary>
    private void BuildPathsFromConfig(LevelConfig config)
    {
        Transform parent = pathsParent != null ? pathsParent : transform;

        Vector3[][] allPaths = config.GetAllPaths();

        for (int i = 0; i < allPaths.Length; i++)
        {
            var waypoints = allPaths[i];
            if (waypoints == null || waypoints.Length == 0) continue;

            // 创建 Path GameObject
            GameObject pathGo;
            if (pathTemplatePrefab != null)
            {
                pathGo = Instantiate(pathTemplatePrefab, parent);
            }
            else
            {
                pathGo = new GameObject($"Path{i}");
                pathGo.transform.SetParent(parent);
                pathGo.AddComponent<LineRenderer>();
            }

            pathGo.name = $"Path{i}";

            // 确保有 Path 组件
            var pathComp = pathGo.GetComponent<Path>();
            if (pathComp == null)
                pathComp = pathGo.AddComponent<Path>();

            // 如果是 fallback 创建的 Path，配置 LineRenderer
            if (pathTemplatePrefab == null)
            {
                ConfigurePathLineRenderer(pathGo.GetComponent<LineRenderer>(), waypoints);
            }

            // 创建 WayPoint 子物体
            var wayPointGOs = new GameObject[waypoints.Length];
            for (int j = 0; j < waypoints.Length; j++)
            {
                GameObject wp;
                if (wayPointPrefab != null)
                {
                    wp = Instantiate(wayPointPrefab, pathGo.transform);
                }
                else
                {
                    wp = new GameObject($"WayPoint_{j}");
                    wp.transform.SetParent(pathGo.transform);
                    if (_runtimeWaypointSprite != null)
                    {
                        var sr = wp.AddComponent<SpriteRenderer>();
                        sr.sprite = _runtimeWaypointSprite;
                        sr.sortingOrder = 5;
                    }
                }
                wp.name = $"WayPoint_{j}";
                wp.transform.position = waypoints[j];
                wayPointGOs[j] = wp;
            }

            // 赋值给 Path 组件
            pathComp.wayPoint = wayPointGOs;

            // 在路径起点创建刷怪点装饰物
            CreateSpawnPointDecoration(pathGo.transform, waypoints[0], i);

            // 只保留实际使用的路线数
            while (BuiltPaths.Count <= i)
                BuiltPaths.Add(null);
            BuiltPaths[i] = pathComp;
        }
    }

    /// <summary>
    /// 在路径起点（刷怪点）创建装饰物：复用 Spawner 的精灵与缩放，
    /// 纯视觉、无逻辑组件。
    /// </summary>
    private void CreateSpawnPointDecoration(Transform parent, Vector3 position, int pathIndex)
    {
        Sprite sprite = _spawnPointSprite;
        Vector3 decorationScale = new Vector3(0.1f, 0.1f, 0.1f);
        Color decorationColor = Color.white;

        // 未指定 sprite 时从场景中 Spawner 的 SpriteRenderer 获取
        if (sprite == null)
        {
            var spawner = FindFirstObjectByType<Spawner>();
            if (spawner != null)
            {
                var spawnerSR = spawner.GetComponent<SpriteRenderer>();
                if (spawnerSR != null && spawnerSR.sprite != null)
                {
                    sprite = spawnerSR.sprite;
                    decorationScale = spawnerSR.transform.localScale;
                    decorationColor = spawnerSR.color;
                }
            }
        }

        if (sprite == null) return;

        var go = new GameObject($"SpawnPoint_{pathIndex}");
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = decorationScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = decorationColor;
        sr.sortingOrder = 4;
    }

    /// <summary>
    /// 配置路径的 LineRenderer，使其在 Game 视图中可见。
    /// </summary>
    private void ConfigurePathLineRenderer(LineRenderer lr, Vector3[] points)
    {
        if (lr == null || points == null || points.Length < 2) return;

        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;
        lr.useWorldSpace = true;
        lr.sortingOrder = 3;
        lr.loop = false;

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.3f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        lr.colorGradient = gradient;

        var runtimeMat = new Material(Shader.Find("Sprites/Default"));
        if (runtimeMat != null)
        {
            lr.material = runtimeMat;
        }

        lr.enabled = true;
    }

    /// <summary>
    /// 从配置的波次组还原到 Spawner 组件。
    /// </summary>
    public void InjectWavesToSpawner(Spawner spawner, LevelConfig config)
    {
        if (spawner == null || config == null) return;

        // 展平 WaveGroup 为 WaveData 列表
        var waveList = config.FlattenToWaveDataList();

        // 取实际使用的路线（BuiltPaths 可能有空位）
        var validPaths = new List<Path>();
        foreach (var p in BuiltPaths)
            if (p != null) validPaths.Add(p);

        // 使用 Spawner 的公开注入方法
        spawner.InitializeFromConfig(
            waveList.ToArray(),
            validPaths.ToArray(),
            config.specialWaveIndex,
            config.specialEnemyColor
        );
    }
}
