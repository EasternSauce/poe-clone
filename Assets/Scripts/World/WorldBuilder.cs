using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// Builds the world at start-up, the same way on every client (fixed seeds, so spectators see
    /// exactly what the player sees): four areas laid out apart from each other, each with its own
    /// ground, edges, themed props, monster level and gates.
    ///
    ///   Haven (town, safe)  —  Greenwood (lv 1)  —  Haunted Graveyard (lv 4)  —  Ashen Ruins (lv 7)
    ///                                                                         |
    ///                                                               Frozen Hollow (lv 10)
    ///
    /// Greenwood is the scene's original forest; the others are dressed from <see cref="AreaKit"/>
    /// using the project's stylized prefabs and materials. The game starts in Haven, with the
    /// starter gear on the ground around the player. Named spots (NPC stands, the ruins' altar)
    /// are kept in <see cref="Spots"/> for the features that use them.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public const int Greenwood = 0;
        public const int Haven = 1;
        public const int Graveyard = 2;
        public const int Ruins = 3;
        public const int Frozen = 4;

        public const float HalfSize = 48f;

        public static WorldBuilder Instance { get; private set; }

        public static readonly string[] AreaNames = { "Greenwood", "Haven", "Haunted Graveyard", "Ashen Ruins", "Frozen Hollow" };
        public static readonly int[] MonsterLevels = { 1, 0, 4, 7, 10 };

        private static readonly Vector3[] Centers =
        {
            Vector3.zero,
            new Vector3(-220f, 0f, 0f),
            new Vector3(220f, 0f, 0f),
            new Vector3(440f, 0f, 0f),
            new Vector3(660f, 0f, 0f)
        };

        private static readonly Color[] AreaColors =
        {
            new Color(0.55f, 0.75f, 0.55f),
            new Color(0.82f, 0.74f, 0.56f),
            new Color(0.40f, 0.46f, 0.42f),
            new Color(0.58f, 0.44f, 0.36f),
            new Color(0.80f, 0.88f, 0.95f)
        };

        // Per area, one spawn weight per EnemyKinds entry:
        // Zombie, Raider, Brute, Archer, Fire Caster, Frost Caster, Storm Caster,
        // Skeleton, Wraith, Ember Knight (missing entries: the kind's own weight, 0 for natives).
        private static readonly float[][] KindWeights =
        {
            new float[] { 30, 20, 6, 22, 8, 8, 6 },
            null,
            new float[] { 24, 4, 8, 8, 3, 16, 6, 26, 18, 0 },
            new float[] { 6, 14, 10, 8, 22, 5, 10, 4, 0, 22 },
            new float[] { 0, 6, 14, 10, 0, 26, 12, 14, 22, 0 }
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
            return Centers[Mathf.Clamp(area, 0, Centers.Length - 1)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
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
            AreaGate[] oldGates = FindObjectsByType<AreaGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            PlayerController player = FindAnyObjectByType<PlayerController>();

            if (kit == null || manager == null || ground == null || oldGates.Length == 0 || player == null)
            {
                Debug.LogWarning("WorldBuilder: missing area kit, area manager, ground, gates or player - keeping the original single area.");
                if (player != null)
                    StarterLoot.PlaceAround(player.transform.position);
                return;
            }

            // One of the scene's proof-of-concept gates becomes the template for every new gate.
            gateTemplate = Instantiate(oldGates[0].gameObject, root);
            gateTemplate.name = "GateTemplate";
            gateTemplate.SetActive(false);
            foreach (AreaGate old in oldGates)
                Destroy(old.gameObject);

            spawnPoints = new Transform[Centers.Length];
            for (int a = 0; a < Centers.Length; a++)
                spawnPoints[a] = Marker("Spawn_" + AreaNames[a], Centers[a] + new Vector3(0f, 1.1f, -6f), 0f);

            for (int a = 1; a < Centers.Length; a++)
                BuildGround(ground, a);

            BuildHaven();
            BuildGraveyard();
            BuildRuins();
            BuildFrozen();
            BuildWaystones();

            // Gates: Haven - Greenwood - Graveyard - Ruins, and a way home from the Ruins.
            Connect(Haven, new Vector3(40f, 0f, 0f), Greenwood, new Vector3(-40f, 0f, 2f));
            Connect(Greenwood, new Vector3(40f, 0f, -2f), Graveyard, new Vector3(-40f, 0f, 0f));
            Connect(Graveyard, new Vector3(40f, 0f, 0f), Ruins, new Vector3(-40f, 0f, 0f));
            OneWayGate(Ruins, new Vector3(0f, 0f, -40f), Haven, spawnPoints[Haven]);
            Connect(Ruins, new Vector3(0f, 0f, 40f), Frozen, new Vector3(-40f, 0f, 0f));

            SetUpSpawners();
            BuildTownsfolk();
            PlaceBosses();

            var definitions = new AreaDefinition[Centers.Length];
            for (int a = 0; a < Centers.Length; a++)
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

            // Colliders made this frame aren't in the physics world until it syncs; the starter
            // loot below finds the ground by raycast.
            Physics.SyncTransforms();

            // Start in town.
            MovePlayer(player, spawnPoints[Haven]);
            manager.SetAreas(definitions, Haven);
            StarterLoot.PlaceAround(spawnPoints[Haven].position + Vector3.down * 1.1f);
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

            // Invisible walls at the edges, like the original area's.
            var bounds = new GameObject("Bounds_" + AreaNames[area]).transform;
            bounds.SetParent(root, false);
            Wall(bounds, c + new Vector3(0f, 5f, 49.5f), new Vector3(100f, 10f, 1f));
            Wall(bounds, c + new Vector3(0f, 5f, -49.5f), new Vector3(100f, 10f, 1f));
            Wall(bounds, c + new Vector3(49.5f, 5f, 0f), new Vector3(1f, 10f, 100f));
            Wall(bounds, c + new Vector3(-49.5f, 5f, 0f), new Vector3(1f, 10f, 100f));
        }

        private static Texture2D GroundTexture(int area)
        {
            switch (area)
            {
                case Haven:
                    return GroundTextures.Make(11, new Color(0.30f, 0.45f, 0.22f), new Color(0.50f, 0.60f, 0.30f),
                        new Color(0.55f, 0.46f, 0.32f), 0.02f, 6f);
                case Graveyard:
                    return GroundTextures.Make(22, new Color(0.17f, 0.20f, 0.17f), new Color(0.31f, 0.33f, 0.27f),
                        new Color(0.40f, 0.40f, 0.36f), 0.015f, 7f);
                case Frozen:
                    return GroundTextures.Make(44, new Color(0.78f, 0.84f, 0.90f), new Color(0.93f, 0.96f, 0.98f),
                        new Color(0.55f, 0.72f, 0.88f), 0.012f, 7f);
                default:
                    return GroundTextures.Make(33, new Color(0.22f, 0.19f, 0.18f), new Color(0.44f, 0.37f, 0.31f),
                        new Color(0.85f, 0.35f, 0.12f), 0.01f, 8f);
            }
        }

        // Haven: a market town round a well, houses in a ring, lamps, stalls, crates.
        private void BuildHaven()
        {
            Begin(Haven, 101);
            Vector3 c = Centers[Haven];
            Transform t = Group("Haven");

            // Plaza and the well at its heart.
            Cyl(t, c + new Vector3(0f, 0.02f, 0f), 11f, 0.04f, kit.Mat("Stone"), solid: false);
            Cyl(t, c + new Vector3(0f, 0.45f, 0f), 1.3f, 0.9f, kit.Mat("Stone"));
            Cyl(t, c + new Vector3(0f, 0.92f, 0f), 1.05f, 0.04f, kit.Mat("Water"), solid: false);
            Box(t, c + new Vector3(-1.2f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.18f), kit.Mat("Wood"));
            Box(t, c + new Vector3(1.2f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.18f), kit.Mat("Wood"));
            Box(t, c + new Vector3(0f, 2.9f, 0f), new Vector3(3.2f, 0.25f, 2.2f), kit.Mat("Roof"), euler: new Vector3(0f, 0f, 0f));
            Claim(c, 12f);

            // Dirt road from the plaza to the gate east, and one south.
            Box(t, c + new Vector3(25f, 0.025f, 0f), new Vector3(28f, 0.05f, 3.6f), kit.Mat("TanDark"), solid: false);
            Box(t, c + new Vector3(0f, 0.025f, -24f), new Vector3(3.6f, 0.05f, 26f), kit.Mat("TanDark"), solid: false);

            // Houses in a ring, facing the well.
            float[] houseAngles = { 25f, 70f, 115f, 160f, 205f, 250f, 330f };
            foreach (float deg in houseAngles)
            {
                float rad = deg * Mathf.Deg2Rad;
                float dist = 27f + R(-2f, 3f);
                Vector3 p = c + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * dist;
                if (!Free(p, 6f))
                    continue;
                GameObject house = Prefab(kit.house, t, p, 0f, Vector3.one * R(0.95f, 1.15f));
                Vector3 toWell = c - p;
                house.transform.rotation = Quaternion.LookRotation(new Vector3(toWell.x, 0f, toWell.z));
                Claim(p, 6.5f);
            }

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
            Spots["Guard"] = c + new Vector3(33f, 0f, 4f);
        }

        // The Haunted Graveyard: plots of tombstones behind iron fences, dead trees, a crypt.
        private void BuildGraveyard()
        {
            Begin(Graveyard, 202);
            Vector3 c = Centers[Graveyard];
            Transform t = Group("Graveyard");

            // Central path, west to east.
            Box(t, c + new Vector3(0f, 0.025f, 0f), new Vector3(86f, 0.05f, 3.6f), kit.Mat("Ash"), solid: false);
            Claim(c + new Vector3(-30f, 0f, 0f), 3f);
            Claim(c + new Vector3(0f, 0f, 0f), 3f);
            Claim(c + new Vector3(30f, 0f, 0f), 3f);

            // The crypt, north of the path.
            Vector3 crypt = c + new Vector3(0f, 0f, 20f);
            Box(t, crypt + new Vector3(0f, 2f, 0f), new Vector3(7f, 4f, 7f), kit.Mat("TombstoneDark"));
            Box(t, crypt + new Vector3(0f, 4.4f, 0f), new Vector3(7.8f, 0.8f, 7.8f), kit.Mat("Tombstone"));
            Box(t, crypt + new Vector3(0f, 1.3f, -3.55f), new Vector3(2f, 2.6f, 0.2f), kit.Mat("Charred"), solid: false);
            for (int k = -1; k <= 1; k += 2)
                Cyl(t, crypt + new Vector3(k * 2.4f, 1.9f, -4.2f), 0.35f, 3.8f, kit.Mat("Tombstone"));
            Claim(crypt, 7f);
            Spots["Crypt"] = crypt + new Vector3(0f, 0f, -6f);

            // Plots of graves, each fenced.
            Vector2[] plots =
            {
                new Vector2(-26f, 15f), new Vector2(-26f, -15f), new Vector2(26f, 15f),
                new Vector2(26f, -15f), new Vector2(-6f, -18f), new Vector2(14f, -30f)
            };
            foreach (Vector2 plot in plots)
                GravePlot(t, c + Flat(plot.x, plot.y));

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
            Box(t, c + new Vector3(0f, 0.07f, 8f), new Vector3(20f, 0.14f, 20f), kit.Mat("Sandstone"), solid: false);
            Vector3 temple = c + new Vector3(0f, 0f, 8f);
            for (int k = 0; k < 10; k++)
            {
                float rad = k * 36f * Mathf.Deg2Rad;
                Vector3 p = temple + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 8.5f;
                if (k == 3 || k == 7)
                    FallenColumn(t, p, k * 36f + 70f);
                else
                    Prefab(kit.pillar, t, p + Vector3.up * 0.14f, R(0f, 360f), new Vector3(1f, R(0.45f, 1.05f), 1f));
            }
            Box(t, temple + new Vector3(0f, 0.64f, 3f), new Vector3(2.4f, 1f, 1.4f), kit.Mat("Sandstone"));
            Ball(t, temple + new Vector3(-0.7f, 1.34f, 3f), 0.35f, kit.Mat("Ember"));
            Ball(t, temple + new Vector3(0.7f, 1.34f, 3f), 0.35f, kit.Mat("Ember"));
            Claim(temple, 11f);
            Spots["Altar"] = temple;

            // Broken walls: rows of blocks with gaps, a few fallen.
            for (int w = 0; w < 9; w++)
            {
                Vector3 start = c + Flat(R(-40f, 40f), R(-40f, 40f));
                if (!Free(start, 5f))
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
                Vector3 p = c + Flat(R(-44f, 44f), R(-44f, 44f));
                if (Vector3.Distance(p, temple) < 11f)
                    continue;
                Box(t, p + Vector3.up * 0.03f, new Vector3(R(0.25f, 0.5f), 0.05f, R(3f, 8f)), kit.Mat("Lava"), solid: false,
                    euler: new Vector3(0f, R(0f, 180f), 0f));
            }

            Scatter(t, 8, 10f, 42f, p => Brazier(t, p), 1f);
            Scatter(t, 14, 8f, 46f, p => DeadTree(t, p, kit.Mat("Charred")), 1.6f);
            Scatter(t, 10, 8f, 46f, p => Ball(t, p, R(0.6f, 1.2f), kit.Mat("Ash"), flatten: 0.35f, solid: false), 1f);
            Scatter(t, 14, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.5f)), 1.5f);
        }

        // The Frozen Hollow: snow, frosted pines, ice crystals and frozen ponds round the queen's
        // throne, a ring of ice spikes north of the centre.
        private void BuildFrozen()
        {
            Begin(Frozen, 404);
            Vector3 c = Centers[Frozen];
            Transform t = Group("Frozen");

            Vector3 throne = c + new Vector3(0f, 0f, 24f);
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
                Vector3 p = c + Flat(R(-38f, 38f), R(-38f, 38f));
                float radius = R(3f, 6f);
                if (!Free(p, radius))
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
            EnemySpawner original = FindAnyObjectByType<EnemySpawner>();
            if (original == null || original.EnemyPrefab == null)
                return;

            GameObject prefab = original.EnemyPrefab;
            original.Configure(prefab, Centers[Greenwood], 40f, 22, MonsterLevels[Greenwood], KindWeights[Greenwood]);
            original.SetSafeSpots(SafeSpots(Greenwood));

            foreach (int area in new[] { Graveyard, Ruins, Frozen })
            {
                var go = new GameObject("Spawner_" + AreaNames[area]);
                go.transform.SetParent(root, false);
                go.transform.position = Centers[area];
                var spawner = go.AddComponent<EnemySpawner>();
                spawner.Configure(prefab, Centers[area], 40f, 24, MonsterLevels[area], KindWeights[area]);
                spawner.SetSafeSpots(SafeSpots(area));
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
            foreach (Transform child in root)
            {
                if (child.name.StartsWith("Arrive_" + AreaNames[area] + "_"))
                    spots.Add(child.position);
            }
            return spots;
        }

        // ------------------------------------------------------------------ gates

        // A gate in each area leading to the other; you arrive a few steps in front of the gate back.
        private void Connect(int a, Vector3 offsetA, int b, Vector3 offsetB)
        {
            Vector3 gateA = Centers[a] + offsetA;
            Vector3 gateB = Centers[b] + offsetB;
            Transform arriveInA = Marker("Arrive_" + AreaNames[a] + "_from_" + AreaNames[b], InFront(a, gateA), Yaw(gateA, Centers[a]));
            Transform arriveInB = Marker("Arrive_" + AreaNames[b] + "_from_" + AreaNames[a], InFront(b, gateB), Yaw(gateB, Centers[b]));
            ClearSpot(arriveInA.position, 2f);
            ClearSpot(arriveInB.position, 2f);

            Gate(a, gateA, b, arriveInB);
            Gate(b, gateB, a, arriveInA);
        }

        private void OneWayGate(int from, Vector3 offset, int to, Transform arrival)
        {
            Gate(from, Centers[from] + offset, to, arrival);
        }

        private void Gate(int from, Vector3 position, int to, Transform arrival)
        {
            ClearSpot(position, 4.5f);
            GameObject gate = Instantiate(gateTemplate, root);
            gate.name = "Gate_" + AreaNames[from] + "_to_" + AreaNames[to];
            gate.transform.position = new Vector3(position.x, gateTemplate.transform.position.y, position.z);
            // Face into the area, so you walk through it heading out.
            Vector3 inward = Centers[from] - position;
            inward.y = 0f;
            gate.transform.rotation = Quaternion.LookRotation(inward.sqrMagnitude > 0.01f ? inward : Vector3.forward);

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
            "Trees", "Rocks", "Bushes", "Haven", "Graveyard", "Ruins", "Frozen"
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

        // A dark stone column with a glowing blue crystal on top; talk to it to travel.
        private void BuildWaystones()
        {
            Transform t = Group("Waystones");
            for (int a = 0; a < Centers.Length; a++)
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

                Transform arrival = Marker("Arrive_" + AreaNames[a] + "_waystone", p + new Vector3(0f, 1.1f, 2.6f), 0f);
                Waystone.Attach(stone, a, arrival);
                Npc.CreateFixed(stone, NpcRole.Waystone, "Waystone", 3.5f);
            }
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

        private static void Wall(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
        }

        // Places up to `count` things at random spots in a ring round the current area's centre.
        private void Scatter(Transform t, int count, float minRadius, float maxRadius, System.Func<Vector3, GameObject> place, float radius)
        {
            Vector3 c = Centers[CurrentArea()];
            for (int k = 0, attempts = 0; k < count && attempts < count * 12; attempts++)
            {
                Vector3 p = c + Flat(R(-maxRadius, maxRadius), R(-maxRadius, maxRadius));
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
                if (d < minRadius || d > maxRadius || !Free(p, radius))
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
