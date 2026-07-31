using UnityEngine;

/// <summary>
/// 奶妈怪治疗Trait：挂在敌人身上即可，由 Enemy2.Start() 按数据自动添加。
/// 周期性治疗周围所有非死亡敌人，治疗量按目标最大生命百分比计算。
/// </summary>
[RequireComponent(typeof(Enemy2))]
public class EnemyHealerTrait : MonoBehaviour
{
    private Enemy2 _self;
    private EnemyData2 _data;
    private float _timer;
    private static readonly Collider2D[] _hits = new Collider2D[32];

    void Awake()
    {
        _self = GetComponent<Enemy2>();
        _data = _self != null ? _self.Data : null;
    }

    void Update()
    {
        if (_self == null || _data == null) return;
        if (Time.timeScale == 0f) return;

        _timer += Time.deltaTime;
        if (_timer < _data.healInterval) return;
        _timer = 0f;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, _data.healRadius, _hits);
        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null) continue;
            var enemy = _hits[i].GetComponentInParent<Enemy2>();
            if (enemy == null || enemy == _self) continue;
            if (enemy.IsDead()) continue;

            int healAmount = Mathf.RoundToInt(enemy.MaxHealth * _data.healPercentOfMax / 100f);
            if (healAmount > 0)
                enemy.Heal(healAmount);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (_data == null) return;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _data.healRadius);
    }
}
