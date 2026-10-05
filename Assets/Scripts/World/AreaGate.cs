using System.Collections.Generic;
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

        /// <summary>While this says so, the gate doesn't work (sealed until a quest opens it; see Quests.QuestBarrier).</summary>
        public System.Func<bool> Locked;

        /// <summary>What the player is told walking into a locked gate.</summary>
        public string LockedMessage = "The way is sealed";

        private static readonly List<AreaGate> all = new List<AreaGate>();
        private float lockedToldAt = -10f;

        private bool armed = true;
        private Collider box;
        private PlayerController player;
        private Bounds clickBounds;
        private bool clickBoundsReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        /// <summary>A point in the middle of the gate's trigger, at ground level: walking there goes through.</summary>
        public Vector3 WalkPoint
        {
            get
            {
                Vector3 c = box != null ? box.bounds.center : transform.position;
                return new Vector3(c.x, transform.position.y, c.z);
            }
        }

        /// <summary>
        /// The gate drawn under a screen point (its arch, panel, or the trigger in it), so clicking
        /// one walks the player through it instead of attacking the air.
        /// </summary>
        public static AreaGate AtScreen(Vector2 point)
        {
            Camera cam = Camera.main;
            if (cam == null || all.Count == 0)
                return null;
            AreaManager manager = AreaManager.Instance;
            if (manager == null || manager.IsSwitching)
                return null;

            Ray ray = cam.ScreenPointToRay(point);
            AreaGate best = null;
            float bestDistance = float.MaxValue;
            foreach (AreaGate gate in all)
            {
                if (gate == null || !gate.isActiveAndEnabled || gate.fromAreaIndex != manager.CurrentAreaIndex)
                    continue;
                if (gate.ClickBounds().IntersectRay(ray, out float distance) && distance < bestDistance)
                {
                    best = gate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Require the player to leave a gate they landed inside before it can transfer them.</summary>
        public static void DisarmAtArrival(int areaIndex, Vector3 position)
        {
            foreach (AreaGate gate in all)
            {
                if (gate != null && gate.fromAreaIndex == areaIndex && gate.box != null &&
                    gate.box.bounds.Contains(position))
                    gate.armed = false;
            }
        }

        // Everything the gate draws plus its trigger, measured once (gates don't move).
        private Bounds ClickBounds()
        {
            if (!clickBoundsReady)
            {
                clickBounds = box.bounds;
                foreach (Renderer r in GetComponentsInChildren<Renderer>())
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                        clickBounds.Encapsulate(r.bounds);
                }
                clickBoundsReady = true;
            }
            return clickBounds;
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

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

            if (Locked != null && Locked())
            {
                if (Time.time - lockedToldAt > 3f)
                {
                    lockedToldAt = Time.time;
                    UI.CombatText.Show(player.transform.position + Vector3.up * 2.4f, LockedMessage, new Color(0.7f, 0.85f, 1f), 0.85f);
                }
                return;
            }

            var manager = AreaManager.Instance;
            if (manager == null || manager.IsSwitching || manager.CurrentAreaIndex != fromAreaIndex)
                return;

            armed = false;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(AudioManager.Instance.gateOpen, transform.position);

            manager.EnterArea(targetAreaIndex, arrival);
        }
    }
}
