using UnityEngine;
using UnityEngine.Tilemaps;

public class HighGroundTinter : MonoBehaviour
{
    public static HighGroundTinter Instance;
    private Tilemap tilemap;
    private Color originalColor;

    void Awake()
    {
        Instance = this;
        tilemap = GetComponent<Tilemap>();
        if (tilemap != null) originalColor = tilemap.color;
    }

    public void SetToGold()
    {
        if (tilemap != null)
        {
            // 金黄色数值 (R:1, G:0.84, B:0, A:1)
            tilemap.color = new Color(1f, 0.84f, 0f, 1f);
        }
    }

    public void ResetColor()
    {
        if (tilemap != null) tilemap.color = originalColor;
    }
}