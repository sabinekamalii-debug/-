using UnityEngine;

/// <summary>
/// 先锋侦察标记被动：挂在先锋干员身上即可。
/// 干员移动/战斗/避让过程中，只要有敌人进入侦察半径，就把它「标记」（青色侦察环 + 限时增伤）。
/// 标记独立于「打 / 避让」抉择——无论玩家最后选战斗还是避让，路过的敌人都已被标记。
/// 配合高移动速度(OperatorBrain.moveSpeed)，实现「高速穿插侦察、路过即标记、他人集火」的先锋定位。
/// </summary>
public class VanguardRecon : MonoBehaviour
{
    [Header("侦察标记参数")]
    [Tooltip("侦察半径：敌人进入该范围即被标记")]
    public float markRadius = 2.5f;

    [Tooltip("标记持续时间（秒），先锋再次路过会刷新")]
    public float markDuration = 10f;

    [Tooltip("检测节流间隔（秒），越小越灵敏但更耗性能")]
    public float scanInterval = 0.2f;

    private float _scanTimer;
    private readonly Collider2D[] _hits = new Collider2D[32];

    void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = scanInterval;

        // 用非分配版本避免每帧 GC
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, markRadius, _hits);
        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null) continue;
            var enemy = _hits[i].GetComponentInParent<Enemy2>();
            if (enemy != null)
                enemy.MarkForRecon(markDuration);
        }
    }

    // 在编辑器里可视化侦察半径，方便调参
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, markRadius);
    }
}
