using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Global
{
    /// <summary>
    /// 确保全局始终只有一个 AudioListener，从而消除 Unity 运行时警告
    /// "There are 2 audio listeners in the scene"。
    ///
    /// 通过 [RuntimeInitializeOnLoadMethod] 自动创建常驻实例，无需手动挂载到任何场景/prefab
    /// （原脚本因 0 引用从未被挂载，所以一直没生效）。
    ///
    /// 关键冲突背景：Naninovel 在没有 AudioListener 的早期场景会自建一个 AudioListener 并
    /// DontDestroyOnLoad 常驻；之后加载带主摄像机 AudioListener 的战斗场景时二者叠加成 2 个。
    /// 本脚本保留主摄像机上的 AudioListener，删除其余（含 Naninovel 常驻自建的那个）。
    /// 删除后 Naninovel 的 listenerCache 指向已销毁对象，其内部 `if (obj)` 对销毁对象返回 false，
    /// 下次访问会重新 FindFirstObjectByType 找到主摄像机那个并缓存，不再新建 —— 冲突闭环解除。
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class AudioListenerEnforcer : MonoBehaviour
    {
        private static AudioListenerEnforcer _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null) return;
            new GameObject("[AudioListenerEnforcer]").AddComponent<AudioListenerEnforcer>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSingle();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureSingle();

        private void Update()
        {
            // 前 5 秒持续检查，捕获延迟初始化的 AudioListener（如 Naninovel）
            if (Time.frameCount <= 300) EnsureSingle();
            else enabled = false;
        }

        private static void EnsureSingle()
        {
            var listeners = Object.FindObjectsOfType<AudioListener>(true);

            // 保留策略：优先保留主摄像机上的；没有则保留第一个；都没有就自建一个兜底
            AudioListener keep = (Camera.main != null) ? Camera.main.GetComponent<AudioListener>() : null;
            if (keep == null && listeners.Length > 0) keep = listeners[0];
            if (keep == null) keep = _instance.gameObject.AddComponent<AudioListener>();

            // 删掉其余所有（运行时必须用 Destroy，不能用 DestroyImmediate）
            foreach (var l in listeners)
                if (!ReferenceEquals(l, keep)) Destroy(l);
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
