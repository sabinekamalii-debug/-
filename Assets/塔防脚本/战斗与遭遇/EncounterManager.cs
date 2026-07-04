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

    void Awake()
    {
        Instance = this;
        AutoBindUIIfNeeded();

        if (panelRoot != null) panelRoot.SetActive(false);

        // 防止重复注册（Domain Reload 关闭/脚本重载等情况下容易叠加）
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
        // 目标：不改逻辑，只是把“忘记拖引用/对象失活导致找不到”这种配置问题自动补齐，避免空引用
        if (panelRoot == null)
        {
            // 常见情况：脚本挂在 Canvas 或 Panel 根物体上
            panelRoot = gameObject;
        }

        if (fightButton != null && avoidButton != null) return;

        var searchRoot = panelRoot != null ? panelRoot.transform : transform;
        var buttons = searchRoot.GetComponentsInChildren<Button>(true);

        // 先按名字猜（更不容易绑错）
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
                    n.Contains("huibi") || n.Contains("duobi") || n.Contains("回避") || n.Contains("躲") || n.Contains("闪避"))
                {
                    avoidButton = b;
                    break;
                }
            }
        }

        // 最后兜底：如果就两个按钮且还没绑齐，按顺序绑定（会提示警告，便于你手动拖对）
        if ((fightButton == null || avoidButton == null) && buttons != null && buttons.Length == 2)
        {
            if (fightButton == null) fightButton = buttons[0];
            if (avoidButton == null) avoidButton = buttons[1];
        }
    }

    public void TriggerEncounter(OperatorUnit unit)
    {
        currentOperator = unit;
        Time.timeScale = 0f; // 暂停时间

        // 0. 强制隐藏结束菜单，避免结束菜单在背后可被误点（只应在真正胜利/失败后才弹出）
        var endMenu = FindFirstObjectByType<LevelEndMenu>();
        if (endMenu != null) endMenu.ForceHideEndMenu();

        // 1. 激活黑底和按钮
        if (panelRoot != null) panelRoot.SetActive(true);

        // 2. 呼叫角色：把你自己的图层提到黑屏前面来！
        if (currentOperator != null)
        {
            currentOperator.SetHighlight(true);
        }
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

        // 菜单关闭时，把角色的图层降回去
        if (currentOperator != null)
        {
            currentOperator.SetHighlight(false);
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f; // 恢复时间
        currentOperator = null;
    }

    /// <summary> 外部调用：强制关闭遭遇战菜单（如结束菜单弹出时先关掉遭遇战，避免挡住结束菜单按钮）。 </summary>
    public void ForceCloseEncounterMenu()
    {
        CloseMenu();
    }
}