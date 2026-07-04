using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡地图总控：管理关卡解锁和随机类型显示。
/// 挂在「关卡选择场景」的根物体或 Scroll View 上。
/// 
/// 功能：
/// - 线性解锁：打完第N关自动解锁第N+1关
/// - 随机类型：每4关一组，随机分配商店/精英/Boss等
/// - 状态管理：处理从关卡返回时的进度保存
/// </summary>
public class LevelMapController : MonoBehaviour
{
    public static LevelMapController Instance { get; private set; }

    [Header("关卡顺序")]
    [Tooltip("与 Build Settings 中的场景名一致，如 level 1, level 2。共16关。")]
    public string[] levelOrder = new[]
    {
        "level 1","level 2","level 3","level 4",
        "level 5","level 6","level 7","level 8",
        "level 9","level 10","level 11","level 12",
        "level 13","level 14","level 15","level 16",
    };

    [Header("关卡随机配置")]
    [Tooltip("拖入简单关卡配置（推荐）")]
    public SimpleLevelRandomConfig simpleLevelRandomConfig;

    [Header("关卡连线配置")]
    [Tooltip("拖入连线配置（不填则线性解锁 1→2→3→...→16）")]
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

#if UNITY_EDITOR
        if (clearProgressOnEnterInEditor)
        {
            LevelProgress.ClearAll();
            LevelRandomizer.Reset();
        }
#endif

        LevelProgress.SetLevelOrder(levelOrder);
        LevelProgress.SetConnectionConfig(connectionConfig);

        if (simpleLevelRandomConfig != null)
        {
            LevelRandomizer.SetSimpleConfig(simpleLevelRandomConfig);
        }
        
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
        
        RefreshAllLevelButtons();
        
        EnsureLinesBehindButtons();
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