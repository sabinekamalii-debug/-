using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 干员商店面板上的「铲子」工具：
/// 拖拽铲子到已部署的干员身上可将其铲除（销毁），
/// 不返还部署费用，但会触发该干员卡片的购买冷却，
/// 冷却结束后即可重新部署。
///
/// 与 DeploymentManager 的 R 键「撤退」区别：
/// - 撤退（R）：返还部署费用、无购买冷却，可立即重新部署。
/// - 铲子：不返还费用、触发购买冷却，需等冷却结束才能重新部署。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ShovelTool : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("检测设置")]
    [Tooltip("干员所在 Layer，用于松手时检测是否拖到了干员身上。留空则自动取 DeploymentManager.operatorLayer。")]
    public LayerMask operatorLayer;

    [Header("拖拽视觉")]
    [Tooltip("拖拽时是否克隆自身作为跟随鼠标的图标。")]
    public bool useDragVisual = true;

    private Canvas _rootCanvas;
    private GameObject _dragVisual;

    private void Awake()
    {
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null) _rootCanvas = _rootCanvas.rootCanvas;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 部署目标选择中或撤退模式中，禁用铲子，避免与既有流程冲突
        if (IsBusy()) return;

        if (operatorLayer == 0 && DeploymentManager.Instance != null)
            operatorLayer = DeploymentManager.Instance.operatorLayer;

        if (useDragVisual) CreateDragVisual(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragVisual != null) UpdateDragVisualPos(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragVisual != null) Destroy(_dragVisual);

        if (IsBusy()) return;
        if (operatorLayer == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(eventData.position);
        Vector2 pos2D = new Vector2(worldPos.x, worldPos.y);
        Collider2D hit = Physics2D.OverlapPoint(pos2D, operatorLayer);
        if (hit == null) return;

        OperatorUnit unit = hit.GetComponent<OperatorUnit>();
        if (unit == null) unit = hit.GetComponentInParent<OperatorUnit>();
        if (unit == null) return;

        ShovelOperator(unit);
    }

    private void ShovelOperator(OperatorUnit unit)
    {
        string opName = unit.data != null ? unit.data.operatorName : "干员";

        // 1. 触发对应卡片的购买冷却（按干员名匹配），冷却结束后才可重新部署
        OperatorCard card = FindCardForOperator(unit);
        if (card != null) card.OnDeployedSuccessfully(0);

        // 2. 释放阻挡的敌人（与撤退一致，避免敌人卡在已消失的干员上）
        if (unit.blocker != null) unit.blocker.ReleaseAllEnemies();

        // 3. 销毁干员（不返还部署费用——与撤退的核心区别）
        Destroy(unit.gameObject);

        if (SystemMessageUI.Instance != null)
            SystemMessageUI.Instance.ShowMessage($"已铲除 {opName}（不返还费用，等待冷却后可重新部署）", Color.yellow);
    }

    private OperatorCard FindCardForOperator(OperatorUnit unit)
    {
        if (unit == null || unit.data == null) return null;
        string name = unit.data.operatorName;
        foreach (var card in FindObjectsOfType<OperatorCard>())
        {
            if (card != null && card.operatorData != null && card.operatorData.operatorName == name)
                return card;
        }
        return null;
    }

    /// <summary>部署目标选择中（isGamePaused &amp; pendingOperator）或撤退模式中时，禁用铲子。</summary>
    private bool IsBusy()
    {
        var dm = DeploymentManager.Instance;
        if (dm == null) return false;
        return dm.isRetreatMode || dm.isGamePaused;
    }

    // ===== 拖拽视觉（克隆自身，保证与铲子外观一致）=====

    private void CreateDragVisual(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;
        _dragVisual = Instantiate(gameObject, _rootCanvas.transform, true);
        _dragVisual.name = "ShovelDragVisual";

        // 移除克隆体上的拖拽脚本，避免它也响应拖拽
        var clone = _dragVisual.GetComponent<ShovelTool>();
        if (clone != null) Destroy(clone);

        // 禁用所有 Graphic 的射线检测，避免挡住松手时的世界射线
        foreach (var g in _dragVisual.GetComponentsInChildren<Graphic>())
            g.raycastTarget = false;

        UpdateDragVisualPos(eventData);
    }

    private void UpdateDragVisualPos(PointerEventData eventData)
    {
        if (_dragVisual == null || _rootCanvas == null) return;
        Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_rootCanvas.transform, eventData.position, cam, out Vector2 local))
        {
            ((RectTransform)_dragVisual.transform).localPosition = local;
        }
    }
}
