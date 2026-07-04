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
    [SerializeField] private string entryScene = "RogueEntry";
    [SerializeField] private string battleScene = "plot";
    [SerializeField] private string resultScene = "RogueResult";
    [SerializeField] private string collectionScene = "StoryCardCollection";

    [Header("调试")]
    [SerializeField] private bool strictCheckCurrentScene = true;
    [SerializeField] private bool verboseLog = true;

    /// <summary> 进入收藏页前记录当前场景，返回时回到该场景（从哪进回哪）。 </summary>
    private static string _returnSceneFromCollection;

    /// <summary> 从剧情碎片返回关卡时，目标关卡加载后应直接弹出结束菜单（不重打）。读取后清除。 </summary>
    private static string _showEndMenuWhenLevelLoads;

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
        // 兼容旧场景：若曾保存为已删除的 RogueBattle_Template，强制改为 plot
        if (string.Equals(battleScene, "RogueBattle_Template", System.StringComparison.OrdinalIgnoreCase))
            battleScene = "plot";
    }

    public void EnterBattleFromEntry()
    {
        TryRoute(entryScene, battleScene);
    }

    public void EnterResultFromBattle()
    {
        TryRoute(battleScene, resultScene);
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
}
