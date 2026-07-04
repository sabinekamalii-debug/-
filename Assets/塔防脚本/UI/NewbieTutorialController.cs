using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// 新手教程：进入场景后显示头像+文本框，文字逐字出现，点击屏幕播下一句，全部播完后自动隐藏。
/// 教程未关闭时全局暂停且不可点击其他内容，只有点击推进教程；全部播完后恢复。
/// </summary>
public class NewbieTutorialController : MonoBehaviour
{
    /// <summary> 当前是否有新手教程正在显示（此时应保持 Time.timeScale=0，供 GameSpeedBoost 等不覆盖）。 </summary>
    public static bool IsTutorialActive { get; private set; }

    [Header("教程根物体（包含头像、背景、文本框，全部播完后会 SetActive(false)）")]
    [Tooltip("拖入 Hierarchy 里的「新手教程」根物体")]
    public GameObject tutorialRoot;

    [Header("文本框")]
    [Tooltip("显示对话内容的 TMP_Text，一般在「新手教程 (真)`下")]
    public TMP_Text dialogueText;

    [Header("对话内容（按顺序播放，可多行）")]
    [TextArea(2, 6)]
    public string[] lines = new string[] { "欢迎来到关卡，点击屏幕继续。" };

    [Header("打字速度")]
    [Tooltip("每显示一个字符的间隔（秒）")]
    public float typewriterInterval = 0.05f;

    [Header("Ctrl 加速")]
    [Tooltip("按住 Ctrl 时的打字加速倍率")]
    public float ctrlSpeedMultiplier = 1.8f;
    [Tooltip("按住 Ctrl 时，当前行打完后自动跳到下一行的等待时间（秒）")]
    public float autoAdvanceDelay = 0.3f;

    [Header("对话框存在时失活、关闭后重新激活")]
    [Tooltip("拖入需要在教程进行时暂时失活、教程结束后再激活的物体（可多个）")]
    public GameObject[] deactivateWhileTutorialActive;

    private int _currentIndex;
    private bool _typewriterRunning;
    private Coroutine _typewriterCoroutine;
    private GameObject _blocker;
    private bool _ctrlHeld;
    private float _autoAdvanceTimer;

    private void Start()
    {
        if (tutorialRoot == null)
        {
            var found = GameObject.Find("新手教程");
            if (found != null) tutorialRoot = found;
            else tutorialRoot = gameObject;
        }
        if (dialogueText == null && tutorialRoot != null)
            dialogueText = tutorialRoot.GetComponentInChildren<TMP_Text>(true);

        if (dialogueText == null || lines == null || lines.Length == 0)
        {
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            return;
        }

        _currentIndex = 0;
        if (tutorialRoot != null) tutorialRoot.SetActive(true);
        SetDeactivateListActive(false);
        IsTutorialActive = true;
        EnsureBlockerAndPause();
        PlayCurrentLine();
    }

    private void OnDisable()
    {
        IsTutorialActive = false;
        SetDeactivateListActive(true);
        if (_blocker != null) _blocker.SetActive(false);
        Time.timeScale = 1f;
    }

    private void SetDeactivateListActive(bool active)
    {
        if (deactivateWhileTutorialActive == null) return;
        for (int i = 0; i < deactivateWhileTutorialActive.Length; i++)
        {
            if (deactivateWhileTutorialActive[i] != null)
                deactivateWhileTutorialActive[i].SetActive(active);
        }
    }

    /// <summary> 教程显示时：暂停游戏并铺满屏透明挡板，使其他一切不可点击。 </summary>
    private void EnsureBlockerAndPause()
    {
        Time.timeScale = 0f;
        if (tutorialRoot == null) return;
        if (_blocker != null)
        {
            _blocker.SetActive(true);
            return;
        }
        Transform parent = tutorialRoot.transform.parent;
        Canvas canvas = tutorialRoot.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        parent = canvas.transform;

        GameObject go = new GameObject("NewbieTutorial_Blocker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(tutorialRoot.transform.GetSiblingIndex());
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.01f);
        img.raycastTarget = true;
        _blocker = go;
    }

    private void ResumeAndHideBlocker()
    {
        IsTutorialActive = false;
        SetDeactivateListActive(true);
        Time.timeScale = 1f;
        if (_blocker != null) _blocker.SetActive(false);
    }

    private void Update()
    {
        if (tutorialRoot == null || !tutorialRoot.activeSelf) return;
        Time.timeScale = 0f;

        _ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        // Ctrl 按住时：自动快进
        if (_ctrlHeld)
        {
            if (_typewriterRunning)
            {
                // 正在打字 → 直接跳完当前行的全部文字
                SkipTypewriter();
                _autoAdvanceTimer = 0f;
                return;
            }

            // 当前行已显示完毕 → 等一小段时间后自动跳下一行
            _autoAdvanceTimer += Time.unscaledDeltaTime;
            if (_autoAdvanceTimer >= autoAdvanceDelay)
            {
                _autoAdvanceTimer = 0f;
                AdvanceToNextLine();
            }
            return;
        }

        // 松开 Ctrl 时重置计时器
        _autoAdvanceTimer = 0f;

        // 普通鼠标点击推进
        if (!Input.GetMouseButtonDown(0)) return;

        if (_typewriterRunning)
        {
            SkipTypewriter();
            return;
        }

        AdvanceToNextLine();
    }

    private void AdvanceToNextLine()
    {
        _currentIndex++;
        if (_currentIndex >= lines.Length)
        {
            ResumeAndHideBlocker();
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            return;
        }

        PlayCurrentLine();
    }

    private void PlayCurrentLine()
    {
        if (dialogueText == null || _currentIndex < 0 || _currentIndex >= lines.Length) return;
        string line = lines[_currentIndex] ?? "";
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(line));
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        _typewriterRunning = true;
        dialogueText.text = "";
        for (int i = 0; i <= fullText.Length; i++)
        {
            dialogueText.text = fullText.Substring(0, i);
            if (i < fullText.Length && typewriterInterval > 0f)
            {
                float interval = _ctrlHeld ? typewriterInterval / ctrlSpeedMultiplier : typewriterInterval;
                yield return new WaitForSecondsRealtime(interval);
            }
        }
        _typewriterRunning = false;
        _typewriterCoroutine = null;
    }

    private void SkipTypewriter()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }
        _typewriterRunning = false;
        if (dialogueText != null && _currentIndex >= 0 && _currentIndex < lines.Length)
            dialogueText.text = lines[_currentIndex] ?? "";
    }
}
