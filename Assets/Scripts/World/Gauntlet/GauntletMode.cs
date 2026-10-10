using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// The debug class gauntlet (Editor only, started from the PoeClone menu or DevTest): pick a
    /// level and a build, get its kit, walk from the start room into the arena, and fight enemies
    /// (singles and groups, at your level) one encounter after another, then three random field
    /// bosses. No experience, loot or gold. Everything measured goes to GauntletResults/ (see
    /// <see cref="GauntletRecorder"/>). With "auto=1" a bot plays it against frozen enemies.
    /// Runs inside <see cref="MinimalCombatMode"/>'s stripped scene.
    /// </summary>
    public sealed class GauntletMode : MonoBehaviour
    {
        public const int EncounterCount = 20;
        public const int BossCount = 3;
        public const int DefaultRolls = 10;

        private enum Phase { Setup, StartRoom, Encounters, Bosses, Done }

        public static GauntletMode Instance { get; private set; }

        private static readonly string[] NormalKinds =
        {
            "Zombie", "Raider", "Brute", "Archer", "Fire Caster", "Frost Caster", "Storm Caster", "Skeleton", "Wraith",
            "Ember Knight", "Forest Shaman", "Necromancer", "Skeleton Archer", "Frost Giant", "Giant Spider", "Dire Wolf",
            "Bog Slime", "Grave Bat", "Corpse Ooze", "Crypt Spider", "Magma Beetle", "Hellhound", "Frost Wolf", "Ice Crawler"
        };
        private static readonly string[] EliteKinds = { "Briarbound", "Grave Siren", "Rime Stalker", "Cinder Penitent", "Hollowmaw", "Barrow Castellan" };

        private struct Encounter
        {
            public string Label;
            public List<int> Kinds;
            public bool Elite;  // the first kind is an elite
        }

        // Rooms: the arena round the origin, the start room south of it, a corridor between.
        private const float ArenaHalf = 30f;
        private static readonly Vector3 BotMark = new Vector3(0f, 1.1f, -8f);
        private static readonly Vector3 StartRoomCenter = new Vector3(0f, 0f, -42f);
        private const float StartRoomHalf = 8f;
        private const float DoorHalf = 3f;
        private static readonly Vector3 ArenaRespawn = new Vector3(0f, 1.1f, -25f);

        public bool Automated { get; private set; }
        public GauntletBuild Build { get; private set; }
        public int Level { get; private set; }
        public int Rolls { get; private set; } = DefaultRolls;
        public GauntletRecorder Recorder { get; private set; }
        public string Status { get; private set; } = "choose a level and a build";
        public string ResultPath { get; private set; }
        public bool Finished => phase == Phase.Done;
        public bool Started => phase != Phase.Setup;
        public bool RerollsAllowed => Started && !Automated && Level > 1 && phase != Phase.Done;

        private Phase phase = Phase.Setup;
        private PlayerStats player;
        private PlayerInventory inventory;
        private GameObject enemyPrefab;
        private GauntletLoadout.Result kit;
        private System.Random rng;
        private int seed;
        private readonly List<Encounter> encounters = new List<Encounter>();
        private readonly List<int> bosses = new List<int>();
        private int encounterIndex;   // 1-based once fighting
        private int bossIndex;
        private GameObject doorBlock;
        private GauntletUI ui;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            player = FindAnyObjectByType<PlayerStats>();
            inventory = player.GetComponent<PlayerInventory>();
            enemyPrefab = FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include)?.EnemyPrefab;
            player.ExperienceLocked = true;
            Automated = MinimalCombatMode.Option("auto") == "1";

            // The inventory's reroll buttons: set before the inventory panel builds itself.
            InventoryUI.ExtraSlotButton = new InventoryUI.SlotButton
            {
                Label = "R",
                Tooltip = "<b>Reroll</b>\nReplace this item with a random rare of the same kind at your level.",
                Enabled = slot => RerollsAllowed,
                Clicked = Reroll
            };

            BuildRooms();
            PoeClone.Network.UiEventSystemBootstrap.EnsureExists();
        }

        private IEnumerator Start()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("Gauntlet: no enemy prefab in the scene.");
                yield break;
            }
            // Let the equipment and skill listeners bind first, as MinimalCombatMode does.
            yield return null;
            if (Automated)
            {
                GauntletBuild build = GauntletBuilds.Get(MinimalCombatMode.Option("build")) ?? GauntletBuilds.All[0];
                int.TryParse(MinimalCombatMode.Option("level"), out int level);
                if (int.TryParse(MinimalCombatMode.Option("rolls"), out int rolls) && rolls > 0) Rolls = rolls;
                int.TryParse(MinimalCombatMode.Option("seed"), out int fixedSeed);
                Begin(build, Mathf.Max(1, level), fixedSeed != 0 ? fixedSeed : 1234);
            }
            else
            {
                MinimalCombatMode.PlacePlayer(player, StartRoomCenter + new Vector3(0f, 1.1f, -2f), Quaternion.identity);
                ui = gameObject.AddComponent<GauntletUI>();
                ui.Bind(this);
            }
        }

        // ------------------------------------------------------------------ rooms

        private void BuildRooms()
        {
            Color floor = MinimalCombatMode.FloorColor, wall = MinimalCombatMode.WallColor;
            float a = ArenaHalf;
            MinimalCombatMode.Block(transform, "Gauntlet Arena Floor", new Vector3(0f, -0.5f, 0f), new Vector3(a * 2f, 1f, a * 2f), floor);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(-a, 1f, 0f), new Vector3(1f, 2f, a * 2f + 1f), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(a, 1f, 0f), new Vector3(1f, 2f, a * 2f + 1f), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(0f, 1f, a), new Vector3(a * 2f + 1f, 2f, 1f), wall);
            // The south wall has the door in it.
            float side = a - DoorHalf;
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(-(DoorHalf + side * 0.5f), 1f, -a), new Vector3(side + 0.5f, 2f, 1f), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(DoorHalf + side * 0.5f, 1f, -a), new Vector3(side + 0.5f, 2f, 1f), wall);
            if (Automated) return;

            float corridorStart = StartRoomCenter.z + StartRoomHalf, corridorLength = -a - corridorStart;
            MinimalCombatMode.Block(transform, "Gauntlet Corridor", new Vector3(0f, -0.5f, corridorStart + corridorLength * 0.5f),
                new Vector3(DoorHalf * 2f, 1f, corridorLength + 0.2f), floor * 0.9f);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(-DoorHalf - 0.5f, 1f, corridorStart + corridorLength * 0.5f), new Vector3(1f, 2f, corridorLength), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", new Vector3(DoorHalf + 0.5f, 1f, corridorStart + corridorLength * 0.5f), new Vector3(1f, 2f, corridorLength), wall);

            float s = StartRoomHalf;
            Vector3 c = StartRoomCenter;
            MinimalCombatMode.Block(transform, "Gauntlet Start Floor", c + new Vector3(0f, -0.5f, 0f), new Vector3(s * 2f, 1f, s * 2f), floor * 0.85f);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", c + new Vector3(-s, 1f, 0f), new Vector3(1f, 2f, s * 2f + 1f), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", c + new Vector3(s, 1f, 0f), new Vector3(1f, 2f, s * 2f + 1f), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", c + new Vector3(0f, 1f, -s), new Vector3(s * 2f + 1f, 2f, 1f), wall);
            float startSide = s - DoorHalf;
            MinimalCombatMode.Block(transform, "Gauntlet Wall", c + new Vector3(-(DoorHalf + startSide * 0.5f), 1f, s), new Vector3(startSide + 0.5f, 2f, 1f), wall);
            MinimalCombatMode.Block(transform, "Gauntlet Wall", c + new Vector3(DoorHalf + startSide * 0.5f, 1f, s), new Vector3(startSide + 0.5f, 2f, 1f), wall);
        }

        // ------------------------------------------------------------------ starting

        /// <summary>Levels the character, kits it out for the build and opens the way to the arena.</summary>
        public void Begin(GauntletBuild build, int level, int fixedSeed = 0)
        {
            if (Started) return;
            Build = build;
            Level = Mathf.Clamp(level, 1, 100);
            seed = fixedSeed != 0 ? fixedSeed : System.Environment.TickCount & 0x7fffffff;
            rng = new System.Random(seed);

            player.RestoreProgress(Level, 0);
            // The stat sheet learns the new level now, not next frame: gear requirements read it.
            player.GetComponent<PlayerStatsLink>()?.PushBase();
            var notes = new List<string>();
            GauntletLoadout.AllocatePassives(player.GetComponent<PlayerPassives>(), build, Level, notes);
            if (Level <= 1)
            {
                // Level 1 is a fresh character: the usual starter gear on the floor, no rares.
                if (Automated) kit = GauntletLoadout.EquipStarter(inventory, build);
                else
                {
                    kit = new GauntletLoadout.Result();
                    StarterLoot.PlaceAround(player.transform.position - new Vector3(0f, 1.1f, 0f));
                }
            }
            else
                kit = GauntletLoadout.Equip(inventory, build, Level, Rolls, new System.Random(seed ^ 0x5bd1e995));
            notes.AddRange(kit.Notes);
            inventory.SetPotions(PlayerInventory.MaxPotions, PlayerInventory.MaxPotions);
            ItemData regret = ItemData.RegretItem();
            regret.StackCount = 100;
            if (!inventory.Grid.TryAutoPlace(regret)) notes.Add("no room in the bag for the Orbs of Regret");
            player.Heal(player.MaxHealth);
            player.RestoreMana(player.MaxMana);

            PlanFights(new System.Random(seed));
            Recorder = new GauntletRecorder(player, Automated ? "automated" : "manual", build, Level, Level > 1 ? Rolls : 0, seed);
            Recorder.Data.enemiesFrozen = Automated;
            Recorder.Data.notes.AddRange(notes);
            foreach (string n in notes) Debug.LogWarning("Gauntlet: " + n);
            phase = Phase.StartRoom;
            Status = "walk through the door to begin";

            if (Automated)
            {
                MinimalCombatMode.PlacePlayer(player, BotMark, Quaternion.identity);
                StartCoroutine(BotStart());
            }
        }

        private IEnumerator BotStart()
        {
            // A frame for the skill bar and stat sheet to catch up with the new gear.
            yield return null;
            yield return null;
            gameObject.AddComponent<GauntletBot>().Bind(this, Build);
            EnterArena();
        }

        private void PlanFights(System.Random plan)
        {
            encounters.Clear();
            int elites = 0;
            for (int k = 0; k < EncounterCount; k++)
            {
                double roll = plan.NextDouble();
                var e = new Encounter { Kinds = new List<int>() };
                // At least three elite fights, spread through the run.
                bool elite = (k % 7 == 6 && elites < 3) || (roll < 0.08 && elites < 4);
                if (elite)
                {
                    elites++;
                    e.Elite = true;
                    e.Label = "elite";
                    e.Kinds.Add(EnemyKinds.IndexOf(EliteKinds[plan.Next(EliteKinds.Length)]));
                    int escorts = plan.Next(0, 3);
                    for (int i = 0; i < escorts; i++) e.Kinds.Add(RandomNormal(plan));
                }
                else if (roll < 0.45)
                {
                    e.Label = "single";
                    e.Kinds.Add(RandomNormal(plan));
                }
                else if (roll < 0.78)
                {
                    e.Label = "pack";
                    int kind = RandomNormal(plan), size = plan.Next(3, 6);
                    for (int i = 0; i < size; i++) e.Kinds.Add(kind);
                }
                else
                {
                    e.Label = "mixed group";
                    int size = plan.Next(2, 5);
                    for (int i = 0; i < size; i++) e.Kinds.Add(RandomNormal(plan));
                }
                encounters.Add(e);
            }

            bosses.Clear();
            var pool = new List<int>();
            for (int k = 0; k < EnemyKinds.All.Length; k++)
                if (EnemyKinds.All[k].IsBoss && EnemyKinds.All[k].Boss != BossStyle.Shepherd) pool.Add(k);
            for (int k = 0; k < BossCount && pool.Count > 0; k++)
            {
                int pick = plan.Next(pool.Count);
                bosses.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        private static int RandomNormal(System.Random plan) => EnemyKinds.IndexOf(NormalKinds[plan.Next(NormalKinds.Length)]);

        // ------------------------------------------------------------------ the run

        private void Update()
        {
            if (Recorder == null || phase == Phase.Done) return;
            Recorder.Tick();
            if (phase == Phase.StartRoom && !Automated && player.transform.position.z > -ArenaHalf + 3f)
                EnterArena();
        }

        private void EnterArena()
        {
            if (phase != Phase.StartRoom) return;
            phase = Phase.Encounters;
            // The character as it walks in: after any manual rerolls, respecs or picked-up gear.
            Recorder.CaptureStart(kit.Picks);
            if (!Automated)
            {
                doorBlock = MinimalCombatMode.Block(transform, "Gauntlet Door", new Vector3(0f, 1f, -ArenaHalf), new Vector3(DoorHalf * 2f + 0.5f, 2f, 1f), MinimalCombatMode.WallColor * 0.7f);
                player.SetSpawnPoint(ArenaRespawn, Quaternion.identity);
            }
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float gap = Automated ? 0.6f : 1.5f;
            for (encounterIndex = 1; encounterIndex <= encounters.Count; encounterIndex++)
            {
                yield return new WaitForSeconds(gap);
                Encounter e = encounters[encounterIndex - 1];
                Recorder.CurrentEncounter = encounterIndex;
                Recorder.Phase = "encounter " + encounterIndex + " (" + e.Label + ")";
                var record = new GauntletRecorder.EncounterRecord { index = encounterIndex, label = e.Label, spawnedAt = Recorder.Now };
                Recorder.Data.encounters.Add(record);
                Status = "encounter " + encounterIndex + " / " + encounters.Count + ": " + e.Label;
                ReturnBot();
                SpawnEncounter(e, record);
                Recorder.InCombat = true;
                float timeout = Automated ? 120f : float.MaxValue;
                float started = Time.time;
                yield return new WaitUntil(() => NoEnemiesLeft() || Time.time - started > timeout);
                Recorder.InCombat = false;
                if (!NoEnemiesLeft())
                {
                    record.timedOut = true;
                    ClearEnemies();
                }
                record.clearedAt = Recorder.Now;
            }

            phase = Phase.Bosses;
            Recorder.CurrentEncounter = 0;
            for (bossIndex = 1; bossIndex <= bosses.Count; bossIndex++)
            {
                yield return new WaitForSeconds(Automated ? 1f : 4f);
                EnemyKind kind = EnemyKinds.Get(bosses[bossIndex - 1]);
                Status = "boss " + bossIndex + " / " + bosses.Count + ": " + kind.Name;
                Recorder.Phase = "boss " + kind.Name;
                ReturnBot();
                EnemyHealth boss = SpawnBoss(bosses[bossIndex - 1]);
                var record = new GauntletRecorder.BossRecord { name = kind.Name, level = Level, maxHealth = boss.MaxHealth, spawnedAt = Recorder.Now };
                Recorder.Data.bossFights.Add(record);
                Recorder.CurrentBoss = record;
                Recorder.InCombat = true;
                float timeout = Automated ? 240f : float.MaxValue;
                float started = Time.time;
                yield return new WaitUntil(() => boss == null || boss.IsDead || Time.time - started > timeout);
                Recorder.InCombat = false;
                if (boss != null && boss.IsDead)
                {
                    record.diedAt = Recorder.Now;
                    if (record.firstHitAt >= 0f)
                    {
                        record.timeToKill = Mathf.Max(0.01f, record.diedAt - record.firstHitAt);
                        record.dps = record.maxHealth / record.timeToKill;
                    }
                    record.dpsFromSpawn = record.maxHealth / Mathf.Max(0.01f, record.diedAt - record.spawnedAt);
                }
                else
                    record.timedOut = true;
                Recorder.CurrentBoss = null;
                ClearEnemies();
            }
            Finish(true);
        }

        // The bot walks a little towards every fight; it starts each one from the same spot.
        private void ReturnBot()
        {
            if (Automated) MinimalCombatMode.PlacePlayer(player, BotMark, Quaternion.identity);
        }

        private bool NoEnemiesLeft()
        {
            foreach (EnemyHealth e in EnemyHealth.Active)
                if (e != null && !e.IsDead && !e.IsRemote) return false;
            return true;
        }

        private static void ClearEnemies()
        {
            foreach (EnemyHealth e in new List<EnemyHealth>(EnemyHealth.Active))
                if (e != null && !e.IsDead) Destroy(e.gameObject);
        }

        // ------------------------------------------------------------------ spawning

        private void SpawnEncounter(Encounter e, GauntletRecorder.EncounterRecord record)
        {
            Vector3 origin = player.transform.position;
            for (int i = 0; i < e.Kinds.Count; i++)
            {
                int kindIndex = e.Kinds[i];
                EnemyKind kind = EnemyKinds.Get(kindIndex);
                Vector3 at = Automated ? FrozenSpot(origin, i, e.Kinds.Count, kind.Scale) : ArenaSpot();
                at.y = 1.1f * kind.Scale;
                Quaternion facing = Quaternion.LookRotation(Flat(origin - at));
                GameObject go = Instantiate(enemyPrefab, at, facing, transform);
                EnemyKinds.Apply(go, kindIndex, Level);
                EnemyHealth health = go.GetComponent<EnemyHealth>();
                bool elite = e.Elite && i == 0;
                Recorder.Expect(health, elite ? "elite" : "normal");
                record.enemies.Add(kind.Name);
                if (Automated) Freeze(go);
                else go.GetComponent<EnemyController>()?.Alert();
            }
            Physics.SyncTransforms();
        }

        private EnemyHealth SpawnBoss(int kindIndex)
        {
            EnemyKind kind = EnemyKinds.Get(kindIndex);
            Vector3 origin = player.transform.position;
            Vector3 at = Automated ? origin + Vector3.forward * (3f + 1.5f * kind.Scale) : new Vector3(0f, 0f, 10f);
            at.y = 1.1f * kind.Scale;
            GameObject go = Instantiate(enemyPrefab, at, Quaternion.LookRotation(Flat(origin - at)), transform);
            go.name = kind.Name;
            EnemyKinds.Apply(go, kindIndex, Level);
            EnemyHealth health = go.GetComponent<EnemyHealth>();
            Recorder.Expect(health, "boss");
            if (Automated)
                Freeze(go);
            else
            {
                go.AddComponent<BossAbilities>().Configure(kind, Level, enemyPrefab);
                health.RevealBossHealth(kind.Name);
                go.GetComponent<EnemyController>()?.Alert();
            }
            Physics.SyncTransforms();
            return health;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
        }

        // Somewhere in the far half of the arena, clear of the player.
        private Vector3 ArenaSpot()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var at = new Vector3((float)(rng.NextDouble() * 2 - 1) * (ArenaHalf - 4f), 0f, 2f + (float)rng.NextDouble() * (ArenaHalf - 6f));
                if (Flat(at - player.transform.position).magnitude > 9f) return at;
            }
            return new Vector3(0f, 0f, ArenaHalf - 4f);
        }

        // Frozen enemies stand in an arc in front of the player, close enough for melee.
        private static Vector3 FrozenSpot(Vector3 origin, int i, int count, float scale)
        {
            float spread = count > 1 ? Mathf.Lerp(-35f, 35f, i / (float)(count - 1)) : 0f;
            float distance = 2.2f + 0.8f * scale + (i % 2) * 0.6f;
            Vector3 at = origin + Quaternion.Euler(0f, spread, 0f) * Vector3.forward * distance;
            at.x = Mathf.Clamp(at.x, -ArenaHalf + 2f, ArenaHalf - 2f);
            at.z = Mathf.Clamp(at.z, -ArenaHalf + 2f, ArenaHalf - 2f);
            return at;
        }

        // The automated run's enemies don't move, attack or cast; they only take hits.
        private static void Freeze(GameObject enemy)
        {
            foreach (MonoBehaviour b in new MonoBehaviour[]
                { enemy.GetComponent<EnemyController>(), enemy.GetComponent<EnemyCombat>(), enemy.GetComponent<EnemySkills>() })
                if (b != null) b.enabled = false;
        }

        // ------------------------------------------------------------------ rerolls and the end

        private void Reroll(EquipSlot slot)
        {
            if (!RerollsAllowed) return;
            ItemData before = inventory.Equipment.Get(slot);
            ItemData after = GauntletLoadout.RandomRare(rng, before, slot, Level, Build, inventory.Equipment.Get(EquipSlot.MainHand));
            if (after == null) return;
            inventory.Equipment.Unequip(slot);
            if (!inventory.Equipment.Restore(slot, after, out _))
            {
                // A new main hand that doesn't suit the off hand: the off hand goes to the bag.
                ItemData off = inventory.Equipment.Unequip(EquipSlot.OffHand);
                if (off != null && !inventory.Grid.TryAutoPlace(off)) inventory.ThrowAway(off);
                if (!inventory.Equipment.Restore(slot, after, out _))
                {
                    if (before != null) inventory.Equipment.Restore(slot, before, out _);
                    return;
                }
            }
            Recorder?.Rerolled(slot, before, after);
        }

        /// <summary>Ends the run and writes the results (also when Play stops early).</summary>
        public void Finish(bool completed)
        {
            if (Recorder == null || phase == Phase.Done) return;
            phase = Phase.Done;
            StopAllCoroutines();
            GetComponent<GauntletBot>()?.Stop();
            Recorder.Finish(completed, kit != null ? kit.Picks : null);
            Recorder.Detach();
            ResultPath = Recorder.Write();
            Status = completed ? "finished" : "stopped early";
            Debug.Log("Gauntlet " + Status + ": " + ResultPath);
            if (doorBlock != null) Destroy(doorBlock);
            ui?.ShowResults(Recorder.Summary(), ResultPath);
        }

        private void OnDestroy()
        {
            // Play stopped mid-run: keep what was measured.
            if (Recorder != null && phase != Phase.Done && phase != Phase.Setup)
                Finish(false);
            if (Instance == this) Instance = null;
            InventoryUI.ExtraSlotButton = null;
        }

        // ------------------------------------------------------------------ for the HUD and the bot

        public int EncounterNumber => encounterIndex;
        public int BossNumber => bossIndex;
        public int Deaths => Recorder != null ? Recorder.Data.deaths : 0;
        public float Elapsed => Recorder != null ? Recorder.Now : 0f;
    }
}
