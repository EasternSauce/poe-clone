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
        }

        private readonly SnapshotTimeline timeline = new SnapshotTimeline();
        private readonly Dictionary<int, Puppet> puppets = new Dictionary<int, Puppet>();
        private readonly Dictionary<int, LootDrop> loot = new Dictionary<int, LootDrop>();
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
        private CameraFollow cameraFollow;
        private LoadingScreenUI loadingScreen;
        private GameObject enemyPrefab;
        private Transform puppetParent;

        private EntityState playerApplied;
        private bool playerHasPose;
        private Vector3 playerLastPosition;
        private int appliedLevel;
        private int appliedFade;
        private Coroutine fadeRoutine;
        private string[] appliedEquipment;
        private Dictionary<string, ItemData> itemsById;

        public bool IsActive => active;

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

            // The spectator's own (local, starter) inventory isn't the player's - don't let I/C open it.
            SetEnabled(FindAnyObjectByType<InventoryUI>(), false);
            SetEnabled(FindAnyObjectByType<CharacterPageUI>(), false);

            hud = FindAnyObjectByType<PlayerHUD>();
            if (hud != null)
            {
                hud.SpectatorMode = true;
                hud.enabled = false; // until there's a player to show
            }

            Camera cam = Camera.main;
            cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            loadingScreen = FindAnyObjectByType<LoadingScreenUI>();

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

            // Gear only needs to look right here, and the look is keyed by base id, so one display
            // item per base covers every starter item and every drop.
            itemsById = new Dictionary<string, ItemData>();
            foreach (string id in ItemGenerator.BaseIds)
                itemsById[id] = ItemGenerator.Display(id, null, ItemRarity.Normal);
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

        /// <summary>The player left / the connection dropped: clear the stage.</summary>
        public void ResetReplica()
        {
            timeline.Clear();
            currentPid = int.MinValue;

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
            playerHasPose = false;
            appliedLevel = 0;
            appliedEquipment = null;

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
            if (!active || !timeline.HasData)
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
                AreaManager.Instance.ApplyAreaImmediate(s.area);

            ApplyFade(s.fade);
            ApplyHud(s.hud);
            ApplyEquipment(s.eq);
            ApplyPlayerEvents(s.p);

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

        private void OnPlayerStrike()
        {
            if (active && playerStats != null && playerAttack != null &&
                CharacterAttackAnimator.IsRangedProfile(playerAttack.ProfileId))
                PlayerArrow.LaunchVisual(playerStats.transform, CharacterAttackAnimator.AttackRange(WeaponType.Bow));
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

            if (appliedLevel > 0 && h.lv > appliedLevel)
                PlaySfx(AudioManager.Instance != null ? AudioManager.Instance.playerLevelUp : null, playerStats.transform.position);
            appliedLevel = h.lv;

            playerStats.ApplyReplicatedState(h.lv, h.xp, h.str, h.dex, h.itl, h.hp, h.mhp, h.mp, h.mmp,
                h.dead != 0, h.cd, h.rv != 0);
        }

        private void ApplyEquipment(string[] eq)
        {
            if (eq == null || playerInventory == null || playerInventory.Equipment == null)
                return;

            EquipSlot[] slots = SlotRules.AllSlots;
            if (appliedEquipment == null)
                appliedEquipment = new string[slots.Length];

            // Two passes: empty every slot that changed, then fill them. Filling one at a time could
            // be refused mid-swap by the hand rules (a bow arriving while the old shield is still on).
            for (int k = 0; k < slots.Length && k < eq.Length; k++)
            {
                if (appliedEquipment[k] != (eq[k] ?? string.Empty))
                    playerInventory.Equipment.Unequip(slots[k]);
            }

            for (int k = 0; k < slots.Length && k < eq.Length; k++)
            {
                string id = eq[k] ?? string.Empty;
                if (appliedEquipment[k] == id)
                    continue;
                appliedEquipment[k] = id;

                if (id.Length > 0 && itemsById.TryGetValue(id, out ItemData item))
                    playerInventory.Equipment.TryEquip(slots[k], item, out _);
            }
        }

        private void ApplyPlayerEvents(EntityState p)
        {
            if (playerStats == null || p == null)
                return;

            EntityState prev = playerApplied;
            playerApplied = p.Clone();

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

            if (p.d == 0 && p.atk > prev.atk && playerAttack != null)
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

        private void ApplyEnemyEvents(Puppet puppet, EntityState e)
        {
            EntityState prev = puppet.Applied;
            puppet.Applied = e.Clone();
            if (prev == null || puppet.Health == null)
                return;

            Vector3 at = puppet.Root.transform.position;
            puppet.Health.ApplyReplicatedHealth(e.hp, e.mhp);

            if (puppet.Health.IsDead)
                return;

            if (e.d != 0)
            {
                // Topples from its last interpolated (still standing) pose. Deliberately not snapped
                // to e's position: the real body may already be part-way through its fall in this
                // snapshot, and the local collapse would then sink it twice as far.
                puppet.Health.ApplyReplicatedDeath(instant: false);
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.enemyDeath) : null, at);
                return;
            }

            if (e.ch != 0 && prev.ch == 0)
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.enemyAggro) : null, at);

            if (e.atk > prev.atk && puppet.Attack != null)
                puppet.Attack.PlayReplicated(e.ap);

            if (e.sk > prev.sk)
                EnemySkills.PlayVisual(this, EnemyKinds.Get(e.k), puppet.Root.transform, new Vector3(e.sx, puppet.Root.transform.position.y, e.sz));

            if (e.stg > prev.stg && puppet.Stagger != null)
            {
                puppet.Stagger.Trigger();
                PlaySfx(AudioManager.Instance != null ? Pick(AudioManager.Instance.meleeHit) : null, at);
            }
        }

        // ---------------------------------------------------------------- continuous poses

        private void PosePlayer(StateSnapshot from, StateSnapshot to, float alpha)
        {
            if (playerStats == null)
                return;

            SnapshotTimeline.Interpolate(from.p, to.p, alpha, scratch);
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

                puppet.Root.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, scratch.r, 0f));
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
                if (!existing.Root.activeSelf)
                {
                    existing.Root.SetActive(true);
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

            EnemyKind kind = EnemyKinds.Get(e.k);
            EnemyKinds.ApplyLook(go, kind);
            go.GetComponent<EnemyHealth>()?.SetKindIndex(e.k);

            // Awake has run (adding EnemyCombat + the attack animator); Start hasn't. Switch off
            // everything that would think for itself before it gets the chance.
            SetEnabled(go.GetComponent<EnemyController>(), false);
            SetEnabled(go.GetComponent<EnemyCombat>(), false);
            var cc = go.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

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

            // A caster's or archer's replayed attack sends a harmless bolt/arrow at the player.
            if (kind.IsRanged && puppet.Attack != null)
            {
                Transform body = go.transform;
                puppet.Attack.StrikeFrame += () =>
                {
                    if (body != null && playerStats != null)
                        EnemyProjectile.LaunchVisual(EnemyCombat.BoltOrigin(body), playerStats.transform.position, kind);
                };
            }

            if (puppet.Health != null)
            {
                puppet.Health.ApplyReplicatedHealth(e.hp, e.mhp);
                if (e.d != 0)
                    puppet.Health.ApplyReplicatedDeath(instant: true);
            }

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
