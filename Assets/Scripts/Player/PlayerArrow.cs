using UnityEngine;
using PoeClone.Combat;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    /// <summary>
    /// An arrow from the player's bow. Flies straight ahead and hits the first thing in its path:
    /// an enemy takes the damage, anything else (a tree, a rock) just stops it. There is no ammo.
    /// Spectators get the same arrow as a harmless visual.
    /// </summary>
    public class PlayerArrow : MonoBehaviour
    {
        private const float Speed = 26f;
        private const float Radius = 0.3f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private Vector3 direction;
        private float travelLeft;
        private float damage;
        private Transform owner;   // the shooter, never hit by its own arrow
        private bool harmless;

        /// <summary>Where arrows leave the bow: chest height, a little in front.</summary>
        public static Vector3 Origin(Transform shooter)
        {
            return shooter.position + Vector3.up * 0.3f + shooter.forward * 0.6f;
        }

        public static void Launch(Transform shooter, float range, float damage)
        {
            Create(shooter, range, damage, harmless: false);
        }

        public static void LaunchVisual(Transform shooter, float range)
        {
            Create(shooter, range, 0f, harmless: true);
        }

        private static void Create(Transform shooter, float range, float damage, bool harmless)
        {
            Vector3 forward = shooter.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            var root = new GameObject("Player Arrow");
            root.transform.SetPositionAndRotation(Origin(shooter), Quaternion.LookRotation(forward));
            RuntimePrimitives.BuildArrow(root.transform);

            var arrow = root.AddComponent<PlayerArrow>();
            arrow.direction = forward;
            arrow.travelLeft = range;
            arrow.damage = damage;
            arrow.owner = shooter;
            arrow.harmless = harmless;
        }

        private void Update()
        {
            float step = Mathf.Min(Speed * Time.deltaTime, travelLeft);
            if (step <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Sweep this frame's stretch of the flight so a fast arrow can't skip through anything.
            int count = Physics.SphereCastNonAlloc(transform.position, Radius, direction, Hits, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            RaycastHit? nearest = null;
            for (int k = 0; k < count; k++)
            {
                RaycastHit hit = Hits[k];
                if (owner != null && hit.collider.transform.IsChildOf(owner))
                    continue;
                // Starting inside something reports distance 0 at the origin; treat that as a hit too.
                if (nearest == null || hit.distance < nearest.Value.distance)
                    nearest = hit;
            }

            if (nearest != null)
            {
                Strike(nearest.Value.collider);
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
            travelLeft -= step;
        }

        private void Strike(Collider collider)
        {
            if (harmless)
                return;

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null)
                return;

            target.TakeDamage(damage);

            var hitTransform = (target as Component)?.transform;
            if (hitTransform != null)
                CombatText.Show(hitTransform.position + Vector3.up * 1.6f * hitTransform.localScale.y,
                    Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), CombatText.PhysicalColor);
        }
    }
}
