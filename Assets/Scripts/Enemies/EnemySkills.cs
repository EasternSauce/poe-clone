using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A (non-boss) enemy's special move, used now and then in a fight when it makes sense:
    /// see <see cref="EnemySkill"/>. Blasts glow on the ground first, so they can be dodged.
    /// Added by <see cref="EnemyKinds.Apply"/> for kinds that have a skill.
    ///
    /// Spectators: <see cref="UseCount"/> and <see cref="LastTarget"/> go out in the snapshot, and the
    /// replica plays the same move's visuals (<see cref="PlayVisual"/>) with no damage.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemySkills : MonoBehaviour
    {
        private const float ChargeSpeed = 16f;
        private const float ChargeMaxSeconds = 0.6f;
        private const int MaxMinions = 3;

        /// <summary>Skills used so far (a counter, so a use between two snapshots is never lost).</summary>
        public int UseCount { get; private set; }

        /// <summary>Where the latest skill was aimed.</summary>
        public Vector3 LastTarget { get; private set; }

        /// <summary>Winding up or charging: the enemy's own movement waits.</summary>
        public bool Busy => Time.time < busyUntil;

        private EnemyKind kind;
        private int level;
        private EnemyHealth health;
        private CharacterController body;
        private CharacterAttackAnimator attackAnimator;
        private EnemyController controller;
        private Stagger stagger;
        private PlayerStats player;
        private float nextUse;
        private float busyUntil = -1f;
        private readonly List<EnemyHealth> minions = new List<EnemyHealth>();

        public void Configure(EnemyKind enemyKind, int monsterLevel)
        {
            kind = enemyKind;
            level = monsterLevel;
            enabled = true;
            // The first use comes a little into a fight, not the moment it starts.
            nextUse = Time.time + Random.Range(2.5f, 5f);
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            body = GetComponent<CharacterController>();
            stagger = GetComponent<Stagger>();
            controller = GetComponent<EnemyController>();
            health.Died += OnDied;
        }

        private void Start()
        {
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
        }

        // Raised skeletons fall with the necromancer.
        private void OnDied()
        {
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    minion.TakeDamage(minion.CurrentHealth + 1f);
            }
            minions.Clear();
        }

        private void Update()
        {
            if (kind == null || health.IsDead)
                return;
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerStats>();
                if (player == null)
                    return;
            }

            TrackPlayer();

            if (player.IsDead || Time.time < nextUse || Busy)
                return;
            if ((stagger != null && stagger.IsStaggered) || (attackAnimator != null && attackAnimator.IsAttacking))
                return;

            Vector3 toPlayer = Flat(player.transform.position - transform.position);
            float distance = toPlayer.magnitude;
            if (!InPosition(distance))
                return;

            nextUse = Time.time + kind.SkillCooldown * Random.Range(0.8f, 1.2f);
            Use(distance);
        }

        // Whether this skill makes sense from here.
        private bool InPosition(float distance)
        {
            switch (kind.Skill)
            {
                case EnemySkill.Slam: return distance < 3.2f * transform.localScale.y;
                case EnemySkill.Charge: return distance > 4f && distance < 11f;
                case EnemySkill.Volley: return distance < kind.AttackRange;
                case EnemySkill.Strike: return distance < kind.AttackRange + 2f;
                case EnemySkill.Blink: return distance > 4f && distance < 14f;
                case EnemySkill.WarCry: return distance < 14f && AnyHurtAllyNear();
                case EnemySkill.Summon: return distance < 14f && minions.FindAll(m => m != null && !m.IsDead).Count < MaxMinions;
                case EnemySkill.Leap: return distance > 3f && distance < LeapMaxDistance - 2f;
                default: return false;
            }
        }

        private void Use(float distance)
        {
            Vector3 target = player.transform.position;
            Vector3 facing = Flat(target - transform.position);
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(facing);

            UseCount++;
            // Enraged, aimed moves lead the player: the blast lands, or the volley flies, where
            // they're heading rather than where they stand.
            bool enraged = controller != null && controller.IsEnraged;
            if (enraged && kind.Skill == EnemySkill.Strike)
                target = PlayerMotion.Predict(player, StrikeWindUp(kind));
            else if (enraged && kind.Skill == EnemySkill.Volley)
                target = PlayerMotion.Intercept(player, EnemyCombat.BoltOrigin(transform), kind.ProjectileSpeed);
            if (kind.Skill == EnemySkill.Leap)
                target = LeapLanding(transform.position, PredictPlayer(LeapCrouch + LeapAir), LeapGap(transform));
            LastTarget = kind.Skill == EnemySkill.Slam ? transform.position : target;
            float damage = kind.Damage * EnemyKinds.DamageScale(level) * (controller != null ? controller.DamageMultiplier : 1f);

            switch (kind.Skill)
            {
                case EnemySkill.Slam:
                    busyUntil = Time.time + SlamWindUp;
                    StartCoroutine(GroundTelegraph.Run(transform.position, SlamRadius(kind, transform), SlamWindUp, kind.DamageType,
                        center => HitIfInside(center, SlamRadius(kind, transform), damage * 1.4f)));
                    break;
                case EnemySkill.Strike:
                    float windUp = StrikeWindUp(kind);
                    StartCoroutine(GroundTelegraph.Run(target, StrikeRadius, windUp, kind.DamageType,
                        center => HitIfInside(center, StrikeRadius, damage * 1.3f)));
                    break;
                case EnemySkill.Volley:
                    Volley(transform, kind, target, player, damage);
                    break;
                case EnemySkill.Charge:
                    StartCoroutine(Charge(damage * 1.3f));
                    break;
                case EnemySkill.Blink:
                    Blink(target);
                    break;
                case EnemySkill.WarCry:
                    WarCry();
                    break;
                case EnemySkill.Summon:
                    Summon();
                    break;
                case EnemySkill.Leap:
                    StartCoroutine(Leap(target, damage * 1.4f));
                    break;
            }
        }

        // Crouches, springs into the air in a fast arc and comes down where the player is heading
        // (a glow shows where from the moment it crouches), hitting everything round the landing.
        // Anyone it passes through low on the way is hit too (once: then the landing doesn't).
        // A stagger while it's still crouched calls the jump off.
        private IEnumerator Leap(Vector3 landing, float damage)
        {
            busyUntil = Time.time + LeapCrouch + LeapAir + 0.3f;
            GetComponentInChildren<CreatureAnimator>()?.Crouch(LeapCrouch);

            bool calledOff = false;
            bool struck = false;
            float radius = LeapRadius(transform);
            StartCoroutine(GroundTelegraph.Run(landing, radius, LeapCrouch + LeapAir, kind.DamageType, center =>
            {
                if (calledOff)
                    return;
                if (!struck)
                    HitIfInside(center, radius, damage);
                SkillEffects.Shockwave(center, radius, DustColor, 0.35f);
            }));

            for (float t = 0f; t < LeapCrouch; t += Time.deltaTime)
            {
                if (health.IsDead || (stagger != null && stagger.IsStaggered))
                {
                    calledOff = true;
                    busyUntil = Time.time;
                    yield break;
                }
                yield return null;
            }

            Vector3 start = transform.position;
            landing.y = start.y;
            Vector3 flat = Flat(landing - start);
            float height = Mathf.Lerp(1.0f, 2.2f, flat.magnitude / LeapMaxDistance) * Mathf.Max(0.6f, transform.localScale.y);
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(flat);
            SkillEffects.Shockwave(start, 1.0f * transform.localScale.x, DustColor, 0.3f);

            float gap = LeapGap(transform);
            for (float t = 0f; t < LeapAir && !health.IsDead; t += Time.deltaTime)
            {
                float f = Mathf.Clamp01(t / LeapAir);
                float rise = height * 4f * f * (1f - f);
                Vector3 want = Vector3.Lerp(start, landing, f) + Vector3.up * rise;
                body.Move(KeepClear(want, gap) - transform.position);

                // In the way: still low enough to bowl into the player rather than sail over them.
                if (!struck && player != null && !player.IsDead && rise < 1.5f &&
                    Flat(player.transform.position - transform.position).magnitude < gap + 0.3f)
                {
                    struck = true;
                    player.TakeHit(damage, kind.DamageType);
                    SkillEffects.Shockwave(player.transform.position, 1.2f, DustColor, 0.3f);
                }
                yield return null;
            }
            if (!health.IsDead)
                body.Move(KeepClear(landing, gap) - transform.position + Vector3.down * 0.2f);
        }

        // Never comes down on top of the player (the colliders would overlap and it would end up
        // standing on their head): wherever the player has moved to, it stays this far off.
        private Vector3 KeepClear(Vector3 want, float gap)
        {
            if (player == null)
                return want;
            Vector3 away = Flat(want - player.transform.position);
            if (away.magnitude >= gap)
                return want;
            Vector3 dir = away.sqrMagnitude > 0.0001f ? away.normalized : Flat(transform.position - player.transform.position).normalized;
            Vector3 p = player.transform.position + dir * gap;
            return new Vector3(p.x, want.y, p.z);
        }

        // The player's ground speed, measured from how they move (their controller's own velocity
        // reads zero), smoothed so a single odd frame doesn't throw the aim.
        private Vector3 playerVelocity;
        private Vector3 lastPlayerPosition;
        private bool trackingPlayer;

        private void TrackPlayer()
        {
            Vector3 at = player.transform.position;
            if (trackingPlayer && Time.deltaTime > 0f)
            {
                Vector3 step = Flat(at - lastPlayerPosition) / Time.deltaTime;
                if (step.magnitude > 30f)
                    step = Vector3.zero; // a teleport, not a run
                playerVelocity = Vector3.Lerp(playerVelocity, step, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            lastPlayerPosition = at;
            trackingPlayer = true;
        }

        // Where the player will be in this many seconds if they keep going the way they are.
        private Vector3 PredictPlayer(float seconds)
        {
            Vector3 at = player.transform.position + playerVelocity * seconds;

            // No further than it can jump.
            Vector3 reach = Flat(at - transform.position);
            if (reach.magnitude > LeapMaxDistance)
                at = transform.position + reach.normalized * LeapMaxDistance;
            at.y = player.transform.position.y;
            return at;
        }

        // The player's collider plus its own, with a little room to spare.
        private static float LeapGap(Transform body) => 0.5f + 0.6f * body.localScale.x + 0.35f;

        // Comes down just short of the player rather than on top of them.
        private static Vector3 LeapLanding(Vector3 from, Vector3 target, float gap)
        {
            Vector3 flat = Flat(target - from);
            if (flat.magnitude > gap)
                target -= flat.normalized * gap;
            return target;
        }

        private void HitIfInside(Vector3 center, float radius, float damage)
        {
            if (this == null || health.IsDead || player == null || player.IsDead)
                return;
            if (Flat(player.transform.position - center).magnitude <= radius)
                player.TakeHit(damage, kind.DamageType);
        }

        // Dashes straight at the player; hits them if it gets there.
        private IEnumerator Charge(float damage)
        {
            busyUntil = Time.time + ChargeMaxSeconds;
            SkillEffects.Shockwave(transform.position, 1.2f, DustColor, 0.3f);
            float until = Time.time + ChargeMaxSeconds;
            while (Time.time < until && !health.IsDead)
            {
                if (stagger != null && stagger.IsStaggered)
                    break;
                Vector3 toPlayer = Flat(player.transform.position - transform.position);
                if (toPlayer.magnitude < 1.6f)
                {
                    player.TakeHit(damage, kind.DamageType);
                    SkillEffects.Shockwave(player.transform.position, 1.4f, DustColor, 0.3f);
                    break;
                }
                transform.rotation = Quaternion.LookRotation(toPlayer);
                body.Move(toPlayer.normalized * ChargeSpeed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
                yield return null;
            }
            busyUntil = Time.time + 0.2f;
        }

        // Reappears a couple of metres to one side of the player, if there's room.
        private void Blink(Vector3 target)
        {
            Vector3 away = Flat(transform.position - target).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? 1f : -1f);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector3 offset = Quaternion.AngleAxis(attempt * 40f, Vector3.up) * (away + side).normalized * 2.6f;
                Vector3 spot = new Vector3(target.x + offset.x, transform.position.y, target.z + offset.z);
                if (!OnlyCharacters(spot + Vector3.up * 0.3f, 0.5f))
                    continue; // scenery in the way

                BlinkPuff(transform.position, kind);
                body.enabled = false;
                transform.position = spot;
                body.enabled = true;
                transform.rotation = Quaternion.LookRotation(Flat(target - spot) + Vector3.forward * 0.001f);
                BlinkPuff(spot, kind);
                return;
            }
        }

        // Nothing there but characters and the floor.
        private static bool OnlyCharacters(Vector3 p, float radius)
        {
            foreach (Collider c in Physics.OverlapSphere(p, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!(c is CharacterController) && !c.gameObject.name.StartsWith("Ground"))
                    return false;
            }
            return true;
        }

        // Heals every enemy close by (itself too) by a quarter of their life.
        private void WarCry()
        {
            SkillEffects.Shockwave(transform.position, WarCryRadius, HealColor, 0.5f);
            foreach (Collider c in Physics.OverlapSphere(transform.position, WarCryRadius))
            {
                EnemyHealth ally = c.GetComponentInParent<EnemyHealth>();
                if (ally != null && !ally.IsDead)
                    ally.Heal(ally.MaxHealth * 0.25f);
            }
        }

        private bool AnyHurtAllyNear()
        {
            foreach (Collider c in Physics.OverlapSphere(transform.position, WarCryRadius))
            {
                EnemyHealth ally = c.GetComponentInParent<EnemyHealth>();
                if (ally != null && !ally.IsDead && ally.CurrentHealth < ally.MaxHealth * 0.8f)
                    return true;
            }
            return false;
        }

        private void Summon()
        {
            EnemySpawner spawner = GetComponentInParent<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null)
                return;

            minions.RemoveAll(m => m == null || m.IsDead);
            int count = Mathf.Min(2, MaxMinions - minions.Count);
            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f + Random.value;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.5f;
                GameObject skeleton = Instantiate(spawner.EnemyPrefab, at, transform.rotation, transform.parent);
                EnemyKinds.Apply(skeleton, EnemyKinds.SkeletonIndex, level);
                minions.Add(skeleton.GetComponent<EnemyHealth>());
                SkillEffects.Shockwave(at, 1.2f, SummonColor, 0.5f);
            }
        }

        // ------------------------------------------------------------------ shared with spectators

        private const float SlamWindUp = 0.9f;
        private const float LeapCrouch = 0.3f;
        private const float LeapAir = 0.42f;
        private const float LeapMaxDistance = 14f;
        private const float StrikeRadius = 2.2f;
        private const float WarCryRadius = 8f;

        private static readonly Color DustColor = new Color(0.62f, 0.52f, 0.40f);
        private static readonly Color HealColor = new Color(0.45f, 1f, 0.45f);
        private static readonly Color SummonColor = new Color(0.4f, 1f, 0.5f);
        private static readonly Color BlinkColor = new Color(0.55f, 1f, 0.8f);

        private static float SlamRadius(EnemyKind kind, Transform body) => 2.6f * body.localScale.y;
        private static float LeapRadius(Transform body) => 1.7f * Mathf.Max(0.8f, body.localScale.y);

        // Lightning comes down faster than fire or ice.
        private static float StrikeWindUp(EnemyKind kind) => kind.DamageType == DamageType.Lightning ? 0.7f : 1.1f;

        private static void BlinkPuff(Vector3 at, EnemyKind kind)
        {
            SkillEffects.Shockwave(at, 1.3f, BlinkColor, 0.4f);
        }

        // Three arrows (or bolts) fanned out at the target.
        private static void Volley(Transform body, EnemyKind kind, Vector3 target, PlayerStats hit, float damage)
        {
            Vector3 from = EnemyCombat.BoltOrigin(body);
            for (int k = -1; k <= 1; k++)
            {
                Vector3 aim = from + Quaternion.AngleAxis(k * 14f, Vector3.up) * (target - from);
                if (hit != null)
                    EnemyProjectile.LaunchAt(from, aim, hit, kind, damage);
                else
                    EnemyProjectile.LaunchVisual(from, aim, kind);
            }
        }

        /// <summary>
        /// Spectator replica: the look of a skill an enemy just used, with no damage. Movement
        /// (charges, blinks) arrives with the replicated pose; this adds the flashes and patches.
        /// </summary>
        public static void PlayVisual(MonoBehaviour host, EnemyKind kind, Transform body, Vector3 target)
        {
            switch (kind.Skill)
            {
                case EnemySkill.Slam:
                    host.StartCoroutine(GroundTelegraph.Run(body.position, SlamRadius(kind, body), SlamWindUp, kind.DamageType, null));
                    break;
                case EnemySkill.Strike:
                    host.StartCoroutine(GroundTelegraph.Run(target, StrikeRadius, StrikeWindUp(kind), kind.DamageType, null));
                    break;
                case EnemySkill.Volley:
                    Volley(body, kind, target, null, 0f);
                    break;
                case EnemySkill.Charge:
                    SkillEffects.Shockwave(body.position, 1.2f, DustColor, 0.3f);
                    break;
                case EnemySkill.Blink:
                    BlinkPuff(body.position, kind);
                    break;
                case EnemySkill.WarCry:
                    SkillEffects.Shockwave(body.position, WarCryRadius, HealColor, 0.5f);
                    break;
                case EnemySkill.Summon:
                    SkillEffects.Shockwave(body.position, 2.5f, SummonColor, 0.5f);
                    break;
                case EnemySkill.Leap:
                    float leapRadius = LeapRadius(body);
                    host.StartCoroutine(GroundTelegraph.Run(target, leapRadius, LeapCrouch + LeapAir, kind.DamageType,
                        center => SkillEffects.Shockwave(center, leapRadius, DustColor, 0.35f)));
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
