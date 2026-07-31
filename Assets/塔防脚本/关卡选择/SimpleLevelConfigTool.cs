#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class SimpleLevelConfigTool : EditorWindow
{
    [MenuItem("魔塔/简单关卡配置工具")]
    public static void ShowWindow()
    {
        GetWindow<SimpleLevelConfigTool>("简单关卡配置");
    }

    private string _configName = "Plot1_LevelConfig";
    private Vector2 _scrollPos;

    private class GroupConfig
    {
        public int shop = 0;
        public int elite = 0;
        public int boss = 0;
        public int randomEvent = 0;
        public int rest = 0;
    }

    private GroupConfig _group1 = new GroupConfig { shop = 1 };
    private GroupConfig _group2 = new GroupConfig { elite = 1 };
    private GroupConfig _group3 = new GroupConfig { shop = 1, elite = 1 };
    private GroupConfig _group4 = new GroupConfig { shop = 1, elite = 1, boss = 1 };

    void OnEnable()
    {
        LoadFromExistingAsset();
    }

    void LoadFromExistingAsset()
    {
        var guids = AssetDatabase.FindAssets("t:SimpleLevelRandomConfig");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var asset = AssetDatabase.LoadAssetAtPath<SimpleLevelRandomConfig>(path);
        if (asset == null) return;

        _configName = System.IO.Path.GetFileNameWithoutExtension(path);

        _group1 = new GroupConfig { shop = asset.group1to4.shopCount, elite = asset.group1to4.eliteCount, boss = asset.group1to4.bossCount, randomEvent = asset.group1to4.randomEventCount, rest = asset.group1to4.restCount };
        _group2 = new GroupConfig { shop = asset.group5to8.shopCount, elite = asset.group5to8.eliteCount, boss = asset.group5to8.bossCount, randomEvent = asset.group5to8.randomEventCount, rest = asset.group5to8.restCount };
        _group3 = new GroupConfig { shop = asset.group9to12.shopCount, elite = asset.group9to12.eliteCount, boss = asset.group9to12.bossCount, randomEvent = asset.group9to12.randomEventCount, rest = asset.group9to12.restCount };
        _group4 = new GroupConfig { shop = asset.group13to16.shopCount, elite = asset.group13to16.eliteCount, boss = asset.group13to16.bossCount, randomEvent = asset.group13to16.randomEventCount, rest = asset.group13to16.restCount };
    }

    void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, false, true);

        GUILayout.Label("📝 创建简单关卡配置", EditorStyles.boldLabel);
        GUILayout.Space(10);

        _configName = EditorGUILayout.TextField("配置名称", _configName);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox("每段4个关卡，设置每段有多少个各种类型的关卡", MessageType.Info);
        GUILayout.Space(10);

        DrawGroupConfig("🎯 第1-4关", _group1);
        GUILayout.Space(10);

        DrawGroupConfig("🎯 第5-8关", _group2);
        GUILayout.Space(10);

        DrawGroupConfig("🎯 第9-12关", _group3);
        GUILayout.Space(10);

        DrawGroupConfig("🎯 第13-16关", _group4);
        GUILayout.Space(20);

        if (GUILayout.Button("✨ 创建配置文件", GUILayout.Height(30)))
        {
            CreateConfig();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("📋 重置为默认值"))
        {
            _group1 = new GroupConfig { shop = 1 };
            _group2 = new GroupConfig { elite = 1 };
            _group3 = new GroupConfig { shop = 1, elite = 1 };
            _group4 = new GroupConfig { shop = 1, elite = 1, boss = 1 };
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawGroupConfig(string title, GroupConfig config)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        config.shop = EditorGUILayout.IntSlider("🛒 商店", config.shop, 0, 4);
        config.elite = EditorGUILayout.IntSlider("⚔️ 精英", config.elite, 0, 4);
        config.boss = EditorGUILayout.IntSlider("👹 Boss", config.boss, 0, 4);
        config.randomEvent = EditorGUILayout.IntSlider("❓ 随机事件", config.randomEvent, 0, 4);
        config.rest = EditorGUILayout.IntSlider("🏕️ 休息点", config.rest, 0, 4);

        int total = config.shop + config.elite + config.boss + config.randomEvent + config.rest;
        if (total > 4)
        {
            EditorGUILayout.HelpBox($"⚠️ 总数 {total} 超过4个！", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"已用: {total}/4，剩余普通: {4 - total}", MessageType.Info);
        }

        EditorGUI.indentLevel--;
    }

    void CreateConfig()
    {
        if (string.IsNullOrEmpty(_configName))
        {
            EditorUtility.DisplayDialog("错误", "请输入配置名称！", "确定");
            return;
        }

        string folderPath = "Assets/塔防脚本/数据/关卡";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/塔防脚本/数据"))
            {
                AssetDatabase.CreateFolder("Assets/塔防脚本", "数据");
            }
            AssetDatabase.CreateFolder("Assets/塔防脚本/数据", "关卡");
        }

        string assetPath = folderPath + "/" + _configName + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<SimpleLevelRandomConfig>(assetPath);
        if (existing != null)
        {
            bool overwrite = EditorUtility.DisplayDialog("提示",
                $"配置文件 {_configName} 已存在，是否覆盖？",
                "覆盖", "取消");
            if (!overwrite) return;
        }

        var config = CreateInstance<SimpleLevelRandomConfig>();
        config.group1to4.shopCount = _group1.shop;
        config.group1to4.eliteCount = _group1.elite;
        config.group1to4.bossCount = _group1.boss;
        config.group1to4.randomEventCount = _group1.randomEvent;
        config.group1to4.restCount = _group1.rest;

        config.group5to8.shopCount = _group2.shop;
        config.group5to8.eliteCount = _group2.elite;
        config.group5to8.bossCount = _group2.boss;
        config.group5to8.randomEventCount = _group2.randomEvent;
        config.group5to8.restCount = _group2.rest;

        config.group9to12.shopCount = _group3.shop;
        config.group9to12.eliteCount = _group3.elite;
        config.group9to12.bossCount = _group3.boss;
        config.group9to12.randomEventCount = _group3.randomEvent;
        config.group9to12.restCount = _group3.rest;

        config.group13to16.shopCount = _group4.shop;
        config.group13to16.eliteCount = _group4.elite;
        config.group13to16.bossCount = _group4.boss;
        config.group13to16.randomEventCount = _group4.randomEvent;
        config.group13to16.restCount = _group4.rest;

        if (existing != null)
        {
            EditorUtility.CopySerialized(config, existing);
            AssetDatabase.SaveAssets();
            Selection.activeObject = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;
        }

        EditorGUIUtility.PingObject(Selection.activeObject);
        EditorUtility.DisplayDialog("成功", $"配置文件已创建：\n{assetPath}\n\n现在可以把这个配置拖到 plot 场景的 LevelMapController 上了！", "确定");
    }
}
#endif
