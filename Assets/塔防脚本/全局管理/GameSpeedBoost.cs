using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按住 Ctrl 时游戏速度变为 8 倍，松开恢复 1 倍。
/// 以下情况不覆盖 Time.timeScale，保持其它系统的暂停逻辑：游戏结束、遭遇战菜单、部署拖拽/寻路选点/撤退模式、传送模式。
/// </summary>
public class GameSpeedBoost : MonoBehaviour
{
    [Header("倍速")]
    [Tooltip("按住快捷键时的 Time.timeScale，运行时强制为 24")]
    public float speedWhenHeld = 24f;

    void Awake()
    {
        speedWhenHeld = 24f;
    }

    [Header("快捷键")]
    [Tooltip("勾选 Left 或 Right 即生效，可同时勾选两个")]
    public bool useLeftControl = true;
    public bool useRightControl = true;

    void Update()
    {
        // 游戏结束：保持 0，不干预
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
            return;

        // 新手教程进行中：保持 0，不干预
        if (NewbieTutorialController.IsTutorialActive)
            return;

        // 部署拖拽、寻路选点、撤退模式：DeploymentManager 已设 timeScale=0，不覆盖
        if (DeploymentManager.Instance != null && DeploymentManager.Instance.isGamePaused)
            return;

        // 传送模式（R 键选干员 + 目标格子）：TeleportController 已设 timeScale=0，不覆盖
        if (TeleportController.Instance != null && TeleportController.Instance.IsInTeleportMode)
            return;

        // 第一关抽卡流程中：保持 0，不干预
        if (RogueResultController.IsMidGameDrop || RogueResultController.IsFirstStageDrop)
            return;

        // 紫色敌人死亡抽卡中：保持 0，不干预
        if (GameManager.Instance != null && GameManager.Instance.IsPurpleEnemyDropProcessing())
            return;

        bool leftHeld  = useLeftControl  && Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        bool rightHeld = useRightControl && Keyboard.current != null && Keyboard.current.rightCtrlKey.isPressed;

        Time.timeScale = (leftHeld || rightHeld) ? speedWhenHeld : 1f;
    }
}
