using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance;

    [Header("UI组件引用 (把Panel拖进来)")]
    public GameObject panelRoot;
    public Button fightButton;
    public Button avoidButton;

    [Header("超时（防软锁死）")]
    [Tooltip("秒数内未选择则自动视为避让并关闭菜单，0 表示不超时")]
    public float autoCloseAfterSeconds = 12f;

    private OperatorUnit currentOperator;
    private Coroutine _timeoutRoutine;
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
    }

    void AutoBindUIIfNeeded()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (fightButton != null && avoidButton != null) return;

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

        if ((fightButton == null || avoidButton == null) && buttons != null && buttons.Length == 2)
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

    void CloseMenu()
    {
        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        if (currentOperator != null)
            currentOperator.SetHighlight(false);

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
}
