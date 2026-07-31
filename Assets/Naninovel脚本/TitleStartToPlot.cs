using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class TitleStartToPlot : MonoBehaviour
{
    [Tooltip("开始游戏时进入的场景")]
    public string entrySceneName = SceneNames.RogueEntry;

    void Start()
    {
        // 只在 NewGameButton 上生效；剧情碎片按钮虽然也挂了本脚本，但不应接管它的行为
        if (gameObject.name != "NewGameButton") return;

        var btn = GetComponent<Button>();
        if (btn == null) return;
        // 在 Start 中移除所有监听，确保 Naninovel TitleNewGameButton.OnEnable 添加的监听也被清除
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickStart);
    }

    void OnClickStart()
    {
        Time.timeScale = 1f;
        // 每次点击开始游戏都强制重置，开始全新一局
        RogueRuntimeState.ForceResetRun();
        VideoSceneLoader.LoadScene(entrySceneName);
    }
}
