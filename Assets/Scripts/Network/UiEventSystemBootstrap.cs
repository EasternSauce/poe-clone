using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// The scene had no interactive uGUI before chat (PlayerHUD/LoadingScreenUI are display-only),
    /// so there is no EventSystem yet. The project runs with Active Input Handling set to
    /// "Input System Package (New)" only (see ProjectSettings, activeInputHandler: 1), so the
    /// legacy StandaloneInputModule that Unity normally pairs with EventSystem would silently
    /// never deliver clicks or keyboard text - it has to be InputSystemUIInputModule instead.
    /// </summary>
    internal static class UiEventSystemBootstrap
    {
        public static void EnsureExists()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
