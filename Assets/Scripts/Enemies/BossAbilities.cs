using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;
using Random = UnityEngine.Random;

namespace PoeClone.Enemies
{
    /// <summary>
    /// What a field boss does besides its plain blows: a set of big, readable moves of its own,
    /// one at a time while the player is close, each telegraphed before it lands. Every boss has a
    /// body of its own (<see cref="CreatureBuilder"/>) and its own moves, in its own file:
    ///
    /// - Gravelord Mortis (BossAbilities.Mortis): a gravedigger; burrows, opens graves, throws his coffin.
    /// - The Ashen Warlord (.Warlord): an empty suit of burning armour; drags fire, tosses embers, its plates fly.
    /// - Rimeheart (.Rimeheart): a floating heart of ice; pulses rings of frost, spirals shards, raises mirrors.
    /// - Bramblesow (.Bramblesow): a giant boar with a thicket on its back; charges, bursts roots, shakes thorns.
    /// - Vex, the Tunnel King (.Vex): a small, quick goblin king; smoke and backstabs, tripwires, spores.
    /// - The Bell-Ringer (.BellRinger): carries a cracked bell; tolls rings of sound, drops the bell on you.
    /// - The Sunforged Idol (.SunIdol): a statue; sweeps sunbeams, slams stone hands, sinks and rises elsewhere.
    /// - Hrimgar the Huntress (.Hrimgar): throws spears and nets, pounces, hunts with frost wolves.
    ///
    /// At half life each boss roars into a second phase: something of its own, and its moves come
    /// faster. While a move plays the boss is <see cref="Busy"/> (its controller and plain attacks
    /// wait). Added by <see cref="World.BossLair"/> when it places the boss.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public partial class BossAbilities : MonoBehaviour
    {
        // One of the boss's moves: when it fits, and what it does.
        private sealed class BossMove
        {
            public string Name;
            public float MinRange;
            public float MaxRange = 99f;
            public bool SecondPhaseOnly;
            public Func<bool> Allowed;
            public Func<IEnumerator> Run;
        }

        private static readonly Color Dust = new Color(0.72f, 0.64f, 0.5f, 1f);

        private EnemyHealth health;
        private EnemyKind kind;
        private int level;
        private GameObject minionPrefab;
        private PlayerStats player;
        private PlayerController playerMotion;
        private CharacterController body;
        private CreatureAnimator anim;
        private EnemyController controller;

        // Set up per boss (SetUp* in each boss's file).
        private readonly List<BossMove> moves = new List<BossMove>();
        private Func<IEnumerator> closer;           // how it gets to a player out of reach
        private float closerRange = 6f;              // ...beyond this distance
        private float closerEvery = 3.5f;
        private Func<IEnumerator> enrage;            // its half-life roar's own effect
        private float engageRange = 18f;
        private float moveEvery = 3.0f;
        private float moveEveryEnraged = 2.0f;
        private int calmMaxMinions = 2;
        private int enragedMaxMinions = 3;

        private float nextMove;
        private float nextCloser;
        private bool busy;
        private bool secondPhase;
        private string last;
        private readonly List<EnemyHealth> minions = new List<EnemyHealth>();
        // Traps, orbiting plates, mirrors: gone with the boss.
        private readonly List<GameObject> leftovers = new List<GameObject>();

        // The boss's tempo (EnemyKind.Tempo): every wind-up, flight and pause is divided by it.
        private float T => kind != null ? Mathf.Max(0.1f, kind.Tempo * (controller != null ? controller.AttackSpeedMultiplier : 1f)) : 1f;

        /// <summary>A move is playing: the boss's controller and its plain attacks wait.</summary>
        public bool Busy => busy || (shepherd != null && shepherd.IsPlaying);

        // The act boss's own animator: its clips count as moves (see ShepherdFight).
        private ShepherdAnimator shepherd;

        /// <summary>Past half life: roared, and moving faster.</summary>
        public bool InSecondPhase => secondPhase;

        public void Configure(EnemyKind bossKind, int monsterLevel, GameObject enemyPrefab)
        {
            kind = bossKind;
            level = monsterLevel;
            minionPrefab = enemyPrefab;
            if (kind.Boss == BossStyle.Shepherd && GetComponent<ShepherdFight>() == null)
                gameObject.AddComponent<ShepherdFight>().Configure(kind, level);

            moves.Clear();
            switch (kind.Boss)
            {
                case BossStyle.Gravelord: SetUpMortis(); break;
                case BossStyle.Warlord: SetUpWarlord(); break;
                case BossStyle.FrostQueen: SetUpRimeheart(); break;
                case BossStyle.Bramblesow: SetUpBramblesow(); break;
                case BossStyle.TunnelKing: SetUpVex(); break;
                case BossStyle.BellRinger: SetUpBellRinger(); break;
                case BossStyle.SunIdol: SetUpSunIdol(); break;
                case BossStyle.Huntress: SetUpHrimgar(); break;
            }
        }

        private void Add(string name, Func<IEnumerator> run, float minRange = 0f, float maxRange = 99f, bool secondPhaseOnly = false, Func<bool> allowed = null)
        {
            moves.Add(new BossMove { Name = name, Run = run, MinRange = minRange, MaxRange = maxRange, SecondPhaseOnly = secondPhaseOnly, Allowed = allowed });
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            controller = GetComponent<EnemyController>();
            health.Died += OnBossDied;
            body = GetComponent<CharacterController>();
        }

        private void Start()
        {
            anim = GetComponentInChildren<CreatureAnimator>();
            shepherd = GetComponentInChildren<ShepherdAnimator>();
            OnStarted();
        }

        // Per-boss start: see each boss's file.
        private void OnStarted()
        {
            if (kind == null)
                return;
            if (kind.Boss == BossStyle.Huntress)
                nextPack = Time.time + 1f;
        }

        // The raised dead and the brood fall with their master; its traps go too.
        private void OnBossDied()
        {
            busy = false;
            StopAllCoroutines();
            health.Immune = false;
            SetVisible(true);
            if (anim != null)
            {
                anim.Sunk = 0f;
                anim.EndAct();
            }
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    minion.TakeDamage(minion.CurrentHealth + 1f);
            }
            minions.Clear();
            ClearLeftovers();
        }

        private void OnEnable()
        {
            // The first move comes a little after the fight starts, not on the first frame.
            nextMove = Time.time + 3f / T;
            nextCloser = Time.time + 2f / T;
        }

        private void OnDestroy()
        {
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    Destroy(minion.gameObject);
            }
            ClearLeftovers();
        }

        private void ClearLeftovers()
        {
            foreach (GameObject go in leftovers)
            {
                if (go != null)
                    Destroy(go);
            }
            leftovers.Clear();
        }

        private void Update()
        {
            if (kind == null || health.IsDead)
                return;
            // The act boss fights its own fight (ShepherdFight), not the field bosses' moves.
            if (kind.Boss == BossStyle.Shepherd)
                return;

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerStats>();
                if (player == null)
                    return;
                playerMotion = player.GetComponent<PlayerController>();
            }

            Tick();
            if (busy || Scripted)
                return;

            float distance = Flat(player.transform.position - transform.position).magnitude;
            if (player.IsDead || distance > engageRange)
            {
                // Out of the fight: the timer waits.
                nextMove = Mathf.Max(nextMove, Time.time + 2f / T);
                nextCloser = Mathf.Max(nextCloser, Time.time + 1f / T);
                return;
            }

            if (!secondPhase && health.CurrentHealth <= health.MaxHealth * 0.5f)
            {
                secondPhase = true;
                StartCoroutine(RunBusy(Roar()));
                return;
            }

            // Its plain blow finishes first.
            CharacterAttackAnimator swing = anim != null ? anim.GetComponent<CharacterAttackAnimator>() : null;
            if (swing != null && swing.IsAttacking)
                return;

            // Out of reach: it closes in its own way, on its own timer.
            if (closer != null && distance > closerRange && Time.time >= nextCloser)
            {
                nextCloser = Time.time + closerEvery / T;
                nextMove = Mathf.Max(nextMove, Time.time + 1.2f / T);
                StartCoroutine(RunBusy(closer()));
                return;
            }

            if (Time.time < nextMove)
                return;

            BossMove move = PickMove(distance);
            if (move == null)
                return;
            last = move.Name;
            nextMove = Time.time + (secondPhase ? moveEveryEnraged : moveEvery) / T;
            EnemySounds.Play(kind, EnemySounds.Event.Attack, transform.position);
            StartCoroutine(RunBusy(move.Run()));
        }

        /// <summary>For a showcase: picks no moves of its own (and no half-life roar); only <see cref="Play"/> runs them.</summary>
        public bool Scripted { get; set; }

        /// <summary>Every move it has, in an order that shows them all: its own moves, closing in, the roar, then the moves only its second phase uses.</summary>
        public List<string> ShowcaseOrder()
        {
            var order = new List<string>();
            foreach (BossMove m in moves)
                if (!m.SecondPhaseOnly)
                    order.Add(m.Name);
            if (closer != null)
                order.Add("closer");
            order.Add("roar");
            foreach (BossMove m in moves)
                if (m.SecondPhaseOnly)
                    order.Add(m.Name);
            return order;
        }

        /// <summary>
        /// Plays one of its moves now, by name ("closer" for its way of closing in, "roar" for the
        /// half-life roar), for demos and tests. Returns what happened.
        /// </summary>
        public string Play(string moveName)
        {
            if (kind == null || health.IsDead)
                return "dead or not set up";
            if (busy)
                return "busy";
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerStats>();
                playerMotion = player != null ? player.GetComponent<PlayerController>() : null;
                if (player == null)
                    return "no player";
            }
            if (moveName == "closer" && closer != null)
            {
                StartCoroutine(RunBusy(closer()));
                return "closing in";
            }
            if (moveName == "roar")
            {
                secondPhase = true;
                StartCoroutine(RunBusy(Roar()));
                return "roaring";
            }
            foreach (BossMove m in moves)
            {
                if (m.Name != moveName)
                    continue;
                last = m.Name;
                nextMove = Time.time + moveEvery / T;
                StartCoroutine(RunBusy(m.Run()));
                return "playing " + m.Name;
            }
            var names = new List<string>();
            foreach (BossMove m in moves)
                names.Add(m.Name);
            return "no move " + moveName + " (has " + string.Join(", ", names) + ", closer, roar)";
        }

        // Things that run all fight long (orbiting plates, armed traps, the pack): each boss's own.
        private void Tick()
        {
            switch (kind.Boss)
            {
                case BossStyle.Warlord: TickWarlord(); break;
                case BossStyle.TunnelKing: TickVex(); break;
                case BossStyle.Huntress: TickHrimgar(); break;
            }
        }

        // Runs a move, the boss busy for as long as it plays.
        private IEnumerator RunBusy(IEnumerator routine)
        {
            busy = true;
            while (routine != null)
            {
                bool more;
                try
                {
                    more = routine.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    more = false;
                }
                if (!more || health.IsDead)
                    break;
                yield return routine.Current;
            }
            if (anim != null)
                anim.EndAct();
            busy = false;
        }

        // The boss's moves in turn, skipping the one just used and any that make no sense from here.
        private BossMove PickMove(float distance)
        {
            var fitting = new List<BossMove>();
            BossMove fallback = null;
            foreach (BossMove m in moves)
            {
                if (m.SecondPhaseOnly && !secondPhase)
                    continue;
                if (m.Allowed != null && !m.Allowed())
                    continue;
                if (distance < m.MinRange || distance > m.MaxRange)
                    continue;
                fallback = fallback ?? m;
                if (m.Name == last && moves.Count > 1)
                    continue;
                fitting.Add(m);
            }
            if (fitting.Count == 0)
                return fallback;
            return fitting[Random.Range(0, fitting.Count)];
        }

        // Half life: it stops, roars, and the fight changes gear.
        private IEnumerator Roar()
        {
            CombatText.Show(transform.position + Vector3.up * health.BarHeight * transform.localScale.y,
                kind.Name + " grows furious!", CombatText.ColorFor(kind.DamageType), 1.4f);
            EnemySounds.Play(kind, EnemySounds.Event.Aggro, transform.position);
            CameraSystem.CameraFollow.Shake(0.3f, 0.9f);
            Act(CreatureAnimator.BossAct.Roar, 1.2f / T);

            float roar = 1.2f / T;
            for (float t = 0f; t < roar; t += Time.deltaTime)
            {
                if (Mathf.Repeat(t, 0.3f) < Time.deltaTime)
                    SkillEffects.Shockwave(transform.position, 2f + 4f * t, GroundTelegraph.FillColor(kind.DamageType), 0.35f);
                yield return null;
            }
            if (enrage != null)
                yield return enrage();
            nextMove = Time.time + 1.2f / T;
        }

        // ------------------------------------------------------------------ shared pieces

        private Vector3 PlayerAt => player != null ? Flat(player.transform.position) + Vector3.up * transform.position.y : transform.position;

        private Vector3 ToPlayer()
        {
            Vector3 to = Flat(player.transform.position - transform.position);
            return to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
        }

        private float Size => transform.localScale.x;

        private void Act(CreatureAnimator.BossAct act, float seconds)
        {
            if (anim != null)
                anim.Perform(act, seconds);
        }

        // Gathers itself, springs into the air, and comes down where the player stood at launch.
        // The landing glows on the ground from the moment it crouches.
        private IEnumerator LeapAt(Vector3 landing, float radius, float damage, DamageType type, float gather, float air, Action<Vector3> landed = null,
            float height = -1f)
        {
            gather /= T;
            air /= T;
            float gap = (body != null ? body.radius * Size : 1f) + 0.6f;
            Vector3 start = transform.position;
            Vector3 jump = Flat(landing - start);
            if (jump.magnitude > 18f)
                landing = start + jump.normalized * 18f;
            landing.y = start.y;
            landing = World.GroundObstacleMotion.Clamp(body, start, KeepClear(landing, gap));
            Face(landing - start);

            StartCoroutine(GroundTelegraph.Run(landing, radius, gather + air, type, at =>
            {
                if (this == null || health.IsDead)
                    return;
                HitIfInside(at, radius, damage, type);
                SkillEffects.Shockwave(at, radius, Dust, 0.45f);
                CameraSystem.CameraFollow.Shake(0.3f, 0.4f);
                landed?.Invoke(at);
            }));

            if (anim != null)
                anim.Crouch(gather);
            yield return new WaitForSeconds(gather);

            SkillEffects.Shockwave(start, 1.2f * Size, Dust, 0.3f);
            if (height < 0f)
                height = 3.2f + 0.8f * Size;
            Vector3 from = transform.position;
            for (float t = 0f; t < air; t += Time.deltaTime)
            {
                float f = Mathf.Clamp01(t / air);
                Vector3 want = Vector3.Lerp(from, landing, f) + Vector3.up * height * 4f * f * (1f - f);
                Vector3 grounded = new Vector3(transform.position.x, start.y, transform.position.z);
                MoveTo(World.GroundObstacleMotion.Clamp(body, grounded, KeepClear(want, gap)));
                yield return null;
            }
            Vector3 groundPosition = new Vector3(transform.position.x, start.y, transform.position.z);
            MoveTo(World.GroundObstacleMotion.Clamp(body, groundPosition, KeepClear(landing, gap)) + Vector3.down * 0.2f);
            yield return Pause(0.3f);
        }

        // A glowing patch that fills in over the wind-up, then hurts the player if they're still on
        // it. Patches sharing a <paramref name="hitOnce"/> flag hurt at most once between them.
        private IEnumerator Eruption(Vector3 center, float radius, float windUp, DamageType type, float damageMultiplier, bool[] hitOnce = null,
            Action<Vector3> burst = null)
        {
            return GroundTelegraph.Run(center, radius, windUp, type, at =>
            {
                if (this == null || health.IsDead)
                    return;
                if (hitOnce == null || !hitOnce[0])
                {
                    if (HitIfInside(at, radius, damageMultiplier, type) && hitOnce != null)
                        hitOnce[0] = true;
                }
                burst?.Invoke(at);
            }, kind);
        }

        // Eruptions along a line from a point towards a direction, one after another (a crack in the ground).
        private void EruptionLine(Vector3 origin, Vector3 dir, int count, float step, float radius, float windUp, float delayStep, DamageType type,
            float damage, Action<Vector3> burst = null)
        {
            var once = new bool[1];
            for (int k = 0; k < count; k++)
                StartCoroutine(Eruption(origin + dir * step * k, radius, windUp + k * delayStep / T, type, damage, once, burst));
        }

        // Eruptions in a ring round a point, all at once after the wind-up.
        private void Ring(Vector3 center, float ringRadius, int count, float patchRadius, float windUp, DamageType type, float damage = 0.9f)
        {
            var once = new bool[1];
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                Vector3 at = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius;
                StartCoroutine(Eruption(at, patchRadius, windUp, type, damage, once));
            }
        }

        /// <summary>
        /// A wave going out from a point (a toll, a pulse of frost): a band of the given thickness
        /// that grows to the max radius, with gaps (angles in degrees, each this wide) to slip
        /// through. It hurts once whoever it passes over outside a gap.
        /// </summary>
        private IEnumerator Wave(Vector3 center, float maxRadius, float speed, float thickness, float[] gapAngles, float gapWidth,
            DamageType type, float damage, Color color, Action<PlayerStats> onHit = null)
        {
            var root = new GameObject("BossWave");
            leftovers.Add(root);
            root.transform.position = new Vector3(center.x, Debris.GroundBelow(center + Vector3.up) + 0.1f, center.z);
            const int pieces = 48;
            var segments = new List<Transform>();
            var angles = new List<float>();
            for (int i = 0; i < pieces; i++)
            {
                float a = i * 360f / pieces;
                if (InGap(a, gapAngles, gapWidth))
                    continue;
                GameObject seg = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, i % 2 == 0 ? color : Color.Lerp(color, Color.white, 0.35f));
                segments.Add(seg.transform);
                angles.Add(a);
            }

            bool hit = false;
            for (float r = 0.8f; r < maxRadius; r += speed * Time.deltaTime)
            {
                if (this == null)
                    yield break;
                float chord = 2f * Mathf.PI * r / pieces * 1.05f;
                for (int i = 0; i < segments.Count; i++)
                {
                    float rad = angles[i] * Mathf.Deg2Rad;
                    Transform s = segments[i];
                    s.localPosition = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * r + Vector3.up * 0.25f;
                    s.localRotation = Quaternion.Euler(0f, angles[i], 0f);
                    s.localScale = new Vector3(chord, 0.5f + 0.25f * Mathf.Sin(r * 2f + i), thickness * 0.6f);
                }

                if (!hit && player != null && !player.IsDead && !health.IsDead)
                {
                    Vector3 off = Flat(player.transform.position - center);
                    float d = off.magnitude;
                    float a = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(d - r) < thickness * 0.5f + 0.3f && !InGap(a, gapAngles, gapWidth))
                    {
                        hit = true;
                        if (player.TakeHit(DamageOf(damage), type, attack: false))
                            onHit?.Invoke(player);
                    }
                }
                yield return null;
            }
            Destroy(root);
        }

        private static bool InGap(float angle, float[] gaps, float width)
        {
            if (gaps == null)
                return false;
            foreach (float g in gaps)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(angle, g)) < width * 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Throws a thing (a coffin, an ember, a net) in an arc onto a spot marked on the ground;
        /// <paramref name="landed"/> gets the spot. The thing is destroyed on landing unless kept.
        /// </summary>
        private IEnumerator Lob(GameObject thing, Vector3 from, Vector3 to, float flight, float radius, DamageType type, Action<Vector3> landed,
            bool keep = false, float spin = 360f, float hover = 0f)
        {
            to.y = Debris.GroundBelow(to + Vector3.up) + hover;
            leftovers.Add(thing);
            StartCoroutine(GroundTelegraph.Run(to, radius, flight, type, null));
            float arc = Mathf.Max(2f, Flat(to - from).magnitude * 0.35f);
            Quaternion rest = thing.transform.rotation;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                if (thing == null)
                    yield break;
                float f = t / flight;
                thing.transform.position = Vector3.Lerp(from, to, f) + Vector3.up * arc * 4f * f * (1f - f);
                thing.transform.rotation = rest * Quaternion.Euler(spin * f, 0f, 0f);
                yield return null;
            }
            if (thing != null)
            {
                thing.transform.position = to;
                thing.transform.rotation = rest;
            }
            if (this != null && !health.IsDead)
                landed?.Invoke(to);
            if (!keep && thing != null)
                Destroy(thing);
        }

        /// <summary>
        /// A thing flying straight (a dagger, a spear, a thorn, a shard): hits the player once if it
        /// passes within the radius, and is gone at the end of its range.
        /// </summary>
        private IEnumerator Missile(GameObject thing, Vector3 from, Vector3 direction, float speed, float range, float hitRadius,
            DamageType type, float damage, Action<PlayerStats> onHit = null, bool spin = false)
        {
            leftovers.Add(thing);
            direction = Flat(direction).normalized;
            thing.transform.position = from;
            thing.transform.rotation = Quaternion.LookRotation(direction);
            for (float travelled = 0f; travelled < range; travelled += speed * Time.deltaTime)
            {
                if (thing == null)
                    yield break;
                thing.transform.position += direction * speed * Time.deltaTime;
                if (spin)
                    thing.transform.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);
                if (player != null && !player.IsDead && Flat(player.transform.position - thing.transform.position).magnitude < hitRadius)
                {
                    if (player.TakeHit(DamageOf(damage), type, attack: true))
                        onHit?.Invoke(player);
                    break;
                }
                yield return null;
            }
            if (thing != null)
                Destroy(thing);
        }

        /// <summary>A lingering patch (fire, spores): every half second it does something to whoever stands in it.</summary>
        private void Patch(Vector3 at, float radius, float seconds, Color color, Action<PlayerStats> inside)
        {
            GameObject pool = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, color);
            pool.name = "BossPatch";
            at.y = Debris.GroundBelow(at + Vector3.up);
            pool.transform.position = at + Vector3.up * 0.16f;
            pool.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            VenomPuddle p = pool.AddComponent<VenomPuddle>();
            p.Seconds = seconds;
            p.Radius = radius;
            p.Tick = 0.5f;
            p.Inside = (center, r) =>
            {
                if (this == null || health.IsDead || player == null || player.IsDead)
                    return;
                if (Flat(player.transform.position - center).magnitude <= r)
                    inside?.Invoke(player);
            };
            leftovers.Add(pool);
        }

        private void Summon(int kindIndex, int count, Color glow, Vector3? around = null, float spread = -1f)
        {
            minions.RemoveAll(m => m == null || m.IsDead);
            count = Mathf.Min(count, CurrentMaxMinions - minions.Count);
            if (minionPrefab == null || kindIndex < 0 || count <= 0)
                return;

            Vector3 center = around ?? transform.position;
            if (spread < 0f)
                spread = 2.2f + 1.2f * Size;
            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f + Random.value;
                Vector3 at = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * spread;
                at.y = transform.position.y - Size + EnemyKinds.Get(kindIndex).Scale * 1.1f;
                GameObject minion = Instantiate(minionPrefab, at, Quaternion.LookRotation(Flat(transform.position - at) + Vector3.forward * 0.001f), transform.parent);
                EnemyKinds.Apply(minion, kindIndex, level);
                EnemyHealth h = minion.GetComponent<EnemyHealth>();
                minions.Add(h);
                minion.GetComponent<EnemyController>()?.Alert();
                SkillEffects.Shockwave(at, 1.4f, glow, 0.5f);
            }
        }

        private int LiveMinions()
        {
            minions.RemoveAll(m => m == null || m.IsDead);
            return minions.Count;
        }

        private int CurrentMaxMinions => secondPhase ? enragedMaxMinions : calmMaxMinions;

        private float DamageOf(float multiplier)
        {
            float rage = controller != null ? controller.DamageMultiplier : 1f;
            return kind.Damage * multiplier * EnemyKinds.DamageScale(level, kind) * rage;
        }

        private bool HitIfInside(Vector3 center, float radius, float damageMultiplier, DamageType? type = null, bool attack = false)
        {
            if (player == null || player.IsDead || Flat(player.transform.position - center).magnitude > radius)
                return false;
            return player.TakeHit(DamageOf(damageMultiplier), type ?? kind.DamageType, attack);
        }

        // Whether the player stands in a strip from start along dir.
        private bool InStrip(Vector3 start, Vector3 dir, float length, float width)
        {
            if (player == null || player.IsDead)
                return false;
            Vector3 off = Flat(player.transform.position - start);
            float along = Vector3.Dot(off, dir);
            float across = Mathf.Abs(Vector3.Dot(off, Vector3.Cross(Vector3.up, dir)));
            return along >= -0.5f && along <= length + 0.5f && across <= width * 0.5f + 0.3f;
        }

        private IEnumerator KnockPlayer(Vector3 direction, float distance)
        {
            CharacterController playerBody = player != null ? player.GetComponent<CharacterController>() : null;
            if (playerBody == null)
                yield break;
            direction = Flat(direction).normalized;
            const float seconds = 0.22f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (player == null || player.IsDead || !playerBody.enabled)
                    yield break;
                Vector3 from = player.transform.position;
                Vector3 to = World.GroundObstacleMotion.Clamp(playerBody, from, from + direction * (distance / seconds) * Time.deltaTime);
                playerBody.Move(to - from);
                yield return null;
            }
        }

        // Hides or shows the whole body (a smoke bomb, burrowing).
        private void SetVisible(bool visible)
        {
            Transform model = transform.Find("Model");
            if (model == null)
                return;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
        }

        // A named part of the body (the coffin, the bell, the spear), or null.
        private Transform Part(string name)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        // A copy of a part of the body, loose in the world (thrown, dropped).
        private GameObject Copy(Transform part)
        {
            GameObject copy = Instantiate(part.gameObject);
            copy.transform.SetPositionAndRotation(part.position, part.rotation);
            copy.transform.localScale = part.lossyScale;
            copy.SetActive(true);
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
            return copy;
        }

        private void MoveTo(Vector3 position)
        {
            if (body != null && body.enabled)
                body.Move(position - transform.position);
            else
                transform.position = position;
        }

        // Puts the boss somewhere at once (a blink, rising elsewhere).
        private void Teleport(Vector3 position)
        {
            position.y = transform.position.y;
            bool was = body != null && body.enabled;
            if (body != null)
                body.enabled = false;
            transform.position = position;
            if (body != null)
                body.enabled = was;
        }

        // Somewhere open near a point, inside the area (never inside a wall).
        private Vector3 OpenNear(Vector3 want)
        {
            int area = World.AreaManager.Instance != null ? World.AreaManager.Instance.CurrentAreaIndex : -1;
            if (area < 0)
                return want;
            World.AreaShape shape = World.WorldBuilder.Shape(area);
            // Somewhere outside every layout (a test sandbox): nothing to keep inside.
            if (shape.Contains(want, 2.5f) || !shape.Contains(transform.position))
                return want;
            for (int i = 1; i <= 8; i++)
            {
                Vector3 toward = Vector3.Lerp(want, transform.position, i / 8f);
                if (shape.Contains(toward, 2.5f))
                    return toward;
            }
            return transform.position;
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

        private void Face(Vector3 direction)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        // Waits this long at the boss's tempo.
        private IEnumerator Pause(float seconds)
        {
            seconds /= T;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // A loose prop made of primitives (thrown things, traps), parented to nothing.
        private static GameObject Prop(string name)
        {
            return new GameObject(name);
        }

        private static GameObject Piece(Transform parent, PrimitiveType type, Color color, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            GameObject go = RuntimePrimitives.Create(type, parent, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }
    }
}
