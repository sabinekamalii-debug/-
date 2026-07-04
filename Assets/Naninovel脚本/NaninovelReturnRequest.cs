using System;

public static class NaninovelReturnRequest
{
    static string _scriptName;
    static string _labelName;
    static bool _isPlayingReturnScript;

    public static bool HasRequest => !string.IsNullOrEmpty(_scriptName);
    public static bool IsPlayingReturnScript => _isPlayingReturnScript;

    public static void Set(string scriptName, string labelName = "")
    {
        _scriptName = scriptName ?? "";
        _labelName = labelName ?? "";
    }

    public static void SetPlayingReturnScript() { _isPlayingReturnScript = true; }
    public static void ClearPlayingReturnScript() { _isPlayingReturnScript = false; }

    public static bool TryConsume(out string scriptName, out string labelName)
    {
        if (string.IsNullOrEmpty(_scriptName)) { scriptName = null; labelName = null; return false; }
        scriptName = _scriptName; labelName = _labelName; _scriptName = null; _labelName = null; return true;
    }

    public static void Clear() { _scriptName = null; _labelName = null; _isPlayingReturnScript = false; }
}
