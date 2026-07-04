using UnityEngine;
using UnityEngine.SceneManagement;
using Naninovel;
using Naninovel.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// 挂在"塔防关卡场景"（如 level A）里任意一个常驻 GameObject 上。
/// 进入关卡后会让 Naninovel 的 UI 和主摄像机失活；离开关卡时由 LevelEndMenu 调用 Reactivate 再激活。
/// </summary>
public class HideNaninovelUIOnLevelLoad : MonoBehaviour
{
    [Tooltip("进入关卡后延迟多少秒再失活 Naninovel UI/相机，避免尚未创建完就执行")]
    public float delaySeconds = 0.25f;
    [Tooltip("进入关卡时是否同时失活 Naninovel 主摄像机（关卡用场景自己的 Main Camera）")]
    public bool deactivateNaninovelCamera = true;
    [Tooltip("进入关卡时是否顺便停止 Naninovel 剧情里正在播放的 BGM。")]
    [SerializeField] private bool stopNaninovelBgmOnEnter = false;
    [Tooltip("可选。填 BGM 名时，进关后会尝试用 Naninovel 播放；若 Engine 不可用则用下面的 Bgm Clip 直接播。")]
    [SerializeField] private string bgmToPlayOnEnter = "";
    [SerializeField] private float bgmVolumeOnEnter = 0.4f;
    [SerializeField] private bool bgmLoopOnEnter = true;
    [Tooltip("进关卡时若 Engine 不可用，用此 Clip 直接播放。")]
    [SerializeField] private AudioClip bgmClipOnEnter;

    void Start()
    {
        if (Engine.Initialized && deactivateNaninovelCamera)
            SetNaninovelCameraActive(false);
        FixTitleUIRaycast.DisableContinueTriggerRaycast();
        StartCoroutine(DeactivateWhenReady());
    }

    IEnumerator DeactivateWhenReady()
    {
        const float engineWaitMax = 0.8f;
        float waited = 0f;
        while (!Engine.Initialized && waited < engineWaitMax)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(delaySeconds);

        if (Engine.Initialized)
        {
            if (stopNaninovelBgmOnEnter)
            {
                var audioManager = Engine.GetService<IAudioManager>();
                if (audioManager != null)
                    audioManager.StopAllBgm(0f).Forget();
            }
            else if (bgmClipOnEnter == null && !string.IsNullOrEmpty(bgmToPlayOnEnter))
            {
                var audioManager = Engine.GetService<IAudioManager>();
                if (audioManager != null)
                    audioManager.PlayBgm(bgmToPlayOnEnter, bgmVolumeOnEnter, 0f, bgmLoopOnEnter).Forget();
            }
            SetNaninovelUIActive(false);
            HideAllNaninovelActors();
            var scriptPlayer = Engine.GetService<IScriptPlayer>();
            if (scriptPlayer != null)
                scriptPlayer.Stop();
        }

        if (!stopNaninovelBgmOnEnter && bgmClipOnEnter != null)
        {
            var src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.clip = bgmClipOnEnter;
            src.volume = bgmVolumeOnEnter;
            src.loop = bgmLoopOnEnter;
            src.playOnAwake = false;
            src.Play();
        }
    }

    static void HideAllNaninovelActors()
    {
        if (Engine.Services == null) return;
        foreach (var manager in Engine.Services.OfType<IActorManager>())
        {
            foreach (var actor in manager.Actors)
            {
                if (actor != null && actor.Visible)
                    actor.Visible = false;
            }
        }
    }

    static Transform FindNaninovelUIRoot()
    {
        var naninovel = GameObject.Find("Naninovel<Runtime>");
        if (naninovel != null)
        {
            var ui = naninovel.transform.Find("UI");
            if (ui != null) return ui;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                if (!root.name.Contains("Naninovel")) continue;
                var ui = root.transform.Find("UI");
                if (ui != null) return ui;
            }
        }
        return null;
    }

    public static void SetNaninovelUIActive(bool active)
    {
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot != null)
            uiRoot.gameObject.SetActive(active);
    }

    public static void ReactivateNaninovelUI()
    {
        SetNaninovelUIActive(true);
        ReactivateDialoguePrinter();
        EnsureTextPrinterGameObjectActive();
        BindNaninovelUICanvasesToUICamera();
        ForceDialogueCanvasToCamera();
        ForceAllNaninovelCanvasesToCameraMode();
        FixDialogueRectInViewport();
        FixTitleUIRaycast.ApplyOnce();
    }

    static void ReactivateDialoguePrinter()
    {
        if (!Engine.Initialized) return;
        var printerManager = Engine.GetService<ITextPrinterManager>();
        if (printerManager == null) return;
        var dialogue = printerManager.GetActor("Dialogue");
        if (dialogue == null) return;
        dialogue.Visible = true;
        if (dialogue is UITextPrinter uiPrinter && uiPrinter.PrinterPanel != null)
            uiPrinter.PrinterPanel.Show();
        if (dialogue is UnityEngine.Component comp && !comp.gameObject.activeSelf)
            comp.gameObject.SetActive(true);
    }

    public static void ForceShowDialoguePanel() => ReactivateDialoguePrinter();

    static void EnsureTextPrinterGameObjectActive()
    {
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        var textPrinter = uiRoot.Find("TextPrinter");
        if (textPrinter != null && !textPrinter.gameObject.activeSelf)
            textPrinter.gameObject.SetActive(true);
    }

    static void BindNaninovelUICanvasesToUICamera()
    {
        if (!Engine.Initialized) return;
        var cameraManager = Engine.GetService<ICameraManager>();
        if (cameraManager?.UICamera == null) return;
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        var uiCam = cameraManager.UICamera;
        foreach (var canvas in uiRoot.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (canvas.worldCamera != uiCam)
                canvas.worldCamera = uiCam;
        }
    }

    public static void BindNaninovelUICanvasesToUICameraPublic()
    {
        BindNaninovelUICanvasesToUICamera();
        ForceDialogueCanvasToCamera();
        ForceAllNaninovelCanvasesToCameraMode();
        FixDialogueRectInViewport();
    }

    public static void ForceDialogueCanvasToCamera()
    {
        if (!Engine.Initialized) return;
        var cameraManager = Engine.GetService<ICameraManager>();
        if (cameraManager?.UICamera == null) return;
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        var uiCam = cameraManager.UICamera;
        var textPrinter = uiRoot.Find("TextPrinter");
        if (textPrinter == null) return;
        var dialogue = textPrinter.Find("Dialogue");
        if (dialogue == null) dialogue = textPrinter.Find("NewDialoguePrinter");
        if (dialogue == null) return;
        var canvas = dialogue.GetComponent<Canvas>();
        if (canvas == null) return;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCam;
        if (canvas.sortingOrder < 500) canvas.sortingOrder = 500;
    }

    public static void FixDialogueRectInViewport()
    {
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        var textPrinter = uiRoot.Find("TextPrinter");
        if (textPrinter == null) return;
        var dialogue = textPrinter.Find("Dialogue");
        if (dialogue == null) dialogue = textPrinter.Find("NewDialoguePrinter");
        if (dialogue == null) return;
        var rt = dialogue.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

    public static void ForceAllNaninovelCanvasesToCameraMode()
    {
        if (!Engine.Initialized) return;
        var cameraManager = Engine.GetService<ICameraManager>();
        if (cameraManager?.UICamera == null) return;
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        var uiCam = cameraManager.UICamera;
        const int modalSortOrder = 500;
        const int titleSortOrder = 100;
        foreach (var canvas in uiRoot.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if (canvas.worldCamera != uiCam)
                canvas.worldCamera = uiCam;
            bool isTitleUI = canvas.gameObject.name.Contains("TitleUI");
            int order = isTitleUI ? titleSortOrder : modalSortOrder;
            if (canvas.sortingOrder != order && (isTitleUI || canvas.sortingOrder < modalSortOrder))
                canvas.sortingOrder = order;
        }
    }

    public static void SetNaninovelCameraActive(bool active)
    {
        if (!Engine.Initialized) return;
        var cameraManager = Engine.GetService<ICameraManager>();
        if (cameraManager == null) return;
        if (cameraManager.Camera != null && cameraManager.Camera.gameObject != null)
            cameraManager.Camera.gameObject.SetActive(active);
        if (cameraManager.UICamera != null && cameraManager.UICamera.gameObject != null)
            cameraManager.UICamera.gameObject.SetActive(active);
    }

    public static void ReactivateNaninovelCamera()
    {
        SetNaninovelCameraActive(true);
        ReattachNaninovelCamerasToURPStack();
        EnsureNaninovelUICameraOnTop();
        EnsureNaninovelCanvasesVisibleInGameView();
    }

    public static void EnsureNaninovelURPAndDialogueVisible()
    {
        if (!Engine.Initialized) return;
        SetNaninovelCameraActive(true);
        ReattachNaninovelCamerasToURPStack();
        EnsureNaninovelUICameraOnTop();
        BindNaninovelUICanvasesToUICamera();
        ForceDialogueCanvasToCamera();
        ForceAllNaninovelCanvasesToCameraMode();
        FixDialogueRectInViewport();
    }

    static void ReattachNaninovelCamerasToURPStack()
    {
        if (!Engine.Initialized) return;
        var cameraManager = Engine.GetService<ICameraManager>();
        if (cameraManager?.Camera == null || !cameraManager.Camera.enabled) return;

        var mainCam = cameraManager.Camera;
        var uiCam = cameraManager.UICamera;

        try
        {
            Type extensionsType = Type.GetType("UnityEngine.Rendering.Universal.CameraExtensions, Unity.RenderPipelines.Universal.Runtime");
            if (extensionsType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    extensionsType = asm.GetType("UnityEngine.Rendering.Universal.CameraExtensions");
                    if (extensionsType != null) break;
                }
            }
            if (extensionsType == null) return;

            var getDataMethod = extensionsType.GetMethod("GetUniversalAdditionalCameraData", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Camera) }, null);
            if (getDataMethod == null) return;

            object baseData = null;
            var activeScene = SceneManager.GetActiveScene();
            Camera[] all = new Camera[Camera.allCamerasCount];
            Camera.GetAllCameras(all);
            for (int pass = 0; pass < 2 && baseData == null; pass++)
            {
                foreach (var cam in all)
                {
                    if (cam == null || !cam.isActiveAndEnabled || cam.gameObject == null || !cam.gameObject.activeInHierarchy) continue;
                    if (cam == mainCam || cam == uiCam) continue;
                    if (cam.cameraType != CameraType.Game) continue;
                    if (pass == 0 && cam.gameObject.scene != activeScene) continue;
                    var data = getDataMethod.Invoke(null, new object[] { cam });
                    if (data == null) continue;
                    var rt = data.GetType().GetProperty("renderType")?.GetValue(data);
                    if (rt == null) continue;
                    if (rt is Enum e && Convert.ToInt32(e) != 0) continue;
                    baseData = data;
                    break;
                }
            }
            if (baseData == null) return;

            var stackProp = baseData.GetType().GetProperty("cameraStack");
            if (stackProp == null) return;
            var stack = stackProp.GetValue(baseData) as System.Collections.IList;
            if (stack == null) return;

            if (!stack.Contains(mainCam)) stack.Add(mainCam);
            if (uiCam != null && !stack.Contains(uiCam)) stack.Add(uiCam);
        }
        catch (Exception) { }
    }

    static void EnsureNaninovelCanvasesVisibleInGameView()
    {
        if (!Engine.Initialized) return;
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        ForceAllNaninovelCanvasesToCameraMode();
        FixDialogueRectInViewport();
    }

    public static void ForceAllNaninovelCanvasesToOverlay()
    {
        var uiRoot = FindNaninovelUIRoot();
        if (uiRoot == null) return;
        if (!uiRoot.gameObject.activeSelf)
            uiRoot.gameObject.SetActive(true);
        ForceAllNaninovelCanvasesToCameraMode();
        FixDialogueRectInViewport();
    }

    static void EnsureNaninovelUICameraOnTop()
    {
        if (!Engine.Initialized) return;
        var cameraManager = Engine.GetService<ICameraManager>();
        if (cameraManager?.UICamera == null) return;
        var uiCam = cameraManager.UICamera;
        float maxDepth = -1000f;
        foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam != null && cam != uiCam && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                if (cam.depth > maxDepth) maxDepth = cam.depth;
            }
        }
        if (uiCam.depth <= maxDepth)
            uiCam.depth = maxDepth + 1f;
    }
}

public class NaninovelUICanvasOverlayEnforcer : MonoBehaviour
{
    float _remaining;
    public void StartEnforce(float seconds) { _remaining = seconds; }
    void LateUpdate()
    {
        if (_remaining <= 0f) return;
        _remaining -= Time.unscaledDeltaTime;
        if (_remaining <= 0f) Destroy(this);
    }
}
