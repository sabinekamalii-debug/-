using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.Reflection;
using TMPro;
// using Naninovel; // TODO: Naninovel包缺失，临时禁用

/// <summary>
/// 满足以下任一条件后 0.5 秒弹出结束菜单：
/// - 守护点生命值 ≤ 0（失败）
/// - 所有波次已出完且场上敌人全部被消灭（胜利）
/// 建议：在 level A 里建一个空物体（如 LevelEndMenuController），挂本脚本，把「结束菜单」画布拖到 endMenuCanvas。
/// </summary>
public class LevelEndMenu : MonoBehaviour
{
    public static LevelEndMenu Instance;

    [Header("要显示/隐藏的结束菜单画布")]
    [Tooltip("拖入 Hierarchy 里的「结束菜单」Canvas；不拖则用本物体（若脚本挂在画布上，隐藏后协程会停止，建议挂到别的物体上并拖入画布）")]
    public GameObject endMenuCanvas;

    [Header("判定依赖（可留空则自动查找）")]
    public GameManager gameManager;
    public Spawner spawner;

    [Header("弹出延迟")]
    public float showDelay = 0.5f;

    [Header("剧情跳转")]
    const string PlotSceneName = "Title"; // 继续剧情时加载的场景，与 Build Settings 一致
    public string scriptName = "plot1";
    [Tooltip("留空则按当前场景名自动生成（如 level 1 → AfterLevel1，level A → AfterLevelA）；填写则使用该标签。")]
    public string labelName = "AfterLevelA";

    [Header("剧情卡片（胜利时发放，左侧面板显示；不填则不发放）")]
    public StoryCardData cardToUnlockOnWin;
    [Tooltip("可选。拖入场景里做好的「获得剧情碎片」卡片根物体（挂有 FragmentCardHideAfterSeconds），胜利解锁卡时会激活，脚本负责 5 秒后消失。")]
    public GameObject fragmentCardObject;

    [Header("肉鸽结算")]
    [Tooltip("勾选后：结束菜单弹出时发布本场战斗结果，点击「继续剧情」将进入肉鸽结算场景 RogueResult 而非剧情场景。")]
    public bool goToRogueResultOnContinue;

    [Header("两个按钮（均可拖拽；仅「继续剧情」会在失败时变灰不可点、字变红）")]
    [Tooltip("「继续剧情」按钮")]
    public Button continuePlotButton;
    [Tooltip("「重新挑战」或「查看剧情碎片」按钮")]
    public Button retryButton;
    [Tooltip("若存在则优先：点击后跳转剧情碎片场景，返回时回到本关卡。")]
    public Button goToCollectionButton;

    private bool _alreadyShown;
    private bool _isLose; // 本次是否为失败（守护点生命≤0）
    private bool _wasFirstClear; // 本关胜利时是否为首通（用于肉鸽结算 firstClear）
    private Spawner[] _allSpawners; // 场景中全部刷怪点，胜利需全部出完波

    void Awake()
    {
        Instance = this;
        BindButtonsInCode();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary> 代码里强制绑定按钮点击，避免 Inspector 未挂接导致点击无反应。 </summary>
    void BindButtonsInCode()
    {
        TryResolveButtons();
        if (continuePlotButton != null)
        {
            continuePlotButton.onClick.RemoveListener(OnContinuePlot);
            continuePlotButton.onClick.AddListener(OnContinuePlot);
        }
        if (goToCollectionButton != null)
        {
            goToCollectionButton.onClick.RemoveListener(OnGoToCollection);
            goToCollectionButton.onClick.AddListener(OnGoToCollection);
        }
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryLevel);
            retryButton.onClick.AddListener(OnRetryLevel);
        }
    }

    const string CollectionSceneName = "StoryCardCollection";

    void TryResolveButtons()
    {
        continuePlotButton = null;
        retryButton = null;
        goToCollectionButton = null;

        GameObject root = endMenuCanvas != null ? endMenuCanvas : gameObject;
        if (root == null) return;
        var buttons = root.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length < 2) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            var t = buttons[i].GetComponentInChildren<TMP_Text>(true);
            string label = t != null ? t.text : "";
            bool isContinue = label.Contains("继续");
            bool isGoToCollection = (label.Contains("剧情碎片") || label.Contains("查看")) && !label.Contains("继续");
            bool isRetry = label.Contains("重新") || label.Contains("挑战") || label.Contains("重来");
            if (isContinue && continuePlotButton == null) continuePlotButton = buttons[i];
            else if (isGoToCollection && goToCollectionButton == null) goToCollectionButton = buttons[i];
            else if (isRetry && retryButton == null) retryButton = buttons[i];
        }
        if (continuePlotButton == null && buttons.Length >= 1) continuePlotButton = buttons[0];
        if (retryButton == null && goToCollectionButton == null && buttons.Length >= 2) retryButton = buttons[1];
        if (retryButton == continuePlotButton && buttons.Length >= 2)
            retryButton = buttons[0] == continuePlotButton ? buttons[1] : buttons[0];
    }

    void Start()
    {
        if (endMenuCanvas == null) endMenuCanvas = gameObject;
        if (gameManager == null) gameManager = GameManager.Instance;
        if (spawner == null) spawner = FindFirstObjectByType<Spawner>();

        string returnLevel = RogueFlowRouter.GetAndClearReturnFromCollectionLevel();
        if (!string.IsNullOrEmpty(returnLevel) && returnLevel == SceneManager.GetActiveScene().name)
        {
            _alreadyShown = true;
            _isLose = false;
            _wasFirstClear = false;
            TryResolveButtons();
            BindButtonsInCode();
            if (endMenuCanvas != null)
            {
                if (endMenuCanvas == gameObject)
                {
                    var c = endMenuCanvas.GetComponent<Canvas>();
                    if (c != null) c.enabled = true;
                }
                else
                    endMenuCanvas.SetActive(true);
            }
            ApplyContinueButtonState();
            return;
        }

        // 方案B：胜利判定用场景中全部刷怪点，需全部出完波才算过关
        _allSpawners = FindObjectsOfType<Spawner>();

        TryResolveButtons();
        BindButtonsInCode();

        _alreadyShown = false;
        ForceHideEndMenu();

        StartCoroutine(WaitThenShowWhenEnd());
    }

    /// <summary> 强制隐藏结束菜单（未胜利/失败时调用，避免被误点。遭遇战弹出时也会调用。）
    /// 若脚本挂在结束菜单画布本身上（endMenuCanvas == gameObject），则只禁用 Canvas 组件而不禁用 GameObject，否则协程会停、胜利后无法弹出菜单导致卡死。 </summary>
    public void ForceHideEndMenu()
    {
        if (endMenuCanvas == null) return;
        if (endMenuCanvas == gameObject)
        {
            var c = endMenuCanvas.GetComponent<Canvas>();
            if (c != null && c.enabled) c.enabled = false;
            return;
        }
        if (endMenuCanvas.activeSelf)
            endMenuCanvas.SetActive(false);
    }

    IEnumerator WaitThenShowWhenEnd()
    {
        while (!_alreadyShown)
        {
            bool lose = gameManager != null && gameManager.isGameOver;
            // 胜利：所有刷怪点都出完波 + 场上敌人数为 0（单刷怪点关卡行为不变）
            bool waveDone = _allSpawners != null && _allSpawners.Length > 0;
            if (waveDone)
                for (int i = 0; i < _allSpawners.Length; i++)
                    if (_allSpawners[i] == null || !_allSpawners[i].AllWavesSpawned) { waveDone = false; break; }
            bool noEnemies = Enemy2.ActiveEnemyCount <= 0;
            bool win = waveDone && noEnemies;

            // 失败时不弹出结束菜单，由GameManager处理死亡特效
            if (lose)
            {
                _isLose = lose;
                _alreadyShown = true;
                // 发布战斗结果，供结算场景使用
                string sceneName = SceneManager.GetActiveScene().name;
                if (goToRogueResultOnContinue || IsLevelSceneName(sceneName))
                    PublishRogueBattleResultAndEnsureRun();
                yield break;
            }
            // 胜利时正常弹出结束菜单
            else if (win)
            {
                _isLose = lose;
                yield return new WaitForSecondsRealtime(showDelay);
                if (_alreadyShown) yield break;
                _alreadyShown = true;
                Time.timeScale = 1f;
                if (EncounterManager.Instance != null)
                    EncounterManager.Instance.ForceCloseEncounterMenu();
                if (endMenuCanvas != null)
                {
                    if (endMenuCanvas == gameObject)
                    {
                        var c = endMenuCanvas.GetComponent<Canvas>();
                        if (c != null) c.enabled = true;
                    }
                    else
                        endMenuCanvas.SetActive(true);
                }
                TryResolveButtons();
                BindButtonsInCode();
                ApplyContinueButtonState();
                string sceneName = SceneManager.GetActiveScene().name;
                _wasFirstClear = !_isLose && !LevelProgress.IsCompleted(sceneName);
                if (!_isLose)
                {
                    LevelProgress.MarkCompleted(sceneName);
                    
                    // 标记关卡为首次通关（用于卡片显示控制）
                    TryMarkLevelAsFirstCleared(sceneName);
                    
                    // 自动激活关卡内的卡片（如果存在且是首次通关）
                    ActivateLevelCardIfFirstClear(sceneName);
                    
                    if (cardToUnlockOnWin != null)
                    {
                        StoryCardUnlockState.Unlock(cardToUnlockOnWin.cardId);
                        if (StoryCardPanel.Instance != null)
                        {
                            StoryCardPanel.Instance.Refresh();
                            StoryCardPanel.Instance.gameObject.SetActive(true);
                        }
                        if (fragmentCardObject != null)
                            fragmentCardObject.SetActive(true);
                    }
                }
                if (goToRogueResultOnContinue || IsLevelSceneName(sceneName))
                    PublishRogueBattleResultAndEnsureRun();
                yield break;
            }

            yield return null;
        }
    }

    void ApplyContinueButtonState()
    {
        if (continuePlotButton == null) return;
        // 失败时继续剧情按钮也可用，方便跳转到结算界面
        continuePlotButton.interactable = true;
        if (continuePlotButton.image != null)
            continuePlotButton.image.color = Color.white;
        var tmp = continuePlotButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.color = new Color(0.15f, 0.15f, 0.15f, 1f); // 深灰/黑，白底按钮上可读
        var legacyText = continuePlotButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (legacyText != null) legacyText.color = new Color(0.15f, 0.15f, 0.15f, 1f);
    }

    /// <summary> “继续剧情”按钮点击：若勾选了肉鸽结算或当前为 level 关卡场景则进入 RogueResult，否则加载剧情场景。仅在真正胜利/失败弹出结束菜单后才响应。 </summary>
    public void OnContinuePlot()
    {
        if (!_alreadyShown) return; // 未到结束条件就误点了（如遭遇战界面在后面），直接忽略
        Time.timeScale = 1f;
        string sceneName = SceneManager.GetActiveScene().name;
        bool isLevelScene = IsLevelSceneName(sceneName);
        if (goToRogueResultOnContinue || isLevelScene)
        {
            VideoSceneLoader.LoadScene("RogueResult");
            return;
        }
        // HideNaninovelUIOnLevelLoad.ReactivateNaninovelUI();
        // HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();
        string actualLabel;
        if (string.IsNullOrEmpty(labelName))
        {
            string nameForLabel = sceneName.Replace(" ", "");
            actualLabel = "After" + (nameForLabel.Length > 0 ? char.ToUpperInvariant(nameForLabel[0]) + nameForLabel.Substring(1) : "LevelA");
        }
        else
            actualLabel = labelName;
        // NaninovelReturnRequest.Set(scriptName, actualLabel); // TODO: Naninovel包缺失
        VideoSceneLoader.LoadScene(PlotSceneName);
    }

    /// <summary> 发布本场战斗结果到肉鸽状态，供 RogueResult 场景消费。仅在 goToRogueResultOnContinue 为 true 时在弹出结束菜单后调用。 </summary>
    private void PublishRogueBattleResultAndEnsureRun()
    {
        RogueRuntimeState.StartRunIfNeeded();
        string sceneName = SceneManager.GetActiveScene().name;
        int stage = ParseStageFromSceneName(sceneName);
        bool isWin = !_isLose;
        int guardianHpEnd = (gameManager != null && isWin) ? Mathf.Max(0, gameManager.playerHealth) : 0;
        int defaultMaxHp = (gameManager != null) ? 5 : 5; // GameManager 默认 5，无引用时用 5
        bool noHit = isWin && guardianHpEnd >= defaultMaxHp;
        var result = new RogueBattleResult
        {
            stage = stage,
            isWin = isWin,
            noHit = noHit,
            guardianHpEnd = guardianHpEnd,
            firstClear = _wasFirstClear,
            betPlaced = false
        };
        RogueRuntimeState.PublishBattleResult(result);
    }

    /// <summary> 是否为 level 关卡场景名（如 "level 1"、"level 2"），用于自动走肉鸽结算与发布结果。 </summary>
    private static bool IsLevelSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        string lower = sceneName.ToLowerInvariant().Replace(" ", "");
        if (!lower.StartsWith("level") || lower.Length <= 5) return false;
        return int.TryParse(lower.Substring(5), out int n) && n >= 1;
    }

    private static int ParseStageFromSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return 1;
        string lower = sceneName.ToLowerInvariant().Replace(" ", "");
        if (lower.StartsWith("level"))
        {
            string num = lower.Substring(5);
            if (int.TryParse(num, out int n) && n >= 1) return n;
        }
        return 1;
    }

    /// <summary> “查看剧情碎片”按钮点击：记录当前关卡场景后跳转剧情碎片场景，返回时回到本关卡。 </summary>
    public void OnGoToCollection()
    {
        if (!_alreadyShown) return;
        Time.timeScale = 1f;
        string fromScene = SceneManager.GetActiveScene().name;
        RogueFlowRouter.SetReturnSceneBeforeOpeningCollection(fromScene);
        VideoSceneLoader.LoadScene(CollectionSceneName);
    }

    /// <summary> 自动激活关卡内的卡片（如果是首次通关） </summary>
    private void ActivateLevelCardIfFirstClear(string sceneName)
    {
        // 兼容旧逻辑：若项目中存在 LevelCardObjectController，则反射调用 CollectCard。
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (behaviours == null || behaviours.Length == 0) return;

        foreach (var behaviour in behaviours)
        {
            if (behaviour == null) continue;
            var type = behaviour.GetType();
            if (!string.Equals(type.Name, "LevelCardObjectController", StringComparison.Ordinal)) continue;

            MethodInfo collectMethod = type.GetMethod("CollectCard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (collectMethod != null && collectMethod.GetParameters().Length == 0)
                collectMethod.Invoke(behaviour, null);
        }
    }

    /// <summary> 兼容旧首通管理器：存在则调用，不存在则跳过。 </summary>
    private static void TryMarkLevelAsFirstCleared(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type managerType = assemblies[i].GetType("LevelFirstClearManager", false);
            if (managerType == null) continue;

            MethodInfo markMethod = managerType.GetMethod("MarkLevelAsFirstCleared", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (markMethod != null)
            {
                var p = markMethod.GetParameters();
                if (p.Length == 1 && p[0].ParameterType == typeof(string))
                    markMethod.Invoke(null, new object[] { sceneName });
            }
            return;
        }
    }

    /// <summary> "重新挑战"按钮点击：重新加载当前塔防关卡，关卡从头开始。仅在真正胜利/失败弹出结束菜单后才响应。 </summary>
    public void OnRetryLevel()
    {
        if (!_alreadyShown) return; // 未到结束条件就误点，直接忽略
        Time.timeScale = 1f;
        string sceneName = SceneManager.GetActiveScene().name;
        // 标记为重新挑战（清零关卡内状态，但关卡进度保留）
        LevelSceneLoadContext.SetFromRetry();
                VideoSceneLoader.LoadScene(sceneName);
    }

}
