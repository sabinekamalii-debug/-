using UnityEngine;

/// <summary>
/// 全局点击检测：
/// - 只在指定的干员图层（例如 My）上做射线检测
/// - 即使敌人碰撞体与干员重叠，也能稳定点中干员来释放技能
/// </summary>
public class OperatorInputController : MonoBehaviour
{
    [Header("点击检测设置")]
    [Tooltip("干员所在的 Layer，只会在这些 Layer 上检测鼠标点击（例如 My）")]
    public LayerMask operatorLayer;

    void Update()
    {
        if (TeleportController.Instance != null && TeleportController.Instance.IsInTeleportMode)
            return;
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 pos2D = new Vector2(mouseWorld.x, mouseWorld.y);

            // 只检测指定 Layer 上的碰撞体，敌人层会被完全忽略
            Collider2D hit = Physics2D.OverlapPoint(pos2D, operatorLayer);
            if (hit != null)
            {
                OperatorUnit op = hit.GetComponent<OperatorUnit>();
                if (op == null) op = hit.GetComponentInParent<OperatorUnit>();
                if (op != null)
                {
                    op.OnClickedForSkill();
                }
            }
        }
    }
}

