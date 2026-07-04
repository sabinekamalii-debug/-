using UnityEngine;

/// <summary>
/// 挂在刷怪点（Spawner）上的远程攻击脚本：专门攻击我方干员（OperatorUnit）。
/// - 在一定范围内寻找最近的干员。
/// - 有子弹预制体时生成子弹（使用 Projectile 脚本）飞向干员。
/// - 没有子弹预制体时，直接对干员造成伤害（瞬发）。
/// - 当范围内存在干员时，显示一个预警图片（刷怪点的子对象），否则隐藏。
/// </summary>
public class SpawnerShooter : MonoBehaviour
{
    [Header("攻击参数")]
    [Tooltip("攻击半径（世界单位）")]
    public float attackRange = 4f;

    [Tooltip("攻击间隔（秒）")]
    public float attackInterval = 1.2f;

    [Tooltip("每次攻击对干员造成的伤害数值")]
    public int attackDamage = 300;

    [Tooltip("是否无视干员防御")]
    public bool ignoreDefense = false;

    [Header("目标设置")]
    [Tooltip("干员所在的 Layer（比如 My），只会攻击这些层上的物体")]
    public LayerMask operatorLayer;

    [Header("子弹与特效")]
    [Tooltip("要生成的子弹预制体，建议使用已有的远程子弹预制体，需要挂 Projectile 脚本")]
    public GameObject bulletPrefab;

    [Tooltip("子弹发射起点，不填则用刷怪点自己的位置")]
    public Transform firePoint;

    [Tooltip("有目标进入范围且准备攻击时显示的预警图片（刷怪点的子对象），平时请在场景里设为不激活")]
    public GameObject warningImage;

    private float _attackTimer;
    private Transform _currentTarget;
    private SpawnerHealth _health;
    private bool _loggedOperatorLayerWarning;

    private void Awake()
    {
        CacheHealth();
    }

    /// <summary> 获取本物体或父物体上的 SpawnerHealth，避免脚本在不同物体上时取不到。 </summary>
    private void CacheHealth()
    {
        if (_health != null) return;
        _health = GetComponent<SpawnerHealth>();
        if (_health == null) _health = GetComponentInParent<SpawnerHealth>();
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        CacheHealth();
        // 刷怪点已被打爆时，直接停止一切攻击逻辑（无 SpawnerHealth 时也视为已失效，不攻击）
        if (_health == null || _health.isBroken)
        {
            if (warningImage != null && warningImage.activeSelf)
                warningImage.SetActive(false);
            return;
        }

        _attackTimer -= Time.deltaTime;

        _currentTarget = FindTarget();
        bool hasTarget = _currentTarget != null;
        if (warningImage != null)
            warningImage.SetActive(hasTarget);

        if (!hasTarget) return;
        if (_attackTimer > 0f) return;

        _attackTimer = attackInterval > 0f ? attackInterval : 0.5f;
        PerformAttack(_currentTarget);
    }

    /// <summary>
    /// 在攻击范围内寻找最近的干员（OperatorUnit）。
    /// </summary>
    private Transform FindTarget()
    {
        if (attackRange <= 0f) return null;
        if (operatorLayer == 0)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, operatorLayer);
        if (hits == null || hits.Length == 0) return null;

        Collider2D nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var col in hits)
        {
            if (col == null) continue;
            if (col.GetComponentInParent<OperatorUnit>() == null) continue;

            float dist = Vector2.Distance(transform.position, col.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = col;
            }
        }

        return nearest != null ? nearest.transform : null;
    }

    /// <summary>
    /// 执行一次攻击：优先用子弹；没有子弹时直接让干员掉血。
    /// </summary>
    private void PerformAttack(Transform target)
    {
        if (target == null) return;

        // 有子弹预制体：生成子弹飞向目标
        if (bulletPrefab != null)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            GameObject bulletGO = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            Projectile proj = bulletGO.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Seek(target, attackDamage, ignoreDefense);
            }
            return;
        }

        // 无子弹：直接对干员造成伤害
        OperatorUnit op = target.GetComponent<OperatorUnit>();
        if (op == null) op = target.GetComponentInParent<OperatorUnit>();
        if (op != null)
        {
            op.TakeDamage(attackDamage, ignoreDefense);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

