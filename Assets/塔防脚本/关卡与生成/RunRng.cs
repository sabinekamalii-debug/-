using UnityEngine;

public class RunRng
{
    private System.Random _rng;

    public int Seed { get; }

    public RunRng(int seed)
    {
        Seed = seed;
        _rng = new System.Random(seed);
    }

    /// <summary>
    /// 从 RunSeed 和关卡号派生关卡专属子种子，确保同一关卡跨会话产生相同修饰序列。
    /// 使用 Knuth 乘法散列常数 0x9E3779B1。
    /// </summary>
    public static int DeriveLevelSeed(int runSeed, int levelNumber)
    {
        unchecked { return runSeed ^ (int)(levelNumber * 2654435761); }
    }

    public float NextFloat()
    {
        return (float)_rng.NextDouble();
    }

    public float NextFloat(float min, float max)
    {
        return min + (max - min) * (float)_rng.NextDouble();
    }

    public int NextInt(int min, int max)
    {
        return _rng.Next(min, max + 1);
    }

    public bool NextBool(float probability = 0.5f)
    {
        return (float)_rng.NextDouble() < probability;
    }

    public T NextWeighted<T>(T[] items, float[] weights)
    {
        if (items == null || items.Length == 0) return default;
        if (weights == null || weights.Length != items.Length)
            return items[_rng.Next(0, items.Length)];

        float total = 0f;
        foreach (var w in weights) total += Mathf.Max(0f, w);
        if (total <= 0f) return items[0];

        float roll = (float)_rng.NextDouble() * total;
        float cumulative = 0f;
        for (int i = 0; i < items.Length; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll < cumulative) return items[i];
        }
        return items[items.Length - 1];
    }

    public void Shuffle<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}