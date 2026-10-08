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
        public const float BowRangeMultiplier = 1.5f;
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
        private Combat.DamageType damageType = Combat.DamageType.Physical;
        private float chillSeconds;     // an ice shard slows what it hits
        private float igniteBonus;      // a burning arrow's own chance to ignite
        private Vector3 launchPosition;
        private int venomArrowLevel;

        public PlayerArrow Venomous(int level)
        {
            venomArrowLevel = Mathf.Max(1, level);
            return this;
        }

        private float DistanceTo(Vector3 at)
        {
            Vector3 offset = at - launchPosition;
            offset.y = 0f;
            return offset.magnitude;
        }
        private HashSet<Enemies.EnemyHealth> pierced; // a piercing arrow flies on through these

        // Projectiles loosed together (Ice Shard's fan, extra arrows or bolts) share this: each
        // target is hurt by the first of them to reach it, and the rest do nothing to it.
        private HashSet<IDamageable> volley;

        public float TravelRemaining => travelLeft;

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

        public static PlayerArrow Launch(Transform shooter, float range, float damage, Vector3? direction = null, HashSet<IDamageable> volley = null)
        {
            PlayerArrow arrow = Create(shooter, range * BowRangeMultiplier, damage, harmless: false, direction: direction);
            arrow.volley = volley;
            return arrow;
        }

        public static void LaunchVisual(Transform shooter, float range, Vector3? direction = null)
        {
            Create(shooter, range, 0f, harmless: true, direction: direction);
        }

        /// <summary>
        /// A bow skill's arrow (see PlayerSkills.ReleaseBow). Make it pierce or burn with
        /// <see cref="Piercing"/> and <see cref="Burning"/>. Harmless: a spectator's copy.
        /// </summary>
        public static PlayerArrow LaunchArrow(Transform shooter, float range, float damage, Vector3 direction, HashSet<IDamageable> volley, bool harmless = false, Color? arrowColor = null)
        {
            PlayerArrow arrow = Create(shooter, range * BowRangeMultiplier, damage, harmless, direction: direction, arrowColor: arrowColor);
            arrow.volley = volley;
            return arrow;
        }

        /// <summary>Flies on through every enemy in its path (walls and trees still stop it).</summary>
        public PlayerArrow Piercing(Color glow)
        {
            pierced = new HashSet<Enemies.EnemyHealth>();
            transform.localScale = new Vector3(1.2f, 1.2f, 1.5f);
            GameObject streak = RuntimePrimitives.Create(PrimitiveType.Sphere, transform, glow);
            streak.transform.localScale = new Vector3(0.12f, 0.12f, 0.9f);
            streak.transform.localPosition = new Vector3(0f, 0f, -0.35f);
            return this;
        }

        /// <summary>Deals fire, bursts round what it hits, and may set it burning.</summary>
        public PlayerArrow Burning(Color flame, float radius, float igniteChance)
        {
            textColor = CombatText.FireColor;
            damageType = Combat.DamageType.Fire;
            burstRadius = radius;
            igniteBonus = igniteChance;
            GameObject fire = RuntimePrimitives.Create(PrimitiveType.Sphere, transform, flame);
            fire.transform.localScale = Vector3.one * 0.3f;
            fire.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            GameObject glow = RuntimePrimitives.Create(PrimitiveType.Sphere, transform, Color.Lerp(flame, Color.yellow, 0.5f));
            glow.transform.localScale = Vector3.one * 0.17f;
            glow.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            return this;
        }

        /// <summary>A Fire Bolt: an orb that bursts on impact, hurting everything within the radius.</summary>
        public static void LaunchBolt(Transform shooter, float range, float damage, Color color, float burstRadius, Vector3? direction = null, bool harmless = false, HashSet<IDamageable> volley = null)
        {
            PlayerArrow bolt = Create(shooter, range, damage, harmless, orb: color, direction: direction);
            bolt.volley = volley;
            bolt.burstRadius = burstRadius;
            bolt.textColor = CombatText.FireColor;
            bolt.isAttack = false;
            bolt.damageType = Combat.DamageType.Fire;
        }

        /// <summary>An Ice Shard: a small, quick shard that chills the enemy it hits.</summary>
        public static void LaunchShard(Transform shooter, float range, float damage, Color color, float chillSeconds, Vector3 direction, bool harmless = false, HashSet<IDamageable> volley = null)
        {
            PlayerArrow shard = Create(shooter, range, damage, harmless, orb: color, direction: direction);
            shard.volley = volley;
            shard.transform.localScale = Vector3.one * 0.6f;
            shard.textColor = CombatText.ColdColor;
            shard.isAttack = false;
            shard.damageType = Combat.DamageType.Cold;
            shard.chillSeconds = chillSeconds;
        }

        private static PlayerArrow Create(Transform shooter, float range, float damage, bool harmless, Color? orb = null, Vector3? direction = null, Color? arrowColor = null)
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
                RuntimePrimitives.BuildArrow(root.transform, arrowColor);
            }

            var arrow = root.AddComponent<PlayerArrow>();
            arrow.direction = forward;
            arrow.travelLeft = range;
            arrow.damage = damage;
            arrow.owner = shooter;
            arrow.launchPosition = root.transform.position;
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

            // A spectator's copy: its enemies have no colliders, so it stops (and bursts) at the
            // first one it passes close to, the way the player's real one did.
            if (harmless && nearest == null && pierced == null)
            {
                Vector3 at;
                if (PassesEnemy(step, out at))
                {
                    if (burstRadius > 0f)
                        Skills.SkillEffects.Shockwave(at, burstRadius, textColor, 0.25f);
                    Destroy(gameObject);
                    return;
                }
            }

            // A piercing arrow strikes the enemy and keeps going (it sweeps on from here next frame,
            // past that enemy).
            if (nearest != null && pierced != null)
            {
                Enemies.EnemyHealth through = nearest.Value.collider.GetComponentInParent<Enemies.EnemyHealth>();
                if (through != null && !through.IsDead)
                {
                    pierced.Add(through);
                    Strike(nearest.Value.collider);
                    return;
                }
            }

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

        private bool PassesEnemy(float step, out Vector3 at)
        {
            at = Vector3.zero;
            float best = float.MaxValue;
            Vector3 from = transform.position;
            foreach (Enemies.EnemyHealth enemy in Enemies.EnemyHealth.Active)
            {
                if (enemy == null || enemy.IsDead)
                    continue;
                Vector3 to = enemy.transform.position - from;
                to.y = 0f;
                float along = Vector3.Dot(to, direction);
                if (along < 0f || along > step)
                    continue;
                float reach = EnemyRadius + 0.4f * enemy.transform.localScale.x;
                if ((to - direction * along).sqrMagnitude <= reach * reach && along < best)
                {
                    best = along;
                    at = from + direction * along;
                }
            }
            return best < float.MaxValue;
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
                if (pierced != null && pierced.Count > 0)
                {
                    Enemies.EnemyHealth passed = hit.collider.GetComponentInParent<Enemies.EnemyHealth>();
                    if (passed != null && (passed.IsDead || pierced.Contains(passed)))
                        continue;
                }
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
                    HitEffects.Deal(owner, enemy, damage * 0.4f, isAttack, textColor, damageType, igniteBonus: igniteBonus,
                        projectileDistance: DistanceTo(at), hitOrigin: launchPosition);
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
                if (Enemies.Graveward.Blocks(enemy, launchPosition)) return;
                // Chilled before the hit lands, so "against chilled enemies" and Shatter count it.
                if (chillSeconds > 0f)
                {
                    Enemies.EnemyController ai = enemy.GetComponent<Enemies.EnemyController>();
                    if (ai != null)
                        ai.Chill(chillSeconds);
                }
                HitEffects.Deal(owner, enemy, damage, isAttack, textColor, damageType, igniteBonus: igniteBonus,
                    projectileDistance: DistanceTo(enemy.transform.position), venomArrowLevel: venomArrowLevel, hitOrigin: launchPosition);
                return;
            }

            if (target is Enemies.SerpentPursuit serpent)
            {
                serpent.TakeArrowHit(owner, damage, isAttack, damageType, textColor, igniteBonus,
                    projectileDistance: DistanceTo(serpent.MouthPosition), venomArrowLevel: venomArrowLevel);
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
