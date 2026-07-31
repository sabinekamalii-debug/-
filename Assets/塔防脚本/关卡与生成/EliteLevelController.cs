using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 精英关卡控制器：在普通塔防玩法之上增加精英机制。
/// 1. 精英强化 — 所有敌人获得属性 buff（血量/防御），外观变橙
/// 2. 环境危机 — 周期性降低干员攻击力
/// 3. 精英奖励 — 击杀精英敌人额外获得部署费用
/// 挂在场景任意物体上即可，自动查找所有 Spawner 和干员。
/// </summary>
public class EliteLevelController : MonoBehaviour
{
    [Header("精英强化")]
    [Tooltip("敌人血量乘数（1.5 = 血量提升50%）")]
    public float eliteHealthMultiplier = 1.5f;
    [Tooltip("敌人防御加成")]
    public int eliteDefenseBonus = 50;
    [Tooltip("精英敌人击杀额外费用")]
    public int eliteBonusDP = 2;

    [Header("环境危机")]
    [Tooltip("是否启用周期性危机")]
    public bool enableHazard = true;
    [Tooltip("危机周期（秒），每过这么久触发一次")]
    public float hazardInterval = 25f;
    [Tooltip("危机持续时间（秒）")]
    public float hazardDuration = 6f;
    [Tooltip("危机期间干员攻击力乘数（0.5 = 攻击力减半）")]
    public float hazardAttackMultiplier = 0.5f;

    [Header("UI（可选）")]
    [Tooltip("危机警告文本，不需要可留空")]
    public TMP_Text hazardWarningText;

    [Header("调试")]
    public bool debugLog = false;

    private bool _hazardActive = false;

    void Start()
    {
        if (enableHazard)
            StartCoroutine(HazardLoop());

        if (hazardWarningText != null)
            hazardWarningText.gameObject.SetActive(false);

        // 给场景中所有 Spawner 挂上精英 Hook
        foreach (var sp in FindObjectsOfType<Spawner>())
        {
            var hook = sp.gameObject.GetComponent<EliteEnemyHook>();
            if (hook == null)
                hook = sp.gameObject.AddComponent<EliteEnemyHook>();
            hook.healthMultiplier = eliteHealthMultiplier;
            hook.defenseBonus = eliteDefenseBonus;
            hook.bonusDP = eliteBonusDP;
        }

        if (debugLog) Debug.Log("[EliteLevelController] 精英关卡初始化完成");
    }

    IEnumerator HazardLoop()
    {
        var wait = new WaitForSeconds(1f);
        float timer = 0f;

        while (this != null && enabled)
        {
            yield return wait;
            timer += 1f;

            if (!_hazardActive && timer >= hazardInterval)
            {
                timer = 0f;
                StartHazard();
            }
            else if (_hazardActive && timer >= hazardDuration)
            {
                timer = 0f;
                EndHazard();
            }
        }
    }

    void StartHazard()
    {
        _hazardActive = true;
        if (hazardWarningText != null)
        {
            hazardWarningText.text = "⚠ 环境危机！干员攻击力下降 ⚠";
            hazardWarningText.gameObject.SetActive(true);
        }
        foreach (var op in FindObjectsOfType<OperatorUnit>())
        {
            var marker = op.gameObject.GetComponent<HazardDebuffMarker>();
            if (marker == null)
                marker = op.gameObject.AddComponent<HazardDebuffMarker>();
            marker.Apply(hazardAttackMultiplier);
        }
        if (debugLog) Debug.Log("[EliteLevelController] 环境危机开始");
    }

    void EndHazard()
    {
        _hazardActive = false;
        if (hazardWarningText != null)
            hazardWarningText.gameObject.SetActive(false);
        foreach (var op in FindObjectsOfType<OperatorUnit>())
        {
            var marker = op.GetComponent<HazardDebuffMarker>();
            if (marker != null) marker.Remove();
        }
        if (debugLog) Debug.Log("[EliteLevelController] 环境危机结束");
    }

    public bool IsHazardActive() => _hazardActive;
}

/// <summary>
/// 挂在 Spawner 上，定期扫描新激活的敌人并施加精英 buff。
/// </summary>
public class EliteEnemyHook : MonoBehaviour
{
    [HideInInspector] public float healthMultiplier = 1.5f;
    [HideInInspector] public int defenseBonus = 50;
    [HideInInspector] public int bonusDP = 2;

    private Spawner _spawner;

    void Start()
    {
        _spawner = GetComponent<Spawner>();
        StartCoroutine(ScanLoop());
    }

    IEnumerator ScanLoop()
    {
        var wait = new WaitForSeconds(0.3f);
        while (this != null)
        {
            foreach (var enemy in FindObjectsOfType<Enemy2>())
            {
                if (enemy.GetComponent<EliteBuffMarker>() == null)
                {
                    var marker = enemy.gameObject.AddComponent<EliteBuffMarker>();
                    marker.Apply(healthMultiplier, defenseBonus, bonusDP);
                }
            }
            yield return wait;
        }
    }
}

/// <summary>
/// 标记已施加精英 buff 的敌人。
/// </summary>
public class EliteBuffMarker : MonoBehaviour
{
    private int _bonusDP;
    private bool _applied = false;

    public void Apply(float hpMult, int defBonus, int bonusDP)
    {
        if (_applied) return;
        _applied = true;
        _bonusDP = bonusDP;

        var enemy = GetComponent<Enemy2>();
        if (enemy == null) return;

        enemy.ApplyEliteBuff(hpMult, defBonus, bonusDP);

        // 橙色调色
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.color = new Color(
                Mathf.Min(sr.color.r * 1.3f, 1f),
                Mathf.Min(sr.color.g * 0.7f, 1f),
                Mathf.Min(sr.color.b * 0.4f, 1f),
                sr.color.a
            );
        }
    }

    public int GetBonusDP() => _bonusDP;
}

/// <summary>
/// 危机减益：直接修改 OperatorUnit.runtimeAttackDamage。
/// </summary>
public class HazardDebuffMarker : MonoBehaviour
{
    private int _storedDamage = -1;
    private bool _active = false;

    public void Apply(float multiplier)
    {
        if (_active) return;
        var unit = GetComponent<OperatorUnit>();
        if (unit == null) return;
        _storedDamage = unit.runtimeAttackDamage;
        unit.runtimeAttackDamage = Mathf.Max(1, (int)(_storedDamage * multiplier));
        _active = true;
    }

    public void Remove()
    {
        if (!_active) return;
        var unit = GetComponent<OperatorUnit>();
        if (unit != null && _storedDamage > 0)
            unit.runtimeAttackDamage = _storedDamage;
        _active = false;
        _storedDamage = -1;
    }
}
