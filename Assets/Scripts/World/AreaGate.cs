using UnityEngine;
using PoeClone.Audio;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// A gate that sends the player to another area when they walk into it (its trigger box),
    /// arriving at <see cref="arrival"/> (in front of the gate back) or the area's spawn point.
    /// Checks the player against the box itself every frame rather than relying on trigger
    /// messages, which a character controller doesn't reliably raise against a static trigger.
    /// It has to be left (stepped out of) before it works again, so arriving next to a gate can't
    /// bounce the player straight back.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AreaGate : MonoBehaviour
    {
        public int targetAreaIndex;
        public int fromAreaIndex;

        [Tooltip("Where the player appears in the target area (in front of the gate back). Empty = the area's spawn point.")]
        public Transform arrival;

        private bool armed = true;
        private Collider box;
        private PlayerController player;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            box = GetComponent<Collider>();
        }

        private void Update()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
                if (player == null)
                    return;
            }

            // A spectator's puppet player (controller off) never walks through gates itself.
            if (!player.enabled)
                return;

            bool inside = box.bounds.Contains(player.transform.position);
            if (!inside)
            {
                armed = true;
                return;
            }

            if (!armed)
                return;

            var manager = AreaManager.Instance;
            if (manager == null || manager.IsSwitching || manager.CurrentAreaIndex == targetAreaIndex)
                return;

            armed = false;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(AudioManager.Instance.gateOpen, transform.position);

            manager.EnterArea(targetAreaIndex, arrival);
        }
    }
}
