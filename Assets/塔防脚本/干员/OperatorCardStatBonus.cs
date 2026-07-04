using UnityEngine;

/// <summary>
/// 挂在「画布里的干员购买处」上（和 OperatorCard 同物体，如 角色画布/地面远程）。
/// 配置该购买位部署出的干员的属性加成：攻击、防御、部署费用、血量。
/// 部署时会把本脚本的数值应用到生成的干员身上。
/// </summary>
[RequireComponent(typeof(OperatorCard))]
public class OperatorCardStatBonus : MonoBehaviour
{
    [Header("属性加成（部署出的干员在数据基础上的加值）")]
    [Tooltip("攻击力增加量")]
    public int attackBonus = 0;

    [Tooltip("防御力增加量（每 100 点约 1% 减伤）")]
    public int defenseBonus = 0;

    [Tooltip("部署费用增加量（实际扣费 = 数据费用 + 本值）")]
    public int deployCostBonus = 0;

    [Tooltip("最大血量增加量（当前血量会同步增加，保持满血）")]
    public int healthBonus = 0;
}
