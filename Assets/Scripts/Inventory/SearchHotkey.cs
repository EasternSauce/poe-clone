using UnityEngine;
using UnityEngine.InputSystem;

namespace PoeClone.Inventory
{
    /// <summary>Ctrl+F opens a search box's prompt while its row is on screen (not while typing elsewhere).</summary>
    public class SearchHotkey : MonoBehaviour
    {
        public System.Action Open;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && keyboard.ctrlKey.isPressed && !UiKit.IsTypingInTextField())
                Open?.Invoke();
        }
    }
}
