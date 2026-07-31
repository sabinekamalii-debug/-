using UnityEngine;
using System;

namespace TowerDefense.Global
{
    /// <summary>
    /// 运行时日志过滤器 - 可以完全禁用运行时的控制台输出
    /// </summary>
    [DefaultExecutionOrder(-32001)]
    public class RuntimeLoggerFilter : MonoBehaviour
    {
        private static RuntimeLoggerFilter _instance;
        private static ILogHandler _originalLogHandler;

        [Header("运行时日志配置")]
        [Tooltip("是否启用运行时日志（Release 构建议关闭）")]
        [SerializeField] private bool enableRuntimeLogs = true;

        [Tooltip("只显示错误和警告（忽略 Info 级别）")]
        [SerializeField] private bool onlyErrorsAndWarnings = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnRuntimeInitialize()
        {
            // 不替换日志处理器，让 Debug.Log 正常工作
            // 原始代码会吞掉所有 Log 级别日志，导致控制台看不到任何 Debug.Log
        }

        private void Awake()
        {
            if (_instance != null) return;
            _instance = this;
        }

        private class FilteredLogHandler : ILogHandler
        {
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                var settings = GetSettings();
                // 没有实例时直接放行所有日志
                if (settings == null)
                {
                    _originalLogHandler?.LogFormat(logType, context, format, args);
                    return;
                }

                if (!settings.enableRuntimeLogs) return;
                if (settings.onlyErrorsAndWarnings && logType != LogType.Error && logType != LogType.Warning && logType != LogType.Exception) return;

                _originalLogHandler?.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                var settings = GetSettings();
                if (settings == null || !settings.enableRuntimeLogs) return;

                _originalLogHandler?.LogException(exception, context);
            }

            private static RuntimeLoggerFilter GetSettings()
            {
                if (_instance != null) return _instance;
                _instance = UnityEngine.Object.FindObjectOfType<RuntimeLoggerFilter>();
                return _instance;
            }
        }
    }
}