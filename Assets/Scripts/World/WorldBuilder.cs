using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Network;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.World
{
    /// <summary>
    /// Builds fixed authored areas, themed scenery and existing story content identically on every client.
    /// Route: Haven - Greenwood - The Lost Hollows (the crossroads: a dead-end branch to the Haunted
    /// Graveyard, the deep road on to the Ashen Ruins) - Frozen Hollow.
    /// Layout geometry and permanent anchors live in AreaLayouts; the initial destination is supplied by the character-resume rules.
    /// </summary>
    public partial class WorldBuilder : MonoBehaviour
    {
        public const int Greenwood = 0;
        public const int Haven = 1;
        public const int Graveyard = 2;
        public const int Ruins = 3;
        public const int Frozen = 4;
        public const int Cave = 6; // Preserve the final arena's saved area ID (5).
        public const int Desert = 9;
        public const int Sahret = 10;
        public const int SaltBasin = 11;
        public const int Channels = 12;
        public const int DrownedWorks = 13;
        public const int Orchard = 14;
        public const int Reservoir = 15;
        public const int BuriedWard = 16;
        public const int FirstRoot = 17;
        public static bool IsActTwo(int area) => area >= Desert && area <= FirstRoot;
        public static readonly int[] WorldAreas = { Greenwood, Haven, Graveyard, Ruins, Frozen, Cave, Desert, Sahret, SaltBasin, Channels, DrownedWorks, Orchard, Reservoir, BuriedWard, FirstRoot };

        public static WorldBuilder Instance { get; private set; }

        public static readonly string[] AreaNames = { "Greenwood", "Haven", "Haunted Graveyard", "Ashen Ruins", "Frozen Hollow", "The Shed Sanctuary", "The Lost Hollows",
            "The Warren", "The Drowned Belfry", "The Sundered Expanse", "Sahret, the Sunken City", "The Bleached Basin", "The Thirsting Channels", "The Drowned Works", "The Orchard of Empty Jars", "The Crownless Reservoir", "The Buried Ward", "The First Root" };
        public static readonly int[] MonsterLevels = { 1, 0, 17, 25, 34, 37, 9, 9, 17, 38, 0, 40, 42, 42, 44, 46, 48, 50 };

        private static readonly Vector3[] Centers =
        {
            Vector3.zero,
            new Vector3(-420f, 0f, 0f),
            new Vector3(420f, 0f, 0f),
            new Vector3(840f, 0f, 0f),
            new Vector3(1260f, 0f, 0f),
            ActArenaCenter,
            new Vector3(420f, 0f, -420f),
            // The boss dens behind their gates (WorldBuilder.BossLairs).
            new Vector3(840f, 0f, -420f),
            new Vector3(1260f, 0f, -420f),
            new Vector3(2200f, 0f, 0f),
            new Vector3(2720f, 0f, 0f),
            new Vector3(3240f, 0f, 0f),
            new Vector3(3760f, 0f, 0f),
            new Vector3(3240f, 0f, -520f),
            new Vector3(3760f, 0f, 520f),
            new Vector3(4280f, 0f, 0f),
            new Vector3(4800f, 0f, 0f),
            new Vector3(5320f, 0f, 0f)
        };

        /// <summary>The colour that stands for an area (gate panels, its minimap ground).</summary>
        public static Color AreaColor(int area)
        {
            return AreaColors[Mathf.Clamp(area, 0, AreaColors.Length - 1)];
        }

        private static readonly Color[] AreaColors =
        {
            new Color(0.55f, 0.75f, 0.55f),
            new Color(0.82f, 0.74f, 0.56f),
            new Color(0.40f, 0.46f, 0.42f),
            new Color(0.58f, 0.44f, 0.36f),
            new Color(0.80f, 0.88f, 0.95f),
            new Color(0.50f, 0.44f, 0.38f),
            new Color(0.31f, 0.35f, 0.37f),
            new Color(0.42f, 0.36f, 0.28f),
            new Color(0.38f, 0.44f, 0.46f),
            new Color(0.87f, 0.66f, 0.36f),
            new Color(0.78f, 0.64f, 0.45f),
            new Color(0.85f, 0.83f, 0.70f),
            new Color(0.70f, 0.39f, 0.26f),
            new Color(0.28f, 0.45f, 0.43f),
            new Color(0.48f, 0.49f, 0.25f),
            new Color(0.61f, 0.75f, 0.65f),
            new Color(0.40f, 0.55f, 0.54f),
            new Color(0.58f, 0.36f, 0.43f)
        };

        // Explicit native rosters for opening spawns and respawns. Named entries keep additions
        // independent of kind-array positions; omitted kinds never leak into an area's mix.
        // Packs count for more than their weight suggests. Haven and the act arena have no mix.
        private static readonly float[][] KindWeights =
        {
            Roster(("Raider", 18f), ("Archer", 16f), ("Forest Shaman", 10f),
                ("Giant Spider", 14f), ("Dire Wolf", 9f), ("Bog Slime", 12f), ("Briarbound", 9f)),
            Roster(),
            Roster(("Zombie", 16f), ("Skeleton", 20f), ("Skeleton Archer", 14f), ("Wraith", 12f), ("Necromancer", 10f), ("Storm Caster", 8f),
                ("Grave Siren", 11f), ("Barrow Castellan", 10f), ("Bone Reaver", 10f), ("Bone Warden", 9f)),
            Roster(("Fire Caster", 24f), ("Ember Knight", 24f), ("Magma Beetle", 20f), ("Hellhound", 12f),
                ("Cinder Penitent", 12f)),
            Roster(("Frost Caster", 20f), ("Frost Giant", 12f), ("Frost Wolf", 10f),
                ("Ice Crawler", 16f), ("Rime Stalker", 10f)),
            Roster(),
            // The Lost Hollows have cave dwellers, rather than the graveyard's entire undead mix.
            Roster(("Brute", 16f), ("Storm Caster", 12f), ("Grave Bat", 12f),
                ("Corpse Ooze", 18f), ("Crypt Spider", 22f), ("Hollowmaw", 11f)),
            // The boss dens behind their gates hold only their boss.
            Roster(),
            Roster(),
            Roster(("Dune Skitter", 20f), ("Glassback Scorpion", 17f), ("Sandlurker", 13f),
                ("Dustroad Reaver", 20f), ("Dustroad Javelineer", 17f), ("Ashwind Caller", 8f)),
            Roster(),
            Roster(("Saltback Crab", 30f), ("Brine Spitter", 24f), ("Saltbound Dredger", 28f),
                ("Glassback Scorpion", 10f), ("Sandlurker", 8f)),
            Roster(("Channel Reaver", 30f), ("Clay Bearer", 24f), ("Sandlurker", 16f),
                ("Dustroad Javelineer", 22f), ("Ashwind Caller", 8f)),
            Roster(("Drowned Mechanic", 32f), ("Brine Spitter", 24f), ("Saltback Crab", 18f),
                ("Corpse Ooze", 16f), ("Clay Bearer", 10f)),
            Roster(("Rootbound Harvester", 30), ("Jar Swarm", 26), ("Orchard Hound", 24), ("Jar Keeper", 20)),
            Roster(("Bronze Sluiceguard", 30), ("Drowned Courtier", 25), ("Tithe Priest", 20), ("Reservoir Skimmer", 25)),
            Roster(("Ward Gaoler", 28), ("Cistern Leech", 28), ("Memory Scribe", 22), ("Drowned Fugitive", 22)),
            Roster(("Root Goliath", 24), ("Marrow Tick", 30), ("Sap Cantor", 22), ("Grafted Stalker", 24))
        };

        private static float[] Roster(params (string name, float weight)[] entries)
        {
            var weights = new float[EnemyKinds.All.Length];
            foreach (var entry in entries)
            {
                int index = EnemyKinds.IndexOf(entry.name);
                if (index < 0)
                    throw new System.ArgumentException($"Unknown area enemy: {entry.name}");
                weights[index] = entry.weight;
            }
            return weights;
        }

        /// <summary>Named places other features hang things on (NPC stands, the boss arena).</summary>
        public readonly Dictionary<string, Vector3> Spots = new Dictionary<string, Vector3>();

        private AreaKit kit;
        private System.Random rng;
        private readonly List<Vector3> claimed = new List<Vector3>(); // x, z, radius
        private Transform root;
        private GameObject gateTemplate;
        private Transform[] spawnPoints;

        public static Vector3 Center(int area)
        {
            if (area == ActArena) return ActArenaCenter;
            return Centers[Mathf.Clamp(area, 0, Centers.Length - 1)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureBuilt();
        }

        /// <summary>Scene reloads during character selection do not rerun runtime-init hooks.</summary>
        public static void EnsureBuilt()
        {
            IEnumerator build = BuildRoutine(SaveSystem.StartingArea);
            Drain(build);
        }

        /// <summary>
        /// Builds the world a step per frame, so a loading screen keeps animating while it runs.
        /// Does nothing if the world is already built or shouldn't be built yet.
        /// </summary>
        public static IEnumerator BuildRoutine(int startArea)
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("PoeClone.LootSimulator.Active", false)) return Nothing();
#endif
            if (MinimalCombatMode.Enabled) return Nothing();
            var session = PoeClone.Network.GameSessionController.Instance;
            if (session != null && session.Role == PoeClone.Network.SessionRole.Player && !session.PlayGranted) return Nothing();
            if (Instance != null) return Nothing();
            var go = new GameObject("World");
            return Flatten(go.AddComponent<WorldBuilder>().Build(startArea));
        }

        private static IEnumerator Flatten(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                {
                    IEnumerator steps = Flatten(nested);
                    while (steps.MoveNext()) yield return steps.Current;
                }
                else yield return routine.Current;
            }
        }

        private static IEnumerator Nothing()
        {
            yield break;
        }

        private void OnDestroy()
        {
            ReleaseStreamedResources();
            if (grassMaterial != null) Destroy(grassMaterial);
            if (Instance == this)
                Instance = null;
        }

        private IEnumerator Build(int startArea)
        {
            Instance = this;
            root = transform;
            kit = AreaKit.Load();

            AreaManager manager = FindAnyObjectByType<AreaManager>();
            GameObject ground = GameObject.Find("Ground");
            PlayerController player = FindAnyObjectByType<PlayerController>();

            if (kit == null || kit.greenwoodScenery == null || manager == null || ground == null || player == null)
            {
                Debug.LogError("WorldBuilder: missing area kit, Greenwood scenery, area manager, ground or player; the world cannot be built.");
                yield break;
            }

            // Before any gate or waystone clears its spot, so props in the way are removed.
            Instantiate(kit.greenwoodScenery).name = "Greenwood Scenery";
            Physics.SyncTransforms();
            ManualPointLightManager.Refresh();
            groundTemplate = ground;
            originalSpawner = FindAnyObjectByType<EnemySpawner>();
            if (originalSpawner != null) originalSpawner.enabled = false;
            spawnersByArea = new EnemySpawner[Centers.Length];
            gateTemplate = CreateGateTemplate();
            spawnPoints = new Transform[Centers.Length];
            var definitions = new AreaDefinition[Centers.Length];
            for (int area = 0; area < Centers.Length; area++)
            {
                spawnPoints[area] = Marker("Spawn_" + AreaNames[area], Center(area) + new Vector3(0, 1.1f, -6), 0);
                definitions[area] = new AreaDefinition
                {
                    areaName = AreaNames[area], spawnPoint = spawnPoints[area],
                    monsterLevel = MonsterLevels[area], isTown = area == Haven || area == Sahret,
                    respawnPoint = area == Desert ? spawnPoints[area] : null
                };
            }
            foreach (int area in WorldAreas)
                waystoneArrivals[area] = Marker("Arrive_" + AreaNames[area] + "_waystone", WaystoneSpot(area) + new Vector3(0, 1.1f, 2.6f), 0);
            InitShapes();
            // Travel anchors are cheap and remain valid before the destination is built.
            foreach (var link in AreaLayouts.Links) Connect(link.a, link.b);
            ConnectDen(Cave, WarrenGateLocal, WarrenGateInward, Warren);
            ConnectDen(Graveyard, BelfryGateLocal, BelfryGateInward, Belfry);
            BuildSanctuaryTravel();
            ConfigureCaveLighting(player.transform);
            ambience = gameObject.AddComponent<WorldAmbience>();
            yield return EnsureArea(startArea);
            MovePlayer(player, spawnPoints[startArea]);
            manager.AreaChanged += OnAreaChanged;
            manager.SetAreas(definitions, startArea);
            StarterLoot.PlaceAt(starterSpots, spawnPoints[startArea].position + Vector3.down * 1.1f);
            UI.LoadingStatus.Clear();
        }

        private readonly Dictionary<int, Transform> waystoneArrivals = new Dictionary<int, Transform>();
        public Transform WaystoneArrival(int area) => waystoneArrivals.TryGetValue(area, out var arrival) ? arrival : null;
        /// <summary>Where the gate from <paramref name="from"/> leads into <paramref name="area"/>.</summary>
        public Transform GateArrival(int area, int from) => root.Find("Arrive_" + AreaNames[area] + "_from_" + AreaNames[from]);
        private GameObject groundTemplate;
        private EnemySpawner originalSpawner;
        private WorldAmbience ambience;
        private readonly HashSet<int> builtAreas = new HashSet<int>();
        private int loadingArea = -1;
        private bool buildingArea;
        private bool NeedsArea(int area) => area == loadingArea;
        public bool IsAreaReady(int area) => builtAreas.Contains(area);

        // Serialize builds: the authored builders share their current area and random generator.
        public IEnumerator EnsureArea(int area, Transform arrival = null, Vector3? focus = null)
        {
            while (buildingArea || streamingBusy) yield return null;
            yield return WorldBuildScheduler.Run(EnsureAreaRoutine(area, arrival, focus), 8d);
        }

        private IEnumerator EnsureAreaRoutine(int area, Transform arrival, Vector3? focus)
        {
            if (builtAreas.Contains(area))
            {
                yield return EnsureArrival(area, focus ?? (arrival != null ? arrival : spawnPoints[area]).position);
                yield break;
            }
            if (area < 0 || area >= Centers.Length) yield break;
            buildingArea = true;
            loadingArea = area;
            float started = Time.realtimeSinceStartup;
            float stageStarted = started;
            try
            {
                Loading("Terrain: " + AreaNames[area]);
                yield return null;
                if (UsesChunks(area))
                    yield return PrepareStreamedArea(area);
                else if (area != ActArena && area != Warren && area != Belfry)
                    yield return BuildGround(groundTemplate, area);
                Debug.Log($"Area load [{AreaNames[area]}] layout/materials: {(Time.realtimeSinceStartup - stageStarted) * 1000f:F0}ms.");
                stageStarted = Time.realtimeSinceStartup;
                Loading("Scenery: " + AreaNames[area]);
                yield return null;
                if (IsActTwo(area) || area == ActArena) EnsureDesertMaterials();
                planningContent = UsesChunks(area);
                switch (area)
                {
                    case Haven: BuildHaven(); break;
                    case Greenwood: BuildGlowshrooms(); BuildGreenwoodOutskirts(); break;
                    case Graveyard: BuildGraveyard(); break;
                    case Ruins: BuildRuins(); break;
                    case Frozen: BuildFrozen(); break;
                    case Cave: BuildCave(); break;
                    case Desert: BuildActTwo(); break;
                    case Sahret: BuildSahret(); break;
                    case SaltBasin: BuildSaltBasin(); break;
                    case Channels: BuildThirstingChannels(); break;
                    case DrownedWorks: BuildDrownedWorks(); break;
                    case Orchard: BuildEmptyOrchard(); break;
                    case Reservoir: BuildCrownlessReservoir(); break;
                    case BuriedWard: case FirstRoot: BuildRootDescent(area); break;
                    case ActArena: CopySpawn(BuildActArena(), area); break;
                    case Warren: CopySpawn(BuildWarren(), area); break;
                    case Belfry: CopySpawn(BuildBelfry(), area); break;
                }
                BuildWaystones();
                BuildOutposts();
                BuildFieldBossLairs();
                Loading("Quest setup: " + AreaNames[area]);
                yield return null;
                if (!UsesChunks(area)) yield return BuildBorders();
                if (!UsesChunks(area)) ClearAreaTravel(area);
                BuildQuestSites();
                SetUpSpawners();
                if (area == Desert) SetUpDesertEncounters();
                if (area == Haven) BuildTownsfolk();
                else if ((area == Graveyard || area == Ruins || area == Cave) && originalSpawner != null)
                    BuildOutpostFolk(originalSpawner.EnemyPrefab);
                PlaceBosses();
                if (!UsesChunks(area))
                {
                    Physics.SyncTransforms();
                    ManualPointLightManager.Refresh();
                }
                // Five seconds is a soft cover budget. Never expose missing collision or enemies.
                // Optional wildlife and wind decoration can finish after the cover lifts.
                if (UsesChunks(area))
                    StartCoroutine(PrepareOptionalDetails(area));
                else if (Time.realtimeSinceStartup - started >= 5f)
                    StartCoroutine(FinishAreaDetails(area));
                else
                    yield return AreaDetails(area);
                planningContent = false;
                if (HasGrass(area))
                {
                    grassClearings[area] = claimed.ToArray();
                    if (!UsesChunks(area))
                        yield return BuildGrass(area, root.Find("Ground_" + AreaNames[area])?.gameObject);
                }
                Debug.Log($"Area load [{AreaNames[area]}] content plan: {(Time.realtimeSinceStartup - stageStarted) * 1000f:F0}ms.");
                yield return EnsureArrival(area, focus ?? (arrival != null ? arrival : spawnPoints[area]).position);
                builtAreas.Add(area);
                // Audio is optional and never blocks entry.
                ambience.Build(FindAnyObjectByType<AreaManager>(), FindAnyObjectByType<PlayerController>().transform, area);
                if (!UsesChunks(area)) WorldActivity.Refresh();
                Debug.Log($"Area load [{AreaNames[area]}] world ready: {(Time.realtimeSinceStartup - started) * 1000f:F0}ms.");
            }
            finally { planningContent = false; loadingArea = -1; buildingArea = false; }
        }

        private void ClearAreaTravel(int area, Rect? chunk = null)
        {
            bool Relevant(Vector3 point)
            {
                if (!chunk.HasValue) return true;
                Rect bounds = chunk.Value;
                Vector3 local = point - Center(area);
                // Include props which straddle the chunk edge and the travel approach.
                return local.x >= bounds.xMin - 12 && local.x <= bounds.xMax + 12 &&
                    local.z >= bounds.yMin - 12 && local.z <= bounds.yMax + 12;
            }
            // Gates were registered before scenery; clear their approaches after construction.
            Physics.SyncTransforms();
            foreach (var gate in gatePoints)
                if (gate.area == area && Relevant(gate.position)) ClearSpot(gate.position, 4.5f);
            string prefix = "Arrive_" + AreaNames[area] + "_";
            foreach (Transform marker in root)
                if (marker.name.StartsWith(prefix) && Relevant(marker.position)) ClearSpot(marker.position, 2f);
        }

        private void CopySpawn(AreaDefinition definition, int area)
        {
            spawnPoints[area].SetPositionAndRotation(definition.spawnPoint.position, definition.spawnPoint.rotation);
        }

        private IEnumerator FinishAreaDetails(int area)
        {
            var manager = FindAnyObjectByType<AreaManager>();
            while (buildingArea || streamingBusy || Time.timeScale <= 0f ||
                (manager != null && (manager.IsSwitching || (manager.loadingScreen != null && manager.loadingScreen.IsShowing))))
                yield return null;
            buildingArea = true;
            loadingArea = area;
            try { yield return AreaDetails(area); }
            finally { loadingArea = -1; buildingArea = false; }
        }

        private IEnumerator AreaDetails(int area)
        {
            yield return null;
            BuildAmbientDetails();
            yield return null;
            yield return BuildWildlife(FindAnyObjectByType<PlayerController>().transform);
            if (!planningContent)
            {
                ManualPointLightManager.Refresh();
                WorldActivity.Refresh();
            }
        }

        // Replicas need terrain before applying a snapshot; ordinary travel uses the coroutine.
        public bool EnsureAreaImmediate(int area, Vector3? focus = null)
        {
            if (builtAreas.Contains(area) && !UsesChunks(area)) return true;
            if (buildingArea || streamingBusy) return false;
            Drain(EnsureArea(area, focus: focus));
            return builtAreas.Contains(area);
        }

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
                if (routine.Current is IEnumerator nested) Drain(nested);
        }

        private static void Loading(string step) => UI.LoadingStatus.Set(step);

        private static void MovePlayer(PlayerController player, Transform to)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(to.position, to.rotation);
            if (cc != null) cc.enabled = true;
            var walk = player.GetComponentInChildren<CharacterWalkAnimator>();
            if (walk != null) walk.ResetAnimatorState();

            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.SetSpawnPoint(to.position, to.rotation);
                stats.ProtectArrival();
            }

            var cam = Camera.main != null ? Camera.main.GetComponent<CameraSystem.CameraFollow>() : null;
            if (cam != null)
                cam.SnapToTarget();
        }

        // ------------------------------------------------------------------ areas

        private IEnumerator BuildGround(GameObject template, int area)
        {
            Vector3 c = Centers[area];
            GameObject ground = Instantiate(template, root);
            ground.SetActive(true);
            ground.name = "Ground_" + AreaNames[area];
            ground.transform.position = new Vector3(c.x, template.transform.position.y, c.z);
            UseGroundTexture(ground, area);
            yield return null;

            // Cut to the area's outline, walled round its edge.
            IEnumerator shaping = ShapeGround(ground, area);
            while (shaping.MoveNext())
                yield return null;
        }

        // Its own floor: a generated texture on a copy of the ground material.
        private void UseGroundTexture(GameObject ground, int area)
        {
            Renderer r = ground.GetComponent<Renderer>();
            var material = new Material(r.sharedMaterial);
            material.SetTexture("_BaseMap", GroundTexture(area));
            material.SetColor("_BaseColor", Color.white);
            r.sharedMaterial = material;
        }

        private readonly Dictionary<int, Texture2D> groundTextures = new Dictionary<int, Texture2D>();

        private Texture2D GroundTexture(int area)
        {
            if (!groundTextures.TryGetValue(area, out var texture) || texture == null)
            {
                texture = CreateGroundTexture(area);
                groundTextures[area] = texture;
            }
            return texture;
        }

        private static Texture2D CreateGroundTexture(int area)
        {
            switch (area)
            {
                case BuriedWard:
                    return GroundTextures.Flagstones(161, new Color(0.34f, 0.43f, 0.40f), new Color(0.10f, 0.17f, 0.16f), 6);
                case FirstRoot:
                    return GroundTextures.Make(171, new Color(0.17f, 0.10f, 0.14f), new Color(0.39f, 0.24f, 0.25f),
                        new Color(0.55f, 0.41f, 0.30f), 0.009f, 6, grain: 0.05f);
                case Orchard:
                    return GroundTextures.Make(141, new Color(0.21f, 0.22f, 0.12f), new Color(0.43f, 0.40f, 0.24f),
                        new Color(0.62f, 0.55f, 0.34f), 0.009f, 6, grain: 0.05f);
                case Reservoir:
                    return GroundTextures.Flagstones(151, new Color(0.64f, 0.64f, 0.48f), new Color(0.22f, 0.31f, 0.26f), 6);
                case Channels:
                    return GroundTextures.Make(121, new Color(0.38f, 0.22f, 0.15f), new Color(0.73f, 0.47f, 0.29f),
                        new Color(0.85f, 0.66f, 0.43f), 0.008f, 6f, grain: 0.04f);
                case DrownedWorks:
                    return GroundTextures.Flagstones(131, new Color(0.25f, 0.32f, 0.29f), new Color(0.07f, 0.12f, 0.11f), 6);
                case SaltBasin:
                    return GroundTextures.Make(112, new Color(0.64f, 0.61f, 0.49f), new Color(0.92f, 0.90f, 0.78f),
                        new Color(1f, 0.98f, 0.90f), 0.015f, 9f, grain: 0.045f);
                case Desert:
                case Sahret:
                    return GroundTextures.Make(99, new Color(0.57f, 0.39f, 0.20f), new Color(0.88f, 0.69f, 0.40f),
                        new Color(0.97f, 0.81f, 0.53f), 0.007f, 5f, grain: 0.035f);
                case Greenwood:
                    // Deep, cool forest grass with bald patches of soil.
                    return GroundTextures.Grass(1, new Color(0.12f, 0.17f, 0.08f), new Color(0.21f, 0.31f, 0.12f),
                        new Color(0.36f, 0.38f, 0.18f), new Color(0.24f, 0.19f, 0.13f), 0.5f);
                case Haven:
                    // Kept town grass: a little warmer, and less worn through.
                    return GroundTextures.Grass(11, new Color(0.15f, 0.19f, 0.09f), new Color(0.24f, 0.32f, 0.13f),
                        new Color(0.40f, 0.40f, 0.20f), new Color(0.27f, 0.21f, 0.14f), 0.3f);
                case Graveyard:
                    return GroundTextures.Make(22, new Color(0.17f, 0.20f, 0.17f), new Color(0.31f, 0.33f, 0.27f),
                        new Color(0.40f, 0.40f, 0.36f), 0.015f, 7f);
                case Frozen:
                    // Wind-packed drifts: cool blue hollows between bright crests, with glints.
                    return GroundTextures.Make(44, new Color(0.64f, 0.73f, 0.86f), new Color(0.95f, 0.97f, 0.99f),
                        new Color(1f, 1f, 1f), 0.012f, 4f, grain: 0.05f);
                case Cave:
                    return GroundTextures.Make(66, new Color(0.18f, 0.20f, 0.21f), new Color(0.32f, 0.33f, 0.30f),
                        new Color(0.29f, 0.35f, 0.32f), 0.012f, 7f);
                default:
                    return GroundTextures.Make(33, new Color(0.22f, 0.19f, 0.18f), new Color(0.44f, 0.37f, 0.31f),
                        new Color(0.85f, 0.35f, 0.12f), 0.01f, 8f);
            }
        }

        // Floors laid over an area's ground: generated once, on copies of the kit's flat material.
        private Material dirtFloor, mudFloor, flagstoneFloor, kerbFloor;

        private Material DirtFloor => dirtFloor != null ? dirtFloor : dirtFloor = FloorMaterial("DirtFloor",
            GroundTextures.Dirt(5, new Color(0.33f, 0.26f, 0.17f), new Color(0.50f, 0.40f, 0.27f),
                new Color(0.46f, 0.42f, 0.36f), 90), 4f);

        private Material MudFloor => mudFloor != null ? mudFloor : mudFloor = FloorMaterial("MudFloor",
            GroundTextures.Dirt(9, new Color(0.17f, 0.16f, 0.15f), new Color(0.29f, 0.27f, 0.24f),
                new Color(0.36f, 0.36f, 0.34f), 70), 4f);

        private Material FlagstoneFloor => flagstoneFloor != null ? flagstoneFloor : flagstoneFloor = FloorMaterial("FlagstoneFloor",
            GroundTextures.Flagstones(7, new Color(0.27f, 0.25f, 0.22f), new Color(0.09f, 0.08f, 0.07f), 6), 4.5f);

        // The square's edging: the same stones, darker and larger.
        private Material KerbFloor => kerbFloor != null ? kerbFloor : kerbFloor = FloorMaterial("KerbFloor",
            FlagstoneFloor.GetTexture("_BaseMap"), 7f, new Color(0.72f, 0.7f, 0.68f));

        private Material FloorMaterial(string name, Texture texture, float tileSize, Color? tint = null)
        {
            var material = new Material(kit.Mat("TanDark")) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint ?? Color.white);
            material.SetFloat("_TriplanarTileSize", tileSize);
            return material;
        }

        // A flat, smooth-edged disc lying on the ground (Unity's cylinder shows its 20 sides).
        // Lobes above zero bend the rim into an irregular, puddle-like outline picked by seed.
        private void FloorDisc(Transform parent, string name, Vector3 center, float radius, Material material,
            float lobes = 0f, float seed = 0f)
        {
            if (DeferContent(center, () => FloorDisc(parent, name, center, radius, material, lobes, seed))) return;
            const int segments = 72;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int k = 0; k < segments; k++)
            {
                float a = k * Mathf.PI * 2f / segments;
                float edge = 1f + lobes * (0.6f * Mathf.Sin(2f * a + seed) + 0.3f * Mathf.Sin(3f * a + seed * 1.7f) + 0.2f * Mathf.Sin(5f * a + seed * 2.9f));
                vertices[k + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * edge;
                triangles[k * 3] = 0;
                triangles[k * 3 + 1] = (k + 1) % segments + 1;
                triangles[k * 3 + 2] = k + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var disc = new GameObject(name);
            disc.transform.SetParent(parent, false);
            disc.transform.position = center;
            disc.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = disc.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Haven: cottages and working yards along winding lanes around a market square.
        private void BuildHaven()
        {
            Begin(Haven, 101);
            Vector3 c = Centers[Haven];
            Transform t = Group("Haven");

            // Flagstone square, edged with a ring of darker stones, and the well at its heart.
            FloorDisc(t, "HavenSquareKerb", c + new Vector3(0f, 0.045f, 0f), 11.7f, KerbFloor);
            FloorDisc(t, "HavenSquare", c + new Vector3(0f, 0.055f, 0f), 11f, FlagstoneFloor);
            Transform well = Holder(t, "HavenWell", c, Quaternion.identity);
            LocalCyl(well, new Vector3(0f, 0.45f, 0f), 1.3f, 0.9f, kit.Mat("Stone"));
            var wellWater = new Material(kit.Mat("Water")) { name = "StillWellWater" };
            wellWater.SetFloat("_LivingWater", 0);
            LocalCyl(well, new Vector3(0f, 0.92f, 0f), 1.05f, 0.04f, wellWater, solid: false);
            LocalBox(well, new Vector3(-1.2f, 1.75f, 0f), new Vector3(0.18f, 3.5f, 0.18f), kit.Mat("Wood"));
            LocalBox(well, new Vector3(1.2f, 1.75f, 0f), new Vector3(0.18f, 3.5f, 0.18f), kit.Mat("Wood"));
            // A windlass between the posts, its bucket hanging over the water, under a tiled hood.
            LocalBox(well, new Vector3(0f, 2.15f, 0f), new Vector3(2.3f, 0.16f, 0.16f), kit.Mat("Wood"), false);
            LocalBox(well, new Vector3(0f, 1.7f, 0f), new Vector3(0.04f, 0.9f, 0.04f), kit.Mat("Rope"), false);
            LocalCyl(well, new Vector3(0f, 1.1f, 0f), 0.2f, 0.32f, kit.Mat("Wood"), false);
            var hood = new PieceMesh();
            GableRoof(hood, RoofStyle.RedShingle, new Vector3(0f, 2.75f, 0f), 0.75f, 0.8f, 0.35f, 3.3f, 372);
            hood.Build(LocalFrame(well, "WellRoof", Vector3.zero, 90f), "Roof");
            well.rotation = Quaternion.Euler(0f, -25f, 0f);
            Claim(c, 12f);

            // Reserve lanes before scattering vegetation or yard clutter.
            BuildHavenRoads(t);
            BuildVillageHomes(t);
            BuildSmithWorkshop(t, c + Flat(10f, -15f));
            BuildVillageCommons(t);
            foreach (Vector3 local in new[] { Flat(-9, -17), Flat(-21, 12), Flat(-31, -12),
                Flat(21, 31), Flat(95, -5), Flat(-23, -14) })
                Claim(c + local, 2f);

            // Market stalls on the west side of the plaza; the merchant stands at the first.
            string[] cloths = { "ClothRed", "ClothBlue", "ClothYellow" };
            for (int k = 0; k < 3; k++)
            {
                float rad = (200f + k * 38f) * Mathf.Deg2Rad;
                Vector3 p = c + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 15f;
                Stall(t, p, c, kit.Mat(cloths[k]));
                if (k == 0)
                    Spots["Merchant"] = p + (c - p).normalized * 2.2f;
            }

            // The stash: a banded chest by the stalls, opened like talking to someone.
            BuildStashChest(t, c + new Vector3(-7f, 0f, -8.5f), c);

            // Lamp posts round the plaza.
            for (int k = 0; k < 8; k++)
            {
                float rad = (k * 45f + 22f) * Mathf.Deg2Rad;
                Lamp(t, c + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 11.8f);
            }

            // Trees and bushes round the outskirts.
            Scatter(t, 16, 34f, 45f, p => Prefab(Coin() ? kit.oak : kit.pine, t, p, R(0f, 360f), Vector3.one * R(0.9f, 1.25f)), 2.2f);
            Scatter(t, 18, 30f, 46f, p => Prefab(kit.bushSmall, t, p, R(0f, 360f), Vector3.one * R(0.3f, 0.5f)), 0.6f);

            Spots["Elder"] = c + new Vector3(-3.5f, 0f, 3.5f);
            Spots["Smith"] = c + new Vector3(5.5f, 0f, -7f);
            Spots["WeaponMentor"] = c + new Vector3(3f, 0f, -8.5f);
            Spots["Guard"] = c + new Vector3(112f, 0f, 4f);

            // Four benches by the spawn point, side-on to the camera so the names of the things lying
            // on them don't overlap; a new character's first gear lies on them.
            Bench(t, c + new Vector3(-4.6f, 0f, -4.4f), 0f);
            Bench(t, c + new Vector3(4.2f, 0f, -2.6f), 0f);
            Bench(t, c + new Vector3(-5.4f, 0f, -0.8f), 0f);
            Bench(t, c + new Vector3(5.0f, 0f, 1.0f), 0f);
        }

        // Where the starter gear goes: two spots along the seat of each of Haven's benches.
        private readonly List<Vector3> starterSpots = new List<Vector3>();

        // A plain wooden bench: a seat on two slab legs, long side along the yaw.
        private void Bench(Transform t, Vector3 p, float yaw)
        {
            var bench = new GameObject("Bench").transform;
            bench.SetParent(t, false);
            bench.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            const float seatTop = 0.55f;
            // Fill the footprint so the player cannot slip below the seat and step up its edge.
            var blocker = bench.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, seatTop * 0.5f, 0f);
            blocker.size = new Vector3(3f, seatTop, 0.6f);
            LocalBox(bench, new Vector3(0f, seatTop - 0.05f, 0f), new Vector3(3f, 0.1f, 0.6f), kit.Mat("Wood"));
            for (int side = -1; side <= 1; side += 2)
                LocalBox(bench, new Vector3(side * 1.3f, (seatTop - 0.1f) * 0.5f, 0f), new Vector3(0.12f, seatTop - 0.1f, 0.5f), kit.Mat("Wood"));
            Claim(p, 1.8f);

            for (int side = -1; side <= 1; side += 2)
                starterSpots.Add(bench.TransformPoint(new Vector3(side * 1f, seatTop, 0f)));
        }

        // The Ashen Ruins: a broken temple on a dais (the boss arena), toppled columns, walls,
        // braziers, cracks of lava and burnt trees.
        private void BuildRuins()
        {
            Begin(Ruins, 303);
            Vector3 c = Centers[Ruins];
            Transform t = Group("Ruins");

            // The temple dais, with a ring of columns (some broken) and an altar.
            // The north-east pocket keeps the dais away from the waystone and the north gate.
            Vector3 temple = c + AreaLayouts.BossLocal(Ruins);
            PlanWeatheredSlab(t, "TempleDais", temple, new Vector2(10.3f, 9.8f), 0.14f, kit.Mat("Sandstone"), 18f, solid: false);
            for (int k = 0; k < 10; k++)
            {
                float rad = k * 36f * Mathf.Deg2Rad;
                Vector3 p = temple + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 8.5f;
                if (k == 3 || k == 7)
                    FallenColumn(t, p, k * 36f + 70f);
                else
                    PlanPrefab(kit.pillar, t, p + Vector3.up * 0.14f, R(0f, 360f), new Vector3(1f, R(0.45f, 1.05f), 1f));
            }
            PlanWeatheredSlab(t, "AltarBase", temple + new Vector3(0f, 0.14f, 3f), new Vector2(1.3f, 0.85f), 0.85f, kit.Mat("Sandstone"), 18f);
            PlanWeatheredSlab(t, "AltarCap", temple + new Vector3(0f, 0.99f, 3f), new Vector2(1.5f, 1f), 0.15f, kit.Mat("Sandstone"), 18f);
            PlanFlame(t, temple + new Vector3(-0.7f, 1.34f, 3f), 0.35f, kit.Mat("Ember"));
            PlanFlame(t, temple + new Vector3(0.7f, 1.34f, 3f), 0.35f, kit.Mat("Ember"));
            PlanGlow(t, temple + new Vector3(0f, 1.9f, 3f), FireLight, 10f, 5f, flicker: true);
            Claim(temple, 11f);
            Spots["Altar"] = temple;

            // Broken walls: coursed stone piers with gaps, ragged tops and rubble.
            for (int walls = 0, attempts = 0; walls < 6 && attempts < 80; attempts++)
            {
                Vector3 start = c + Flat(R(-140f, 140f), R(-110f, 110f));
                if (!Free(start, 5f) || !Shape(Ruins).Contains(start, 16f))
                    continue;
                RuinedWall(t, start, R(0f, 180f), rng.Next(3, 7));
                walls++;
            }

            // Cracks of lava in the ash.
            for (int k = 0; k < 16; k++)
            {
                Vector3 p = c + Flat(R(-140f, 140f), R(-110f, 110f));
                if (Vector3.Distance(p, temple) < 11f || !Shape(Ruins).Contains(p, 5f))
                    continue;
                LavaCrack(t, p, R(0.25f, 0.5f), R(3f, 8f), R(0f, 180f));
                if (k % 2 == 0)
                    PlanGlow(t, p + Vector3.up * 0.5f, LavaLight, 5.5f, 2.5f, flicker: true);
            }

            Scatter(t, 8, 10f, 42f, p => Brazier(t, p), 1f);
            Scatter(t, 14, 8f, 46f, p => DeadTree(t, p, kit.Mat("Charred")), 1.6f);
            Scatter(t, 10, 8f, 46f, p => Ball(t, p, R(0.6f, 1.2f), kit.Mat("Ash"), flatten: 0.35f, solid: false), 1f);
            Scatter(t, 14, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.5f)), 1.5f);
        }

        // A crack of lava seeping through the ash: a channel that wanders, tapers to nothing at both
        // ends and sometimes forks, with ragged sides, on a wider scorched margin. Its shape has its
        // own seed, so the area's other scenery lands where it always has.
        private void LavaCrack(Transform t, Vector3 p, float width, float length, float yaw)
        {
            if (DeferContent(p, () => LavaCrack(t, p, width, length, yaw))) return;
            var shape = new System.Random(Mathf.RoundToInt(p.x * 131f + p.z * 17f + yaw * 7f));
            float Rand(float min, float max) => min + (float)shape.NextDouble() * (max - min);

            // Strands, laid out along the crack's own forward (the way its lava flows): the main crack
            // centred on the spot, then a fork or two off its middle.
            var strands = new List<(List<Vector3> points, float width, bool fork)>();
            List<Vector3> main = CrackPath(Vector3.back * length * 0.5f, 0f, length, Rand);
            strands.Add((main, width, false));
            for (int forks = shape.Next(0, 3), f = 0; f < forks; f++)
            {
                int at = shape.Next(main.Count / 4, main.Count * 3 / 4);
                Vector3 along = main[at + 1] - main[at];
                float turn = Rand(0.5f, 1.05f) * (shape.Next(2) == 0 ? -1f : 1f);
                strands.Add((CrackPath(main[at], Mathf.Atan2(along.x, along.z) + turn, length * Rand(0.25f, 0.45f), Rand),
                    width * Rand(0.45f, 0.65f), true));
            }

            var crack = new GameObject("LavaCrack");
            crack.transform.SetParent(t, false);
            crack.transform.SetPositionAndRotation(p + Vector3.up * 0.04f, Quaternion.Euler(0f, yaw, 0f));
            crack.AddComponent<MeshFilter>().sharedMesh = CrackMesh("LavaCrack", strands, 1f, 0f, 0.65f, 1.15f, Rand);
            crack.AddComponent<MeshRenderer>();
            PoeClone.Visuals.LavaSurface.Attach(crack, emitFromMesh: true);

            var scorch = new GameObject("Scorch");
            scorch.transform.SetParent(crack.transform, false);
            scorch.transform.localPosition = Vector3.down * 0.02f;
            scorch.AddComponent<MeshFilter>().sharedMesh = CrackMesh("LavaScorch", strands, 2.4f, 0.12f, 0.55f, 1.35f, Rand);
            var scorchRenderer = scorch.AddComponent<MeshRenderer>();
            scorchRenderer.sharedMaterial = kit.Mat("Charred");
            scorchRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // A wandering line from start: the heading drifts a little each step but keeps its course.
        private static List<Vector3> CrackPath(Vector3 start, float heading, float length, System.Func<float, float, float> rand)
        {
            int steps = Mathf.Max(5, Mathf.RoundToInt(length / 0.3f));
            float step = length / steps, course = heading;
            var points = new List<Vector3> { start };
            for (int k = 0; k < steps; k++)
            {
                heading = Mathf.Lerp(heading + rand(-0.45f, 0.45f), course, 0.25f);
                start += new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)) * step;
                points.Add(start);
            }
            return points;
        }

        // Flat ribbons along each strand, each side ragged on its own. The main crack swells in the
        // middle and pinches out at both ends; a fork starts wide where it leaves and thins to a point.
        private static Mesh CrackMesh(string name, List<(List<Vector3> points, float width, bool fork)> strands, float widen, float margin,
            float raggedMin, float raggedMax, System.Func<float, float, float> rand)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var strand in strands)
            {
                List<Vector3> points = strand.points;
                int first = vertices.Count;
                for (int k = 0; k < points.Count; k++)
                {
                    float u = k / (float)(points.Count - 1);
                    float profile = strand.fork ? Mathf.Pow(1f - u, 0.7f) : Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.6f);
                    float half = profile > 0.001f ? (strand.width * profile * widen + margin) * 0.5f : 0f;
                    Vector3 tangent = points[Mathf.Min(k + 1, points.Count - 1)] - points[Mathf.Max(k - 1, 0)];
                    Vector3 side = new Vector3(tangent.z, 0f, -tangent.x).normalized;
                    vertices.Add(points[k] + side * half * rand(raggedMin, raggedMax));
                    vertices.Add(points[k] - side * half * rand(raggedMin, raggedMax));
                    if (k == 0)
                        continue;
                    int a = first + (k - 1) * 2;
                    triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The Frozen Hollow: snow, frosted pines, ice crystals and frozen ponds round the queen's
        // throne, a ring of ice spikes north-east of the centre, clear of the waystone-to-door route.
        private void BuildFrozen()
        {
            Begin(Frozen, 404);
            Vector3 c = Centers[Frozen];
            Transform t = Group("Frozen");

            Vector3 throne = c + AreaLayouts.BossLocal(Frozen);
            PlanCyl(t, throne + Vector3.up * 0.04f, 9f, 0.08f, kit.Mat("Ice"), solid: false);
            for (int k = 0; k < 12; k++)
            {
                float rad = k * 30f * Mathf.Deg2Rad;
                Vector3 p = throne + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 9.5f;
                if (k == 9)
                    continue; // the way in, facing south
                PlanIceSpike(t, p, R(2.2f, 3.6f), new Vector3(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
            }
            PlanBox(t, throne + new Vector3(0f, 0.9f, 3.2f), new Vector3(2.6f, 1.8f, 1f), kit.Mat("Ice"));
            PlanBox(t, throne + new Vector3(0f, 0.35f, 2.4f), new Vector3(2.2f, 0.7f, 1.2f), kit.Mat("Ice"));
            Claim(throne, 11f);
            Spots["Throne"] = throne;

            // Frozen ponds.
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = c + Flat(R(-130f, 130f), R(-140f, 140f));
                float radius = R(3f, 6f);
                if (!Free(p, radius) || !Shape(Frozen).Contains(p, radius + 2f))
                    continue;
                PlanCyl(t, p + Vector3.up * 0.03f, radius, 0.06f, kit.Mat("Ice"), solid: false);
                Claim(p, radius);
            }

            Scatter(t, 14, 8f, 46f, p => CrystalCluster(t, p), 1.6f);
            Scatter(t, 20, 14f, 46f, p => FrostedPine(t, p), 2f);
            Scatter(t, 14, 6f, 46f, p => SnowDrift(t, p, R(0.7f, 1.5f)), 1.2f);
            Scatter(t, 12, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.6f)), 1.5f);
        }

        // Faceted lumps of snow banked against each other, highest on one side like a wind drift.
        // Its own seed from the spot keeps the area's scatter sequence unchanged.
        private GameObject SnowDrift(Transform t, Vector3 p, float size)
        {
            var random = new System.Random(Mathf.RoundToInt(p.x * 13f + p.z * 7f));
            var drift = new GameObject("SnowDrift").transform;
            drift.SetParent(t, false);
            drift.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
            int lumps = 2 + random.Next(3);
            for (int k = 0; k < lumps; k++)
            {
                float scale = size * (1f - k * 0.18f) * (0.8f + (float)random.NextDouble() * 0.3f);
                var lump = new GameObject("SnowLump");
                lump.transform.SetParent(drift, false);
                lump.transform.localPosition = new Vector3(k * size * 0.75f, -scale * 0.12f, ((float)random.NextDouble() - 0.5f) * size * 0.6f);
                lump.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                lump.transform.localScale = new Vector3(scale * 2.2f, scale * (0.75f - k * 0.12f), scale * 1.6f);
                lump.AddComponent<MeshFilter>().sharedMesh = kit.icoHead;
                var renderer = lump.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = kit.Mat("Snow");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return drift.gameObject;
        }

        // A tall cone of ice (a box where the kit has no cone mesh).
        private GameObject IceSpike(Transform t, Vector3 p, float height, Vector3 euler)
        {
            if (kit.cone == null)
                return Box(t, p + Vector3.up * height * 0.5f, new Vector3(0.6f, height, 0.6f), kit.Mat("Ice"), euler: euler);

            var spike = new GameObject("IceSpike");
            spike.transform.SetParent(t, false);
            spike.transform.SetPositionAndRotation(p, Quaternion.Euler(euler));
            spike.transform.localScale = new Vector3(0.9f, height, 0.9f);
            spike.AddComponent<MeshFilter>().sharedMesh = kit.cone;
            spike.AddComponent<MeshRenderer>().sharedMaterial = kit.Mat("Ice");
            var col = spike.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 1f;
            col.center = new Vector3(0f, 0.5f, 0f);
            return spike;
        }

        private GameObject CrystalCluster(Transform t, Vector3 p)
        {
            var group = new GameObject("Crystals").transform;
            group.SetParent(t, false);
            group.position = p;
            int n = rng.Next(2, 5);
            for (int k = 0; k < n; k++)
            {
                Vector3 offset = Flat(R(-0.8f, 0.8f), R(-0.8f, 0.8f));
                IceSpike(group, p + offset, R(0.8f, 2.2f), new Vector3(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)));
            }
            Glow(group, p + Vector3.up * 1.3f, IceLight, 6.5f, 3f);
            return group.gameObject;
        }

        // A pine dusted with snow: its own materials, tinted pale.
        private GameObject FrostedPine(Transform t, Vector3 p)
        {
            GameObject pine = Prefab(kit.pine, t, p, R(0f, 360f), Vector3.one * R(0.9f, 1.3f));
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in pine.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(0.78f, 0.88f, 0.92f));
                r.SetPropertyBlock(block);
            }
            return pine;
        }

        private void SetUpSpawners()
        {
            EnemySpawner original = originalSpawner;
            if (original == null || original.EnemyPrefab == null)
                return;

            GameObject prefab = original.EnemyPrefab;
            if (NeedsArea(Greenwood))
            {
                original.Configure(prefab, Centers[Greenwood], AreaShape.MaxRadius, 48, MonsterLevels[Greenwood], KindWeights[Greenwood]);
                original.SetSafeSpots(SafeSpots(Greenwood));
                original.SetArrivalSpots(ArrivalSpots(Greenwood));
                AreaShape greenwood = Shape(Greenwood);
                original.SetBounds(p => greenwood.Contains(p, 3f));
                spawnersByArea[Greenwood] = original;

                original.ConfigureStreaming(Greenwood);
                original.enabled = false;
            }

            foreach (int area in new[] { Graveyard, Ruins, Frozen, Cave, SaltBasin, Channels, DrownedWorks, Orchard, Reservoir, BuriedWard, FirstRoot })
            {
                if (!NeedsArea(area)) continue;
                var go = new GameObject("Spawner_" + AreaNames[area]);
                go.transform.SetParent(root, false);
                go.transform.position = Centers[area];
                var spawner = go.AddComponent<EnemySpawner>();
                int count = area == Orchard ? 68 : area == Reservoir || area == BuriedWard || area == FirstRoot ? 72 : 48;
                spawner.Configure(prefab, Centers[area], AreaShape.MaxRadius, count, MonsterLevels[area], KindWeights[area]);
                ConfigureCourtPopulation(spawner, area);
                ConfigureRootPopulation(spawner, area);
                spawner.SetSafeSpots(SafeSpots(area));
                spawner.SetArrivalSpots(ArrivalSpots(area));
                AreaShape shape = Shape(area);
                spawner.SetBounds(p => shape.Contains(p, 3f));
                spawner.ConfigureStreaming(area);
                spawnersByArea[area] = spawner;
            }

            // Off until OnAreaChanged switches on the current area's: the build spans several
            // frames, and a spawner's Start (its first wave) must wait for its area's first visit.
            foreach (EnemySpawner spawner in spawnersByArea)
                if (spawner != null && spawner == spawnersByArea[loadingArea]) spawner.enabled = false;
        }

        // One EnemySpawner per area (null for Haven, the town), so its enemies can be parked while
        // the player is elsewhere instead of chasing/animating/physics-ticking miles off screen -
        // which is most of the areas, most of the time, and was the main cost behind even Haven
        // (otherwise nearly empty) running slowly on weaker/mobile devices.
        private EnemySpawner[] spawnersByArea;

        // A spectator's scene mirrors whichever area the real player it's watching is in purely for
        // display (see SpectatorReplica), but its own EnemySpawners are deliberately and permanently
        // disabled there (no local respawning of puppet stand-ins) - leave that alone rather than
        // flipping them back on here.
        private void OnAreaChanged(int area)
        {
            if (spawnersByArea == null)
                return;
            if (GameSessionController.Instance != null && GameSessionController.Instance.Role == SessionRole.Spectator)
                return;

            for (int i = 0; i < spawnersByArea.Length; i++)
            {
                EnemySpawner spawner = spawnersByArea[i];
                if (spawner != null)
                    spawner.SetAreaActive(i == area);
            }
        }

        // The Gravelord in front of the graveyard's crypt, the Ashen Warlord on the temple altar.
        private void PlaceBosses()
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null)
                return;

            Transform t = Group("Bosses");
            if (NeedsArea(Graveyard) && Spots.TryGetValue("Crypt", out Vector3 crypt))
                BossLair.Create(t, Graveyard, BossIndex("Gravelord Mortis"), MonsterLevels[Graveyard], spawner.EnemyPrefab, crypt, Vector3.back);
            if (NeedsArea(Ruins) && Spots.TryGetValue("Altar", out Vector3 altar))
                BossLair.Create(t, Ruins, BossIndex("Ashen Warlord"), MonsterLevels[Ruins], spawner.EnemyPrefab, altar, Vector3.back);
            if (NeedsArea(Frozen) && Spots.TryGetValue("Throne", out Vector3 throne))
                BossLair.Create(t, Frozen, BossIndex("Rimeheart"), MonsterLevels[Frozen], spawner.EnemyPrefab, throne, Vector3.back);
            foreach (FieldLair lair in FieldLairs)
                if (NeedsArea(lair.Area)) BossLair.Create(t, lair.Area, BossIndex(lair.Boss), MonsterLevels[lair.Area], spawner.EnemyPrefab, Center(lair.Area) + lair.Local, lair.Facing);
            if (NeedsArea(SaltBasin)) BossLair.Create(t, SaltBasin, BossIndex("Uruun, the Salt Matriarch"), MonsterLevels[SaltBasin],
                spawner.EnemyPrefab, Center(SaltBasin) + BasinMatriarchLocal, Vector3.back);
            if (NeedsArea(SaltBasin)) BossLair.Create(t, SaltBasin, BossIndex("Namar, Keeper of the Dry Well"), MonsterLevels[SaltBasin],
                spawner.EnemyPrefab, Center(SaltBasin) + BasinKeeperLocal, Vector3.back);
            if (NeedsArea(Channels)) BossLair.Create(t, Channels, BossIndex("The Sluice Warden"), MonsterLevels[Channels],
                spawner.EnemyPrefab, Center(Channels) + ChannelWardenLocal, Vector3.back);
            if (NeedsArea(DrownedWorks)) BossLair.Create(t, DrownedWorks, BossIndex("The Last Engineer"), MonsterLevels[DrownedWorks],
                spawner.EnemyPrefab, Center(DrownedWorks) + WorksEngineerLocal, Vector3.back);
            foreach (int area in new[] { Orchard, Reservoir, BuriedWard, FirstRoot })
                if (NeedsArea(area)) BossLair.Create(t, area, BossIndex(area == Orchard ? "Mother of the Last Harvest" : area == Reservoir ? "Ishara, Keeper of the Borrowed River" :
                    area == BuriedWard ? "Ossan, Warden of the Unremembered" : "The First Root, Heart of the Tithe"),
                    MonsterLevels[area], spawner.EnemyPrefab, Center(area) + AreaLayouts.BossLocal(area), Vector3.back);
        }

        private static int BossIndex(string name)
        {
            for (int k = 0; k < EnemyKinds.All.Length; k++)
            {
                if (EnemyKinds.All[k].Name == name)
                    return k;
            }
            return 0;
        }

        // Haven's people, at their spots: the Elder by the well, the Merchant at the first stall,
        // the Smith by the forge corner and the Guard at the east gate. Same rig as the monsters.
        private void BuildTownsfolk()
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null)
                return;

            Transform t = Group("Townsfolk");
            Vector3 c = Centers[Haven];
            AddNpc(spawner.EnemyPrefab, t, NpcRole.Elder, "Elder Maren", c, new EnemyKind
            {
                Name = "Elder", Scale = 0.97f, HideHorns = true,
                Cloth = new Color(0.36f, 0.26f, 0.48f), Pants = new Color(0.25f, 0.20f, 0.30f),
                Skin = new Color(0.80f, 0.66f, 0.55f), Eyes = new Color(0.15f, 0.15f, 0.2f)
            });
            AddNpc(spawner.EnemyPrefab, t, NpcRole.Merchant, "Merchant Oda", c, new EnemyKind
            {
                Name = "Merchant", Scale = 1f, HideHorns = true,
                Cloth = new Color(0.25f, 0.52f, 0.32f), Pants = new Color(0.45f, 0.35f, 0.22f),
                Skin = new Color(0.62f, 0.46f, 0.34f), Eyes = new Color(0.1f, 0.08f, 0.06f)
            });
            AddNpc(spawner.EnemyPrefab, t, NpcRole.Smith, "Smith Bram", c, new EnemyKind
            {
                Name = "Smith", Scale = 1.08f, HideHorns = true,
                Cloth = new Color(0.42f, 0.30f, 0.20f), Pants = new Color(0.22f, 0.20f, 0.19f),
                Skin = new Color(0.74f, 0.55f, 0.42f), Eyes = new Color(0.1f, 0.08f, 0.06f),
                Gear = new[] { "iron_mace" }
            });
            AddNpc(spawner.EnemyPrefab, t, NpcRole.Guard, "Captain Hale", c, new EnemyKind
            {
                Name = "Guard", Scale = 1.04f, HideHorns = true,
                Cloth = new Color(0.22f, 0.32f, 0.58f), Pants = new Color(0.30f, 0.30f, 0.34f),
                Skin = new Color(0.78f, 0.62f, 0.50f), Eyes = new Color(0.1f, 0.1f, 0.15f),
                Gear = new[] { "iron_helmet", "rusty_sword", "wooden_shield" }
            });

            AddNpc(spawner.EnemyPrefab, t, NpcRole.WeaponMentor, "Mentor Ilyra", c, new EnemyKind
            {
                Name = "Weapon Mentor", Scale = 1f, HideHorns = true,
                Cloth = new Color(0.28f, 0.42f, 0.48f), Pants = new Color(0.20f, 0.21f, 0.27f),
                Skin = new Color(0.72f, 0.56f, 0.43f), Eyes = new Color(0.16f, 0.25f, 0.3f)
            });
            BuildVillageFolk(spawner.EnemyPrefab, t);

        }

        private Npc AddNpc(GameObject prefab, Transform parent, NpcRole role, string name, Vector3 faceTowards, EnemyKind look)
        {
            if (!Spots.TryGetValue(role.ToString(), out Vector3 spot))
                return null;
            if (DeferContent(spot, () =>
            {
                Npc npc = AddNpc(prefab, parent, role, name, faceTowards, look);
                if (role == NpcRole.Guide && npc != null)
                    PoeClone.Quests.QuestCaptive.Attach(npc, "captive", spot);
            }, canClear: false)) return null;
            Vector3 position = spot + Vector3.up * 1.1f * look.Scale;
            Vector3 facing = faceTowards - spot;
            facing.y = 0f;
            return Npc.Create(prefab, role, name, look, position, facing, parent);
        }

        private List<Vector3> ArrivalSpots(int area)
        {
            var spots = new List<Vector3> { WaystoneSpot(area), spawnPoints[area].position };
            foreach (var gate in gatePoints)
                if (gate.area == area) spots.Add(gate.position);
            string prefix = "Arrive_" + AreaNames[area] + "_";
            foreach (Transform child in root)
                if (child.name.StartsWith(prefix)) spots.Add(child.position);
            return spots;
        }

        // Keep boss clearings and quest props clear of ordinary packs.
        private List<Vector3> SafeSpots(int area)
        {
            var spots = new List<Vector3>();
            // Keep ordinary opening packs and replacements out of the boss clearings.
            if (area == SaltBasin)
            {
                spots.Add(Center(area) + BasinMatriarchLocal);
                spots.Add(Center(area) + BasinKeeperLocal);
            }
            if (area == Channels) spots.Add(Center(area) + ChannelWardenLocal);
            if (area == DrownedWorks) spots.Add(Center(area) + WorksEngineerLocal);
            if (area == Orchard || area == Reservoir || area == BuriedWard || area == FirstRoot) spots.Add(Center(area) + AreaLayouts.BossLocal(area));
            if (UsesChunks(area))
            {
                foreach (string group in AreaLayouts.QuestGroups(area))
                    spots.AddRange(QuestPoints(area, group));
                spots.Add(Center(area) + AreaLayouts.BossLocal(area));
                foreach (var lair in FieldLairs)
                    if (lair.Area == area) spots.Add(Center(area) + lair.Local);
            }
            // Quest props shouldn't have a monster standing on them when the player gets there.
            Transform sites = root.Find("QuestSites");
            if (sites != null)
            {
                foreach (Transform site in sites)
                {
                    if (Shape(area).Contains(site.position))
                        spots.Add(site.position);
                }
            }
            return spots;
        }

        // ------------------------------------------------------------------ gates

        private GameObject CreateGateTemplate()
        {
            var gate = new GameObject("GateTemplate");
            gate.transform.SetParent(root, false);
            gate.SetActive(false);

            Material stone = kit.Mat("Stone");
            for (int side = -1; side <= 1; side += 2)
            {
                var pillar = new GameObject("Pillar").transform;
                pillar.SetParent(gate.transform, false);
                pillar.localPosition = new Vector3(side * 2.6f, 0f, 0f);
                LocalBox(pillar, new Vector3(0f, 0.175f, 0f), new Vector3(1.1f, 0.35f, 1.1f), stone, solid: false).name = "Base";
                LocalCyl(pillar, new Vector3(0f, 2f, 0f), 0.7f, 3.3f, stone, solid: false).name = "Column";
                LocalBox(pillar, new Vector3(0f, 3.7f, 0f), new Vector3(1f, 0.3f, 1f), stone, solid: false).name = "Cap";
                // Colliderless parts lose their shadow by default; without the pillars the
                // lintel's shadow floats on the grass away from the gate.
                foreach (Renderer r in pillar.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var pillarCollider = pillar.gameObject.AddComponent<BoxCollider>();
                pillarCollider.center = new Vector3(0f, 1.925f, 0f);
                pillarCollider.size = new Vector3(1f, 3.85f, 1f);
            }
            LocalBox(gate.transform, new Vector3(0f, 3.85f, 0f), new Vector3(6.2f, 0.4f, 1f), stone).name = "Lintel";

            Shader portalShader = Shader.Find("Universal Render Pipeline/Lit") ?? kit.Mat("Water").shader;
            Material portal = new Material(portalShader);
            portal.SetColor("_BaseColor", new Color(.3f, .4f, .5f));
            portal.SetFloat("_Smoothness", .1f);
            portal.EnableKeyword("_EMISSION");
            portal.SetColor("_EmissionColor", new Color(.15f, .2f, .25f));
            LocalBox(gate.transform, new Vector3(0f, 1.9f, 0f), new Vector3(4.2f, 3.4f, .15f), portal, solid: false).name = "PortalPanel";

            var trigger = gate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0f);
            trigger.size = new Vector3(4f, 2.4f, 1.6f);
            gate.AddComponent<AreaGate>();
            return gate;
        }

        // A gate in each area leading to the other, at its authored spot (AreaLayouts.GateLocal);
        // you arrive a few steps in front of the gate back.
        private void Connect(int a, int b)
        {
            Vector3 gateA = GateByWall(a, Center(a) + AreaLayouts.GateLocal(a, b), AreaLayouts.GateYaw(a, b));
            Vector3 gateB = GateByWall(b, Center(b) + AreaLayouts.GateLocal(b, a), AreaLayouts.GateYaw(b, a));
            Transform arriveInA = Marker("Arrive_" + AreaNames[a] + "_from_" + AreaNames[b], InFront(a, b, gateA), AreaLayouts.GateYaw(a, b));
            Transform arriveInB = Marker("Arrive_" + AreaNames[b] + "_from_" + AreaNames[a], InFront(b, a, gateB), AreaLayouts.GateYaw(b, a));
            ClearSpot(arriveInA.position, 2f);
            ClearSpot(arriveInB.position, 2f);

            Gate(a, gateA, b, arriveInB);
            Gate(b, gateB, a, arriveInA);
        }

        // Keep the doorway in its authored passage, backed by the nearest outer wall.
        private static Vector3 GateByWall(int area, Vector3 authored, float yaw)
        {
            AreaShape shape = Shape(area);
            Vector3 inward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * 3.6f;
            Vector3 result = authored;
            for (float d = 0.25f; d <= 32f; d += 0.25f)
            {
                Vector3 next = authored - inward * d;
                if (!shape.Contains(next, 0.8f) || !shape.Contains(next + side, 0.5f) || !shape.Contains(next - side, 0.5f))
                    break;
                result = next;
            }
            return result;
        }

        private void Gate(int from, Vector3 position, int to, Transform arrival, float? yaw = null)
        {
            gatePoints.Add((from, position));
            ClearSpot(position, 4.5f);
            GameObject gate = Instantiate(gateTemplate, root);
            gate.name = "Gate_" + AreaNames[from] + "_to_" + AreaNames[to];
            gate.transform.position = new Vector3(position.x, gateTemplate.transform.position.y, position.z);
            // Face down the final approach, keeping the opening square across the path.
            gate.transform.rotation = Quaternion.Euler(0f, yaw ?? AreaLayouts.GateYaw(from, to), 0f);

            AreaGate g = gate.GetComponent<AreaGate>();
            g.fromAreaIndex = from;
            g.targetAreaIndex = to;
            g.arrival = arrival;
            if (from == Reservoir && to == BuriedWard)
            {
                g.Locked = () => PoeClone.Quests.QuestLog.Instance == null || !PoeClone.Quests.QuestLog.Instance.PropUsed("act2_release#0");
                g.LockedMessage = "Open the river return sluice to drain the lower stair";
            }
            if (from == BuriedWard && to == FirstRoot)
            {
                g.Locked = () => PoeClone.Quests.QuestLog.Instance == null || !PoeClone.Quests.QuestLog.Instance.PropUsed("boss:Ossan, Warden of the Unremembered");
                g.LockedMessage = "Ossan still holds the root door";
            }

            Renderer panel = FindRenderer(gate.transform, "PortalPanel");
            if (panel != null)
            {
                var block = new MaterialPropertyBlock();
                panel.GetPropertyBlock(block);
                block.SetColor("_BaseColor", AreaColors[to] * 0.6f);
                block.SetColor("_EmissionColor", AreaColors[to] * 0.35f);
                panel.SetPropertyBlock(block);
            }

            WorldLabel.Create(gate.transform, "To " + AreaNames[to], AreaColors[to] + new Color(0.2f, 0.2f, 0.2f), 5.2f);
            Claim(position, 4f);
            gate.SetActive(true);
        }

        private static Vector3 InFront(int area, int destination, Vector3 gate)
        {
            Vector3 inward = Quaternion.Euler(0f, AreaLayouts.GateYaw(area, destination), 0f) * Vector3.forward;
            return gate + inward.normalized * 5f + Vector3.up * 1.1f;
        }

        private static float Yaw(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // The original forest has trees everywhere; make room where a gate goes.
        // Groups whose direct children are loose scenery (the original forest's, and the built
        // areas' own), which may be cleared away from gates, arrivals and waystones.
        private static readonly HashSet<string> ClearableGroups = new HashSet<string>
        {
            "Trees", "Rocks", "Bushes", "Houses", "Haven", "Graveyard", "Ruins", "Frozen", "Cave", "Outskirts", "Glowshrooms"
        };

        private static void ClearSpot(Vector3 at, float radius)
        {
            foreach (Collider c in Physics.OverlapSphere(at + Vector3.up, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<EnemyHealth>() != null ||
                    c.GetComponentInParent<AreaGate>() != null || c.GetComponentInParent<Waystone>() != null || c.GetComponentInParent<Npc>() != null)
                    continue;
                if (c.gameObject.name.StartsWith("Ground") || c.gameObject.name == "Wall")
                    continue;

                Transform prop = c.transform;
                while (prop.parent != null && !ClearableGroups.Contains(prop.parent.name))
                    prop = prop.parent;
                if (prop.parent == null)
                    continue;

                // Off at once (physics and later checks ignore it), gone at the end of the frame.
                prop.gameObject.SetActive(false);
                Destroy(prop.gameObject);
            }
        }

        private static Renderer FindRenderer(Transform rootTransform, string name)
        {
            foreach (Renderer r in rootTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == name)
                    return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ props

        private void Stall(Transform t, Vector3 p, Vector3 faceTowards, Material cloth)
        {
            if (DeferContent(p, () => Stall(t, p, faceTowards, cloth))) return;
            var stall = new GameObject("Stall").transform;
            stall.SetParent(t, false);
            stall.position = p;
            Vector3 d = faceTowards - p;
            stall.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z));

            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    LocalBox(stall, new Vector3(x * 1.3f, 1.2f, z * 0.8f), new Vector3(0.15f, 2.4f, 0.15f), kit.Mat("Wood"));
            LocalBox(stall, new Vector3(0f, 0.55f, 0.75f), new Vector3(2.6f, 1.1f, 0.6f), kit.Mat("Wood"), solid: true);
            LocalBox(stall, new Vector3(0f, 2.55f, 0f), new Vector3(3.1f, 0.15f, 2.2f), cloth, euler: new Vector3(-8f, 0f, 0f));
            LocalBox(stall, new Vector3(-0.6f, 1.2f, 0.75f), new Vector3(0.5f, 0.2f, 0.4f), kit.Mat("ClothGreen"), solid: false);
            LocalBox(stall, new Vector3(0.5f, 1.25f, 0.7f), new Vector3(0.4f, 0.3f, 0.3f), kit.Mat("Pumpkin"), solid: false);
            for (int strip = -1; strip <= 1; strip++)
                WindCloth.Create(stall, new Vector3(strip * 0.95f, 2.42f, -1.05f), 0.94f, 0.28f, cloth);
            // Stock waits beside the counter.
            Crate(t, stall.TransformPoint(new Vector3(-1.95f, 0f, 0.55f)), stall.eulerAngles.y + 12f);
            Barrel(t, stall.TransformPoint(new Vector3(1.95f, 0f, 0.7f)));
            Claim(p, 2.6f);
        }

        // A plank crate bound with iron at its edges.
        private void Crate(Transform t, Vector3 p, float yaw)
        {
            if (DeferContent(p, () => Crate(t, p, yaw))) return;
            var mesh = new PieceMesh();
            mesh.Block(mesh.Slot(kit.Mat("Wood")), Vector3.up * 0.4f, Vector3.right * 0.4f, Vector3.up * 0.4f, Vector3.forward * 0.4f);
            int iron = mesh.Slot(kit.Mat("Iron"));
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    mesh.Block(iron, new Vector3(a * 0.4f, 0.4f, b * 0.4f), Vector3.right * 0.05f, Vector3.up * 0.41f, Vector3.forward * 0.05f);
                    mesh.Block(iron, new Vector3(0f, 0.4f + a * 0.4f, b * 0.4f), Vector3.right * 0.41f, Vector3.up * 0.05f, Vector3.forward * 0.05f);
                    mesh.Block(iron, new Vector3(b * 0.4f, 0.4f + a * 0.4f, 0f), Vector3.right * 0.05f, Vector3.up * 0.05f, Vector3.forward * 0.41f);
                }
            GameObject crate = mesh.Build(t, "Crate");
            crate.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            var collider = crate.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 0.4f;
            collider.size = Vector3.one * 0.8f;
        }

        // A bellied barrel with three iron hoops.
        private void Barrel(Transform t, Vector3 p)
        {
            if (DeferContent(p, () => Barrel(t, p))) return;
            var mesh = new PieceMesh();
            int wood = mesh.Slot(kit.Mat("Wood"));
            mesh.Frustum(wood, Vector3.zero, Vector3.up * 0.5f, 0.32f, 0.4f, 10, Vector3.forward);
            mesh.Frustum(wood, Vector3.up * 0.5f, Vector3.up, 0.4f, 0.32f, 10, Vector3.forward);
            int iron = mesh.Slot(kit.Mat("Iron"));
            foreach (float y in new[] { 0.14f, 0.5f, 0.86f })
            {
                float r = 0.32f + 0.08f * (1f - Mathf.Abs(y - 0.5f) * 2f) + 0.025f;
                mesh.Frustum(iron, Vector3.up * (y - 0.035f), Vector3.up * (y + 0.035f), r, r, 10, Vector3.forward);
            }
            GameObject barrel = mesh.Build(t, "Barrel");
            barrel.transform.position = p;
            var collider = barrel.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.5f;
            collider.radius = 0.38f;
            collider.height = 1f;
        }

        private void Lamp(Transform t, Vector3 p)
        {
            if (DeferContent(p, () => Lamp(t, p))) return;
            Box(t, p + Vector3.up * 1.1f, new Vector3(0.14f, 2.2f, 0.14f), kit.Mat("Iron"));
            Box(t, p + Vector3.up * 2.25f, new Vector3(0.5f, 0.08f, 0.5f), kit.Mat("Iron"), solid: false);
            Box(t, p + new Vector3(0f, 2.75f, 0f), new Vector3(0.5f, 0.08f, 0.5f), kit.Mat("Iron"), solid: false);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(t, p + new Vector3(x * 0.21f, 2.5f, z * 0.21f), new Vector3(0.035f, 0.46f, 0.035f), kit.Mat("Iron"), solid: false);
            Flame(t, p + new Vector3(0f, 2.46f, 0f), 0.12f, kit.Mat("Lantern"), 1.35f, 0.2f);
            Glow(t, p + new Vector3(0f, 2.3f, 0f), LanternLight, 10f, 8f, true);
            Claim(p, 0.6f);
        }

        private void FallenColumn(Transform t, Vector3 p, float yaw)
        {
            if (DeferContent(p, () => FallenColumn(t, p, yaw))) return;
            Cyl(t, p + Vector3.up * 0.55f, 0.55f, 3.5f, kit.Mat("Sandstone"), euler: new Vector3(90f, yaw, 0f));
        }

        private Material sootyStone;
        private Material SootyStone
        {
            get
            {
                if (sootyStone != null) return sootyStone;
                sootyStone = new Material(kit.Mat("Sandstone")) { name = "SootyStone" };
                sootyStone.SetColor("_BaseColor", sootyStone.GetColor("_BaseColor") * new Color(0.58f, 0.54f, 0.5f));
                return sootyStone;
            }
        }

        // A broken wall: piers of staggered stone courses on a wider footing, with ragged,
        // fire-blackened tops, gaps where piers have fallen, and their rubble at the foot.
        private void RuinedWall(Transform t, Vector3 start, float yaw, int piers)
        {
            if (planningContent) Claim(start + Quaternion.Euler(0, yaw, 0) * Vector3.forward * (piers * 2.1f * 0.5f), piers * 1.1f + 1f);
            if (DeferContent(start, () => RuinedWall(t, start, yaw, piers))) return;
            const float pierLength = 2f, spacing = 2.1f, thickness = 0.9f, course = 0.42f;
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 along = facing * Vector3.forward, across = facing * Vector3.right;
            Material stone = kit.Mat("Sandstone");
            Transform wall = new GameObject("RuinedWall").transform;
            wall.SetParent(t, false);
            var batch = new PrimitiveBatch();
            Material Pick() => rng.NextDouble() < 0.3 ? SootyStone : stone;
            void Rubble(Vector3 around, int count)
            {
                for (int k = 0; k < count; k++)
                {
                    Vector3 size = new Vector3(R(0.35f, 0.8f), R(0.22f, 0.4f), R(0.4f, 0.9f));
                    Vector3 at = around + across * R(-1.6f, 1.6f) + along * R(-0.9f, 0.9f) + Vector3.up * (size.y * 0.4f);
                    batch.Add(PrimitiveType.Cube, Pick(), at, Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)), size);
                }
            }
            for (int b = 0; b < piers; b++)
            {
                Vector3 middle = start + along * (b * spacing);
                if (rng.NextDouble() < 0.25)
                {
                    Rubble(middle, rng.Next(2, 5));
                    continue;
                }
                float height = R(0.8f, 2.6f);
                var pier = new GameObject("Pier").transform;
                pier.SetParent(wall, false);
                pier.SetPositionAndRotation(middle + Vector3.up * (height * 0.5f), facing);
                pier.gameObject.AddComponent<BoxCollider>().size = new Vector3(thickness, height, pierLength);
                batch.Add(PrimitiveType.Cube, stone, middle + Vector3.up * 0.12f, facing, new Vector3(thickness + 0.25f, 0.24f, pierLength + 0.1f));
                int rows = Mathf.Max(1, Mathf.RoundToInt((height - 0.24f) / course));
                for (int row = 0; row < rows; row++)
                {
                    bool top = row == rows - 1;
                    // Odd courses start with a short stone so the joints stagger.
                    float cursor = -pierLength * 0.5f, end = pierLength * 0.5f;
                    float first = row % 2 == 1 ? R(0.3f, 0.5f) : R(0.6f, 0.9f);
                    for (float length = first; cursor < end - 0.15f; length = R(0.55f, 0.95f))
                    {
                        length = Mathf.Min(length, end - cursor);
                        float stoneHeight = (course - 0.03f) * (top ? R(0.45f, 1f) : 1f);
                        if (!(top && rng.NextDouble() < 0.4))
                        {
                            Vector3 at = middle + along * (cursor + length * 0.5f) + across * R(-0.04f, 0.04f) +
                                Vector3.up * (0.24f + row * course + stoneHeight * 0.5f);
                            batch.Add(PrimitiveType.Cube, Pick(), at, facing * Quaternion.Euler(R(-2f, 2f), R(-3f, 3f), R(-2f, 2f)),
                                new Vector3(thickness * R(0.92f, 1f), stoneHeight, length - 0.04f));
                        }
                        cursor += length;
                    }
                }
                if (rng.NextDouble() < 0.5) Rubble(middle + across * (Coin() ? 1.1f : -1.1f), rng.Next(1, 3));
            }
            Rubble(start + along * (piers * spacing + 0.6f), rng.Next(2, 4));
            batch.Build(wall, "Stones", shadows: true);
            Claim(start + along * (piers * spacing * 0.5f), piers * 1.1f + 1f);
        }

        private GameObject Brazier(Transform t, Vector3 p)
        {
            GameObject bowl = Cyl(t, p + Vector3.up * 0.5f, 0.45f, 1f, kit.Mat("Stone"));
            Flame(t, p + Vector3.up * 1.1f, 0.3f, kit.Mat("Ember"));
            Flame(t, p + new Vector3(0.12f, 1.3f, 0.05f), 0.16f, kit.Mat("Lantern"));
            Glow(t, p + Vector3.up * 1.6f, FireLight, 9f, 5f, flicker: true);
            return bowl;
        }

        // ------------------------------------------------------------------ building blocks

        private void Begin(int area, int seed)
        {
            currentArea = area;
            rng = new System.Random(seed);
            claimed.Clear();
            Claim(WaystoneSpot(area), 2.5f);
        }

        /// <summary>Each area's waystone: just south of its spawn point.</summary>
        public static Vector3 WaystoneSpot(int area)
        {
            return Center(area) + WaystoneLocal(area);
        }

        // Haven's stands inside the paved square rather than across its kerb.
        public static Vector3 WaystoneLocal(int area)
        {
            if (area == Desert) return new Vector3(-76f, 0f, -76f);
            if (area == SaltBasin) return new Vector3(-9f, 0f, -108f);
            if (area == Orchard) return new Vector3(0, 0, -108);
            if (area == Reservoir) return new Vector3(-109, 0, -100);
            if (area == BuriedWard || area == FirstRoot) return new Vector3(0, 0, -108);
            if (area == Channels) return new Vector3(-106f, 0f, -93f);
            if (area == DrownedWorks) return new Vector3(0f, 0f, -112f);
            return new Vector3(0f, 0f, area == Haven ? -8f : -11f);
        }

        private void BuildStashChest(Transform parent, Vector3 p, Vector3 faceTowards)
        {
            var chest = new GameObject("Stash");
            chest.transform.SetParent(parent, false);
            chest.transform.position = p;
            Vector3 d = faceTowards - p;
            chest.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z));

            Transform ct = chest.transform;
            LocalBox(ct, new Vector3(0f, 0.42f, 0f), new Vector3(1.6f, 0.84f, 1f), kit.Mat("Wood"));
            LocalBox(ct, new Vector3(0f, 0.95f, 0f), new Vector3(1.66f, 0.26f, 1.06f), kit.Mat("Wood"), solid: false);
            foreach (float x in new[] { -0.55f, 0.55f })
                LocalBox(ct, new Vector3(x, 0.55f, 0f), new Vector3(0.12f, 1.02f, 1.1f), kit.Mat("Iron"), solid: false);
            LocalBox(ct, new Vector3(0f, 0.78f, 0.54f), new Vector3(0.22f, 0.26f, 0.06f), kit.Mat("Iron"), solid: false);

            Claim(p, 1.8f);
            Npc.CreateFixed(chest, NpcRole.Stash, "Stash", 1.7f).UseModelHitBox();
        }

        // ------------------------------------------------------------------ light sources

        private static readonly Color LanternLight = new Color(1f, 0.80f, 0.50f);
        private static readonly Color CandleLight = new Color(1f, 0.68f, 0.35f);
        private static readonly Color FireLight = new Color(1f, 0.52f, 0.18f);
        private static readonly Color LavaLight = new Color(1f, 0.32f, 0.08f);
        private static readonly Color SpiritLight = new Color(0.45f, 1f, 0.55f);
        private static readonly Color IceLight = new Color(0.45f, 0.85f, 1f);
        private static readonly Color WaystoneLight = new Color(0.40f, 0.60f, 1f);
        private static readonly Color ShroomLight = new Color(0.30f, 0.95f, 0.80f);

        /// <summary>
        /// A point light for the ToonLit shader (see <see cref="ManualPointLightManager"/>, which
        /// sends it the lights nearest the view); flames flicker.
        /// </summary>
        private Light Glow(Transform parent, Vector3 p, Color color, float range, float intensity, bool flicker = false)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            if (flicker)
            {
                TorchFlicker f = go.AddComponent<TorchFlicker>();
                f.torchLight = light;
                f.baseIntensity = intensity;
                f.flickerAmount = intensity * 0.15f;
                f.flickerSpeed = 6f;
            }
            return light;
        }

        // Greenwood's own light: clusters of glowing mushrooms among its trees (the area is the
        // scene's original, so spots are checked against its colliders rather than claims).
        private void BuildGlowshrooms()
        {
            Begin(Greenwood, 505);
            Vector3 c = Centers[Greenwood];
            Transform t = Group("Glowshrooms");
            AreaShape shape = Shape(Greenwood);
            for (int k = 0, attempts = 0; k < 18 && attempts < 400; attempts++)
            {
                Vector3 p = c + Flat(R(-AreaShape.MaxRadius, AreaShape.MaxRadius), R(-AreaShape.MaxRadius, AreaShape.MaxRadius));
                if (Vector3.Distance(p, c) < 8f || !shape.Contains(p, 6f) || !Free(p, 6f))
                    continue;
                if (HitsScenery(p + Vector3.up * 1.2f, 1f))
                    continue;
                Glowshrooms(t, p);
                Claim(p, 6f);
                k++;
            }
        }

        // Static scenery only: characters stand at random spots, which would make the layout
        // differ between the player and spectators.
        private static bool HitsScenery(Vector3 p, float radius)
        {
            foreach (Collider c in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c is CharacterController || c.attachedRigidbody != null || c.GetComponentInParent<EnemyHealth>() != null)
                    continue;
                // The floor and the edge's invisible wall aren't scenery in the way.
                if (c.gameObject.name.StartsWith("Ground") || c.gameObject.name == "Wall")
                    continue;
                return true;
            }
            return false;
        }

        private void Glowshrooms(Transform t, Vector3 p)
        {
            if (DeferContent(p, () => Glowshrooms(t, p))) return;
            var group = new GameObject("Glowshroom").transform;
            group.SetParent(t, false);
            group.position = p;
            int n = rng.Next(3, 6);
            for (int k = 0; k < n; k++)
            {
                Vector3 o = new Vector3(R(-0.5f, 0.5f), 0f, R(-0.5f, 0.5f));
                float h = R(0.15f, 0.4f);
                GameObject stem = RuntimePrimitives.Create(PrimitiveType.Cylinder, group, new Color(0.85f, 0.88f, 0.80f));
                stem.transform.localPosition = o + Vector3.up * h * 0.5f;
                stem.transform.localScale = new Vector3(0.06f, h * 0.5f, 0.06f);
                GameObject cap = RuntimePrimitives.Create(PrimitiveType.Sphere, group, ShroomLight);
                float w = R(0.18f, 0.32f);
                cap.transform.localPosition = o + Vector3.up * h;
                cap.transform.localScale = new Vector3(w, w * 0.45f, w);
            }
            Glow(group, p + Vector3.up * 0.7f, ShroomLight, 5.5f, 2.8f);
        }

        private Transform Group(string name)
        {
            Transform existing = root.Find(name);
            if (existing != null) return existing;
            var g = new GameObject(name).transform;
            g.SetParent(root, false);
            return g;
        }

        private Transform Marker(string name, Vector3 position, float yaw)
        {
            var m = new GameObject(name).transform;
            m.SetParent(root, false);
            m.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return m;
        }

        // Places up to `count` things at random spots in a ring round the current area's centre.
        // A ring reaching 40 m or more means "out to the edge": sample the authored walkable space,
        // with proportionally more things.
        private void Scatter(Transform t, int count, float minRadius, float maxRadius, System.Func<Vector3, GameObject> place, float radius)
        {
            Vector3 c = Centers[CurrentArea()];
            bool wide = maxRadius >= 40f;
            AreaShape shape = Shape(CurrentArea());
            if (wide)
            {
                count = Mathf.RoundToInt(count * ScatterScale);
                maxRadius = AreaShape.MaxRadius;
            }
            for (int k = 0, attempts = 0; k < count && attempts < count * 12; attempts++)
            {
                Vector3 p = c + Flat(R(-maxRadius, maxRadius), R(-maxRadius, maxRadius));
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
                if (d < minRadius || d > maxRadius || !Free(p, radius))
                    continue;
                if (!shape.Contains(p, shape.IsCave ? radius + 8f : radius + 2f))
                    continue;
                Vector3 point = p;
                ScheduleContent(point, () => place(point));
                Claim(p, radius);
                k++;
            }
        }

        private int currentArea;

        private int CurrentArea()
        {
            return currentArea;
        }

        private bool Free(Vector3 p, float radius)
        {
            if (Shape(CurrentArea()).IsBridge(p, radius + 3f)) return false;
            foreach (Vector3 c in claimed)
            {
                float dx = c.x - p.x;
                float dz = c.y - p.z;
                if (dx * dx + dz * dz < (c.z + radius) * (c.z + radius))
                    return false;
            }
            return true;
        }

        private void Claim(Vector3 p, float radius)
        {
            claimed.Add(new Vector3(p.x, p.z, radius));
        }

        private float R(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private bool Coin()
        {
            return rng.NextDouble() < 0.5;
        }

        private static Vector3 Flat(float x, float z)
        {
            return new Vector3(x, 0f, z);
        }

        private GameObject Prefab(GameObject prefab, Transform t, Vector3 p, float yaw, Vector3 scale)
        {
            GameObject go = Instantiate(prefab, p, Quaternion.Euler(0f, yaw, 0f), t);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, scale);
            return go;
        }

        // A closed, chipped stone outline instead of a pristine rectangular primitive.
        // Duplicate triangle vertices preserve the facets and keep the top shading flat.
        private GameObject WeatheredSlab(Transform parent, string name, Vector3 p, Vector2 radii,
            float height, Material material, float yaw, bool solid = true, float topScale = 0.96f)
        {
            const int sides = 20;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 Edge(int k, bool top)
            {
                float angle = k * Mathf.PI * 2f / sides;
                float wear = 1f + 0.055f * Mathf.Sin(k * 2.3f) + 0.035f * Mathf.Cos(k * 4.1f);
                float scale = wear * (top ? topScale : 1f);
                return new Vector3(Mathf.Cos(angle) * radii.x * scale, top ? height : 0f,
                    Mathf.Sin(angle) * radii.y * scale);
            }
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(a.x, a.z)); uv.Add(new Vector2(b.x, b.z)); uv.Add(new Vector2(c.x, c.z));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            for (int k = 0; k < sides; k++)
            {
                Vector3 a = Edge(k, false), b = Edge((k + 1) % sides, false);
                Vector3 at = Edge(k, true), bt = Edge((k + 1) % sides, true);
                Face(Vector3.up * height, bt, at);
                Face(Vector3.zero, a, b);
                Face(a, at, bt); Face(a, bt, b);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Transform slab = Holder(parent, name, p, Quaternion.Euler(0f, yaw, 0f));
            slab.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            slab.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) slab.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            return slab.gameObject;
        }

        private GameObject Box(Transform t, Vector3 p, Vector3 size, Material mat, bool solid = true, Vector3 euler = default)
        {
            return Primitive(PrimitiveType.Cube, t, p, size, mat, solid, euler, local: false);
        }

        private GameObject LocalBox(Transform t, Vector3 p, Vector3 size, Material mat, bool solid = true, Vector3 euler = default)
        {
            return Primitive(PrimitiveType.Cube, t, p, size, mat, solid, euler, local: true);
        }

        // Unity's cylinder is 2 units tall at scale 1.
        private GameObject Cyl(Transform t, Vector3 p, float radius, float height, Material mat, bool solid = true, Vector3 euler = default)
        {
            return Primitive(PrimitiveType.Cylinder, t, p, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, solid, euler, local: false);
        }

        private GameObject LocalCyl(Transform t, Vector3 p, float radius, float height, Material mat, bool solid = true)
        {
            return Primitive(PrimitiveType.Cylinder, t, p, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, solid, default, local: true);
        }

        private GameObject Ball(Transform t, Vector3 p, float radius, Material mat, float flatten = 1f, bool solid = true)
        {
            return Primitive(PrimitiveType.Sphere, t, p, new Vector3(radius * 2f, radius * 2f * flatten, radius * 2f), mat, solid, default, local: false);
        }

        private GameObject LocalBall(Transform t, Vector3 p, float radius, Material mat)
        {
            return Primitive(PrimitiveType.Sphere, t, p, Vector3.one * radius * 2f, mat, false, default, local: true);
        }

        // Many small primitives merged into one mesh per material: a draw call or two instead of hundreds.
        private sealed class PrimitiveBatch
        {
            private static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
            private readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();

            public void Add(PrimitiveType type, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                if (!meshes.TryGetValue(type, out Mesh mesh) || mesh == null)
                {
                    GameObject probe = GameObject.CreatePrimitive(type);
                    meshes[type] = mesh = probe.GetComponent<MeshFilter>().sharedMesh;
                    DestroyImmediate(probe);
                }
                if (!parts.TryGetValue(material, out var list)) parts[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
            }

            public GameObject Build(Transform parent, string name, bool shadows)
            {
                if (parts.Count == 0) return null;
                var submeshes = new List<CombineInstance>();
                var materials = new List<Material>();
                foreach (var part in parts)
                {
                    var merged = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    merged.CombineMeshes(part.Value.ToArray(), true, true);
                    submeshes.Add(new CombineInstance { mesh = merged, transform = Matrix4x4.identity });
                    materials.Add(part.Key);
                }
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(submeshes.ToArray(), false, true);
                foreach (CombineInstance submesh in submeshes) DestroyImmediate(submesh.mesh);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials.ToArray();
                if (!shadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go;
            }
        }

        private static GameObject Primitive(PrimitiveType type, Transform t, Vector3 p, Vector3 scale, Material mat, bool solid, Vector3 euler, bool local)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (!solid)
                DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(t, false);
            if (local)
            {
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler(euler);
            }
            else
            {
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(euler));
            }
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            // Small decor doesn't need to cast shadows; it's a lot of draw calls on a phone.
            if (!solid)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
