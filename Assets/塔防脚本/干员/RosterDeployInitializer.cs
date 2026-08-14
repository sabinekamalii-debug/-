using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 开局自选阵容的「消费端」：战斗场景加载后，读取 RogueRuntimeState.SelectedRoster，
/// 先彻底销毁 BattleScene 里写死的旧 OperatorCard（旧模式每关固定干员残留，其
/// operatorData 引用已失效、名字是场景写死的旧文本），再按玩家选的阵容
/// 动态重建正确数量的部署卡，让选的人真正进战斗。
///
/// 设计意图：每个关卡都是随机干员、全看玩家自己有多少干员（SelectedRoster），
/// 不再是旧模式每关固定干员。SelectedRoster 为空时兜底用已解锁干员填充，保证不为空。
///
/// 全局自动生效：通过 [RuntimeInitializeOnLoadMethod] 监听每次场景加载，
/// 无需在 96 个关卡场景里逐一手动挂组件。
/// </summary>
public static class RosterDeployInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 同步（不等帧）处理：在场景第一帧渲染之前就把旧卡销毁并重建为玩家阵容，
        // 彻底避免「先显示场景写死的默认干员卡 → 下一帧才切换成选中干员」的闪变。
        // 不论是否已选人都执行：
        // - 选了人 → 按玩家阵容部署；
        // - 没选人 / 选人丢失 → 用已解锁干员兜底填充，避免旧模式写死固定干员残留。
        ApplyRoster();
        EnsureStarUpgradePanel();
        EnsureShovelTool();
    }

    /// <summary>
    /// 在战斗场景的「干员商店面板」底部自动注入一个「铲子」工具：
    /// 拖拽铲子到已部署的干员身上可铲除该干员（不返还费用、触发购买冷却）。
    /// 通过运行时自动注入，96 个战斗场景都无需手动摆 UI。
    /// </summary>
    private static void EnsureShovelTool()
    {
        var scroll = Object.FindFirstObjectByType<OperatorShopScroll>();
        if (scroll == null) return;
        // 非战斗场景（无部署系统）不需要铲子
        if (Object.FindFirstObjectByType<DeploymentManager>() == null) return;
        // 已存在则不重复创建（场景重载兜底）
        if (scroll.GetComponentInChildren<ShovelTool>(true) != null) return;

        Transform panel = scroll.transform;

        // 在 viewport 底部预留空间放铲子，避免与滚动的卡片重叠
        var viewportRect = scroll.viewport != null ? scroll.viewport : scroll.GetComponentInChildren<RectMask2D>()?.rectTransform;
        if (viewportRect != null)
        {
            Vector2 offsetMin = viewportRect.offsetMin;
            // 仅当底部还没预留过（避免重复叠加）时预留
            if (offsetMin.y < 1.0f)
                viewportRect.offsetMin = new Vector2(offsetMin.x, 1.15f);
        }

        var go = new GameObject("ShovelTool");
        go.transform.SetParent(panel, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(2.0f, 1.0f);
        rect.anchoredPosition = new Vector2(0f, 0.08f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
        bg.raycastTarget = true; // 根 Image 接收拖拽射线

        // 铲子文字（与部署卡同名/费用文字一致：WorldSpace 世界单位字号、TMP）
        var labelGo = new GameObject("文字");
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var labelTmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
        labelTmp.text = "铲除干员";
        labelTmp.fontSize = 0.6f;
        labelTmp.alignment = TMPro.TextAlignmentOptions.Center;
        labelTmp.color = new Color(1f, 0.78f, 0.35f);
        labelTmp.enableWordWrapping = false;
        labelTmp.raycastTarget = false;

        var shovel = go.AddComponent<ShovelTool>();
        // operatorLayer 留空：ShovelTool 运行时会自动取 DeploymentManager.operatorLayer
        // 不调用 DontDestroyOnLoad：铲子随商店面板按场景重建即可
    }

    /// <summary>
    /// 在战斗场景与金币商店场景自动挂上「干员升星」面板，
    /// 这样 96 个关卡场景都无需手动摆 UI 就能局内花金币升星。
    /// </summary>
    private static void EnsureStarUpgradePanel()
    {
        if (!OperatorStarRegistry.IsRunActive) return;
        if (Object.FindFirstObjectByType<OperatorStarUpgradePanel>() != null) return;

        // 战斗场景：有部署系统；商店场景：有金币商店。其余（剧情/菜单）不注入。
        bool isBattle = Object.FindFirstObjectByType<DeploymentManager>() != null;
        bool isShop = Object.FindFirstObjectByType<GoldShopController>() != null;
        if (!isBattle && !isShop) return;

        var go = new GameObject("[OperatorStarUpgradePanel]");
        var panel = go.AddComponent<OperatorStarUpgradePanel>();
        // 商店本就是静态界面，无需暂停；战斗中打开面板则暂停，避免看板时被偷家
        panel.pauseWhileOpen = isBattle;
    }

    private static void ApplyRoster()
    {
        var roster = RogueRuntimeState.SelectedRoster;
        int rosterCount = roster != null ? roster.Count : 0;

        // 运行时加载全部干员数据，建立 operatorName -> OperatorData 映射
        var allData = Resources.LoadAll<OperatorData>("");
        var byName = new Dictionary<string, OperatorData>();
        foreach (var d in allData)
        {
            if (d != null && !string.IsNullOrEmpty(d.operatorName) && !byName.ContainsKey(d.operatorName))
                byName[d.operatorName] = d;
        }

        // 找到部署卡容器（OperatorShopScroll.content，没有则取任意旧卡的父级）
        var scroll = Object.FindFirstObjectByType<OperatorShopScroll>();
        Transform content = scroll != null ? scroll.content : null;
        if (content == null)
        {
            var anyCards = Object.FindObjectsOfType<OperatorCard>(true);
            if (anyCards.Length > 0) content = anyCards[0].transform.parent;
        }
        if (content == null)
        {
            Debug.LogWarning("[RosterDeploy] 未找到部署卡容器（OperatorShopScroll.content），跳过。");
            return;
        }

        // 1) 彻底销毁场景里所有旧 OperatorCard（旧模式每关固定干员的残留，
        //    其 operatorData 引用已失效、名字是写死的旧文本），避免显示错乱卡。
        //    先 SetActive(false) 立即隐藏：Destroy 延迟到帧末，若只 Destroy 旧卡会多渲染一帧。
        var oldCards = content.GetComponentsInChildren<OperatorCard>(true);
        foreach (var c in oldCards)
        {
            c.gameObject.SetActive(false);
            Object.Destroy(c.gameObject);
        }

        if (rosterCount == 0)
        {
            // 兜底：没选人时，用当前已解锁（isInitialAvailable）的干员填充，
            // 保证部署栏不为空（符合「看玩家有多少干员」的设计）。
            var fallback = new List<OperatorData>();
            foreach (var d in allData)
                if (d != null && d.isInitialAvailable) fallback.Add(d);
            if (fallback.Count == 0) fallback.AddRange(allData);
            var names = fallback.ConvertAll(d => d.operatorName);
            Debug.LogWarning($"[RosterDeploy] SelectedRoster 为空，已用 {names.Count} 个可用干员兜底填充部署栏。");
            BuildCards(content, names, byName);
            return;
        }

        // 2) 按玩家选的阵容动态重建部署卡
        BuildCards(content, new List<string>(roster), byName);
    }

    /// <summary> 在 content 下按顺序为 names 里的每个干员生成一张部署卡。
    /// 复用 BattleScene「干员商店面板」里静态干员卡的既有布局约定：卡片锚点在
    /// content 顶部(0.5,1)、pivot(0.5,0.5)，每张从上往下按 (cardHeight+cardSpacing)
    /// 纵向排布；文字用 WorldSpace 世界单位字号（名字 0.5 / 费用 0.55）。
    /// 注意：TMP 组件必须用具体类 TextMeshProUGUI，不能用抽象基类 TMP_Text，
    /// 否则 AddComponent 失败导致名字/费用永远无法显示。 </summary>
    private static void BuildCards(Transform content, List<string> names, Dictionary<string, OperatorData> byName)
    {
        int built = 0;
        int miss = 0;
        // 与 BattleScene 静态干员卡片一致：首卡 y=-1.1，之后每张下移 2.35（cardHeight 2.2 + spacing 0.15）。
        float step = 2.35f;
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name)) continue;
            if (!byName.TryGetValue(name, out var data))
            {
                miss++;
                Debug.LogWarning($"[RosterDeploy] 阵容干员「{name}」在 Resources/OperatorData 中找不到对应数据，跳过。");
                continue;
            }

            var go = new GameObject("Card_" + name);
            go.transform.SetParent(content, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2f, 2.2f);
            rect.anchoredPosition = new Vector2(0f, -1.1f - i * step); // 关键：纵向堆叠，否则全部重叠

            // 卡片背景
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.11f, 0.2f, 0.95f);
            bgImg.raycastTarget = true; // 根 Image 负责接收拖拽射线，子物体一律不拦截

            var card = go.AddComponent<OperatorCard>();
            // 关键：动态卡显式绑定滚动面板，避免 OnBeginDrag 时 GetComponentInParent 找不到
            // OperatorShopScroll（_shopScroll 为 null 会误入「无面板」分支，被 Time.timeScale==0 拦截 → 无法拖拽）
            var ownerScroll = content.GetComponentInParent<OperatorShopScroll>();
            if (ownerScroll != null) card.BindShopScroll(ownerScroll);

            // 立绘（ApplyRosterOperator 会替换为 data.icon）
            var iconGo = new GameObject("立绘");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.04f, 0.22f);
            iconRect.anchorMax = new Vector2(0.96f, 0.96f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.offsetMin = Vector2.zero; iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false; // 不拦截拖拽射线
            card.characterIcon = iconImg;

            // 分隔线（立绘与名字之间）
            var sepGo = new GameObject("分隔线");
            sepGo.transform.SetParent(go.transform, false);
            var sepRect = sepGo.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.06f, 0.18f);
            sepRect.anchorMax = new Vector2(0.94f, 0.2f);
            sepRect.offsetMin = Vector2.zero; sepRect.offsetMax = Vector2.zero;
            var sepImg = sepGo.AddComponent<Image>();
            sepImg.color = new Color(0.75f, 0.6f, 0.35f, 1f);
            sepImg.raycastTarget = false; // 不拦截拖拽射线

            // 名字文本（必须有名为「名字」的子物体，RefreshNameAndCost 按此查找）
            var nameGo = new GameObject("名字");
            nameGo.transform.SetParent(go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.06f, 0.02f); nameRect.anchorMax = new Vector2(0.65f, 0.2f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.offsetMin = Vector2.zero; nameRect.offsetMax = Vector2.zero;
            var nameTmp = nameGo.AddComponent<TMPro.TextMeshProUGUI>();
            nameTmp.fontSize = 0.5f; // WorldSpace 世界单位字号
            nameTmp.alignment = TMPro.TextAlignmentOptions.Center;
            nameTmp.color = new Color(0.92f, 0.92f, 0.95f);
            nameTmp.enableWordWrapping = false;
            card.nameText = nameTmp;

            // 费用文本（必须有名为「费用文字」的子物体，RefreshNameAndCost 按此查找）
            var costGo = new GameObject("费用文字");
            costGo.transform.SetParent(go.transform, false);
            var costRect = costGo.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.65f, 0.02f); costRect.anchorMax = new Vector2(0.96f, 0.2f);
            costRect.pivot = new Vector2(0.5f, 0.5f);
            costRect.offsetMin = Vector2.zero; costRect.offsetMax = Vector2.zero;
            var costTmp = costGo.AddComponent<TMPro.TextMeshProUGUI>();
            costTmp.fontSize = 0.55f; // WorldSpace 世界单位字号
            costTmp.alignment = TMPro.TextAlignmentOptions.Center;
            costTmp.color = new Color(1f, 0.82f, 0.15f);
            card.costText = costTmp;

            card.ApplyRosterOperator(data);
            built++;
        }
        Debug.Log($"[RosterDeploy] 已按玩家阵容部署 {built} 张干员卡（缺失 {miss}）。");
        var sb = content.GetComponentInParent<OperatorShopScroll>();
        Debug.Log($"[RosterDeploy-DIAG] content名={content.name}, content父={(content.parent != null ? content.parent.name : "NULL")}, " +
                  $"向上找到OperatorShopScroll={(sb != null ? sb.name : "NULL")}, " +
                  $"首个动态卡layer={(content.childCount > 0 ? LayerMask.LayerToName(content.GetChild(0).gameObject.layer) : "?")}");
    }

}
