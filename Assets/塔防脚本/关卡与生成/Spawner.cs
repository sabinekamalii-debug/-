using UnityEngine;
using System.Collections.Generic;

public class Spawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyPoolEntry
    {
        public EnemyType enemyType;
        public ObjectPooler pool;
    }

    [Header("Global UI")]
    public UIController ui;

    [Header("Waves")]
    public WaveData[] waves;

    [Header("Paths")]
    [Tooltip("Assign path objects in order. WaveData.pathIndex selects which path to use.")]
    public Path[] paths;

    [Header("特殊生成配置")]
    [Tooltip("如果大于0，则只有第 N 波（根据WaveData的waveNumberDisplay）生成的第一个怪物会被染色，其他波次不变色。")]
    public int specialWaveIndex = 2;
    [Tooltip("要染的颜色（默认为紫色）")]
    public Color specialEnemyColor = new Color(0.6f, 0f, 1f, 1f);

    [Header("敌人池（配 EnemyType 与 ObjectPooler 一一对应）")]
    public List<EnemyPoolEntry> enemyPools = new List<EnemyPoolEntry>();

    private int currentWaveIndex = 0;
    private int enemiesSpawnedInWave = 0;
    private float spawnTimer;
    
    // 用于跟踪特殊波次是否已经染色过
    private bool hasSpecialWaveColored = false;

    private int totalEnemiesCalculated = 0;
    private int totalSpawnedSoFar = 0;
    private int maxWaveNum = 0;

    public bool AllWavesSpawned => waves != null && currentWaveIndex >= waves.Length;

    [Header("Spawner Health")]
    private SpawnerHealth healthScript;

    void Start()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1 && gameObject.layer != enemyLayer)
            gameObject.layer = enemyLayer;

        Enemy2.ResetActiveEnemyCountForNewLevel();
        CalculateTotals();
        UpdateAllUI();
        healthScript = GetComponent<SpawnerHealth>();

        if (waves != null && waves.Length > 0)
            spawnTimer = waves[0].delayBeforeWave;
    }

    /// <summary>
    /// 从 LevelConfig 动态注入波次和路线数据（替代 Inspector 拖拽）。
    /// 由 BattleSceneBootstrap / MapBuilder 在运行时调用。
    /// </summary>
    public void InitializeFromConfig(WaveData[] waveArray, Path[] pathArray,
        int specialWaveIdx = 0, Color? specialColor = null)
    {
        waves = waveArray;
        paths = pathArray;
        specialWaveIndex = specialWaveIdx;
        if (specialColor.HasValue)
            specialEnemyColor = specialColor.Value;

        // 重置状态
        currentWaveIndex = 0;
        enemiesSpawnedInWave = 0;
        hasSpecialWaveColored = false;
        totalSpawnedSoFar = 0;

        CalculateTotals();
        UpdateAllUI();

        if (waves != null && waves.Length > 0)
            spawnTimer = waves[0].delayBeforeWave;
    }

    void Update()
    {
        if (waves == null || currentWaveIndex >= waves.Length) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f) SpawnEnemy();
    }

    void SpawnEnemy()
    {
        WaveData currentWave = waves[currentWaveIndex];
        GameObject spawnedObject = null;

        var entry = enemyPools.Find(e => e.enemyType == currentWave.enemyType);
        if (entry != null)
            spawnedObject = entry.pool != null ? entry.pool.GetPooledObject() : null;

        if (spawnedObject == null) return;

        spawnedObject.transform.position = transform.position;

        Enemy2 enemyScript = spawnedObject.GetComponent<Enemy2>();
        if (enemyScript != null && paths != null && paths.Length > 0)
        {
            int idx = currentWave.pathIndex;
            if (idx >= 0 && idx < paths.Length && paths[idx] != null)
                enemyScript.SetPath(paths[idx]);
        }

        spawnedObject.SetActive(true);

        if (healthScript != null && healthScript.isBroken && enemyScript != null)
            enemyScript.ApplySpawnDebuff(0.5f);

        // 检查是否是特殊波次的第一个敌人（在增加计数之前检查）
        bool isFirstEnemyOfSpecialWave = (specialWaveIndex > 0 && 
                                          currentWave.waveNumberDisplay == specialWaveIndex && 
                                          enemiesSpawnedInWave == 0);

        enemiesSpawnedInWave++;
        totalSpawnedSoFar++;

        // 如果当前生成的怪物属于我们指定的特殊波次，并且是第一个敌人
        if (isFirstEnemyOfSpecialWave && !hasSpecialWaveColored)
        {
            SpriteRenderer[] srs = spawnedObject.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null && srs.Length > 0)
            {
                int coloredCount = 0;
                foreach (var sr in srs)
                {
                    // 忽略可能作为血条背景等的无用渲染器（可以根据需要优化，这里简单全染）
                    sr.color = specialEnemyColor;
                    coloredCount++;
                }
                // 标记已经染色过，本波次后续敌人不再染色
                hasSpecialWaveColored = true;
            }
            else
            {
            }

            // 【关键】告知怪物它是紫色天赋怪，死后会触发抽卡
            Enemy2 e2 = spawnedObject.GetComponent<Enemy2>();
            if (e2 != null)
            {
                e2.isPurpleTalentEnemy = true;
            }
        }

        if (ui != null) ui.UpdateEnemyUI(totalSpawnedSoFar, totalEnemiesCalculated);

        if (enemiesSpawnedInWave >= currentWave.enemiesPerWave)
        {
            currentWaveIndex++;
            enemiesSpawnedInWave = 0;
            // 注意：hasSpecialWaveColored 不应该在这里重置
            // 因为整个关卡中只需要染色一次（指定波次的第一个敌人）
            if (currentWaveIndex < waves.Length)
                spawnTimer = waves[currentWaveIndex].delayBeforeWave;
        }
        else
        {
            spawnTimer = currentWave.spawnInterval;
        }
    }

    void CalculateTotals()
    {
        totalEnemiesCalculated = 0;
        maxWaveNum = 0;
        if (waves == null) return;

        foreach (var w in waves)
        {
            if (w == null) continue;
            totalEnemiesCalculated += w.enemiesPerWave;
            if (w.waveNumberDisplay > maxWaveNum)
                maxWaveNum = w.waveNumberDisplay;
        }
    }

    void UpdateAllUI()
    {
        if (ui == null) return;
        ui.UpdateEnemyUI(0, totalEnemiesCalculated);
    }
}
