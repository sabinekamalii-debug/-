using UnityEngine;

public enum GambleMode { None, Buff, Debuff }

public static class LevelRunModifiers
{
    public static float EnemyHpMultiplier { get; private set; } = 1f;
    public static float EnemySpeedMultiplier { get; private set; } = 1f;

    // 赌博卡（spc_gamble）：每场战斗前独立掷骰，决定本关敌人与奖励的增减
    public static GambleMode GambleResult { get; private set; } = GambleMode.None;

    public static void Apply(LevelConfig config)
    {
        if (config == null) { Reset(); return; }
        EnemyHpMultiplier = config.enemyHpMultiplier;
        EnemySpeedMultiplier = config.enemySpeedMultiplier;
        if (Mathf.Approximately(EnemyHpMultiplier, 0f)) EnemyHpMultiplier = 1f;
        if (Mathf.Approximately(EnemySpeedMultiplier, 0f)) EnemySpeedMultiplier = 1f;
    }

    public static void Reset()
    {
        EnemyHpMultiplier = 1f;
        EnemySpeedMultiplier = 1f;
        GambleResult = GambleMode.None;
    }

    /// <summary> 赌博卡：每关开始掷骰，写入本关结果并覆盖敌人血量倍率。 </summary>
    public static void RollGambleIfActive(bool active)
    {
        if (!active)
        {
            GambleResult = GambleMode.None;
            return;
        }
        bool buff = Random.value < 0.5f;
        GambleResult = buff ? GambleMode.Buff : GambleMode.Debuff;
        EnemyHpMultiplier = buff ? 1.5f : 0.5f;
    }
}