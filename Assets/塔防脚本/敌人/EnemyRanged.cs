//using UnityEngine;
//using System.Collections.Generic;

//public class EnemyRanged : MonoBehaviour
//{
//    [Header("攻击设置")]
//    public float attackRange = 5.0f;     // 索敌范围
//    public float attackInterval = 2.0f;  // 攻速
//    public int attackDamage = 10;
//    public LayerMask operatorLayer;      // 干员所在的图层 (一定要设置！)

//    [Header("弹道引用")]
//    public GameObject projectilePrefab;  // 子弹预制体
//    public Transform firePoint;          // 子弹发射点

//    // 内部变量
//    private float attackTimer = 0f;
//    private Transform currentTarget;

//    void Update()
//    {
//        attackTimer += Time.deltaTime;

//        // 1. 持续索敌 (也可以优化为每0.2秒索敌一次)
//        FindPriorityTarget();

//        // 2. 如果有目标且冷却好了，开火
//        if (currentTarget != null && attackTimer >= attackInterval)
//        {
//            Attack();
//            attackTimer = 0f;
//        }
//    }

//    // --- 核心：寻找“最早部署”的目标 ---
//    void FindPriorityTarget()
//    {
//        // 扫描范围内所有物体
//        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, operatorLayer);

//        Transform bestTarget = null;
//        int minID = int.MaxValue; // 先设为一个无限大的数

//        foreach (var hit in hits)
//        {
//            // 确保找到的是干员 (且还活着)
//            OperatorUnit op = hit.GetComponent<OperatorUnit>();
//            if (op == null) op = hit.GetComponentInParent<OperatorUnit>();

//            if (op != null)
//            {
//                // 【核心逻辑】比谁的工号更小
//                if (op.deployID < minID)
//                {
//                    minID = op.deployID;
//                    bestTarget = op.transform;
//                }
//            }
//        }

//        currentTarget = bestTarget;
//    }
//    void Attack()
//    {
//        if (projectilePrefab != null && firePoint != null)
//        {
//            // 生成子弹
//            GameObject projObj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

//            // 获取你的 Projectile 脚本并初始化
//            Projectile projScript = projObj.GetComponent<Projectile>();
//            if (projScript != null)
//            {
//                projScript.Seek(currentTarget, attackDamage);
//            }
//        }
//    }

//    // --- 辅助线：在 Scene 窗口画个红圈 ---
//    void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.red;
//        Gizmos.DrawWireSphere(transform.position, attackRange);
//    }
//}