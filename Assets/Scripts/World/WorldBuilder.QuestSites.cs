using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Quests;
using PoeClone.Visuals;

namespace PoeClone.World
{
    /// <summary>
    /// The story's places out in the wilds: Tobias's warded hut in the Haunted Graveyard, the
    /// Emberwatch camp deep in the Ashen Ruins (both safe ground: see <see cref="Sanctuary"/>),
    /// and everything the quests have the player use (<see cref="QuestProp"/>) - bone totems,
    /// reliquaries, caches, sunstones, the ice over the north gate (<see cref="QuestBarrier"/>),
    /// the door beneath the ice. Fixed spots, so every client builds the same thing.
    /// </summary>
    public partial class WorldBuilder
    {
        private const float KeeperWard = 9f;
        private const float CampWard = 13.5f;

        /// <summary>The spawner that fills an area (null for Haven).</summary>
        public EnemySpawner SpawnerFor(int area)
        {
            return spawnersByArea != null && area >= 0 && area < spawnersByArea.Length ? spawnersByArea[area] : null;
        }

        // A spot in an area, pulled in towards the middle if it would sit too near the edge.
        private static Vector3 Site(int area, float x, float z)
        {
            Vector3 c = Centers[area];
            Vector3 p = c + new Vector3(x, 0f, z) * 1.8f;
            AreaShape shape = Shape(area);
            return shape.NearestOpen(p, 16f);
        }

        private static Vector3[] QuestPoints(int area, string group)
        {
            Vector3[] anchors = AreaLayouts.QuestAnchors[group];
            var points = new Vector3[anchors.Length];
            for (int i = 0; i < anchors.Length; i++)
                points[i] = Shape(area).NearestOpen(Center(area) + anchors[i], 5f);
            return points;
        }

        // Makes room: scenery with colliders (ClearSpot), and loose decor of the area's own group
        // (lava cracks, ash heaps, lights) standing within the radius.
        private void ClearSite(int area, Vector3 at, float radius)
        {
            Physics.SyncTransforms();
            ClearSpot(at, radius);
            // The edge's dressing reaches well in (spurs of rock and ice): any piece of it in the way goes.
            Transform border = root.Find("Border_" + AreaNames[area]);
            if (border != null)
            {
                foreach (Renderer r in border.GetComponentsInChildren<Renderer>())
                {
                    Vector3 d = r.transform.position - at;
                    d.y = 0f;
                    if (d.magnitude < radius)
                        r.gameObject.SetActive(false);
                }
            }

            string group = area == Graveyard ? "Graveyard" : area == Ruins ? "Ruins" : area == Frozen ? "Frozen" : null;
            Transform decor = group != null ? root.Find(group) : null;
            if (decor == null)
                return;
            for (int k = decor.childCount - 1; k >= 0; k--)
            {
                Transform child = decor.GetChild(k);
                Vector3 d = child.position - at;
                d.y = 0f;
                if (d.magnitude < radius)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        private static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return Quaternion.LookRotation(d.sqrMagnitude > 0.001f ? d : Vector3.forward);
        }

        private Transform Holder(Transform parent, string name, Vector3 p, Quaternion rotation)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.SetPositionAndRotation(p, rotation);
            return go;
        }

        // ------------------------------------------------------------------ outposts

        // Before the spawners: their wards keep monsters from spawning inside.
        private void BuildOutposts()
        {
            BuildKeeperHut();
            BuildEmberwatch();
        }

        // Tobias's hut, in the graveyard's south-west corner, inside a ring of ward candles.
        private void BuildKeeperHut()
        {
            Begin(Graveyard, 611);
            Vector3 c = Centers[Graveyard];
            Vector3 site = Site(Graveyard, -40f, -34f);
            ClearSite(Graveyard, site, KeeperWard + 1f);
            Transform t = Group("KeeperHut");

            Vector3 toCentre = (c - site);
            toCentre.y = 0f;
            toCentre.Normalize();
            Vector3 hutAt = site - toCentre * 6f;
            GameObject hut = Prefab(kit.house, t, hutAt, 0f, Vector3.one * 0.72f);
            hut.transform.rotation = Facing(hutAt, c);

            // Ward candles round the edge, glowing spirit-green, and a lantern on a post.
            for (int k = 0; k < 14; k++)
            {
                float rad = k * Mathf.PI * 2f / 14f;
                Vector3 p = site + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * KeeperWard;
                LocalCyl(t, t.InverseTransformPoint(p + Vector3.up * 0.25f), 0.07f, 0.5f, kit.Mat("Candle"), solid: false);
                GameObject flame = RuntimePrimitives.Create(PrimitiveType.Sphere, t, SpiritLight);
                flame.transform.position = p + Vector3.up * 0.58f;
                flame.transform.localScale = new Vector3(0.09f, 0.15f, 0.09f);
                if (k % 3 == 0)
                    Glow(t, p + Vector3.up * 0.9f, SpiritLight, 5f, 2.2f, flicker: true);
            }
            Lamp(t, site + toCentre * 2f + Vector3.Cross(Vector3.up, toCentre) * 2.6f);

            // A shovel in a fresh mound, a bench, his own small plot.
            Box(t, site + Vector3.Cross(Vector3.up, toCentre) * -3f + Vector3.up * 0.15f, new Vector3(1.2f, 0.3f, 2f), kit.Mat("TanDark"),
                solid: false, euler: new Vector3(0f, Quaternion.LookRotation(toCentre).eulerAngles.y, 0f));
            Box(t, site + Vector3.Cross(Vector3.up, toCentre) * -3.4f + Vector3.up * 0.8f, new Vector3(0.08f, 1.5f, 0.08f), kit.Mat("Wood"),
                solid: false, euler: new Vector3(15f, 0f, 10f));
            // (Not Bench(): that one lays out the starter gear.)
            Box(t, site + Vector3.Cross(Vector3.up, toCentre) * 3f - toCentre * 1f + Vector3.up * 0.25f, new Vector3(0.6f, 0.5f, 2.4f), kit.Mat("Wood"),
                euler: new Vector3(0f, Quaternion.LookRotation(toCentre).eulerAngles.y, 0f));
            Tombstone(t, site - toCentre * 0.5f + Vector3.Cross(Vector3.up, toCentre) * 5.5f);

            Spots["Gravekeeper"] = site + toCentre * 0.5f;
            Sanctuary.Add(site, KeeperWard);
        }

        // Emberwatch: a palisade camp south-east of the ruined temple, its gap facing the temple.
        private void BuildEmberwatch()
        {
            Begin(Ruins, 622);
            Vector3 c = Centers[Ruins];
            Vector3 site = Site(Ruins, 42f, -40f);
            ClearSite(Ruins, site, CampWard + 2f);
            Transform t = Group("Emberwatch");

            Vector3 toCentre = (c - site);
            toCentre.y = 0f;
            toCentre.Normalize();
            float openingYaw = Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg;
            Vector3 side = Vector3.Cross(Vector3.up, toCentre);

            // Packed earth, then the palisade: sharpened stakes, open towards the temple.
            Cyl(t, site + Vector3.up * 0.03f, CampWard - 1f, 0.05f, kit.Mat("TanDark"), solid: false);
            const float wallRadius = 12f;
            for (float deg = 0f; deg < 360f; deg += 5.5f)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(deg, openingYaw)) < 22f)
                    continue;
                float rad = deg * Mathf.Deg2Rad;
                Vector3 p = site + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * wallRadius;
                float h = R(2.4f, 3.1f);
                Cyl(t, p + Vector3.up * h * 0.5f, 0.28f, h, kit.Mat("Wood"));
                if (kit.cone != null)
                {
                    var tip = new GameObject("StakeTip");
                    tip.transform.SetParent(t, false);
                    tip.transform.SetPositionAndRotation(p + Vector3.up * h, Quaternion.identity);
                    tip.transform.localScale = new Vector3(0.55f, 0.5f, 0.55f);
                    tip.AddComponent<MeshFilter>().sharedMesh = kit.cone;
                    tip.AddComponent<MeshRenderer>().sharedMaterial = kit.Mat("Wood");
                }
            }
            // Two watch torches at the opening.
            for (int s = -1; s <= 1; s += 2)
            {
                float rad = (openingYaw + s * 25f) * Mathf.Deg2Rad;
                Vector3 p = site + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * wallRadius;
                Box(t, p + Vector3.up * 1.6f, new Vector3(0.18f, 3.2f, 0.18f), kit.Mat("Wood"));
                Ball(t, p + Vector3.up * 3.3f, 0.22f, kit.Mat("Ember"), solid: false);
                Glow(t, p + Vector3.up * 3.6f, FireLight, 9f, 4.5f, flicker: true);
            }

            // The big campfire in the middle.
            for (int k = 0; k < 4; k++)
                Cyl(t, site + Vector3.up * 0.15f, 0.12f, 1.6f, kit.Mat("Charred"), solid: false, euler: new Vector3(90f, k * 45f, 0f));
            Ball(t, site + Vector3.up * 0.35f, 0.45f, kit.Mat("Ember"), flatten: 0.6f, solid: false);
            Ball(t, site + Vector3.up * 0.7f, 0.25f, kit.Mat("Lantern"), solid: false);
            Cyl(t, site + Vector3.up * 0.4f, 1.1f, 0.8f, kit.Mat("Stone"), solid: true).GetComponent<Renderer>().enabled = false;
            Glow(t, site + Vector3.up * 1.4f, FireLight, 14f, 7f, flicker: true);

            // Tents round the back half, facing the fire.
            float[] tentAngles = { 110f, 180f, 250f };
            string[] cloths = { "ClothRed", "ClothRed", "ClothYellow" };
            for (int k = 0; k < tentAngles.Length; k++)
            {
                float rad = (openingYaw + tentAngles[k]) * Mathf.Deg2Rad;
                Vector3 p = site + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * 7.5f;
                Tent(t, p, Facing(p, site), kit.Mat(cloths[k]), k == 1 ? 1.3f : 1f);
            }

            // A banner of the watch, weapon racks, crates.
            Vector3 banner = site - toCentre * 3.5f + side * 3.5f;
            Box(t, banner + Vector3.up * 2.5f, new Vector3(0.14f, 5f, 0.14f), kit.Mat("Wood"));
            Box(t, banner + Vector3.up * 4.1f + side * 0.6f, new Vector3(1.1f, 1.6f, 0.05f), kit.Mat("ClothRed"), solid: false,
                euler: new Vector3(0f, openingYaw + 90f, 0f));
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = site + side * R(-8f, 8f) - toCentre * R(-2f, 8f);
                if (Vector3.Distance(p, site) < 3f)
                    continue;
                Box(t, p + Vector3.up * 0.4f, Vector3.one * 0.8f, kit.Mat("Wood"), euler: new Vector3(0f, R(0f, 90f), 0f));
            }
            Cyl(t, site + side * -5.5f + toCentre * 2f + Vector3.up * 0.5f, 0.38f, 1f, kit.Mat("Wood"));
            Cyl(t, site + side * -6.2f + toCentre * 1.4f + Vector3.up * 0.5f, 0.38f, 1f, kit.Mat("Wood"));

            Spots["Commander"] = site + toCentre * 2.4f + side * 1.2f;
            Spots["Seer"] = site - toCentre * 1.2f - side * 2.6f;
            Spots["CampFire"] = site;
            Sanctuary.Add(site, CampWard);
        }

        // A ridge tent: two cloth slopes over a dark floor, open at the front.
        private void Tent(Transform t, Vector3 p, Quaternion rotation, Material cloth, float size)
        {
            Transform tent = Holder(t, "Tent", p, rotation);
            tent.localScale = Vector3.one * size;
            for (int s = -1; s <= 1; s += 2)
                LocalBox(tent, new Vector3(s * 0.85f, 1f, 0f), new Vector3(0.08f, 2.3f, 3f), cloth, euler: new Vector3(0f, 0f, s * 38f));
            LocalBox(tent, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.05f, 3f), kit.Mat("TanDark"), solid: false);
            LocalBox(tent, new Vector3(0f, 1.9f, 0f), new Vector3(0.12f, 0.12f, 3.2f), kit.Mat("Wood"), solid: false);
            Claim(p, 2.2f * size);
        }

        // The keeper, the commander and the seer, once the enemy rig is known.
        private void BuildOutpostFolk(GameObject prefab)
        {
            Transform t = Group("OutpostFolk");
            Spots.TryGetValue("CampFire", out Vector3 fire);
            AddNpc(prefab, t, NpcRole.Gravekeeper, "Tobias the Gravekeeper", Centers[Graveyard], new EnemyKind
            {
                Name = "Gravekeeper", Scale = 0.94f, HideHorns = true,
                Cloth = new Color(0.30f, 0.31f, 0.28f), Pants = new Color(0.22f, 0.20f, 0.18f),
                Skin = new Color(0.72f, 0.64f, 0.58f), Eyes = new Color(0.2f, 0.2f, 0.2f),
                Gear = new[] { "gnarled_staff" }
            });
            AddNpc(prefab, t, NpcRole.Commander, "Commander Varek", Centers[Ruins], new EnemyKind
            {
                Name = "Commander", Scale = 1.08f, HideHorns = true,
                Cloth = new Color(0.58f, 0.16f, 0.12f), Pants = new Color(0.26f, 0.22f, 0.22f),
                Skin = new Color(0.66f, 0.50f, 0.40f), Eyes = new Color(0.1f, 0.08f, 0.06f),
                Gear = new[] { "iron_helmet", "rusty_sword", "wooden_shield" }
            });
            AddNpc(prefab, t, NpcRole.Seer, "Seer Ysolde", fire, new EnemyKind
            {
                Name = "Seer", Scale = 0.98f, HideHorns = true,
                Cloth = new Color(0.62f, 0.34f, 0.12f), Pants = new Color(0.30f, 0.16f, 0.10f),
                Skin = new Color(0.80f, 0.68f, 0.58f), Eyes = new Color(0.9f, 0.55f, 0.2f),
                Gear = new[] { "gnarled_staff" }
            });
        }

        // ------------------------------------------------------------------ quest sites

        // After the gates (the ice seals one) and borders.
        private void BuildQuestSites()
        {
            Transform t = Group("QuestSites");

            // The Hollow Call: bone totems in the Greenwood.
            Begin(Greenwood, 631);
            Vector3[] totems = QuestPoints(Greenwood, "totems");
            for (int k = 0; k < totems.Length; k++)
                Totem(t, totems[k], k);

            // The Lost Patrol: Hale's scouts.
            Vector3[] scouts = QuestPoints(Greenwood, "patrol");
            string[] names = { "Aldo", "Bren", "Corin" };
            for (int k = 0; k < scouts.Length; k++)
                FallenScout(t, scouts[k], k, names[k]);

            // The Gravelord: Mortis's reliquaries; the Lost Caravan: Oda's crates.
            Begin(Graveyard, 632);
            Vector3[] relics = QuestPoints(Graveyard, "relics");
            for (int k = 0; k < relics.Length; k++)
                Reliquary(t, relics[k], k);
            Vector3[] crates = QuestPoints(Graveyard, "caravan");
            for (int k = 0; k < crates.Length; k++)
                OdaCrate(t, crates[k], k);

            // Embers of War: supply caches, the Ashen Shrine. The Frozen Seal: sunstones, the ice.
            Begin(Ruins, 633);
            Vector3[] caches = QuestPoints(Ruins, "supplies");
            for (int k = 0; k < caches.Length; k++)
                SupplyCache(t, caches[k], k);
            AshenShrine(t, QuestPoints(Ruins, "shrine")[0]);
            Vector3[] suns = QuestPoints(Ruins, "sunstones");
            for (int k = 0; k < suns.Length; k++)
                Sunstone(t, suns[k], k);
            IceSeal(t);

            // The Frozen Seal: the lost scouts, the door beneath the ice.
            Begin(Frozen, 634);
            Vector3[] frozen = QuestPoints(Frozen, "scouts");
            string[] journals =
            {
                "\"Day 3. Spiders the size of carts in the pines. Lost Mott to one. The cold is worse than the spiders.\"",
                "\"Day 5. Saw her. A queen on a throne of ice, many-legged, crawlers swarming at her feet. She was singing. The dead walked past her, north, and she let them.\"",
                "\"Day 6. Behind the throne, a door taller than a house. Antlers cut in the stone. It's warm. Why is it warm? Something breathes behind it. I can't feel my\""
            };
            for (int k = 0; k < frozen.Length; k++)
                FrozenScout(t, frozen[k], k, journals[k]);
            StagDoor(t, SanctuaryDoorSpot);
        }

        private Light QuestGlow(Transform parent, Vector3 p, Color color, float range, float intensity)
        {
            Light light = Glow(parent, p, color, range, intensity, flicker: true);
            light.enabled = false;
            return light;
        }

        private void Totem(Transform t, Vector3 p, int index)
        {
            ClearSite(Greenwood, p, 3f);
            Transform site = Holder(t, "BoneTotem", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            Transform whole = Holder(site, "Whole", p, site.rotation);
            LocalCyl(whole, new Vector3(0f, 1.2f, 0f), 0.14f, 2.4f, kit.Mat("Bone"));
            LocalBox(whole, new Vector3(0f, 1.7f, 0f), new Vector3(1.3f, 0.1f, 0.1f), kit.Mat("Bone"), solid: false);
            LocalBox(whole, new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 0.08f, 0.08f), kit.Mat("Rope"), solid: false);
            LocalBall(whole, new Vector3(0f, 2.55f, 0f), 0.24f, kit.Mat("Bone"));
            // Antlers: a pair of forked tines either side of the skull.
            for (int s = -1; s <= 1; s += 2)
            {
                LocalBox(whole, new Vector3(s * 0.32f, 2.85f, 0f), new Vector3(0.06f, 0.7f, 0.06f), kit.Mat("Bone"), solid: false,
                    euler: new Vector3(0f, 0f, -s * 35f));
                LocalBox(whole, new Vector3(s * 0.55f, 3.15f, 0f), new Vector3(0.05f, 0.45f, 0.05f), kit.Mat("Bone"), solid: false,
                    euler: new Vector3(0f, 0f, -s * 10f));
                LocalBox(whole, new Vector3(s * 0.32f, 3.15f, 0f), new Vector3(0.05f, 0.35f, 0.05f), kit.Mat("Bone"), solid: false,
                    euler: new Vector3(0f, 0f, s * 20f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject eye = RuntimePrimitives.Create(PrimitiveType.Sphere, whole, SpiritLight);
                eye.transform.localPosition = new Vector3(s * 0.08f, 2.58f, 0.2f);
                eye.transform.localScale = Vector3.one * 0.07f;
            }
            Glow(whole, p + Vector3.up * 2.2f, SpiritLight, 7f, 3.5f, flicker: true);
            Transform spent = Holder(site, "Shards", p, site.rotation);
            Bones(spent, p + new Vector3(0.6f, 0f, 0.2f));
            Bones(spent, p + new Vector3(-0.5f, 0f, -0.4f));
            LocalCyl(spent, new Vector3(0f, 0.3f, 0f), 0.14f, 0.6f, kit.Mat("Bone"));

            QuestProp prop = QuestProp.Attach(site.gameObject, "totems", index, PropAction.Smash, "Bone Totem",
                "A pole of lashed grave-bone, taller than a man, crowned with a skull and a pair of carved antlers. It hums, low, like a bell that won't stop ringing. Every dead thing nearby has turned to face it.",
                "Smash the totem", "The totem shatters - the dead come running!", 3.6f,
                new[] { "Zombie", "Zombie", "Raider" }, 3);
            prop.SetLooks(whole.gameObject, spent.gameObject, null);
        }

        private void FallenScout(Transform t, Vector3 p, int index, string name)
        {
            ClearSite(Greenwood, p, 2.5f);
            Transform site = Holder(t, "FallenScout", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            Body(site, kit.Mat("ClothBlue"), arrows: true);
            Transform badge = Holder(site, "Badge", p, site.rotation);
            LocalBall(badge, new Vector3(0.15f, 0.42f, 0.1f), 0.08f, kit.Mat("Gold"));
            Glow(badge, p + Vector3.up * 0.9f, LanternLight, 3.5f, 1.6f);

            QuestProp prop = QuestProp.Attach(site.gameObject, "patrol", index, PropAction.Take, "Fallen Scout",
                "One of Hale's scouts, " + name + ", face down in the leaves. Three raider arrows in his back; he never saw them. His sword is still sheathed.\n\nThe watch badge is still pinned to his coat.",
                "Take " + name + "'s badge", name + "'s badge taken... wolves!", 1.6f,
                new[] { "Dire Wolf" }, 2);
            prop.SetLooks(badge.gameObject, null, null);
        }

        // Someone lying dead: a body on its side, a head, and (raiders' work) arrows in the back.
        private void Body(Transform site, Material cloth, bool arrows)
        {
            LocalBox(site, new Vector3(0f, 0.22f, 0f), new Vector3(0.55f, 0.35f, 1.2f), cloth);
            LocalBall(site, new Vector3(0f, 0.22f, 0.8f), 0.2f, kit.Mat("Tan"));
            LocalBox(site, new Vector3(0.18f, 0.12f, -0.95f), new Vector3(0.16f, 0.16f, 0.8f), kit.Mat("Leather"), solid: false);
            LocalBox(site, new Vector3(-0.18f, 0.12f, -0.95f), new Vector3(0.16f, 0.16f, 0.8f), kit.Mat("Leather"), solid: false,
                euler: new Vector3(0f, 12f, 0f));
            if (!arrows)
                return;
            for (int k = 0; k < 3; k++)
                LocalBox(site, new Vector3(R(-0.15f, 0.15f), 0.6f, R(-0.3f, 0.4f)), new Vector3(0.03f, 0.7f, 0.03f), kit.Mat("Wood"), solid: false,
                    euler: new Vector3(R(-25f, 25f), 0f, R(-25f, 25f)));
        }

        private void Reliquary(Transform t, Vector3 p, int index)
        {
            ClearSite(Graveyard, p, 3.5f);
            Transform site = Holder(t, "Reliquary", p, Facing(p, Centers[Graveyard]));
            LocalBox(site, new Vector3(0f, 0.2f, 0f), new Vector3(2.2f, 0.4f, 1.6f), kit.Mat("TombstoneDark"));
            Transform whole = Holder(site, "Whole", p, site.rotation);
            LocalBox(whole, new Vector3(0f, 0.85f, 0f), new Vector3(1.5f, 0.9f, 0.9f), kit.Mat("Tombstone"));
            LocalBox(whole, new Vector3(0f, 1.38f, 0f), new Vector3(1.7f, 0.16f, 1.05f), kit.Mat("TombstoneDark"), solid: false);
            LocalBall(whole, new Vector3(0f, 1.62f, 0f), 0.2f, kit.Mat("Bone"));
            LocalBox(whole, new Vector3(0f, 0.9f, -0.46f), new Vector3(0.5f, 0.5f, 0.04f), kit.Mat("Gold"), solid: false);
            Glow(whole, p + Vector3.up * 1.9f, new Color(0.65f, 0.4f, 1f), 7f, 3.5f, flicker: true);
            Transform spent = Holder(site, "Broken", p, site.rotation);
            LocalBox(spent, new Vector3(-0.35f, 0.6f, 0f), new Vector3(0.7f, 0.45f, 0.9f), kit.Mat("Tombstone"), euler: new Vector3(0f, 0f, 14f));
            LocalBox(spent, new Vector3(0.6f, 0.5f, 0.3f), new Vector3(0.6f, 0.25f, 0.6f), kit.Mat("Tombstone"), solid: false, euler: new Vector3(20f, 30f, 0f));
            Bones(spent, p + new Vector3(0.3f, 0.35f, -1.1f));
            for (int s = -1; s <= 1; s += 2)
                Candles(site, p + site.right * s * 1.5f);

            QuestProp prop = QuestProp.Attach(site.gameObject, "relics", index, PropAction.Smash, "Reliquary of Mortis",
                "A stone casket on a plinth, sealed with gold. Through a crack you can see bones wrapped in purple silk - part of the Gravelord, kept safe from death. The candles around it never burn down.",
                "Break the reliquary", "The reliquary breaks - its guardians rise!", 2.3f,
                new[] { "Skeleton", "Skeleton", "Skeleton Archer" }, 4);
            prop.SetLooks(whole.gameObject, spent.gameObject, null);
        }

        private void OdaCrate(Transform t, Vector3 p, int index)
        {
            ClearSite(Graveyard, p, 2.5f);
            Transform site = Holder(t, "OdaCrate", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            LocalBox(site, new Vector3(0f, 0.4f, 0f), new Vector3(1.3f, 0.8f, 0.9f), kit.Mat("ClothGreen"));
            LocalBox(site, new Vector3(0f, 0.4f, 0f), new Vector3(1.36f, 0.12f, 0.96f), kit.Mat("Wood"), solid: false);
            LocalBox(site, new Vector3(1.4f, 0.12f, 0.6f), new Vector3(0.1f, 0.25f, 1.2f), kit.Mat("Wood"), solid: false, euler: new Vector3(0f, 30f, 80f));
            Transform goods = Holder(site, "Goods", p, site.rotation);
            LocalBox(goods, new Vector3(0f, 0.86f, 0f), new Vector3(1.32f, 0.1f, 0.92f), kit.Mat("Wood"), solid: false);
            Glow(goods, p + Vector3.up * 1.2f, LanternLight, 3.5f, 1.4f);

            QuestProp prop = QuestProp.Attach(site.gameObject, "caravan", index, PropAction.Take, "Oda's Crate",
                "A crate painted green, stencilled with Oda's mark: a coin with a smile. The lid is nailed down tight - the dead have no use for silk and spices.",
                "Pry it open and take the goods", "Goods taken - something stirs in the dark!", 1.6f,
                new[] { "Grave Bat", "Grave Bat", "Corpse Ooze" }, 3);
            prop.SetLooks(goods.gameObject, null, null);
        }

        private void SupplyCache(Transform t, Vector3 p, int index)
        {
            ClearSite(Ruins, p, 2.5f);
            Transform site = Holder(t, "SupplyCache", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            LocalBox(site, new Vector3(0f, 0.06f, 0f), new Vector3(2f, 0.12f, 1.4f), kit.Mat("Wood"), solid: false);
            Transform whole = Holder(site, "Supplies", p, site.rotation);
            LocalBox(whole, new Vector3(-0.45f, 0.5f, 0f), new Vector3(0.8f, 0.8f, 0.8f), kit.Mat("Wood"));
            LocalBox(whole, new Vector3(0.45f, 0.42f, 0.1f), new Vector3(0.7f, 0.65f, 0.7f), kit.Mat("Wood"));
            LocalBox(whole, new Vector3(0f, 0.95f, 0f), new Vector3(1.8f, 0.06f, 1.1f), kit.Mat("ClothRed"), solid: false, euler: new Vector3(0f, 0f, 4f));
            Glow(whole, p + Vector3.up * 1.4f, LanternLight, 3.5f, 1.4f);
            Transform spent = Holder(site, "Emptied", p, site.rotation);
            LocalBox(spent, new Vector3(0.3f, 0.1f, 0.4f), new Vector3(1.6f, 0.05f, 1f), kit.Mat("ClothRed"), solid: false, euler: new Vector3(0f, 25f, 0f));

            QuestProp prop = QuestProp.Attach(site.gameObject, "supplies", index, PropAction.Take, "Emberwatch Cache",
                "Crates under a red cloth - the Emberwatch's colour - half buried in ash. Paw prints all round them, the size of your hand, and scorch marks where the hounds breathed on the wood.",
                "Gather the supplies", "Supplies gathered - hounds!", 1.6f,
                new[] { "Hellhound" }, 3);
            prop.SetLooks(whole.gameObject, spent.gameObject, null);
        }

        private void AshenShrine(Transform t, Vector3 p)
        {
            ClearSite(Ruins, p, 8f);
            Transform site = Holder(t, "AshenShrine", p, Quaternion.identity);
            Cyl(site, p + Vector3.up * 0.04f, 6.5f, 0.06f, kit.Mat("Ash"), solid: false);
            for (int k = 0; k < 24; k++)
            {
                float rad = k * Mathf.PI * 2f / 24f;
                Box(site, p + new Vector3(Mathf.Cos(rad), 0.08f, Mathf.Sin(rad)) * 6.5f, new Vector3(0.35f, 0.1f, 1.6f), kit.Mat("Ember"), solid: false,
                    euler: new Vector3(0f, -rad * Mathf.Rad2Deg, 0f));
            }
            for (int k = 0; k < 6; k++)
            {
                float rad = (k * 60f + 15f) * Mathf.Deg2Rad;
                Prefab(kit.pillar, site, p + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 7.6f, R(0f, 360f), new Vector3(0.6f, R(0.35f, 0.6f), 0.6f));
            }
            Cyl(site, p + Vector3.up * 0.45f, 0.8f, 0.9f, kit.Mat("Sandstone"));
            Ball(site, p + Vector3.up * 1f, 0.45f, kit.Mat("Ash"), flatten: 0.5f, solid: false);
            Light glow = QuestGlow(site, p + Vector3.up * 2f, new Color(1f, 0.45f, 0.15f), 12f, 3f);

            QuestProp prop = QuestProp.Attach(site.gameObject, "rite", 0, PropAction.Rite, "Ashen Shrine",
                "A ring of embers that never cool, round a stone bowl full of grey ash. The ash is warm. It shifts, slowly, as if something under it were breathing.\n\nYsolde's words come back to you: begin the rite, and do not leave the circle until the fire turns white.",
                "Begin the rite", "The ward is broken!", 2.4f,
                new[] { "Ember Knight", "Fire Caster", "Hellhound", "Raider" }, 0);
            prop.SetLooks(null, null, glow);
        }

        private void Sunstone(Transform t, Vector3 p, int index)
        {
            ClearSite(Ruins, p, 3f);
            Transform site = Holder(t, "Sunstone", p, Facing(p, Centers[Ruins]));
            LocalBox(site, new Vector3(0f, 0.25f, 0f), new Vector3(1.8f, 0.5f, 1.8f), kit.Mat("Sandstone"));
            LocalBox(site, new Vector3(0f, 1.8f, 0f), new Vector3(0.8f, 2.6f, 0.8f), kit.Mat("Sandstone"), euler: new Vector3(0f, 45f, 0f));
            Transform cold = Holder(site, "Cold", p, site.rotation);
            LocalBall(cold, new Vector3(0f, 3.5f, 0f), 0.55f, kit.Mat("RockDark"));
            Transform lit = Holder(site, "Lit", p, site.rotation);
            LocalBall(lit, new Vector3(0f, 3.5f, 0f), 0.6f, kit.Mat("Ember"));
            LocalBall(lit, new Vector3(0f, 3.6f, 0f), 0.35f, kit.Mat("Lantern"));
            Light glow = QuestGlow(site, p + Vector3.up * 4f, new Color(1f, 0.7f, 0.3f), 14f, 6f);

            QuestProp prop = QuestProp.Attach(site.gameObject, "sunstones", index, PropAction.Light, "Sunstone",
                "A sandstone pillar topped with a black stone sphere. Old sun-signs are carved round the base. Lay your palm on the sphere: it is cold as the grave - but deep inside, something answers, like an ember under ash.",
                "Wake the sunstone", "The sunstone blazes - and the fire draws beetles!", 4.2f,
                new[] { "Magma Beetle" }, 2);
            prop.SetLooks(cold.gameObject, lit.gameObject, glow);
        }

        // Ice over the Ruins' north gate (to the Frozen Hollow) until the sunstones burn it away.
        private void IceSeal(Transform t)
        {
            Transform gateTransform = root.Find("Gate_" + AreaNames[Ruins] + "_to_" + AreaNames[Frozen]);
            AreaGate gate = gateTransform != null ? gateTransform.GetComponent<AreaGate>() : null;
            if (gate == null)
                return;
            Vector3 g = gateTransform.position;
            g.y = 0f;
            Vector3 inward = Centers[Ruins] - g;
            inward.y = 0f;
            inward.Normalize();
            Vector3 at = g + inward * 1.5f;

            var wall = new GameObject("IceSeal").transform;
            wall.SetParent(t, false);
            wall.SetPositionAndRotation(at, Quaternion.LookRotation(inward));
            for (int k = -5; k <= 5; k++)
            {
                float h = 4.5f - Mathf.Abs(k) * 0.3f + R(-0.4f, 0.4f);
                LocalBox(wall, new Vector3(k * 1.15f, h * 0.5f, R(-0.3f, 0.3f)), new Vector3(1.3f, h, 1.2f), kit.Mat("Ice"),
                    euler: new Vector3(R(-6f, 6f), R(-15f, 15f), R(-6f, 6f)));
                IceSpike(wall, wall.TransformPoint(new Vector3(k * 1.15f + 0.4f, 0f, -0.9f)), R(1f, 2.4f), new Vector3(R(-20f, 20f), R(0f, 360f), R(-20f, 20f)));
            }
            Glow(wall, at + Vector3.up * 2.5f - inward * 0.5f, IceLight, 9f, 4f);
            QuestBarrier.Create(wall.gameObject, "sunstones", gate, "Sealed under ice that no fire of yours will melt", 6f);
        }

        private void FrozenScout(Transform t, Vector3 p, int index, string journal)
        {
            ClearSite(Frozen, p, 2.5f);
            Transform site = Holder(t, "FrozenScout", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            Body(site, kit.Mat("ClothRed"), arrows: false);
            LocalBox(site, new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.7f, 2.3f), kit.Mat("Ice"), solid: false, euler: new Vector3(0f, 4f, 0f));
            Transform book = Holder(site, "Journal", p, site.rotation);
            LocalBox(book, new Vector3(0.75f, 0.06f, 0.4f), new Vector3(0.32f, 0.08f, 0.42f), kit.Mat("Leather"), solid: false, euler: new Vector3(0f, 20f, 0f));
            Glow(book, p + Vector3.up * 0.8f, LanternLight, 3.5f, 1.6f);

            QuestProp prop = QuestProp.Attach(site.gameObject, "scouts", index, PropAction.Take, "Frozen Scout",
                "An Emberwatch scout, frozen into the snow, red cloak gone white with frost. A journal lies by one hand. The last page reads:\n\n<i>" + journal + "</i>",
                "Take the journal", "Journal taken - the cold stirs!", 1.6f,
                new[] { "Frost Wolf", "Ice Crawler" }, 2);
            prop.SetLooks(book.gameObject, null, null);
        }

        // The Shepherd's door, north-west of Rimeheart's throne: shut, warm, waiting.
        private void StagDoor(Transform t, Vector3 p)
        {
            ClearSite(Frozen, p, 8f);
            Transform site = Holder(t, "StagDoor", p, Quaternion.identity);
            // Steps up to it, two great posts and a lintel, the door itself.
            LocalBox(site, new Vector3(0f, 0.2f, -1.5f), new Vector3(10f, 0.4f, 3f), kit.Mat("RockDark"));
            for (int s = -1; s <= 1; s += 2)
                LocalBox(site, new Vector3(s * 4.2f, 4.5f, 0f), new Vector3(1.6f, 9f, 1.8f), kit.Mat("TombstoneDark"));
            LocalBox(site, new Vector3(0f, 9.4f, 0f), new Vector3(10.4f, 1.4f, 2f), kit.Mat("TombstoneDark"));
            GameObject slab = LocalBox(site, new Vector3(0f, 4.4f, 0.1f), new Vector3(6.8f, 8.8f, 1f), kit.Mat("Tombstone"));
            slab.AddComponent<ActBossDoor>();
            LocalBox(site, new Vector3(0f, 4.4f, -0.42f), new Vector3(0.1f, 8.6f, 0.1f), kit.Mat("RockDark"), solid: false);
            // Antlers carved over the lintel: a great branching rack, rimed with frost.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 foot = new Vector3(s * 1.5f, 9.95f, 0f);
                Vector3 fork = new Vector3(s * 2.5f, 11.8f, 0f);
                Vector3 crown = new Vector3(s * 3.3f, 13.5f, 0f);
                AntlerBranch(site, foot, fork, 0.38f);
                AntlerBranch(site, fork, crown, 0.3f);
                AntlerBranch(site, crown, new Vector3(s * 3.5f, 14.4f, 0f), 0.2f);
                AntlerBranch(site, Vector3.Lerp(foot, fork, 0.6f), new Vector3(s * 4.5f, 12.1f, 0f), 0.25f);
                AntlerBranch(site, fork, new Vector3(s * 1.6f, 13.2f, 0f), 0.23f);
                AntlerBranch(site, Vector3.Lerp(fork, crown, 0.75f), new Vector3(s * 4.4f, 13.9f, 0f), 0.2f);
            }
            // Runes down the seam, glowing a deep, warm red behind the frost.
            for (int k = 0; k < 6; k++)
            {
                GameObject rune = RuntimePrimitives.Create(PrimitiveType.Cube, site, new Color(1f, 0.35f, 0.2f));
                rune.transform.localPosition = new Vector3(k % 2 == 0 ? -0.7f : 0.7f, 1.6f + k * 1.2f, -0.42f);
                rune.transform.localScale = new Vector3(0.45f, 0.18f, 0.05f);
                rune.transform.localRotation = Quaternion.Euler(0f, 0f, k % 2 == 0 ? 25f : -25f);
            }
            Glow(site, p + new Vector3(0f, 4f, -2f), new Color(1f, 0.4f, 0.25f), 12f, 4f, flicker: true);
            // Ice heaped against the posts, clear of the door's face.
            for (int k = 0; k < 4; k++)
                IceSpike(site, p + new Vector3((k % 2 == 0 ? -1f : 1f) * R(5.2f, 6.5f), 0f, R(-2.5f, 0.5f)), R(1f, 2.6f), new Vector3(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)));

            // Read from the foot of the steps (its tag would be lost inside the stone).
            Transform reading = Holder(site, "Inscription", p + new Vector3(0f, 0f, -2.6f), Quaternion.identity);
            QuestProp prop = QuestProp.Attach(reading.gameObject, "door", 0, PropAction.Read, "Door of the Shepherd",
                "A door taller than a house, cut from one block of black stone. Antlers branch across the lintel, carved, rimed with frost. Yet the stone is warm under your hand, and from somewhere behind it comes a slow, enormous breath.\n\nWords are cut down the seam, in a script older than Haven:\n\n<i>HERE SLEEPS THE SHEPHERD, FATHER OF THE DEAD.\nTHREE WARDENS HOLD HIS DOOR: THE PRIEST, THE KING, THE QUEEN.\nWHEN THE LAST WARDEN FALLS, HE WAKES.</i>",
                "Copy the inscription", "Behind the door, something shifts in its sleep.", 3.2f);
            prop.SetLooks(null, null, null);
            // Rotate the whole finished site, including its inscription, light and ice dressing.
            site.rotation = SanctuaryDoorRotation;
        }

        private void AntlerBranch(Transform parent, Vector3 start, Vector3 end, float thickness)
        {
            Vector3 direction = end - start;
            // A slight overlap closes the joints and embeds each root into the lintel.
            GameObject branch = LocalBox(parent, (start + end) * 0.5f,
                new Vector3(thickness, direction.magnitude + thickness * 0.5f, thickness),
                kit.Mat("Bone"), solid: false);
            branch.name = "CarvedAntler";
            branch.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        }
    }
}
