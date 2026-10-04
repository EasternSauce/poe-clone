using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// What a boss does besides its plain blows: its own set of big, readable moves, one at a time
    /// while the player is close, each telegraphed on the ground before it lands.
    ///
    /// - Gravelord Mortis (greataxe): leaps onto the player and buries the axe where he lands;
    ///   spins the axe all round himself; raises zombies.
    /// - The Ashen Warlord (maul): slams the maul down and sends a line of eruptions racing at the
    ///   player; quake-leaps; rains fire.
    /// - Rimeheart (a giant frost spider): pounces; bursts frost all round her, chilling; hatches
    ///   ice crawlers; calls down ice.
    ///
    /// At half life each boss roars into a second phase: a burst of its own, and its moves come
    /// faster. While a move plays the boss is <see cref="Busy"/> (its controller and plain attacks
    /// wait). Added by <see cref="World.BossLair"/> when it places the boss.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class BossAbilities : MonoBehaviour
    {
        private const float EngageRange = 18f;
        private const float MoveEvery = 3.0f;
        private const float MoveEveryEnraged = 2.0f;
        private const int MaxMinions = 5;

        private enum Move
        {
            Leap,
            Spin,
            RaiseDead,
            Fissure,
            Rain,
            Nova,
            Brood
        }

        private static readonly Color Dust = new Color(0.72f, 0.64f, 0.5f, 1f);

        private EnemyHealth health;
        private EnemyKind kind;
        private int level;
        private GameObject minionPrefab;
        private PlayerStats player;
        private CharacterController body;
        private CharacterAttackAnimator attackAnimator;
        private CharacterWalkAnimator walk;
        private CreatureAnimator creature;

        private float nextMove;
        private float nextLeap;

        // The boss's tempo (EnemyKind.Tempo): every wind-up, flight and pause is divided by it.
        private float T => kind != null ? Mathf.Max(0.1f, kind.Tempo) : 1f;

        // Leaping (the bosses' way of closing in) has its own, shorter timer than the other moves.
        private const float LeapEvery = 3.5f;
        private bool busy;
        private bool secondPhase;
        private Move last = Move.Rain;
        private readonly List<EnemyHealth> minions = new List<EnemyHealth>();

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
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            health.Died += OnBossDied;
            body = GetComponent<CharacterController>();
        }

        private void Start()
        {
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
            walk = GetComponentInChildren<CharacterWalkAnimator>();
            creature = GetComponentInChildren<CreatureAnimator>();
            shepherd = GetComponentInChildren<ShepherdAnimator>();
        }

        // The raised dead and the brood fall with their master.
        private void OnBossDied()
        {
            busy = false;
            SetCrouch(0f);
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    minion.TakeDamage(minion.CurrentHealth + 1f);
            }
            minions.Clear();
        }

        private void OnEnable()
        {
            // The first move comes a little after the fight starts, not on the first frame.
            nextMove = Time.time + 3.5f / T;
            nextLeap = Time.time + 2f / T;
        }

        private void OnDestroy()
        {
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    Destroy(minion.gameObject);
            }
        }

        private void Update()
        {
            if (kind == null || health.IsDead || busy)
                return;
            // The act boss fights its own fight (ShepherdFight), not the field bosses' moves.
            if (kind.Boss == BossStyle.Shepherd)
                return;

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerStats>();
                if (player == null)
                    return;
            }

            float distance = Flat(player.transform.position - transform.position).magnitude;
            if (player.IsDead || distance > EngageRange)
            {
                // Out of the fight: the timer waits.
                nextMove = Mathf.Max(nextMove, Time.time + 2f / T);
                nextLeap = Mathf.Max(nextLeap, Time.time + 1f / T);
                return;
            }

            if (!secondPhase && health.CurrentHealth <= health.MaxHealth * 0.5f)
            {
                secondPhase = true;
                StartCoroutine(Roar());
                return;
            }

            // Its plain blow finishes first.
            if (Time.time < Mathf.Min(nextMove, distance > 4.5f ? nextLeap : float.MaxValue) || (attackAnimator != null && attackAnimator.IsAttacking && !kind.IsCreature))
                return;

            // Out of reach: it leaps after the player on its own timer.
            if (distance > 4.5f && Time.time >= nextLeap && kind.Boss != BossStyle.None)
            {
                nextLeap = Time.time + LeapEvery / T;
                StartCoroutine(LeapSlam());
                return;
            }

            Move move = PickMove(distance);
            last = move;
            nextMove = Time.time + (secondPhase ? MoveEveryEnraged : MoveEvery) / T;
            StartCoroutine(Run(move));
        }

        // The boss's moves in turn, skipping the one just used and any that make no sense from
        // here (no leaping at someone standing next to it, no spinning at someone far away).
        private Move PickMove(float distance)
        {
            Move[] set;
            switch (kind.Boss)
            {
                case BossStyle.Gravelord: set = new[] { Move.Spin, Move.RaiseDead }; break;
                case BossStyle.Warlord: set = new[] { Move.Fissure, Move.Rain }; break;
                default: set = new[] { Move.Nova, Move.Brood, Move.Rain }; break;
            }

            var fitting = new List<Move>();
            foreach (Move m in set)
            {
                if (m == last && set.Length > 1)
                    continue;
                if (m == Move.Leap && distance < 4.5f)
                    continue;
                if ((m == Move.Spin || m == Move.Nova) && distance > 7f)
                    continue;
                if ((m == Move.RaiseDead || m == Move.Brood) && LiveMinions() >= MaxMinions)
                    continue;
                fitting.Add(m);
            }
            if (fitting.Count == 0)
                return set[0] == last && set.Length > 1 ? set[1] : set[0];
            return fitting[Random.Range(0, fitting.Count)];
        }

        private IEnumerator Run(Move move)
        {
            switch (move)
            {
                case Move.Leap: return LeapSlam();
                case Move.Spin: return ReapingSpin();
                case Move.RaiseDead: return RaiseDead(2);
                case Move.Fissure: return Fissure();
                case Move.Rain: return RainDown(kind.Boss == BossStyle.Warlord ? DamageType.Fire : DamageType.Cold, secondPhase ? 9 : 7);
                case Move.Nova: return FrostBurst(7.5f);
                default: return Brood(secondPhase ? 3 : 2);
            }
        }

        // ------------------------------------------------------------------ the moves

        // Gathers itself, springs into the air, and comes down where the player is heading,
        // weapon first. The landing glows on the ground from the moment it crouches.
        private IEnumerator LeapSlam()
        {
            busy = true;
            float gather = 0.4f / T;
            float air = 0.55f / T;
            float scale = transform.localScale.x;
            float radius = 3.0f + 0.6f * scale;
            float gap = (body != null ? body.radius * scale : 1f) + 0.6f;

            Vector3 start = transform.position;
            // Aims where the player is heading - a beat past it, so running straight on doesn't
            // clear the landing.
            Vector3 landing = PlayerMotion.Predict(player, gather + air + 0.35f);
            Vector3 jump = Flat(landing - start);
            if (jump.magnitude > 18f)
                landing = start + jump.normalized * 18f;
            landing.y = start.y;
            Face(landing - start);

            bool landed = false;
            StartCoroutine(GroundTelegraph.Run(landing, radius, gather + air, kind.DamageType, at =>
            {
                if (this == null || health.IsDead)
                    return;
                landed = true;
                HitIfInside(at, radius, 1.6f);
                SkillEffects.Shockwave(at, radius, Dust, 0.45f);
                SkillEffects.Shockwave(at, radius * 0.6f, GroundTelegraph.FillColor(kind.DamageType), 0.35f);
                CameraSystem.CameraFollow.Shake(0.35f, 0.45f);
                OnLanded(at);
            }));

            // The weapon goes up as it gathers and comes down as it lands.
            SwingTimed(gather + air);
            if (creature != null)
                creature.Crouch(gather);

            for (float t = 0f; t < gather; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                SetCrouch(Mathf.SmoothStep(0f, 1f, t / gather));
                yield return null;
            }

            SkillEffects.Shockwave(start, 1.2f * scale, Dust, 0.3f);
            float height = 3.2f + 0.8f * scale;
            Vector3 from = transform.position;
            for (float t = 0f; t < air; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                float f = Mathf.Clamp01(t / air);
                // Tucked at the top of the arc, stretching out for the landing.
                SetCrouch(f < 0.5f ? Mathf.Lerp(1f, 0.35f, f * 2f) : Mathf.Lerp(0.35f, 0.7f, (f - 0.5f) * 2f));
                Vector3 want = Vector3.Lerp(from, landing, f) + Vector3.up * height * 4f * f * (1f - f);
                MoveTo(KeepClear(want, gap));
                yield return null;
            }
            MoveTo(KeepClear(landing, gap) + Vector3.down * 0.2f);

            // Hold the landing crouch a beat, then rise.
            float recover = 0.3f / T;
            for (float t = 0f; t < recover; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                SetCrouch(Mathf.Lerp(landed ? 0.8f : 0.5f, 0f, t / recover));
                yield return null;
            }
            SetCrouch(0f);
            busy = false;
        }

        // What follows a leap's landing: the Gravelord's dead claw up round him (second phase),
        // the Warlord's quake throws up a ring of fire, the Queen's frost bites.
        private void OnLanded(Vector3 at)
        {
            if (kind.Boss == BossStyle.Gravelord && secondPhase)
                StartCoroutine(RaiseDead(1));
            else if (kind.Boss == BossStyle.Warlord)
                Ring(at, 4.2f + transform.localScale.x, secondPhase ? 10 : 8, 0.9f, 1.0f / T, DamageType.Fire);
            else if (kind.Boss == BossStyle.FrostQueen && player != null && Flat(player.transform.position - at).magnitude < 5f)
                player.GetComponent<PlayerController>()?.Chill(2f);
        }

        // Draws the axe back while the ground all round glows, then whirls through a full turn.
        private IEnumerator ReapingSpin()
        {
            busy = true;
            float windUp = 0.8f / T;
            float radius = 4.6f + 0.7f * transform.localScale.x;

            if (attackAnimator != null)
            {
                attackAnimator.PlaybackSpeed = CharacterAttackAnimator.StrikeSeconds(WeaponType.Greatsword) / windUp;
                attackAnimator.PlayAttack(WeaponType.Greatsword);
            }
            StartCoroutine(GroundTelegraph.Run(transform.position, radius, windUp, kind.DamageType, at =>
            {
                if (this == null || health.IsDead)
                    return;
                HitIfInside(at, radius, 1.4f);
                SkillEffects.Shockwave(at, radius, Dust, 0.35f);
                CameraSystem.CameraFollow.Shake(0.15f, 0.3f);
            }));

            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                SetCrouch(0.4f * (t / windUp));
                yield return null;
            }

            float yaw = transform.eulerAngles.y;
            float spin = 0.4f / T;
            for (float t = 0f; t < spin; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                transform.rotation = Quaternion.Euler(0f, yaw + 360f * (t / spin), 0f);
                SetCrouch(0.4f * (1f - t / spin));
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            SetCrouch(0f);
            yield return Pause(0.15f);
            busy = false;
        }

        // Raises the maul high and brings it down: the ground splits towards the player in a
        // line of eruptions, one after another.
        private IEnumerator Fissure()
        {
            busy = true;
            float windUp = 0.7f / T;
            Vector3 toPlayer = Flat(PlayerMotion.Predict(player, windUp + 0.4f) - transform.position);
            Vector3 dir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
            Face(dir);

            SwingTimed(windUp);
            // The patches overlap so the line reads as one crack: it hurts once per slam.
            var once = new bool[1];
            int count = secondPhase ? 9 : 7;
            float step = 1.7f;
            Vector3 origin = transform.position + dir * (1.2f * transform.localScale.x);
            for (int k = 0; k < count; k++)
            {
                Vector3 at = origin + dir * step * k;
                // A second phase splits into a fork on either side of the main line.
                StartCoroutine(Eruption(at, 1.7f, windUp + k * 0.07f / T, DamageType.Fire, 1.2f, once));
                if (secondPhase && k > 1 && k % 2 == 0)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * (0.35f * step * k);
                    StartCoroutine(Eruption(at + side, 1.1f, windUp + (k * 0.07f + 0.1f) / T, DamageType.Fire, 1.0f, once));
                    StartCoroutine(Eruption(at - side, 1.1f, windUp + (k * 0.07f + 0.1f) / T, DamageType.Fire, 1.0f, once));
                }
            }

            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                SetCrouch(t / windUp < 0.7f ? 0f : 0.6f);
                yield return null;
            }
            CameraSystem.CameraFollow.Shake(0.3f, 0.4f);
            SkillEffects.Shockwave(origin, 1.8f, Dust, 0.35f);
            yield return Pause(0.25f);
            SetCrouch(0f);
            busy = false;
        }

        // Rimeheart rears and the frost bursts out of her all round, chilling whoever it catches.
        private IEnumerator FrostBurst(float radius)
        {
            busy = true;
            float windUp = 0.85f / T;
            if (creature != null)
                creature.Crouch(windUp);
            StartCoroutine(GroundTelegraph.Run(transform.position, radius, windUp, DamageType.Cold, at =>
            {
                if (this == null || health.IsDead)
                    return;
                if (HitIfInside(at, radius, 1.3f))
                    player.GetComponent<PlayerController>()?.Chill(3f);
                SkillEffects.Shockwave(at, radius, GroundTelegraph.FillColor(DamageType.Cold), 0.5f);
                CameraSystem.CameraFollow.Shake(0.15f, 0.3f);
            }));
            yield return Pause(0.85f + 0.3f);
            busy = false;
        }

        private IEnumerator RaiseDead(int count)
        {
            if (kind.Weapon != WeaponType.Unarmed)
                SwingTimed(0.6f / T);
            yield return Pause(0.6f);
            if (health.IsDead)
                yield break;
            CameraSystem.CameraFollow.Shake(0.12f, 0.3f);
            Summon(0, count, new Color(0.3f, 0.9f, 0.4f));
        }

        // Ice crawlers hatch round the queen.
        private IEnumerator Brood(int count)
        {
            busy = true;
            if (creature != null)
                creature.Crouch(0.8f / T);
            yield return Pause(0.8f);
            if (!health.IsDead)
                Summon(EnemyKinds.IndexOf("Ice Crawler"), count, GroundTelegraph.FillColor(DamageType.Cold));
            busy = false;
        }

        // Patches round the player (the first right under them, or where they're heading in the
        // second phase) that burst after a beat.
        private IEnumerator RainDown(DamageType type, int count)
        {
            // Each patch comes down where the player will be when it lands, scattered round that.
            for (int k = 0; k < count; k++)
            {
                float windUp = (0.9f + k * 0.15f) / T;
                Vector3 target = PlayerMotion.Predict(player, windUp);
                Vector2 scatter = Random.insideUnitCircle * 3f;
                Vector3 at = k == 0 ? target : target + new Vector3(scatter.x, 0f, scatter.y);
                StartCoroutine(Eruption(at, 2.7f, windUp, type, 1.2f));
            }
            yield break;
        }

        // Half life: it stops, roars, and the fight changes gear.
        private IEnumerator Roar()
        {
            busy = true;
            CombatText.Show(transform.position + Vector3.up * health.BarHeight * transform.localScale.y,
                kind.Name + " grows furious!", CombatText.ColorFor(kind.DamageType), 1.4f);
            EnemySounds.Play(kind, EnemySounds.Event.Aggro, transform.position);
            CameraSystem.CameraFollow.Shake(0.3f, 0.9f);
            if (creature != null)
                creature.Crouch(1.2f / T);

            float roar = 1.2f / T;
            for (float t = 0f; t < roar; t += Time.deltaTime)
            {
                if (health.IsDead)
                    yield break;
                // Rears back then hunches, roaring.
                SetCrouch(Mathf.Sin(t / roar * Mathf.PI) * 0.8f);
                if (Mathf.Repeat(t, 0.3f) < Time.deltaTime)
                    SkillEffects.Shockwave(transform.position, 2f + 4f * t, GroundTelegraph.FillColor(kind.DamageType), 0.35f);
                yield return null;
            }
            SetCrouch(0f);

            switch (kind.Boss)
            {
                case BossStyle.Gravelord: Summon(0, 3, new Color(0.3f, 0.9f, 0.4f)); break;
                case BossStyle.Warlord: Ring(transform.position, 5f + transform.localScale.x, 10, 0.9f, 1.5f / T, DamageType.Fire); break;
                default: Summon(EnemyKinds.IndexOf("Ice Crawler"), 3, GroundTelegraph.FillColor(DamageType.Cold)); break;
            }
            nextMove = Time.time + 1.5f / T;
            busy = false;
        }

        // ------------------------------------------------------------------ pieces

        // A glowing patch that fills in over the wind-up, then hurts the player if they're still on
        // it. Patches sharing a <paramref name="hitOnce"/> flag hurt at most once between them.
        private IEnumerator Eruption(Vector3 center, float radius, float windUp, DamageType type, float damageMultiplier, bool[] hitOnce = null)
        {
            return GroundTelegraph.Run(center, radius, windUp, type, at =>
            {
                if (this == null || (health.IsDead && type != DamageType.Fire))
                    return;
                if (hitOnce == null || !hitOnce[0])
                {
                    if (HitIfInside(at, radius, damageMultiplier, type) && hitOnce != null)
                        hitOnce[0] = true;
                }
                SkillEffects.Shockwave(at, radius, GroundTelegraph.FillColor(type), 0.3f);
            });
        }

        // Eruptions in a ring round a point, all at once after the wind-up.
        private void Ring(Vector3 center, float ringRadius, int count, float patchRadius, float windUp, DamageType type)
        {
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                Vector3 at = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius;
                StartCoroutine(Eruption(at, patchRadius + 0.4f, windUp, type, 0.9f));
            }
        }

        private void Summon(int kindIndex, int count, Color glow)
        {
            minions.RemoveAll(m => m == null || m.IsDead);
            count = Mathf.Min(count, MaxMinions - minions.Count);
            if (minionPrefab == null || kindIndex < 0 || count <= 0)
                return;

            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f + Random.value;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (2.2f + 1.2f * transform.localScale.x);
                at.y = 1.1f;
                GameObject minion = Instantiate(minionPrefab, at, Quaternion.LookRotation(Flat(transform.position - at) + Vector3.forward * 0.001f), transform.parent);
                EnemyKinds.Apply(minion, kindIndex, level);
                minions.Add(minion.GetComponent<EnemyHealth>());
                SkillEffects.Shockwave(at, 1.4f, glow, 0.5f);
            }
        }

        private int LiveMinions()
        {
            minions.RemoveAll(m => m == null || m.IsDead);
            return minions.Count;
        }

        // Plays the boss's weapon swing so its blow lands after the given time.
        private void SwingTimed(float seconds)
        {
            if (attackAnimator == null || kind.Weapon == WeaponType.Unarmed || kind.IsCreature)
                return;
            attackAnimator.PlaybackSpeed = CharacterAttackAnimator.StrikeSeconds(kind.Weapon) / Mathf.Max(0.1f, seconds);
            attackAnimator.PlayAttack(kind.Weapon);
        }

        private bool HitIfInside(Vector3 center, float radius, float damageMultiplier, DamageType? type = null)
        {
            if (player == null || player.IsDead || Flat(player.transform.position - center).magnitude > radius)
                return false;
            player.TakeHit(kind.Damage * damageMultiplier * EnemyKinds.DamageScale(level, kind), type ?? kind.DamageType);
            return true;
        }

        private void SetCrouch(float amount)
        {
            if (walk != null)
                walk.Crouch = amount;
        }

        private void MoveTo(Vector3 position)
        {
            if (body != null && body.enabled)
                body.Move(position - transform.position);
            else
                transform.position = position;
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
    }
}
