using UnityEngine;

/// <summary>
/// 挂到干员或敌人身上时，该单位造成的攻击伤害将无视目标的防御值（按原始伤害结算，不参与防御减伤）。
/// 仅对“有防御值”的目标生效（敌人 Enemy2、干员 OperatorUnit）；刷怪点等无防御单位不受影响。
/// </summary>
public class IgnoreDefenseAttacker : MonoBehaviour
{
}
