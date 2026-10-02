using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public enum NpcRole
    {
        Elder,
        Merchant,
        Smith,
        Guard,
        Waystone   // not a person: the travel stone (see Waystone)
    }

    /// <summary>
    /// A townsperson the player can talk to (click or tap them; see <see cref="Player.NpcInteractor"/>).
    /// Built from the enemy rig with its AI stripped, dressed per role, with a name tag that also
    /// shows "!" when they have something new for the player. Turns to face the player nearby.
    /// </summary>
    public class Npc : MonoBehaviour
    {
        public const float TalkReach = 3.2f;

        private static readonly List<Npc> all = new List<Npc>();

        public static IReadOnlyList<Npc> All => all;

        public NpcRole Role { get; private set; }
        public string DisplayName { get; private set; }

        private WorldLabel label;
        private string marker = "";
        private Quaternion homeRotation;
        private Transform player;
        private bool turnsToPlayer = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        public static Npc Find(NpcRole role)
        {
            foreach (Npc npc in all)
            {
                if (npc != null && npc.Role == role)
                    return npc;
            }
            return null;
        }

        /// <summary>
        /// The NPC drawn under a screen point: a generous box round their body on screen, so they're
        /// easy to hit with a finger. The nearest one wins if they overlap.
        /// </summary>
        public static Npc AtScreen(Vector2 point)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return null;

            Npc best = null;
            float bestDepth = float.MaxValue;
            foreach (Npc npc in all)
            {
                if (npc == null || !npc.isActiveAndEnabled)
                    continue;

                Vector3 feet = cam.WorldToScreenPoint(npc.transform.position);
                Vector3 head = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * 2.1f);
                if (feet.z <= 0f)
                    continue;

                float height = Mathf.Abs(head.y - feet.y);
                float halfWidth = Mathf.Max(height * 0.28f, 24f);
                bool inside = point.y >= Mathf.Min(feet.y, head.y) - 8f && point.y <= Mathf.Max(feet.y, head.y) + 8f &&
                              Mathf.Abs(point.x - (feet.x + head.x) * 0.5f) <= halfWidth;
                if (inside && feet.z < bestDepth)
                {
                    best = npc;
                    bestDepth = feet.z;
                }
            }
            return best;
        }

        /// <summary>Builds an NPC from the enemy prefab at a spot, facing the given direction.</summary>
        public static Npc Create(GameObject enemyPrefab, NpcRole role, string displayName, EnemyKind look,
            Vector3 position, Vector3 facing, Transform parent)
        {
            // Instantiated under an inactive holder so the enemy scripts never wake up; they're
            // stripped before the NPC goes live.
            var holder = new GameObject("NpcHolder");
            holder.SetActive(false);
            GameObject go = Instantiate(enemyPrefab, holder.transform);
            go.name = "NPC_" + displayName;

            // Dependents first (the health bar requires the health).
            Strip<UI.EnemyHealthBarUI>(go);
            Strip<EnemyCombat>(go);
            Strip<EnemyController>(go);
            Strip<EnemyHealth>(go);

            EnemyKinds.ApplyLook(go, look);

            // Arms down at the sides (the rig's default is the zombie reach).
            CharacterWalkAnimator walk = go.GetComponentInChildren<CharacterWalkAnimator>();
            if (walk != null)
                walk.SetArmRestAngle(0f);

            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing.sqrMagnitude > 0f ? facing : Vector3.back));
            Destroy(holder);

            Npc npc = go.AddComponent<Npc>();
            npc.Role = role;
            npc.DisplayName = displayName;
            npc.homeRotation = go.transform.rotation;
            npc.label = WorldLabel.Create(go.transform, displayName, NameColor, 2.45f, 26);
            go.SetActive(true);
            return npc;
        }

        private static void Strip<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component != null)
                DestroyImmediate(component);
        }

        /// <summary>Makes a fixed object (a waystone) talkable, with a name tag; it doesn't turn.</summary>
        public static Npc CreateFixed(GameObject go, NpcRole role, string displayName, float labelHeight)
        {
            Npc npc = go.AddComponent<Npc>();
            npc.Role = role;
            npc.DisplayName = displayName;
            npc.turnsToPlayer = false;
            npc.homeRotation = go.transform.rotation;
            npc.label = WorldLabel.Create(go.transform, displayName, new Color(0.65f, 0.85f, 1f), labelHeight, 24);
            return npc;
        }

        private static readonly Color NameColor = new Color(0.95f, 0.88f, 0.62f);
        private static readonly Color MarkerColor = new Color(1f, 0.82f, 0.25f);

        /// <summary>"!" for something new to say (a quest to give or hand in), "" for nothing.</summary>
        public void SetMarker(string text)
        {
            if (text == marker || label == null)
                return;
            marker = text;
            label.SetText(string.IsNullOrEmpty(text) ? DisplayName : text + "  " + DisplayName + "  " + text,
                string.IsNullOrEmpty(text) ? NameColor : MarkerColor);
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

        private void Update()
        {
            if (!turnsToPlayer)
                return;

            if (player == null)
            {
                var controller = FindAnyObjectByType<Player.PlayerController>();
                if (controller == null)
                    return;
                player = controller.transform;
            }

            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            Quaternion want = offset.sqrMagnitude < 8f * 8f && offset.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(offset)
                : homeRotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 240f * Time.deltaTime);
        }
    }
}
