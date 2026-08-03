using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 关卡地图总控：StS风格分叉路径地图。
///
/// 功能：
/// - 大局驱动：从 RogueRuntimeState.CurrentActConfig 读取地图配置
/// - StS地图生成：种子化生成分叉路径图（路径分叉+合并）
/// - 动态按钮：根据图节点创建按钮，按楼层/列定位
/// - 连线绘制：节点间绘制路径连线
/// - 分叉解锁：完成节点后解锁其连接的下一层节点
/// </summary>
public class LevelMapController : MonoBehaviour
{
    public static LevelMapController Instance { get; private set; }

    [Header("关卡随机配置")]
    [Tooltip("拖入关卡随机配置（灵活，支持自定义区间）")]
    public LevelRandomConfig levelRandomConfig;

    [Header("关卡随机配置（简化版）")]
    [Tooltip("拖入简单关卡配置。LevelRandomConfig 为空时才使用这个。")]
    public SimpleLevelRandomConfig simpleLevelRandomConfig;

    [Header("关卡类型图标")]
    [Tooltip("各节点类型的图标配置")]
    public LevelTypeConfig levelTypeConfig;

    [Header("划动区域（不填则自动找）")]
    public ScrollRect scrollRect;

    [Header("地图布局参数")]
    [Tooltip("每层之间的垂直间距")]
    public float floorSpacing = 440f;
    [Tooltip("每列之间的水平间距")]
    public float columnSpacing = 200f;
    [Tooltip("节点按钮大小")]
    public Vector2 buttonSize = new Vector2(120f, 120f);

    [Header("编辑器调试")]
    [Tooltip("勾选时，每次进入场景都会清空进度。默认关闭。")]
    [SerializeField] bool clearProgressOnEnterInEditor = false;

    private MapGraph _graph;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        RogueRuntimeState.InitIfNeeded();

#if UNITY_EDITOR
        if (clearProgressOnEnterInEditor)
        {
            LevelProgress.ClearAll();
            LevelProgress.ClearNodeProgress();
        }
#endif

        // 直接运行 plot 场景测试时，CurrentActId 可能为 0，自动选默认大局
        if (RogueRuntimeState.CurrentActId <= 0)
        {
            var defaultAct = ActRegistry.GetActConfig(2) ?? ActRegistry.GetFirstAct();
            if (defaultAct != null)
                RogueRuntimeState.StartAct(defaultAct.actId);
        }

        // 获取或生成地图图
        _graph = RogueRuntimeState.CurrentMapGraph;
        if (_graph == null)
        {
            // 没有活跃 run → 开始新 run（会生成图）
            if (!RogueRuntimeState.HasActiveRun)
            {
                RogueRuntimeState.StartRunIfNeeded();
            }
            // 从种子重新生成（返回场景时）
            if (_graph == null && RogueRuntimeState.RunSeed != 0)
            {
                _graph = StSMapGenerator.Generate(RogueRuntimeState.RunSeed, RogueRuntimeState.CurrentActConfig);
                RogueRuntimeState.SetMapGraph(_graph);
            }
        }

        if (_graph != null)
        {
            LevelProgress.SetMapGraph(_graph);
            // 确保起点节点标记完成（解锁第一层）
            var startNode = _graph.GetStartNode();
            if (startNode != null && !LevelProgress.IsNodeCompleted(startNode.nodeId))
                LevelProgress.MarkNodeCompleted(startNode.nodeId);
            BuildMapUI();
        }

        CheckAndApplyReturnContext();
    }

    /// <summary>
    /// 构建地图 UI：清除旧按钮 → 创建节点按钮 → 绘制连线 → 设置滚动区域。
    /// </summary>
    void BuildMapUI()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect == null) return;

        var content = scrollRect.content;
        if (content == null) return;

        // 清除旧按钮（保留 MapBackground 和 Lines）
        var existingButtons = content.GetComponentsInChildren<LevelNodeButton>(true);
        foreach (var btn in existingButtons)
        {
            if (btn != null)
                DestroyImmediate(btn.gameObject);
        }

        // 设置 Content 高度（保持现有 pivot 和宽度不变）
        float contentHeight = _graph.floorCount * floorSpacing + 200f;
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);

        // 同步 MapBackground 尺寸匹配 Content（左右拉伸匹配宽度，高度手动设）
        var bg = content.Find("MapBackground");
        if (bg != null)
        {
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(1f, 1f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(0f, -contentHeight / 2f);
            bgRect.sizeDelta = new Vector2(0f, contentHeight);
        }

        // 确保有 Lines 容器
        var linesObj = content.Find("Lines");
        if (linesObj == null)
        {
            var linesGo = new GameObject("Lines");
            linesGo.transform.SetParent(content, false);
            linesObj = linesGo.transform;
        }
        // 设置 Lines 的 anchor 匹配 Content pivot
        var linesRect = linesObj as RectTransform;
        if (linesRect != null)
        {
            linesRect.anchorMin = new Vector2(0.5f, 1f);
            linesRect.anchorMax = new Vector2(0.5f, 1f);
            linesRect.pivot = new Vector2(0.5f, 1f);
            linesRect.anchoredPosition = Vector2.zero;
            linesRect.sizeDelta = Vector2.zero;
        }
        // 清除旧连线
        for (int i = linesObj.childCount - 1; i >= 0; i--)
            DestroyImmediate(linesObj.GetChild(i).gameObject);

        // 创建所有节点按钮
        foreach (var node in _graph.allNodes)
        {
            CreateNodeButton(node, content);
        }

        // 使用 LevelLineConnector 生成优美连线（渐变色+辉光+箭头+流动光点）
        var lineConnector = linesObj.GetComponent<LevelLineConnector>();
        if (lineConnector != null)
        {
            lineConnector.enabled = true;
            lineConnector.GenerateLines();
        }
        else
        {
            // 没有 LevelLineConnector 时用简陋连线回退
            DrawConnections(_graph, linesObj);
        }

        // Lines 放在 MapBackground 之后
        int bgIndex = bg != null ? bg.GetSiblingIndex() : 0;
        linesObj.SetSiblingIndex(bgIndex + 1);
    }

    /// <summary> 创建单个节点按钮。 </summary>
    void CreateNodeButton(MapNodeData node, Transform parent)
    {
        var go = new GameObject($"Node_{node.nodeId}_{node.nodeType}");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        // anchor 设为 (0.5, 1.0) 匹配 Content pivot，这样 anchoredPosition 直接是相对顶部中心的偏移
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Vector2 pos = GetNodePosition(node);
        rect.anchoredPosition = pos;
        rect.sizeDelta = buttonSize;

        var image = go.AddComponent<Image>();
        image.raycastTarget = true;

        // 先设置 sprite，再添加 LevelNodeButton（Awake 中会读取 iconImage.sprite）
        Sprite typeSprite = GetNodeSprite(node.nodeType);
        if (typeSprite != null)
            image.sprite = typeSprite;

        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.disabledColor = new Color(colors.disabledColor.r, colors.disabledColor.g, colors.disabledColor.b, 1f);
        button.colors = colors;

        // 添加 LevelNodeButton 组件并初始化
        var nodeButton = go.AddComponent<LevelNodeButton>();
        nodeButton.levelTypeConfig = levelTypeConfig;

        // 设置 iconImage 字段（反射，因为它是 private/serialized）
        var iconField = typeof(LevelNodeButton).GetField("iconImage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        iconField?.SetValue(nodeButton, image);

        var nameTextField = typeof(LevelNodeButton).GetField("nameText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (nameTextField != null && nameTextField.GetValue(nodeButton) == null)
            nameTextField.SetValue(nodeButton, image.GetComponent<TMP_Text>());

        // 框架 Image
        var frameField = typeof(LevelNodeButton).GetField("frameImage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 初始化节点数据
        nodeButton.Init(node);
    }

    /// <summary> 计算节点的 UI 坐标（适配 Content pivot=(0.5,1.0) 顶部中心，Y 向下为负）。 </summary>
    Sprite GetNodeSprite(LevelType type)
    {
        if (levelTypeConfig == null) return null;
        switch (type)
        {
            case LevelType.Shop: return levelTypeConfig.shopIcon;
            case LevelType.Elite: return levelTypeConfig.eliteIcon;
            case LevelType.Boss: return levelTypeConfig.bossIcon;
            case LevelType.RandomEvent: return levelTypeConfig.randomEventIcon;
            case LevelType.Rest: return levelTypeConfig.restIcon;
            case LevelType.Start: return levelTypeConfig.startIcon != null ? levelTypeConfig.startIcon : levelTypeConfig.normalBattleIcon;
            default: return levelTypeConfig.normalBattleIcon;
        }
    }

    Vector2 GetNodePosition(MapNodeData node)
    {
        float x = (node.column - (_graph.maxColumns - 1) / 2f) * columnSpacing;
        // pivot=(0.5,1.0): Y=0 在顶部，向下为负。floor 0（起点）在最底部，Boss 在最顶部。
        float totalHeight = (_graph.floorCount + 1) * floorSpacing + 200f;
        // Boss 在顶部（Y=-100），起点在底部（Y=-(totalHeight-100)）
        float y = -100f - (_graph.floorCount - node.floor) * floorSpacing;
        return new Vector2(x, y);
    }

    /// <summary> 绘制节点间的连线。 </summary>
    void DrawConnections(MapGraph graph, Transform linesParent)
    {
        foreach (var node in graph.allNodes)
        {
            foreach (var targetId in node.nextNodeIds)
            {
                var target = graph.GetNode(targetId);
                if (target == null) continue;
                DrawLine(linesParent, node, target);
            }
        }
    }

    /// <summary> 绘制单条连线（使用 Image 拉伸+旋转）。 </summary>
    void DrawLine(Transform parent, MapNodeData from, MapNodeData to)
    {
        var go = new GameObject($"Line_{from.nodeId}_{to.nodeId}");
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.7f, 0.7f, 0.8f, 0.8f);
        image.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        // anchor 匹配 Content pivot
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 pos1 = GetNodePosition(from);
        Vector2 pos2 = GetNodePosition(to);

        Vector2 mid = (pos1 + pos2) / 2f;
        float distance = Vector2.Distance(pos1, pos2);
        float angle = Mathf.Atan2(pos2.y - pos1.y, pos2.x - pos1.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = mid;
        rect.sizeDelta = new Vector2(distance, 6f);
        rect.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary> 刷新所有连线的颜色状态。 </summary>
    void RefreshLineStates()
    {
        if (_graph == null) return;
        var content = scrollRect?.content;
        if (content == null) return;

        var linesParent = content.Find("Lines");
        if (linesParent == null) return;

        foreach (Transform lineObj in linesParent)
        {
            var image = lineObj.GetComponent<Image>();
            if (image == null) continue;

            // 解析连线名：Line_fromId_toId
            string name = lineObj.name;
            string[] parts = name.Split('_');
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[1], out int fromId) || !int.TryParse(parts[2], out int toId))
                continue;

            bool fromCompleted = LevelProgress.IsNodeCompleted(fromId);
            bool toCompleted = LevelProgress.IsNodeCompleted(toId);

            if (fromCompleted && toCompleted)
            {
                // 已走过的路径：暗绿色
                image.color = new Color(0.3f, 0.5f, 0.3f, 0.5f);
            }
            else if (fromCompleted)
            {
                // 可走路径：亮白色
                image.color = new Color(0.9f, 0.9f, 1.0f, 0.7f);
            }
            else
            {
                // 未解锁路径：暗灰色
                image.color = new Color(0.3f, 0.3f, 0.35f, 0.4f);
            }
        }
    }

    void CheckAndApplyReturnContext()
    {
        var context = LevelSceneLoadContext.GetAndClear();
        if (context == null) return;

        switch (context.loadType)
        {
            case LevelSceneLoadType.FromSelection:
                break;
            case LevelSceneLoadType.FromRetry:
                break;
            case LevelSceneLoadType.FromVictory:
                break;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RefreshAllLevelButtons()
    {
        if (scrollRect == null || scrollRect.content == null) return;
        var buttons = scrollRect.content.GetComponentsInChildren<LevelNodeButton>(true);
        if (buttons == null) return;
        foreach (var btn in buttons)
        {
            if (btn != null)
            {
                var method = btn.GetType().GetMethod("RefreshLockState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null) method.Invoke(btn, null);
            }
        }
        RefreshLineStates();
    }

    void Start()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        // 确保连线已绘制（Awake 中可能因时序问题未执行）
        if (_graph != null && scrollRect != null && scrollRect.content != null)
        {
            var linesObj = scrollRect.content.Find("Lines");
            if (linesObj != null)
            {
                var connector = linesObj.GetComponent<LevelLineConnector>();
                if (connector != null && connector.enabled)
                {
                    connector.GenerateLines();
                    var bg = scrollRect.content.Find("MapBackground");
                    int bgIndex = bg != null ? bg.GetSiblingIndex() : 0;
                    linesObj.SetSiblingIndex(bgIndex + 1);
                }
            }
        }

        // 滚动到底部（起点视野）
        if (scrollRect != null)
            scrollRect.normalizedPosition = new Vector2(0.5f, 0f);

        ForceInputModuleActive();
        RefreshAllLevelButtons();
    }

    private static void ForceInputModuleActive()
    {
#if UNITY_EDITOR
        if (EventSystem.current != null)
        {
            var standalone = EventSystem.current.currentInputModule as StandaloneInputModule;
            if (standalone != null)
                standalone.forceModuleActive = true;
        }

        if (InputSystem.settings != null)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
#endif
    }
}
