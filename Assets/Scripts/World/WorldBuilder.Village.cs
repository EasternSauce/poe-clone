using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private Vector3 laundryWorkSpot, laundryBasket, laundryFacing, gardenWorkSpot, gardenFacing;
        private WindCloth[] villageLaundry;

        private sealed class VillageHome
        {
            public Vector3 Door, Lane, Work, Facing, Basket;
            public VillageActivity Activity = VillageActivity.Sweep;
            public WindCloth[] Laundry;
        }
        private readonly List<VillageHome> villageHomes = new List<VillageHome>();

        // A continuous ribbon avoids overlapping road cubes and their visible seams.
        // Authored bends are sampled smoothly; edges and width vary without rerolling routes.
        private void WindingPath(Transform parent, string name, int area, float width, Material material,
            params Vector3[] knots)
        {
            WindingPath(parent, name, area, width, material, 0f, knots);
        }

        // flare widens the start into a mouth (1 = twice as wide), easing back over a few metres,
        // so a lane opens onto a square instead of butting into it at full width.
        private void WindingPath(Transform parent, string name, int area, float width, Material material,
            float flare, params Vector3[] knots)
        {
            if (knots.Length < 2) return;
            var points = new List<Vector3>();
            for (int segment = 0; segment < knots.Length - 1; segment++)
            {
                Vector3 a = knots[Mathf.Max(0, segment - 1)];
                Vector3 b = knots[segment];
                Vector3 c = knots[segment + 1];
                Vector3 d = knots[Mathf.Min(knots.Length - 1, segment + 2)];
                int steps = Mathf.Max(4, Mathf.CeilToInt(Vector3.Distance(b, c) / 0.8f));
                for (int step = 0; step < steps; step++)
                {
                    float u = (float)step / steps;
                    points.Add(0.5f * ((2f * b) + (-a + c) * u +
                        (2f * a - 5f * b + 4f * c - d) * u * u +
                        (-a + 3f * b - 3f * c + d) * u * u * u));
                }
            }
            points.Add(knots[knots.Length - 1]);
            var vertices = new Vector3[points.Count * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            AreaShape shape = Shape(area);
            Vector3 center = Center(area);
            float distance = 0f;
            bool previousInside = false;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
                Vector3 tangent = points[Mathf.Min(points.Count - 1, i + 1)] - points[Mathf.Max(0, i - 1)];
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                // Each edge wanders on its own noise, so the verge looks trodden rather than ruled.
                float half = width * 0.5f * (1f + 0.09f * Mathf.Sin(distance * 0.47f))
                    * (1f + flare * Mathf.Exp(-distance / 3f));
                float left = half * (1f + 0.22f * (Mathf.PerlinNoise(distance * 0.55f, width * 7.3f) - 0.5f));
                float right = half * (1f + 0.22f * (Mathf.PerlinNoise(distance * 0.55f + 41f, width * 7.3f) - 0.5f));
                vertices[i * 2] = points[i] + side * left + Vector3.up * 0.035f;
                vertices[i * 2 + 1] = points[i] - side * right + Vector3.up * 0.035f;
                uv[i * 2] = new Vector2(0, distance / width);
                uv[i * 2 + 1] = new Vector2(1, distance / width);
                bool inside = shape.Contains(center + vertices[i * 2]) &&
                    shape.Contains(center + vertices[i * 2 + 1]);
                if (i > 0 && inside && previousInside)
                {
                    int v = i * 2;
                    triangles.Add(v - 2); triangles.Add(v - 1); triangles.Add(v);
                    triangles.Add(v); triangles.Add(v - 1); triangles.Add(v + 1);
                }
                previousInside = inside;
                if (inside) Claim(center + points[i], half + 1.1f);
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var road = new GameObject(name);
            road.transform.SetParent(parent, false);
            road.transform.position = center;
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = road.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void BuildVillageHomes(Transform parent)
        {
            // Position, lane-facing point: compact square, then several small neighbourhoods.
            villageHomes.Clear();
            Vector3[] homes =
            {
                Flat(-15, 17), Flat(12, 19), Flat(-17, -24), Flat(18, -34),
                Flat(-36, 17), Flat(-43, -14), Flat(-65, -2), Flat(-83, 41),
                Flat(-25, -48), Flat(8, -61), Flat(-35, -79),
                Flat(-7, 43), Flat(25, 38), Flat(47, 54),
                Flat(39, 16), Flat(54, -24), Flat(77, 16), Flat(105, -13)
            };
            Vector3[] lanes =
            {
                Flat(-4, 12), Flat(1, 14), Flat(-3, -24), Flat(4, -39),
                Flat(-32, 5), Flat(-43, 0), Flat(-63, 12), Flat(-83, 24),
                Flat(-5, -52), Flat(-11, -62), Flat(-20, -79),
                Flat(8, 41), Flat(17, 47), Flat(37, 64),
                Flat(36, -2), Flat(54, -8), Flat(77, 0), Flat(105, 7)
            };
            Vector3 center = Center(Haven);
            for (int i = 0; i < homes.Length; i++)
            {
                Vector3 p = center + homes[i];
                float scale = i < 4 ? 1.5f : 1.25f + (i % 3) * 0.12f;
                GameObject house = Prefab(kit.house, parent, p, 0, Vector3.one * scale);
                house.name = "VillageCottage_" + (i + 1);
                // The prefab's door is on local -Z, so face its back away from the lane.
                house.transform.rotation = Quaternion.LookRotation(homes[i] - lanes[i]);
                Vector3 doorstep = house.transform.TransformPoint(new Vector3(0, 0, -2f));
                Vector3 approach = lanes[i] + (doorstep - center - lanes[i]) * 0.5f;
                approach += Vector3.Cross(Vector3.up, homes[i] - lanes[i]).normalized * 0.7f;
                WindingPath(parent, "CottageFootpath_" + (i + 1), Haven, 1.5f, DirtFloor, 0.8f,
                    lanes[i], approach, doorstep - center);
                Claim(p, 7.5f);
                Lamp(parent, doorstep + house.transform.right * 2.8f);

                // Timber-framed fronts and small porches give the shared prefab a village scale.
                LocalBox(house.transform, new Vector3(0, 2.3f, -1.76f), new Vector3(4.1f, 0.13f, 0.12f), kit.Mat("Wood"), false);
                for (int side = -1; side <= 1; side += 2)
                    LocalBox(house.transform, new Vector3(side * 1.9f, 1.2f, -1.76f), new Vector3(0.14f, 2.4f, 0.12f), kit.Mat("Wood"), false);
                LocalBox(house.transform, new Vector3(0, 1.95f, -2.1f), new Vector3(1.8f, 0.12f, 1f),
                    kit.Mat(i % 3 == 0 ? "ClothRed" : "Roof"), false, new Vector3(-8, 0, 0));
                LocalCyl(house.transform, new Vector3(2.7f, 0.45f, 0), 0.35f, 0.9f, kit.Mat("Wood"));
                var home = new VillageHome
                {
                    Door = doorstep - house.transform.forward * 1.5f,
                    Lane = center + lanes[i],
                    Work = doorstep - house.transform.forward * 1.5f,
                    Facing = -house.transform.forward
                };
                CottageDetails(house.transform, i, home);
                // Laundry and vegetable beds need separate yards.
                if (i % 2 == 0 && home.Laundry == null) VillageGarden(parent, house.transform, home);
                // Nella and Lio already use the first garden and washing line.
                if (i < 2)
                {
                    home.Activity = VillageActivity.Sweep;
                    home.Work = home.Door;
                    home.Facing = -house.transform.forward;
                }
                villageHomes.Add(home);
                Claim(home.Work, 1.5f);
                Claim(home.Door, 1.5f);
            }
        }

        private void VillageGarden(Transform parent, Transform house, VillageHome home)
        {
            var yard = new GameObject("KitchenGarden").transform;
            yard.SetParent(parent, false);
            yard.SetPositionAndRotation(house.TransformPoint(new Vector3(0, 0, 4.8f)), house.rotation);
            LocalBox(yard, new Vector3(0, 0.025f, 0), new Vector3(4.8f, 0.05f, 3.4f), DirtFloor, false);
            for (int row = -1; row <= 1; row++)
                for (int plant = -2; plant <= 2; plant++)
                {
                    Vector3 p = yard.TransformPoint(new Vector3(plant * 0.8f, 0.18f, row * 0.9f));
                    Ball(yard, p, 0.24f, kit.Mat("ClothGreen"), flatten: 0.65f, solid: false);
                }
            // Three sides enclosed; the side towards the cottage stays open.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int z = -1; z <= 1; z++)
                    LocalBox(yard, new Vector3(side * 2.7f, 0.5f, z * 1.8f), new Vector3(0.15f, 1f, 0.15f), kit.Mat("Wood"));
                LocalBox(yard, new Vector3(side * 2.7f, 0.65f, 0), new Vector3(0.1f, 0.14f, 3.8f), kit.Mat("Wood"));
            }
            LocalBox(yard, new Vector3(0, 0.65f, 1.8f), new Vector3(5.5f, 0.14f, 0.1f), kit.Mat("Wood"));
            Claim(yard.position, 4f);
            home.Activity = VillageActivity.Garden;
            home.Work = yard.TransformPoint(new Vector3(0, 0, -1.3f));
            home.Facing = yard.forward;
            if (gardenWorkSpot == Vector3.zero)
            {
                gardenWorkSpot = yard.TransformPoint(new Vector3(0, 0, -1.3f));
                gardenFacing = yard.forward;
            }
        }

        private void CottageDetails(Transform house, int index, VillageHome home)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(house, new Vector3(side * 1.2f, 1.08f, -1.95f), new Vector3(0.85f, 0.22f, 0.36f), kit.Mat("Wood"), false);
                for (int flower = -1; flower <= 1; flower++)
                {
                    Vector3 p = house.TransformPoint(new Vector3(side * 1.2f + flower * 0.23f, 1.27f, -1.96f));
                    Ball(house, p, 0.09f * house.localScale.x, kit.Mat(index % 2 == 0 ? "ClothYellow" : "ClothRed"), solid: false);
                }
            }
            var woodpile = new GameObject("StackedFirewood").transform;
            woodpile.SetParent(house, false);
            woodpile.localPosition = new Vector3(-2.5f, 0, 0.4f);
            for (int row = 0; row < 2; row++)
                for (int log = 0; log < 3 - row; log++)
                {
                    GameObject piece = LocalCyl(woodpile, new Vector3((log - 1f + row * 0.5f) * 0.3f, 0.16f + row * 0.28f, 0),
                        0.15f, 1.2f, kit.Mat("Bark"), false);
                    piece.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
            if (index % 3 != 1) return;
            var laundry = new GameObject("LaundryLine").transform;
            laundry.SetParent(house, false);
            laundry.localPosition = new Vector3(0, 0, 4.6f);
            for (int side = -1; side <= 1; side += 2)
                LocalBox(laundry, new Vector3(side * 2.4f, 1.05f, 0), new Vector3(0.12f, 2.1f, 0.12f), kit.Mat("Wood"));
            LocalBox(laundry, new Vector3(0, 2f, 0), new Vector3(4.8f, 0.035f, 0.035f), kit.Mat("Wood"), false);
            var clothes = new WindCloth[3];
            for (int cloth = -1; cloth <= 1; cloth++)
                clothes[cloth + 1] = WindCloth.Create(laundry, new Vector3(cloth * 1.25f, 1.95f, 0), 0.8f, 0.9f,
                    kit.Mat(cloth == 0 ? "ClothYellow" : "ClothBlue"));
            LocalCyl(laundry, new Vector3(1.5f, 0.22f, -1.1f), 0.42f, 0.44f, kit.Mat("Wood"), false);
            home.Activity = VillageActivity.Laundry;
            home.Laundry = clothes;
            home.Work = laundry.TransformPoint(new Vector3(0, 0, -0.85f));
            home.Basket = laundry.TransformPoint(new Vector3(1.5f, 0.45f, -1.1f));
            home.Facing = laundry.forward;
            if (index == 1)
            {
                villageLaundry = clothes;
                laundryWorkSpot = laundry.TransformPoint(new Vector3(0, 0, -0.85f));
                laundryBasket = laundry.TransformPoint(new Vector3(1.5f, 0.45f, -1.1f));
                laundryFacing = laundry.forward;
            }
            Claim(laundry.position, 4f);
        }

        private void BuildVillageCommons(Transform parent)
        {
            Vector3 center = Center(Haven);
            var board = new GameObject("VillageNoticeboard").transform;
            board.SetParent(parent, false);
            // Inside the square's north-west edge, notices (local -Z) facing the well.
            Vector3 outward = Flat(-8, 9).normalized;
            board.SetPositionAndRotation(center + outward * 9f, Quaternion.LookRotation(outward));
            Claim(board.position, 2f);
            for (int side = -1; side <= 1; side += 2)
                LocalBox(board, new Vector3(side * 1.1f, 1.1f, 0), new Vector3(0.16f, 2.2f, 0.16f), kit.Mat("Wood"));
            LocalBox(board, new Vector3(0, 1.65f, 0), new Vector3(2.6f, 1.2f, 0.18f), kit.Mat("Wood"));
            LocalBox(board, new Vector3(0, 2.35f, 0), new Vector3(3, 0.15f, 0.8f), kit.Mat("Roof"), false);
            for (int note = -1; note <= 1; note++)
                LocalBox(board, new Vector3(note * 0.65f, 1.65f + note * 0.1f, -0.105f), new Vector3(0.45f, 0.65f, 0.025f), kit.Mat("Bone"), false,
                    new Vector3(0, 0, note * 7));

            Vector3 fire = center + Flat(-27, -12);
            var nook = new GameObject("VillageHearth").transform;
            nook.SetParent(parent, false);
            nook.position = fire;
            FloorDisc(nook, "HearthEarth", fire + Vector3.up * 0.06f, 5f, DirtFloor);
            for (int stone = 0; stone < 10; stone++)
            {
                float angle = stone * Mathf.PI / 5f;
                Ball(nook, fire + new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)) * 1.15f,
                    0.3f, kit.Mat("Stone"), flatten: 0.7f);
            }
            Box(nook, fire + Vector3.up * 0.18f, new Vector3(1.6f, 0.25f, 0.25f), kit.Mat("Bark"), false, new Vector3(0, 35, 0));
            Box(nook, fire + Vector3.up * 0.32f, new Vector3(1.6f, 0.25f, 0.25f), kit.Mat("Bark"), false, new Vector3(0, -35, 0));
            Flame(nook, fire + Vector3.up * 0.55f, 0.48f, kit.Mat("Ember"), flatten: 1.4f);
            Glow(nook, fire + Vector3.up * 1.1f, new Color(1f, 0.59f, 0.26f), 9, 3, true);
            // Social seating is separate from the starter-loot benches.
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(nook, new Vector3(0, 0.55f, side * 3.4f), new Vector3(3.8f, 0.18f, 0.7f), kit.Mat("Wood"));
                for (int leg = -1; leg <= 1; leg += 2)
                    LocalBox(nook, new Vector3(leg * 1.4f, 0.25f, side * 3.4f), new Vector3(0.2f, 0.5f, 0.6f), kit.Mat("Wood"));
            }
            Claim(fire, 6f);

            var cart = new GameObject("ProduceHandcart").transform;
            cart.SetParent(parent, false);
            cart.SetPositionAndRotation(center + Flat(91, -5), Quaternion.Euler(0, -20, 0));
            LocalBox(cart, new Vector3(0, 0.8f, 0), new Vector3(2, 0.18f, 2.8f), kit.Mat("Wood"));
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(cart, new Vector3(side, 1.05f, 0), new Vector3(0.12f, 0.5f, 2.8f), kit.Mat("Wood"));
                LocalBox(cart, new Vector3(side * 0.7f, 0.65f, -2), new Vector3(0.12f, 0.12f, 2), kit.Mat("Wood"));
                GameObject wheel = LocalCyl(cart, new Vector3(side * 1.15f, 0.6f, 0.3f), 0.6f, 0.16f, kit.Mat("Wood"));
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            for (int sack = -1; sack <= 1; sack++)
                Ball(cart, cart.TransformPoint(new Vector3(sack * 0.6f, 1.18f, sack * 0.5f)), 0.4f, kit.Mat("Pumpkin"), solid: false);
            Claim(cart.position, 4f);
        }

        private void BuildVillageFolk(GameObject prefab, Transform parent)
        {
            BuildCottageResidents(prefab, parent);
            VillagePerson(prefab, parent, "Baker Tessa", Flat(-9, -17), new Color(0.72f, 0.45f, 0.29f),
                "You're just in time for the smell of the morning loaves. Bram says it reaches his forge before I do.",
                "Ask about her baking", "We keep a little of each batch for whoever comes through the gate hungry. Oda calls it bad business. Funny how she's always first in the queue.");
            VillagePerson(prefab, parent, "Gardener Nella", Flat(-21, 12), new Color(0.36f, 0.52f, 0.27f),
                "Mind the flowers, dear. The yellow ones survived the frost, so they've earned their place.",
                "Ask about the gardens", "Every cottage grows something different. We swap beans for onions and onions for gossip. The gossip grows fastest.");
            VillagePerson(prefab, parent, "Woodcutter Fen", Flat(-31, -12), new Color(0.49f, 0.31f, 0.21f),
                "Come warm your hands. I've split enough wood to keep this fire going till the stars come out.",
                "Ask about village evenings", "We used to hurry indoors at sunset. Now someone brings a kettle, someone brings a story, and we stay a little longer. Makes the dark seem farther away.");
            VillagePerson(prefab, parent, "Seamstress Lio", Flat(21, 31), new Color(0.35f, 0.43f, 0.67f),
                "Good drying weather today. If the wind steals another shirt, Captain Hale can arrest it.",
                "Ask about the washing", "That blue cloth was a travelling cloak once. Then a curtain, now three aprons. Here in Haven, nothing's worn out until it has had at least three lives.");
            VillagePerson(prefab, parent, "Carter Dain", Flat(95, -5), new Color(0.55f, 0.49f, 0.26f),
                "The cart's wheel squeaks, but it still gets the pumpkins home. That's more than I can say for my knees.",
                "Ask about the lanes", "These bends follow old trees and old neighbours. You could lay a straight road, I suppose, but then you'd miss Nella's flowers and Tessa's kitchen window.");
            VillagePerson(prefab, parent, "Storyteller Edda", Flat(-23, -14), new Color(0.57f, 0.34f, 0.51f),
                "A new face! Sit by the fire. I've a story short enough to tell before the kettle boils.",
                "Hear a village story", "When the well first ran dry, everyone brought one cup of water from home. By dusk it was full again. Fen says that isn't how wells work. I say that's how villages work.");
        }

        private void BuildCottageResidents(GameObject prefab, Transform parent)
        {
            string[] names = { "Mira", "Corin", "Hett", "Oswin", "Sera", "Tobin", "Ada", "Wren", "Perrin",
                "Maud", "Ivo", "Orla", "Rowan", "Bess", "Galen", "Elsie", "Ansel", "Faye" };
            Color[] clothes = { new Color(0.43f, 0.53f, 0.32f), new Color(0.57f, 0.37f, 0.29f),
                new Color(0.36f, 0.44f, 0.62f), new Color(0.60f, 0.49f, 0.30f), new Color(0.51f, 0.35f, 0.53f) };
            for (int i = 0; i < villageHomes.Count; i++)
            {
                VillageHome home = villageHomes[i];
                string greeting = home.Activity == VillageActivity.Garden ? "A little water now, a full pot come autumn." :
                    home.Activity == VillageActivity.Laundry ? "One more shirt, then I can take a turn down the lane." :
                    "The leaves always find their way back to my doorstep.";
                VillagePerson(prefab, parent, names[i], home.Work - Center(Haven), clothes[i % clothes.Length],
                    greeting, "Ask about this neighborhood",
                    "We look after these cottages together. A spare onion, a mended sleeve, a swept path for an old neighbour. There's always something to do, even this far from the square.", home);
            }
        }

        private void VillagePerson(GameObject prefab, Transform parent, string name, Vector3 local, Color cloth,
            string greeting, string topic, string story, VillageHome home = null)
        {
            Vector3 p = Center(Haven) + local;
            var look = new PoeClone.Enemies.EnemyKind
            {
                Name = "Villager", Scale = 0.96f, HideHorns = true, Cloth = cloth,
                Pants = new Color(0.29f, 0.25f, 0.22f), Skin = new Color(0.73f, 0.56f, 0.43f),
                Eyes = new Color(0.13f, 0.11f, 0.09f)
            };
            Npc npc = Npc.Create(prefab, NpcRole.Villager, name, look, p + Vector3.up * 1.1f * look.Scale,
                -local.normalized, parent);
            npc.SetConversation(greeting, topic, story);
            VillageActivity activity = name.Contains("Tessa") ? VillageActivity.Sweep :
                name.Contains("Nella") ? VillageActivity.Garden : name.Contains("Fen") ? VillageActivity.WarmHands :
                name.Contains("Lio") ? VillageActivity.Laundry : name.Contains("Dain") ? VillageActivity.Produce : VillageActivity.Chat;
            if (home != null) activity = home.Activity;
            Vector3 work = activity == VillageActivity.Laundry ? laundryWorkSpot :
                activity == VillageActivity.Garden ? gardenWorkSpot : p;
            Vector3 face = activity == VillageActivity.Laundry ? laundryFacing :
                activity == VillageActivity.Garden ? gardenFacing :
                activity == VillageActivity.WarmHands ? (Center(Haven) + Flat(-27, -12) - p).normalized :
                activity == VillageActivity.Produce ? (Center(Haven) + Flat(91, -5) - p).normalized : -local.normalized;
            if (home != null) { work = home.Work; face = home.Facing; }
            var walk = npc.GetComponentInChildren<CharacterWalkAnimator>();
            GameObject tool = null;
            if (walk != null && walk.WorkHand != null && (activity == VillageActivity.Sweep || activity == VillageActivity.Garden))
            {
                tool = new GameObject(activity == VillageActivity.Sweep ? "SweepingBroom" : "WateringCan");
                tool.transform.SetParent(walk.WorkHand, false);
                tool.transform.localPosition = new Vector3(0, -0.45f, 0);
                if (activity == VillageActivity.Sweep)
                {
                    // Ground-based broom motion drives both hands, rather than
                    // inheriting one forearm's up/down rotation.
                    tool.transform.SetParent(walk.transform, false);
                    tool.transform.localPosition = new Vector3(0, 0.02f, 0.62f);
                    LocalBox(tool.transform, new Vector3(0, 0.95f, 0), new Vector3(0.055f, 1.4f, 0.055f), kit.Mat("Wood"), false);
                    LocalBox(tool.transform, new Vector3(0, 0.125f, 0), new Vector3(0.38f, 0.25f, 0.13f), kit.Mat("Tan"), false);
                    walk.BindSweepingBroom(tool.transform);
                }
                else
                {
                    LocalCyl(tool.transform, new Vector3(0, -0.1f, 0), 0.19f, 0.32f, kit.Mat("Iron"), false);
                    LocalBox(tool.transform, new Vector3(0, 0, 0.28f), new Vector3(0.08f, 0.08f, 0.4f), kit.Mat("Iron"), false, new Vector3(-20, 0, 0));
                }
                tool.SetActive(false);
            }
            Vector3 social = Center(Haven) + (activity == VillageActivity.Produce ? Flat(84, 5) :
                activity == VillageActivity.Laundry ? Flat(21, 31) :
                activity == VillageActivity.Garden ? Flat(-21, 12) :
                activity == VillageActivity.Sweep ? Flat(-8, 6) :
                activity == VillageActivity.WarmHands ? Flat(-35, -5) : Flat(-20, -8));
            work.y = p.y; social.y = p.y;
            var routine = npc.gameObject.AddComponent<VillageRoutine>();
            // Cottage residents stay in their own neighborhoods rather than converging on the square.
            if (home != null)
                routine.Configure(Shape(Haven), activity, face, tool, work, home.Lane, home.Door);
            else
                routine.Configure(Shape(Haven), activity, face, tool, work, social, p);
            if (activity == VillageActivity.Laundry)
                routine.BindLaundry(home != null ? home.Laundry : villageLaundry, home != null ? home.Basket : laundryBasket);
        }

        private void BuildSmithWorkshop(Transform parent, Vector3 p)
        {
            var workshop = new GameObject("SmithWorkshop").transform;
            workshop.SetParent(parent, false);
            workshop.position = p;
            LocalBox(workshop, new Vector3(0, 0.03f, 0), new Vector3(7, 0.06f, 5), kit.Mat("Stone"), false);
            LocalBox(workshop, new Vector3(0, 1.5f, -2.3f), new Vector3(7, 3, 0.3f), kit.Mat("Wall"));
            for (int side = -1; side <= 1; side += 2)
                LocalBox(workshop, new Vector3(side * 3.2f, 1.5f, 2), new Vector3(0.22f, 3, 0.22f), kit.Mat("Wood"));
            LocalBox(workshop, new Vector3(0, 3.2f, 0), new Vector3(7.8f, 0.25f, 5.8f), kit.Mat("Roof"), false, new Vector3(9, 0, 0));
            LocalBox(workshop, new Vector3(2, 0.65f, -0.9f), new Vector3(1.5f, 1.3f, 1.3f), kit.Mat("Stone"));
            LocalBox(workshop, new Vector3(2, 1.35f, -0.9f), new Vector3(1.1f, 0.12f, 0.9f), kit.Mat("Ember"), false);
            LocalBox(workshop, new Vector3(-1.3f, 0.55f, 0), new Vector3(0.6f, 1.1f, 0.6f), kit.Mat("Wood"));
            LocalBox(workshop, new Vector3(-1.3f, 1.15f, 0), new Vector3(1.6f, 0.3f, 0.65f), kit.Mat("Iron"));
            Flame(workshop, p + new Vector3(2, 1.58f, -0.9f), 0.25f, kit.Mat("Ember"), 1.1f, 0.25f);
            Glow(workshop, p + new Vector3(2, 1.7f, -0.9f), FireLight, 6, 2, true);
            Claim(p, 5f);
        }
    }
}
