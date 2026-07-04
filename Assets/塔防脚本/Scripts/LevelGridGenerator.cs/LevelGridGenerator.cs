using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelGridGenerator : MonoBehaviour
{
    [Header("关卡配置")]
    public LevelGridConfig config;

    [Header("Tilemap 引用")]
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public Tilemap highGroundTilemap;

    [Header("Tile 资源（需要你在 Inspector 拖拽")]
    public TileBase groundTile;
    public TileBase wallTile;
    public TileBase highGroundTile;

    [Header("生成偏移（左下角世界格坐标）")]
    public Vector3Int origin = Vector3Int.zero;

    [ContextMenu("Generate From Config")]
    public void Generate()
    {
        if (config == null) return;
        if (config.rows == null || config.rows.Length == 0) return;
        if (config.rows.Length != config.height) config.height = config.rows.Length;

        // 清空旧地图
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        if (highGroundTilemap != null) highGroundTilemap.ClearAllTiles();

        for (int y = 0; y < config.height; y++)
        {
            string row = config.rows[y] ?? string.Empty;

            for (int x = 0; x < config.width; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                Vector3Int cellPos = origin + new Vector3Int(x, config.height - 1 - y, 0);

                switch (c)
                {
                    case 'G':
                        if (groundTilemap != null && groundTile != null)
                            groundTilemap.SetTile(cellPos, groundTile);
                        break;
                    case 'W':
                        if (wallTilemap != null && wallTile != null)
                            wallTilemap.SetTile(cellPos, wallTile);
                        break;
                    case 'H':
                        if (highGroundTilemap != null && highGroundTile != null)
                            highGroundTilemap.SetTile(cellPos, highGroundTile);
                        break;
                    default:
                        // '.' 或未知字符：保持为空
                        break;
                }
            }
        }
    }
}
