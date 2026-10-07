using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// The Shepherd's fight, phase 1. In reach he swings his crook (Sweep, Jab, Slam; never the
    /// same twice running). On their own timers come the specials:
    /// <list type="bullet">
    /// <item>Hook &amp; Pull: the hook catches the player and yanks them in, straight into a Sweep.</item>
    /// <item>Cobra Lunge: he sways like a cobra while his path lights up on the ground, then dives
    /// along it. For a player keeping their distance.</item>
    /// <item>Serpent's Call: he plants the crook, the ground cracks under and round the player, and
    /// snakes burst up and bite.</item>
    /// </list>
    /// Blows land on the clip's hit times (<see cref="ShepherdAnimator.Hit"/>), or when a warning
    /// patch on the ground goes off. While a clip plays the boss is busy (<see cref="BossAbilities.Busy"/>):
    /// no walking, no plain attacks.
    /// </summary>
    public class ShepherdFight : MonoBehaviour
    {
        private const float EngageRange = 24f;
        // How fast every clip plays (and so every wind-up and warning). Keep this aligned with
        // the other act bosses' reduced tempo.
        private float Pace => 1.6f * (kind != null ? kind.Tempo / 2f : 1f)
            * (walker != null ? walker.AttackSpeedMultiplier : 1f);
        private const float Breather = 0.075f;

        private const float HookEvery = 2.25f;
        private const float LungeEvery = 3f;
        private const float CallEvery = 4f;
        private const float SnakeWindUp = 0.45f;

        // Where his moves reach, at scale 1 (they grow with him).
        private const float Reach = 3.0f;
        private const float HookReach = 3.4f;
        internal const float LungeLength = 12.6f;
        private const float LungeWidth = 3f;
        private const float SnakeRadius = 1.2f;

        private static readonly string[] CrookMoves = { "Sweep", "Jab", "Slam" };
        private static readonly string[] BiteMoves = { "Bite", "DoubleBite" };

        // Phase 2's bites, at scale 1: how far the snake arm reaches for the player, and how big
        // the snap of its jaws is where it lands.
        private const float BiteReach = 3.0f;
        private const float BiteRadius = 0.5f;
        private const float BiteShoot = 0.07f;

        /// <summary>1: the old shepherd with his crook. 2: the disguise off, giant, the snake arm grafted on.</summary>
        public int Phase { get; private set; } = 1;

        private SnakeLimb snakeArm;
        private readonly System.Collections.Generic.List<SnakeLimb> backSnakes = new System.Collections.Generic.List<SnakeLimb>();

        // Phase 2's specials, each on its own timer, never two straight after one another.
        private const float VolleyEvery = 3f;
        private const float SpoutsEvery = 4.5f;
        private const float SnatchEvery = 4f;
        private const float DiveEvery = 5.5f;
        private const float SpecialGap = 0.6f;
        private float nextVolley, nextSpouts, nextSnatch, nextDive, nextSpecial;
        private EnemyController walker;

        // Closer than this (at scale 1) the arm would have to bend back under him: a bite reaches
        // at least this far out in front.
        private const float BiteMin = 1.4f;

        private EnemyKind kind;
        private int level;
        private ShepherdAnimator anim;
        private EnemyHealth health;
        private PlayerStats player;

        private float nextSwing;
        private float nextHook;
        private float nextLunge;
        private float nextCall;
        private string lastMove;
        private string queued;

        private float Scale => transform.localScale.x;

        // How much bigger than in phase 1 he is: distances authored in metres grow with him.
        private float Grow => Scale / ShepherdLook.BaseScale;

        /// <summary>Testing: he picks no moves of his own, only those <see cref="Force"/>d on him.</summary>
        public bool Manual;

        public void Configure(EnemyKind bossKind, int monsterLevel)
        {
            kind = bossKind;
            level = monsterLevel;
        }

        private void Start()
        {
            anim = GetComponentInChildren<ShepherdAnimator>();
            health = GetComponent<EnemyHealth>();
            walker = GetComponent<EnemyController>();
            if (anim != null)
                anim.Hit += OnHit;
            // Phase 1 can't be skipped past: damage holds at the threshold until he has changed.
            if (health != null)
            {
                health.Floor = health.MaxHealth * PhaseTwoAt;
                health.Died += OnDied;
            }
            nextSwing = Time.time + 0.5f;
            nextHook = Time.time + 1.5f;
            nextLunge = Time.time + 2f;
            nextCall = Time.time + 3.5f;
        }

        private void OnDestroy()
        {
            if (anim != null)
                anim.Hit -= OnHit;
            if (health != null)
                health.Died -= OnDied;
            ZoomOut(false);
            foreach (GameObject left in leftBehind)
            {
                if (left != null)
                    Destroy(left);
            }
        }

        private void OnDisable()
        {
            bool phaseThreeArenaView = Phase >= 3 && World.AreaManager.Instance != null
                && World.AreaManager.Instance.CurrentAreaIndex == World.WorldBuilder.ActArena;
            if (!phaseThreeArenaView)
                ZoomOut(false);
        }

        // Grown giant, he doesn't fit the normal view: it pulls back while he's fighting.
        private const float GiantZoom = 1.5f;
        private bool zoomed;

        private void OnDied() { ZoomOut(false); }

        private void ZoomOut(bool on)
        {
            if (on == zoomed)
                return;
            zoomed = on;
            CameraSystem.CameraFollow.Zoom = on ? GiantZoom : 1f;
        }

        private void Update()
        {
            bool dead = health != null && health.IsDead;
            bool insideActArena = World.AreaManager.Instance != null
                && World.AreaManager.Instance.CurrentAreaIndex == World.WorldBuilder.ActArena;
            bool phaseThreeArenaView = Phase >= 3 && insideActArena && !dead;
            bool nearGiant = Grow > 1.01f && player != null
                && Flat(player.transform.position - transform.position).magnitude < EngageRange * Grow;
            ZoomOut(phaseThreeArenaView || (!dead && player != null && !player.IsDead && nearGiant));
            if (kind == null || anim == null || anim.Demo || dead || changing || Phase >= 3)
                return;
            if (Phase == 1 && health != null && health.CurrentHealth <= health.MaxHealth * PhaseTwoAt + 0.01f)
            {
                BeginPhase2();
                return;
            }
            if (anim.IsPlaying)
                return;
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerStats>();
                if (player == null)
                    return;
            }
            if (player.IsDead)
                return;

            // A move that follows straight on from the last one (the Sweep after a pull).
            if (queued != null)
            {
                Begin(queued);
                queued = null;
                return;
            }
            // He walks in until he's in reach, and no closer.
            if (walker != null)
                walker.StandOff = Phase >= 2 ? BiteReach * Scale * 0.65f : Reach * Scale * 0.8f;
            if (anim.Target == null)
                anim.Target = player.transform;
            anim.MinGap = 0.5f * Scale + 2f;

            if (Manual)
                return;

            float distance = Flat(player.transform.position - transform.position).magnitude;
            if (distance > EngageRange * Grow)
                return;

            float reach = (Reach + 0.4f) * Scale;
            float now = Time.time;

            if (Phase >= 2)
            {
                PickPhaseTwo(distance, now);
                return;
            }
            if (now >= nextLunge && distance > 4.5f * Scale * 0.8f && distance < LungeLength * Grow + 0.8f * Scale)
            {
                nextLunge = now + LungeEvery * SpecialCooldownScale;
                Begin("CobraLunge");
                return;
            }
            if (now >= nextCall && (distance > reach || now >= nextSwing))
            {
                nextCall = now + CallEvery * SpecialCooldownScale;
                Begin("SerpentCall");
                return;
            }
            if (now >= nextHook && distance <= HookReach * Scale)
            {
                nextHook = now + HookEvery * SpecialCooldownScale;
                Begin("HookPull");
                return;
            }
            if (distance <= reach && now >= nextSwing)
            {
                string move;
                do
                    move = CrookMoves[Random.Range(0, CrookMoves.Length)];
                while (move == lastMove);
                Begin(move);
            }
        }

        /// <summary>Into phase 2: he drops the disguise (<see cref="ShepherdLook.ToPhase2"/>) and fights with the snake arm.</summary>
        public void EnterPhase2()
        {
            if (Phase >= 2)
                return;
            Phase = 2;
            if (anim != null)
            {
                anim.Stop();
                anim.BodyPitchOffset = -ShepherdLook.Stoop;
            }
            queued = null;
            if (health != null)
                health.Floor = 0f;
            ShepherdLook.ToPhase2(transform);
            foreach (SnakeLimb limb in GetComponentsInChildren<SnakeLimb>())
            {
                if (limb.name == "SnakeArm")
                    snakeArm = limb;
                else
                    backSnakes.Add(limb);
            }
            StartPhaseTwoTimers();
        }

        /// <summary>Animation review entry point; lethal phase-two wiring comes with phase-three combat.</summary>
        public void BeginPhase3()
        {
            if (Phase != 2 || changing) return;
            StopAllCoroutines();
            if (anim != null) anim.Stop();
            queued = null;
            changing = true;
            var reveal = GetComponent<CarrionSaintReveal>();
            if (reveal == null) reveal = gameObject.AddComponent<CarrionSaintReveal>();
            reveal.Begin(() =>
            {
                Phase = 3; changing = false;
                if (!Manual)
                {
                    health.Immune = false;
                    gameObject.AddComponent<CarrionSaintFight>().Configure(kind.Damage * EnemyKinds.DamageScale(level, kind));
                }
            });
        }

        private void StartPhaseTwoTimers()
        {
            float now = Time.time;
            nextVolley = now + 0.75f * SpecialCooldownScale;
            nextSnatch = now + 1.75f * SpecialCooldownScale;
            nextSpouts = now + 3.25f * SpecialCooldownScale;
            nextDive = now + 4.75f * SpecialCooldownScale;
            nextSpecial = now + 0.5f;
        }

        // Phase 2: the specials on their timers (a gap after each), and in reach the bites.
        private void PickPhaseTwo(float distance, float now)
        {
            if (now >= nextSpecial)
            {
                string special = null;
                if (now >= nextSnatch && distance > 3f * Grow)
                {
                    special = "Snatch";
                    nextSnatch = now + SnatchEvery * SpecialCooldownScale;
                }
                else if (now >= nextDive)
                {
                    special = "SerpentDive";
                    nextDive = now + DiveEvery * SpecialCooldownScale;
                }
                else if (now >= nextSpouts)
                {
                    special = "Spouts";
                    nextSpouts = now + SpoutsEvery * SpecialCooldownScale;
                }
                else if (now >= nextVolley && distance > BiteReach * Scale * 0.5f)
                {
                    special = "VenomVolley";
                    nextVolley = now + VolleyEvery * SpecialCooldownScale;
                }
                if (special != null)
                {
                    Begin(special);
                    nextSpecial = now + anim.Current.Duration / Pace + SpecialGap * SpecialCooldownScale;
                    return;
                }
            }
            // The snake arm reaches far: he bites from well outside where he'd swing a crook.
            if (distance <= BiteReach * Scale && now >= nextSwing)
                Begin(lastMove == "DoubleBite" || Random.value < 0.6f ? "Bite" : "DoubleBite");
        }

        /// <summary>Phase three keeps the arena snake attacks while the old body rig is hidden.</summary>
        public void PhaseThreeArenaSpecial(int choice)
        {
            if (Phase != 3 || health == null || health.IsDead) return;
            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            if (player == null || player.IsDead) return;
            switch (choice % 4)
            {
                case 0: Spouts(); break;
                case 1: StartCoroutine(Snatch()); break;
                case 2: SerpentDive(); break;
                default:
                    // The phase-two back snakes are hidden; venom comes from the new body.
                    for (int i = -1; i <= 1; i += 2)
                        Spit(transform.TransformPoint(new Vector3(i * 0.8f, 2.1f, 1.0f)), 1.5f * Grow);
                    break;
            }
        }

        // Moves that play another move's clip.
        private static string ClipFor(string move)
        {
            switch (move)
            {
                case "VenomVolley": return "Spit";
                case "Spouts": return "SerpentCall";
                case "SerpentDive": return "SerpentCall";
                case "Snatch": return "Burrow";
                default: return move;
            }
        }

        // ------------------------------------------------------------------ phase 2's specials

        private float BaseHit => kind.Damage * EnemyKinds.DamageScale(level, kind)
            * (walker != null ? walker.DamageMultiplier : 1f);

        // Special cooldowns are doubled while calm; enrage restores their current cadence.
        private float SpecialCooldownScale => walker != null && walker.IsEnraged ? 1f : 2f;

        // A glob of venom from a mouth to near the player: splash damage where it lands, then a
        // puddle that poisons whoever stands in it.
        private void Spit(Vector3 mouth, float spread)
        {
            if (player == null)
                return;
            Vector3 at = player.transform.position + Flat(Random.insideUnitSphere) * spread;
            float radius = 1.6f * Grow * (Phase >= 3 ? 1.25f : 1f);
            VenomGlob.Lob(mouth, at, 0.55f, 0.35f * Grow * (Phase >= 3 ? 1.25f : 1f), radius, 4f,
                spot => HitInside(spot, radius + 0.2f, 0.7f, attack: false),
                (centre, r) =>
                {
                    if (player != null && !player.IsDead && Flat(player.transform.position - centre).magnitude <= r)
                        player.PoisonFromPool(BaseHit * 0.16666667f, 2f);
                });
        }

        // Venom Volley: every snake on his back strikes forward and spits at the player.
        private void VenomWave()
        {
            if (player == null)
                return;
            Vector3 to = Flat(player.transform.position - transform.position).normalized;
            for (int i = 0; i < backSnakes.Count; i++)
            {
                SnakeLimb limb = backSnakes[i];
                if (limb == null)
                    continue;
                Vector3 lunge = limb.transform.position + to * limb.Length * 0.6f + Vector3.up * limb.Length * 0.2f;
                limb.Strike(lunge, 0.02f + 0.05f * i, 0.07f, 0.05f, 0.25f, mouth => Spit(mouth, 1.6f * Grow));
            }
        }

        // Venom Spouts: snakes burst out of the floor around the player and each spits twice.
        private void Spouts()
        {
            if (player == null)
                return;
            Vector3 centre = player.transform.position;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 spot = centre + Quaternion.Euler(0f, turn + i * 120f + Random.Range(-20f, 20f), 0f) * Vector3.forward * 3.5f * Grow;
                spot.y = Debris.GroundBelow(spot + Vector3.up * 2f);
                StartCoroutine(Spout(spot, 0.15f * i));
            }
        }

        private IEnumerator Spout(Vector3 spot, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (player == null)
                yield break;
            float height = 3.2f * ShepherdLook.BaseScale * Mathf.Sqrt(Grow) * (Phase >= 3 ? 1.1f : 0.8f);
            GroundSnake snake = GroundSnake.Spawn(spot, player.transform.position - spot, height, 1.6f);
            for (int k = 0; k < 2; k++)
            {
                yield return new WaitForSeconds(k == 0 ? 0.45f : 0.6f);
                if (player == null || player.IsDead)
                    yield break;
                if (snake != null) Spit(snake.MouthPosition, 0.8f * Grow);
            }
        }

        // Burrow Snatch: with the arm in the ground, a ring follows the player under the floor,
        // locks, and the snake erupts out of it jaws-first.
        private IEnumerator Snatch()
        {
            const float Track = 0.8f;
            const float Lock = 0.35f;
            float radius = 1.3f * Grow * (Phase >= 3 ? 1.3f : 1f);
            var ring = new GameObject("SnatchWarning");
            GameObject outer = RuntimePrimitives.Create(PrimitiveType.Cylinder, ring.transform, new Color(0.22f, 0.16f, 0.10f));
            outer.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            GameObject inner = RuntimePrimitives.Create(PrimitiveType.Cylinder, ring.transform, new Color(0.85f, 0.70f, 0.45f));
            inner.transform.localPosition = Vector3.up * 0.02f;

            Vector3 at = transform.position;
            for (float t = 0f; t < Track + Lock; t += Time.deltaTime)
            {
                if (player == null || health == null || health.IsDead)
                    break;
                if (t < Track)
                {
                    at = player.transform.position;
                    at.y = Debris.GroundBelow(at + Vector3.up) + 0.17f;
                }
                ring.transform.position = at;
                float f = Mathf.Clamp01(t / (Track + Lock));
                inner.transform.localScale = new Vector3(radius * 2f * f, 0.01f, radius * 2f * f);
                yield return null;
            }
            Destroy(ring);
            if (player == null || health == null || health.IsDead)
                yield break;

            GroundSnake.Spawn(at, transform.position - at, 3.2f * ShepherdLook.BaseScale * Mathf.Sqrt(Grow) * (Phase >= 3 ? 1.4f : 1.15f), 0.4f);
            CameraSystem.CameraFollow.Shake(0.35f, 0.35f);
            HitInside(at, radius + 0.3f, 4f);
        }

        // Serpent Dive: a huge snake bursts from the ground on one side of the player, arcs over
        // them and dives back in on the other side; its path lights up first.
        private void SerpentDive()
        {
            if (player == null)
                return;
            Vector3 across = Vector3.Cross(Vector3.up, Flat(player.transform.position - transform.position)).normalized;
            if (across.sqrMagnitude < 0.01f)
                across = transform.right;
            if (Random.value < 0.5f)
                across = -across;
            float length = 11f * Grow;
            Vector3 centre = player.transform.position;
            Vector3 from = centre - across * length * 0.5f;
            Vector3 to = centre + across * length * 0.5f;
            from.y = Debris.GroundBelow(from + Vector3.up * 3f);
            to.y = Debris.GroundBelow(to + Vector3.up * 3f);
            float width = 2.2f * Grow * (Phase >= 3 ? 1.3f : 1f);
            const float Flight = 0.9f;
            StartCoroutine(GroundTelegraph.RunLine(from, across, length, width, 0.7f, DamageType.Physical, () =>
            {
                DivingSerpent.Launch(from, to, 4.5f * Grow * (Phase >= 3 ? 1.2f : 1f), 0.9f * Grow * (Phase >= 3 ? 1.3f : 1f), Flight);
                StartCoroutine(After(Flight * 0.5f, () => HitAlong(from, across, length, width * 0.5f + 0.3f, 1.5f)));
            }));
        }

        private IEnumerator After(float seconds, System.Action then)
        {
            yield return new WaitForSeconds(seconds);
            if (health == null || !health.IsDead)
                then();
        }

        // ------------------------------------------------------------------ the graft (phase 1 -> 2)

        /// <summary>Phase 2 comes at two thirds of his life.</summary>
        public const float PhaseTwoAt = 2f / 3f;

        private bool changing;
        private Transform graftCrook;
        private readonly System.Collections.Generic.List<GameObject> leftBehind = new System.Collections.Generic.List<GameObject>();

        private static readonly Color Blood = new Color(0.30f, 0.03f, 0.05f);

        /// <summary>
        /// The change into phase 2, played out (the "Graft" clip, immune throughout): he tears off
        /// his lantern arm, drives the crook into the stump, and it grows into the snake arm as the
        /// disguise bursts off him and he grows giant. <see cref="EnterPhase2"/> is the instant version.
        /// </summary>
        public void BeginPhase2()
        {
            if (Phase >= 2 || changing || anim == null)
                return;
            StartCoroutine(GraftTransition());
        }

        private IEnumerator GraftTransition()
        {
            changing = true;
            if (health != null)
            {
                health.Immune = true;
                health.Floor = 0f;
            }
            queued = null;
            anim.Stop();
            if (player == null)
                player = FindAnyObjectByType<PlayerStats>();
            if (player != null)
                FacePlayer();
            graftCrook = anim.Crook;
            anim.Play(ShepherdAnimator.Find("Graft"), 1f);
            yield return null;
            while (anim.IsPlaying && anim.Current != null && anim.Current.Name == "Graft")
                yield return null;

            if (health != null)
                health.Immune = false;
            changing = false;
            nextSwing = Time.time + 0.4f;
            nextLunge = Time.time + 3f;
            nextCall = Time.time + 5f;
        }

        private void GraftStep(int step)
        {
            Transform model = anim.transform;
            switch (step)
            {
                case 0: // The crook goes into the ground beside him, upright.
                    if (graftCrook != null)
                        graftCrook.SetParent(transform.parent, true);
                    break;
                case 1:
                    TearArm(model);
                    break;
                case 2: // Back into his hand.
                    if (graftCrook != null)
                    {
                        graftCrook.SetParent(FindIn(model, "Socket_MainHand"), false);
                        graftCrook.localPosition = Vector3.zero;
                    }
                    break;
                case 3:
                    Graft(model);
                    break;
                case 4:
                    StartCoroutine(Unfold(model));
                    break;
                case 5:
                    EnemyController.PlayEnrageStart(transform, kind, kind.BarHeight);
                    CameraSystem.CameraFollow.Shake(0.5f, 0.6f);
                    break;
            }
        }

        // He rips his own left arm off: it flies, lantern still burning in its hand, and stays
        // where it falls. A spray of dark blood from the shoulder.
        private void TearArm(Transform model)
        {
            Transform arm = FindIn(model, "ArmL");
            if (arm == null)
                return;
            GameObject torn = Instantiate(arm.gameObject, arm.position, arm.rotation);
            CopyColours(arm.gameObject, torn);
            torn.name = "Torn Arm";
            torn.transform.localScale = arm.lossyScale;
            foreach (UprightStaff upright in torn.GetComponentsInChildren<UprightStaff>())
                Destroy(upright);
            arm.gameObject.SetActive(false);
            anim.Lantern = null;
            ManualPointLightManager.Refresh();
            Debris.Throw(torn, -transform.right * 6f + Vector3.up * 5f + transform.forward * 1.5f,
                new Vector3(260f, 40f, 320f), 0f, 0.25f * Scale);
            leftBehind.Add(torn);

            Spray(arm.position, -transform.right, 30, 1.6f);
            CameraSystem.CameraFollow.Shake(0.45f, 0.4f);
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayRandomAtPoint(Audio.AudioManager.Instance.meleeHit, arm.position);
        }

        // The crook driven into the stump, pointing out from the shoulder: from here it is the arm.
        private void Graft(Transform model)
        {
            Transform upper = FindIn(model, "UpperOffset");
            if (graftCrook == null || upper == null)
                return;
            foreach (UprightStaff upright in graftCrook.GetComponentsInChildren<UprightStaff>())
                Destroy(upright);
            anim.Crook = null;
            graftCrook.SetParent(upper, false);
            graftCrook.localPosition = new Vector3(-0.52f, 1.50f, 0f);
            graftCrook.localRotation = Quaternion.LookRotation(Vector3.forward, new Vector3(-1f, 0.35f, 0f).normalized);
            Spray(graftCrook.position, -transform.right, 8, 1f);
            CameraSystem.CameraFollow.Shake(0.35f, 0.3f);
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayRandomAtPoint(Audio.AudioManager.Instance.meleeHit, graftCrook.position);
        }

        // The disguise bursts off in pieces, the crook becomes the living snake arm, snakes sprout
        // from his back, and he grows into his giant form.
        private IEnumerator Unfold(Transform model)
        {
            Vector3 chest = transform.position + Vector3.up * Scale;
            foreach (Transform t in model.GetComponentsInChildren<Transform>())
            {
                if (t.name != ShepherdLook.Disguise)
                    continue;
                GameObject scrap = Instantiate(t.gameObject, t.position, t.rotation);
                CopyColours(t.gameObject, scrap);
                scrap.transform.localScale = t.lossyScale;
                Vector3 away = t.position - chest;
                away.y = Mathf.Max(0.3f, away.y);
                Debris.Throw(scrap, away.normalized * 7f + Vector3.up * 3f,
                    new Vector3(Random.Range(-400f, 400f), Random.Range(-400f, 400f), Random.Range(-400f, 400f)), 1.8f, 0.1f);
            }

            float from = transform.localScale.x;
            Vector3 at = transform.position;
            ShepherdLook.ToPhase2(transform);
            transform.localScale = Vector3.one * from;
            transform.position = at;
            if (graftCrook != null)
                graftCrook.gameObject.SetActive(false);

            var backs = new System.Collections.Generic.List<Transform>();
            foreach (SnakeLimb limb in GetComponentsInChildren<SnakeLimb>())
            {
                if (limb.name == "SnakeArm")
                    snakeArm = limb;
                else
                {
                    backs.Add(limb.transform);
                    backSnakes.Add(limb);
                }
            }
            if (snakeArm != null)
                snakeArm.transform.localScale = Vector3.one * 0.3f;
            foreach (Transform b in backs)
                b.localScale = Vector3.zero;

            const float Seconds = 0.9f;
            for (float t = 0f; t < Seconds; t += Time.deltaTime)
            {
                float f = Mathf.SmoothStep(0f, 1f, t / Seconds);
                float s = Mathf.Lerp(from, ShepherdLook.Phase2Scale, f);
                transform.localScale = Vector3.one * s;
                transform.position = at + Vector3.up * (s - from);
                if (snakeArm != null)
                    snakeArm.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, f);
                for (int i = 0; i < backs.Count; i++)
                    backs[i].localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t / Seconds - 0.15f * i) / 0.6f));
                anim.BodyPitchOffset = Mathf.Lerp(0f, -ShepherdLook.Stoop, f);
                yield return null;
            }
            transform.localScale = Vector3.one * ShepherdLook.Phase2Scale;
            transform.position = at + Vector3.up * (ShepherdLook.Phase2Scale - from);
            if (snakeArm != null)
                snakeArm.transform.localScale = Vector3.one;
            foreach (Transform b in backs)
                b.localScale = Vector3.one;
            anim.BodyPitchOffset = -ShepherdLook.Stoop;
            Phase = 2;
            StartPhaseTwoTimers();
            Physics.SyncTransforms();
        }

        // Dark blood spraying out from a wound, mostly in one direction.
        private void Spray(Vector3 from, Vector3 toward, int drops, float scale)
        {
            for (int i = 0; i < drops; i++)
            {
                GameObject drop = RuntimePrimitives.Create(PrimitiveType.Sphere, null, Blood);
                drop.transform.position = from;
                drop.transform.localScale = Vector3.one * Random.Range(0.05f, 0.13f) * scale;
                Vector3 v = (toward.normalized + Random.insideUnitSphere * 0.9f).normalized * Random.Range(4f, 11f) + Vector3.up * Random.Range(1f, 5f);
                Debris.Throw(drop, v, Vector3.zero, Random.Range(1.2f, 2.2f), 0.03f);
            }
        }

        // A copy keeps its shapes but not the colours set on the original's renderers (they live in
        // per-renderer property blocks): carry them across, renderer by renderer.
        private static void CopyColours(GameObject from, GameObject to)
        {
            Renderer[] a = from.GetComponentsInChildren<Renderer>(true);
            Renderer[] b = to.GetComponentsInChildren<Renderer>(true);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                a[i].GetPropertyBlock(block);
                b[i].SetPropertyBlock(block);
            }
        }

        private static Transform FindIn(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        /// <summary>Testing: does <paramref name="move"/> now, warnings, damage and all, cutting off whatever he was doing.</summary>
        public bool Force(string move)
        {
            if (anim == null || ShepherdAnimator.Find(ClipFor(move)) == null)
                return false;
            anim.Stop();
            queued = null;
            if (player == null)
                player = FindAnyObjectByType<PlayerStats>();
            if (player == null)
                return false;
            Begin(move);
            return true;
        }

        private void Begin(string move)
        {
            ShepherdAnimator.Clip clip = ShepherdAnimator.Find(ClipFor(move));
            FacePlayer();
            lastMove = move;
            anim.Play(clip, Pace);
            nextSwing = Time.time + clip.Duration / Pace + Breather;

            // Moves whose warning goes down at the start.
            switch (move)
            {
                case "Slam":
                {
                    Vector3 at = transform.position + transform.forward * 1.8f * Scale;
                    StartCoroutine(GroundTelegraph.Run(at, 1.3f * Scale, clip.Hits[0] / Pace, DamageType.Physical,
                        c => HitInside(c, 1.3f * Scale, 1.4f)));
                    break;
                }
                case "Bite":
                    SnakeBite(clip.Hits[0] / Pace);
                    break;
                case "Snatch":
                    if (snakeArm != null)
                    {
                        // Down into the ground just ahead of him, and held there while it tunnels.
                        Vector3 into = transform.position + transform.forward * 2f * Scale;
                        into.y = Debris.GroundBelow(into + Vector3.up) - 2f * Scale;
                        float plunge = clip.Hits[0] / Pace;
                        snakeArm.Strike(into, plunge - 0.1f, 0.1f, 1.2f, 0.3f, null);
                    }
                    break;
                case "DoubleBite":
                    SnakeBite(clip.Hits[0] / Pace);
                    StartCoroutine(SecondBite(0.45f / Pace, (clip.Hits[1] - 0.45f) / Pace));
                    break;
                case "CobraLunge":
                {
                    Vector3 from = transform.position;
                    Vector3 dir = transform.forward;
                    float length = LungeLength * Grow + 0.8f * Scale;
                    StartCoroutine(GroundTelegraph.RunLine(from, dir, length, LungeWidth * Scale, clip.Hits[0] / Pace, DamageType.Physical,
                        () => HitAlong(from, dir, length, LungeWidth * Scale * 0.5f + 0.3f, 1.8f)));
                    break;
                }
            }
        }

        // The snake arm strikes where the player is now (as far as it reaches), its jaws snapping
        // shut <paramref name="lands"/> seconds from now.
        private void SnakeBite(float lands)
        {
            if (snakeArm == null || player == null)
                return;
            Vector3 to = Flat(player.transform.position - transform.position);
            float reach = BiteReach * Scale;
            if (to.magnitude > reach)
                to = to.normalized * reach;
            // Right under him the snake can't reach down and back: it bites the ground just ahead.
            float least = BiteMin * Scale;
            if (to.magnitude < least)
                to = (to.sqrMagnitude > 0.01f ? to.normalized : transform.forward) * least;
            Vector3 spot = transform.position + to;
            spot.y = player.transform.position.y;
            // No warning on the ground: the coil and the lunge read clearly enough.
            float radius = BiteRadius * Scale;
            snakeArm.Strike(spot + Vector3.up * 0.3f, lands - BiteShoot, BiteShoot, 0.12f, 0.25f,
                mouth =>
                {
                    if (Audio.AudioManager.Instance != null)
                        Audio.AudioManager.Instance.PlayRandomAtPoint(Audio.AudioManager.Instance.meleeHit, mouth);
                    HitInside(mouth, radius + 0.3f, 1.0f);
                });
        }

        private IEnumerator SecondBite(float after, float lands)
        {
            yield return new WaitForSeconds(after);
            if (Phase >= 2 && anim != null && anim.Current != null && anim.Current.Name == "DoubleBite")
                SnakeBite(lands);
        }

        // Blows that land on the animation's own hit frames.
        private void OnHit(ShepherdAnimator.Clip clip, int index)
        {
            if (player == null || player.IsDead)
                return;
            switch (clip.Name)
            {
                case "Sweep": HitInCone(Reach * Scale, 75f, 1f); break;
                case "Jab": HitInCone((Reach + 0.4f) * Scale, 20f, 1.2f); break;
                case "HookPull":
                    if (index == 0 && HitInCone(HookReach * Scale, 22f, 0.6f))
                        Yank();
                    break;
                case "SerpentCall":
                    if (lastMove == "Spouts")
                        Spouts();
                    else if (lastMove == "SerpentDive")
                        SerpentDive();
                    else
                        CallSerpents();
                    break;
                case "Spit": VenomWave(); break;
                case "Burrow": StartCoroutine(Snatch()); break;
                case "Graft": GraftStep(index); break;
            }
        }

        // The hook caught: the player is dragged in to just in front of him, and the Sweep follows.
        private void Yank()
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            Vector3 to = Flat(transform.position - player.transform.position);
            float gap = 1.6f * Scale;
            if (pc != null && to.magnitude > gap)
                pc.Dash(to, to.magnitude - gap, 0.22f);
            queued = "Sweep";
        }

        // Snakes burst up under the player and round them, and one or two between him and them.
        private void CallSerpents()
        {
            Vector3 target = player.transform.position;
            target.y = transform.position.y;
            var spots = new System.Collections.Generic.List<Vector3> { target };
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < 4; i++)
                spots.Add(target + Quaternion.Euler(0f, turn + i * 90f, 0f) * Vector3.forward * 3.6f * Grow);
            spots.Add(Vector3.Lerp(transform.position, target, 0.5f));

            foreach (Vector3 spot in spots)
            {
                Vector3 at = spot;
                StartCoroutine(GroundTelegraph.Run(at, SnakeRadius * Scale, SnakeWindUp, DamageType.Physical, c =>
                {
                    Vector3 strike = player != null ? player.transform.position - c : Vector3.forward;
                    // Taller with him, but not so tall they wall off the view.
                    GroundSnake.Spawn(c, strike, 3.2f * ShepherdLook.BaseScale * Mathf.Sqrt(Grow));
                    HitInside(c, SnakeRadius * Scale + 0.2f, 1.1f);
                }));
            }
        }

        private bool HitInCone(float reach, float halfAngle, float multiplier)
        {
            Vector3 to = Flat(player.transform.position - transform.position);
            if (to.magnitude > reach || Vector3.Angle(transform.forward, to) > halfAngle)
                return false;
            Damage(multiplier);
            return true;
        }

        private void HitInside(Vector3 centre, float radius, float multiplier, bool attack = true)
        {
            if (player != null && !player.IsDead && Flat(player.transform.position - centre).magnitude <= radius)
                Damage(multiplier, attack);
        }

        private void HitAlong(Vector3 from, Vector3 dir, float length, float halfWidth, float multiplier)
        {
            if (player == null || player.IsDead)
                return;
            Vector3 to = Flat(player.transform.position - from);
            float along = Vector3.Dot(to, dir);
            if (along >= -0.5f && along <= length && Vector3.Cross(dir, to).magnitude <= halfWidth)
                Damage(multiplier);
        }

        // From phase 2 on, everything he hits with is venomous: each hit also leaves a poison stack.
        private const float PoisonShare = 0.4f;
        private const float PoisonSeconds = 3f;

        private void Damage(float multiplier, bool attack = true)
        {
            float hit = BaseHit * multiplier;
            if (player.TakeHit(hit, kind.DamageType, attack) && Phase >= 2 && !player.IsDead)
                player.Poison(hit * PoisonShare, PoisonSeconds);
        }

        private void FacePlayer()
        {
            Vector3 to = Flat(player.transform.position - transform.position);
            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(to);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
