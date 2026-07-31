#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// 关卡标准化工具 v3 - Additive 模式版本
/// 关键修复：master 和 target 同时打开 Additive，OpenScene(Single) 会关闭所有场景
/// </summary>
public class LevelStandardizer : EditorWindow
{
    private const string MASTER_SCENE_PATH = "Assets/Scenes/level/level 1.unity";

    private static readonly string[] COPY_TARGETS = {
        "遭遇战菜单",
        "结束菜单",
        "角色画布",
        "敌我信息canvas (1)",
        "角色商店处",
        "Managers"
    };

    private static readonly (string path, int levelNumber)[] TARGET_LEVELS = {
        ("Assets/Scenes/level/level 3.unity", 3),
        ("Assets/Scenes/level/level 4.unity", 4),
        ("Assets/Scenes/level/level 5.unity", 5),
        ("Assets/Scenes/level/level 6.unity", 6),
        ("Assets/Scenes/level/level 7.unity", 7),
        ("Assets/Scenes/level/level 8.unity", 8),
        ("Assets/Scenes/level/level 9.unity", 9),
        ("Assets/Scenes/level/level 10.unity", 10),
        ("Assets/Scenes/level/level 11.unity", 11),
        ("Assets/Scenes/level/level 12.unity", 12),
        ("Assets/Scenes/level/level 13.unity", 13),
        ("Assets/Scenes/level/level 14.unity", 14),
        ("Assets/Scenes/level/level 15.unity", 15),
        ("Assets/Scenes/level/level 16.unity", 16),
        ("Assets/Scenes/level/level boss.unity", 17),
        ("Assets/Scenes/level/level elite.unity", 18),
    };

    [MenuItem("Tools/关卡标准化/执行全部关卡")]
    public static void StandardizeAllLevels()
    {
        if (!EditorUtility.DisplayDialog("关卡标准化 v3",
            $"将从 level 1 复制核心对象覆盖到 {TARGET_LEVELS.Length} 个关卡。\n\n" +
            "使用 Additive 模式打开场景，会保留您当前打开的场景。\n\n" +
            "确定继续？",
            "确定", "取消"))
            return;

        int success = 0;
        int partial = 0;
        int failed = 0;
        List<string> failures = new List<string>();

        Scene masterScene = default;

        // 步骤 1：以 Additive 方式打开 master
        try
        {
            // 先检查 level 1 是否已经打开
            bool alreadyOpen = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene s = EditorSceneManager.GetSceneAt(i);
                if (s.path == MASTER_SCENE_PATH && s.isLoaded)
                {
                    masterScene = s;
                    alreadyOpen = true;
                    break;
                }
            }

            if (!alreadyOpen)
            {
                masterScene = EditorSceneManager.OpenScene(MASTER_SCENE_PATH, OpenSceneMode.Additive);
            }

            Debug.Log($"[LevelStandardizer] Master scene loaded: {masterScene.path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LevelStandardizer] 无法打开 master scene: {ex.Message}");
            EditorUtility.DisplayDialog("错误", "无法打开 level 1，请关闭它后再试", "确定");
            return;
        }

        // 步骤 2：逐个处理目标关卡（Additive 模式）
        foreach (var (path, levelNum) in TARGET_LEVELS)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[LevelStandardizer] 跳过不存在的场景: {path}");
                continue;
            }

            var result = ProcessSingleLevel(path, levelNum, masterScene);
            switch (result)
            {
                case ProcessResult.FullSuccess:
                    success++;
                    Debug.Log($"[LevelStandardizer] [OK] Level {levelNum}: 完全成功");
                    break;
                case ProcessResult.PartialSuccess:
                    partial++;
                    Debug.LogWarning($"[LevelStandardizer] [PARTIAL] Level {levelNum}: 部分成功");
                    failures.Add($"Level {levelNum} (部分)");
                    break;
                case ProcessResult.Failed:
                    failed++;
                    Debug.LogError($"[LevelStandardizer] [FAIL] Level {levelNum}: 失败");
                    failures.Add($"Level {levelNum}");
                    break;
            }
        }

        // 步骤 3：关闭 master
        try
        {
            if (masterScene.IsValid() && masterScene.isLoaded)
            {
                EditorSceneManager.CloseScene(masterScene, true);
                Debug.Log("[LevelStandardizer] Master scene closed");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] 关闭 master 失败: {ex.Message}");
        }

        string msg = $"完全成功: {success}\n部分成功: {partial}\n失败: {failed}\n合计: {TARGET_LEVELS.Length}";
        if (failures.Count > 0)
            msg += "\n\n失败列表:\n" + string.Join("\n", failures);
        EditorUtility.DisplayDialog("关卡标准化完成", msg, "确定");
    }

    private enum ProcessResult
    {
        FullSuccess,
        PartialSuccess,
        Failed,
    }

    /// <summary>
    /// 处理单个关卡。Additive 模式：master 和 target 同时打开
    /// </summary>
    private static ProcessResult ProcessSingleLevel(string scenePath, int levelNumber, Scene masterScene)
    {
        Debug.Log($"[LevelStandardizer] --- 开始 Level {levelNumber}: {scenePath} ---");

        // 步骤 A：以 Additive 模式打开目标关卡（master 不会被关闭）
        Scene targetScene = default;
        try
        {
            // 检查目标场景是否已经打开
            bool alreadyOpen = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene s = EditorSceneManager.GetSceneAt(i);
                if (s.path == scenePath && s.isLoaded)
                {
                    targetScene = s;
                    alreadyOpen = true;
                    break;
                }
            }

            if (!alreadyOpen)
            {
                targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            if (!targetScene.IsValid())
            {
                Debug.LogError($"[LevelStandardizer] Level {levelNumber}: target scene invalid");
                return ProcessResult.Failed;
            }

            Debug.Log($"[LevelStandardizer] Level {levelNumber}: target scene loaded, roots={targetScene.rootCount}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LevelStandardizer] Level {levelNumber}: OpenScene failed: {ex.Message}");
            return ProcessResult.Failed;
        }

        // 步骤 B：在 master 中找到 6 个对象（master 现在还开着）
        Dictionary<string, GameObject> masterObjects;
        try
        {
            masterObjects = FindRootObjectsByName(masterScene, COPY_TARGETS);
            if (masterObjects.Count != COPY_TARGETS.Length)
            {
                Debug.LogError($"[LevelStandardizer] Level {levelNumber}: master 缺对象 {masterObjects.Count}/{COPY_TARGETS.Length}");
                var missing = COPY_TARGETS.Where(n => !masterObjects.ContainsKey(n));
                Debug.LogError($"  Missing: {string.Join(",", missing)}");
                // 不返回失败，尝试继续
            }
            else
            {
                Debug.Log($"[LevelStandardizer] Level {levelNumber}: master 全部 {masterObjects.Count} 个对象就绪");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LevelStandardizer] Level {levelNumber}: FindRoot failed: {ex.Message}");
            TryCloseScene(targetScene);
            return ProcessResult.Failed;
        }

        bool allCopied = true;
        bool anyCopied = false;

        // 步骤 C：删除目标场景中已有的同名对象（master 仍开着）
        try
        {
            DeleteExistingCopies(targetScene);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: 已删除旧对象");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: 删除失败: {ex.Message}");
        }

        // 步骤 D：从 master 复制 6 个对象到 target（master 仍开着！）
        Dictionary<string, GameObject> copiedObjects = new Dictionary<string, GameObject>();
        foreach (var name in COPY_TARGETS)
        {
            if (!masterObjects.TryGetValue(name, out GameObject masterObj) || masterObj == null)
            {
                Debug.LogWarning($"  跳过 {name}: master 对象不可用");
                allCopied = false;
                continue;
            }

            try
            {
                GameObject clone = Object.Instantiate(masterObj);
                clone.name = name;
                SceneManager.MoveGameObjectToScene(clone, targetScene);
                copiedObjects[name] = clone;
                anyCopied = true;
                Debug.Log($"  复制成功: {name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"  复制 {name} 失败: {ex.Message}");
                allCopied = false;
            }
        }

        if (!anyCopied)
        {
            Debug.LogError($"[LevelStandardizer] Level {levelNumber}: 6 个对象全部复制失败");
            TryCloseScene(targetScene);
            return ProcessResult.Failed;
        }

        // 步骤 E：修复 Canvas
        try
        {
            FixCanvasSettings(targetScene, copiedObjects);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: Canvas 修复完成");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: Canvas 修复失败: {ex.Message}");
            allCopied = false;
        }

        // 步骤 F：修复 Transform
        try
        {
            FixTransforms(copiedObjects);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: Transform 修复完成");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: Transform 修复失败: {ex.Message}");
            allCopied = false;
        }

        // 步骤 G：重新接线引用
        try
        {
            FixManagerReferences(targetScene, copiedObjects);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: 引用接线完成");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: 引用接线失败: {ex.Message}");
            allCopied = false;
        }

        // 步骤 H：修复 GridSystem
        try
        {
            FixGridDefensePoint(targetScene);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: Grid 修复完成");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: Grid 修复失败: {ex.Message}");
        }

        // 步骤 I：设置关卡参数
        try
        {
            FixLevelParameters(copiedObjects, levelNumber);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: 参数设置完成");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: 参数设置失败: {ex.Message}");
            allCopied = false;
        }

        // 步骤 J：遭遇战菜单设 inactive
        try
        {
            if (copiedObjects.TryGetValue("遭遇战菜单", out GameObject enc) && enc != null)
            {
                enc.SetActive(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] Level {levelNumber}: 遭遇战菜单禁用失败: {ex.Message}");
        }

        // 步骤 K：保存并关闭 target 场景
        bool saved = TrySaveScene(targetScene, levelNumber);
        TryCloseScene(targetScene);

        if (saved && allCopied) return ProcessResult.FullSuccess;
        if (saved) return ProcessResult.PartialSuccess;
        return ProcessResult.Failed;
    }

    private static bool TrySaveScene(Scene scene, int levelNumber)
    {
        try
        {
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LevelStandardizer] Level {levelNumber}: SaveScene = {saved}");
            return saved;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LevelStandardizer] Level {levelNumber}: SaveScene failed: {ex.Message}");
            return false;
        }
    }

    private static void TryCloseScene(Scene scene)
    {
        try
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LevelStandardizer] CloseScene failed: {ex.Message}");
        }
    }

    private static Dictionary<string, GameObject> FindRootObjectsByName(Scene scene, string[] names)
    {
        var result = new Dictionary<string, GameObject>();
        var roots = scene.GetRootGameObjects();
        foreach (var name in names)
        {
            foreach (var root in roots)
            {
                if (root != null && root.name == name)
                {
                    result[name] = root;
                    break;
                }
            }
        }
        return result;
    }

    private static void DeleteExistingCopies(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        foreach (var name in COPY_TARGETS)
        {
            foreach (var root in roots)
            {
                if (root != null && root.name == name && root.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(root);
                    break;
                }
            }
        }
        // 特别处理：旧版敌我信息canvas（不带 "(1)"）
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == "敌我信息canvas" && root.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }
    }

    private static Camera FindMainCamera(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root != null && root.name == "Managers")
            {
                var cam = root.GetComponentInChildren<Camera>();
                if (cam != null) return cam;
            }
        }
        return Object.FindObjectOfType<Camera>();
    }

    private static GameObject FindChildRecursive(GameObject parent, string childName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent.transform)
        {
            if (child.name == childName) return child.gameObject;
            var found = FindChildRecursive(child.gameObject, childName);
            if (found != null) return found;
        }
        return null;
    }

    private static void FixCanvasSettings(Scene targetScene, Dictionary<string, GameObject> copiedObjects)
    {
        Camera mainCam = FindMainCamera(targetScene);

        var canvasConfigs = new Dictionary<string, (int renderMode, int sortingOrder, int planeDistance)>
        {
            { "遭遇战菜单", (1, 100, 100) },
            { "结束菜单", (1, 0, 100) },
            { "角色画布", (2, 0, 100) },
            { "敌我信息canvas (1)", (1, 122, 100) },
        };

        foreach (var kvp in canvasConfigs)
        {
            if (!copiedObjects.TryGetValue(kvp.Key, out GameObject go) || go == null) continue;
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.GetComponentInChildren<Canvas>();
            if (canvas == null) continue;
            canvas.renderMode = (RenderMode)kvp.Value.renderMode;
            canvas.sortingOrder = kvp.Value.sortingOrder;
            canvas.planeDistance = kvp.Value.planeDistance;
            if (kvp.Value.renderMode != 2 && mainCam != null)
                canvas.worldCamera = mainCam;
            EditorUtility.SetDirty(canvas);
        }
    }

    private static void FixTransforms(Dictionary<string, GameObject> copiedObjects)
    {
        var rectConfigs = new Dictionary<string, (Vector3 pos, Vector3 scale)>
        {
            { "遭遇战菜单", (new Vector3(-0.0977805257f, 0, 90), new Vector3(0.009259259f, 0.009259259f, 0.009259259f)) },
            { "结束菜单", (new Vector3(-0.0977805257f, 0, 90), new Vector3(0.009259259f, 0.009259259f, 0.009259259f)) },
            { "角色画布", (new Vector3(0, 0, 90), new Vector3(1, 1, 1)) },
            { "敌我信息canvas (1)", (new Vector3(-0.0977805257f, 0, 90), new Vector3(0.009259259f, 0.009259259f, 0.009259259f)) },
        };

        foreach (var kvp in rectConfigs)
        {
            if (!copiedObjects.TryGetValue(kvp.Key, out GameObject go) || go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.localPosition = kvp.Value.pos;
            rt.localScale = kvp.Value.scale;
            EditorUtility.SetDirty(rt);
        }

        if (copiedObjects.TryGetValue("角色商店处", out GameObject shopGo) && shopGo != null)
        {
            var t = shopGo.transform;
            t.localPosition = new Vector3(7.88f, 0, 0);
            t.localScale = new Vector3(0.12f, 0.38f, 1);
            EditorUtility.SetDirty(t);
        }
    }

    private static void FixManagerReferences(Scene targetScene, Dictionary<string, GameObject> copiedObjects)
    {
        if (!copiedObjects.TryGetValue("Managers", out GameObject managersGo) || managersGo == null)
            return;

        var roots = targetScene.GetRootGameObjects();
        GameObject endMenu = null, infoCanvas = null, charCanvas = null;
        GameObject spawnerGo = null, defensePoint = null, gridGo = null;

        foreach (var root in roots)
        {
            if (root == null) continue;
            switch (root.name)
            {
                case "结束菜单": endMenu = root; break;
                case "敌我信息canvas (1)": infoCanvas = root; break;
                case "角色画布": charCanvas = root; break;
                case "Spawner": spawnerGo = root; break;
                case "守护点": defensePoint = root; break;
                case "Grid": gridGo = root; break;
            }
        }

        SafeWireReference(managersGo, "LevelEndMenu", "endMenuCanvas",
            endMenu?.GetComponent<Canvas>()?.gameObject);
        SafeWireReference(managersGo, "LevelEndMenu", "spawner",
            FindComponent(spawnerGo, "Spawner"));
        SafeWireReference(managersGo, "GameManager", "uiController",
            FindComponent(infoCanvas, "UIController"));
        SafeWireReference(managersGo, "SystemMessageUI", "messageText",
            FindNestedTMP(infoCanvas, "文本对话", "新手教程 （真）"));
        SafeWireReference(managersGo, "TeleportController", "defensePointCooldownParentCanvas",
            charCanvas?.GetComponent<Canvas>());
        SafeWireReference(managersGo, "TeleportController", "defensePoint",
            defensePoint?.transform);
        SafeWireReference(managersGo, "Spawner", "ui",
            FindComponent(infoCanvas, "UIController"));

        if (gridGo != null && defensePoint != null)
            SafeWireReference(gridGo, "GridSystem", "defensePoint", defensePoint.transform);
    }

    private static object FindComponent(GameObject go, string typeName)
    {
        if (go == null) return null;
        var comp = go.GetComponent(typeName) as MonoBehaviour;
        if (comp != null) return comp;
        return go;
    }

    private static object FindNestedTMP(GameObject root, string child1, string child2)
    {
        if (root == null) return null;
        var c1 = FindChildRecursive(root, child1);
        if (c1 == null) return null;
        var c2 = FindChildRecursive(c1, child2);
        if (c2 == null) return null;
        return c2.GetComponent("TextMeshProUGUI") as MonoBehaviour;
    }

    private static void SafeWireReference(GameObject owner, string componentName, string fieldName, object value)
    {
        if (owner == null || value == null) return;
        var comp = owner.GetComponent(componentName) as MonoBehaviour;
        if (comp == null) return;
        try
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.objectReferenceValue = value as Object;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(comp);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"    {componentName}.{fieldName} 接线失败: {ex.Message}");
        }
    }

    private static void FixGridDefensePoint(Scene targetScene)
    {
        // 已包含在 FixManagerReferences 中
    }

    private static void FixLevelParameters(Dictionary<string, GameObject> copiedObjects, int levelNumber)
    {
        if (!copiedObjects.TryGetValue("Managers", out GameObject managersGo) || managersGo == null)
            return;

        SetSerializedField(managersGo, "LevelEndMenu", "labelName", $"AfterLevel{levelNumber}");
        SetSerializedField(managersGo, "GameManager", "playerHealth", levelNumber <= 4 ? 1 : 3);
        SetSerializedField(managersGo, "DeploymentManager", "currentDP", levelNumber <= 4 ? 60 : 70);
        SetSerializedField(managersGo, "TeleportController", "teleportCooldownDuration", 50f);
    }

    private static void SetSerializedField(GameObject owner, string componentName, string fieldName, object value)
    {
        if (owner == null) return;
        var comp = owner.GetComponent(componentName) as MonoBehaviour;
        if (comp == null) return;
        try
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Boolean:
                    prop.intValue = System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = System.Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.ToString();
                    break;
                default:
                    prop.objectReferenceValue = value as Object;
                    break;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(comp);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"    {componentName}.{fieldName} 设置失败: {ex.Message}");
        }
    }
}
#endif