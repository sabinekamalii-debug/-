using System;
using UnityEngine;

public static class NaninovelReturnRequest
{
    static string _scriptName;
    static string _labelName;
    static string _returnScene;
    static bool _isPlayingReturnScript;

    // Domain Reload 禁用时，每次 Enter Play Mode 手动清空全部请求状态。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlaymodeEnter()
    {
        _scriptName = null;
        _labelName = null;
        _returnScene = null;
        _isPlayingReturnScript = false;
    }

    public static bool HasRequest => !string.IsNullOrEmpty(_scriptName);
    public static bool IsPlayingReturnScript => _isPlayingReturnScript;
    public static string ReturnScene => _returnScene;

    /// <summary>
    /// 设置待播放的剧本，可选返回场景（播完后自动切回该场景）
    /// </summary>
    public static void Set(string scriptName, string labelName = "", string returnScene = "")
    {
        _scriptName = scriptName ?? "";
        _labelName = labelName ?? "";
        _returnScene = returnScene ?? "";
    }

    public static void SetPlayingReturnScript() { _isPlayingReturnScript = true; }
    public static void ClearPlayingReturnScript() { _isPlayingReturnScript = false; }
    public static void ClearReturnScene() { _returnScene = null; }

    public static bool TryConsume(out string scriptName, out string labelName)
    {
        if (string.IsNullOrEmpty(_scriptName)) { scriptName = null; labelName = null; return false; }
        scriptName = _scriptName; labelName = _labelName; _scriptName = null; _labelName = null; return true;
    }

    public static void Clear() { _scriptName = null; _labelName = null; _returnScene = null; _isPlayingReturnScript = false; }
}
