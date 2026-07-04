using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SimpleLevelConfig", menuName = "魔塔/简单关卡配置", order = 3)]
public class SimpleLevelRandomConfig : ScriptableObject
{
    [Serializable]
    public class LevelGroupConfig
    {
        [Header("商店")]
        [Range(0, 4)]
        public int shopCount = 0;

        [Header("精英")]
        [Range(0, 4)]
        public int eliteCount = 0;

        [Header("Boss")]
        [Range(0, 4)]
        public int bossCount = 0;

        [Header("随机事件")]
        [Range(0, 4)]
        public int randomEventCount = 0;

        [Header("休息点")]
        [Range(0, 4)]
        public int restCount = 0;

        public int GetTotal()
        {
            return shopCount + eliteCount + bossCount + randomEventCount + restCount;
        }
    }

    [Header("第1-4关")]
    public LevelGroupConfig group1to4 = new LevelGroupConfig { shopCount = 1 };

    [Header("第5-8关")]
    public LevelGroupConfig group5to8 = new LevelGroupConfig { eliteCount = 1 };

    [Header("第9-12关")]
    public LevelGroupConfig group9to12 = new LevelGroupConfig { shopCount = 1, eliteCount = 1 };

    [Header("第13-16关")]
    public LevelGroupConfig group13to16 = new LevelGroupConfig { shopCount = 1, eliteCount = 1, bossCount = 1 };

    public List<LevelRangeFull> ConvertToRanges()
    {
        return new List<LevelRangeFull>
        {
            new LevelRangeFull { startLevel = 1, endLevel = 4, config = group1to4 },
            new LevelRangeFull { startLevel = 5, endLevel = 8, config = group5to8 },
            new LevelRangeFull { startLevel = 9, endLevel = 12, config = group9to12 },
            new LevelRangeFull { startLevel = 13, endLevel = 16, config = group13to16 }
        };
    }
}

public class LevelRangeFull
{
    public int startLevel;
    public int endLevel;
    public SimpleLevelRandomConfig.LevelGroupConfig config;
}
