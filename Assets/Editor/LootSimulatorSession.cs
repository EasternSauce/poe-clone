using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace PoeClone.EditorTools
{
    /// <summary>Editor-only, disposable Play configuration with an isolated UI scene.</summary>
    internal static class LootSimulatorSession
    {
        private const string ScenePath = "Assets/Editor/LootSimulatorPreview.unity";
        private const string ActiveKey = "PoeClone.LootSimulator.Active";
        private const string PreviousSceneKey = "PoeClone.LootSimulator.PreviousStartScene";

        [InitializeOnLoadMethod]
        private static void InstallCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static string Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play before launching Loot Simulator.";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return "Temporary loot launch scene already exists: " + ScenePath;
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool saved = EditorSceneManager.SaveScene(preview, ScenePath);
            EditorSceneManager.CloseScene(preview, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (!saved) return "Could not create loot simulator launch scene.";
            SessionState.SetString(PreviousSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            InstallCleanup();
            EditorApplication.isPlaying = true;
            return "Starting standalone Loot Simulator. Stop Play or close the simulator to restore the previous launch configuration.";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            var camera = new GameObject("Loot Simulator Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 0;
            new GameObject("Loot Simulator EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            PoeClone.Inventory.TouchMode.SetForced(false);
            new GameObject("Loot Simulator").AddComponent<LootSimulatorUI>().Open();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousSceneKey, ""));
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseString(PreviousSceneKey);
            AssetDatabase.DeleteAsset(ScenePath);
        }
    }
}
