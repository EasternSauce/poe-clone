using UnityEngine;
using PoeClone.Visuals;
using PoeClone.Enemies;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void ConfigureCourtPopulation(EnemySpawner spawner, int area)
        {
            if (area == Orchard)
            {
                float[] outskirts = Roster(("Orchard Hound", 4), ("Jar Swarm", 3), ("Rootbound Harvester", 1));
                float[] village = Roster(("Rootbound Harvester", 4), ("Jar Keeper", 3), ("Jar Swarm", 2));
                spawner.SetKindSelector(p => EnemyKinds.PickIndex((p - Center(area)).z < -30 ? outskirts : village));
            }
            else if (area == Reservoir)
            {
                float[] causeway = Roster(("Bronze Sluiceguard", 3), ("Reservoir Skimmer", 4), ("Drowned Courtier", 1));
                float[] court = Roster(("Bronze Sluiceguard", 3), ("Drowned Courtier", 3), ("Tithe Priest", 2));
                spawner.SetKindSelector(p => EnemyKinds.PickIndex((p - Center(area)).z < 0 ? causeway : court));
            }
        }

        private void BuildEmptyOrchard()
        {
            PrepareWaterRoute(Orchard, 14429, new Vector3(0, 0, -114));
            Transform orchard = Group("The Orchard of Empty Jars");
            Vector3 c = Center(Orchard);
            AreaShape shape = Shape(Orchard);
            var bark = new Material(kit.Mat("DeadWood")) { name = "OrchardDeadBark" };
            bark.SetColor("_BaseColor", new Color(0.30f, 0.29f, 0.18f));
            var clay = new Material(desertStone) { name = "OldOrchardClay" };
            clay.SetColor("_BaseColor", new Color(0.52f, 0.32f, 0.19f));

            // Rows of trees trace the old planting beds; clear courts remain around objectives.
            for (int z = -133; z <= 139; z += 18)
                for (int x = -130; x <= 130; x += 19)
                {
                    Vector3 at = c + new Vector3(x + R(-2, 2), 0, z + R(-2, 2));
                    bool onEdge = shape.Contains(at, -8) && !shape.Contains(at, 7);
                    bool planted = shape.Contains(at, 6) && (x + 130) % 38 == 0;
                    if ((!onEdge && !planted) || (at - (c + AreaLayouts.BossLocal(Orchard))).sqrMagnitude < 24 * 24 ||
                        (at - WaystoneSpot(Orchard)).sqrMagnitude < 17 * 17) continue;
                    bool objective = false;
                    foreach (var anchor in AreaLayouts.QuestAnchors["act2_jars"])
                        if ((at - (c + anchor)).sqrMagnitude < 10 * 10) objective = true;
                    if (objective) continue;
                    PlanPrefab(kit.Tree("OrchardSnag"), orchard, at, R(0, 360), Vector3.one);
                    if (rng.NextDouble() < 0.6)
                    {
                        PlanBox(orchard, at + new Vector3(0.9f, 2.8f, 0), new Vector3(0.04f, 1.5f, 0.04f), kit.Mat("Wood"), false);
                        HangingJar(orchard, at + new Vector3(0.9f, 1.8f, 0), clay);
                    }
                }
            DryChannel(orchard, c, clay, new[] { new Vector3(0, 0, -114), new Vector3(-26, 0, -78),
                new Vector3(-70, 0, -47), new Vector3(-91, 0, 9), new Vector3(-59, 0, 85) });

            // Low ruined homes around a village square keep the isometric sightline open.
            foreach (Vector3 local in new[] { new Vector3(-17, 0, 6), new Vector3(14, 0, 31), new Vector3(76, 0, -33) })
            {
                Vector3 at = c + local;
                for (int side = -1; side <= 1; side += 2)
                    PlanBox(orchard, at + new Vector3(side * 4, 1.1f, 0), new Vector3(0.7f, 2.2f, 7), clay);
                PlanBox(orchard, at + new Vector3(0, 0.7f, 3.5f), new Vector3(8, 1.4f, 0.7f), clay);
                Crate(orchard, at + new Vector3(0, 0, 1), 25);
                PlanBones(orchard, at + new Vector3(1, 0, -1));
                SahretJar(orchard, at + new Vector3(-2, 0, -2), 0.6f);
            }
            // The Mother's well sits behind the fight, with exposed roots pointing toward it.
            Vector3 well = c + AreaLayouts.BossLocal(Orchard) + new Vector3(0, 0, 20);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30;
                PlanBox(orchard, well + Quaternion.Euler(0, angle, 0) * new Vector3(0, 0.6f, 4),
                    new Vector3(2.2f, 1.2f, 0.8f), clay, true, new Vector3(0, angle, 0));
            }
            PlanCyl(orchard, well + Vector3.up * 0.15f, 3.5f, 0.1f, kit.Mat("Charred"), false);
            for (int i = 0; i < 7; i++)
            {
                float yaw = -70 + i * 23;
                Vector3 at = well + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0.08f, -7);
                PlanBox(orchard, at, new Vector3(0.4f, 0.16f, 7), bark, false, new Vector3(0, yaw, 0));
            }
            PlanGlow(orchard, well + Vector3.up * 1.5f, new Color(0.55f, 0.75f, 0.25f), 12, 4);
        }

        private void HangingJar(Transform parent, Vector3 at, Material clay)
        {
            if (DeferContent(at, () => HangingJar(parent, at, clay))) return;
            Cyl(parent, at, 0.45f, 0.8f, clay, false);
            Cyl(parent, at + Vector3.up * 0.48f, 0.23f, 0.2f, clay, false);
            Cyl(parent, at + Vector3.up * 0.59f, 0.16f, 0.02f, kit.Mat("Charred"), false);
        }

        private void BuildCrownlessReservoir()
        {
            PrepareWaterRoute(Reservoir, 15431, new Vector3(-113, 0, -109));
            Transform court = Group("The Crownless Reservoir");
            Vector3 c = Center(Reservoir);
            var pale = new Material(desertStone) { name = "ReservoirPaleLimestone" };
            pale.SetColor("_BaseColor", new Color(0.82f, 0.79f, 0.63f));
            var bronze = new Material(kit.Mat("Iron")) { name = "VerdigrisBronze" };
            bronze.SetColor("_BaseColor", new Color(0.26f, 0.49f, 0.40f));
            AreaShape shape = Shape(Reservoir);
            // Monumental outer piers and low terraces frame the playable courts.
            for (int z = -160; z <= 160; z += 28)
                for (int x = -150; x <= 150; x += 30)
                {
                    Vector3 at = c + new Vector3(x, 0, z);
                    if (shape.Contains(at, -11)) continue;
                    PlanBox(court, at + Vector3.up * 3.5f, new Vector3(9, 7, 9), pale, false);
                    PlanBox(court, at + Vector3.up * 7.2f, new Vector3(10, 0.4f, 10), bronze, false);
                }
            foreach (Vector3 pool in new[] { new Vector3(-64, 0, -18), new Vector3(45, 0, -57), new Vector3(-49, 0, 77) })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 at = c + pool + new Vector3(side * 21, 0, 0);
                    HeadlessCourtStatue(court, at, pale, bronze);
                    PlanGlow(court, at + new Vector3(0, 2, -2), new Color(0.45f, 0.85f, 0.76f), 11, 4);
                }
            }
            DryChannel(court, c, bronze, new[] { new Vector3(-113, 0, -109), new Vector3(-85, 0, -67),
                new Vector3(-62, 0, -18), new Vector3(5, 0, 18), new Vector3(85, 0, 35) });
            // Palace columns sit at the edges of Ishara's broad arena.
            Vector3 palace = c + AreaLayouts.BossLocal(Reservoir);
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                {
                    Vector3 at = palace + new Vector3(side * 29, 0, -17 + row * 17);
                    PlanCyl(court, at + Vector3.up * 4.5f, 1.1f, 9, pale);
                    PlanBox(court, at + Vector3.up * 9, new Vector3(3, 0.6f, 3), bronze, false);
                }
            PlanBox(court, palace + new Vector3(0, 8.5f, 31), new Vector3(48, 1, 3), pale, false);
            for (int side = -1; side <= 1; side += 2)
                PlanBox(court, palace + new Vector3(side * 22, 4, 30), new Vector3(3, 8, 4), pale);
            for (int i = 0; i < 4; i++)
                SahretJar(court, c + new Vector3(105 + i % 2 * 3, 0, 24 + i / 2 * 5), 0.7f);
            // Drained landing stages suggest a deliberately lowered waterline.
            for (int i = 0; i < 6; i++)
                PlanBox(court, c + new Vector3(66 + i * 2, 0.035f, -71), new Vector3(1.7f, 0.07f, 8), kit.Mat("Wood"), false);
        }

        private void HeadlessCourtStatue(Transform parent, Vector3 at, Material stone, Material bronze)
        {
            if (DeferContent(at, () => HeadlessCourtStatue(parent, at, stone, bronze))) return;
            Box(parent, at + Vector3.up * 0.35f, new Vector3(3, 0.7f, 3), stone);
            Box(parent, at + Vector3.up * 2.3f, new Vector3(1.4f, 3.4f, 0.9f), stone, false);
            Box(parent, at + Vector3.up * 3.4f, new Vector3(2.4f, 0.7f, 1), bronze, false);
            Cyl(parent, at + Vector3.up * 4, 0.2f, 0.35f, stone, false);
            WeatheredSlab(parent, "FallenRoyalHead", at + new Vector3(1.6f, 0, -1.5f),
                new Vector2(0.4f, 0.5f), 0.65f, stone, 30, false);
        }
    }
}
