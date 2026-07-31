using UnityEngine;

public static class LevelRunModifiers
{
    public static float EnemyHpMultiplier { get; private set; } = 1f;
    public static float EnemySpeedMultiplier { get; private set; } = 1f;

    public static void Apply(LevelConfig config)
    {
        if (config == null) { Reset(); return; }
        EnemyHpMultiplier = config.enemyHpMultiplier;
        EnemySpeedMultiplier = config.enemySpeedMultiplier;
        if (Mathf.Approximately(EnemyHpMultiplier, 0f)) EnemyHpMultiplier = 1f;
        if (Mathf.Approximately(EnemySpeedMultiplier, 0f)) EnemySpeedMultiplier = 1f;
        Debug.Log($"[LevelRunModifiers] HP×{EnemyHpMultiplier:F2} SPD×{EnemySpeedMultiplier:F2}");
    }

    public static void Reset()
    {
        EnemyHpMultiplier = 1f;
        EnemySpeedMultiplier = 1f;
    }
}