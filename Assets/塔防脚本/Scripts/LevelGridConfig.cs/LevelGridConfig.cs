using UnityEngine;

[CreateAssetMenu(fileName = "LevelGridConfig", menuName = "Levels/Level Grid Config")]
public class LevelGridConfig : ScriptableObject
{
    [Header("网格尺寸（列 = 宽，行 = 高)")]
    public int width = 16;
    public int height = 10;

    [Tooltip("每一行一个字符串，从上到下。长度需要等于 width。\n. = 空，G = 地面，W = 墙，H = 高台。")]
    public string[] rows;
}
