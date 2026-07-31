using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RunModifierConfig", menuName = "魔塔/混合模式修饰配置", order = 101)]
public class RunModifierConfig : ScriptableObject
{
    [Header("混合模式阈值")]
    [Tooltip("前 N 关强制使用固定 LevelConfig，不叠加随机修饰")]
    public int fixedCutoff = 5;

    [Header("敌人血量倍率区间")]
    [Tooltip("最小倍率")]
    public float enemyHpMin = 1.0f;
    [Tooltip("最大倍率")]
    public float enemyHpMax = 1.3f;

    [Header("敌人移速倍率区间")]
    [Tooltip("最小倍率")]
    public float enemySpeedMin = 1.0f;
    [Tooltip("最大倍率")]
    public float enemySpeedMax = 1.2f;

    [Header("初始 DP 抖动区间")]
    [Tooltip("最小偏移（相对源值）")]
    public int startDPOffsetMin = -2;
    [Tooltip("最大偏移（相对源值）")]
    public int startDPOffsetMax = 3;

    [Header("守护点血量抖动区间")]
    [Tooltip("最小偏移")]
    public int maxLifePointOffsetMin = -1;
    [Tooltip("最大偏移")]
    public int maxLifePointOffsetMax = 2;

    [Header("敌人池替换")]
    [Tooltip("是否启用敌人池替换（从 availableEnemyTypes 按权重抽取替换波次中的敌人类型）")]
    public bool enableEnemyPoolSwap = true;

    [Tooltip("每个波次条目被替换的概率")]
    [Range(0f, 1f)]
    public float enemySwapChance = 0.3f;

    [Header("难度随层数递增")]
    [Tooltip("每层额外 HP 倍率增量（叠加到区间上限）")]
    public float hpGrowthPerStage = 0.02f;
    [Tooltip("每层额外速度倍率增量")]
    public float speedGrowthPerStage = 0.01f;

    [Header("随机模式")]
    [Tooltip("随机模式下固定关卡数（0 = 全部关卡都随机）")]
    public int randomModeFixedCutoff = 0;

    public float GetHpMax(int stage)
    {
        return enemyHpMax + Mathf.Max(0, stage - 1) * hpGrowthPerStage;
    }

    public float GetSpeedMax(int stage)
    {
        return enemySpeedMax + Mathf.Max(0, stage - 1) * speedGrowthPerStage;
    }

    public int GetFixedCutoff(GameMode mode)
    {
        return mode switch
        {
            GameMode.Fixed => int.MaxValue,
            GameMode.Hybrid => fixedCutoff,
            GameMode.Random => randomModeFixedCutoff,
            _ => fixedCutoff,
        };
    }
}