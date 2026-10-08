using UnityEngine;

namespace PoeClone.World
{
    /// <summary>A small pool of readable light follows the player only in caves.</summary>
    public sealed class CaveVisibility : MonoBehaviour
    {
        private Light lamp;
        private void Awake() { lamp = GetComponent<Light>(); }
        private void LateUpdate()
        {
            AreaManager manager = AreaManager.Instance;
            lamp.enabled = manager != null && manager.CurrentAreaIndex >= 0 && WorldBuilder.Shape(manager.CurrentAreaIndex).IsCave;
        }
    }
}
