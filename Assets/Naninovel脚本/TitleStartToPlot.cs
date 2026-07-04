using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class TitleStartToPlot : MonoBehaviour
{
    [Tooltip("入口场景名，无存档时进入")]
    public string entrySceneName = "RogueEntry";
    [Tooltip("关卡选择场景名，有存档时进入")]
    public string plotSceneName = "plot";

    void Awake()
    {
        // 只在 NewGameButton 上生效；剧情碎片按钮虽然也挂了本脚本，但不应接管它的行为
        if (gameObject.name != "NewGameButton") return;

        RogueRuntimeState.InitIfNeeded();
        var btn = GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickStart);
        RefreshButtonLabel();
    }

    void RefreshButtonLabel()
    {
        string label = RogueRuntimeState.HasActiveRun ? "继续" : "开始游戏";
        var tmp = GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = label;
        else
        {
            var legacy = GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
        }
    }

    void OnClickStart()
    {
        Time.timeScale = 1f;
        if (RogueRuntimeState.HasActiveRun)
            VideoSceneLoader.LoadScene(plotSceneName);
        else
            VideoSceneLoader.LoadScene(entrySceneName);
    }
}
