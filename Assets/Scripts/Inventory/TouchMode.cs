using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PoeClone.Inventory
{
    /// <summary>
    /// Whether this session is played by touch (phone/tablet browser), and the small bits of
    /// shared state the touch layout needs. Decided once at start-up from the platform and the
    /// browser's primary pointer, so a touch laptop with a mouse stays on the desktop controls;
    /// <see cref="SetForced"/> turns it on afterwards (?touch=1, or the editor's touch simulation),
    /// which is why layouts listen to <see cref="Changed"/> instead of reading it once.
    ///
    /// Lives in the Inventory assembly (with <see cref="UiKit"/>) so both the inventory screens and
    /// the gameplay scripts in Assembly-CSharp can read it.
    /// </summary>
    public static class TouchMode
    {
        /// <summary>Canvas height, in reference units, every touch-layout canvas is scaled to.</summary>
        public const float ReferenceHeight = 660f;

        private static bool detected;
        private static bool forced;
        private static readonly List<RectTransform> blockers = new List<RectTransform>();

        public static bool Active => detected || forced;

        /// <summary>Fires when <see cref="Active"/> flips.</summary>
        public static event Action Changed;

        /// <summary>
        /// Scale for OnGUI drawing (HUD, enemy bars), which works in raw screen pixels. Desktop keeps
        /// its 1:1 look; on a phone, raw pixels are tiny, so it scales with the screen height.
        /// </summary>
        public static float GuiScale => Active ? Mathf.Max(1f, Screen.height / 540f) : 1f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int TouchBridge_IsCoarsePointer();
#endif

        // SubsystemRegistration rather than a static initializer: domain reload is off in this
        // project, so statics would otherwise carry over from the previous play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            forced = false;
            Changed = null;
            blockers.Clear();
            detected = UnityEngine.Device.Application.isMobilePlatform || IsCoarsePointer();
        }

        // iPads report a desktop user agent, so the platform check alone misses them; the
        // browser's primary pointer being a finger catches those.
        private static bool IsCoarsePointer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TouchBridge_IsCoarsePointer() != 0;
#else
            return false;
#endif
        }

        public static void SetForced(bool on)
        {
            bool wasActive = Active;
            forced = on;
            if (Active != wasActive)
                Changed?.Invoke();
        }

        /// <summary>
        /// Registers on-screen touch controls, so screens that read raw touches (the inventory)
        /// can ignore a tap that was really meant for a button sitting on top of them.
        /// </summary>
        public static void AddBlocker(RectTransform rect)
        {
            if (rect != null && !blockers.Contains(rect))
                blockers.Add(rect);
        }

        public static bool IsOverBlocker(Vector2 screenPos)
        {
            foreach (RectTransform rect in blockers)
            {
                if (rect != null && rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null))
                    return true;
            }
            return false;
        }
    }
}
