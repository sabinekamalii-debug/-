using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Naninovel;
using Naninovel.UI;

public class NaninovelReturnAutoPlayer : MonoBehaviour
{
    static NaninovelReturnAutoPlayer _instance;

    // Domain Reload 禁用时，每次 Enter Play Mode 手动重置静态引用，
    // 避免 _instance 指向已被销毁的 DontDestroyOnLoad 幽灵对象导致 Ensure 跳过。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlaymodeEnter()
    {
        _instance = null;
    }

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

        // 提取返回场景（在 TryConsume 之后仍有 ReturnScene 可用，因为它没被清掉）
        string returnScene = NaninovelReturnRequest.ReturnScene;
        NaninovelReturnRequest.ClearReturnScene();

        NaninovelReturnRequest.SetPlayingReturnScript();
        scriptPath = NormalizeScriptName(scriptPath);

        HideNaninovelUIOnLevelLoad.ReactivateNaninovelUI();
        HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();

        var uiManager = Engine.GetService<IUIManager>();
        if (uiManager != null)
        {
            var titleUI = uiManager.GetUI<ITitleUI>();
            if (titleUI != null && titleUI.Visible)
                titleUI.Hide();
        }

        FixTitleUIRaycast.EnableContinueTriggerRaycast();

        var player = Engine.GetService<IScriptPlayer>();
        if (player == null || string.IsNullOrEmpty(scriptPath)) yield break;

        // 不修改 SkipMode（ReadOnly 会跳过已读命令，Everything 会全跳）
        // 关键修复：LoadAndPlay 是异步的，必须先等 Playing 变 true 再等它变 false
        // 否则 WaitWhile 在 Playing 还是 false 时就立刻通过，导致剧本秒退

        if (string.IsNullOrEmpty(label))
            player.LoadAndPlay(scriptPath).Forget();
        else
            player.LoadAndPlayAtLabel(scriptPath, label).Forget();

        // 如果有返回场景，等剧本播完后自动切回去
        if (!string.IsNullOrEmpty(returnScene))
        {
            // 先等 Playing 变 true（LoadAndPlay 异步，不会立刻生效）
            yield return new WaitUntil(() => player.Playing);
            // 再等 Playing 变 false（剧本播完）
            yield return new WaitWhile(() => player.Playing);
            yield return new WaitForSecondsRealtime(0.8f);
            VideoSceneLoader.LoadScene(returnScene);
        }
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
