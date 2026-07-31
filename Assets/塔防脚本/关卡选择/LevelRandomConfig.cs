using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelRandomConfig", menuName = "魔塔/关卡随机配置", order = 2)]
public class LevelRandomConfig : ScriptableObject
{
    [Serializable]
    public class LevelRange
    {
        [Header("关卡范围")]
        [Tooltip("起始关卡（包含）")]
        public int startLevel = 1;

        [Tooltip("结束关卡（包含）")]
        public int endLevel = 3;

        [Space(5)]
        [Header("🛒 商店")]
        [Range(0, 4)]
        [Tooltip("这个范围内有多少个商店")]
        public int shopCount = 0;

        [Header("⚔️ 精英")]
        [Range(0, 4)]
        [Tooltip("这个范围内有多少个精英关卡")]
        public int eliteCount = 0;

        [Header("👹 Boss")]
        [Range(0, 4)]
        [Tooltip("这个范围内有多少个Boss关卡")]
        public int bossCount = 0;

        [Header("❓ 随机事件")]
        [Range(0, 4)]
        [Tooltip("这个范围内有多少个随机事件")]
        public int randomEventCount = 0;

        [Header("🏕️ 休息点")]
        [Range(0, 4)]
        [Tooltip("这个范围内有多少个休息点")]
        public int restCount = 0;
    }

    [Header("关卡随机配置")]
    [Tooltip("每一段配置一个范围，灵活支持任意分组")]
    public List<LevelRange> ranges = new List<LevelRange>();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (ranges == null) return;
        foreach (var range in ranges)
        {
            if (range == null) continue;
            int rangeSize = Mathf.Max(1, range.endLevel - range.startLevel + 1);

            int used = 0;
            used = ClampField(ref range.shopCount, rangeSize, used);
            used = ClampField(ref range.eliteCount, rangeSize, used);
            used = ClampField(ref range.bossCount, rangeSize, used);
            used = ClampField(ref range.randomEventCount, rangeSize, used);
            used = ClampField(ref range.restCount, rangeSize, used);
        }
    }

    static int ClampField(ref int value, int rangeSize, int used)
    {
        int max = rangeSize - used;
        if (value > max) value = max;
        if (value < 0) value = 0;
        return used + value;
    }
#endif
}
