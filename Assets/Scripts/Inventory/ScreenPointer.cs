using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PoeClone.Inventory
{
    /// <summary>Mouse coordinates in the current Unity viewport, including browser CSS resizing.</summary>
    public static class ScreenPointer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int ScreenPointer_Read(out float x, out float y);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void TrackBrowserMouse() => ScreenPointer_Read(out _, out _);
#endif

        public static Vector2 Position
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Normalize against live CSS bounds, then use the engine's current render size.
                // Browser DPI caps can change independently of Input System's mouse scale.
                if (ScreenPointer_Read(out float x, out float y) != 0)
                    return new Vector2(x * Screen.width, y * Screen.height);
#endif
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            }
        }
    }
}
