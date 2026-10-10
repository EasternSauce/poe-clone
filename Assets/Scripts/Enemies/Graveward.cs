using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A short directional ward that stops the player's projectiles crossing it from the front.
    /// It has no collider: characters walk through it, and melee and area hits are unaffected.
    /// </summary>
    public sealed class Graveward : MonoBehaviour
    {
        private const float HalfWidth = 2.9f;
        private const float Height = 3f;
        private const float Duration = 5f;
        private static readonly List<Graveward> Active = new List<Graveward>();
        private EnemyHealth owner;
        private Vector3 normal;
        private float expires;
        private float blockedTextAt;

        private bool IsActive => Time.time < expires && owner != null && !owner.IsDead && owner.gameObject.activeInHierarchy;

        public static void Raise(Transform caster, Vector3 facing, Color color)
        {
            facing.y = 0f;
            facing = facing.sqrMagnitude > 0.001f ? facing.normalized : caster.forward;
            var root = new GameObject("Graveward");
            root.transform.position = caster.position + facing * 0.9f;
            root.transform.rotation = Quaternion.LookRotation(facing);
            var ward = root.AddComponent<Graveward>();
            ward.owner = caster.GetComponent<EnemyHealth>();
            ward.normal = facing;
            ward.expires = Time.time + Duration;
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * (HalfWidth / 2f);
                GameObject stake = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color);
                stake.transform.localPosition = new Vector3(x, 0.05f, 0f);
                stake.transform.localScale = new Vector3(0.065f, 1.8f, 0.06f);
                GameObject rune = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color);
                rune.transform.localPosition = new Vector3(x, 0.65f, 0f);
                rune.transform.localScale = new Vector3(0.18f, 0.18f, 0.07f);
                rune.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            for (int i = 0; i < 3; i++)
            {
                GameObject thread = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color * 0.7f);
                thread.transform.localPosition = new Vector3(0f, -0.5f + i * 0.6f, 0f);
                thread.transform.localScale = new Vector3(HalfWidth * 2f, 0.04f, 0.045f);
            }
            Destroy(root, Duration);
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (!IsActive) Destroy(gameObject);
        }

        /// <summary>
        /// Whether a projectile moving from <paramref name="from"/> to <paramref name="to"/> this frame
        /// crosses a ward from its front; <paramref name="at"/> is where it meets the ward.
        /// </summary>
        public static bool Stops(Vector3 from, Vector3 to, out Vector3 at)
        {
            at = to;
            foreach (Graveward ward in Active)
            {
                if (ward == null || !ward.IsActive) continue;
                Vector3 origin = ward.transform.position;
                float front = Vector3.Dot(from - origin, ward.normal);
                float behind = Vector3.Dot(to - origin, ward.normal);
                if (front <= 0f || behind > 0f) continue;
                Vector3 crossing = Vector3.Lerp(from, to, front / (front - behind));
                Vector3 side = Vector3.Cross(Vector3.up, ward.normal);
                if (Mathf.Abs(Vector3.Dot(crossing - origin, side)) > HalfWidth) continue;
                if (crossing.y < origin.y - 1f || crossing.y > origin.y + Height) continue;
                at = crossing;
                if (Time.time >= ward.blockedTextAt)
                {
                    ward.blockedTextAt = Time.time + 0.35f;
                    UI.CombatText.Show(crossing + Vector3.up * 0.5f, "Warded", UI.CombatText.AvoidColor, 0.7f);
                }
                return true;
            }
            return false;
        }
    }
}
