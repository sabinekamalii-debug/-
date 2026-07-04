using UnityEngine;
using System.Collections;

/// <summary>
/// 挂到「获得剧情碎片」卡片根物体上：激活后显示指定秒数，然后消失（可选用时长的淡出）。
/// 由 LevelEndMenu 在弹出结束菜单并解锁剧情卡时将该物体 SetActive(true)，本脚本负责 5 秒后隐藏。
/// </summary>
public class FragmentCardHideAfterSeconds : MonoBehaviour
{
    [Tooltip("显示多少秒后消失")]
    public float displaySeconds = 5f;
    [Tooltip("消失前淡出时长（0=立即隐藏）")]
    public float fadeOutSeconds = 0f;

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(WaitThenHide());
    }

    IEnumerator WaitThenHide()
    {
        if (displaySeconds > 0f)
            yield return new WaitForSecondsRealtime(displaySeconds);

        if (fadeOutSeconds > 0f)
        {
            float elapsed = 0f;
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                while (elapsed < fadeOutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeOutSeconds);
                    yield return null;
                }
            }
            else
            {
                var img = GetComponent<UnityEngine.UI.Image>();
                var tmp = GetComponentInChildren<TMPro.TMP_Text>(true);
                Color cImg = img != null ? img.color : Color.white;
                Color cTxt = tmp != null ? tmp.color : Color.white;
                elapsed = 0f;
                while (elapsed < fadeOutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeOutSeconds);
                    if (img != null) img.color = new Color(cImg.r, cImg.g, cImg.b, 1f - t);
                    if (tmp != null) tmp.color = new Color(cTxt.r, cTxt.g, cTxt.b, 1f - t);
                    yield return null;
                }
            }
        }

        gameObject.SetActive(false);
    }
}
