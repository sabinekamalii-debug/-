using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 开局自选阵容的「消费端」：战斗场景加载后，读取 RogueRuntimeState.SelectedRoster，
/// 把场景里静态摆放的部署卡片（OperatorCard）按你选的阵容重新绑定，
/// 让选的人真正进战斗，而不是被场景默认的干员覆盖（之前「选了等于白选」的 bug 根因）。
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
        // 没有选阵容（例如还没进 RogueEntry 选人）就不做任何事
        var roster = RogueRuntimeState.SelectedRoster;
        if (roster == null || roster.Count == 0) return;

        CoroutineRunner.Instance.StartCoroutine(ApplyRosterNextFrame());
        EnsureStarUpgradePanel();
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

    private static IEnumerator ApplyRosterNextFrame()
    {
        yield return null;

        var roster = RogueRuntimeState.SelectedRoster;
        if (roster == null || roster.Count == 0) yield break;

        // 运行时加载全部干员数据，建立 RegistryKey -> OperatorData 映射
        var allData = Resources.LoadAll<OperatorData>("");
        var byKey = new Dictionary<string, OperatorData>();
        foreach (var d in allData)
        {
            if (d != null && !string.IsNullOrEmpty(d.RegistryKey) && !byKey.ContainsKey(d.RegistryKey))
                byKey[d.RegistryKey] = d;
        }

        var cards = Object.FindObjectsOfType<OperatorCard>(true);
        var rosterSet = new HashSet<string>(roster);
        int bound = 0;

        // 1) 用场景里已有的卡片，按顺序绑定阵容前 bound 个干员
        for (int i = 0; i < cards.Length && bound < roster.Count; i++)
        {
            string key = roster[bound];
            if (byKey.TryGetValue(key, out var data))
            {
                cards[i].ApplyRosterOperator(data);
                cards[i].gameObject.SetActive(true);
                bound++;
            }
        }

        // 2) 阵容人数多于场景已有卡片数：克隆第一张卡片作为模板补齐
        if (bound < roster.Count && cards.Length > 0)
        {
            var template = cards[0];
            var parent = template.transform.parent;
            for (int k = bound; k < roster.Count; k++)
            {
                string key = roster[k];
                if (!byKey.TryGetValue(key, out var data)) continue;
                var go = Object.Instantiate(template.gameObject, parent, false);
                var card = go.GetComponent<OperatorCard>();
                if (card != null)
                {
                    card.ApplyRosterOperator(data);
                    go.SetActive(true);
                }
                bound++;
            }
        }

        // 3) 隐藏场景里多余的、未被阵容占用的原始卡片（重新取一次含克隆的完整列表）
        var allCards = Object.FindObjectsOfType<OperatorCard>(true);
        foreach (var c in allCards)
        {
            bool inRoster = c.operatorData != null && rosterSet.Contains(c.operatorData.RegistryKey);
            if (!inRoster) c.gameObject.SetActive(false);
        }
    }

    /// <summary> 承载协程的隐藏单例（静态类无法直接 StartCoroutine）。 </summary>
    private class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[RosterDeployInitializer]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<CoroutineRunner>();
                    Object.DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }
}
