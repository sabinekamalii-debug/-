using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 给没有 RangedAttacker 的干员用：按「敌人图层」识别目标，且只有干员碰撞体与敌人/刷怪点碰撞体接触时才会攻击（不隔山打牛）。
/// 在 Inspector 里把 Enemy Layer 设成和 Ranged Attacker 一样的 Enemy 层；敌人/刷怪点需在该层上。
/// 同时支持触发器（OnTrigger）和普通碰撞（OnCollision），任一种接触都会计入。
///
/// 若始终不攻击，请检查：
/// 1. 干员或刷怪点至少有一个挂有 Rigidbody2D（可设为 Kinematic），否则 2D 物理不会产生触发/碰撞回调。
/// 2. Edit → Project Settings → Physics 2D → Layer Collision Matrix 中，My 与 Enemy 层的交叉格必须勾选（允许碰撞）。
/// </summary>
public class SpawnerContactAttacker : MonoBehaviour
{
    [Header("目标与子弹")]
    [Tooltip("视为敌人的 Layer，和 Ranged Attacker 的 Enemy Layer 设成一样；只攻击该层上且发生碰撞的目标")]
    public LayerMask enemyLayer;

    [Header("可选：不填则自动从同物体取")]
    [Tooltip("干员本体，用于读取攻击力、攻速与技能禁攻状态")]
    public OperatorUnit unit;

    [Header("调试")]
    [Tooltip("勾选后在与刷怪点接触/分离时在 Console 打印，用于排查是否收到触发器")]
    public bool debugLogContacts;

    private readonly List<Enemy2> _contactEnemies = new List<Enemy2>();
    private readonly List<SpawnerHealth> _contactSpawners = new List<SpawnerHealth>();
    private float _attackTimer;

    /// <summary> 碰撞体是否在敌人图层上（enemyLayer 为 0 时视为通过）。 </summary>
    private bool IsOnEnemyLayer(GameObject go)
    {
        if (go == null) return false;
        if (enemyLayer == 0) return true;
        return ((1 << go.layer) & enemyLayer) != 0;
    }

    void Awake()
    {
        if (unit == null) unit = GetComponent<OperatorUnit>();
    }

    void TryAddContact(Collider2D other)
    {
        if (other == null) return;
        if (!IsOnEnemyLayer(other.gameObject)) return;
        if (!other.CompareTag("Enemy")) return;

        SpawnerHealth spawner = other.GetComponent<SpawnerHealth>();
        if (spawner != null)
        {
            if (!spawner.isBroken && !_contactSpawners.Contains(spawner))
            {
                _contactSpawners.Add(spawner);
            }
            return;
        }

        Enemy2 enemy = other.GetComponentInParent<Enemy2>();
        if (enemy != null && !_contactEnemies.Contains(enemy))
            _contactEnemies.Add(enemy);
    }

    void TryRemoveContact(Collider2D other)
    {
        if (other == null) return;
        if (!IsOnEnemyLayer(other.gameObject)) return;
        if (!other.CompareTag("Enemy")) return;

        SpawnerHealth spawner = other.GetComponent<SpawnerHealth>();
        if (spawner != null)
        {
            _contactSpawners.Remove(spawner);
            return;
        }

        Enemy2 enemy = other.GetComponentInParent<Enemy2>();
        if (enemy != null)
            _contactEnemies.Remove(enemy);
    }

    void OnTriggerEnter2D(Collider2D other) => TryAddContact(other);
    void OnTriggerExit2D(Collider2D other) => TryRemoveContact(other);

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
            TryAddContact(collision.collider);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
            TryRemoveContact(collision.collider);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (unit != null && unit.skillPreventAttack) return;

        _contactEnemies.RemoveAll(e => e == null);
        _contactSpawners.RemoveAll(s => s == null || s.isBroken);

        if (_contactEnemies.Count == 0 && _contactSpawners.Count == 0) return;

        _attackTimer += Time.deltaTime;
        float interval = unit != null ? unit.runtimeAttackInterval : 1f;
        if (interval <= 0f) interval = 1f;

        if (_attackTimer >= interval)
        {
            int damage = unit != null ? unit.runtimeAttackDamage : 1;

            bool ignoreDefense = unit != null && unit.GetComponent<IgnoreDefenseAttacker>() != null;
            // 若本干员有 UnitBlocker，敌人伤害只由 OperatorUnit 对「被阻挡的」造成，此处不再对接触列表里的敌人出手，避免顺手打死路过的敌人
            bool enemyDamageHandledByBlocker = unit != null && unit.blocker != null;
            if (_contactEnemies.Count > 0 && !enemyDamageHandledByBlocker)
            {
                Enemy2 first = _contactEnemies[0];
                if (first != null) first.TakeDamage(damage, ignoreDefense);
            }
            else if (_contactSpawners.Count > 0)
            {
                SpawnerHealth first = _contactSpawners[0];
                if (first != null && !first.isBroken) first.TakeDamage(damage);
            }

            _attackTimer = 0f;
        }
    }

    /// <summary> 当前是否与敌人或刷怪点碰撞中（可攻击）。 </summary>
    public bool HasTarget()
    {
        _contactEnemies.RemoveAll(e => e == null);
        _contactSpawners.RemoveAll(s => s == null || s.isBroken);
        return _contactEnemies.Count > 0 || _contactSpawners.Count > 0;
    }
}
