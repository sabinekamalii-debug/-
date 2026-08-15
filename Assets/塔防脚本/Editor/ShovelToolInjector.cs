#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 编辑器菜单项：把「铲子」工具作为持久 GameObject 插入到当前打开的 BattleScene（唯一有效战斗场景）
/// 的干员商店面板底部。
/// ⚠️ 只操作 BattleScene，不操作 Assets/Scenes/level/ 下的 [旧版备用] level 1.unity ~ [旧版备用] level 11.unity，
/// 因为那些是【已淘汰的旧架构场景】，真实战斗走 BattleScene + LevelConfig 数据驱动。
///
/// 菜单路径：Tools/战斗场景/仅在当前场景插入铲子
///
/// 运行时注入（RosterDeployInitializer.EnsureShovelTool）同时保留，
/// 作为"忘记执行本菜单"时的兜底（进入 Play 模式后也会自动出现）。
/// </summary>
public static class ShovelToolInjector
{
    private const string ShovelName = "ShovelTool";

    [MenuItem("Tools/战斗场景/仅在当前场景插入铲子")]
    public static void InjectIntoCurrentScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("错误", "当前没有打开的有效场景。请先打开 BattleScene。", "好的");
            return;
        }
        // 仅提示，不硬性拦截：允许在任意有 OperatorShopScroll 的场景里操作
        if (scene.name != "BattleScene")
        {
            if (!EditorUtility.DisplayDialog("场景不是 BattleScene",
                $"当前打开的是「{scene.name}」，真正的战斗场景是 BattleScene（其它 level 场景都是旧淘汰架构）。\n确认仍然要在当前场景插入吗？", "仍然插入", "取消"))
                return;
        }

        bool changed = InjectShovelIntoOpenedScene(out bool alreadyExists);
        if (alreadyExists)
        {
            Debug.Log("[ShovelInjector] 当前场景商店面板里已存在 ShovelTool，跳过。");
            EditorUtility.DisplayDialog("已存在", "当前场景商店面板里已经有 ShovelTool 了，无需重复插入。", "好的");
            return;
        }
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ShovelInjector] ✓ 已在 {scene.name} 插入铲子并保存场景。");
            EditorUtility.DisplayDialog("完成",
                $"已在 {scene.name} 的干员商店面板底部插入铲子（ShovelTool）。\n" +
                "用法：拖拽铲子图标到已部署的干员身上即可铲除（不退费，触发对应卡片购买冷却）。", "好的");
        }
        else
        {
            EditorUtility.DisplayDialog("未找到干员商店面板",
                "当前场景找不到 OperatorShopScroll（干员商店面板）。\n" +
                "请确认当前打开的是 BattleScene 且商店面板对象里挂了 OperatorShopScroll 组件。", "好的");
        }
    }

    /// <summary>在当前已打开的场景里把铲子注入到 OperatorShopScroll 下。返回 (是否修改, 是否已存在)。</summary>
    private static bool InjectShovelIntoOpenedScene(out bool alreadyExists)
    {
        alreadyExists = false;
        var scroll = Object.FindFirstObjectByType<OperatorShopScroll>();
        if (scroll == null) return false;

        Transform panel = scroll.transform;
        if (panel.Find(ShovelName) != null)
        {
            alreadyExists = true;
            return false;
        }

        // --- 1) 为 viewport 底部预留 1.15 单位空间，避免滚动卡片和铲子重叠 ---
        RectTransform viewportRect = scroll.viewport != null ? scroll.viewport :
            scroll.GetComponentInChildren<RectMask2D>()?.rectTransform;
        if (viewportRect != null)
        {
            Vector2 offsetMin = viewportRect.offsetMin;
            if (offsetMin.y < 1.0f)
            {
                Undo.RecordObject(viewportRect, "Reserve Shovel Space");
                viewportRect.offsetMin = new Vector2(offsetMin.x, 1.15f);
                EditorUtility.SetDirty(viewportRect);
            }
        }

        // --- 2) 创建 ShovelTool 根 GameObject（挂在 OperatorShopScroll 下，不进滚动 Content） ---
        var go = new GameObject(ShovelName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel, false);
        Undo.RegisterCreatedObjectUndo(go, "Create Shovel Tool");

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(2.0f, 1.0f);
        rect.anchoredPosition = new Vector2(0f, 0.08f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
        bg.raycastTarget = true;

        // --- 3) 文字子物体（TMP，WorldSpace 单位字号与部署卡一致） ---
        var labelGo = new GameObject("文字", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        Undo.RegisterCreatedObjectUndo(labelGo, "Create Shovel Label");

        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelTmp = labelGo.GetComponent<TMPro.TextMeshProUGUI>();
        labelTmp.text = "铲除干员";
        labelTmp.fontSize = 0.6f;
        labelTmp.alignment = TMPro.TextAlignmentOptions.Center;
        labelTmp.color = new Color(1f, 0.78f, 0.35f);
        labelTmp.enableWordWrapping = false;
        labelTmp.raycastTarget = false;

        // --- 4) 挂上 ShovelTool 脚本（operatorLayer 运行时会自动读 DeploymentManager.operatorLayer） ---
        var shovel = go.AddComponent<ShovelTool>();
        EditorUtility.SetDirty(shovel);

        return true;
    }
}
#endif
