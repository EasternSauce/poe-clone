using UnityEngine;

namespace PoeClone.UI
{
    // Keep frame measurements available to editor diagnostics without an in-game debug UI.
    [DefaultExecutionOrder(-10000)]
    public sealed class PerformanceDebugUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("Performance diagnostics");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceDebugUI>();
        }

        private void Update() => PerformanceDiagnostics.NextFrame();
    }
}
