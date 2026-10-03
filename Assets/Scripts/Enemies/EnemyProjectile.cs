using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A caster's bolt or an archer's arrow. Flies in a straight line at where the player was when it was cast, so
    /// stepping sideways dodges it; hits the player if it passes close enough, otherwise fizzles
    /// at the end of its range. Spectators get the same bolt as a harmless visual.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        private const float HitRadius = 0.75f;
        private const float AimHeight = 0.2f; // above the player's pivot: chest height

        private Vector3 velocity;
        private float travelLeft;
        private float damage;
        private DamageType damageType;
        private PlayerStats target;   // null = visual only (or aimed at a minion)
        private Minion minion;        // aimed at one of the player's minions instead
        private bool spins;
        private float spin;
        private bool[] volleyHit;     // shared by arrows loosed together: only the first to land hurts

        /// <summary>A real bolt that can hit the player.</summary>
        public static void Launch(Vector3 from, PlayerStats target, EnemyKind kind, float damage)
        {
            if (target == null)
                return;
            Create(from, target.transform.position + Vector3.up * AimHeight, kind, damage, target);
        }

        /// <summary>A real bolt aimed at a point (a volley's side arrows), that can still hit the player.</summary>
        public static void LaunchAt(Vector3 from, Vector3 aimAt, PlayerStats target, EnemyKind kind, float damage, bool[] volleyHit = null)
        {
            if (target == null)
                return;
            Create(from, new Vector3(aimAt.x, target.transform.position.y + AimHeight, aimAt.z), kind, damage, target).volleyHit = volleyHit;
        }

        /// <summary>A real bolt at one of the player's minions (an enemy drawn off the player by it).</summary>
        public static void LaunchAt(Vector3 from, Minion target, EnemyKind kind, float damage)
        {
            if (target == null)
                return;
            EnemyProjectile bolt = Create(from, target.transform.position + Vector3.up * AimHeight, kind, damage, null);
            bolt.minion = target;
            bolt.travelLeft = kind.AttackRange * 1.6f;
        }

        /// <summary>A harmless bolt for spectators (the hit itself arrives in the replicated health).</summary>
        public static void LaunchVisual(Vector3 from, Vector3 targetPosition, EnemyKind kind)
        {
            Create(from, targetPosition + Vector3.up * AimHeight, kind, 0f, null);
        }

        private static EnemyProjectile Create(Vector3 from, Vector3 aimAt, EnemyKind kind, float damage, PlayerStats target)
        {
            Vector3 direction = aimAt - from;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            direction.Normalize();

            var root = new GameObject(kind.Name + (kind.Bow ? " Arrow" : " Bolt"));
            root.transform.position = from;

            if (kind.Bow)
            {
                root.transform.rotation = Quaternion.LookRotation(direction);
                RuntimePrimitives.BuildArrow(root.transform);
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
            bolt.spins = !kind.Bow;
            bolt.velocity = direction * Mathf.Max(1f, kind.ProjectileSpeed);
            // A real bolt flies on past a dodge; a visual one just needs to reach its mark.
            bolt.travelLeft = target != null ? kind.AttackRange * 1.6f : Vector3.Distance(from, aimAt);
            bolt.damage = damage;
            bolt.damageType = kind.DamageType;
            bolt.target = target;
            return bolt;
        }

        private void Update()
        {
            float step = velocity.magnitude * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            travelLeft -= step;

            if (spins)
            {
                spin += Time.deltaTime * 720f;
                transform.rotation = Quaternion.Euler(spin, spin * 0.7f, 0f);
            }

            if (target != null && !target.IsDead)
            {
                Vector3 toTarget = target.transform.position + Vector3.up * AimHeight - transform.position;
                if (toTarget.sqrMagnitude <= HitRadius * HitRadius)
                {
                    if (volleyHit == null || !volleyHit[0])
                        target.TakeHit(damage, damageType);
                    if (volleyHit != null)
                        volleyHit[0] = true;
                    Destroy(gameObject);
                    return;
                }
            }

            if (minion != null && !minion.IsDead)
            {
                Vector3 toMinion = minion.transform.position + Vector3.up * AimHeight - transform.position;
                if (toMinion.sqrMagnitude <= HitRadius * HitRadius * 1.4f)
                {
                    minion.TakeHit(damage, damageType);
                    Destroy(gameObject);
                    return;
                }
            }

            if (travelLeft <= 0f)
                Destroy(gameObject);
        }
    }
}
