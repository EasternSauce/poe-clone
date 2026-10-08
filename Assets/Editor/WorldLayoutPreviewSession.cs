using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>A disposable launch scene, separate from the user's open scenes and the game build.</summary>
    internal static class WorldLayoutPreviewSession
    {
        private const string ScenePath = "Assets/Editor/WorldLayoutPreview.unity";
        private const string ActiveKey = "PoeClone.WorldLayoutPreview.Active";
        private const string PreviousSceneKey = "PoeClone.WorldLayoutPreview.PreviousStartScene";

        [InitializeOnLoadMethod]
        private static void InstallCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static bool Prepare()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.LogError("World layout preview launch scene already exists: " + ScenePath);
                return false;
            }
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Game.unity", ScenePath))
            {
                Debug.LogError("Could not create the temporary rendered world preview scene.");
                return false;
            }
            SessionState.SetString(PreviousSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            InstallCleanup();
            return true;
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
