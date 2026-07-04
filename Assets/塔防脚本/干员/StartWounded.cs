using UnityEngine;

/// <summary>
/// 挂在干员身上：开局时该干员血量变为满血的 3/4（残血状态）。
/// 在 OperatorUnit 完成初始化后再扣血，并刷新血条 UI。
/// </summary>
[RequireComponent(typeof(OperatorUnit))]
public class StartWounded : MonoBehaviour
{
    [Tooltip("开局血量比例，默认 0.75 即 3/4")]
    [Range(0.01f, 1f)]
    public float healthRatio = 0.75f;

    private void Start()
    {
        StartCoroutine(ApplyWoundedAfterInit());
    }

    private System.Collections.IEnumerator ApplyWoundedAfterInit()
    {
        yield return null; // 等一帧，确保 OperatorUnit.Start 已执行
        var unit = GetComponent<OperatorUnit>();
        if (unit == null) yield break;
        int maxHp = unit.runtimeMaxHealth;
        if (maxHp <= 0) yield break;
        int woundedHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * healthRatio));
        unit.currentHealth = woundedHp;
        unit.UpdateUIState();
    }
}
