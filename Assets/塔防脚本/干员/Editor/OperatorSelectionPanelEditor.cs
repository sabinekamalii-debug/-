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
    }

    [MenuItem("Tools/干员选择面板/设置干员星级")]
    public static void SetOperatorStarRatings()
    {
        string[] guids = AssetDatabase.FindAssets("t:OperatorData");
        var starMap = new System.Collections.Generic.Dictionary<string, int>
        {
            { "光波", 1 }, { "拳师", 1 }, { "地面射手", 1 },
            { "星豹", 3 },
            { "连弩", 2 }, { "净化", 2 }, { "女战士", 2 },
            { "钩爪", 2 }, { "晶", 2 },
            { "武士", 3 }, { "牧师", 3 }, { "圣光", 3 },
            { "铁壁", 3 }, { "鹰眼", 3 }, { "荆棘", 3 },
            { "珑", 4 }, { "奥术", 4 }, { "法师", 4 },
            { "近卫", 5 }, { "万录朵", 5 },
        };
        var initialAvailable = new System.Collections.Generic.HashSet<string>
        {
            "星豹", "连弩", "净化"
        };
        var quotes = new System.Collections.Generic.Dictionary<string, string>
        {
            { "星豹", "快，是你抓不住我的理由。" },
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

    }
}
