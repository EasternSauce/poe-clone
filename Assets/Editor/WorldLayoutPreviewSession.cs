using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.EditorTools
{
    /// <summary>A disposable launch scene, separate from the user's open scenes and the game build.</summary>
    internal static class WorldLayoutPreviewSession
    {
        private const string ScenePath = "Assets/Editor/WorldLayoutPreview.unity";
        private const string ActiveKey = "PoeClone.WorldLayoutPreview.Active";
        private const string PreviousSceneKey = "PoeClone.WorldLayoutPreview.PreviousStartScene";
        private static GameObject loadingCover;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CoverStartup()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            // Cover the very first frame, including startup menus, until the preview is configured.
            loadingCover = new GameObject("World Layout Preview Loading", typeof(Canvas), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(loadingCover);
            var canvas = loadingCover.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var shade = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(loadingCover.transform, false);
            var image = shade.GetComponent<Image>();
            image.color = Color.black;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // Unity's Game view needs a rendering camera even before the game camera is enabled.
            var cameraObject = new GameObject("Layout Viewer Loading Camera", typeof(Camera));
            cameraObject.transform.SetParent(loadingCover.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = 10000;

            var label = new GameObject("Loading Message", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(loadingCover.transform, false);
            var text = label.GetComponent<Text>();
            text.text = "Loading layout viewer";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }

        public static void Reveal()
        {
            if (loadingCover == null) return;
            loadingCover.SetActive(false);
            Object.Destroy(loadingCover);
            loadingCover = null;
        }

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
