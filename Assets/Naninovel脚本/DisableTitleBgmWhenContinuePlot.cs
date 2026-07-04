using UnityEngine;

public class DisableTitleBgmWhenContinuePlot : MonoBehaviour
{
    void Awake()
    {
        if (!NaninovelReturnRequest.HasRequest) return;
        foreach (var src in GetComponentsInChildren<AudioSource>(true))
        {
            src.enabled = false;
            if (src.isPlaying) src.Stop();
        }
    }
}
