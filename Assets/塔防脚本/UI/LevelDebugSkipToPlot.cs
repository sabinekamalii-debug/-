using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 仅编辑器调试用：在关卡场景中按 M 键可立即结束当前关卡并跳转到下一段剧情（与点击「继续剧情」效果相同）。
/// 挂在关卡场景里任意常驻物体上（如与 LevelEndMenu 同一物体），发布构建中不会生效。
/// </summary>
public class LevelDebugSkipToPlot : MonoBehaviour
{
    const string PlotSceneName = "Title";

    void Update()
    {
#if UNITY_EDITOR
        if (!UnityEngine.Application.isEditor) return;
        if (!Input.GetKeyDown(KeyCode.M)) return;

        LevelEndMenu menu = LevelEndMenu.Instance;
        if (menu == null) return;

        Time.timeScale = 1f;
        // HideNaninovelUIOnLevelLoad.ReactivateNaninovelUI();
        // HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();

        string scriptName = menu.scriptName;
        string actualLabel;
        if (string.IsNullOrEmpty(menu.labelName))
        {
            string sceneName = SceneManager.GetActiveScene().name.Replace(" ", "");
            actualLabel = "After" + (sceneName.Length > 0 ? char.ToUpperInvariant(sceneName[0]) + sceneName.Substring(1) : "LevelA");
        }
        else
            actualLabel = menu.labelName;

        // NaninovelReturnRequest.Set(scriptName, actualLabel); // TODO: Naninovel包缺失
        VideoSceneLoader.LoadScene(PlotSceneName);
#endif
    }
}
