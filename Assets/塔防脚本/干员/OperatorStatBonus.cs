using UnityEngine;

/// <summary>
/// 挂在干员身上：在开局/部署后为该干员增加攻击力、防御力、部署费用、血量。
/// 数值为在 OperatorData 基础上的加算（如攻击+10、血量+50）。
/// </summary>
[RequireComponent(typeof(OperatorUnit))]
public class OperatorStatBonus : MonoBehaviour
{
    [Header("属性加成（在数据基础上的加值，可填负数表示减少）")]
    [Tooltip("攻击力增加量")]
    public int attackBonus = 0;

    [Tooltip("防御力增加量（每 100 点约 1% 减伤）")]
    public int defenseBonus = 0;

    [Tooltip("部署费用增加量（实际扣费 = 数据费用 + 本值）")]
    public int deployCostBonus = 0;

    [Tooltip("最大血量增加量（当前血量会同步增加，保持满血）")]
    public int healthBonus = 0;

    private void Start()
    {
        StartCoroutine(ApplyBonusAfterInit());
    }

    private System.Collections.IEnumerator ApplyBonusAfterInit()
    {
        yield return null;
        ApplyNow();
    }

    /// <summary> 立即应用当前加成到干员（部署时由卡片注入加成后调用）。 </summary>
    public void ApplyNow()
    {
        var unit = GetComponent<OperatorUnit>();
        if (unit == null) return;

        if (attackBonus != 0)
        {
            unit.runtimeAttackDamage += attackBonus;
            if (unit.runtimeAttackDamage < 0) unit.runtimeAttackDamage = 0;
        }

        if (defenseBonus != 0)
        {
            unit.runtimeDefense += defenseBonus;
            if (unit.runtimeDefense < 0) unit.runtimeDefense = 0;
        }

        if (healthBonus != 0)
        {
            unit.runtimeMaxHealth += healthBonus;
            if (unit.runtimeMaxHealth < 1) unit.runtimeMaxHealth = 1;
            unit.currentHealth += healthBonus;
            if (unit.currentHealth > unit.runtimeMaxHealth) unit.currentHealth = unit.runtimeMaxHealth;
            if (unit.currentHealth < 1) unit.currentHealth = 1;
            unit.UpdateUIState();
        }
    }

    /// <summary> 部署时由 OperatorUnit.GetDeployCost() 调用，返回本脚本的部署费用加值。 </summary>
    public int GetDeployCostBonus() => deployCostBonus;
}
