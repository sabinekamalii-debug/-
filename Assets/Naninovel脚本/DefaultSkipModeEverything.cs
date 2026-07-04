using UnityEngine;
using Naninovel;
using System.Collections;

public class DefaultSkipModeEverything : MonoBehaviour
{
    void Start() => StartCoroutine(SetSkipModeWhenReady());

    IEnumerator SetSkipModeWhenReady()
    {
        if (!Engine.Initialized)
            yield return new WaitUntil(() => Engine.Initialized);
        var player = Engine.GetService<IScriptPlayer>();
        if (player != null)
            player.SkipMode = PlayerSkipMode.Everything;
    }
}
