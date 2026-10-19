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
            var events = Object.FindAnyObjectByType<EventSystem>();
            if (events == null)
            {
                var go = new GameObject("EventSystem");
                events = go.AddComponent<EventSystem>();
                Object.DontDestroyOnLoad(go);
            }
            events.enabled = true;
            // An existing EventSystem is not enough: a scene may supply a legacy or disabled module.
            foreach (var other in events.GetComponents<BaseInputModule>())
                if (!(other is InputSystemUIInputModule)) other.enabled = false;
            var input = events.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = events.gameObject.AddComponent<InputSystemUIInputModule>();
            if (input.point == null || input.leftClick == null) input.AssignDefaultActions();
            input.enabled = true;
        }
    }
}
