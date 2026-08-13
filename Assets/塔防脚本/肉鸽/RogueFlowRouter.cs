using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 肉鸽流程路由器（写死流程）：
/// RogueEntry -> plot -> RogueResult -> RogueEntry
/// 收藏页仅允许从 RogueEntry 进入，并可返回 RogueEntry。
/// </summary>
public class RogueFlowRouter : MonoBehaviour
{
    [Header("固定场景名（需与 Build Settings 一致）")]
    [SerializeField] private string entryScene = SceneNames.RogueEntry;
    [SerializeField] private string firstLevelScene = SceneNames.FirstLevel;
    [SerializeField] private string resultScene = SceneNames.RogueResult;
    [SerializeField] private string collectionScene = SceneNames.StoryCardCollection;

    [Header("调试")]
    [SerializeField] private bool strictCheckCurrentScene = true;

    /// <summary> 进入收藏页前记录当前场景，返回时回到该场景（从哪进回哪）。 </summary>
    private static string _returnSceneFromCollection;

    /// <summary> 从剧情碎片返回关卡时，目标关卡加载后应直接弹出结束菜单（不重打）。读取后清除。 </summary>
    private static string _showEndMenuWhenLevelLoads;

    /// <summary>
    /// 每次进入 Play 模式时重置静态路由状态。
    /// 避免编辑器反复测试时，上一次运行残留的 _showEndMenuWhenLevelLoads / _returnSceneFromCollection
    /// 被下一轮直接打开 BattleScene 误读，导致“一开战就弹出结束菜单”。
    /// （正常游戏流程中，收藏→返回的设置与使用都在同一次运行内，不受此重置影响。）
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticState()
    {
        _returnSceneFromCollection = null;
        _showEndMenuWhenLevelLoads = null;
    }

    /// <summary> 从关卡等非入口场景打开剧情碎片页时调用，记录当前场景以便返回。 </summary>
    public static void SetReturnSceneBeforeOpeningCollection(string sceneName)
    {
        _returnSceneFromCollection = sceneName ?? "";
    }

    /// <summary> 若当前是从剧情碎片返回关卡，返回目标关卡名并清除标志；否则返回 null。 </summary>
    public static string GetAndClearReturnFromCollectionLevel()
    {
        string s = _showEndMenuWhenLevelLoads;
        _showEndMenuWhenLevelLoads = null;
        return s;
    }

    private static bool IsLevelSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        string lower = sceneName.ToLowerInvariant().Replace(" ", "");
        if (!lower.StartsWith("level") || lower.Length <= 5) return false;
        return int.TryParse(lower.Substring(5), out int n) && n >= 1;
    }

    private void Awake()
    {
        // 兼容旧场景：若曾保存为已删除的 RogueBattle_Template，强制改为第一关战斗场景
        if (string.Equals(firstLevelScene, "RogueBattle_Template", System.StringComparison.OrdinalIgnoreCase))
            firstLevelScene = SceneNames.FirstLevel;
        // 首战强制进入第一关战斗场景（level 1），不允许误导向 plot 剧情/收藏场景
        if (string.IsNullOrEmpty(firstLevelScene))
            firstLevelScene = SceneNames.FirstLevel;

        // 监听第一关抽卡完成事件
        RogueResultController.OnMidGameDropCompleted += OnFirstStagePickCompleted;
    }

    private void OnDestroy()
    {
        RogueResultController.OnMidGameDropCompleted -= OnFirstStagePickCompleted;
    }

    private void OnFirstStagePickCompleted()
    {
        // 第一关抽卡完成后，自动进入战斗
        EnterBattleFromEntry();
    }

    public void EnterBattleFromEntry()
    {
        // 首战走「新架构」：和地图节点（LevelType.Start）完全一致的路径——
        // 由 ActConfig.normalLevelPool[0] 决定首战 LevelConfig，加载空壳 BattleScene，
        // 运行时由 BattleSceneBootstrap 从 LevelSceneLoadContext 注入关卡内容。
        // 不再写死旧的 level 1 场景（旧场景把关卡数据写死在场景里，与新架构冲突）。
        LevelConfig firstConfig = GetFirstStageLevelConfig();
        if (firstConfig != null)
        {
            int levelId = firstConfig.levelId;
            LevelSceneLoadContext.SetLevelConfig(firstConfig, levelId, SceneNames.BattleScene, -1);
            Time.timeScale = 1f;
            VideoSceneLoader.LoadScene(SceneNames.BattleScene);
            return;
        }

        // 兜底（无 ActConfig / 无 LevelConfig 的旧数据）：退回旧架构直接进 level 1。
        // 注意：level 1.unity 是【已淘汰的旧架构场景】，仅作兜底/参考，不要在其上做新设计
        // （见 Assets/Scenes/level/DEPRECATED_旧场景请勿修改.txt）。新关卡一律走 BattleScene + LevelConfig。
        TryRoute(entryScene, firstLevelScene);
    }

    /// <summary>
    /// 取得首战（Start 节点）对应的 LevelConfig，与 StSMapGenerator 给 Start 节点
    /// 分配 normalPool[0] 的逻辑保持一致。找不到返回 null（调用方走旧架构兜底）。
    /// </summary>
    private LevelConfig GetFirstStageLevelConfig()
    {
        var actConfig = RogueRuntimeState.CurrentActConfig;
        if (actConfig == null || actConfig.normalLevelPool == null || actConfig.normalLevelPool.Length == 0)
            return null;

        int firstId = actConfig.normalLevelPool[0];
        string[] names = {
            $"Level_{firstId:D2}_Battle",
            $"Level_{firstId}_Battle",
            $"LevelConfig_{firstId}",
        };
        foreach (var name in names)
        {
            var config = Resources.Load<LevelConfig>($"LevelConfigs/{name}");
            if (config != null) return config;
        }
        return null;
    }

    public void EnterResultFromBattle()
    {
        // 任何 level 战斗场景结束都可回结果页（不要求精确等于某个场景名）
        TryRouteAnyLevel(resultScene);
    }

    public void ReturnEntryFromResult()
    {
        TryRoute(resultScene, entryScene);
    }

    public void ReturnEntryFromResultAndStartBattle()
    {
        RogueRuntimeState.AutoStartBattleOnEntry = true;
        TryRoute(resultScene, entryScene);
    }

    public void EnterCollectionFromEntry()
    {
        TryRoute(entryScene, collectionScene);
    }

    /// <summary> 从收藏页返回：回到进入收藏页前的场景（若未记录则回入口）。 </summary>
    public void ReturnEntryFromCollection()
    {
        ReturnFromCollectionStatic(entryScene, collectionScene);
    }

    /// <summary> 收藏页无 Router 实例时也可调用（从哪进回哪）。若未记录来源则回 defaultReturnScene。从 TitleUI 进则回 Title 场景（显示主菜单）。 </summary>
    public static void ReturnFromCollectionStatic(string defaultReturnScene, string collectionSceneName)
    {
        string current = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(collectionSceneName) && !string.Equals(current, collectionSceneName))
            return;
        string target = !string.IsNullOrEmpty(_returnSceneFromCollection) ? _returnSceneFromCollection : (defaultReturnScene ?? "RogueEntry");
        _returnSceneFromCollection = null;
        Time.timeScale = 1f;
        if (IsLevelSceneName(target))
            _showEndMenuWhenLevelLoads = target;
        VideoSceneLoader.LoadScene(target);
    }

    private void TryRoute(string expectedCurrent, string next)
    {
        string current = SceneManager.GetActiveScene().name;
        if (strictCheckCurrentScene && !string.Equals(current, expectedCurrent))
            return;

        if (string.Equals(next, collectionScene))
            _returnSceneFromCollection = current;

        Time.timeScale = 1f;
        VideoSceneLoader.LoadScene(next);
    }

    /// <summary> 从任意 level 战斗场景离开（进入结果页）：只要当前是 level 场景即放行。 </summary>
    private void TryRouteAnyLevel(string next)
    {
        string current = SceneManager.GetActiveScene().name;
        if (strictCheckCurrentScene && !IsLevelSceneName(current))
            return;

        if (string.Equals(next, collectionScene))
            _returnSceneFromCollection = current;

        Time.timeScale = 1f;
        VideoSceneLoader.LoadScene(next);
    }
}
