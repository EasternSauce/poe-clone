using UnityEngine;
using PoeClone.Network.Replication;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A caster's bolt or an archer's arrow. Enraged spells track their target until close,
    /// then commit to a straight line; hits if it passes close enough, otherwise fizzles
    /// at the end of its range. Spectators get the same bolt as a harmless visual.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        private const float HitRadius = 0.75f;
        private const float AimHeight = 0.2f; // above the player's pivot: chest height
        private const float HomingCutoff = 3f;
        private const float EnragedArrowSpeed = 1.5f;

        public static float Speed(EnemyKind kind, bool enraged) =>
            Mathf.Max(1f, kind.ProjectileSpeed) * (enraged && kind.Bow ? EnragedArrowSpeed : 1f);

        private Vector3 velocity;
        private float travelLeft;
        private float damage;
        private int rollSeed;
        private DamageType damageType;
        private bool isAttack;
        private PlayerStats target;   // null = visual only (or aimed at a minion)
        private Minion minion;        // aimed at one of the player's minions instead
        private bool spins;
        private bool blockedByScenery;
        private float spin;
        private bool[] volleyHit;     // shared by arrows loosed together: only the first to land hurts
        private Transform homingTarget;

        /// <summary>A real bolt aimed at a point (a volley's side arrows), that can still hit the player.</summary>
        public static void LaunchAt(Vector3 from, Vector3 aimAt, PlayerStats target, EnemyKind kind, float damage, bool[] volleyHit = null, int rollSeed = 0, bool enraged = false)
        {
            if (target == null)
                return;
            EnemyProjectile bolt = Create(from, new Vector3(aimAt.x, target.transform.position.y + AimHeight, aimAt.z), kind, damage, target, rollSeed, enraged, target.transform);
            bolt.volleyHit = volleyHit;
        }

        /// <summary>A real bolt at one of the player's minions (an enemy drawn off the player by it).</summary>
        public static void LaunchAt(Vector3 from, Minion target, EnemyKind kind, float damage, bool enraged = false)
        {
            if (target == null)
                return;
            EnemyProjectile bolt = Create(from, target.transform.position + Vector3.up * AimHeight, kind, damage, null, enraged: enraged, follow: target.transform);
            bolt.minion = target;
            bolt.travelLeft = kind.AttackRange * 1.6f;
        }

        /// <summary>A harmless bolt for spectators (the hit itself arrives in the replicated health).</summary>
        public static void LaunchVisual(Vector3 from, Vector3 targetPosition, EnemyKind kind, bool enraged = false, Transform follow = null)
        {
            Create(from, targetPosition + Vector3.up * AimHeight, kind, 0f, null, enraged: enraged, follow: follow);
        }

        private static EnemyProjectile Create(Vector3 from, Vector3 aimAt, EnemyKind kind, float damage, PlayerStats target, int rollSeed = 0, bool enraged = false, Transform follow = null)
        {
            Vector3 direction = aimAt - from;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            direction.Normalize();

            var root = new GameObject(kind.Name + (kind.Bow ? " Arrow" : " Bolt"));
            root.transform.position = from;

            if (kind.Bow || kind.Javelin)
            {
                root.transform.rotation = Quaternion.LookRotation(direction);
                RuntimePrimitives.BuildArrow(root.transform);
                if (kind.Javelin) root.transform.localScale = new Vector3(1.6f, 1.6f, 2.1f);
            }
            else
            {
                // A bright orb in the element's colour, with a paler spark circling it so it reads as
                // energy rather than a ball (the root spins).
                GameObject core = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, kind.StaffOrb);
                core.transform.localScale = Vector3.one * 0.32f;

                GameObject spark = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, Color.Lerp(kind.StaffOrb, Color.white, 0.6f));
                spark.transform.localScale = Vector3.one * 0.14f;
                spark.transform.localPosition = new Vector3(0.2f, 0f, 0f);
            }

            var bolt = root.AddComponent<EnemyProjectile>();
            bolt.spins = !kind.Bow && !kind.Javelin;
            bolt.blockedByScenery = kind.Javelin;
            bolt.velocity = direction * Speed(kind, enraged);
            bolt.homingTarget = enraged && !kind.Bow && !kind.Javelin ? follow : null;
            // A real bolt flies on past a dodge; a visual one just needs to reach its mark.
            bolt.travelLeft = target != null ? kind.AttackRange * 1.6f : Vector3.Distance(from, aimAt);
            bolt.damage = damage;
            bolt.rollSeed = target != null ? (rollSeed != 0 ? rollSeed : EventRandom.NextSeed()) : 0;
            bolt.damageType = kind.AttackDamageType;
            bolt.isAttack = kind.Bow || kind.Javelin;
            bolt.target = target;
            return bolt;
        }

        private void Update()
        {
            Vector3 previousPosition = transform.position;
            float step = velocity.magnitude * Time.deltaTime;
            if (homingTarget != null)
            {
                if ((target != null && target.IsDead) || (minion != null && minion.IsDead))
                    homingTarget = null;
                else
                {
                    Vector3 toTarget = homingTarget.position + Vector3.up * AimHeight - transform.position;
                    float distance = toTarget.magnitude;
                    // Stop steering permanently on entering the cutoff, including a frame that crosses it.
                    if (distance <= HomingCutoff)
                        homingTarget = null;
                    else
                    {
                        velocity = toTarget / distance * velocity.magnitude;
                        if (distance - step <= HomingCutoff)
                            homingTarget = null;
                    }
                }
            }
            if (blockedByScenery && step > 0)
            {
                foreach (RaycastHit obstacle in Physics.RaycastAll(transform.position, velocity.normalized, step,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle.collider is CharacterController || obstacle.collider.GetComponentInParent<EnemyHealth>() != null ||
                        obstacle.collider.GetComponentInParent<Minion>() != null || obstacle.collider.GetComponentInParent<PlayerStats>() != null) continue;
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position += velocity * Time.deltaTime;
            travelLeft -= step;

            if (spins)
            {
                spin += Time.deltaTime * 720f;
                transform.rotation = Quaternion.Euler(spin, spin * 0.7f, 0f);
            }

            if (target != null && !target.IsDead)
            {
                if (PassesWithin(previousPosition, target.transform.position + Vector3.up * AimHeight, HitRadius))
                {
                    if (volleyHit == null || !volleyHit[0])
                        target.TakeHit(damage, damageType, attack: isAttack, rollSeed: rollSeed);
                    if (volleyHit != null)
                        volleyHit[0] = true;
                    Destroy(gameObject);
                    return;
                }
            }

            if (minion != null && !minion.IsDead)
            {
                if (PassesWithin(previousPosition, minion.transform.position + Vector3.up * AimHeight, HitRadius * Mathf.Sqrt(1.4f)))
                {
                    minion.TakeHit(damage, damageType);
                    Destroy(gameObject);
                    return;
                }
            }

            if (travelLeft <= 0f)
                Destroy(gameObject);
        }

        // Check the whole frame's flight so fast arrows cannot skip over their target.
        private bool PassesWithin(Vector3 from, Vector3 point, float radius)
        {
            Vector3 flight = transform.position - from;
            float along = flight.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(point - from, flight) / flight.sqrMagnitude) : 0f;
            return (point - (from + flight * along)).sqrMagnitude <= radius * radius;
        }
    }
}
