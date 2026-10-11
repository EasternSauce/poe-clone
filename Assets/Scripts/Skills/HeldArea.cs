using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;

namespace PoeClone.Skills
{
    /// <summary>
    /// A damage area kept on the ground round its owner for as long as it's held (Whirlwind),
    /// rather than flashing once per hit: it travels with the owner and fades once let go.
    /// </summary>
    public sealed class HeldArea : MonoBehaviour
    {
        private const float FadeTime = 0.15f;
        private const float Opacity = 0.85f;

        private GroundTelegraph.Decal decal;
        private Vector3 offset;
        private float radius;
        private float heldUntil;

        /// <summary>Shows (or keeps showing) the area at this centre and radius for <paramref name="linger"/> more seconds.</summary>
        public static void Hold(Transform owner, Vector3 center, float radius, Color color, DamageType type, float linger)
        {
            HeldArea area = owner.GetComponent<HeldArea>();
            if (area == null)
                area = owner.gameObject.AddComponent<HeldArea>();
            if (area.decal.Root == null)
            {
                area.decal = GroundTelegraph.ImpactDisc(center, Vector3.forward, radius, 180f, color, type);
                area.decal.SetProgress(1f);
            }
            area.offset = center - owner.position;
            area.offset.y = 0f;
            area.radius = radius;
            area.heldUntil = Time.time + linger;
        }

        /// <summary>Lets the area fade now.</summary>
        public static void Release(Transform owner)
        {
            HeldArea area = owner.GetComponent<HeldArea>();
            if (area != null)
                area.heldUntil = Mathf.Min(area.heldUntil, Time.time);
        }

        private void LateUpdate()
        {
            if (decal.Root == null)
                return;
            float fade = (Time.time - heldUntil) / FadeTime;
            if (fade >= 1f)
            {
                Clear();
                return;
            }
            decal.SetOpacity(Opacity * (1f - Mathf.Clamp01(fade)));
            decal.Follow(transform.position + offset, radius);
        }

        // A hidden owner doesn't update, so its area mustn't linger behind.
        private void OnDisable() => Clear();

        private void Clear()
        {
            if (decal.Root != null)
                Destroy(decal.Root);
            decal = default;
        }
    }
}
