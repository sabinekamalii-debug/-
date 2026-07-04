using UnityEngine;
using UnityEngine.Serialization;

public class Spawner : MonoBehaviour
{
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

    [Header("Pools")]
    [FormerlySerializedAs("enemypool")] public ObjectPooler enemyPool;
    [FormerlySerializedAs("smallbosspool")] public ObjectPooler smallBossPool;
    [FormerlySerializedAs("bigbosspool")] public ObjectPooler bigBossPool;
    [FormerlySerializedAs("gebulinpool")] public ObjectPooler goblinPool;
    [FormerlySerializedAs("kuloupool")] public ObjectPooler skeletonPool;
    [FormerlySerializedAs("smallkuloupool")] public ObjectPooler smallSkeletonPool;
    [FormerlySerializedAs("????")] public ObjectPooler darkKingPool;
    [FormerlySerializedAs("??????")] public ObjectPooler darkKingClonePool;
    [FormerlySerializedAs("????")] public ObjectPooler fireKingPool;
    [FormerlySerializedAs("???")] public ObjectPooler stoneMonsterPool;
    [FormerlySerializedAs("??")] public ObjectPooler beePool;
    [FormerlySerializedAs("???")] public ObjectPooler wanLuDuoPool;

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

    private const int TYPE_ENEMY = 0;
    private const int TYPE_SMALL_BOSS = 1;
    private const int TYPE_BIG_BOSS = 2;
    private const int TYPE_GOBLIN = 3;
    private const int TYPE_SKELETON = 4;
    private const int TYPE_SMALL_SKELETON = 5;
    private const int TYPE_DARK_KING = 6;
    private const int TYPE_DARK_KING_CLONE = 7;
    private const int TYPE_FIRE_KING = 8;
    private const int TYPE_STONE_MONSTER = 9;
    private const int TYPE_BEE = 10;
    private const int TYPE_WANLUDUO = 11;

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
        int requestedType = (int)currentWave.enemyType;

        switch (requestedType)
        {
            case TYPE_ENEMY: spawnedObject = enemyPool != null ? enemyPool.GetPooledObject() : null; break;
            case TYPE_SMALL_BOSS: spawnedObject = smallBossPool != null ? smallBossPool.GetPooledObject() : null; break;
            case TYPE_BIG_BOSS: spawnedObject = bigBossPool != null ? bigBossPool.GetPooledObject() : null; break;
            case TYPE_GOBLIN: spawnedObject = goblinPool != null ? goblinPool.GetPooledObject() : null; break;
            case TYPE_SKELETON: spawnedObject = skeletonPool != null ? skeletonPool.GetPooledObject() : null; break;
            case TYPE_SMALL_SKELETON: spawnedObject = smallSkeletonPool != null ? smallSkeletonPool.GetPooledObject() : null; break;
            case TYPE_DARK_KING: spawnedObject = darkKingPool != null ? darkKingPool.GetPooledObject() : null; break;
            case TYPE_DARK_KING_CLONE: spawnedObject = darkKingClonePool != null ? darkKingClonePool.GetPooledObject() : null; break;
            case TYPE_FIRE_KING: spawnedObject = fireKingPool != null ? fireKingPool.GetPooledObject() : null; break;
            case TYPE_STONE_MONSTER: spawnedObject = stoneMonsterPool != null ? stoneMonsterPool.GetPooledObject() : null; break;
            case TYPE_BEE: spawnedObject = beePool != null ? beePool.GetPooledObject() : null; break;
            case TYPE_WANLUDUO: spawnedObject = wanLuDuoPool != null ? wanLuDuoPool.GetPooledObject() : null; break;
            default:
                break;
        }

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
