using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 关卡地图总控：管理关卡解锁和随机类型显示。
/// 挂在「关卡选择场景」的根物体或 Scroll View 上。
/// 
/// 功能：
/// - 大局驱动：从 RogueRuntimeState.CurrentActConfig 读取节点数和关卡池
/// - 线性解锁：打完第N关自动解锁第N+1关
/// - 随机类型：每4关一组，随机分配商店/精英/Boss等
/// - 状态管理：处理从关卡返回时的进度保存
/// </summary>
public class LevelMapController : MonoBehaviour
{
    public static LevelMapController Instance { get; private set; }

    [Header("关卡顺序（旧版兼容，ActConfig 无值时使用）")]
    [Tooltip("与 Build Settings 中的场景名一致。ActConfig 存在时此字段被忽略。")]
    public string[] levelOrder = new[]
    {
        "level 1","level 2","level 3","level 4",
        "level 5","level 6","level 7","level 8",
        "level 9","level 10","level 11","level 12",
        "level 13","level 14","level 15","level 16",
        "level 17","level 18","level 19","level 20",
        "level 21","level 22","level 23","level 24",
        "level 25","level 26","level 27","level 28",
        "level 29","level 30",
    };

    [Header("关卡随机配置")]
    [Tooltip("拖入关卡随机配置（灵活，支持自定义区间）")]
    public LevelRandomConfig levelRandomConfig;
    
    [Header("关卡随机配置（简化版）")]
    [Tooltip("拖入简单关卡配置。LevelRandomConfig 为空时才使用这个。")]
    public SimpleLevelRandomConfig simpleLevelRandomConfig;

    [Header("关卡连线配置")]
    [Tooltip("拖入连线配置（不填则线性解锁 1→2→3→...→N）")]
    public LevelConnectionConfig connectionConfig;

    [Header("划动区域（不填则自动找）")]
    public ScrollRect scrollRect;

    [Header("编辑器调试")]
    [Tooltip("勾选时，每次进入场景都会清空进度。默认关闭。")]
    [SerializeField] bool clearProgressOnEnterInEditor = false;

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
            LevelProgress.ClearAll();
#endif

        // 直接运行 plot 场景测试时，CurrentActId 可能为 0，自动选第一个大局
        if (RogueRuntimeState.CurrentActId <= 0)
        {
            var firstAct = ActRegistry.GetFirstAct();
            if (firstAct != null)
            {
                RogueRuntimeState.StartAct(firstAct.actId);
            }
        }

        // 如果有 ActConfig，从它生成 levelOrder（覆盖 Inspector 中的默认值）
        var actConfig = RogueRuntimeState.CurrentActConfig;
        if (actConfig != null && actConfig.totalNodes > 0)
        {
            levelOrder = new string[actConfig.totalNodes];
            for (int i = 0; i < actConfig.totalNodes; i++)
                levelOrder[i] = $"level{i + 1}";
        }

        LevelProgress.SetLevelOrder(levelOrder);
        LevelProgress.SetConnectionConfig(connectionConfig);

        if (levelRandomConfig != null)
        {
            LevelRandomizer.SetConfig(levelRandomConfig);
        }
        else if (simpleLevelRandomConfig != null)
        {
            LevelRandomizer.SetSimpleConfig(simpleLevelRandomConfig);
        }
        
        LevelRandomizer.SetActConfig(actConfig);
        LevelRandomizer.Initialize();
        
        CheckAndApplyReturnContext();
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
    }

    void Start()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        // 编辑器下确保输入模块能接收滚轮事件，避免 Game 窗口未点击时滚轮无效
        ForceInputModuleActive();

        RefreshAllLevelButtons();
        
        EnsureLinesBehindButtons();
    }

    /// <summary>
    /// 编辑器下：
    /// - 旧版 StandaloneInputModule：设 forceModuleActive=true
    /// - 新版 InputSystemUIInputModule：通过 InputSystem.settings 确保编辑器下输入直通 Game 窗口
    /// 打包后构建窗口自带焦点，不影响。
    /// </summary>
    private static void ForceInputModuleActive()
    {
#if UNITY_EDITOR
        // 旧版 Input Module
        if (EventSystem.current != null)
        {
            var standalone = EventSystem.current.currentInputModule as StandaloneInputModule;
            if (standalone != null)
                standalone.forceModuleActive = true;
        }

        // 新版 Input System：编辑器下让所有设备输入直接送到 Game 窗口
        if (InputSystem.settings != null)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
#endif
    }

    void LateUpdate()
    {
        EnsureLinesBehindButtons();
    }

    void EnsureLinesBehindButtons()
    {
        if (scrollRect == null || scrollRect.content == null) return;
        var content = scrollRect.content;
        var lines = content.Find("Lines");
        if (lines != null)
        {
            int targetIndex = 1;
            int maxIndex = content.childCount - 1;
            if (maxIndex <= 0) return;
            if (targetIndex > maxIndex) targetIndex = maxIndex;
            if (lines.GetSiblingIndex() != targetIndex)
                lines.SetSiblingIndex(targetIndex);
        }
    }
}