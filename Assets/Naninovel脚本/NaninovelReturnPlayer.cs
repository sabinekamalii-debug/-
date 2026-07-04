using UnityEngine;
using Naninovel;
using System.Collections;

public class NaninovelReturnPlayer : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(ReactivateThenPlayReturn());
    }

    IEnumerator ReactivateThenPlayReturn()
    {
        yield return new WaitUntil(() => Engine.Initialized);
        yield return null;
        HideNaninovelUIOnLevelLoad.ReactivateNaninovelUI();
        HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();
        yield return null;
        yield return null;
        yield return null;
        HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();
        StartCoroutine(ForceShowDialoguePanelAfterFrames(3));
        var uiManager = Engine.GetService<IUIManager>();
        bool shouldHideForScript = NaninovelReturnRequest.HasRequest || NaninovelReturnRequest.IsPlayingReturnScript;
        if (NaninovelReturnRequest.IsPlayingReturnScript)
            NaninovelReturnRequest.ClearPlayingReturnScript();
        if (shouldHideForScript)
        {
            if (uiManager != null)
                uiManager.GetUI<Naninovel.UI.ITitleUI>()?.Hide();
            FixTitleUIRaycast.EnableContinueTriggerRaycast();
            StartCoroutine(EnsureNaninovelUICameraBoundAfterFrames(5));
            StartCoroutine(ReapplyCameraModeAndPositionRepeatedly(2f, 0.25f));
            var audioManager = Engine.GetService<IAudioManager>();
            if (audioManager != null)
                audioManager.StopAllBgm(0f).Forget();
        }
        else
        {
            if (uiManager != null)
                uiManager.GetUI<Naninovel.UI.ITitleUI>()?.Show();
            FixTitleUIRaycast.ApplyOnce();
        }
    }

    IEnumerator ForceShowDialoguePanelAfterFrames(int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
        HideNaninovelUIOnLevelLoad.ForceShowDialoguePanel();
    }

    IEnumerator EnsureNaninovelUICameraBoundAfterFrames(int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
        HideNaninovelUIOnLevelLoad.BindNaninovelUICanvasesToUICameraPublic();
        HideNaninovelUIOnLevelLoad.ForceShowDialoguePanel();
    }

    IEnumerator ReapplyCameraModeAndPositionRepeatedly(float totalSeconds, float interval)
    {
        float t = 0f;
        while (t < totalSeconds)
        {
            yield return new WaitForSecondsRealtime(interval);
            t += interval;
            if (!Engine.Initialized) yield break;
            HideNaninovelUIOnLevelLoad.BindNaninovelUICanvasesToUICameraPublic();
        }
    }
}
