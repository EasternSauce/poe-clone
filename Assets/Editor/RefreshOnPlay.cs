using UnityEditor;

/// <summary>
/// Runs Assets > Refresh every time Play mode is entered, so files changed outside the editor
/// (scripts, audio, meshes) are imported and compiled before the game starts.
/// </summary>
[InitializeOnLoad]
internal static class RefreshOnPlay
{
    static RefreshOnPlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
            AssetDatabase.Refresh();
    }
}
