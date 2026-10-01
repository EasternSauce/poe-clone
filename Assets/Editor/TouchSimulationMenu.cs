using UnityEditor;
using UnityEngine.InputSystem.EnhancedTouch;
using PoeClone.Inventory;

/// <summary>
/// PoeClone > Simulate Touch Controls: plays the phone layout in the editor's Game view, with the
/// mouse acting as a finger (the Input System's touch simulation), so the touch controls can be
/// tried without a Web build. Remembered between sessions; applies on entering Play mode, or
/// immediately when toggled during play.
/// </summary>
[InitializeOnLoad]
internal static class TouchSimulationMenu
{
    private const string MenuPath = "PoeClone/Simulate Touch Controls";
    private const string PrefKey = "PoeClone.SimulateTouch";

    static TouchSimulationMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem(MenuPath, priority = 30)]
    private static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(PrefKey, false);
        EditorPrefs.SetBool(PrefKey, on);
        if (EditorApplication.isPlaying)
            Apply(on);
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, false));
        return true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(PrefKey, false))
            Apply(true);
    }

    private static void Apply(bool on)
    {
        TouchMode.SetForced(on);
        if (on)
            TouchSimulation.Enable();
        else
            TouchSimulation.Disable();
    }
}
