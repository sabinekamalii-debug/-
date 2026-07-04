using System.Collections.Generic;
using UnityEngine;

public static class LevelRandomizer
{
    private static Dictionary<int, LevelType> _levelTypes = new Dictionary<int, LevelType>();
    private static bool _initialized = false;
    private static LevelRandomConfig _config;
    private static SimpleLevelRandomConfig _simpleConfig;

    public static void SetConfig(LevelRandomConfig config)
    {
        _config = config;
        _simpleConfig = null;
    }

    public static void SetSimpleConfig(SimpleLevelRandomConfig config)
    {
        _simpleConfig = config;
        _config = null;
        _initialized = false;
    }

    public static void Initialize()
    {
        // 每次都重新生成，确保配置文件的最新值生效
        _initialized = false;

        _levelTypes.Clear();

        for (int i = 1; i <= 16; i++)
        {
            _levelTypes[i] = LevelType.NormalBattle;
        }

        if (_simpleConfig != null)
        {
            ProcessSimpleConfig(_simpleConfig);
        }
        else if (_config != null && _config.ranges != null)
        {
            foreach (var range in _config.ranges)
            {
                ProcessRange(range);
            }
        }
        else
        {
            ProcessDefault();
        }

        _initialized = true;
    }

    static void ProcessSimpleConfig(SimpleLevelRandomConfig config)
    {
        ProcessFullRange(1, 4, config.group1to4);
        ProcessFullRange(5, 8, config.group5to8);
        ProcessFullRange(9, 12, config.group9to12);
        ProcessFullRange(13, 16, config.group13to16);
    }

    static void ProcessFullRange(int start, int end, SimpleLevelRandomConfig.LevelGroupConfig config)
    {
        if (start < 1 || end > 16 || start > end)
            return;

        List<int> positions = new List<int>();
        for (int i = start; i <= end; i++)
        {
            positions.Add(i);
        }

        Shuffle(positions);

        int assigned = 0;

        for (int i = 0; i < config.shopCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Shop;
        }

        for (int i = 0; i < config.eliteCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Elite;
        }

        for (int i = 0; i < config.bossCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Boss;
        }

        for (int i = 0; i < config.randomEventCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.RandomEvent;
        }

        for (int i = 0; i < config.restCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Rest;
        }
    }

    static void ProcessRange(LevelRandomConfig.LevelRange range)
    {
        if (range.startLevel < 1 || range.endLevel > 16 || range.startLevel > range.endLevel)
            return;

        List<int> positions = new List<int>();
        for (int i = range.startLevel; i <= range.endLevel; i++)
        {
            positions.Add(i);
        }

        Shuffle(positions);

        int assigned = 0;

        for (int i = 0; i < range.shopCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Shop;
        }

        for (int i = 0; i < range.eliteCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Elite;
        }

        for (int i = 0; i < range.bossCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Boss;
        }

        for (int i = 0; i < range.randomEventCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.RandomEvent;
        }

        for (int i = 0; i < range.restCount && assigned < positions.Count; i++, assigned++)
        {
            _levelTypes[positions[assigned]] = LevelType.Rest;
        }
    }

    static void ProcessDefault()
    {
        var config1 = new SimpleLevelRandomConfig.LevelGroupConfig { shopCount = 1 };
        var config2 = new SimpleLevelRandomConfig.LevelGroupConfig { eliteCount = 1 };
        var config3 = new SimpleLevelRandomConfig.LevelGroupConfig { shopCount = 1, eliteCount = 1 };
        var config4 = new SimpleLevelRandomConfig.LevelGroupConfig { shopCount = 1, eliteCount = 1, bossCount = 1 };

        ProcessFullRange(1, 4, config1);
        ProcessFullRange(5, 8, config2);
        ProcessFullRange(9, 12, config3);
        ProcessFullRange(13, 16, config4);
    }

    public static LevelType GetLevelType(int levelNum)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (_levelTypes.ContainsKey(levelNum))
        {
            return _levelTypes[levelNum];
        }

        return LevelType.NormalBattle;
    }

    public static void Reset()
    {
        _initialized = false;
        _levelTypes.Clear();
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
