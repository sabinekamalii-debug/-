using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// 处理场景过渡逻辑：失败点击返回Title、黑屏淡出、死亡剧本跳转
/// 从原RogueResultController分离
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    private GameObject _clickToReturnToTitleOverlay;
    
    /// <summary>
    /// 失败时显示点击屏幕返回Title的覆盖层
    /// </summary>
    public void EnsureClickToReturnToTitleOverlay(TMP_Text titleText)
    {
        if (_clickToReturnToTitleOverlay != null)
        {
            _clickToReturnToTitleOverlay.SetActive(true);
            _clickToReturnToTitleOverlay.transform.SetAsLastSibling();
            return;
        }
        
        Canvas canvas = null;
        if (titleText != null)
            canvas = titleText.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = RogueUIUtil.FindSceneCanvas();
        if (canvas == null) return;

        // 创建全屏点击覆盖层
        var go = new GameObject("ClickToReturnToTitle", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.01f);
        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnClickReturnToTitle);
        
        // 添加提示文字
        var textGo = new GameObject("ReturnToTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.15f);
        textRt.anchorMax = new Vector2(0.5f, 0.15f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(400f, 60f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "点击屏幕返回Title";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        
        _clickToReturnToTitleOverlay = go;
    }
    
    private void OnClickReturnToTitle()
    {
        if (_clickToReturnToTitleOverlay != null)
            _clickToReturnToTitleOverlay.SetActive(false);
        
        // 失败时跳转到死亡剧情，胜利时返回Title
        VideoSceneLoader.LoadScene(SceneNames.Title);
    }
    
    /// <summary>
    /// 跳转到死亡剧本（失败时）
    /// </summary>
    public void LoadDeathScene()
    {
        // 使用NaninovelReturnRequest加载死亡1剧情
        NaninovelReturnRequest.Set("死亡 1", "");
        VideoSceneLoader.LoadScene(SceneNames.Title);
    }
    
    /// <summary>
    /// 失败时的黑屏淡出动画
    /// </summary>
    public IEnumerator FadeToBlackThenLoadDeathScene(TMP_Text titleText)
    {
        // 创建全屏黑色覆盖层
        Canvas canvas = null;
        if (titleText != null)
            canvas = titleText.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = RogueUIUtil.FindSceneCanvas();
        
        if (canvas != null)
        {
            GameObject fadeOverlay = new GameObject("FadeToBlack", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fadeOverlay.transform.SetParent(canvas.transform, false);
            
            RectTransform fadeRt = fadeOverlay.GetComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.offsetMin = Vector2.zero;
            fadeRt.offsetMax = Vector2.zero;
            
            UnityEngine.UI.Image fadeImage = fadeOverlay.GetComponent<UnityEngine.UI.Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = true;
            
            // 1秒内渐变到黑色
            float duration = 1f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeImage.color = new Color(0f, 0f, 0f, t);
                yield return null;
            }
            
            // 跳转前销毁覆盖层
            Destroy(fadeOverlay);
        }
        
        // 跳转到死亡剧本
        LoadDeathScene();
    }
    
    /// <summary>
    /// 隐藏点击返回Title覆盖层
    /// </summary>
    public void HideClickToReturnToTitleOverlay()
    {
        if (_clickToReturnToTitleOverlay != null)
            _clickToReturnToTitleOverlay.SetActive(false);
    }
    
    /// <summary>
    /// 设置失败时背景为深蓝色
    /// </summary>
    public void SetFailureBackground(GameObject resultPanel)
    {
        if (resultPanel != null)
        {
            // 尝试获取背景Image组件
            var backgroundImage = resultPanel.GetComponent<UnityEngine.UI.Image>();
            if (backgroundImage != null)
            {
                // 设置为深蓝色
                backgroundImage.color = new Color(0.1f, 0.2f, 0.4f, 0.8f);
            }
            
            // 同时设置覆盖层为深蓝色
            if (_clickToReturnToTitleOverlay != null)
            {
                var overlayImage = _clickToReturnToTitleOverlay.GetComponent<UnityEngine.UI.Image>();
                if (overlayImage != null)
                {
                    overlayImage.color = new Color(0.1f, 0.2f, 0.4f, 0.01f);
                }
            }
        }
    }
}