using UnityEngine;
using Naninovel;
using Naninovel.UI;
using System.Collections;

public class NaninovelMobileFontSize : MonoBehaviour
{
    [Range(2, 3)] public int mobileFontSizeIndex = 3;

    IEnumerator Start()
    {
        if (!Application.isMobilePlatform) yield break;
        if (!Engine.Initialized)
            yield return new WaitUntil(() => Engine.Initialized);
        var uiManager = Engine.GetService<IUIManager>();
        if (uiManager != null)
            uiManager.FontSize = mobileFontSizeIndex;
    }
}
