#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 编辑器工具：在编辑模式下生成 StS 分叉路径地图节点。
/// 菜单：Tools → 生成StS分叉地图
/// </summary>
public static class StSMapEditorGenerator
{
    [MenuItem("Tools/生成StS分叉地图")]
    public static void GenerateMapInEditor()
    {
        var lmc = Object.FindFirstObjectByType<LevelMapController>();
        if (lmc == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 LevelMapController", "确定");
            return;
        }

        var typeField = typeof(LevelMapController).GetField("levelTypeConfig");
        var typeConfig = typeField?.GetValue(lmc) as LevelTypeConfig;
        if (typeConfig == null)
        {
            EditorUtility.DisplayDialog("错误", "LevelMapController 上未配置 levelTypeConfig", "确定");
            return;
        }

        var srField = typeof(LevelMapController).GetField("scrollRect");
        var sr = srField?.GetValue(lmc) as ScrollRect;
        if (sr == null || sr.content == null)
        {
            EditorUtility.DisplayDialog("错误", "未配置 scrollRect 或 Content", "确定");
            return;
        }

        // 获取或生成图
        RogueRuntimeState.InitIfNeeded();
        var actConfig = RogueRuntimeState.CurrentActConfig;
        if (actConfig == null)
        {
            if (RogueRuntimeState.CurrentActId <= 0)
            {
                var defaultAct = ActRegistry.GetActConfig(2) ?? ActRegistry.GetFirstAct();
                if (defaultAct != null)
                    RogueRuntimeState.StartAct(defaultAct.actId);
            }
            actConfig = RogueRuntimeState.CurrentActConfig;
        }

        int seed = RogueRuntimeState.HasActiveRun && RogueRuntimeState.RunSeed != 0
            ? RogueRuntimeState.RunSeed
            : 12345; // 固定编辑器预览种子

        var graph = StSMapGenerator.Generate(seed, actConfig);
        RogueRuntimeState.SetMapGraph(graph);
        LevelProgress.SetMapGraph(graph);

        var content = sr.content;

        // 清除旧按钮（保留 MapBackground）
        var oldButtons = content.GetComponentsInChildren<LevelNodeButton>(true);
        foreach (var btn in oldButtons)
        {
            if (btn != null)
                Object.DestroyImmediate(btn.gameObject);
        }

        // 设置 Content 高度
        float floorSpacing = (float)typeof(LevelMapController).GetField("floorSpacing").GetValue(lmc);
        float columnSpacing = (float)typeof(LevelMapController).GetField("columnSpacing").GetValue(lmc);
        var buttonSize = (Vector2)typeof(LevelMapController).GetField("buttonSize").GetValue(lmc);
        float contentHeight = graph.floorCount * floorSpacing + 200f;
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
            Object.DestroyImmediate(linesObj.GetChild(i).gameObject);

        // 创建所有节点按钮
        foreach (var node in graph.allNodes)
        {
            CreateNodeButton(node, content, typeConfig, lmc, floorSpacing, columnSpacing, buttonSize, graph);
        }

        // 使用 LevelLineConnector 生成优美连线
        var lineConnector = linesObj.GetComponent<LevelLineConnector>();
        if (lineConnector != null)
        {
            lineConnector.enabled = true;
            lineConnector.GenerateLines();
        }
        else
        {
            DrawConnections(graph, linesObj, lmc, floorSpacing, columnSpacing, graph);
        }

        // Lines 放在 MapBackground 之后
        int bgIndex = bg != null ? bg.GetSiblingIndex() : 0;
        linesObj.SetSiblingIndex(bgIndex + 1);

        // 编辑模式下滚动到底部（起点视野）
        if (sr != null)
        {
            sr.normalizedPosition = new Vector2(0.5f, 0f);
            UnityEditor.EditorUtility.SetDirty(sr);
        }

        // 保存场景
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        Debug.Log($"[StSMapEditorGenerator] 生成了 {graph.allNodes.Count} 个节点，{CountConnections(graph)} 条连线");
    }

    static void CreateNodeButton(MapNodeData node, Transform parent, LevelTypeConfig typeConfig,
        LevelMapController lmc, float floorSpacing, float columnSpacing, Vector2 buttonSize, MapGraph graph)
    {
        var go = new GameObject($"Node_{node.nodeId}_{node.nodeType}");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        // anchor 设为 (0.5, 1.0) 匹配 Content pivot
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = GetNodePosition(node, lmc, floorSpacing, columnSpacing, graph);
        rect.sizeDelta = buttonSize;

        var image = go.AddComponent<Image>();
        image.raycastTarget = true;

        // 编辑模式下直接设置 sprite
        Sprite typeSprite = GetTypeSprite(node.nodeType, typeConfig);
        if (typeSprite != null)
            image.sprite = typeSprite;

        // Start 节点用普通战斗图标如果没有 startIcon
        if (node.nodeType == LevelType.Start && typeSprite == null)
            image.sprite = typeConfig.normalBattleIcon;

        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.disabledColor = new Color(colors.disabledColor.r, colors.disabledColor.g, colors.disabledColor.b, 1f);
        button.colors = colors;

        var nodeButton = go.AddComponent<LevelNodeButton>();
        nodeButton.levelTypeConfig = typeConfig;

        // 设置 iconImage
        var iconField = typeof(LevelNodeButton).GetField("iconImage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        iconField?.SetValue(nodeButton, image);

        nodeButton.Init(node);
    }

    static Sprite GetTypeSprite(LevelType type, LevelTypeConfig config)
    {
        if (config == null) return null;
        switch (type)
        {
            case LevelType.Shop: return config.shopIcon;
            case LevelType.Elite: return config.eliteIcon;
            case LevelType.Boss: return config.bossIcon;
            case LevelType.RandomEvent: return config.randomEventIcon;
            case LevelType.Rest: return config.restIcon;
            case LevelType.Start: return config.startIcon;
            default: return config.normalBattleIcon;
        }
    }

    static Vector2 GetNodePosition(MapNodeData node, LevelMapController lmc,
        float floorSpacing, float columnSpacing, MapGraph graph)
    {
        float x = (node.column - (graph.maxColumns - 1) / 2f) * columnSpacing;
        float y = -100f - (graph.floorCount - node.floor) * floorSpacing;
        return new Vector2(x, y);
    }

    static void DrawConnections(MapGraph graph, Transform linesParent,
        LevelMapController lmc, float floorSpacing, float columnSpacing, MapGraph graphRef)
    {
        foreach (var node in graph.allNodes)
        {
            foreach (var targetId in node.nextNodeIds)
            {
                var target = graph.GetNode(targetId);
                if (target == null) continue;
                DrawLine(linesParent, node, target, lmc, floorSpacing, columnSpacing, graphRef);
            }
        }
    }

    static void DrawLine(Transform parent, MapNodeData from, MapNodeData to,
        LevelMapController lmc, float floorSpacing, float columnSpacing, MapGraph graph)
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

        Vector2 pos1 = GetNodePosition(from, lmc, floorSpacing, columnSpacing, graph);
        Vector2 pos2 = GetNodePosition(to, lmc, floorSpacing, columnSpacing, graph);

        Vector2 mid = (pos1 + pos2) / 2f;
        float distance = Vector2.Distance(pos1, pos2);
        float angle = Mathf.Atan2(pos2.y - pos1.y, pos2.x - pos1.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = mid;
        rect.sizeDelta = new Vector2(distance, 6f);
        rect.rotation = Quaternion.Euler(0, 0, angle);
    }

    static int CountConnections(MapGraph graph)
    {
        int count = 0;
        foreach (var n in graph.allNodes) count += n.nextNodeIds.Count;
        return count;
    }
}
#endif
