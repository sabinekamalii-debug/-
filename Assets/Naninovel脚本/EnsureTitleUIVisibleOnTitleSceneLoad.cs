using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Naninovel;
using Naninovel.UI;

public class EnsureTitleUIVisibleOnTitleSceneLoad : MonoBehaviour
{
    public float firstTryDelay = 0.4f;
    public float secondTryDelay = 2f;

    static EnsureTitleUIVisibleOnTitleSceneLoad _bootstrap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_bootstrap != null) return;
        var existing = Object.FindFirstObjectByType<EnsureTitleUIVisibleOnTitleSceneLoad>();
        if (existing != null) { _bootstrap = existing; return; }
        var go = new GameObject("EnsureTitleUIVisible_Bootstrap");
        Object.DontDestroyOnLoad(go);
        _bootstrap = go.AddComponent<EnsureTitleUIVisibleOnTitleSceneLoad>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == "Title")
            StartCoroutine(EnsureTitleUIVisibleRoutine());
    }

    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Title") return;
        StartCoroutine(EnsureTitleUIVisibleRoutine());
    }

    IEnumerator EnsureTitleUIVisibleRoutine()
    {
        if (NaninovelReturnRequest.HasRequest) yield break;
        yield return new WaitForSecondsRealtime(firstTryDelay);
        if (NaninovelReturnRequest.HasRequest) yield break;

        TryShowTitleUI();
        ForceShowTitleUIGameObject();
        ForceTitleUICanvasVisibleInGameView();

        yield return new WaitForSecondsRealtime(secondTryDelay - firstTryDelay);
        if (NaninovelReturnRequest.HasRequest) yield break;
        if (Engine.Initialized && IsTitleUIVisible()) yield break;

        TryShowTitleUI();
        ForceShowTitleUIGameObject();
        ForceTitleUICanvasVisibleInGameView();
    }

    static void ForceTitleUICanvasVisibleInGameView()
    {
        Transform root = GameObject.Find("TitleUI")?.transform;
        if (root == null)
        {
            var nn = GameObject.Find("Naninovel<Runtime>");
            if (nn != null) root = nn.transform.Find("UI/ModalUI/TitleUI");
        }
        if (root == null) return;
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) return;

        if (Engine.Initialized)
        {
            HideNaninovelUIOnLevelLoad.ReactivateNaninovelCamera();
            HideNaninovelUIOnLevelLoad.BindNaninovelUICanvasesToUICameraPublic();
            var cam = Engine.GetService<ICameraManager>()?.UICamera;
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
            }
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
            (canvas.worldCamera == null || !canvas.worldCamera.isActiveAndEnabled))
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }
    }

    static void ForceShowTitleUIGameObject()
    {
        Transform root = GameObject.Find("TitleUI")?.transform;
        if (root == null)
        {
            var nn = GameObject.Find("Naninovel<Runtime>");
            if (nn != null) root = nn.transform.Find("UI/ModalUI/TitleUI");
        }
        if (root == null) return;
        var go = root.gameObject;
        if (!go.activeSelf) go.SetActive(true);
        var canvas = root.GetComponent<Canvas>();
        if (canvas != null && !canvas.enabled) canvas.enabled = true;
        var cg = root.GetComponent<CanvasGroup>();
        if (cg != null && cg.alpha < 0.01f) cg.alpha = 1f;
    }

    static void TryShowTitleUI()
    {
        if (!Engine.Initialized) return;
        var uiManager = Engine.GetService<IUIManager>();
        if (uiManager == null) return;
        var titleUI = uiManager.GetUI<ITitleUI>();
        if (titleUI == null) return;
        if (!titleUI.Visible)
        {
            titleUI.Show();
            FixTitleUIRaycast.ApplyOnce();
        }
    }

    static bool IsTitleUIVisible()
    {
        if (!Engine.Initialized) return false;
        var ui = Engine.GetService<IUIManager>()?.GetUI<ITitleUI>();
        return ui != null && ui.Visible;
    }
}
