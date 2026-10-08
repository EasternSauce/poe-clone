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
    /// Route: Haven - Greenwood - The Lost Hollows - Haunted Graveyard - Ashen Ruins - Frozen Hollow.
    /// Layout geometry and permanent anchors live in AreaLayouts; the game starts in Haven.
    /// </summary>
    public partial class WorldBuilder : MonoBehaviour
    {
        public const int Greenwood = 0;
        public const int Haven = 1;
        public const int Graveyard = 2;
        public const int Ruins = 3;
        public const int Frozen = 4;
        public const int Cave = 6; // Preserve the final arena's saved area ID (5).
        public static readonly int[] WorldAreas = { Greenwood, Haven, Graveyard, Ruins, Frozen, Cave };

        public static WorldBuilder Instance { get; private set; }

        public static readonly string[] AreaNames = { "Greenwood", "Haven", "Haunted Graveyard", "Ashen Ruins", "Frozen Hollow", "The Shed Sanctuary", "The Lost Hollows" };
        public static readonly int[] MonsterLevels = { 1, 0, 4, 7, 10, 12, 3 };

        private static readonly Vector3[] Centers =
        {
            Vector3.zero,
            new Vector3(-420f, 0f, 0f),
            new Vector3(420f, 0f, 0f),
            new Vector3(840f, 0f, 0f),
            new Vector3(1260f, 0f, 0f),
            ActArenaCenter,
            new Vector3(420f, 0f, -420f)
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
            new Color(0.31f, 0.35f, 0.37f)
        };

        // Per area, one spawn weight per EnemyKinds entry, so each area has its own cast:
        // Zombie, Raider, Brute, Archer, Fire / Frost / Storm Caster, Skeleton, Wraith, Ember Knight,
        // (three bosses, always 0), Forest Shaman, Necromancer, Skeleton Archer, Frost Giant;
        // creatures: Giant Spider, Dire Wolf, Bog Slime, Slimeling (0: only split off), Grave Bat,
        // Corpse Ooze, Oozeling (0), Crypt Spider, Magma Beetle, Hellhound, Frost Wolf, Ice Crawler.
        // Wolves, hounds and bats come in packs, so their weights count for more than they look.
        private static readonly float[][] KindWeights =
        {
            // Greenwood: the living - zombies, raiders, archers, a few brutes and shamans; spiders,
            // wolf packs and bog slimes in the woods.
            new float[] { 22, 18, 8, 18, 0, 0, 3, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 14, 9, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            null,
            // Graveyard: the dead - skeletons (some with bows), wraiths, necromancers; bat swarms,
            // oozes and crypt spiders.
            new float[] { 12, 0, 4, 0, 0, 6, 0, 22, 14, 0, 0, 0, 0, 0, 10, 14, 0, 0, 0, 0, 0, 9, 10, 0, 14, 0, 0, 0, 0 },
            // Ruins: fire - ember knights and fire casters, with brutes and raiders; magma beetles
            // and hellhound packs.
            new float[] { 0, 10, 12, 4, 20, 0, 8, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 18, 10, 0, 0 },
            // Frozen Hollow: cold - frost casters, wraiths, frost giants; frost wolf packs, ice crawlers.
            new float[] { 0, 0, 4, 0, 0, 20, 10, 8, 16, 0, 0, 0, 0, 0, 0, 8, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 16 }
        };

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
            if (MinimalCombatMode.Enabled) return;
            var session = PoeClone.Network.GameSessionController.Instance;
            if (session != null && session.Role == PoeClone.Network.SessionRole.Player && !session.PlayGranted) return;
            if (Instance != null) return;
            var go = new GameObject("World");
            go.AddComponent<WorldBuilder>().Build();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Build()
        {
            Instance = this;
            root = transform;
            kit = AreaKit.Load();

            AreaManager manager = FindAnyObjectByType<AreaManager>();
            GameObject ground = GameObject.Find("Ground");
            PlayerController player = FindAnyObjectByType<PlayerController>();

            if (kit == null || manager == null || ground == null || player == null)
            {
                Debug.LogError("WorldBuilder: missing area kit, area manager, ground or player; the world cannot be built.");
                return;
            }

            gateTemplate = CreateGateTemplate();

            spawnPoints = new Transform[Centers.Length];
            foreach (int a in WorldAreas)
                spawnPoints[a] = Marker("Spawn_" + AreaNames[a], Centers[a] + new Vector3(0f, 1.1f, -6f), 0f);

            InitShapes();
            foreach (int a in WorldAreas)
                if (a != Greenwood) BuildGround(ground, a);
            // The scene's own forest gets the same outline; its square walls go.
            ShapeGround(ground, Greenwood);
            RelocateOriginalScenery();
            GameObject sceneBounds = GameObject.Find("WorldBounds");
            if (sceneBounds != null)
                sceneBounds.SetActive(false);

            BuildHaven();
            BuildGraveyard();
            BuildRuins();
            BuildFrozen();
            BuildCave();
            BuildGlowshrooms();
            BuildGreenwoodOutskirts();
            BuildWaystones();
            BuildOutposts();

            // Gates: Haven - Greenwood - Lost Hollows - Graveyard - Ruins - Frozen Hollow. The way home is the
            // town portal or a waystone.
            Connect(Haven, new Vector3(40f, 0f, 0f), Greenwood, new Vector3(-40f, 0f, 2f));
            Connect(Greenwood, new Vector3(40f, 0f, -2f), Cave, new Vector3(-40f, 0f, 0f));
            Connect(Cave, new Vector3(40f, 0f, 0f), Graveyard, new Vector3(-40f, 0f, 0f));
            Connect(Graveyard, new Vector3(40f, 0f, 0f), Ruins, new Vector3(-40f, 0f, 0f));
            Connect(Ruins, new Vector3(0f, 0f, 40f), Frozen, new Vector3(-40f, 0f, 0f));
            BuildBorders();
            BuildQuestSites();

            SetUpSpawners();
            BuildTownsfolk();
            PlaceBosses();

            var definitions = new AreaDefinition[Centers.Length];
            foreach (int a in WorldAreas)
            {
                definitions[a] = new AreaDefinition
                {
                    areaName = AreaNames[a],
                    groundColor = AreaColors[a],
                    spawnPoint = spawnPoints[a],
                    monsterLevel = MonsterLevels[a],
                    isTown = a == Haven,
                    tintsSharedGround = a == Greenwood
                };
            }

            definitions[ActArena] = BuildActArena();
            ConfigureCaveLighting(player.transform);

            // Colliders made this frame aren't in the physics world until it syncs; the starter
            // loot below finds the ground by raycast.
            Physics.SyncTransforms();
            ManualPointLightManager.Refresh();

            // Start in town.
            MovePlayer(player, spawnPoints[Haven]);
            manager.AreaChanged += OnAreaChanged;
            manager.SetAreas(definitions, Haven);
            StarterLoot.PlaceAt(starterSpots, spawnPoints[Haven].position + Vector3.down * 1.1f);
        }

        private static void MovePlayer(PlayerController player, Transform to)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(to.position, to.rotation);
            if (cc != null) cc.enabled = true;

            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
                stats.SetSpawnPoint(to.position, to.rotation);

            var cam = Camera.main != null ? Camera.main.GetComponent<CameraSystem.CameraFollow>() : null;
            if (cam != null)
                cam.SnapToTarget();
        }

        // ------------------------------------------------------------------ areas

        private void BuildGround(GameObject template, int area)
        {
            Vector3 c = Centers[area];
            GameObject ground = Instantiate(template, root);
            ground.name = "Ground_" + AreaNames[area];
            ground.transform.position = new Vector3(c.x, template.transform.position.y, c.z);

            // Its own floor: a generated texture on a copy of the ground material.
            Renderer r = ground.GetComponent<Renderer>();
            var material = new Material(r.sharedMaterial);
            material.SetTexture("_BaseMap", GroundTexture(area));
            material.SetColor("_BaseColor", Color.white);
            r.sharedMaterial = material;

            // Cut to the area's outline, walled round its edge.
            ShapeGround(ground, area);
        }

        private static Texture2D GroundTexture(int area)
        {
            switch (area)
            {
                case Haven:
                    // Muted, olive town grass with blades and worn dirt flecks.
                    return GroundTextures.Make(11, new Color(0.24f, 0.33f, 0.17f), new Color(0.42f, 0.48f, 0.25f),
                        new Color(0.47f, 0.40f, 0.28f), 0.025f, 6f, grain: 0.14f, blades: 26000);
                case Graveyard:
                    return GroundTextures.Make(22, new Color(0.17f, 0.20f, 0.17f), new Color(0.31f, 0.33f, 0.27f),
                        new Color(0.40f, 0.40f, 0.36f), 0.015f, 7f);
                case Frozen:
                    return GroundTextures.Make(44, new Color(0.78f, 0.84f, 0.90f), new Color(0.93f, 0.96f, 0.98f),
                        new Color(0.55f, 0.72f, 0.88f), 0.012f, 7f);
                case Cave:
                    return GroundTextures.Make(66, new Color(0.18f, 0.20f, 0.21f), new Color(0.32f, 0.33f, 0.30f),
                        new Color(0.29f, 0.35f, 0.32f), 0.012f, 7f);
                default:
                    return GroundTextures.Make(33, new Color(0.22f, 0.19f, 0.18f), new Color(0.44f, 0.37f, 0.31f),
                        new Color(0.85f, 0.35f, 0.12f), 0.01f, 8f);
            }
        }

        // Haven: cottages and working yards along winding lanes around a market square.
        private void BuildHaven()
        {
            Begin(Haven, 101);
            Vector3 c = Centers[Haven];
            Transform t = Group("Haven");

            // Plaza and the well at its heart.
            Cyl(t, c + new Vector3(0f, 0.02f, 0f), 11f, 0.04f, kit.Mat("Stone"), solid: false);
            Transform well = Holder(t, "HavenWell", c, Quaternion.identity);
            LocalCyl(well, new Vector3(0f, 0.45f, 0f), 1.3f, 0.9f, kit.Mat("Stone"));
            LocalCyl(well, new Vector3(0f, 0.92f, 0f), 1.05f, 0.04f, kit.Mat("Water"), solid: false);
            LocalBox(well, new Vector3(-1.2f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.18f), kit.Mat("Wood"));
            LocalBox(well, new Vector3(1.2f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.18f), kit.Mat("Wood"));
            LocalBox(well, new Vector3(0f, 2.9f, 0f), new Vector3(3.2f, 0.25f, 2.2f), kit.Mat("Roof"));
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

            // Crates and barrels by the stalls and houses.
            for (int k = 0; k < 16; k++)
            {
                Vector3 p = c + Flat(R(-24f, 24f), R(-24f, 24f));
                if (Vector3.Distance(p, c) < 12.5f || !Free(p, 1f))
                    continue;
                if (k % 2 == 0)
                    Box(t, p + Vector3.up * 0.4f, Vector3.one * 0.8f, kit.Mat("Wood"), euler: new Vector3(0f, R(0f, 90f), 0f));
                else
                    Cyl(t, p + Vector3.up * 0.5f, 0.38f, 1f, kit.Mat("Wood"));
                Claim(p, 1f);
            }

            // Trees and bushes round the outskirts.
            Scatter(t, 16, 34f, 45f, p => Prefab(Coin() ? kit.oak : kit.pine, t, p, R(0f, 360f), Vector3.one * R(0.9f, 1.25f)), 2.2f);
            Scatter(t, 18, 30f, 46f, p => Prefab(kit.bushSmall, t, p, R(0f, 360f), Vector3.one * R(0.3f, 0.5f)), 0.6f);

            Spots["Elder"] = c + new Vector3(-3.5f, 0f, 3.5f);
            Spots["Smith"] = c + new Vector3(5.5f, 0f, -7f);
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

        // The Haunted Graveyard: plots of tombstones behind iron fences, dead trees, a crypt.
        private void BuildGraveyard()
        {
            Begin(Graveyard, 202);
            Vector3 c = Centers[Graveyard];
            Transform t = Group("Graveyard");

            // Worn winding track, still inside the broad central corridor.
            WindingPath(t, "GraveyardTrack", Graveyard, 3.6f, kit.Mat("Ash"),
                Flat(-121, 0), Flat(-95, -5), Flat(-65, 5), Flat(-33, -5),
                Flat(0, 0), Flat(32, 6), Flat(63, -5), Flat(94, 4), Flat(121, 0));
            Claim(c + new Vector3(-30f, 0f, 0f), 3f);
            Claim(c + new Vector3(0f, 0f, 0f), 3f);
            Claim(c + new Vector3(30f, 0f, 0f), 3f);

            // The crypt, north-west of the path and well away from the central waystone.
            Vector3 crypt = c + AreaLayouts.BossLocal(Graveyard);
            Box(t, crypt + new Vector3(0f, 2f, 0f), new Vector3(7f, 4f, 7f), kit.Mat("TombstoneDark"));
            Box(t, crypt + new Vector3(0f, 4.4f, 0f), new Vector3(7.8f, 0.8f, 7.8f), kit.Mat("Tombstone"));
            Box(t, crypt + new Vector3(0f, 1.3f, -3.55f), new Vector3(2f, 2.6f, 0.2f), kit.Mat("Charred"), solid: false);
            for (int k = -1; k <= 1; k += 2)
            {
                Cyl(t, crypt + new Vector3(k * 2.4f, 1.9f, -4.2f), 0.35f, 3.8f, kit.Mat("Tombstone"));
                // A ghostly green flame on top of each column.
                GameObject flame = RuntimePrimitives.Create(PrimitiveType.Sphere, t, SpiritLight);
                flame.transform.position = crypt + new Vector3(k * 2.4f, 4.05f, -4.2f);
                flame.transform.localScale = new Vector3(0.35f, 0.55f, 0.35f);
                Glow(t, crypt + new Vector3(k * 2.4f, 4.3f, -4.2f), SpiritLight, 10f, 5f, flicker: true);
            }
            Claim(crypt, 7f);
            Spots["Crypt"] = crypt + new Vector3(0f, 0f, -6f);

            // Plots of graves, each fenced.
            Vector2[] plots =
            {
                new Vector2(-26f, 15f), new Vector2(-26f, -15f), new Vector2(26f, 15f),
                new Vector2(26f, -15f), new Vector2(-6f, -18f), new Vector2(14f, -30f)
            };
            foreach (Vector2 plot in plots)
                GravePlot(t, Shape(Graveyard).NearestOpen(c + Flat(plot.x * 2.4f, plot.y * 2.4f), 12f));

            // Dead trees, candles, bones and a few pumpkins.
            Scatter(t, 18, 6f, 46f, p => DeadTree(t, p, kit.Mat("DeadWood")), 1.6f);
            Scatter(t, 22, 6f, 44f, p => Candles(t, p), 0.5f);
            Scatter(t, 12, 6f, 44f, p => Bones(t, p), 0.6f);
            Scatter(t, 6, 6f, 26f, p => Pumpkin(t, p), 0.6f);
            Scatter(t, 12, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.4f)), 1.5f);
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
            WeatheredSlab(t, "TempleDais", temple, new Vector2(10.3f, 9.8f), 0.14f, kit.Mat("Sandstone"), 18f, solid: false);
            for (int k = 0; k < 10; k++)
            {
                float rad = k * 36f * Mathf.Deg2Rad;
                Vector3 p = temple + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 8.5f;
                if (k == 3 || k == 7)
                    FallenColumn(t, p, k * 36f + 70f);
                else
                    Prefab(kit.pillar, t, p + Vector3.up * 0.14f, R(0f, 360f), new Vector3(1f, R(0.45f, 1.05f), 1f));
            }
            WeatheredSlab(t, "AltarBase", temple + new Vector3(0f, 0.14f, 3f), new Vector2(1.3f, 0.85f), 0.85f, kit.Mat("Sandstone"), 18f);
            WeatheredSlab(t, "AltarCap", temple + new Vector3(0f, 0.99f, 3f), new Vector2(1.5f, 1f), 0.15f, kit.Mat("Sandstone"), 18f);
            Ball(t, temple + new Vector3(-0.7f, 1.34f, 3f), 0.35f, kit.Mat("Ember"));
            Ball(t, temple + new Vector3(0.7f, 1.34f, 3f), 0.35f, kit.Mat("Ember"));
            Glow(t, temple + new Vector3(0f, 1.9f, 3f), FireLight, 10f, 5f, flicker: true);
            Claim(temple, 11f);
            Spots["Altar"] = temple;

            // Broken walls: rows of blocks with gaps, a few fallen.
            for (int w = 0; w < 9; w++)
            {
                Vector3 start = c + Flat(R(-140f, 140f), R(-110f, 110f));
                if (!Free(start, 5f) || !Shape(Ruins).Contains(start, 16f))
                    continue;
                float yaw = R(0f, 180f);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                int blocks = rng.Next(3, 7);
                for (int b = 0; b < blocks; b++)
                {
                    if (rng.NextDouble() < 0.25)
                        continue;
                    Vector3 p = start + dir * (b * 2.1f);
                    float height = R(0.8f, 2.6f);
                    Box(t, p + Vector3.up * height * 0.5f, new Vector3(0.9f, height, 2f), kit.Mat("Sandstone"), euler: new Vector3(0f, yaw, 0f));
                }
                Box(t, start + dir * (blocks * 2.1f + 1.5f) + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0.9f), kit.Mat("Sandstone"),
                    euler: new Vector3(R(-10f, 10f), R(0f, 360f), R(-15f, 15f)));
                Claim(start + dir * (blocks * 1.05f), blocks * 1.1f + 1f);
            }

            // Cracks of lava in the ash.
            for (int k = 0; k < 16; k++)
            {
                Vector3 p = c + Flat(R(-140f, 140f), R(-110f, 110f));
                if (Vector3.Distance(p, temple) < 11f || !Shape(Ruins).Contains(p, 5f))
                    continue;
                Box(t, p + Vector3.up * 0.03f, new Vector3(R(0.25f, 0.5f), 0.05f, R(3f, 8f)), kit.Mat("Lava"), solid: false,
                    euler: new Vector3(0f, R(0f, 180f), 0f));
                if (k % 2 == 0)
                    Glow(t, p + Vector3.up * 0.5f, LavaLight, 5.5f, 2.5f, flicker: true);
            }

            Scatter(t, 8, 10f, 42f, p => Brazier(t, p), 1f);
            Scatter(t, 14, 8f, 46f, p => DeadTree(t, p, kit.Mat("Charred")), 1.6f);
            Scatter(t, 10, 8f, 46f, p => Ball(t, p, R(0.6f, 1.2f), kit.Mat("Ash"), flatten: 0.35f, solid: false), 1f);
            Scatter(t, 14, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.5f)), 1.5f);
        }

        // The Frozen Hollow: snow, frosted pines, ice crystals and frozen ponds round the queen's
        // throne, a ring of ice spikes north-east of the centre, clear of the waystone-to-door route.
        private void BuildFrozen()
        {
            Begin(Frozen, 404);
            Vector3 c = Centers[Frozen];
            Transform t = Group("Frozen");

            Vector3 throne = c + AreaLayouts.BossLocal(Frozen);
            Cyl(t, throne + Vector3.up * 0.04f, 9f, 0.08f, kit.Mat("Ice"), solid: false);
            for (int k = 0; k < 12; k++)
            {
                float rad = k * 30f * Mathf.Deg2Rad;
                Vector3 p = throne + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 9.5f;
                if (k == 9)
                    continue; // the way in, facing south
                IceSpike(t, p, R(2.2f, 3.6f), new Vector3(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
            }
            Box(t, throne + new Vector3(0f, 0.9f, 3.2f), new Vector3(2.6f, 1.8f, 1f), kit.Mat("Ice"));
            Box(t, throne + new Vector3(0f, 0.35f, 2.4f), new Vector3(2.2f, 0.7f, 1.2f), kit.Mat("Ice"));
            Claim(throne, 11f);
            Spots["Throne"] = throne;

            // Frozen ponds.
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = c + Flat(R(-130f, 130f), R(-140f, 140f));
                float radius = R(3f, 6f);
                if (!Free(p, radius) || !Shape(Frozen).Contains(p, radius + 2f))
                    continue;
                Cyl(t, p + Vector3.up * 0.03f, radius, 0.06f, kit.Mat("Ice"), solid: false);
                Claim(p, radius);
            }

            Scatter(t, 14, 8f, 46f, p => CrystalCluster(t, p), 1.6f);
            Scatter(t, 20, 14f, 46f, p => FrostedPine(t, p), 2f);
            Scatter(t, 14, 6f, 46f, p => Ball(t, p, R(0.7f, 1.5f), kit.Mat("Snow"), flatten: 0.4f, solid: false), 1.2f);
            Scatter(t, 12, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.6f)), 1.5f);
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
            spawnersByArea = new EnemySpawner[Centers.Length];

            EnemySpawner original = FindAnyObjectByType<EnemySpawner>();
            if (original == null || original.EnemyPrefab == null)
                return;

            GameObject prefab = original.EnemyPrefab;
            original.Configure(prefab, Centers[Greenwood], AreaShape.MaxRadius, 48, MonsterLevels[Greenwood], KindWeights[Greenwood]);
            original.SetSafeSpots(SafeSpots(Greenwood));
            AreaShape greenwood = Shape(Greenwood);
            original.SetBounds(p => greenwood.Contains(p, 3f));
            spawnersByArea[Greenwood] = original;

            foreach (int area in new[] { Graveyard, Ruins, Frozen, Cave })
            {
                var go = new GameObject("Spawner_" + AreaNames[area]);
                go.transform.SetParent(root, false);
                go.transform.position = Centers[area];
                var spawner = go.AddComponent<EnemySpawner>();
                spawner.Configure(prefab, Centers[area], AreaShape.MaxRadius, 48, MonsterLevels[area], KindWeights[area == Cave ? Graveyard : area]);
                spawner.SetSafeSpots(SafeSpots(area));
                AreaShape shape = Shape(area);
                spawner.SetBounds(p => shape.Contains(p, 3f));
                spawnersByArea[area] = spawner;
            }
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
            if (Spots.TryGetValue("Crypt", out Vector3 crypt))
                BossLair.Create(t, Graveyard, BossIndex("Gravelord Mortis"), 5, spawner.EnemyPrefab, crypt, Vector3.back);
            if (Spots.TryGetValue("Altar", out Vector3 altar))
                BossLair.Create(t, Ruins, BossIndex("Ashen Warlord"), 8, spawner.EnemyPrefab, altar, Vector3.back);
            if (Spots.TryGetValue("Throne", out Vector3 throne))
                BossLair.Create(t, Frozen, BossIndex("Rimeheart"), 11, spawner.EnemyPrefab, throne, Vector3.back);
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

            BuildVillageFolk(spawner.EnemyPrefab, t);
            BuildOutpostFolk(spawner.EnemyPrefab);
        }

        private void AddNpc(GameObject prefab, Transform parent, NpcRole role, string name, Vector3 faceTowards, EnemyKind look)
        {
            if (!Spots.TryGetValue(role.ToString(), out Vector3 spot))
                return;
            Vector3 position = spot + Vector3.up * 1.1f * look.Scale;
            Vector3 facing = faceTowards - spot;
            facing.y = 0f;
            Npc.Create(prefab, role, name, look, position, facing, parent);
        }

        // Where players appear in an area (its spawn point and every gate arrival): kept clear of enemies.
        private List<Vector3> SafeSpots(int area)
        {
            var spots = new List<Vector3> { spawnPoints[area].position };
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
            foreach (Transform child in root)
            {
                if (child.name.StartsWith("Arrive_" + AreaNames[area] + "_"))
                    spots.Add(child.position);
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
                pillar.localPosition = new Vector3(side * 1.3f, 0f, 0f);
                LocalBox(pillar, new Vector3(0f, 0.175f, 0f), new Vector3(1.1f, 0.35f, 1.1f), stone, solid: false).name = "Base";
                LocalCyl(pillar, new Vector3(0f, 2f, 0f), 0.7f, 3.3f, stone, solid: false).name = "Column";
                LocalBox(pillar, new Vector3(0f, 3.7f, 0f), new Vector3(1f, 0.3f, 1f), stone, solid: false).name = "Cap";
                var pillarCollider = pillar.gameObject.AddComponent<BoxCollider>();
                pillarCollider.center = new Vector3(0f, 1.925f, 0f);
                pillarCollider.size = new Vector3(1f, 3.85f, 1f);
            }
            LocalBox(gate.transform, new Vector3(0f, 3.85f, 0f), new Vector3(3.6f, 0.4f, 1f), stone).name = "Lintel";

            Shader portalShader = Shader.Find("Universal Render Pipeline/Lit") ?? kit.Mat("Water").shader;
            Material portal = new Material(portalShader);
            portal.SetColor("_BaseColor", new Color(.3f, .4f, .5f));
            portal.SetFloat("_Smoothness", .1f);
            portal.EnableKeyword("_EMISSION");
            portal.SetColor("_EmissionColor", new Color(.15f, .2f, .25f));
            LocalBox(gate.transform, new Vector3(0f, 1.9f, 0f), new Vector3(2.1f, 3.4f, .15f), portal, solid: false).name = "PortalPanel";

            var trigger = gate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0f);
            trigger.size = new Vector3(2f, 2.4f, 1.6f);
            gate.AddComponent<AreaGate>();
            return gate;
        }

        // A gate in each area leading to the other, at the edge of its outline in the given direction;
        // you arrive a few steps in front of the gate back.
        private void Connect(int a, Vector3 offsetA, int b, Vector3 offsetB)
        {
            Vector3 gateA = GatePoint(a, offsetA);
            Vector3 gateB = GatePoint(b, offsetB);
            Transform arriveInA = Marker("Arrive_" + AreaNames[a] + "_from_" + AreaNames[b], InFront(a, gateA), Yaw(gateA, Centers[a]));
            Transform arriveInB = Marker("Arrive_" + AreaNames[b] + "_from_" + AreaNames[a], InFront(b, gateB), Yaw(gateB, Centers[b]));
            ClearSpot(arriveInA.position, 2f);
            ClearSpot(arriveInB.position, 2f);

            Gate(a, gateA, b, arriveInB);
            Gate(b, gateB, a, arriveInA);
        }

        private void Gate(int from, Vector3 position, int to, Transform arrival)
        {
            gatePoints.Add((from, position));
            ClearSpot(position, 4.5f);
            GameObject gate = Instantiate(gateTemplate, root);
            gate.name = "Gate_" + AreaNames[from] + "_to_" + AreaNames[to];
            gate.transform.position = new Vector3(position.x, gateTemplate.transform.position.y, position.z);
            // Each entrance has its own readable angle; rotate the frame and trigger together.
            gate.transform.rotation = Quaternion.Euler(0f, AreaLayouts.GateYaw(from, to), 0f);

            AreaGate g = gate.GetComponent<AreaGate>();
            g.fromAreaIndex = from;
            g.targetAreaIndex = to;
            g.arrival = arrival;

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

        private static Vector3 InFront(int area, Vector3 gate)
        {
            Vector3 inward = (Centers[area] - gate);
            inward.y = 0f;
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
                if (c.gameObject.name.StartsWith("Ground") || c.gameObject.name == "Wall" || c.transform.root.name == "WorldBounds")
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
            Claim(p, 2.6f);
        }

        private void Lamp(Transform t, Vector3 p)
        {
            Box(t, p + Vector3.up * 1.4f, new Vector3(0.14f, 2.8f, 0.14f), kit.Mat("Iron"));
            Box(t, p + new Vector3(0f, 2.75f, 0f), new Vector3(0.5f, 0.08f, 0.5f), kit.Mat("Iron"), solid: false);
            Ball(t, p + new Vector3(0f, 2.55f, 0f), 0.22f, kit.Mat("Lantern"), solid: false);
            Glow(t, p + new Vector3(0f, 2.3f, 0f), LanternLight, 10f, 8f);
            Claim(p, 0.6f);
        }

        private void GravePlot(Transform t, Vector3 center)
        {
            const int cols = 4;
            const int rows = 3;
            const float dx = 2.2f;
            const float dz = 2.6f;
            Vector3 origin = center - new Vector3((cols - 1) * dx * 0.5f, 0f, (rows - 1) * dz * 0.5f);

            for (int r = 0; r < rows; r++)
            {
                for (int k = 0; k < cols; k++)
                {
                    if (rng.NextDouble() < 0.15)
                        continue;
                    Vector3 p = origin + new Vector3(k * dx + R(-0.2f, 0.2f), 0f, r * dz + R(-0.2f, 0.2f));
                    Tombstone(t, p);
                }
            }

            // Iron fence round the plot, with a gap to walk in on the side facing the path.
            float halfW = cols * dx * 0.5f + 0.6f;
            float halfD = rows * dz * 0.5f + 0.6f;
            // The side facing the central path gets a gap in the middle to walk in through.
            float pathSide = center.z > Centers[Graveyard].z ? -halfD : halfD;
            for (float x = -halfW; x <= halfW + 0.01f; x += 1.5f)
            {
                bool gap = Mathf.Abs(x) < 1.6f;
                if (!(gap && Mathf.Approximately(pathSide, -halfD)))
                    FencePost(t, center + new Vector3(x, 0f, -halfD));
                if (!(gap && Mathf.Approximately(pathSide, halfD)))
                    FencePost(t, center + new Vector3(x, 0f, halfD));
            }
            for (float z = -halfD + 1.5f; z < halfD; z += 1.5f)
            {
                FencePost(t, center + new Vector3(-halfW, 0f, z));
                FencePost(t, center + new Vector3(halfW, 0f, z));
            }
            Box(t, center + new Vector3(0f, 1.05f, -pathSide), new Vector3(halfW * 2f, 0.07f, 0.07f), kit.Mat("Iron"), solid: false);
            Box(t, center + new Vector3(-halfW, 1.05f, 0f), new Vector3(0.07f, 0.07f, halfD * 2f), kit.Mat("Iron"), solid: false);
            Box(t, center + new Vector3(halfW, 1.05f, 0f), new Vector3(0.07f, 0.07f, halfD * 2f), kit.Mat("Iron"), solid: false);
            Claim(center, Mathf.Max(halfW, halfD) + 1f);
        }

        private void Tombstone(Transform t, Vector3 p)
        {
            float tilt = R(-8f, 8f);
            int type = rng.Next(3);
            Material stone = Coin() ? kit.Mat("Tombstone") : kit.Mat("TombstoneDark");
            if (type == 0)
            {
                // Slab with a rounded top.
                Box(t, p + Vector3.up * 0.5f, new Vector3(0.75f, 1f, 0.2f), stone, euler: new Vector3(tilt, 0f, 0f));
                Cyl(t, p + new Vector3(0f, 1.0f, 0f), 0.375f, 0.2f, stone, euler: new Vector3(90f, 0f, 0f));
            }
            else if (type == 1)
            {
                // Cross.
                Box(t, p + Vector3.up * 0.75f, new Vector3(0.18f, 1.5f, 0.18f), stone, euler: new Vector3(tilt, 0f, 0f));
                Box(t, p + Vector3.up * 1.1f, new Vector3(0.75f, 0.18f, 0.18f), stone, solid: false, euler: new Vector3(tilt, 0f, 0f));
            }
            else
            {
                // Low grave with a mound of earth before it.
                Box(t, p + Vector3.up * 0.35f, new Vector3(0.6f, 0.7f, 0.18f), stone);
                Ball(t, p + new Vector3(0f, 0.05f, -0.9f), 0.7f, kit.Mat("Moss"), flatten: 0.3f, solid: false);
            }
        }

        private void FencePost(Transform t, Vector3 p)
        {
            Box(t, p + Vector3.up * 0.6f, new Vector3(0.08f, 1.2f, 0.08f), kit.Mat("Iron"));
            Ball(t, p + Vector3.up * 1.22f, 0.07f, kit.Mat("Iron"), solid: false);
        }

        private GameObject DeadTree(Transform t, Vector3 p, Material wood)
        {
            var tree = new GameObject("DeadTree").transform;
            tree.SetParent(t, false);
            tree.position = p;
            tree.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            float height = R(3f, 5f);
            LocalCyl(tree, new Vector3(0f, height * 0.5f, 0f), 0.22f, height, wood);
            int branches = rng.Next(3, 6);
            for (int b = 0; b < branches; b++)
            {
                float y = R(height * 0.45f, height * 0.95f);
                float yaw = R(0f, 360f);
                float length = R(0.9f, 1.8f);
                Transform branch = LocalBox(tree, new Vector3(0f, y, 0f), new Vector3(0.1f, length, 0.1f), wood, solid: false,
                    euler: new Vector3(R(35f, 60f), yaw, 0f)).transform;
                branch.localPosition += branch.up * length * 0.5f;
            }
            return tree.gameObject;
        }

        private GameObject Candles(Transform t, Vector3 p)
        {
            var group = new GameObject("Candles").transform;
            group.SetParent(t, false);
            group.position = p;
            int n = rng.Next(2, 5);
            for (int k = 0; k < n; k++)
            {
                Vector3 o = new Vector3(R(-0.3f, 0.3f), 0f, R(-0.3f, 0.3f));
                float h = R(0.15f, 0.4f);
                LocalCyl(group, o + Vector3.up * h * 0.5f, 0.05f, h, kit.Mat("Candle"), solid: false);
                LocalBall(group, o + Vector3.up * (h + 0.04f), 0.035f, kit.Mat("Ember"));
            }
            Glow(group, p + Vector3.up * 0.6f, CandleLight, 4.5f, 2.5f, flicker: true);
            return group.gameObject;
        }

        private GameObject Bones(Transform t, Vector3 p)
        {
            var group = new GameObject("Bones").transform;
            group.SetParent(t, false);
            group.position = p;
            LocalBall(group, new Vector3(0f, 0.12f, 0f), 0.13f, kit.Mat("Bone"));
            for (int k = 0; k < 3; k++)
                LocalBox(group, new Vector3(R(-0.4f, 0.4f), 0.05f, R(-0.4f, 0.4f)), new Vector3(0.07f, 0.07f, R(0.4f, 0.7f)), kit.Mat("Bone"),
                    solid: false, euler: new Vector3(0f, R(0f, 180f), 0f));
            return group.gameObject;
        }

        private GameObject Pumpkin(Transform t, Vector3 p)
        {
            GameObject pumpkin = Ball(t, p + Vector3.up * 0.25f, 0.33f, kit.Mat("Pumpkin"), flatten: 0.75f, solid: false);
            Cyl(t, p + Vector3.up * 0.55f, 0.04f, 0.12f, kit.Mat("Moss"), solid: false);
            return pumpkin;
        }

        private void FallenColumn(Transform t, Vector3 p, float yaw)
        {
            Cyl(t, p + Vector3.up * 0.55f, 0.55f, 3.5f, kit.Mat("Sandstone"), euler: new Vector3(90f, yaw, 0f));
        }

        private GameObject Brazier(Transform t, Vector3 p)
        {
            GameObject bowl = Cyl(t, p + Vector3.up * 0.5f, 0.45f, 1f, kit.Mat("Stone"));
            Ball(t, p + Vector3.up * 1.1f, 0.3f, kit.Mat("Ember"), solid: false);
            Ball(t, p + new Vector3(0.12f, 1.3f, 0.05f), 0.16f, kit.Mat("Lantern"), solid: false);
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
            return Center(area) + new Vector3(0f, 0f, -11f);
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
            Npc.CreateFixed(chest, NpcRole.Stash, "Stash", 1.7f);
        }

        // A dark stone column with a glowing blue crystal on top; talk to it to travel.
        private void BuildWaystones()
        {
            Transform t = Group("Waystones");
            foreach (int a in WorldAreas)
            {
                Vector3 p = WaystoneSpot(a);
                if (a == Greenwood)
                    ClearSpot(p, 2.5f);

                var stone = new GameObject("Waystone_" + AreaNames[a]);
                stone.transform.SetParent(t, false);
                stone.transform.position = p;
                Cyl(stone.transform, p + Vector3.up * 0.12f, 0.85f, 0.24f, kit.Mat("Stone"));
                Box(stone.transform, p + Vector3.up * 1.3f, new Vector3(0.55f, 2.3f, 0.55f), kit.Mat("TombstoneDark"), euler: new Vector3(0f, 45f, 0f));
                Ball(stone.transform, p + Vector3.up * 2.75f, 0.38f, kit.Mat("Water"), solid: false);
                Glow(stone.transform, p + Vector3.up * 1.8f, WaystoneLight, 6.5f, 4.5f);

                Transform arrival = Marker("Arrive_" + AreaNames[a] + "_waystone", p + new Vector3(0f, 1.1f, 2.6f), 0f);
                Waystone.Attach(stone, a, arrival);
                Npc.CreateFixed(stone, NpcRole.Waystone, "Waystone", 3.5f);
            }
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
        private static Light Glow(Transform parent, Vector3 p, Color color, float range, float intensity, bool flicker = false)
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
                place(p);
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
