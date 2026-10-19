using System.Collections.Generic;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.CameraSystem;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network.Replication;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Spectator-only. Turns this tab's copy of the game scene into a puppet of the real player's
    /// game: every gameplay system (input, AI, combat, spawning, revive) is switched off, and the
    /// player object plus one enemy puppet per replicated enemy are posed each frame from the
    /// <see cref="SnapshotTimeline"/>. Discrete events (swings, hits, deaths, area switches,
    /// loading fades) replay exactly when the playback clock reaches the snapshot that reported
    /// them, so they line up with the interpolated motion. Leg/arm walk cycles need no
    /// replication at all: CharacterWalkAnimator derives them from how fast the puppet moves.
    /// A co-op guest uses the enemy half of it (<see cref="EnterCoopGuest"/>): the host's
    /// enemies, puppeted the same way, but solid and hittable, with the guest's own character
    /// left fully in play.
    /// </summary>
    public class SpectatorReplica : MonoBehaviour
    {
        // Puppets that drop out of the interest area are hidden; if they stay gone this long they
        // are destroyed (and re-created from the next snapshot that mentions them).
        private const float ForgetAfterSeconds = 10f;

        private class Puppet
        {
            public GameObject Root;
            public EnemyHealth Health;
            public CharacterAttackAnimator Attack;
            public Stagger Stagger;
            public CharacterWalkAnimator Walk;
            public EntityState Applied;   // last discrete state applied (counters, death, chase)
            public bool HasPose;
            public Vector3 LastPosition;
            public float LastSeen;
            public int BossLookPhase = 1;
        }

        private SnapshotTimeline timeline = new SnapshotTimeline();

        // Co-op guest: only the host's enemies are puppeted (see EnterCoopGuest).
        private bool coopGuest;
        private bool minionsOnly;
        private const float CoopMinDelay = 0.05f;
        private const float CoopSmoothingRate = 25f;
        private const float CoopSmoothingMaxError = 1.5f;
        private readonly Dictionary<int, Puppet> puppets = new Dictionary<int, Puppet>();
        private readonly Dictionary<int, LootDrop> loot = new Dictionary<int, LootDrop>();
        private readonly Dictionary<int, GearItem> groundItems = new Dictionary<int, GearItem>(); // full stats by drop id
        private LootDrop pointedLoot;
        private int pointedLootId = -1;
        private ItemData pointedLootItem;
        private readonly HashSet<int> lootSeen = new HashSet<int>();
        private readonly List<int> lootGone = new List<int>();
        private readonly List<StateSnapshot> due = new List<StateSnapshot>();
        private readonly List<int> scratchIds = new List<int>();
        private readonly EntityState scratch = new EntityState();
        private readonly HashSet<int> seenThisFrame = new HashSet<int>();

        private bool active;
        private int currentPid = int.MinValue;

        // The real player object, puppeted.
        private PlayerStats playerStats;
        private PlayerInventory playerInventory;
        private CharacterAttackAnimator playerAttack;
        private CharacterWalkAnimator playerWalk;
        private Stagger playerStagger;
        private PlayerHUD hud;
        private int extraArrows;    // the watched player's extra arrows per bow shot
        private CameraFollow cameraFollow;
        private LoadingScreenUI loadingScreen;
        private GameObject enemyPrefab;
        private Transform puppetParent;

        private EntityState playerApplied;
        private int castsApplied = -1;   // the player's newest skill cast drawn so far (-1: not joined yet)
        private bool playerHasPose;
        private Vector3 playerLastPosition;
        private int appliedLevel;
        private int appliedFade;
        private Coroutine fadeRoutine;

        // Gear/menu replication is independent of interpolated scene motion.
        private SpectatorMenuReplica menus;

        public bool IsActive => active;

        /// <summary>The watched player is on a phone or tablet (touch controls).</summary>
        public bool WatchedOnTouch => timeline.Newest != null && timeline.Newest.dev != 0;

        /// <summary>True once at least one snapshot of the current player session is buffered.</summary>
        public bool HasLiveData => timeline.HasData;

        /// <summary>Seconds since the last snapshot arrived (infinite if none yet).</summary>
        public double SecondsSinceLastSnapshot => timeline.SecondsSinceLastArrival(Time.realtimeSinceStartupAsDouble);

        public float BufferDelay => timeline.InterpolationDelay;

        /// <summary>Switches this scene into puppet mode. Safe to call more than once.</summary>
        public void Enter()
        {
            if (active)
                return;
            active = true;

            playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerStats != null)
            {
                GameObject player = playerStats.gameObject;
                SetEnabled(player.GetComponent<PlayerController>(), false);
                SetEnabled(player.GetComponent<PlayerCombat>(), false);
                SetEnabled(player.GetComponent<LootPicker>(), false);
                SetEnabled(player.GetComponent<Skills.PlayerSkills>(), false);
                SetEnabled(player.GetComponent<PlayerPotions>(), false);
                SetEnabled(player.GetComponent<NpcInteractor>(), false);
                SetEnabled(player.GetComponent<Quests.QuestLog>(), false);
                SetEnabled(player.GetComponent<PlayerPassives>(), false);
                SetEnabled(player.GetComponent<TownPortal>(), false);
                // No local revive countdown / "press any key" handling - the real player's values come in via ApplyReplicatedState.
                playerStats.enabled = false;

                var link = player.GetComponent<PlayerStatsLink>();
                if (link != null)
                    Destroy(link); // would re-add gear bonuses on top of the already-totalled replicated stats

                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false; // puppets are positioned directly; also keeps them out of AreaGate triggers

                playerInventory = player.GetComponent<PlayerInventory>();
                playerAttack = player.GetComponentInChildren<CharacterAttackAnimator>();
                playerWalk = player.GetComponentInChildren<CharacterWalkAnimator>();
                playerStagger = player.GetComponent<Stagger>();
                if (playerStagger == null)
                    playerStagger = player.AddComponent<Stagger>();

                // A replayed bow shot looses a harmless arrow, the way the player's did.
                if (playerAttack != null)
                    playerAttack.StrikeFrame += OnPlayerStrike;
            }

            // The menus show the watched player's (read-only), from the gear messages.
            SpectatorMirror.Active = true;
            SpectatorMirror.ClearRemote();
            hud = FindAnyObjectByType<PlayerHUD>();
            if (hud != null)
            {
                hud.SpectatorMode = true;
                hud.enabled = false; // until there's a player to show
            }

            Camera cam = Camera.main;
            cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            loadingScreen = FindAnyObjectByType<LoadingScreenUI>();

            ReplaceLocalEnemies();

            menus = new SpectatorMenuReplica(playerStats, playerInventory,
                FindAnyObjectByType<InventoryUI>(), UpdateGroundItems);
        }

        /// <summary>
        /// Co-op guest: this game's enemies give way to copies of the host's, posed from the
        /// host's snapshots (<see cref="HandleCoopState"/>). The guest's own character, menus and
        /// HUD stay its own. The ground's loot is the host's too (see CoopSession).
        /// </summary>
        public void EnterCoopGuest()
        {
            if (active)
                return;
            active = true;
            coopGuest = true;
            timeline = new SnapshotTimeline(CoopMinDelay);
            var spawner = FindAnyObjectByType<EnemySpawner>();
            enemyPrefab = spawner != null ? spawner.EnemyPrefab : null;
            puppetParent = transform;
        }

        public void PlayCoopMove(CoopEvent e)
        {
            if (!coopGuest || !Party.SharingArea) return;
            Puppet puppet = GetOrCreatePuppet(new EntityState { i = e.id, k = e.ek,
                x = e.x, y = e.y, z = e.z, r = e.r, hp = 1f, mhp = 1f });
            if (puppet == null || puppet.Health.IsDead) return;
            puppet.Root.transform.SetPositionAndRotation(new Vector3(e.x, e.y, e.z), Quaternion.Euler(0f, e.r, 0f));
            bool atUs = (e.f & CoopProtocol.FlagAtYou) != 0;
            if (e.k == CoopProtocol.Skill)
                puppet.Root.GetComponent<EnemySkills>()?.Mirror(new Vector3(e.tx, e.ty, e.tz), e.a, atUs);
            else
                puppet.Root.GetComponent<BossAbilities>()?.PlayMirror(e.mv, atUs, e.sd, e.sp, e.lv);
        }

        /// <summary>Co-op guest: this game's copy of one of the host's enemies, or null.</summary>
        public EnemyHealth PuppetHealth(int id)
        {
            return puppets.TryGetValue(id, out Puppet puppet) && puppet.Root != null ? puppet.Health : null;
        }

        public void HandleCoopState(StateSnapshot s)
        {
            if (active && coopGuest && s != null)
            {
                var entities = new List<EntityState>();
                foreach (EntityState e in s.e ?? System.Array.Empty<EntityState>())
                    if (e != null && (e.i >= PoeClone.Skills.Minion.ReplicationIdBase) == minionsOnly)
                        entities.Add(e);
                timeline.Add(new StateSnapshot { seq = s.seq, hak = s.hak, t = s.t, area = s.area, e = entities.ToArray() }, Time.realtimeSinceStartupAsDouble);
            }
        }

        public void EnterCoopMinions()
        {
            EnterCoopGuest();
            minionsOnly = true;
        }

        public void ClearCoopEnemies()
        {
            timeline.Clear();
            foreach (Puppet puppet in puppets.Values)
                if (puppet.Root != null) Destroy(puppet.Root);
            puppets.Clear();
        }

        // No spawning and none of this game's own enemies or loot: what's shown comes from the stream.
        private void ReplaceLocalEnemies()
        {
            var spawner = FindAnyObjectByType<EnemySpawner>();
            foreach (var each in FindObjectsByType<EnemySpawner>())
                SetEnabled(each, false); // no respawning of this tab's own enemies
            enemyPrefab = spawner != null ? spawner.EnemyPrefab : null;
            puppetParent = spawner != null ? spawner.transform : transform;
            if (enemyPrefab == null)
                Debug.LogWarning("SpectatorReplica: no EnemySpawner prefab found - enemies won't be shown.");

            // This tab's own randomly spawned enemies (and its own starter loot) have nothing to do
            // with the player's; the player's loot arrives in the snapshots.
            foreach (var enemy in FindObjectsByType<EnemyHealth>())
                Destroy(enemy.gameObject);
            foreach (var drop in new List<LootDrop>(LootDrop.All))
                Destroy(drop.gameObject);
        }

        private void UpdateCoopGuest()
        {
            // Anything that spawns enemies of its own here (a quest ambush, a boss lair) is the
            // host's to run: its copies arrive in the stream.
            if (!minionsOnly && !Party.SharingArea)
                return;

            if (!timeline.HasData)
                return;
            if (minionsOnly && AreaManager.Instance != null && timeline.Newest.area != AreaManager.Instance.CurrentAreaIndex)
            {
                foreach (Puppet puppet in puppets.Values)
                    if (puppet.Root != null) puppet.Root.SetActive(false);
                return;
            }
            timeline.Advance(Time.realtimeSinceStartupAsDouble);
            due.Clear();
            timeline.CollectDue(due);
            foreach (StateSnapshot s in due)
            {
                foreach (EntityState e in s.e)
                {
                    if (e == null)
                        continue;
                    Puppet puppet = GetOrCreatePuppet(e);
                    if (puppet != null)
                        ApplyEnemyEvents(puppet, e, s.hak, s.seq);
                }
            }

            if (timeline.TryGetFrame(out StateSnapshot from, out StateSnapshot to, out float alpha))
                PoseEnemies(from, to, alpha);
        }

        /// <summary>Feeds one raw "state" message from the server.</summary>
        public void HandleState(string json)
        {
            if (!active)
                return;

            StateSnapshot s = SnapshotCodec.Deserialize(json);
            if (s == null)
                return;

            if (s.pid != currentPid)
            {
                // A different player session (or the first one we've seen): nothing carries over.
                ResetReplica();
                currentPid = s.pid;
            }

            timeline.Add(s, Time.realtimeSinceStartupAsDouble);
        }

        /// <summary>Feeds the independent menu replica a raw gear message.</summary>
        public void HandleGear(string json)
        {
            if (active) menus?.Receive(json);
        }

        /// <summary>The player left / the connection dropped: clear the stage.</summary>
        public void ResetReplica()
        {
            timeline.Clear();
            currentPid = int.MinValue;
            if (menus != null) menus.Reset();
            else SpectatorMirror.ClearRemote();

            foreach (var puppet in puppets.Values)
            {
                if (puppet.Root != null)
                    Destroy(puppet.Root);
            }
            puppets.Clear();

            foreach (LootDrop drop in loot.Values)
            {
                if (drop != null)
                    Destroy(drop.gameObject);
            }
            loot.Clear();

            if (playerStats != null && playerApplied != null && playerApplied.d != 0)
                CharacterDeathAnimator.ResetOn(playerStats.transform);

            playerApplied = null;
            castsApplied = -1;
            playerHasPose = false;
            appliedLevel = 0;

            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            if (appliedFade != 0 && loadingScreen != null)
                fadeRoutine = StartCoroutine(loadingScreen.FadeOut());
            appliedFade = 0;

            if (hud != null)
                hud.enabled = false;
        }

        private void Update()
        {
            if (!active)
                return;
            if (coopGuest)
            {
                UpdateCoopGuest();
                return;
            }

            UpdateLootPointer();

            // Applied as soon as it arrives (it isn't part of the interpolated motion).
            menus?.ApplyPending(currentPid);

            if (!timeline.HasData)
                return;

            timeline.Advance(Time.realtimeSinceStartupAsDouble);

            due.Clear();
            timeline.CollectDue(due);
            for (int k = 0; k < due.Count; k++)
                ApplyDiscrete(due[k]);

            if (!timeline.TryGetFrame(out StateSnapshot from, out StateSnapshot to, out float alpha))
                return;

            // Very first data of a session: make sure HUD/gear/area are right immediately
            // rather than after the interpolation delay.
            if (playerApplied == null)
                ApplyDiscrete(from);

            if (hud != null && !hud.enabled)
                hud.enabled = true;

            PosePlayer(from, to, alpha);
            PoseEnemies(from, to, alpha);
        }

        // ---------------------------------------------------------------- discrete events

        private void ApplyDiscrete(StateSnapshot s)
        {
            if (AreaManager.Instance != null)
                AreaManager.Instance.ApplyAreaImmediate(s.area, s.p != null ? new Vector3(s.p.x, s.p.y, s.p.z) : (Vector3?)null);

            ApplyFade(s.fade);
            ApplyHud(s.hud);
            if (menus != null && !menus.HasGearFor(currentPid))
                menus.ApplyEquipment(s.eq); // a player without gear messages: worn gear by look only
            SpectatorMenuReplica.ApplyUi(s.ui);
            ApplyPlayerEvents(s.p);
            ApplyCasts(s.sc, s.p);

            for (int k = 0; k < s.e.Length; k++)
            {
                EntityState e = s.e[k];
                if (e == null)
                    continue;
                Puppet puppet = GetOrCreatePuppet(e);
                if (puppet != null)
                    ApplyEnemyEvents(puppet, e);
            }

            ApplyLoot(s.l);
        }

        // Items on the ground: shown as they appear, removed once the player's snapshot stops
        // listing them (picked up, expired, or out of range).
        private void ApplyLoot(LootState[] states)
        {
            lootSeen.Clear();
            if (states != null)
            {
                foreach (LootState l in states)
                {
                    if (l == null)
                        continue;
                    lootSeen.Add(l.i);
                    if (loot.ContainsKey(l.i))
                        continue;

                    ItemData item = ItemGenerator.Display(l.b, l.n, (ItemRarity)l.q);
                    if (item != null)
                        loot[l.i] = LootDrop.Spawn(item, new Vector3(l.x, l.y, l.z), interactive: false, id: l.i);
                }
            }

            lootGone.Clear();
            foreach (var pair in loot)
            {
                if (!lootSeen.Contains(pair.Key))
                    lootGone.Add(pair.Key);
            }
            foreach (int id in lootGone)
            {
                if (loot[id] != null)
                    Destroy(loot[id].gameObject);
                loot.Remove(id);
            }
        }

        // The spectator's own mouse on an item on the ground: outlined, and its tooltip (with the
        // stats from the gear message) shown by InventoryUI, like the player's own hover.
        private void UpdateGroundItems(GearItem[] items)
        {
            groundItems.Clear();
            pointedLootItem = null; // re-read on the next frame, in case its stats just arrived
            if (items != null)
            {
                foreach (GearItem item in items)
                {
                    if (item != null)
                        groundItems[item.x] = item;
                }
            }

        }

        private void UpdateLootPointer()
        {
            LootDrop drop = null;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && !TouchMode.Active && !PlayerController.IsPointerOverUi())
            {
                Vector2 at = mouse.position.ReadValue();
                drop = LootDrop.FindAtScreen(at, displayOnly: true);
                if (drop != null && LootPicker.EnemyUnderPointer(at))
                    drop = null;
            }

            if (drop != pointedLoot)
            {
                if (pointedLoot != null)
                    pointedLoot.SetHighlighted(false);
                if (drop != null)
                    drop.SetHighlighted(true);
                pointedLoot = drop;
                pointedLootId = -1;
            }

            if (drop == null)
            {
                InventoryUI.GroundHover = null;
                return;
            }

            // Its full stats once the gear message has them (only the name and look before that).
            if (pointedLootId != drop.Id || pointedLootItem == null)
            {
                pointedLootId = drop.Id;
                pointedLootItem = groundItems.TryGetValue(drop.Id, out GearItem full) ? GearCodec.ToItem(full) : null;
            }
            InventoryUI.GroundHover = pointedLootItem ?? drop.Item;
            InventoryUI.GroundHoverAt = drop.transform.position;
        }

        // The player's skills: each new cast's look (harmless bolts, rings, lightning) drawn on this
        // tab's copy of the player. Joining mid-fight skips the casts already under way.
        private void ApplyCasts(SkillCastState[] casts, EntityState p)
        {
            if (casts == null || playerStats == null)
                return;

            int newest = castsApplied;
            foreach (SkillCastState c in casts)
            {
                if (c != null && c.n > newest)
                    newest = c.n;
            }

            if (castsApplied < 0 || (p != null && p.d != 0))
            {
                castsApplied = Mathf.Max(0, Mathf.Max(castsApplied, newest));
                return;
            }

            foreach (SkillCastState c in casts)
            {
                if (c == null || c.n <= castsApplied)
                    continue;

                Vector3[] points = null;
                if (c.pts != null && c.pts.Length >= 6)
                {
                    points = new Vector3[c.pts.Length / 3];
                    for (int k = 0; k < points.Length; k++)
                        points[k] = new Vector3(c.pts[k * 3], c.pts[k * 3 + 1], c.pts[k * 3 + 2]);
                }

                Skills.PlayerSkills.PlayVisual(new Skills.PlayerSkills.CastRecord
                {
                    Number = c.n,
                    Skill = (Skills.SkillId)c.s,
                    Level = c.lv,
                    At = new Vector3(c.x, c.y, c.z),
                    Facing = new Vector3(c.dx, 0f, c.dz),
                    Size = c.sz,
                    Count = c.c,
                    Points = points
                }, playerStats.transform);
            }
            castsApplied = newest;
        }

        private void OnPlayerStrike()
        {
            if (active && playerStats != null && playerAttack != null &&
                CharacterAttackAnimator.IsRangedProfile(playerAttack.ProfileId))
            {
                Transform shooter = playerStats.transform;
                float range = CharacterAttackAnimator.AttackRange(WeaponType.Bow);
                foreach (Vector3 direction in HitEffects.Spread(shooter.forward, 1 + extraArrows, PlayerCombat.ArrowSpreadDegrees))
                    PlayerArrow.LaunchVisual(shooter, range, direction);
            }
        }

        private void ApplyFade(int fade)
        {
            if (fade == appliedFade || loadingScreen == null)
                return;

            appliedFade = fade;
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(fade != 0 ? loadingScreen.FadeIn() : loadingScreen.FadeOut());
        }

        private void ApplyHud(PlayerHudState h)
        {
            if (h == null || playerStats == null)
                return;

            extraArrows = h.arw;

            if (appliedLevel > 0 && h.lv > appliedLevel)
                PlaySfx(AudioManager.Instance != null ? AudioManager.Instance.playerLevelUp : null, playerStats.transform.position);
            bool levelChanged = h.lv != appliedLevel;
            appliedLevel = h.lv;

            playerStats.ApplyReplicatedState(h.lv, h.xp, h.str, h.dex, h.itl, h.hp, h.mhp, h.mp, h.mmp,
                h.dead != 0, h.cd, h.rv != 0);

            // Skills can only be slotted once unlocked, which goes by level.
            if (levelChanged)
                menus?.RefreshSkills(currentPid);
        }

        private void ApplyPlayerEvents(EntityState p)
        {
            if (playerStats == null || p == null)
                return;

            EntityState prev = playerApplied;
            playerApplied = p.Clone();
            playerAttack?.ApplyReplicatedHold(p.ap, p.ah != 0 && p.d == 0);

            if (prev == null)
            {
                // Joined mid-session: adopt the current state without replaying old events.
                if (p.d != 0)
                    CharacterDeathAnimator.PlayOn(playerStats.transform);
                return;
            }

            Vector3 at = playerStats.transform.position;

            if (p.d != 0 && prev.d == 0)
            {
                CharacterDeathAnimator.PlayOn(playerStats.transform);
            }
            else if (p.d == 0 && prev.d != 0)
            {
                CharacterDeathAnimator.ResetOn(playerStats.transform);
                playerHasPose = false; // revive teleports to spawn: snap, don't slide
            }

            if (p.d == 0 && p.ah == 0 && p.atk > prev.atk && playerAttack != null)
            {
                playerAttack.PlayReplicated(p.ap);
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.playerSwing) : null, at);
            }

            if (p.stg > prev.stg && p.d == 0)
            {
                playerStagger.Trigger();
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.playerHurt) : null, at);
            }
        }

        private void ApplyEnemyEvents(Puppet puppet, EntityState e, int processedHit = 0, int snapshotSequence = 0)
        {
            EntityState prev = puppet.Applied;
            puppet.Applied = e.Clone();
            if (prev == null || puppet.Health == null)
                return;

            Vector3 at = puppet.Root.transform.position;
            puppet.Health.ApplyReplicatedHealth(e.hp, e.mhp, processedHit, snapshotSequence);
            puppet.Health.HideBossBar = e.bh != 0;

            if (puppet.Health.IsDead)
                return;

            if (e.d != 0)
            {
                // Topples from its last interpolated (still standing) pose. Deliberately not snapped
                // to e's position: the real body may already be part-way through its fall in this
                // snapshot, and the local collapse would then sink it twice as far.
                puppet.Health.ApplyReplicatedDeath(instant: false);
                EnemySounds.Play(EnemyKinds.Get(e.k), EnemySounds.Event.Death, at);
                return;
            }

            if (e.ch != 0 && prev.ch == 0)
                EnemySounds.Play(EnemyKinds.Get(e.k), EnemySounds.Event.Aggro, at);

            if (e.atk > prev.atk && puppet.Attack != null)
            {
                puppet.Attack.PlayReplicated(e.ap);
                if (EnemyKinds.Get(e.k).IsCreature)
                    EnemySounds.Play(EnemyKinds.Get(e.k), EnemySounds.Event.Attack, at);
            }

            if (e.sk > prev.sk && !coopGuest)
                EnemySkills.PlayVisual(this, EnemyKinds.Get(e.k).WithRarity((EnemyRarity)e.er), puppet.Root.transform, new Vector3(e.sx, puppet.Root.transform.position.y, e.sz), e.sk);

            if (e.stg > prev.stg && puppet.Stagger != null)
            {
                puppet.Stagger.Trigger();
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.meleeHit) : null, at);
            }

            ApplyBossVisual(puppet, e, prev, false);
        }

        private static void ApplyBossVisual(Puppet puppet, EntityState state, EntityState previous, bool created)
        {
            if (puppet == null || puppet.Root == null || state == null || state.bp <= 0 ||
                EnemyKinds.Get(state.k).Boss != BossStyle.Shepherd)
                return;

            Transform root = puppet.Root.transform;
            Transform model = root.Find("Model") ?? root;
            Transform saintRig = model.Find(CarrionSaintLook.RigName);
            int oldPhase = previous != null ? previous.bp : 1;

            bool phaseTwoAppearance = state.bp >= 2 || state.bs > ShepherdLook.BaseScale + 0.02f;
            if (phaseTwoAppearance && puppet.BossLookPhase < 2 && saintRig == null)
            {
                ShepherdLook.ToPhase2(root);
                puppet.BossLookPhase = 2;
            }

            if (state.bp >= 3 && saintRig == null)
            {
                bool replayReveal = !created && oldPhase < 3;
                saintRig = CarrionSaintLook.Build(root);
                puppet.BossLookPhase = 3;
                var saintAnimator = saintRig.GetComponent<CarrionSaintAnimator>();
                if (replayReveal)
                {
                    CarrionSaintReveal reveal = root.GetComponent<CarrionSaintReveal>();
                    if (reveal == null) reveal = root.gameObject.AddComponent<CarrionSaintReveal>();
                    reveal.BeginReplica();
                }
                else if (saintAnimator != null)
                    saintAnimator.Reveal = 1f;
                puppet.Health?.RevealBossHealth("Carrion Saint");
            }

            if (state.bp < 3)
            {
                ShepherdAnimator animator = model.GetComponent<ShepherdAnimator>();
                if (animator != null)
                {
                    if (state.ba > 0 && state.ba <= ShepherdAnimator.Clips.Length)
                        animator.Freeze(ShepherdAnimator.Clips[state.ba - 1], state.bt);
                    else if (animator.IsPlaying)
                        animator.Stop();
                }

                if (state.bl != null)
                {
                    SnakeLimb[] limbs = root.GetComponentsInChildren<SnakeLimb>();
                    for (int i = 0; i < state.bl.Length && i < limbs.Length; i++)
                    {
                        BossLimbState strike = state.bl[i];
                        if (strike != null)
                            limbs[i].transform.localScale = Vector3.one * strike.s;
                        int oldCount = previous != null && previous.bl != null && i < previous.bl.Length ? previous.bl[i].c : 0;
                        if (strike != null && strike.a != 0 && (created || strike.c != oldCount))
                            limbs[i].ReplayStrike(new Vector3(strike.x, strike.y, strike.z), strike.w, strike.l,
                                strike.h, strike.r, strike.t);
                    }
                }
            }
            else
            {
                CarrionSaintAnimator animator = model.GetComponentInChildren<CarrionSaintAnimator>();
                if (animator != null)
                {
                    int clipId = state.ba - 100;
                    if (clipId > 0 && clipId <= CarrionSaintAnimator.Clips.Length)
                    {
                        if (previous == null || state.bm != previous.bm || animator.Current != CarrionSaintAnimator.Clips[clipId - 1])
                            animator.Play(CarrionSaintAnimator.Clips[clipId - 1]);
                        animator.SeekReplicated(state.bt);
                    }
                    else if (animator.Current != null)
                        animator.Stop();
                }
            }
        }

        // ---------------------------------------------------------------- continuous poses

        private void PosePlayer(StateSnapshot from, StateSnapshot to, float alpha)
        {
            if (playerStats == null)
                return;

            SnapshotTimeline.Interpolate(from.p, to.p, alpha, scratch, (float)(to.t - from.t));
            Transform t = playerStats.transform;
            Vector3 target = new Vector3(scratch.x, scratch.y, scratch.z);

            bool teleported = !playerHasPose || (target - playerLastPosition).sqrMagnitude >
                              SnapshotTimeline.TeleportDistance * SnapshotTimeline.TeleportDistance;

            t.SetPositionAndRotation(target, Quaternion.Euler(0f, scratch.r, 0f));
            playerLastPosition = target;
            playerHasPose = true;

            if (teleported)
            {
                if (playerWalk != null && playerWalk.enabled)
                    playerWalk.ResetAnimatorState();
                if (cameraFollow != null)
                    cameraFollow.SnapToTarget();
            }
        }

        private void PoseEnemies(StateSnapshot from, StateSnapshot to, float alpha)
        {
            seenThisFrame.Clear();
            float now = Time.unscaledTime;

            for (int k = 0; k < from.e.Length; k++)
            {
                EntityState a = from.e[k];
                if (a == null)
                    continue;

                Puppet puppet = GetOrCreatePuppet(a);
                if (puppet == null)
                    continue;

                seenThisFrame.Add(a.i);
                puppet.LastSeen = now;

                // Corpses are posed by their own local collapse once dead.
                if (puppet.Health != null && puppet.Health.IsDead)
                    continue;

                SnapshotTimeline.Interpolate(a, SnapshotTimeline.FindEnemy(to, a.i) ?? a, alpha, scratch);
                Vector3 target = new Vector3(scratch.x, scratch.y, scratch.z);
                bool teleported = !puppet.HasPose || (target - puppet.LastPosition).sqrMagnitude >
                                  SnapshotTimeline.TeleportDistance * SnapshotTimeline.TeleportDistance;

                Quaternion facing = Quaternion.Euler(0f, scratch.r, 0f);
                Transform body = puppet.Root.transform;
                Vector3 error = target - body.position;
                if (coopGuest && puppet.Root.GetComponent<BossAbilities>() is BossAbilities boss && boss.MirrorBusy)
                    continue;
                if (coopGuest && !teleported && error.sqrMagnitude < CoopSmoothingMaxError * CoopSmoothingMaxError)
                {
                    // Co-op runs ahead of the newest snapshot (extrapolating); a guess that turned
                    // out wrong is eased in over a few frames instead of snapping.
                    float blend = 1f - Mathf.Exp(-CoopSmoothingRate * Time.deltaTime);
                    body.SetPositionAndRotation(body.position + error * blend, Quaternion.Slerp(body.rotation, facing, blend));
                }
                else
                {
                    body.SetPositionAndRotation(target, facing);
                }
                if (scratch.bs > 0f)
                    puppet.Root.transform.localScale = Vector3.one * scratch.bs;
                puppet.LastPosition = target;
                puppet.HasPose = true;

                if (teleported && puppet.Walk != null && puppet.Walk.enabled)
                    puppet.Walk.ResetAnimatorState();
            }

            scratchIds.Clear();
            foreach (var pair in puppets)
            {
                if (seenThisFrame.Contains(pair.Key))
                    continue;

                Puppet puppet = pair.Value;
                if (puppet.Root == null || now - puppet.LastSeen > ForgetAfterSeconds)
                    scratchIds.Add(pair.Key);
                else if (puppet.Root.activeSelf && !(puppet.Health != null && puppet.Health.IsDead))
                    puppet.Root.SetActive(false); // left the area around the player
            }

            foreach (int id in scratchIds)
            {
                if (puppets.TryGetValue(id, out Puppet gone) && gone.Root != null)
                    Destroy(gone.Root);
                puppets.Remove(id);
            }
        }

        private Puppet GetOrCreatePuppet(EntityState e)
        {
            if (puppets.TryGetValue(e.i, out Puppet existing))
            {
                if (existing.Root == null)
                    return null;

                // Back in the interest area. Reactivate before any event is applied to it: a
                // death arriving in the same snapshot starts a coroutine, which an inactive
                // object can't run.
                if (!existing.Root.activeSelf && (!DistanceActivity.IsSleeping(existing.Root) || e.d != 0))
                {
                    DistanceActivity.SetAvailable(existing.Root, true);
                    existing.Root.GetComponent<DistanceActivity>()?.KeepAwake();
                    existing.HasPose = false;
                }
                return existing;
            }

            if (enemyPrefab == null)
                return null;

            Vector3 position = new Vector3(e.x, e.y, e.z);
            Quaternion rotation = Quaternion.Euler(0f, e.r, 0f);
            GameObject go = Instantiate(enemyPrefab, position, rotation, puppetParent);
            go.name = $"EnemyPuppet_{e.i}";

            EnemyKind kind = EnemyKinds.Get(e.k).WithRarity((EnemyRarity)e.er);
            EnemyKinds.ApplyLook(go, kind);
            go.GetComponent<EnemyHealth>()?.SetKindIndex(e.k);
            go.GetComponent<EnemyHealth>()?.SetRarity((EnemyRarity)e.er);

            // Awake has run (adding EnemyCombat + the attack animator); Start hasn't. Switch off
            // everything that would think for itself before it gets the chance.
            SetEnabled(go.GetComponent<EnemyController>(), false);
            SetEnabled(go.GetComponent<EnemyCombat>(), false);
            SetEnabled(go.GetComponent<EnemySkills>(), false);
            SetEnabled(go.GetComponent<BossAbilities>(), false);
            SetEnabled(go.GetComponent<ShepherdFight>(), false);
            SetEnabled(go.GetComponent<CarrionSaintFight>(), false);
            // The player's own minions: a green bar like the player sees, and a mage's bolt isn't
            // aimed at the player.
            bool minion = e.i >= PoeClone.Skills.Minion.ReplicationIdBase;
            if (coopGuest && !minion)
            {
                if (kind.Skill != EnemySkill.None)
                {
                    var skills = go.GetComponent<EnemySkills>() ?? go.AddComponent<EnemySkills>();
                    skills.ConfigureMirror(kind);
                }
                if (kind.IsBoss && kind.Boss != BossStyle.Shepherd)
                {
                    var boss = go.GetComponent<BossAbilities>() ?? go.AddComponent<BossAbilities>();
                    boss.ConfigureMirror(kind);
                    boss.enabled = true;
                }
            }

            // A spectator's puppets are placed directly and need no collider. A co-op guest's
            // enemies stay solid (to walk into and to hit) and send their hits to the host; the
            // host's minions are its partner's allies, so they can't be hit at all.
            var cc = go.GetComponent<CharacterController>();
            var health = go.GetComponent<EnemyHealth>();
            if (health != null)
                health.IsAlly = minion;
            if (cc != null)
                cc.enabled = coopGuest && !minion;
            if (coopGuest && health != null)
            {
                if (minion)
                    health.enabled = false;
                else
                    health.RemoteId = e.i;
            }

            var puppet = new Puppet
            {
                Root = go,
                Health = go.GetComponent<EnemyHealth>(),
                Attack = go.GetComponentInChildren<CharacterAttackAnimator>(),
                Stagger = go.GetComponent<Stagger>(),
                Walk = go.GetComponentInChildren<CharacterWalkAnimator>(),
                Applied = e.Clone(),
                HasPose = true,
                LastPosition = position,
                LastSeen = Time.unscaledTime
            };
            if (puppet.Stagger == null)
                puppet.Stagger = go.AddComponent<Stagger>();

            if (minion)
            {
                var bar = go.GetComponent<EnemyHealthBarUI>();
                if (bar != null)
                    bar.Friendly = true;
            }

            // A caster's or archer's replayed attack sends a harmless bolt/arrow at the player. (In
            // co-op the host sends each bolt and arrow itself, aimed at whoever it was really for.)
            if (kind.IsRanged && puppet.Attack != null && !minion && !coopGuest)
            {
                Transform body = go.transform;
                puppet.Attack.StrikeFrame += () =>
                {
                    if (body == null || playerStats == null)
                        return;
                    if (kind.RainOfArrows)
                        EnemySkills.RainOfArrows(this, kind, playerStats.transform.position, null, 0f);
                    else
                        EnemyProjectile.LaunchVisual(EnemyCombat.BoltOrigin(body), playerStats.transform.position, kind);
                };
            }

            if (puppet.Health != null)
            {
                puppet.Health.ApplyReplicatedHealth(e.hp, e.mhp);
                puppet.Health.HideBossBar = e.bh != 0;
                if (e.d != 0)
                    puppet.Health.ApplyReplicatedDeath(instant: true);
            }

            ApplyBossVisual(puppet, e, null, true);

            puppets[e.i] = puppet;
            return puppet;
        }

        // ---------------------------------------------------------------- helpers

        private static void SetEnabled(Behaviour behaviour, bool value)
        {
            if (behaviour != null)
                behaviour.enabled = value;
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
        }

        private static void PlaySfx(AudioClip clip, Vector3 position)
        {
            if (clip != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(clip, position);
        }
    }
}
