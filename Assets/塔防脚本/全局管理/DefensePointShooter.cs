using UnityEngine;

/// <summary>
/// 守护点专用远程攻击脚本：独立于干员体系。
/// - 定时在攻击范围内寻找敌人（Enemy2 或 SpawnerHealth）。
/// - 如果配置了子弹预制体，则生成子弹（需带 Projectile 脚本）飞向目标。
/// - 如果没配置子弹预制体，则直接对目标造成伤害（瞬发）。
/// 挂法：直接挂在守护点物体上，Inspector 里配置范围、间隔、伤害、敌人 Layer 等即可。
/// </summary>
public class DefensePointShooter : MonoBehaviour
{
    [Header("攻击参数")]
    [Tooltip("攻击半径（世界单位）")]
    public float attackRange = 5f;

    [Tooltip("攻击间隔（秒）")]
    public float attackInterval = 1f;

    [Tooltip("每次攻击造成的伤害数值")]
    public int attackDamage = 500;

    [Tooltip("是否无视防御（传给 Enemy2.TakeDamage 的 ignoreDefense）")]
    public bool ignoreDefense = false;

    [Header("目标设置")]
    [Tooltip("可攻击目标所在 Layer：勾选 Enemy 打敌人、刷怪点；勾选 My 可同时打干员。至少勾选一层。")]
    public LayerMask enemyLayer;

    [Tooltip("是否优先攻击离守护点最近的敌人（利用 GridSystem.defensePoint，为防御点“保命”）")]
    public bool prioritizeNearDefensePoint = true;

    [Header("子弹与特效（可选）")]
    [Tooltip("要生成的子弹预制体，建议使用已有的远程干员子弹预制体，需挂 Projectile 脚本")]
    public GameObject bulletPrefab;

    [Tooltip("子弹发射的起点，一般是守护点身上的一个空物体；不填则用守护点自身位置")]
    public Transform firePoint;

    [Tooltip("有敌人进入范围且准备攻击时显示的预警图片（守护点的子对象），平时请在场景里设为不激活")]
    public GameObject warningImage;

    private float _attackTimer;

    private void Update()
    {
        // 暂停时不攻击
        if (Time.timeScale == 0f) return;

        _attackTimer -= Time.deltaTime;

        Transform target = FindTarget();
        bool hasTarget = target != null;
        if (warningImage != null)
            warningImage.SetActive(hasTarget);

        if (!hasTarget) return;
        if (_attackTimer > 0f) return;

        _attackTimer = attackInterval > 0f ? attackInterval : 0.5f;
        PerformAttack(target);
    }

    /// <summary>
    /// 在攻击范围内寻找目标：
    /// 1. 优先 Enemy2（小怪），若开启 prioritizeNearDefensePoint 则选离守护点最近的敌人。
    /// 2. 没有敌人时，尝试找 SpawnerHealth（刷怪点）。
    /// 3. 再没有则找 OperatorUnit（干员，需 Enemy Layer 包含 My 等干员所在层）。
    /// </summary>
    private Transform FindTarget()
    {
        if (attackRange <= 0f) return null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
        if (hits == null || hits.Length == 0) return null;

        Transform guardPoint = (GridSystem.Instance != null) ? GridSystem.Instance.defensePoint : null;
        bool useGuardPriority = prioritizeNearDefensePoint && guardPoint != null;

        Collider2D nearestEnemy = null;
        float nearestEnemyDist = float.MaxValue;

        Collider2D nearestSpawner = null;
        float nearestSpawnerDist = float.MaxValue;

        Collider2D nearestOperator = null;
        float nearestOperatorDist = float.MaxValue;

        Collider2D guardPriorityEnemy = null;
        float guardPriorityDist = float.MaxValue;

        foreach (var col in hits)
        {
            if (col == null) continue;

            float distFromShooter = Vector2.Distance(transform.position, col.transform.position);

            // 敌人
            Enemy2 enemy = col.GetComponent<Enemy2>();
            if (enemy != null)
            {
                if (distFromShooter < nearestEnemyDist)
                {
                    nearestEnemyDist = distFromShooter;
                    nearestEnemy = col;
                }

                if (useGuardPriority && guardPoint != null)
                {
                    float distToGuard = Vector2.Distance(guardPoint.position, col.transform.position);
                    if (distToGuard < guardPriorityDist)
                    {
                        guardPriorityDist = distToGuard;
                        guardPriorityEnemy = col;
                    }
                }
                continue;
            }

            // 刷怪点
            SpawnerHealth spawner = col.GetComponent<SpawnerHealth>();
            if (spawner != null)
            {
                if (distFromShooter < nearestSpawnerDist)
                {
                    nearestSpawnerDist = distFromShooter;
                    nearestSpawner = col;
                }
                continue;
            }

            // 干员（OperatorUnit，Enemy Layer 需包含 My 等干员所在层）
            OperatorUnit op = col.GetComponent<OperatorUnit>();
            if (op == null) op = col.GetComponentInParent<OperatorUnit>();
            if (op != null && distFromShooter < nearestOperatorDist)
            {
                nearestOperatorDist = distFromShooter;
                nearestOperator = col;
            }
        }

        if (guardPriorityEnemy != null)
            return guardPriorityEnemy.transform;

        if (nearestEnemy != null)
            return nearestEnemy.transform;

        if (nearestSpawner != null)
            return nearestSpawner.transform;

        if (nearestOperator != null)
            return nearestOperator.transform;

        return null;
    }

    /// <summary>
    /// 执行一次攻击：有子弹则生成子弹，没有则直接伤害。
    /// </summary>
    private void PerformAttack(Transform target)
    {
        if (target == null) return;

        // 配了子弹：生成子弹，交给 Projectile 处理命中和伤害
        if (bulletPrefab != null)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            GameObject bulletGO = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            Projectile proj = bulletGO.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Seek(target, attackDamage, ignoreDefense);
            }
            return;
        }

        // 没配子弹：直接对目标造成伤害（瞬发）
        Enemy2 enemy = target.GetComponent<Enemy2>();
        if (enemy == null) enemy = target.GetComponentInParent<Enemy2>();

        if (enemy != null)
        {
            enemy.TakeDamage(attackDamage, ignoreDefense);
            return;
        }

        SpawnerHealth spawner = target.GetComponent<SpawnerHealth>();
        if (spawner == null) spawner = target.GetComponentInParent<SpawnerHealth>();
        if (spawner != null)
        {
            spawner.TakeDamage(attackDamage);
            return;
        }

        // 干员
        OperatorUnit op = target.GetComponent<OperatorUnit>();
        if (op == null) op = target.GetComponentInParent<OperatorUnit>();
        if (op != null)
            op.TakeDamage(attackDamage, ignoreDefense);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

