using UnityEngine;
using UnityEngine.UI;

public class ReturnToTitleButton : MonoBehaviour
{
    [Tooltip("要返回的主菜单场景名（需已加入 Build Settings）")]
    [SerializeField] private string titleSceneName = "Title";

    [Tooltip("左上角边距（像素）。x=向右，y=向下")]
    [SerializeField] private Vector2 topLeftMargin = new Vector2(20f, 20f);

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveListener(GoTitle);
            btn.onClick.AddListener(GoTitle);
        }

        var rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(topLeftMargin.x, -topLeftMargin.y);
        }
    }

    private void GoTitle()
    {
        if (string.IsNullOrEmpty(titleSceneName)) titleSceneName = "Title";
        VideoSceneLoader.LoadScene(titleSceneName);
    }
}
