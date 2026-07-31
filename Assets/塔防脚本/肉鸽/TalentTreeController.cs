using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 天赋树场景控制器（替代旧 SoulShopController）：
/// - 显示当前天赋点
/// - 4 个分支 × 5 节点的 UI 列表
/// - 点击节点解锁（消耗天赋点）
/// - 返回入口
/// UI 在场景中搭建好，本脚本只负责数据绑定和刷新。
/// </summary>
public class TalentTreeController : MonoBehaviour
{
    private TMP_Text _talentPointText;
    private Button _backBtn;

    private void Awake()
    {
        TalentTreeState.InitIfNeeded();
        RogueRuntimeState.InitIfNeeded();
        BindSceneObjects();
    }

    private void Start()
    {
        BindButtons();
        RefreshAll();
    }

    private void BindSceneObjects()
    {
        _talentPointText = FindTmp("TalentPointCount");
        _backBtn = FindButton("Btn_返回入口");
    }

    private void BindButtons()
    {
        if (_backBtn != null)
        {
            _backBtn.onClick.RemoveAllListeners();
            _backBtn.onClick.AddListener(OnBackClicked);
        }

        // 绑定每个节点的按钮
        foreach (var node in TalentTreeData.Nodes)
        {
            var btn = FindButton($"Node_{node.nodeId}");
            if (btn != null)
            {
                string capturedId = node.nodeId;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnNodeClicked(capturedId));
            }
        }
    }

    private void RefreshAll()
    {
        // 刷新天赋点
        if (_talentPointText != null)
            _talentPointText.text = $"天赋点: {TalentTreeState.TalentPoints}";

        // 刷新所有节点
        foreach (var node in TalentTreeData.Nodes)
        {
            RefreshNode(node);
        }
    }

    private void RefreshNode(TalentTreeData.NodeDef node)
    {
        var itemRoot = GameObject.Find($"Node_{node.nodeId}");
        if (itemRoot == null) return;

        bool unlocked = TalentTreeState.IsNodeUnlocked(node.nodeId);
        bool canUnlock = TalentTreeState.CanUnlock(node.nodeId);
        int cost = node.Cost;
        bool canAfford = TalentTreeState.TalentPoints >= cost;

        // 已解锁/可解锁：纯白文字
        // 点数不足：浅暖色文字
        // 前置未解锁：浅灰文字（略暗，暗示锁定但仍可读）
        Color activeText = new Color(1f, 1f, 1f, 1f);
        Color lockedText = new Color(0.82f, 0.82f, 0.88f, 1f);
        Color poorText = new Color(1f, 0.85f, 0.7f, 1f);

        // 刷新名称
        var nameText = FindTmpInChildren(itemRoot, "Name");
        if (nameText != null)
        {
            nameText.text = node.displayName;
            nameText.color = unlocked ? activeText : (canUnlock ? poorText : lockedText);
        }

        // 刷新描述
        var descText = FindTmpInChildren(itemRoot, "Desc");
        if (descText != null)
        {
            descText.text = node.description;
            descText.color = unlocked ? activeText : (canUnlock ? poorText : lockedText);
        }

        // 刷新费用/状态
        var costText = FindTmpInChildren(itemRoot, "Cost");
        if (costText != null)
        {
            if (unlocked)
                costText.text = "已解锁";
            else
                costText.text = $"{cost} 点";
            costText.color = unlocked ? activeText : (canUnlock ? poorText : lockedText);
        }

        // 刷新按钮
        var btn = itemRoot.GetComponent<Button>();
        if (btn == null) btn = FindButtonInParent(itemRoot.name, "Btn_解锁");
        if (btn != null)
        {
            var img = btn.GetComponent<Image>();

            if (unlocked)
            {
                // 已解锁 - 深绿
                btn.interactable = false;
                if (img != null) img.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                SetButtonText(btn, "已激活");
                SetButtonTextColor(btn, activeText);
            }
            else if (canUnlock && canAfford)
            {
                // 可解锁 - 亮绿
                btn.interactable = true;
                if (img != null) img.color = new Color(0.25f, 0.65f, 0.25f, 1f);
                SetButtonText(btn, $"{cost} 点");
                SetButtonTextColor(btn, activeText);
            }
            else if (canUnlock && !canAfford)
            {
                // 点数不足 - 暖琥珀色（比暗红更醒目，暗示"快了"）
                btn.interactable = false;
                if (img != null) img.color = new Color(0.55f, 0.38f, 0.12f, 1f);
                SetButtonText(btn, $"{cost} 点");
                SetButtonTextColor(btn, poorText);
            }
            else
            {
                // 前置未解锁 - 石板蓝灰，可见但不抢眼
                btn.interactable = false;
                if (img != null) img.color = new Color(0.38f, 0.4f, 0.48f, 1f);
                SetButtonText(btn, $"{cost} 点");
                SetButtonTextColor(btn, lockedText);
            }
        }
    }

    private void OnNodeClicked(string nodeId)
    {
        if (TalentTreeState.TryUnlockNode(nodeId))
            RefreshAll();
    }

    private void OnBackClicked()
    {
        VideoSceneLoader.LoadScene(SceneNames.RogueEntry);
    }

    // ─────────────────────────────────────────────
    //  辅助查找（与 SoulShopController 风格一致）
    // ─────────────────────────────────────────────

    private TMP_Text FindTmp(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    private TMP_Text FindTmpInChildren(GameObject root, string goName)
    {
        var transform = root.transform.Find(goName);
        return transform != null ? transform.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private Button FindButtonInParent(string parentName, string btnName)
    {
        var parent = GameObject.Find(parentName);
        if (parent == null) return null;
        var transform = parent.transform.Find(btnName);
        return transform != null ? transform.GetComponent<Button>() : null;
    }

    private static void SetButtonText(Button btn, string text)
    {
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text;
    }

    private static void SetButtonTextColor(Button btn, Color color)
    {
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.color = color;
    }
}
