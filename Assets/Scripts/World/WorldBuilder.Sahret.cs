using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildSahret()
        {
            Begin(Sahret, 9921);
            Transform city = Group("Sahret - Cistern Quarter");
            Vector3 c = Center(Sahret);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(city, c + new Vector3(side * 22, 4, -28), new Vector3(24, 8, 4), desertStone);
                DesertPillar(city, c + new Vector3(side * 9, 0, -28), 10);
                Brazier(city, c + new Vector3(side * 7, 0, -24));
                WindBanner(city, Sahret, new Vector3(side * 10, 0, -25), side * 12, "ClothBlue", true);
            }

            // Low paving leaves the arrival, waystone and both sides of the cistern open.
            for (int z = -22; z <= 24; z += 3)
                for (int x = -9; x <= 9; x += 3)
                {
                    if (z > 2 && z < 15 && Mathf.Abs(x) < 6) continue;
                    WeatheredSlab(city, "CisternCourtPaving", c + new Vector3(x, 0.02f, z),
                        new Vector2(1.35f, 1.35f), 0.035f, desertStone, R(-4, 4), false);
                }

            // A communal water supply is the visual centre of the surviving quarter.
            Transform cistern = Holder(city, "CommunalCistern", c + new Vector3(0, 0, 9), Quaternion.identity);
            LocalCyl(cistern, new Vector3(0, 0.24f, 0), 4.2f, 0.48f, desertStone);
            Transform water = LocalCyl(cistern, new Vector3(0, 0.5f, 0), 3.5f, 0.08f, kit.Mat("Water"), false).transform;
            cistern.gameObject.AddComponent<SahretWaterSupply>().ConfigureCistern(water);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                LocalBox(cistern, new Vector3(Mathf.Sin(angle) * 3.8f, 0.65f, Mathf.Cos(angle) * 3.8f),
                    new Vector3(1.5f, 0.75f, 0.65f), desertStone, euler: new Vector3(0, i * 22.5f, 0));
            }
            LocalCyl(cistern, new Vector3(0, 1.15f, 0), 0.55f, 1.7f, desertStone);
            LocalCyl(cistern, new Vector3(0, 2.1f, 0), 1.2f, 0.25f, desertStone);
            SahretJar(city, c + new Vector3(5, 0, 7), 0.65f);
            SahretJar(city, c + new Vector3(5.8f, 0, 8), 0.45f);

            // Flat roofs, inset doors and blue shutters distinguish this town from Haven.
            SahretHouse(city, c + new Vector3(-28, 0, 18), 90, 5.8f);
            SahretHouse(city, c + new Vector3(-15, 0, 27), 180, 4.8f);
            SahretHouse(city, c + new Vector3(0, 0, 28), 180, 7.2f);
            SahretHouse(city, c + new Vector3(16, 0, 27), 180, 5.5f);
            SahretHouse(city, c + new Vector3(29, 0, 13), -90, 5.2f);

            // A narrow lane between the northern homes opens onto the basin road.
            for (int z = 21; z <= 39; z += 3)
                WeatheredSlab(city, "NorthGateLane", c + new Vector3(8, 0.02f, z),
                    new Vector2(1.3f, 1.35f), 0.035f, desertStone, R(-4, 4), false);
            for (int side = -1; side <= 1; side += 2)
            {
                DesertPillar(city, c + new Vector3(side * 8, 0, 47), 6);
                WindBanner(city, Sahret, new Vector3(side * 9, 0, 45), side * 12, "ClothBlue", true);
            }

            Stall(city, c + new Vector3(-18, 0, -8), c, kit.Mat("ClothBlue"));
            Stall(city, c + new Vector3(-20, 0, 1), c + new Vector3(0, 0, 1), kit.Mat("ClothYellow"));
            Stall(city, c + new Vector3(-18, 0, 12), c + new Vector3(0, 0, 12), kit.Mat("ClothRed"));
            for (int i = 0; i < 5; i++)
                SahretJar(city, c + new Vector3(-24 + i * 0.8f, 0, -15 + i % 2), R(0.35f, 0.6f));
            Crate(city, c + new Vector3(-26, 0, -9), 12);
            Crate(city, c + new Vector3(-26, 0.8f, -9), -8);
            Barrel(city, c + new Vector3(-25, 0, -7));

            Transform forge = Holder(city, "OpenAirForge", c + new Vector3(22, 0, -6), Quaternion.identity);
            LocalBox(forge, new Vector3(3, 1.1f, 0), new Vector3(3, 2.2f, 3), desertStone);
            LocalBox(forge, new Vector3(3, 1.1f, -1.52f), new Vector3(1.9f, 1.25f, 0.05f), kit.Mat("RockDark"), false);
            LocalBox(forge, new Vector3(3, 3.7f, 0.7f), new Vector3(1.3f, 3, 1.3f), desertStone);
            Brazier(forge, forge.position + new Vector3(3, 0, -1.9f));
            LocalCyl(forge, new Vector3(-1, 0.55f, 0), 0.65f, 1.1f, kit.Mat("Wood"));
            LocalBox(forge, new Vector3(-1, 1.25f, 0), new Vector3(1.6f, 0.35f, 0.65f), kit.Mat("Iron"));
            LocalBox(forge, new Vector3(-1, 1.52f, 0), new Vector3(2.1f, 0.2f, 0.8f), kit.Mat("Iron"));
            Barrel(city, c + new Vector3(27, 0, -10));
            Crate(city, c + new Vector3(21, 0, -11), 6);

            // A shaded travellers' corner; its furniture never seeds Haven's starter gear.
            Transform camp = Holder(city, "TravellersShade", c + new Vector3(19, 0, 15), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
                LocalCyl(camp, new Vector3(side * 4, 1.9f, 0), 0.09f, 3.8f, kit.Mat("Wood"));
            WindCloth.Create(camp, new Vector3(0, 3.7f, 0), 8, 3.2f, kit.Mat("ClothBlue"))
                .transform.localRotation = Quaternion.Euler(-85, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                LocalBox(camp, new Vector3(-2 + i * 2, 0.16f, 1), new Vector3(1.4f, 0.3f, 2.6f), kit.Mat("ClothYellow"), false);
                LocalCyl(camp, new Vector3(-2 + i * 2, 0.4f, 2), 0.28f, 1.4f, kit.Mat("ClothRed"), false)
                    .transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            SahretSeat(city, c + new Vector3(8, 0, 17), 90);
            SahretSeat(city, c + new Vector3(-8, 0, 17), -90);
            Brazier(city, c + new Vector3(11, 0, 21));
            BuildStashChest(city, c + new Vector3(9, 0, -8), c + new Vector3(0, 0, -8));

            EnemySpawner source = FindAnyObjectByType<EnemySpawner>();
            if (source == null || source.EnemyPrefab == null) return;
            SahretResident(source.EnemyPrefab, city, "Waterkeeper Nima", new Vector3(6, 0, 11), "ClothBlue",
                "Drink slowly, traveller. This water has farther to go than either of us.",
                "Ask about the cistern", "The old channels still reach the wells beneath the sand. We clear them every dawn. A city can lose its kings and keep living, but never its water.", NpcRole.Waterkeeper);
            SahretResident(source.EnemyPrefab, city, "Caravaner Sef", new Vector3(-15, 0, -7), "ClothYellow",
                "Shade, water, a wall at your back. After the Expanse, that is wealth.",
                "Ask about the market", "Most of these stalls came here on wagons. The road grew dangerous, so the wagons stayed. Now we trade what we can mend, carry or grow.");
            SahretResident(source.EnemyPrefab, city, "Smith Damar", new Vector3(20, 0, -4), "ClothRed",
                "Sand gets into every joint. Leave a tool alone for a day and the desert claims it.",
                "Ask about the forge", "The chimney was a watchtower once. I took the fallen stones and built a hearth. Sahret has enough monuments; it needs things that work.");
            SahretResident(source.EnemyPrefab, city, "Gatewatch Rasha", new Vector3(5, 0, -21), "ClothBlue",
                "Welcome to Sahret. Keep the gate clear; someone else may still be coming in.",
                "Ask about the quarter", "This is the Cistern Quarter. Families live above the market, travellers sleep under the blue awning. Out there the wind takes everything. Here we hold on together.");
            SahretResident(source.EnemyPrefab, city, "Weaver Ilyet", new Vector3(17, 0, 13), "ClothGreen",
                "Sit in the shade a while. I can finish this seam without you standing over me.",
                "Ask about the blue cloth", "Blue for water, yellow for the road, red for a hearth that is still burning. Even a stranger can read our quarter from the gate.");
            BuildSahretRoutines(source.EnemyPrefab, city);
        }

        private void SahretHouse(Transform city, Vector3 at, float yaw, float height)
        {
            Transform house = Holder(city, "SandstoneCourtyardHouse", at, Quaternion.Euler(0, yaw, 0));
            LocalBox(house, new Vector3(0, height * 0.5f, 0), new Vector3(9, height, 7), desertStone);
            LocalBox(house, new Vector3(0, height, 0), new Vector3(9.5f, 0.35f, 7.5f), desertStone);
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(house, new Vector3(side * 4.5f, height + 0.6f, 0), new Vector3(0.45f, 1, 7.5f), desertStone);
                LocalBox(house, new Vector3(0, height + 0.6f, side * 3.5f), new Vector3(9, 1, 0.45f), desertStone);
                LocalBox(house, new Vector3(side * 2.8f, height * 0.62f, 3.52f), new Vector3(1.3f, 1.5f, 0.1f), kit.Mat("ClothBlue"), false);
                for (int slat = -1; slat <= 1; slat++)
                    LocalBox(house, new Vector3(side * 2.8f + slat * 0.4f, height * 0.62f, 3.59f), new Vector3(0.06f, 1.5f, 0.06f), kit.Mat("Wood"), false);
            }
            LocalBox(house, new Vector3(0, 1.35f, 3.52f), new Vector3(1.7f, 2.7f, 0.12f), kit.Mat("Wood"), false);
            LocalBox(house, new Vector3(0, 2.9f, 3.65f), new Vector3(2.3f, 0.4f, 0.4f), desertStone, false);
            WindCloth.Create(house, new Vector3(0, 3.25f, 3.7f), 3.2f, 1.2f, kit.Mat("ClothYellow"), true)
                .transform.localRotation = Quaternion.Euler(-70, 0, 0);
            SahretJar(house, house.TransformPoint(new Vector3(-3.5f, 0, 4.3f)), 0.5f);
        }

        private void SahretJar(Transform parent, Vector3 at, float radius)
        {
            Transform jar = Holder(parent, "WaterJar", at, Quaternion.identity);
            LocalBall(jar, Vector3.up * radius, radius, kit.Mat("Roof"));
            LocalCyl(jar, Vector3.up * radius * 1.9f, radius * 0.45f, radius * 0.45f, kit.Mat("Roof"), false);
            LocalCyl(jar, Vector3.up * radius * 2.14f, radius * 0.3f, 0.025f, kit.Mat("RockDark"), false);
        }

        private void SahretSeat(Transform parent, Vector3 at, float yaw)
        {
            Transform seat = Holder(parent, "CisternStoneSeat", at, Quaternion.Euler(0, yaw, 0));
            LocalBox(seat, new Vector3(0, 0.3f, 0), new Vector3(3, 0.6f, 0.8f), desertStone);
            LocalBox(seat, new Vector3(0, 0.64f, 0), new Vector3(2.6f, 0.08f, 0.75f), kit.Mat("ClothBlue"), false);
        }

        private Npc SahretResident(GameObject prefab, Transform parent, string name, Vector3 local, string cloth,
            string greeting, string topic, string story, NpcRole role = NpcRole.Villager)
        {
            var look = new EnemyKind
            {
                Name = "SahretResident", Scale = 1, HideHorns = true,
                Cloth = kit.Mat(cloth).GetColor("_BaseColor"), Pants = new Color(0.31f, 0.24f, 0.18f),
                Skin = new Color(0.58f, 0.39f, 0.27f), Eyes = new Color(0.12f, 0.09f, 0.06f)
            };
            Npc npc = Npc.Create(prefab, role, name, look,
                Center(Sahret) + local + Vector3.up * 1.1f, -local.normalized, parent);
            npc.SetConversation(greeting, topic, story);
            return npc;
        }
    }
}
