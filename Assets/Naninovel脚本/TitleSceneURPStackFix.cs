using UnityEngine;
using Naninovel;
using System.Collections;

public class TitleSceneURPStackFix : MonoBehaviour
{
    public float initialDelay = 0.8f;
    public float retryWindow = 2.5f;
    public float retryInterval = 0.4f;

    void Start() => StartCoroutine(RunFixRepeatedly());

    IEnumerator RunFixRepeatedly()
    {
        yield return new WaitUntil(() => Engine.Initialized);
        yield return new WaitForSecondsRealtime(initialDelay);
        float elapsed = 0f;
        while (elapsed < retryWindow)
        {
            HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();
            yield return new WaitForSecondsRealtime(retryInterval);
            elapsed += retryInterval;
        }
    }
}
