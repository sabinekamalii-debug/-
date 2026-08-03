using UnityEditor;
using UnityEngine;

public static class OperatorSelectionPanelEditor
{
    [MenuItem("Tools/干员选择面板/重建面板")]
    public static void RebuildPanel()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Canvas not found in scene!");
            return;
        }

        // Remove old panel if exists
        var old = canvas.transform.Find("OperatorSelectionPanel");
        if (old != null)
        {
            Debug.Log("Removing old OperatorSelectionPanel...");
            Object.DestroyImmediate(old.gameObject);
        }

        // Create fresh panel
        var go = new GameObject("OperatorSelectionPanel");
        go.transform.SetParent(canvas.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.AddComponent<OperatorSelectionPanel>();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("OperatorSelectionPanel created. Enter Play mode to see the UI.");
    }

    [MenuItem("Tools/干员选择面板/设置干员星级")]
    public static void SetOperatorStarRatings()
    {
        string[] guids = AssetDatabase.FindAssets("t:OperatorData");
        var starMap = new System.Collections.Generic.Dictionary<string, int>
        {
            { "突击先锋", 1 }, { "游击先锋", 1 }, { "光波", 1 }, { "先锋测试", 1 },
            { "拳师", 1 }, { "地面射手", 1 },
            { "斥候先锋", 2 }, { "风暴先锋", 2 }, { "猎手先锋", 2 }, { "驻防先锋", 2 },
            { "坚守先锋", 2 }, { "连弩", 2 }, { "净化", 2 }, { "女战士", 2 },
            { "钩爪", 2 }, { "晶", 2 },
            { "战术先锋", 3 }, { "武士", 3 }, { "牧师", 3 }, { "圣光", 3 },
            { "铁壁", 3 }, { "鹰眼", 3 }, { "荆棘", 3 },
            { "珑", 4 }, { "奥术", 4 }, { "法师", 4 },
            { "近卫", 5 }, { "万录朵", 5 },
        };
        var initialAvailable = new System.Collections.Generic.HashSet<string>
        {
            "斥候先锋", "战术先锋", "连弩", "净化", "突击先锋", "游击先锋"
        };
        var quotes = new System.Collections.Generic.Dictionary<string, string>
        {
            { "战术先锋", "和我同行吗？可别拖后腿。" },
            { "斥候先锋", "侦察就交给我吧，保证不会漏掉任何敌人。" },
            { "连弩", "弓弦已上好，只等你一声令下。" },
            { "净化", "伤员交给我，你们只管往前冲。" },
            { "近卫", "吾之剑，为正义而挥。" },
            { "鹰眼", "远处的东西，在我眼中和近处没什么两样。" },
            { "万录朵", "...你看起来很有趣。就陪你走一程吧。" },
        };

        int modified = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<OperatorData>(path);
            if (data == null || string.IsNullOrEmpty(data.operatorName)) continue;

            bool changed = false;
            string name = data.operatorName;

            if (starMap.TryGetValue(name, out int maxStar) && data.maxStarRating != maxStar)
            {
                data.maxStarRating = maxStar;
                changed = true;
            }

            bool isInit = initialAvailable.Contains(name);
            if (data.isInitialAvailable != isInit)
            {
                data.isInitialAvailable = isInit;
                changed = true;
            }

            if (quotes.TryGetValue(name, out string quote) && data.selectQuote != quote)
            {
                data.selectQuote = quote;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(data);
                modified++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Set star ratings for {modified} operators.");
    }
}
