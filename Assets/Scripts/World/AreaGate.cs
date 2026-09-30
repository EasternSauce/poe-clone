using UnityEngine;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// Trigger volume that switches the active area when the player walks through it.
    /// Proof-of-concept: reuses the existing world space, just retints the ground and
    /// teleports the player to the target area's spawn point behind a loading screen.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AreaGate : MonoBehaviour
    {
        public int targetAreaIndex;
        public int fromAreaIndex;

        private bool armed = true;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!armed) return;

            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null) return;

            var manager = AreaManager.Instance;
            if (manager == null) return;
            if (manager.CurrentAreaIndex == targetAreaIndex) return;

            armed = false;
            manager.EnterArea(targetAreaIndex);
        }

        private void OnTriggerExit(Collider other)
        {
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc != null) armed = true;
        }
    }
}
