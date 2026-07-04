using UnityEngine;

/// <summary>
/// 挂到任意常驻物体（如首个场景的某个根物体）。运行时会以当前显示器分辨率进入全屏窗口模式，避免“一运行窗口就缩小”。
/// 若不需要可禁用此组件。
/// </summary>
public class ForceFullscreenOnStart : MonoBehaviour
{
    [Tooltip("是否在 Start 时设为全屏窗口（无边框铺满屏幕）")]
    [SerializeField] private bool setFullscreenOnStart = true;

    void Start()
    {
        if (!setFullscreenOnStart) return;
        // 无边框全屏窗口，分辨率与当前显示器一致，避免小窗口
        Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
    }
}
