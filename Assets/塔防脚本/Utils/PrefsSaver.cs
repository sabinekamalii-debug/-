using UnityEngine;

/// <summary>
/// 合并 PlayerPrefs 写盘：同一帧内多次 Save 请求合并为一次同步写盘，
/// 减少切场景路径中的重复磁盘 I/O。始终在帧末 / 失焦 / 退出时落盘，不会丢数据。
/// </summary>
public class PrefsSaver : MonoBehaviour
{
    private static PrefsSaver _instance;
    private static bool _pending;
    private static readonly object _lock = new object();

    public static void Save()
    {
        lock (_lock)
        {
            _pending = true;
        }
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("[PrefsSaver]");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PrefsSaver>();
    }

    private void Awake()
    {
        Application.quitting += Flush;
        Application.focusChanged += OnFocusChanged;
    }

    private void OnDestroy()
    {
        Application.quitting -= Flush;
        Application.focusChanged -= OnFocusChanged;
        Flush();
    }

    private void OnFocusChanged(bool hasFocus)
    {
        // 失去焦点（切后台）时立即落盘，防止意外退出丢数据
        if (!hasFocus) Flush();
    }

    private void Update()
    {
        // 帧末统一落盘：同一帧内的多次 Save 请求只写一次
        bool pending;
        lock (_lock)
        {
            pending = _pending;
            _pending = false;
        }
        if (pending) PlayerPrefs.Save();
    }

    private static void Flush()
    {
        bool pending;
        lock (_lock)
        {
            pending = _pending;
            _pending = false;
        }
        if (pending) PlayerPrefs.Save();
    }
}
