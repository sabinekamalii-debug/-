using UnityEngine;

/// <summary>
/// 远程攻击：攻击范围、攻速、攻击力均来自干员数据（OperatorData.attackRange / attackInterval / attackDamage）。
/// 仅子弹预制体、发射点、敌人 Layer 需在 Inspector 配置。
/// </summary>
public class RangedAttacker : MonoBehaviour
{
    [Header("目标与子弹")]
    [Tooltip("视为敌人的 Layer（只会攻击这些层上的物体）")]
    public LayerMask enemyLayer;

    [Header("守护点优先（远程/高台）")]
    [Tooltip("未阻挡敌人时：若敌人离守护点距离 ≤ 此值，则优先攻击离守护点最近的敌人；≤0 表示不启用")]
    public float guardPointPriorityRange = 6f;

    [Tooltip("要生成的子弹预制体，必须带 Projectile 脚本")]
    public GameObject bulletPrefab;

    [Tooltip("子弹发射的起点，一般是角色身上的一个空物体")]
    public Transform firePoint;

    private float fireCountdown = 0f;
    private Transform target;
    private readonly Collider2D[] scanResults = new Collider2D[10];
    private OperatorUnit unit;
    private float _rangeOverride = -1f;

    /// <summary> 攻击范围：来自干员数据 attackRange；技能可临时改写，设为 ≤0 恢复用数据。 </summary>
    public float range
    {
        get => _rangeOverride > 0 ? _rangeOverride : (unit != null && unit.data != null ? unit.data.attackRange : 5f);
        set => _rangeOverride = value;
    }

    public void ClearRangeOverride() { _rangeOverride = -1f; }

    void Awake()
    {
        unit = GetComponent<OperatorUnit>();
    }

    void Start()
    {
        InvokeRepeating("UpdateTarget", 0f, 0.2f);
    }

    void Update()
    {
        if (Time.timeScale == 0) return;
        if (unit != null && unit.skillPreventAttack) return;

        // 若当前锁定的是已被打爆的刷怪点，则立刻丢失目标，停止继续攻击该位置
        if (target != null)
        {
            var sp = target.GetComponent<SpawnerHealth>();
            if (sp != null && sp.isBroken)
            {
                target = null;
            }
        }

        if (fireCountdown > 0) fireCountdown -= Time.deltaTime;

        if (target != null && fireCountdown <= 0f)
        {
            Shoot();
            float actualRate = unit != null ? (1f / unit.runtimeAttackInterval) : 1f;
            fireCountdown = 1f / actualRate;
        }
    }

    void UpdateTarget()
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = Physics2D.queriesHitTriggers;
        filter.useDepth = false;

        int count = Physics2D.OverlapCircle((Vector2)transform.position, range, filter, scanResults);
        
        // 优先攻击敌人，没有敌人时才攻击刷怪点
        float shortestEnemyDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;
        float shortestSpawnerDistance = Mathf.Infinity;
        GameObject nearestSpawner = null;
        // 守护点优先：未阻挡时，若存在离守护点较近的敌人，选离守护点最近的那个
        Transform guardPoint = GridSystem.Instance != null ? GridSystem.Instance.defensePoint : null;
        bool useGuardPointPriority = guardPoint != null && guardPointPriorityRange > 0f && IsNotBlocking();
        float nearestToGuardPointDist = Mathf.Infinity;
        GameObject nearestToGuardPointEnemy = null;

        for (int i = 0; i < count; i++)
        {
            if (scanResults[i] == null) continue;
            float distance = Vector2.Distance(transform.position, scanResults[i].transform.position);
            
            // 检查是否为敌人（Enemy2）
            Enemy2 enemy = scanResults[i].GetComponent<Enemy2>();
            if (enemy != null)
            {
                if (distance < shortestEnemyDistance)
                {
                    shortestEnemyDistance = distance;
                    nearestEnemy = scanResults[i].gameObject;
                }
                // 未阻挡且启用守护点优先：记录范围内、且离守护点距离 ≤ 阈值的最近敌人
                if (useGuardPointPriority)
                {
                    float distToGuard = Vector2.Distance(guardPoint.position, scanResults[i].transform.position);
                    if (distToGuard <= guardPointPriorityRange && distToGuard < nearestToGuardPointDist)
                    {
                        nearestToGuardPointDist = distToGuard;
                        nearestToGuardPointEnemy = scanResults[i].gameObject;
                    }
                }
            }
            // 检查是否为刷怪点（SpawnerHealth），且尚未被打爆
            else
            {
                SpawnerHealth spawner = scanResults[i].GetComponent<SpawnerHealth>();
                if (spawner != null && !spawner.isBroken && distance < shortestSpawnerDistance)
                {
                    shortestSpawnerDistance = distance;
                    nearestSpawner = scanResults[i].gameObject;
                }
            }
        }
        
        // 未阻挡且存在“离守护点较近”的敌人时，优先打离守护点最近的
        if (nearestToGuardPointEnemy != null)
        {
            target = nearestToGuardPointEnemy.transform;
            return;
        }
        // 否则：优先选择离自己最近的敌人，没有敌人时才选刷怪点
        target = (nearestEnemy != null) ? nearestEnemy.transform : 
                 (nearestSpawner != null) ? nearestSpawner.transform : null;
    }

    /// <summary> 当前是否未阻挡敌人（高台干员或阻挡列表为空）。 </summary>
    private bool IsNotBlocking()
    {
        if (unit == null) return true;
        if (unit.IsStandingOnHighGround()) return true;
        return unit.blocker == null || unit.blocker.blockedEnemies.Count == 0;
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;
        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Projectile bullet = bulletGO.GetComponent<Projectile>();
        int damage = unit != null ? unit.runtimeAttackDamage : 1;
        bool ignoreDefense = unit != null && unit.GetComponent<IgnoreDefenseAttacker>() != null;
        if (bullet != null) bullet.Seek(target, damage, ignoreDefense);
    }

    public bool HasTarget()
    {
        if (target != null && target.gameObject.activeSelf)
            return Vector3.Distance(transform.position, target.position) <= range;
        return false;
    }
}
