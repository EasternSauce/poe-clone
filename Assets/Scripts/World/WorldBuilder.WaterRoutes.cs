using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private static readonly Vector3 ChannelWardenLocal = AreaLayouts.BossLocal(Channels);
        private static readonly Vector3 WorksEngineerLocal = AreaLayouts.BossLocal(DrownedWorks);

        private void PrepareWaterRoute(int area, int seed, Vector3 arrival)
        {
            Begin(area, seed);
            spawnPoints[area].SetPositionAndRotation(Center(area) + arrival + Vector3.up * 1.1f, Quaternion.identity);
            Sanctuary.Add(spawnPoints[area].position, 12f);
            Sanctuary.Add(WaystoneSpot(area), 9f);
        }

        private void BuildThirstingChannels()
        {
            PrepareWaterRoute(Channels, 12411, new Vector3(-112, 0, -101));
            Transform gorge = Group("The Thirsting Channels");
            Vector3 c = Center(Channels);
            AreaShape shape = Shape(Channels);
            var red = new Material(desertStone) { name = "ChannelRedSandstone" };
            red.SetColor("_BaseColor", new Color(0.58f, 0.29f, 0.19f));
            for (int z = -156; z <= 156; z += 24)
                for (int x = -156; x <= 156; x += 25)
                {
                    Vector3 at = c + new Vector3(x + R(-3, 3), 0, z + R(-3, 3));
                    if (shape.Contains(at, -13)) continue;
                    BasinMesa(gorge, at, new Vector2(R(8, 12), R(8, 12)), R(9, 16), red, desertStone);
                }
            foreach (var cut in shape.Cuts)
                BasinMesa(gorge, c + new Vector3(cut.center.x, 0, cut.center.y), cut.radii * 0.9f, 11, red, desertStone);

            // Low banks frame a dry bed; they never cross its walkable centre.
            DryChannel(gorge, c, red, new[] {
                new Vector3(-112, 0, -101), new Vector3(-48, 0, -48),
                new Vector3(-83, 0, -2), new Vector3(-57, 0, 46),
                new Vector3(-7, 0, 68), new Vector3(51, 0, 91) });
            DryChannel(gorge, c, red, new[] {
                new Vector3(-48, 0, -48), new Vector3(14, 0, -65),
                new Vector3(59, 0, -27), new Vector3(58, 0, 33), new Vector3(51, 0, 91) });

            // The surviving overhead spans clear both the player and all combat lanes.
            foreach (Vector3 local in new[] { new Vector3(-75, 0, -7), new Vector3(59, 0, 13) })
            {
                for (int side = -1; side <= 1; side += 2)
                    DesertPillar(gorge, c + local + new Vector3(side * 12, 0, 0), 9);
                Box(gorge, c + local + Vector3.up * 9, new Vector3(26, 1.2f, 2.5f), red, false);
                for (int side = -1; side <= 1; side += 2)
                    Box(gorge, c + local + new Vector3(0, 10, side * 1.3f), new Vector3(26, 1, 0.4f), red, false);
            }
            // Irrigation terraces occupy the edges of the broad eastern court.
            for (int row = 0; row < 4; row++)
            {
                Vector3 at = c + new Vector3(75, 0, -46 + row * 9);
                Box(gorge, at + Vector3.up * 0.25f, new Vector3(14, 0.5f, 0.6f), red);
                for (int i = 0; i < 3; i++)
                    WeatheredSlab(gorge, "DryPlantingBed", at + new Vector3(-4 + i * 4, 0.03f, 3),
                        new Vector2(1.5f, 2), 0.05f, kit.Mat("Charred"), 0, false);
            }
            DesertWagon(gorge, c + new Vector3(-72, 0, 58), 55);
            for (int i = 0; i < 6; i++)
                SahretJar(gorge, c + new Vector3(112 + i % 3 * 3, 0, 43 + i / 3 * 6), 0.6f);
            BasinSaltworks(gorge, c + new Vector3(-54, 0, -57), red, desertStone);

            // The Warden's sealed upstream sluice is a landmark, not an unfinished travel gate.
            Vector3 sluice = c + ChannelWardenLocal + new Vector3(0, 0, 22);
            for (int side = -1; side <= 1; side += 2)
                DesertPillar(gorge, sluice + new Vector3(side * 9, 0, 0), 11);
            Box(gorge, sluice + new Vector3(0, 5, 0), new Vector3(15, 10, 1), kit.Mat("Iron"));
            for (int i = -3; i <= 3; i++)
                Box(gorge, sluice + new Vector3(i * 2, 5, -0.6f), new Vector3(0.3f, 10, 0.35f), red, false);
            WorldLabel.Create(gorge, "The water is held upstream", AreaColor(Channels), 3f)
                .transform.position = sluice + Vector3.up * 3;
        }

        private void DryChannel(Transform parent, Vector3 center, Material stone, Vector3[] knots)
        {
            for (int i = 1; i < knots.Length; i++)
            {
                Vector3 axis = (knots[i] - knots[i - 1]).normalized;
                Vector3 side = Vector3.Cross(axis, Vector3.up);
                float yaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
                for (float d = 0; d < Vector3.Distance(knots[i], knots[i - 1]); d += 7)
                {
                    Vector3 at = center + knots[i - 1] + axis * d;
                    for (int bank = -1; bank <= 1; bank += 2)
                        Box(parent, at + side * bank * 5.5f + Vector3.up * 0.22f,
                            new Vector3(0.45f, 0.44f, 3.6f), stone, false, new Vector3(0, yaw, 0));
                }
            }
        }

        private void BuildDrownedWorks()
        {
            PrepareWaterRoute(DrownedWorks, 13417, new Vector3(0, 0, -117));
            Transform works = Group("The Drowned Works");
            Vector3 c = Center(DrownedWorks);
            var damp = new Material(desertStone) { name = "MineralStainedMasonry" };
            damp.SetColor("_BaseColor", new Color(0.30f, 0.43f, 0.36f));
            // Reservoir edges are traversable rings around pools, connected by two looping galleries.
            foreach (Vector3 pool in new[] { new Vector3(-66, 0, -57), new Vector3(65, 0, 40) })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(works, c + pool + new Vector3(side * 19, 0.3f, 0), new Vector3(0.6f, 0.6f, 18), damp);
                    PumpWheel(works, c + pool + new Vector3(side * 21, 0, 0), damp);
                }
            }
            foreach (Vector3 at in new[] { new Vector3(-93, 0, -8), new Vector3(92, 0, -2),
                new Vector3(-47, 0, 73), new Vector3(39, 0, 81), new Vector3(0, 0, -83) })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = c + at + new Vector3(side * 10, 0, 0);
                    Box(works, p + Vector3.up * 2.7f, new Vector3(1.4f, 5.4f, 1.4f), damp);
                    Glow(works, p + new Vector3(-side, 3, 0), new Color(0.35f, 0.85f, 0.72f), 11, 5);
                }
                Box(works, c + at + Vector3.up * 5.8f, new Vector3(22, 0.8f, 1.4f), damp, false);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = c + new Vector3(-82 + i * 8, 0, 52);
                Cyl(works, at + Vector3.up * 0.7f, 1.7f, 1.4f, kit.Mat("Iron"));
                Cyl(works, at + Vector3.up * 1.6f, 0.4f, 0.5f, damp, false);
                Bones(works, at + new Vector3(0, 0, -4));
            }
            // The engine stands behind the boss's clear floor, with the return valve beside it.
            Vector3 engine = c + WorksEngineerLocal + new Vector3(0, 0, 21);
            PumpWheel(works, engine, damp);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(works, engine + new Vector3(side * 9, 3, 0), new Vector3(3, 6, 3), damp);
                Cyl(works, engine + new Vector3(side * 9, 7, 0), 0.8f, 2, kit.Mat("Iron"), false);
                Glow(works, engine + new Vector3(side * 9, 3, -2), new Color(0.4f, 0.9f, 0.7f), 12, 6);
            }
            Transform valve = Holder(works, "SahretReturnValve", c + new Vector3(-19, 0, 120), Quaternion.identity);
            LocalCyl(valve, Vector3.up, 0.28f, 2, kit.Mat("Iron"));
            Transform handle = Holder(valve, "ValveHandle", valve.position + Vector3.up * 2, Quaternion.identity);
            LocalBox(handle, Vector3.zero, new Vector3(2.2f, 0.2f, 0.2f), kit.Mat("Iron"), false);
            LocalBox(handle, Vector3.zero, new Vector3(0.2f, 0.2f, 2.2f), kit.Mat("Iron"), false);
            valve.gameObject.AddComponent<SahretWaterSupply>().ConfigureValve(handle);
        }

        private void PumpWheel(Transform parent, Vector3 at, Material stone)
        {
            Transform wheel = Holder(parent, "SeizedPumpWheel", at + Vector3.up * 3.7f, Quaternion.identity);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30;
                LocalBox(wheel, Vector3.zero, new Vector3(0.14f, 7, 0.25f), kit.Mat("Iron"), false, new Vector3(0, 0, angle));
                LocalBox(wheel, Quaternion.Euler(0, 0, angle) * Vector3.up * 3.5f,
                    new Vector3(1.9f, 0.35f, 1.1f), kit.Mat("Wood"), false, new Vector3(0, 0, angle));
            }
            for (int side = -1; side <= 1; side += 2)
                Box(parent, at + new Vector3(side * 4.4f, 2, 0), new Vector3(1, 4, 1.5f), stone);
        }
    }
}
