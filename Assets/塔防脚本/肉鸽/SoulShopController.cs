using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 天赋树场景控制器（复用 SoulShop 场景）：
/// 场景中已搭建好天赋树UI（TreeRoot > Node_xxx / Line / Label_xxx）。
/// 本脚本只负责：刷新节点状态、拖拽平移、滚轮缩放。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SoulShopController : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
{
    private RectTransform _treeRoot;
    private Vector2 _dragStartPos;
    private Vector2 _dragStartAnchoredPos;
    private float _currentScale = 1f;
    private const float MinScale = 0.5f;
    private const float MaxScale = 1.5f;

    private void Awake()
    {
        TalentTreeState.InitIfNeeded();
        RogueRuntimeState.InitIfNeeded();
    }

    private void Start()
    {
        var treeController = FindFirstObjectByType<TalentTreeController>();
        if (treeController != null) return;

        // 找场景中已搭建的 TreeRoot
        _treeRoot = GameObject.Find("TreeRoot")?.GetComponent<RectTransform>();
        if (_treeRoot == null)
        {
            Debug.LogError("[SoulShopController] 场景中没有找到 TreeRoot，请检查场景搭建");
            return;
        }

        // ScrollRect/Viewport 的全屏 Image 会拦截 Header 上返回按钮的点击
        var scrollRect = GameObject.Find("TalentScrollRect");
        if (scrollRect != null)
        {
            var srImage = scrollRect.GetComponent<Image>();
            if (srImage != null) srImage.raycastTarget = false;

            var viewport = scrollRect.GetComponent<ScrollRect>()?.viewport;
            if (viewport != null)
            {
                var vpImage = viewport.GetComponent<Image>();
                if (vpImage != null) vpImage.raycastTarget = false;
            }
        }

        // 隐藏旧 UpgradeList
        var oldList = GameObject.Find("UpgradeList");
        if (oldList != null) oldList.SetActive(false);

        // 刷新标题和天赋点
        var title = FindTmp("ShopTitle");
        if (title != null) title.text = "天赋树";

        var soulCount = FindTmp("SoulCount");
        if (soulCount != null)
            soulCount.text = $"天赋点: {TalentTreeState.TalentPoints}";

        // 把拖拽事件挂到 TreeRoot 的 Image 上
        var treeImage = _treeRoot.GetComponent<Image>();
        if (treeImage != null)
        {
            // 确保有 raycastTarget 才能接收拖拽
            treeImage.raycastTarget = true;
        }

        // 刷新所有节点状态
        RefreshAllNodes();

        // 绑定返回按钮
        var backBtn = GameObject.Find("Btn_返回入口");
        if (backBtn != null)
        {
            var btn = backBtn.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => VideoSceneLoader.LoadScene(SceneNames.RogueEntry));
            }
        }
    }

    // ─────────────────────────────────────────────
    //  拖拽平移 + 滚轮缩放
    // ─────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_treeRoot == null) return;
        _dragStartPos = eventData.position;
        _dragStartAnchoredPos = _treeRoot.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_treeRoot == null) return;
        Vector2 delta = eventData.position - _dragStartPos;
        _treeRoot.anchoredPosition = _dragStartAnchoredPos + delta;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_treeRoot == null) return;
        float d = eventData.scrollDelta.y > 0 ? 0.1f : -0.1f;
        _currentScale = Mathf.Clamp(_currentScale + d, MinScale, MaxScale);
        _treeRoot.localScale = Vector3.one * _currentScale;
    }

    // ─────────────────────────────────────────────
    //  刷新节点状态
    // ─────────────────────────────────────────────

    private void RefreshAllNodes()
    {
        foreach (var node in TalentTreeData.Nodes)
        {
            var go = GameObject.Find("Node_" + node.nodeId);
            if (go == null) continue;
            ApplyNodeState(go, node);
        }

        // 刷新连线颜色
        foreach (var node in TalentTreeData.Nodes)
        {
            bool unlocked = TalentTreeState.IsNodeUnlocked(node.nodeId);
            // 连线在节点之前，是同级目录里的 Line 对象
            // 但场景搭建时连线没有唯一名称，跳过连线刷新
        }
    }

    private void ApplyNodeState(GameObject go, TalentTreeData.NodeDef node)
    {
        var btn = go.GetComponent<Button>();
        var img = go.GetComponent<Image>();
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (btn == null || img == null) return;

        bool unlocked = TalentTreeState.IsNodeUnlocked(node.nodeId);
        bool canUnlock = TalentTreeState.CanUnlock(node.nodeId);
        bool canAfford = TalentTreeState.TalentPoints >= node.Cost;

        btn.onClick.RemoveAllListeners();

        if (unlocked)
        {
            // 已解锁 - 唯一允许改颜色的状态
            btn.interactable = false;
            img.color = new Color(0.85f, 0.55f, 0.1f, 1f);
            if (tmp)
            {
                tmp.color = Color.white;
                tmp.text = node.displayName;
            }
        }
        else if (canUnlock && canAfford)
        {
            // 可解锁但不改颜色，只设interactable和绑定事件
            btn.interactable = true;
            if (tmp) tmp.text = $"{node.displayName}\n({node.Cost}点)";
            string capturedId = node.nodeId;
            btn.onClick.AddListener(() =>
            {
                if (TalentTreeState.TryUnlockNode(capturedId))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            });
        }
        else
        {
            // 未解锁 - 不改颜色，保持场景原样
            btn.interactable = false;
            if (tmp) tmp.text = $"{node.displayName}\n({node.Cost}点)";
        }
    }

    private TMP_Text FindTmp(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }
}
