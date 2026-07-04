using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapTinter : MonoBehaviour
{
    public static TilemapTinter Instance;
    private Tilemap tilemap;
    private Color originalColor;

    void Awake()
    {
        Instance = this;
        tilemap = GetComponent<Tilemap>();
        // 记录你最初在面板里设置的那个颜色（比如那个黄绿色）
        if (tilemap != null) originalColor = tilemap.color;
    }

    // 变色：直接改写组件面板上的 Color 属性
    public void SetToBlue()
    {
        if (tilemap != null)
        {
            // 天蓝色数值 (R:0.53, G:0.81, B:0.98, A:1)
            // 这行代码执行后，你会看到 Inspector 面板里的 Color 条直接变蓝
            tilemap.color = new Color(0.53f, 0.81f, 0.98f, 1f);
        }
    }

    public void ResetColor()
    {
        if (tilemap != null)
        {
            // 还原面板原本的数值
            tilemap.color = originalColor;
        }
    }
}