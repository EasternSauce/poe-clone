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

            if (player.IsDead || Time.time < nextUse || Busy || Sanctuary.Contains(player.transform.position, 1f))
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
                case EnemySkill.ThornGarden: return distance < 12f;
                case EnemySkill.Wail: return distance > WailInnerRadius && distance < WailOuterRadius + 1f;
                case EnemySkill.FrostFissures: return distance < 11f;
                case EnemySkill.LastOffering: return distance < 10f;
                case EnemySkill.DraggingBreath: return distance > 2f && distance < BreathRange;
                case EnemySkill.Graveward: return distance < 14f;
                default: return false;
            }
        }

        private void Use(float distance)
        {
            EnemySounds.Play(kind, EnemySounds.Event.Attack, transform.position);
            Vector3 target = player.transform.position;
            Vector3 facing = Flat(target - transform.position);
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(facing);

            UseCount++;
            // Short attacks can lead the player; longer warnings mark their position at launch.
            bool enraged = controller != null && controller.IsEnraged;
            if (enraged && kind.Skill == EnemySkill.Strike && StrikeWindUp(kind) <= 0.7f)
                target = PlayerMotion.Predict(player, StrikeWindUp(kind));
            else if (enraged && kind.Skill == EnemySkill.Volley)
                target = PlayerMotion.Intercept(player, EnemyCombat.BoltOrigin(transform), kind.ProjectileSpeed);
            if (kind.Skill == EnemySkill.Leap)
                target = LeapLanding(transform.position, target, LeapGap(transform));
            LastTarget = kind.Skill == EnemySkill.Slam || kind.Skill == EnemySkill.Wail ? transform.position : target;
            float damage = kind.Damage * EnemyKinds.DamageScale(level, kind) * (controller != null ? controller.DamageMultiplier : 1f);

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
                case EnemySkill.ThornGarden:
                    busyUntil = Time.time + RootWindUp;
                    GetComponentInChildren<CreatureAnimator>()?.Crouch(RootWindUp);
                    StartCoroutine(ThornGarden(this, transform.position, target, kind,
                        () => CanSpecialHit(), center => HitIfInside(center, RootRadius, damage * 0.35f)));
                    break;
                case EnemySkill.Wail:
                    busyUntil = Time.time + WailWindUp;
                    GetComponentInChildren<CreatureAnimator>()?.Crouch(WailWindUp);
                    StartCoroutine(GroundTelegraph.RunRing(LastTarget, WailInnerRadius, WailOuterRadius, WailWindUp, kind.DamageType, center =>
                    {
                        if (!CanSpecialHit()) return;
                        float range = Flat(player.transform.position - center).magnitude;
                        if (range >= WailInnerRadius && range <= WailOuterRadius)
                            player.TakeHit(damage * 1.3f, kind.DamageType, attack: false);
                    }));
                    break;
                case EnemySkill.FrostFissures:
                    busyUntil = Time.time + FissureWindUp;
                    GetComponentInChildren<CreatureAnimator>()?.Crouch(FissureWindUp);
                    Vector3 fissureFrom = transform.position;
                    FrostFissures(this, fissureFrom, target, kind, () =>
                    {
                        if (!CanSpecialHit()) return;
                        if (InsideFissures(fissureFrom, target, player.transform.position))
                            player.TakeHit(damage * 1.2f, kind.DamageType, attack: false);
                    });
                    break;
                case EnemySkill.LastOffering:
                    busyUntil = Time.time + OfferingWindUp;
                    GetComponentInChildren<CreatureAnimator>()?.Crouch(OfferingWindUp);
                    int offeringStagger = stagger != null ? stagger.TriggerCount : 0;
                    StartCoroutine(Offering(target, damage * 1.4f, offeringStagger));
                    break;
                case EnemySkill.DraggingBreath:
                    StartCoroutine(DraggingBreath(target, damage * 1.4f));
                    break;
                case EnemySkill.Graveward:
                    StartCoroutine(RaiseGraveward(target));
                    break;
            }
        }

        private const float OfferingWindUp = 0.55f;
        private const float BreathRange = 22.5f;
        private const float BreathHalfAngle = 24f;
        private const float BreathWindUp = 0.65f;
        private const float BreathChannel = 1.2f;
        private const float BreathDamageInterval = 0.5f;
        private const float WardWindUp = 0.65f;

        private bool CastAlive(int staggerCount) => this != null && health != null && !health.IsDead &&
            (stagger == null || stagger.TriggerCount == staggerCount);

        private IEnumerator Offering(Vector3 target, float damage, int staggerCount)
        {
            yield return new WaitForSeconds(OfferingWindUp);
            if (!CastAlive(staggerCount)) yield break;
            SkillEffects.Arc(transform.position + Vector3.up * 0.15f, target + Vector3.up * 0.3f, kind.Eyes, 0.25f);
            EnemyOffering.Place(target, kind, damage, player);
        }

        private IEnumerator RaiseGraveward(Vector3 target)
        {
            int staggerCount = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + WardWindUp;
            GetComponentInChildren<CreatureAnimator>()?.Crouch(WardWindUp);
            yield return new WaitForSeconds(WardWindUp);
            if (!CastAlive(staggerCount)) yield break;
            // The guardian holds its ground while the ward is up; flanking stays predictable.
            busyUntil = Time.time + 5f;
            Graveward.Raise(transform, Flat(target - transform.position), kind.Eyes, gameplay: true);
        }

        private static bool InsideBreath(Vector3 origin, Vector3 facing, Vector3 target)
        {
            Vector3 offset = Flat(target - origin);
            return offset.sqrMagnitude <= BreathRange * BreathRange &&
                Vector3.Dot(offset.normalized, facing) >= Mathf.Cos(BreathHalfAngle * Mathf.Deg2Rad);
        }

        private IEnumerator DraggingBreath(Vector3 target, float damage)
        {
            Vector3 origin = transform.position;
            Vector3 facing = FissureDirection(origin, target);
            int staggerCount = stagger != null ? stagger.TriggerCount : 0;
            float duration = BreathWindUp + BreathChannel;
            busyUntil = Time.time + duration;
            GetComponentInChildren<CreatureAnimator>()?.Crouch(duration);
            StartCoroutine(GroundTelegraph.RunCone(origin, facing, BreathRange, BreathHalfAngle, duration,
                () => CastAlive(staggerCount)));
            float exposure = 0f;
            try
            {
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    if (!CastAlive(staggerCount)) yield break;
                    if (t >= BreathWindUp && CanSpecialHit() && InsideBreath(origin, facing, player.transform.position))
                    {
                        exposure += Time.deltaTime;
                        if (exposure >= BreathDamageInterval)
                        {
                            exposure -= BreathDamageInterval;
                            player.TakeHit(damage, kind.DamageType, attack: false);
                            if (player.IsDead) yield break;
                        }
                        CharacterController playerBody = player.GetComponent<CharacterController>();
                        PlayerController motion = player.GetComponent<PlayerController>();
                        if (playerBody != null && playerBody.enabled && (motion == null || !motion.IsDashing))
                        {
                            Vector3 offset = Flat(origin - player.transform.position);
                            float step = Mathf.Min(5.4f * Time.deltaTime, Mathf.Max(0f, offset.magnitude - 1.8f));
                            Vector3 destination = player.transform.position + offset.normalized * step;
                            destination = World.GroundObstacleMotion.Clamp(playerBody, player.transform.position, destination);
                            playerBody.Move(destination - player.transform.position);
                        }
                    }
                    else
                    {
                        exposure = 0f;
                    }
                    yield return null;
                }
                if (CanSpecialHit() && InsideBreath(origin, facing, player.transform.position) &&
                    Flat(player.transform.position - origin).magnitude < 2.6f)
                {
                    player.TakeHit(damage, kind.DamageType);
                    SkillEffects.Shockwave(origin + facing, 1.5f, DustColor, 0.3f);
                }
            }
            finally { busyUntil = Time.time; }
        }

        private static IEnumerator OfferingVisual(Vector3 target, EnemyKind kind)
        {
            yield return new WaitForSeconds(OfferingWindUp);
            EnemyOffering.Place(target, kind, 0f, null);
        }

        private static IEnumerator WardVisual(Transform caster, Vector3 target, EnemyKind kind)
        {
            yield return new WaitForSeconds(WardWindUp);
            if (caster != null) Graveward.Raise(caster, Flat(target - caster.position), kind.Eyes, gameplay: false);
        }

        private const float RootRadius = 3.125f;
        private const float RootWindUp = 1f;
        private const float WailInnerRadius = 7.5f;
        private const float WailOuterRadius = 18.75f;
        private const float WailWindUp = 1.25f;
        private const float FissureWindUp = 1.1f;
        private const float FissureHalfLength = 12.5f;
        private const float FissureWidth = 3.75f;

        private bool CanSpecialHit() => this != null && health != null && !health.IsDead &&
            player != null && !player.IsDead && !Sanctuary.Contains(player.transform.position, 1f) &&
            (stagger == null || !stagger.IsStaggered);

        // Three fixed patches grow after a warning, then persist for four seconds. Standing at
        // a seam never takes multiple patch hits on the same tick. Replicas run visuals only.
        private static IEnumerator ThornGarden(MonoBehaviour host, Vector3 from, Vector3 target, EnemyKind kind,
            System.Func<bool> active, System.Action<Vector3> hit)
        {
            Vector3 direction = Flat(target - from).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var centers = new Vector3[4];
            for (int i = 0; i < centers.Length; i++)
            {
                centers[i] = target + direction * ((i - 1.5f) * 6.25f);
                host.StartCoroutine(GroundTelegraph.Run(centers[i], RootRadius, RootWindUp, DamageType.Physical, null));
            }
            yield return new WaitForSeconds(RootWindUp);
            if (active != null && !active()) yield break;
            var roots = new GameObject("BriarboundThornPatches");
            Object.Destroy(roots, 4.1f); // Also cleans up if the host is destroyed mid-coroutine.
            foreach (Vector3 center in centers)
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2f / 7f;
                    GameObject thorn = RuntimePrimitives.Create(PrimitiveType.Capsule, roots.transform, kind.Skin);
                    thorn.transform.position = new Vector3(center.x + Mathf.Sin(a) * 2f, 0.35f, center.z + Mathf.Cos(a) * 2f);
                    thorn.transform.localScale = new Vector3(0.3f, 0.45f, 0.3f);
                    thorn.transform.rotation = Quaternion.Euler(Mathf.Cos(a) * 28f, 0f, Mathf.Sin(a) * 28f);
                }
            }
            for (int tick = 0; tick < 8; tick++)
            {
                if (active != null && !active()) break;
                foreach (Vector3 center in centers)
                {
                    SkillEffects.Shockwave(center, RootRadius, kind.Pants, 0.5f);
                    hit?.Invoke(center);
                }
                yield return new WaitForSeconds(0.5f);
            }
            Object.Destroy(roots);
        }

        private static Vector3 FissureDirection(Vector3 from, Vector3 target)
        {
            Vector3 direction = Flat(target - from);
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private static bool InsideFissures(Vector3 from, Vector3 target, Vector3 position)
        {
            Vector3 axis = FissureDirection(from, target);
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            Vector3 offset = Flat(position - target);
            float along = Mathf.Abs(Vector3.Dot(offset, axis)), across = Mathf.Abs(Vector3.Dot(offset, side));
            return (along <= FissureHalfLength && across <= FissureWidth * 0.5f) ||
                   (across <= FissureHalfLength && along <= FissureWidth * 0.5f);
        }

        private static void FrostFissures(MonoBehaviour host, Vector3 from, Vector3 target, EnemyKind kind, System.Action hit)
        {
            Vector3 axis = FissureDirection(from, target);
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            host.StartCoroutine(GroundTelegraph.RunLine(target - axis * FissureHalfLength, axis,
                FissureHalfLength * 2f, FissureWidth, FissureWindUp, kind.DamageType, () =>
                {
                    // One resolution for both beams prevents double damage at their intersection.
                    hit?.Invoke();
                    SkillEffects.Arc(target - axis * FissureHalfLength + Vector3.up * 0.25f,
                        target + axis * FissureHalfLength + Vector3.up * 0.25f, kind.Eyes, 0.4f);
                    SkillEffects.Arc(target - side * FissureHalfLength + Vector3.up * 0.25f,
                        target + side * FissureHalfLength + Vector3.up * 0.25f, kind.Eyes, 0.4f);
                }));
            host.StartCoroutine(GroundTelegraph.RunLine(target - side * FissureHalfLength, side,
                FissureHalfLength * 2f, FissureWidth, FissureWindUp, kind.DamageType, null));
        }

        // Crouches, springs into the air in a fast arc and comes down near the player's launch position
        // (a glow shows where from the moment it crouches), hitting everything round the landing.
        // Anyone it passes through low on the way is hit too (once: then the landing doesn't).
        // A stagger while it's still crouched calls the jump off.
        private IEnumerator Leap(Vector3 landing, float damage)
        {
            landing = World.GroundObstacleMotion.Clamp(body, transform.position, KeepClear(landing, LeapGap(transform)));
            LastTarget = landing;
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
                // Keep collision checks on the ground plane; the arc cannot clear scenery.
                Vector3 grounded = new Vector3(transform.position.x, start.y, transform.position.z);
                Vector3 clear = World.GroundObstacleMotion.Clamp(body, grounded, KeepClear(want, gap));
                body.Move(clear - transform.position);

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
            {
                Vector3 grounded = new Vector3(transform.position.x, start.y, transform.position.z);
                Vector3 clear = World.GroundObstacleMotion.Clamp(body, grounded, KeepClear(landing, gap));
                body.Move(clear - transform.position + Vector3.down * 0.2f);
            }
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
                player.TakeHit(damage, kind.DamageType, attack: kind.Skill != EnemySkill.Strike);
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
                player.TakeHit(damage, kind.DamageType, attack: kind.Skill != EnemySkill.Strike);
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
            var volleyHit = new bool[1]; // the player is hurt by one arrow of the three at most
            for (int k = -1; k <= 1; k++)
            {
                Vector3 aim = from + Quaternion.AngleAxis(k * 14f, Vector3.up) * (target - from);
                if (hit != null)
                    EnemyProjectile.LaunchAt(from, aim, hit, kind, damage, volleyHit);
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
                case EnemySkill.LastOffering:
                    host.StartCoroutine(OfferingVisual(target, kind));
                    break;
                case EnemySkill.DraggingBreath:
                    host.StartCoroutine(GroundTelegraph.RunCone(body.position, FissureDirection(body.position, target),
                        BreathRange, BreathHalfAngle, BreathWindUp + BreathChannel));
                    break;
                case EnemySkill.Graveward:
                    host.StartCoroutine(WardVisual(body, target, kind));
                    break;
                case EnemySkill.ThornGarden:
                    host.StartCoroutine(ThornGarden(host, body.position, target, kind, null, null));
                    break;
                case EnemySkill.Wail:
                    host.StartCoroutine(GroundTelegraph.RunRing(target, WailInnerRadius, WailOuterRadius, WailWindUp, kind.DamageType, null));
                    break;
                case EnemySkill.FrostFissures:
                    FrostFissures(host, body.position, target, kind, null);
                    break;
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
