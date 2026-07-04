using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 全局视频加载器（自动引导版）：
/// - 启动时自动创建全屏视频加载层（无需手动摆 UI）。
/// - 场景切换时通过 LoadScene 接口显示/隐藏循环视频。
/// - 默认从 StreamingAssets/视频/加载中.mp4 播放。
/// </summary>
[DefaultExecutionOrder(-10000)]
public class VideoSceneLoader : MonoBehaviour
{
    public static VideoSceneLoader Instance { get; private set; }

    private const bool ForceDisableLogs = true;

    [Header("视频文件（StreamingAssets 相对路径）")]
    [SerializeField] private string startupVideoRelativePath = "视频/加载中.mp4";
    [SerializeField] private VideoClip startupVideoClip;

    [Header("最短显示时长（秒）")]
    [SerializeField] private float startupMinShowSeconds = 0.8f;
    [SerializeField] private float transitionMinShowSeconds = 0.35f;
    [SerializeField] private bool verboseLogs = false;
    [SerializeField] private float prepareTimeoutSeconds = 3f;

    [Header("可选：手动指定（不填则运行时自动创建）")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private VideoPlayer videoPlayer;

    private RawImage loadingImage;
    private RenderTexture renderTexture;
    private Image fallbackBackground;
    private Text fallbackText;
    private bool isLoading;
    private bool startupLoadingActive;
    private bool videoReady;
    private bool videoFailed;
    private Coroutine prepareWatchdog;
    private static bool startupRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (startupRequested) return;
        startupRequested = true;

        var go = new GameObject("[VideoSceneLoader]");
        DontDestroyOnLoad(go);
        var loader = go.AddComponent<VideoSceneLoader>();
        Instance = loader;

        loader.StartCoroutine(loader.StartupRoutine());
    }

    /// <summary>
    /// 确保单例存在并返回实例
    /// </summary>
    private static VideoSceneLoader EnsureInstance()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("[VideoSceneLoader]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<VideoSceneLoader>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (ForceDisableLogs)
            verboseLogs = false;

        try
        {
            EnsureRuntimeUI();
            ConfigureVideoSource();
            if (!startupRequested)
                SetLoadingVisible(false);
            Log("Awake 完成，加载器已初始化。");

            if (startupRequested)
            {
                startupRequested = false;
                BeginStartupLoading();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VideoSceneLoader] 初始化失败: {e.Message}\n{e.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
            renderTexture.Release();
    }

    public static void LoadScene(string sceneName)
    {
        EnsureInstance().StartCoroutine(EnsureInstance().LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (isLoading) yield break;
        isLoading = true;
        RogueRuntimeState.SaveRunStateIfNeeded();
        Log($"开始切场景加载：{sceneName}");

        SetLoadingVisible(true);
        float startTime = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            SetLoadingVisible(false);
            isLoading = false;
            yield break;
        }

        while (!op.isDone)
            yield return null;

        while (Time.unscaledTime - startTime < transitionMinShowSeconds)
            yield return null;

        if (!startupLoadingActive)
            SetLoadingVisible(false);
        isLoading = false;
        Log($"场景加载完成：{sceneName}");

        if (sceneName == "Title")
        {
            yield return new WaitForSecondsRealtime(0.05f);
            SetLoadingVisible(false);
        }
    }

    private void BeginStartupLoading()
    {
        if (startupLoadingActive) return;
        startupLoadingActive = true;
        Log("开始启动阶段加载动画。");
        StartCoroutine(StartupRoutine());
    }

    private IEnumerator StartupRoutine()
    {
        SetLoadingVisible(true);
        float start = Time.unscaledTime;

        yield return null; // 等第一帧，确保首场景完成初始化

        while (Time.unscaledTime - start < startupMinShowSeconds)
            yield return null;

        // 启动阶段不要过早隐藏：至少等待到视频准备完成或确认失败（超时/解码错误）。
        float waitStart = Time.unscaledTime;
        while (!videoReady && !videoFailed && Time.unscaledTime - waitStart < prepareTimeoutSeconds)
            yield return null;

        startupLoadingActive = false;
        if (!isLoading)
            SetLoadingVisible(false);
        Log("启动阶段加载动画结束。");
    }

    private void EnsureRuntimeUI()
    {
        if (loadingRoot == null)
        {
            loadingRoot = new GameObject("LoadingVideoCanvas");
            loadingRoot.transform.SetParent(transform, false);

            var canvas = loadingRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            loadingRoot.AddComponent<CanvasScaler>();
            loadingRoot.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("LoadingVideoImage");
            imageGo.transform.SetParent(loadingRoot.transform, false);
            loadingImage = imageGo.AddComponent<RawImage>();
            var rt = imageGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            loadingImage.color = Color.white;

            var bgGo = new GameObject("LoadingFallbackBackground");
            bgGo.transform.SetParent(loadingRoot.transform, false);
            fallbackBackground = bgGo.AddComponent<Image>();
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            fallbackBackground.color = Color.black;

            var textGo = new GameObject("LoadingFallbackText");
            textGo.transform.SetParent(bgGo.transform, false);
            fallbackText = textGo.AddComponent<Text>();
            var txtRt = textGo.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.5f, 0.1f);
            txtRt.anchorMax = new Vector2(0.5f, 0.1f);
            txtRt.sizeDelta = new Vector2(600f, 80f);
            txtRt.anchoredPosition = Vector2.zero;
            fallbackText.alignment = TextAnchor.MiddleCenter;
            fallbackText.color = Color.white;
            fallbackText.fontSize = 36;
            fallbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fallbackText.text = "Loading...";
        }
        else if (loadingImage == null)
        {
            loadingImage = loadingRoot.GetComponentInChildren<RawImage>(true);
            if (loadingImage == null)
            {
                var imageGo = new GameObject("LoadingVideoImage");
                imageGo.transform.SetParent(loadingRoot.transform, false);
                loadingImage = imageGo.AddComponent<RawImage>();
                var rt = imageGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        if (videoPlayer == null)
        {
            videoPlayer = loadingRoot.GetComponent<VideoPlayer>();
            if (videoPlayer == null)
                videoPlayer = loadingRoot.AddComponent<VideoPlayer>();
        }

        if (renderTexture == null)
            renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.errorReceived -= HandleVideoError;
        videoPlayer.prepareCompleted -= HandleVideoPrepared;
        videoPlayer.started -= HandleVideoStarted;
        videoPlayer.loopPointReached -= HandleLoopPointReached;
        videoPlayer.errorReceived += HandleVideoError;
        videoPlayer.prepareCompleted += HandleVideoPrepared;
        videoPlayer.started += HandleVideoStarted;
        videoPlayer.loopPointReached += HandleLoopPointReached;

        if (loadingImage != null)
            loadingImage.texture = renderTexture;
    }

    private void ConfigureVideoSource()
    {
        if (videoPlayer == null) return;

        if (prepareWatchdog != null)
        {
            StopCoroutine(prepareWatchdog);
            prepareWatchdog = null;
        }

        // 编辑器下自动抓取项目内常见路径的视频资源，避免自动引导对象无法手动拖引用。
#if UNITY_EDITOR
        if (startupVideoClip == null)
        {
            // 优先使用已转码的新 mp4（_bt709 / _fixed），避免旧视频编码/色彩标记不兼容。
            startupVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/视频/加载中_bt709.mp4");
            if (startupVideoClip == null)
                startupVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/视频/加载中_fixed.mp4");
            if (startupVideoClip == null)
                startupVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/视频/加载中.mp4");
            if (startupVideoClip == null)
                startupVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/video/加载中_bt709.mp4");
            if (startupVideoClip == null)
                startupVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/video/加载中_fixed.mp4");
            if (startupVideoClip == null)
                startupVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/video/加载中.mp4");
        }
#endif

        if (startupVideoClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = startupVideoClip;
            videoReady = false;
            videoFailed = false;
            LogInfo($"使用 VideoClip 资源：{startupVideoClip.name}");
            if (loadingImage != null) loadingImage.enabled = true;
            if (fallbackBackground != null) fallbackBackground.enabled = false;
            if (fallbackText != null) fallbackText.enabled = false;
            return;
        }

        var filePath = System.IO.Path.Combine(Application.streamingAssetsPath, startupVideoRelativePath);
        if (!System.IO.File.Exists(filePath))
        {
            // 编辑器下兼容直接放在 Assets/视频/ 的情况。
            var editorFallback = System.IO.Path.Combine(Application.dataPath, startupVideoRelativePath);
            if (System.IO.File.Exists(editorFallback))
                filePath = editorFallback;
        }

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = new System.Uri(filePath).AbsoluteUri;
        videoReady = false;
        videoFailed = false;
        LogInfo($"使用 URL 视频路径：{videoPlayer.url}");

        bool hasVideo = System.IO.File.Exists(filePath);
        if (!hasVideo)
            LogWarn($"未找到加载视频文件: {filePath}");

        if (loadingImage != null) loadingImage.enabled = hasVideo;
        if (fallbackBackground != null) fallbackBackground.enabled = !hasVideo;
        if (fallbackText != null) fallbackText.enabled = !hasVideo;
    }

    private void SetLoadingVisible(bool visible)
    {
        LogInfo($"SetLoadingVisible({visible})");
        if (loadingRoot != null)
            loadingRoot.SetActive(visible);

        if (videoPlayer == null) return;
        if (visible)
        {
            if (!videoPlayer.isPrepared && !videoReady)
            {
                LogStep("VideoPlayer.Prepare()");
                videoPlayer.Prepare();
                if (prepareWatchdog != null) StopCoroutine(prepareWatchdog);
                prepareWatchdog = StartCoroutine(PrepareWatchdog());
            }
            else
            {
                LogStep("VideoPlayer.Play()");
                videoPlayer.Play();
            }
        }
        else
        {
            LogStep("VideoPlayer.Stop()");
            videoPlayer.Stop();
        }
    }

    private IEnumerator PrepareWatchdog()
    {
        float start = Time.unscaledTime;
        while (!videoReady && Time.unscaledTime - start < prepareTimeoutSeconds)
            yield return null;

        if (!videoReady)
        {
            videoFailed = true;
            LogError($"Prepare 超时（>{prepareTimeoutSeconds:0.##}s），可能是编码不兼容。建议转码 H.264 Baseline/Main + yuv420p + CFR。");
            if (fallbackBackground != null) fallbackBackground.enabled = true;
            if (fallbackText != null) fallbackText.enabled = true;
            if (loadingImage != null) loadingImage.enabled = false;
        }
        prepareWatchdog = null;
    }

    private void HandleVideoPrepared(VideoPlayer vp)
    {
        videoReady = true;
        videoFailed = false;
        LogOk("视频 Prepare 完成。");
        LogInfo($"视频信息: source={vp.source}, width={vp.width}, height={vp.height}, length={vp.length:0.###}, fps={vp.frameRate:0.###}");
        if (loadingRoot != null && loadingRoot.activeInHierarchy)
        {
            LogStep("加载层可见，开始播放视频。");
            vp.Play();
        }
    }

    private void HandleVideoStarted(VideoPlayer vp)
    {
        LogOk("视频开始播放。");
    }

    private void HandleLoopPointReached(VideoPlayer vp)
    {
        LogInfo("视频到达循环点。");
    }

    private void HandleVideoError(VideoPlayer vp, string message)
    {
        videoFailed = true;
        if (fallbackBackground != null) fallbackBackground.enabled = true;
        if (fallbackText != null) fallbackText.enabled = true;
        if (loadingImage != null) loadingImage.enabled = false;
    }

    private void Log(string message) { }
    private void LogInfo(string message) { }
    private void LogStep(string message) { }
    private void LogOk(string message) { }
    private void LogWarn(string message) { }
    private void LogError(string message) { }
}
