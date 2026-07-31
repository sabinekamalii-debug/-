using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 卡片翻卡视频播放器：管理视频播放、点击继续覆盖层、RenderTexture等
/// 从原RogueResultController分离
/// </summary>
public class CardRevealPlayer : MonoBehaviour
{
    [Header("视频配置")]
    [SerializeField] private VideoClip attackCardRevealVideo;
    [SerializeField] private VideoClip defenseCardRevealVideo;
    [SerializeField] private VideoClip guardianCardRevealVideo;
    [SerializeField] private VideoClip rareCardRevealVideo;
    [SerializeField] private VideoClip skillCardRevealVideo;
    [SerializeField] private VideoClip specialCardRevealVideo;
    [SerializeField] private VideoPlayer cardRevealVideoPlayer;
    [SerializeField] private GameObject cardRevealVideoPanel;
    
    [Header("动画配置")]
    [SerializeField] private string revealAnimatorStateName = "Reveal";
    [SerializeField] private float revealAnimDuration = 0.6f;
    [SerializeField] private float cardRevealVideoScale = 0.85f;
    [SerializeField] private float cardRevealVideoSpeed = 1.5f;
    
    private VideoPlayer _fallbackRevealVideoPlayer;
    private GameObject _fallbackRevealVideoPanel;
    private RenderTexture _fallbackRevealRenderTexture;
    private GameObject _revealClickOverlay;
    private bool _revealClickToClose;
    
    /// <summary>
    /// 开始翻卡协程
    /// </summary>
    public IEnumerator RevealThenPick(int slotIndex, TalentCardData card, Transform slotRoot, System.Action onRevealComplete)
    {
        var clip = GetRevealVideoForType(card != null ? card.cardType : TalentCardType.Special);
        var player = cardRevealVideoPlayer != null ? cardRevealVideoPlayer : EnsureFallbackRevealVideoPlayer();
        
        if (clip != null && player != null)
        {
            var panel = cardRevealVideoPanel != null ? cardRevealVideoPanel : (player.gameObject);
            if (_fallbackRevealVideoPanel != null && player == _fallbackRevealVideoPlayer)
            {
                panel = _fallbackRevealVideoPanel;
                EnsureRenderTextureForClip(clip);
                if (_fallbackRevealRenderTexture != null) player.targetTexture = _fallbackRevealRenderTexture;
                if (slotRoot != null)
                {
                    panel.transform.SetParent(slotRoot, false);
                    var rt = panel.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    float scale = Mathf.Clamp(cardRevealVideoScale, 0.3f, 1f);
                    panel.transform.localScale = new Vector3(scale, scale, 1f);
                    panel.transform.SetAsLastSibling();
                }
            }
            panel.SetActive(true);
            player.clip = clip;
            float speed = Mathf.Clamp(cardRevealVideoSpeed, 0.5f, 10f);
            player.playbackSpeed = speed;
            player.Prepare();
            player.Play();
            float waitTime = (float)clip.length / speed;
            if (waitTime <= 0f) waitTime = revealAnimDuration;
            yield return new WaitForSecondsRealtime(waitTime);
            player.Stop();
            panel.SetActive(false);
        }
        else if (slotRoot != null)
        {
            var anim = slotRoot.GetComponent<Animator>();
            if (anim != null && !string.IsNullOrEmpty(revealAnimatorStateName))
            {
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                anim.Play(revealAnimatorStateName, 0, 0f);
                float elapsed = 0f;
                while (elapsed < revealAnimDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(revealAnimDuration);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(revealAnimDuration);
        }
        
        // 翻卡完成，触发回调
        onRevealComplete?.Invoke();
        
        // 显示点击继续覆盖层
        EnsureRevealClickOverlay();
        _revealClickToClose = false;
        if (_revealClickOverlay != null)
        {
            _revealClickOverlay.SetActive(true);
            _revealClickOverlay.transform.SetAsLastSibling();
            var overlayBtn = _revealClickOverlay.GetComponent<Button>();
            if (overlayBtn != null)
            {
                overlayBtn.onClick.RemoveAllListeners();
                overlayBtn.onClick.AddListener(OnRevealClickToClose);
            }
        }
        yield return new WaitUntil(() => _revealClickToClose);
        if (_revealClickOverlay != null) _revealClickOverlay.SetActive(false);
    }
    
    /// <summary>
    /// 根据卡片类型获取对应的翻卡视频
    /// </summary>
    public VideoClip GetRevealVideoForType(TalentCardType type)
    {
        switch (type)
        {
            case TalentCardType.Attack: return attackCardRevealVideo;
            case TalentCardType.Defense: return defenseCardRevealVideo;
            case TalentCardType.Guardian: return guardianCardRevealVideo;
            case TalentCardType.Rare: return rareCardRevealVideo;
            case TalentCardType.Skill: return skillCardRevealVideo;
            case TalentCardType.Special: return specialCardRevealVideo;
            default: return attackCardRevealVideo;
        }
    }
    
    /// <summary>
    /// 创建点击继续覆盖层
    /// </summary>
    private void EnsureRevealClickOverlay()
    {
        if (_revealClickOverlay != null) return;
        
        var go = new GameObject("RevealClickToContinue", typeof(RectTransform), typeof(Image), typeof(Button));
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
        
        // 添加提示文字
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.15f);
        textRect.anchorMax = new Vector2(0.5f, 0.15f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400f, 60f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "点击屏幕继续";
        tmp.fontSize = 28;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        
        go.SetActive(false);
        _revealClickOverlay = go;
    }
    
    private void OnRevealClickToClose()
    {
        _revealClickToClose = true;
    }
    
    /// <summary>
    /// 未在Inspector拖VideoPlayer时，自动创建播视频用的VideoPlayer + RawImage
    /// </summary>
    private VideoPlayer EnsureFallbackRevealVideoPlayer()
    {
        if (_fallbackRevealVideoPlayer != null) return _fallbackRevealVideoPlayer;
        
        var canvas = RogueUIUtil.FindSceneCanvas();
        if (canvas == null) return null;
        
        _fallbackRevealRenderTexture = new RenderTexture(1280, 720, 0);
        _fallbackRevealRenderTexture.name = "RogueRevealVideoRT";
        
        var panelGo = new GameObject("RogueResult_RevealVideoPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var rawImg = panelGo.AddComponent<RawImage>();
        rawImg.color = Color.white;
        rawImg.texture = _fallbackRevealRenderTexture;
        
        var videoGo = new GameObject("RogueResult_RevealVideoPlayer");
        videoGo.transform.SetParent(panelGo.transform, false);
        var vp = videoGo.AddComponent<VideoPlayer>();
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = _fallbackRevealRenderTexture;
        vp.isLooping = false;
        vp.playOnAwake = false;
        
        panelGo.SetActive(false);
        _fallbackRevealVideoPlayer = vp;
        _fallbackRevealVideoPanel = panelGo;
        return _fallbackRevealVideoPlayer;
    }
    
    /// <summary>
    /// 按视频尺寸创建/更新RenderTexture，不裁切视频
    /// </summary>
    private void EnsureRenderTextureForClip(VideoClip clip)
    {
        if (clip == null) return;
        uint w = clip.width;
        uint h = clip.height;
        if (w < 1) w = 1280;
        if (h < 1) h = 720;
        int iw = (int)w;
        int ih = (int)h;
        
        if (_fallbackRevealRenderTexture != null && _fallbackRevealRenderTexture.width == iw && _fallbackRevealRenderTexture.height == ih)
            return;
        
        if (_fallbackRevealRenderTexture != null)
        {
            _fallbackRevealRenderTexture.Release();
            _fallbackRevealRenderTexture = null;
        }
        
        _fallbackRevealRenderTexture = new RenderTexture(iw, ih, 0);
        _fallbackRevealRenderTexture.name = "RogueRevealVideoRT";
        
        if (_fallbackRevealVideoPanel != null)
        {
            var raw = _fallbackRevealVideoPanel.GetComponent<RawImage>();
            if (raw != null) raw.texture = _fallbackRevealRenderTexture;
        }
    }
    
    /// <summary>
    /// 停止并清理视频播放器
    /// </summary>
    public void Cleanup()
    {
        if (_fallbackRevealVideoPlayer != null)
        {
            _fallbackRevealVideoPlayer.Stop();
        }
        
        if (_revealClickOverlay != null)
        {
            Destroy(_revealClickOverlay);
        }
        
        if (_fallbackRevealRenderTexture != null)
        {
            _fallbackRevealRenderTexture.Release();
            _fallbackRevealRenderTexture = null;
        }
    }
}