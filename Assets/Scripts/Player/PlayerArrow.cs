using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    /// <summary>
    /// An arrow from the player's bow (or a Fire Bolt spell). Flies straight ahead and hits the
    /// first thing in its path: an enemy takes the damage, anything else (a tree, a rock) just stops
    /// it. A bolt also bursts, damaging every enemy around the impact. There is no ammo.
    /// Spectators get the same arrow as a harmless visual.
    /// </summary>
    public class PlayerArrow : MonoBehaviour
    {
        private const float Speed = 26f;
        private const float Radius = 0.3f;
        // Enemies are hit from further off than walls are: a shot that visibly grazes one counts.
        private const float EnemyRadius = 0.6f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private Vector3 direction;
        private float travelLeft;
        private float damage;
        private Transform owner;   // the shooter, never hit by its own arrow
        private bool harmless;
        private float burstRadius;
        private Color textColor = CombatText.PhysicalColor;
        private bool isAttack = true;   // an arrow; a bolt is a spell
        private float chillSeconds;     // an ice shard slows what it hits

        // Projectiles loosed together (Ice Shard's fan, extra arrows or bolts) share this: each
        // target is hurt by the first of them to reach it, and the rest do nothing to it.
        private HashSet<IDamageable> volley;

        /// <summary>A shared record for projectiles loosed together (see the volley parameters).</summary>
        public static HashSet<IDamageable> NewVolley()
        {
            return new HashSet<IDamageable>();
        }

        // The first hit on this target from this projectile's volley (always true without one).
        private bool FirstVolleyHit(IDamageable target)
        {
            return volley == null || volley.Add(target);
        }

        /// <summary>Where arrows leave the bow: chest height, a little in front.</summary>
        public static Vector3 Origin(Transform shooter)
        {
            return shooter.position + Vector3.up * 0.3f + shooter.forward * 0.6f;
        }

        public static void Launch(Transform shooter, float range, float damage, Vector3? direction = null, HashSet<IDamageable> volley = null)
        {
            Create(shooter, range, damage, harmless: false, direction: direction).volley = volley;
        }

        public static void LaunchVisual(Transform shooter, float range)
        {
            Create(shooter, range, 0f, harmless: true);
        }

        /// <summary>A Fire Bolt: an orb that bursts on impact, hurting everything within the radius.</summary>
        public static void LaunchBolt(Transform shooter, float range, float damage, Color color, float burstRadius, Vector3? direction = null, bool harmless = false, HashSet<IDamageable> volley = null)
        {
            PlayerArrow bolt = Create(shooter, range, damage, harmless, orb: color, direction: direction);
            bolt.volley = volley;
            bolt.burstRadius = burstRadius;
            bolt.textColor = CombatText.FireColor;
            bolt.isAttack = false;
        }

        /// <summary>An Ice Shard: a small, quick shard that chills the enemy it hits.</summary>
        public static void LaunchShard(Transform shooter, float range, float damage, Color color, float chillSeconds, Vector3 direction, bool harmless = false, HashSet<IDamageable> volley = null)
        {
            PlayerArrow shard = Create(shooter, range, damage, harmless, orb: color, direction: direction);
            shard.volley = volley;
            shard.transform.localScale = Vector3.one * 0.6f;
            shard.textColor = CombatText.ColdColor;
            shard.isAttack = false;
            shard.chillSeconds = chillSeconds;
        }

        private static PlayerArrow Create(Transform shooter, float range, float damage, bool harmless, Color? orb = null, Vector3? direction = null)
        {
            Vector3 forward = direction ?? shooter.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            var root = new GameObject(orb.HasValue ? "Player Bolt" : "Player Arrow");
            root.transform.SetPositionAndRotation(Origin(shooter), Quaternion.LookRotation(forward));
            if (orb.HasValue)
            {
                GameObject core = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, orb.Value);
                core.transform.localScale = Vector3.one * 0.4f;
                GameObject trail = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, Color.Lerp(orb.Value, Color.white, 0.5f));
                trail.transform.localScale = Vector3.one * 0.22f;
                trail.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            }
            else
            {
                RuntimePrimitives.BuildArrow(root.transform);
            }

            var arrow = root.AddComponent<PlayerArrow>();
            arrow.direction = forward;
            arrow.travelLeft = range;
            arrow.damage = damage;
            arrow.owner = shooter;
            arrow.harmless = harmless;
            return arrow;
        }

        private void Update()
        {
            float step = Mathf.Min(Speed * Time.deltaTime, travelLeft);
            if (step <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Sweep this frame's stretch of the flight so a fast arrow can't skip through anything:
            // once wide for enemies, once narrow for everything else.
            RaycastHit? nearest = null;
            Sweep(EnemyRadius, step, enemiesOnly: true, ref nearest);
            Sweep(Radius, step, enemiesOnly: false, ref nearest);

            if (nearest != null)
            {
                Strike(nearest.Value.collider);
                if (burstRadius > 0f)
                {
                    // A hit reported at distance 0 (started inside it) has no contact point.
                    Vector3 at = nearest.Value.distance > 0f ? nearest.Value.point : transform.position;
                    if (harmless)
                        Skills.SkillEffects.Shockwave(at, burstRadius, textColor, 0.25f); // a spectator's copy: just the look
                    else
                        Burst(at, nearest.Value.collider);
                }
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
            travelLeft -= step;
        }

        private void Sweep(float radius, float step, bool enemiesOnly, ref RaycastHit? nearest)
        {
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, Hits, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            for (int k = 0; k < count; k++)
            {
                RaycastHit hit = Hits[k];
                if (owner != null && hit.collider.transform.IsChildOf(owner))
                    continue;
                if (enemiesOnly)
                {
                    Enemies.EnemyHealth enemy = hit.collider.GetComponentInParent<Enemies.EnemyHealth>();
                    if (enemy == null || enemy.IsDead)
                        continue;
                }
                // Starting inside something reports distance 0 at the origin; treat that as a hit too.
                if (nearest == null || hit.distance < nearest.Value.distance)
                    nearest = hit;
            }
        }

        // Splash damage around the impact (the enemy struck directly already took its share).
        private void Burst(Vector3 at, Collider struck)
        {
            Skills.SkillEffects.Shockwave(at, burstRadius, textColor, 0.25f);

            IDamageable direct = struck.GetComponentInParent<IDamageable>();
            var done = new System.Collections.Generic.HashSet<IDamageable> { direct };
            foreach (Collider c in Physics.OverlapSphere(at, burstRadius))
            {
                IDamageable target = c.GetComponentInParent<IDamageable>();
                if (target == null || !done.Add(target) || (owner != null && c.transform.IsChildOf(owner)))
                    continue;
                if (target is Enemies.EnemyHealth enemy && !enemy.IsDead && FirstVolleyHit(target))
                    HitEffects.Deal(owner, enemy, damage * 0.4f, isAttack, textColor);
            }
        }

        private void Strike(Collider collider)
        {
            if (harmless)
                return;

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || !FirstVolleyHit(target))
                return;

            if (target is Enemies.EnemyHealth enemy)
            {
                HitEffects.Deal(owner, enemy, damage, isAttack, textColor);
                if (chillSeconds > 0f && !enemy.IsDead)
                {
                    Enemies.EnemyController ai = enemy.GetComponent<Enemies.EnemyController>();
                    if (ai != null)
                        ai.Chill(chillSeconds);
                }
                return;
            }

            target.TakeDamage(damage);

            var hitTransform = (target as Component)?.transform;
            if (hitTransform != null)
                CombatText.Show(hitTransform.position + Vector3.up * 1.6f * hitTransform.localScale.y,
                    Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), textColor);
        }
    }
}
