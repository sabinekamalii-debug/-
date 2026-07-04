using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Naninovel;
using Naninovel.UI;

public class NaninovelReturnAutoPlayer : MonoBehaviour
{
    static NaninovelReturnAutoPlayer _instance;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("NaninovelReturnAutoPlayer");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<NaninovelReturnAutoPlayer>();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Title") return;
        if (!NaninovelReturnRequest.HasRequest) return;
        StartCoroutine(PlayWhenReady());
    }

    IEnumerator PlayWhenReady()
    {
        yield return new WaitUntil(() => Engine.Initialized);
        if (!NaninovelReturnRequest.HasRequest) yield break;

        if (!NaninovelReturnRequest.TryConsume(out string scriptPath, out string label))
            yield break;
        NaninovelReturnRequest.SetPlayingReturnScript();
        scriptPath = NormalizeScriptName(scriptPath);
        HideNaninovelUIOnLevelLoad.ReactivateNaninovelUI();
        HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();
        var uiManager = Engine.GetService<IUIManager>();
        if (uiManager != null)
            uiManager.GetUI<ITitleUI>()?.Hide();
        FixTitleUIRaycast.EnableContinueTriggerRaycast();

        var player = Engine.GetService<IScriptPlayer>();
        if (player == null || string.IsNullOrEmpty(scriptPath)) yield break;
        if (string.IsNullOrEmpty(label))
            player.LoadAndPlay(scriptPath).Forget();
        else
            player.LoadAndPlayAtLabel(scriptPath, label).Forget();
    }

    static string NormalizeScriptName(string scriptPath)
    {
        var s = (scriptPath ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return s;
        if (s.StartsWith("魔王") && !s.StartsWith("魔王 "))
        {
            var suffix = s.Substring(2).Trim();
            if (!string.IsNullOrEmpty(suffix))
                s = $"魔王 {suffix}";
        }
        return s;
    }
}
