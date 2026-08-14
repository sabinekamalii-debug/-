using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance;

    [Header("UI组件引用 (把Panel拖进来)")]
    public GameObject panelRoot;
    public Button fightButton;
    public Button avoidButton;
    [Tooltip("「返回」按钮：仅先锋干员遭遇时显示（先锋是打探情报的斥候，收集完可返回守护点领部署点）。可不拖：自动按名称匹配")]
    public Button returnButton;
    [Tooltip("「一路避让」按钮：默认隐藏，鼠标悬浮在「避让」上时在其旁边显示。已在场景中手动创建并绑定（位于避让按钮右侧）")]
    public Button avoidAllButton;

    [Header("超时（防软锁死）")]
    [Tooltip("秒数内未选择则自动视为避让并关闭菜单，0 表示不超时")]
    public float autoCloseAfterSeconds = 12f;

    private OperatorUnit currentOperator;
    private Coroutine _timeoutRoutine;
    private Coroutine _hideAvoidAllRoutine;
    private bool _isActive;

    /// <summary>当前是否有遭遇战菜单正在显示（供 GameSpeedBoost 等判断是否应保持暂停）。</summary>
    public bool IsEncounterActive => _isActive;

    void Awake()
    {
        Instance = this;
        AutoBindUIIfNeeded();

        if (panelRoot != null) panelRoot.SetActive(false);

        if (fightButton != null)
        {
            fightButton.onClick.RemoveListener(OnFightClicked);
            fightButton.onClick.AddListener(OnFightClicked);
        }

        if (avoidButton != null)
        {
            avoidButton.onClick.RemoveListener(OnAvoidClicked);
            avoidButton.onClick.AddListener(OnAvoidClicked);
        }

        if (avoidAllButton != null)
        {
            avoidAllButton.onClick.RemoveAllListeners();
            avoidAllButton.onClick.AddListener(OnAvoidAllClicked);
            avoidAllButton.gameObject.SetActive(false);
        }

        // 鼠标悬浮「避让」按钮时显示「一路避让」按钮
        SetupAvoidAllHover();

        // 注意：returnButton 的回调在 TriggerEncounter 中按干员类型（先锋/非先锋）动态绑定，
        // 这里不再固定绑定，避免与非先锋的「返回」/先锋的「侦察」逻辑冲突。
    }

    void AutoBindUIIfNeeded()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (fightButton != null && avoidButton != null && returnButton != null) return;

        var searchRoot = panelRoot != null ? panelRoot.transform : transform;
        var buttons = searchRoot.GetComponentsInChildren<Button>(true);

        if (fightButton == null)
        {
            foreach (var b in buttons)
            {
                var n = b.name.ToLowerInvariant();
                if (n.Contains("fight") || n.Contains("battle") || n.Contains("atk") || n.Contains("attack") ||
                    n.Contains("zhandou") || n.Contains("gongji") || n.Contains("战斗") || n.Contains("攻击"))
                {
                    fightButton = b;
                    break;
                }
            }
        }

        if (avoidButton == null)
        {
            foreach (var b in buttons)
            {
                var n = b.name.ToLowerInvariant();
                if (n.Contains("avoid") || n.Contains("escape") || n.Contains("run") ||
                    n.Contains("huibi") || n.Contains("duobi") || n.Contains("回避") || n.Contains("躲") || n.Contains("闪避") || n.Contains("避让"))
                {
                    avoidButton = b;
                    break;
                }
            }
        }

        if (returnButton == null)
        {
            foreach (var b in buttons)
            {
                var n = b.name.ToLowerInvariant();
                if (n.Contains("return") || n.Contains("fanhui") || n.Contains("返回") || n.Contains("撤退"))
                {
                    returnButton = b;
                    break;
                }
            }
        }

        if ((fightButton == null || avoidButton == null || returnButton == null) && buttons != null && buttons.Length == 2)
        {
            if (fightButton == null) fightButton = buttons[0];
            if (avoidButton == null) avoidButton = buttons[1];
        }
    }

    public void TriggerEncounter(OperatorUnit unit)
    {
        currentOperator = unit;
        _isActive = true;
        Time.timeScale = 0f;

        var endMenu = FindFirstObjectByType<LevelEndMenu>();
        if (endMenu != null) endMenu.ForceHideEndMenu();

        if (panelRoot != null) panelRoot.SetActive(true);

        // 「一路避让」按钮默认隐藏，仅悬浮「避让」时显示
        if (avoidAllButton != null) avoidAllButton.gameObject.SetActive(false);

        // 第三个按钮「返回」：仅先锋干员显示（先锋是打探情报的斥候，收集完情报后可返回守护点领部署点）。
        // 先锋遭遇战 = 战斗 / 避让 / 返回（3个按钮）；其他干员遭遇战 = 战斗 / 避让（2个按钮，无返回）。
        bool isVanguard = currentOperator != null && currentOperator.data != null
            && currentOperator.data.opType == OperatorData.OperatorType.Vanguard;
        if (returnButton != null)
        {
            returnButton.gameObject.SetActive(isVanguard);
            if (isVanguard)
            {
                var label = returnButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null) label.text = "返回";
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        if (currentOperator != null)
            currentOperator.SetHighlight(true);

        if (autoCloseAfterSeconds > 0 && _timeoutRoutine == null)
            _timeoutRoutine = StartCoroutine(TimeoutRoutine());
    }

    void OnFightClicked()
    {
        if (currentOperator != null) currentOperator.ResolveEncounter(true);
        CloseMenu();
    }

    void OnAvoidClicked()
    {
        if (currentOperator != null) currentOperator.ResolveEncounter(false);
        CloseMenu();
    }

    void OnReturnClicked()
    {
        if (currentOperator != null) currentOperator.ReturnToGuardPoint();
        CloseMenu();
    }

    /// <summary>「一路避让」：当前干员后续遇到敌人直接默认避让，不再弹菜单、不暂停。 </summary>
    void OnAvoidAllClicked()
    {
        if (currentOperator != null)
        {
            currentOperator.EnableAvoidAllEncounters();
            currentOperator.ResolveEncounter(false); // 对当前敌人立即按避让处理
        }
        CloseMenu();
    }

    void CloseMenu()
    {
        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        if (_hideAvoidAllRoutine != null)
        {
            StopCoroutine(_hideAvoidAllRoutine);
            _hideAvoidAllRoutine = null;
        }

        if (currentOperator != null)
            currentOperator.SetHighlight(false);

        if (avoidAllButton != null) avoidAllButton.gameObject.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        _isActive = false;
        currentOperator = null;
    }

    IEnumerator TimeoutRoutine()
    {
        float timer = autoCloseAfterSeconds;
        while (timer > 0)
        {
            yield return new WaitForSecondsRealtime(1f);
            timer -= 1f;
        }
        OnAvoidClicked();
    }

    public void ForceCloseEncounterMenu()
    {
        CloseMenu();
    }

    #region 一路避让按钮（场景绑定 + 悬浮显隐）

    private void SetupAvoidAllHover()
    {
        if (avoidButton == null || avoidAllButton == null) return;
        AddHoverEvents(avoidButton);
        AddHoverEvents(avoidAllButton);
    }

    private void AddHoverEvents(Button target)
    {
        var trigger = target.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowAvoidAll());
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ScheduleHideAvoidAll());
        trigger.triggers.Add(exit);
    }

    private void ShowAvoidAll()
    {
        if (avoidAllButton == null) return;
        if (_hideAvoidAllRoutine != null)
        {
            StopCoroutine(_hideAvoidAllRoutine);
            _hideAvoidAllRoutine = null;
        }
        avoidAllButton.gameObject.SetActive(true);
    }

    private void ScheduleHideAvoidAll()
    {
        if (avoidAllButton == null) return;
        if (_hideAvoidAllRoutine != null) StopCoroutine(_hideAvoidAllRoutine);
        _hideAvoidAllRoutine = StartCoroutine(HideAvoidAllAfterDelay());
    }

    private IEnumerator HideAvoidAllAfterDelay()
    {
        // 短暂延迟，避免鼠标从「避让」移动到「一路避让」的瞬间误隐藏
        yield return new WaitForSecondsRealtime(0.18f);
        if (avoidAllButton != null) avoidAllButton.gameObject.SetActive(false);
        _hideAvoidAllRoutine = null;
    }

    #endregion
}
