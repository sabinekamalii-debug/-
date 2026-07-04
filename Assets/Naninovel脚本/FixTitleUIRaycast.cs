using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Naninovel;

public class FixTitleUIRaycast : MonoBehaviour
{
    const bool EmitWarnings = false;

    Graphic _continueTriggerGraphic;
    Transform _titleUIRoot;
    float _nextCheck;
    bool _lastTitleVisible;
    static bool _warnedNoTitleUIEnsure;
    static bool _warnedTitleInactive;
    static bool _warnedNoCanvas;
    static bool _warnedApplyNoTitle;

    void Start() => Invoke(nameof(Apply), 0.5f);

    void Update()
    {
        bool titleVisible = IsTitleUIVisible();
        if (titleVisible && !_lastTitleVisible)
            EnsureTitleUIVisibleAndBgmEnabled();
        _lastTitleVisible = titleVisible;

        if (_continueTriggerGraphic == null) return;
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.15f;

        if (_continueTriggerGraphic.raycastTarget == titleVisible)
            _continueTriggerGraphic.raycastTarget = !titleVisible;
    }

    static void EnsureTitleUIVisibleAndBgmEnabled()
    {
        var titleRoot = FindTitleUI();
        if (titleRoot == null) { WarnOnce(ref _warnedNoTitleUIEnsure, "no TitleUI"); return; }
        if (!titleRoot.gameObject.activeInHierarchy) { WarnOnce(ref _warnedTitleInactive, "TitleUI inactive"); return; }

        var canvas = titleRoot.GetComponent<Canvas>();
        if (canvas != null)
        {
            var cam = Engine.Initialized ? Engine.GetService<ICameraManager>()?.UICamera : null;
            if (cam != null)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera) canvas.renderMode = RenderMode.ScreenSpaceCamera;
                if (canvas.worldCamera != cam) canvas.worldCamera = cam;
            }
            const int titleSortOrder = 100;
            if (canvas.sortingOrder != titleSortOrder) canvas.sortingOrder = titleSortOrder;
        }

        foreach (var src in titleRoot.GetComponentsInChildren<AudioSource>(true))
        {
            if (!src.enabled) src.enabled = true;
            if (src.clip != null && !src.isPlaying) src.Play();
        }
    }

    bool IsTitleUIVisible()
    {
        if (_titleUIRoot != null && _titleUIRoot.gameObject.activeInHierarchy) return true;
        var t = FindTitleUI();
        if (t != null && t.gameObject.activeInHierarchy) return true;
        if (!Engine.Initialized) return true;
        return Engine.GetService<IUIManager>()?.GetUI<Naninovel.UI.ITitleUI>()?.Visible ?? false;
    }

    void Apply()
    {
        ApplyOnce();
        _titleUIRoot = FindTitleUI();
        CacheContinueTrigger();
        if (_continueTriggerGraphic != null)
            _continueTriggerGraphic.raycastTarget = false;
    }

    void CacheContinueTrigger()
    {
        var nn = GameObject.Find("Naninovel<Runtime>");
        if (nn == null) return;
        var ui = nn.transform.Find("UI");
        if (ui == null) return;
        for (int i = 0; i < ui.childCount; i++)
        {
            var c = ui.GetChild(i);
            if (c == null || !c.name.Contains("ContinueInput")) continue;
            var trigger = c.Find("Trigger");
            if (trigger != null)
            {
                _continueTriggerGraphic = trigger.GetComponent<Graphic>();
                if (_continueTriggerGraphic == null) _continueTriggerGraphic = trigger.GetComponentInChildren<Graphic>(true);
                break;
            }
        }
    }

    public static void ApplyOnce()
    {
        var titleUI = FindTitleUI();
        if (titleUI == null) { WarnOnce(ref _warnedApplyNoTitle, "ApplyOnce: no TitleUI"); return; }

        var image = titleUI.transform.Find("Image");
        if (image != null)
        {
            var graphic = image.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget) graphic.raycastTarget = false;
        }
        DisableContinueTriggerRaycast();

        if (titleUI.gameObject.activeInHierarchy && Engine.Initialized &&
            Engine.GetService<IUIManager>()?.GetUI<Naninovel.UI.ITitleUI>()?.Visible == true)
            EnsureTitleUIVisibleAndBgmEnabled();
    }

    public static void DisableContinueTriggerRaycast() => SetContinueTriggerRaycast(false);
    public static void EnableContinueTriggerRaycast() => SetContinueTriggerRaycast(true);

    static void SetContinueTriggerRaycast(bool enable)
    {
        var nn = GameObject.Find("Naninovel<Runtime>");
        if (nn == null) return;
        var ui = nn.transform.Find("UI");
        if (ui == null) return;
        for (int i = 0; i < ui.childCount; i++)
        {
            var c = ui.GetChild(i);
            if (c == null || !c.name.Contains("ContinueInput")) continue;
            var trigger = c.Find("Trigger");
            if (trigger != null)
            {
                var g = trigger.GetComponent<Graphic>();
                if (g == null) g = trigger.GetComponentInChildren<Graphic>(true);
                if (g != null) g.raycastTarget = enable;
                break;
            }
        }
    }

    static Transform FindTitleUI()
    {
        var t = GameObject.Find("TitleUI")?.transform;
        if (t != null) return t;
        var nn = GameObject.Find("Naninovel<Runtime>");
        if (nn != null)
        {
            var ui = nn.transform.Find("UI/ModalUI/TitleUI");
            if (ui != null) return ui;
        }
        return null;
    }

    static void WarnOnce(ref bool warned, string message)
    {
        if (!EmitWarnings || warned) return;
        warned = true;
    }
}
