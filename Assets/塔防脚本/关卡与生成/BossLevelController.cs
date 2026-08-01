using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Boss关卡控制器。
/// 
/// 核心机制：
/// 1. Boss 分阶段 — 血量到阈值后切换阶段，改变攻击模式
/// 2. 召唤小怪 — 阶段2+定时召唤小怪辅助战斗
/// 3. Boss 远程攻击 — 周期性攻击守护点和附近干员
/// 4. 狂暴模式 — 最后阶段大幅提升攻击频率
/// 5. Boss 血条 — 顶部大型血条 UI
/// 
/// 使用方式：挂在场景任意物体上，配置 Spawner 引用和 Boss 血条 UI。
/// </summary>
public class BossLevelController : MonoBehaviour
{
    [Header("Boss 设置")]
    [Tooltip("Boss 的敌人类型索引（0=普通,3=哥布林,4=骷髅,5=小骷髅,6=黑之魔王,8=火之魔王）")]
    public int bossEnemyType = 6;
    [Tooltip("Boss 血量倍数（相对原始 EnemyData）")]
    public float bossHealthMultiplier = 5f;

    [Header("阶段配置")]
    [Tooltip("阶段1→2的血量百分比阈值")]
    [Range(0, 1)] public float phase1End = 0.66f;
    [Tooltip("阶段2→3的血量百分比阈值")]
    [Range(0, 1)] public float phase2End = 0.33f;

    [Header("Boss 远程攻击")]
    public bool enableBossRanged = true;
    [Tooltip("攻击间隔（秒）")]
    public float bossAttackInterval = 3f;
    [Tooltip("每次攻击对守护点造成的伤害")]
    public int bossAttackDamage = 2;
    [Tooltip("攻击范围")]
    public float bossAttackRange = 10f;

    [Header("召唤小怪")]
    public bool enableSummon = true;
    [Tooltip("召唤间隔（秒）")]
    public float summonInterval = 8f;
    [Tooltip("每次召唤数量")]
    public int summonCount = 3;
    [Tooltip("召唤的小怪类型索引")]
    public int summonEnemyType = 5;

    [Header("狂暴模式")]
    [Tooltip("狂暴时攻击间隔倍数（越小越快）")]
    public float enrageAttackIntervalMult = 0.5f;
    [Tooltip("狂暴时召唤间隔倍数")]
    public float enrageSummonIntervalMult = 0.6f;

    [Header("Boss 血条 UI")]
    public GameObject bossHealthBarRoot;
    public Image bossHealthFill;
    public TMP_Text bossNameText;
    public TMP_Text phaseAlertText;
    public string bossDisplayName = "黑之魔王";

    [Header("引用")]
    [Tooltip("Spawner 组件，用于从对象池获取 Boss 和小怪；不填则自动查找")]
    public Spawner spawner;

    private Enemy2 _bossEnemy;
    private int _currentPhase = 1;
    private float _attackTimer;
    private float _summonTimer;
    private bool _bossSpawned = false;
    private bool _bossDefeated = false;

    void Start()
    {
        if (spawner == null)
        {
            var all = FindObjectsOfType<Spawner>();
            if (all.Length > 0) spawner = all[0];
        }

        if (bossNameText != null) bossNameText.text = bossDisplayName;
        if (phaseAlertText != null) phaseAlertText.gameObject.SetActive(false);
        if (bossHealthBarRoot != null) bossHealthBarRoot.SetActive(false);

        StartCoroutine(SpawnBossDelayed());
    }

    IEnumerator SpawnBossDelayed()
    {
        yield return new WaitForSeconds(2f);
        SpawnBoss();
    }

    void SpawnBoss()
    {
        if (spawner == null)
        {
            return;
        }

        GameObject bossObj = GetPooledEnemy(bossEnemyType);
        if (bossObj == null)
        {
            return;
        }

        bossObj.transform.position = spawner.transform.position;

        _bossEnemy = bossObj.GetComponent<Enemy2>();
        if (_bossEnemy != null && spawner.paths != null && spawner.paths.Length > 0)
            _bossEnemy.SetPath(spawner.paths[0]);

        bossObj.SetActive(true);

        if (_bossEnemy != null)
            _bossEnemy.ApplyEliteBuff(bossHealthMultiplier, 100, 20);

        _bossSpawned = true;
        _bossDefeated = false;
        _attackTimer = bossAttackInterval;
        _summonTimer = summonInterval;

        if (bossHealthBarRoot != null) bossHealthBarRoot.SetActive(true);
        ShowPhaseAlert("阶段 1 — Boss 出现！");
    }

    GameObject GetPooledEnemy(int type)
    {
        if (spawner == null) return null;
        var entry = spawner.enemyPools.Find(e => (int)e.enemyType == type);
        return entry?.pool?.GetPooledObject();
    }

    void Update()
    {
        if (!_bossSpawned || _bossEnemy == null) return;

        if (!_bossEnemy.gameObject.activeSelf)
        {
            if (!_bossDefeated) OnBossDefeated();
            return;
        }

        UpdateBossHealthBar();
        CheckPhaseTransition();

        if (enableBossRanged)
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                BossAttack();
                float interval = _currentPhase == 3
                    ? bossAttackInterval * enrageAttackIntervalMult
                    : bossAttackInterval;
                _attackTimer = interval;
            }
        }

        if (enableSummon && _currentPhase >= 2)
        {
            _summonTimer -= Time.deltaTime;
            if (_summonTimer <= 0f)
            {
                SummonMinions();
                float interval = _currentPhase == 3
                    ? summonInterval * enrageSummonIntervalMult
                    : summonInterval;
                _summonTimer = interval;
            }
        }
    }

    void UpdateBossHealthBar()
    {
        if (bossHealthFill == null || _bossEnemy == null) return;
        float ratio = _bossEnemy.GetHealthRatio();
        bossHealthFill.fillAmount = Mathf.Clamp01(ratio);
    }

    void CheckPhaseTransition()
    {
        if (_bossEnemy == null) return;
        float ratio = _bossEnemy.GetHealthRatio();

        if (_currentPhase == 1 && ratio <= phase1End)
            EnterPhase(2);
        else if (_currentPhase == 2 && ratio <= phase2End)
            EnterPhase(3);
    }

    void EnterPhase(int phase)
    {
        _currentPhase = phase;
        string alert = phase == 2 ? "阶段 2 — Boss 开始召唤小怪！" : "阶段 3 — Boss 狂暴！";
        ShowPhaseAlert(alert);

        if (_bossEnemy == null) return;
        foreach (var sr in _bossEnemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (phase == 2)
                sr.color = new Color(0.8f, 0.5f, 1f, 1f); // 紫色
            else if (phase == 3)
                sr.color = new Color(1f, 0.3f, 0.3f, 1f); // 红色
        }
    }

    void ShowPhaseAlert(string text)
    {
        if (phaseAlertText == null) return;
        phaseAlertText.text = text;
        phaseAlertText.gameObject.SetActive(true);
        StartCoroutine(HidePhaseAlert(2.5f));
    }

    IEnumerator HidePhaseAlert(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (phaseAlertText != null) phaseAlertText.gameObject.SetActive(false);
    }

    void BossAttack()
    {
        if (_bossEnemy == null) return;

        if (GameManager.Instance != null)
            GameManager.Instance.TakeDamage(bossAttackDamage);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _bossEnemy.transform.position, bossAttackRange,
            LayerMask.GetMask("My"));

        foreach (var hit in hits)
        {
            var op = hit.GetComponent<OperatorUnit>();
            if (op != null) op.TakeDamage(bossAttackDamage, false);
        }
    }

    void SummonMinions()
    {
        if (spawner == null || _bossEnemy == null) return;

        int spawned = 0;
        for (int i = 0; i < summonCount; i++)
        {
            GameObject minion = GetPooledEnemy(summonEnemyType);
            if (minion == null) continue;

            Vector2 offset = Random.insideUnitCircle * 1.5f;
            minion.transform.position = _bossEnemy.transform.position + (Vector3)offset;

            Enemy2 enemyScript = minion.GetComponent<Enemy2>();
            if (enemyScript != null && spawner.paths != null && spawner.paths.Length > 0)
                enemyScript.SetPath(spawner.paths[Random.Range(0, spawner.paths.Length)]);

            minion.SetActive(true);
            spawned++;
        }
    }

    void OnBossDefeated()
    {
        _bossDefeated = true;
        _bossSpawned = false;

        if (bossHealthBarRoot != null) bossHealthBarRoot.SetActive(false);
        ShowPhaseAlert("Boss 已击败！");

        if (DeploymentManager.Instance != null)
            DeploymentManager.Instance.AddDP(30);
    }

    public int GetCurrentPhase() => _currentPhase;
    public bool IsBossAlive() => _bossSpawned && _bossEnemy != null && _bossEnemy.gameObject.activeSelf;
}
