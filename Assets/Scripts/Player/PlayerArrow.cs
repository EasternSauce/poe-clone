using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Combat;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    /// <summary>
    /// An arrow from the player's bow (or a Fire Bolt spell). Flies straight ahead and hits the
    /// first thing in its path: an enemy takes the damage, anything else (a tree, a rock) just stops
    /// it. It ignores the shooter's movement, so a shot loosed while strafing still flies at what
    /// was aimed at. A bolt also bursts, damaging every enemy around the impact. There is no ammo.
    /// Spectators get the same arrow as a harmless visual.
    /// </summary>
    public class PlayerArrow : MonoBehaviour
    {
        private const float Speed = 26f;
        private float speed = Speed;
        private bool boneShard;
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
        private WeaponSkillBonuses tree;
        private float skillIncreased;
        private int piercesLeft, chainsLeft;
        private bool forked;
        private float sourceDamageMultiplier = 1f;
        private bool returnRulesInitialized, returns;
        private float returnStarted;
        private readonly ReturningProjectileFlight returnFlight = new ReturningProjectileFlight();
        private bool returning => returnFlight.Returning;
        private readonly HashSet<IDamageable> returnPassed = new HashSet<IDamageable>();
        private HashSet<IDamageable> returnVolley;
        private static ConditionalWeakTable<HashSet<IDamageable>, HashSet<IDamageable>> returnVolleys =
            new ConditionalWeakTable<HashSet<IDamageable>, HashSet<IDamageable>>();
        private float DamageMultiplier => sourceDamageMultiplier * (returns ? ReturningProjectileFlight.DamageMultiplier : 1f);

        /// <summary>Keep damage attributed to the player while launching from an effigy.</summary>
        public PlayerArrow From(Transform origin, float multiplier)
            => FromPosition(origin.position, multiplier);

        public PlayerArrow FromPosition(Vector3 origin, float multiplier = 1f)
        {
            transform.position = origin + Vector3.up * 0.3f + direction * 0.6f;
            launchPosition = transform.position;
            sourceDamageMultiplier = Mathf.Max(0f, multiplier);
            return this;
        }

        private void InitializeReturnRules()
        {
            if (returnRulesInitialized) return;
            returnRulesInitialized = true;
            returns = isAttack && !harmless && owner != null &&
                owner.GetComponent<PlayerInventory>()?.Stats.Total(StatType.ReturningVolley) > 0f;
            if (!returns) return;
            pierced = new HashSet<Enemies.EnemyHealth>();
            returnVolley = volley != null ? returnVolleys.GetValue(volley, _ => new HashSet<IDamageable>()) : new HashSet<IDamageable>();
        }

        private void BeginReturn()
        {
            if (!returnFlight.BeginReturn()) return;
            returnStarted = Time.time;
            returnPassed.Clear();
            pierced.Clear();
        }

        public PlayerArrow WithSkill(WeaponSkillBonuses bonuses, float increasedDamage = 0f)
        {
            tree = bonuses;
            skillIncreased = increasedDamage;
            if (tree == null) return this;
            piercesLeft = Mathf.RoundToInt(tree.Get(WeaponSkillEffect.Pierce));
            chainsLeft = Mathf.RoundToInt(tree.Get(WeaponSkillEffect.Chains));
            if ((piercesLeft > 0 || chainsLeft > 0 || tree.Get(WeaponSkillEffect.Fork) > 0) && pierced == null)
                pierced = new HashSet<Enemies.EnemyHealth>();
            return this;
        }

        private void OnSkillImpact(Enemies.EnemyHealth hit)
        {
            if (tree == null || hit == null) return;
            if (!forked && tree.Get(WeaponSkillEffect.NoFork) <= 0 && tree.Get(WeaponSkillEffect.Fork) > 0 && travelLeft > 1f)
            {
                forked = true;
                foreach (float angle in new[] { -28f, 28f })
                {
                    PlayerArrow child = Create(owner, travelLeft, damage, harmless, direction: Quaternion.Euler(0f, angle, 0f) * direction,
                        orb: isAttack ? (Color?)null : textColor);
                    child.transform.position = transform.position + direction * 0.4f;
                    child.isAttack = isAttack; child.damageType = damageType; child.textColor = textColor;
                    child.burstRadius = burstRadius; child.chillSeconds = chillSeconds; child.igniteBonus = igniteBonus;
                    child.venomArrowLevel = venomArrowLevel; child.launchPosition = launchPosition; child.volley = volley;
                    child.WithSkill(tree, skillIncreased); child.forked = true;
                    child.pierced.Add(hit);
                }
            }
            if (piercesLeft > 0) { piercesLeft--; return; }
            if (chainsLeft > 0)
            {
                Enemies.EnemyHealth next = null;
                float best = 7f * tree.RangeMultiplier;
                foreach (var enemy in Enemies.EnemyHealth.Active)
                {
                    if (enemy == null || enemy.IsDead || enemy == hit || pierced.Contains(enemy)) continue;
                    float d = Vector3.Distance(enemy.transform.position, transform.position);
                    if (d < best) { best = d; next = enemy; }
                }
                if (next != null)
                {
                    chainsLeft--;
                    direction = (next.transform.position + Vector3.up * 0.3f - transform.position).normalized;
                    transform.rotation = Quaternion.LookRotation(direction);
                    travelLeft = Mathf.Max(travelLeft, best + 1f);
                    return;
                }
            }
            // The native Piercing Shot remains unlimited; other projectiles stop after their allowance.
            if (!nativePiercing) Destroy(gameObject);
        }
        private bool nativePiercing;

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
            return returning ? returnVolley.Add(target) : volley == null || volley.Add(target);
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

        /// <summary>A rain arrow that struck an enemy continues from its impact before returning.</summary>
        public static void ReturnAfterRainImpact(Transform shooter, Vector3 impact, Vector3 shotOrigin, float damage,
            WeaponSkillBonuses tree, HashSet<IDamageable> struck)
        {
            Vector3 direction = impact - shotOrigin;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : shooter.forward;
            var arrow = Create(shooter, 3f, damage, false, direction: direction);
            arrow.volley = struck;
            arrow.WithSkill(tree).FromPosition(impact - direction * 0.6f);
            arrow.launchPosition = shotOrigin;
            arrow.InitializeReturnRules();
            arrow.returnFlight.HitEnemy();
            foreach (var target in struck) arrow.returnPassed.Add(target);
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
            nativePiercing = true;
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
        public static PlayerArrow LaunchBolt(Transform shooter, float range, float damage, Color color, float burstRadius, Vector3? direction = null, bool harmless = false, HashSet<IDamageable> volley = null)
        {
            PlayerArrow bolt = Create(shooter, range, damage, harmless, orb: color, direction: direction);
            bolt.volley = volley;
            bolt.burstRadius = burstRadius;
            bolt.textColor = CombatText.FireColor;
            bolt.isAttack = false;
            bolt.damageType = Combat.DamageType.Fire;
            return bolt;
        }

        /// <summary>An Ice Shard: a small, quick shard that chills the enemy it hits.</summary>
        public static PlayerArrow LaunchShard(Transform shooter, float range, float damage, Color color, float chillSeconds, Vector3 direction, bool harmless = false, HashSet<IDamageable> volley = null)
        {
            PlayerArrow shard = Create(shooter, range, damage, harmless, orb: color, direction: direction);
            shard.volley = volley;
            shard.transform.localScale = Vector3.one * 0.6f;
            shard.textColor = CombatText.ColdColor;
            shard.isAttack = false;
            shard.damageType = Combat.DamageType.Cold;
            shard.chillSeconds = chillSeconds;
            return shard;
        }

        public static PlayerArrow LaunchBoneShard(Vector3 at, Vector3 direction, float damage, float speedMultiplier, bool harmless = false)
        {
            var shard = Create(null, WeaponSkillRules.BonecrusherRange, damage, harmless, direction: direction,
                spawnPosition: at + Vector3.up * 0.8f, bone: true);
            shard.boneShard = true;
            shard.isAttack = false;
            shard.speed = 36f * Mathf.Max(0.25f, speedMultiplier);
            shard.textColor = CombatText.PhysicalColor;
            return shard;
        }

        private static PlayerArrow Create(Transform shooter, float range, float damage, bool harmless, Color? orb = null, Vector3? direction = null, Color? arrowColor = null, Vector3? spawnPosition = null, bool bone = false)
        {
            Vector3 forward = direction ?? (shooter != null ? shooter.forward : Vector3.forward);
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            var root = new GameObject(orb.HasValue ? "Player Bolt" : "Player Arrow");
            root.transform.SetPositionAndRotation(spawnPosition ?? Origin(shooter), Quaternion.LookRotation(forward));
            if (bone)
            {
                root.name = "Bonecrusher shard";
                Color ivory = new Color(0.92f, 0.86f, 0.7f);
                var shaft = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, ivory);
                shaft.transform.localScale = new Vector3(0.1f, 0.1f, 0.55f);
                for (int i = -1; i <= 1; i += 2)
                {
                    var end = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, ivory);
                    end.transform.localPosition = new Vector3(i * 0.06f, 0f, -0.22f);
                    end.transform.localScale = new Vector3(0.17f, 0.14f, 0.17f);
                }
            }
            else if (orb.HasValue)
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
            InitializeReturnRules();
            if (returns && returnFlight.CanTurn) BeginReturn();
            float remaining = returns && returnFlight.HasHit ? returnFlight.RemainingDistance : travelLeft;
            if (returning)
            {
                if (owner == null || Time.time - returnStarted > 6f) { Destroy(gameObject); return; }
                Vector3 toOwner = owner.position + Vector3.up * 0.3f - transform.position;
                if (toOwner.magnitude <= 0.3f) { Destroy(gameObject); return; }
                direction = toOwner.normalized;
                transform.rotation = Quaternion.LookRotation(direction);
                remaining = toOwner.magnitude;
            }
            float step = Mathf.Min(speed * (tree?.ProjectileSpeedMultiplier ?? 1f) * Time.deltaTime, remaining);
            if (step <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 motion = direction * step;

            // Sweep this frame's stretch of the flight so a fast arrow can't skip through anything:
            // once wide for enemies, once narrow for everything else.
            RaycastHit? nearest = null;
            Sweep(EnemyRadius, direction, step, enemiesOnly: true, ref nearest);
            Sweep(Radius, direction, step, enemiesOnly: false, ref nearest);

            // A Graveward in the way absorbs it before it reaches anything behind the ward.
            if (Enemies.Graveward.Stops(transform.position, transform.position + motion, out Vector3 warded) &&
                (nearest == null || nearest.Value.distance > Vector3.Distance(transform.position, warded)))
            {
                Skills.SkillEffects.Shockwave(warded, 0.6f, textColor, 0.2f, type: damageType);
                Destroy(gameObject);
                return;
            }

            // A spectator's copy: its enemies have no colliders, so it stops (and bursts) at the
            // first one it passes close to, the way the player's real one did.
            if (harmless && nearest == null && pierced == null)
            {
                Vector3 at;
                if (PassesEnemy(direction, step, out at))
                {
                    if (burstRadius > 0f)
                        Skills.SkillEffects.Shockwave(at, burstRadius, textColor, 0.25f, type: damageType);
                    Destroy(gameObject);
                    return;
                }
            }

            if (returns && nearest != null)
            {
                var target = nearest.Value.collider.GetComponentInParent<IDamageable>();
                if (target is Enemies.EnemyHealth || target is Enemies.SerpentPursuit)
                {
                    float moved = Mathf.Clamp(nearest.Value.distance, 0f, step);
                    transform.position += direction * moved;
                    returnFlight.Advance(moved);
                    returnFlight.HitEnemy();
                    returnPassed.Add(target);
                    Strike(nearest.Value.collider);
                    if (burstRadius > 0f) Burst(transform.position, nearest.Value.collider);
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
                    OnSkillImpact(through);
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
                        Skills.SkillEffects.Shockwave(at, burstRadius, textColor, 0.25f, type: damageType); // a spectator's copy: just the look
                    else
                        Burst(at, nearest.Value.collider);
                }
                Destroy(gameObject);
                return;
            }

            transform.position += motion;
            travelLeft -= step;
            if (returns) returnFlight.Advance(step);
        }

        private bool PassesEnemy(Vector3 heading, float length, out Vector3 at)
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
                float along = Vector3.Dot(to, heading);
                if (along < 0f || along > length)
                    continue;
                float reach = EnemyRadius + 0.4f * enemy.transform.localScale.x;
                if ((to - heading * along).sqrMagnitude <= reach * reach && along < best)
                {
                    best = along;
                    at = from + heading * along;
                }
            }
            return best < float.MaxValue;
        }

        private void Sweep(float radius, Vector3 heading, float length, bool enemiesOnly, ref RaycastHit? nearest)
        {
            int count = Physics.SphereCastNonAlloc(transform.position, radius, heading, Hits, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            for (int k = 0; k < count; k++)
            {
                RaycastHit hit = Hits[k];
                if (boneShard)
                {
                    if (hit.collider.GetComponentInParent<PlayerStats>() != null ||
                        (Party.Partner != null && hit.collider.transform.IsChildOf(Party.Partner))) continue;
                    var enemy = hit.collider.GetComponentInParent<Enemies.EnemyHealth>();
                    if (enemy != null && (enemy.IsDead || enemy.IsAlly)) continue;
                }
                if (owner != null && hit.collider.transform.IsChildOf(owner))
                    continue;
                if (returns && returnPassed.Contains(hit.collider.GetComponentInParent<IDamageable>()))
                    continue;
                if (pierced != null)
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
            Skills.SkillEffects.Shockwave(at, burstRadius, textColor, 0.25f, type: damageType);

            IDamageable direct = struck.GetComponentInParent<IDamageable>();
            var done = new System.Collections.Generic.HashSet<IDamageable> { direct };
            foreach (Collider c in Physics.OverlapSphere(at, burstRadius))
            {
                IDamageable target = c.GetComponentInParent<IDamageable>();
                if (target == null || !done.Add(target) || (owner != null && c.transform.IsChildOf(owner)))
                    continue;
                if (target is Enemies.EnemyHealth enemy && !enemy.IsDead && FirstVolleyHit(target))
                    HitEffects.Deal(owner, enemy, damage * 0.4f, isAttack, textColor, damageType, igniteBonus: igniteBonus,
                        projectileDistance: DistanceTo(at), hitOrigin: launchPosition, tree: tree, skillIncreased: skillIncreased, sourceDamageMultiplier: DamageMultiplier, impactSound: isAttack ? "combat.bow.impact" : null);
            }
        }

        private void Strike(Collider collider)
        {
            if (harmless)
                return;

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (boneShard && !(target is Enemies.EnemyHealth) && !(target is Enemies.SerpentPursuit)) return;
            if (target == null)
            {
                if (isAttack) PoeClone.Audio.AudioManager.Instance?.PlayEffect("combat.bow.impact", collider.ClosestPoint(transform.position));
                return;
            }
            if (!FirstVolleyHit(target)) return;

            if (target is Enemies.EnemyHealth enemy)
            {
                // Chilled before the hit lands, so "against chilled enemies" and Shatter count it.
                if (chillSeconds > 0f)
                {
                    Enemies.EnemyController ai = enemy.GetComponent<Enemies.EnemyController>();
                    if (ai != null)
                        ai.Chill(chillSeconds * (tree?.DurationMultiplier ?? 1f));
                }
                HitEffects.Deal(owner, enemy, damage, isAttack, textColor, damageType, igniteBonus: igniteBonus,
                    projectileDistance: DistanceTo(enemy.transform.position), venomArrowLevel: venomArrowLevel, hitOrigin: launchPosition, tree: tree, skillIncreased: skillIncreased, sourceDamageMultiplier: DamageMultiplier, impactSound: isAttack ? "combat.bow.impact" : null);
                return;
            }

            if (target is Enemies.SerpentPursuit serpent)
            {
                serpent.TakeArrowHit(owner, damage, isAttack, damageType, textColor, igniteBonus,
                    projectileDistance: DistanceTo(serpent.MouthPosition), venomArrowLevel: venomArrowLevel, tree: tree, skillIncreased: skillIncreased,
                    sourceDamageMultiplier: DamageMultiplier, impactSound: isAttack ? "combat.bow.impact" : null);
                return;
            }

            if (isAttack) PoeClone.Audio.AudioManager.Instance?.PlayEffect("combat.bow.impact", collider.ClosestPoint(transform.position));
            target.TakeDamage(damage * DamageMultiplier);

            var hitTransform = (target as Component)?.transform;
            if (hitTransform != null)
                CombatText.Show(hitTransform.position + Vector3.up * 1.6f * hitTransform.localScale.y,
                    Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), textColor);
        }
    }
}
