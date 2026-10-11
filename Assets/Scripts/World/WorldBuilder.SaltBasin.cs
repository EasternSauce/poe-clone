using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildSaltBasin()
        {
            Begin(SaltBasin, 11407);
            Transform basin = Group("The Bleached Basin");
            Vector3 c = Center(SaltBasin);
            AreaShape shape = Shape(SaltBasin);
            var red = new Material(desertStone) { name = "BasinRedStrata" };
            red.SetColor("_BaseColor", new Color(0.57f, 0.29f, 0.20f));
            var salt = new Material(desertStone) { name = "SaltCrust" };
            salt.SetColor("_BaseColor", new Color(0.93f, 0.91f, 0.80f));
            var cracks = new Material(desertStone) { name = "SaltFissures" };
            cracks.SetColor("_BaseColor", new Color(0.43f, 0.37f, 0.29f));

            spawnPoints[SaltBasin].SetPositionAndRotation(c + new Vector3(0, 1.1f, -128), Quaternion.identity);
            Sanctuary.Add(spawnPoints[SaltBasin].position, 12f);
            Sanctuary.Add(WaystoneSpot(SaltBasin), 9f);

            // Layered, flat-topped red mesas enclose a dry lake, rather than rolling dunes.
            for (int z = -160; z <= 160; z += 28)
                for (int x = -154; x <= 154; x += 29)
                {
                    Vector3 p = c + new Vector3(x + R(-4, 4), 0, z + R(-4, 4));
                    if (shape.Contains(p, -16)) continue;
                    BasinMesa(basin, p, new Vector2(R(9, 15), R(8, 13)), R(7, 13), red, salt);
                }
            foreach (var cut in shape.Cuts)
                BasinMesa(basin, c + new Vector3(cut.center.x, 0, cut.center.y), cut.radii * 0.94f,
                    8f, red, salt);

            // Broken polygons of salt crust are flat decoration, so the floor stays traversable.
            var fissures = new PrimitiveBatch();
            for (int z = -100; z < 130; z += 9)
                for (int x = -135; x < 135; x += 10)
                {
                    Vector3 p = c + new Vector3(x + R(-3, 3), 0.026f, z + R(-3, 3));
                    if (!shape.Contains(p, 7f)) continue;
                    float yaw = R(0, 360);
                    for (int branch = 0; branch < 3; branch++)
                    {
                        Quaternion turn = Quaternion.Euler(0, yaw + branch * 120 + R(-18, 18), 0);
                        float length = R(2f, 4.8f);
                        fissures.Add(PrimitiveType.Cube, cracks, p + turn * Vector3.forward * length * 0.5f,
                            turn, new Vector3(0.055f, 0.012f, length));
                    }
                }
            fissures.Build(basin, "DryLakeFissures", shadows: false);

            // A clear entry terrace and blue cloth recall the water city behind the player.
            for (int side = -1; side <= 1; side += 2)
            {
                DesertPillar(basin, c + new Vector3(side * 7, 0, -126), 4.5f);
                WindBanner(basin, SaltBasin, new Vector3(side * 8, 0, -120), side * 15, "ClothBlue", true);
            }

            // Follow the old watercourse: low paired channel walls leave the centre open.
            Vector3[] channel = { new Vector3(-5, 0, -91), new Vector3(-18, 0, -58),
                new Vector3(-60, 0, -20), new Vector3(-75, 0, 3), new Vector3(-46, 0, 65) };
            for (int i = 1; i < channel.Length; i++)
            {
                Vector3 axis = (channel[i] - channel[i - 1]).normalized;
                Vector3 side = Vector3.Cross(axis, Vector3.up);
                float length = Vector3.Distance(channel[i], channel[i - 1]);
                for (float d = 0; d < length; d += 6f)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    Vector3 p = c + channel[i - 1] + axis * d;
                    float yaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
                    for (int bank = -1; bank <= 1; bank += 2)
                        Box(basin, p + side * bank * 4.7f + Vector3.up * 0.35f,
                            new Vector3(0.65f, 0.7f, 3.8f), red, euler: new Vector3(0, yaw, 0));
                }
            }

            BasinSaltworks(basin, c + new Vector3(68, 0, -8), red, salt);
            BasinSaltworks(basin, c + new Vector3(-116, 0, 57), red, salt);

            // A ruined pump at the far end is the destination landmark for this first area pass.
            Vector3 pump = c + new Vector3(-29, 0, 103);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(basin, pump + new Vector3(side * 5, 3, 0), new Vector3(1.3f, 6, 1.8f), red);
                DesertPillar(basin, pump + new Vector3(side * 10, 0, 4), side < 0 ? 6 : 3);
            }
            Box(basin, pump + new Vector3(0, 5.6f, 0), new Vector3(11, 0.7f, 1.2f), kit.Mat("Wood"), false);
            Transform wheel = Holder(basin, "StrandedPumpWheel", pump + new Vector3(0, 3.2f, -1.5f), Quaternion.Euler(12, 0, 0));
            for (int spoke = 0; spoke < 10; spoke++)
            {
                float angle = spoke * 36;
                LocalBox(wheel, Vector3.zero, new Vector3(0.18f, 6, 0.22f), kit.Mat("Wood"), false,
                    new Vector3(0, 0, angle));
                Vector3 rim = Quaternion.Euler(0, 0, angle) * Vector3.up * 3;
                LocalBox(wheel, rim, new Vector3(1.95f, 0.35f, 1.3f), kit.Mat("Wood"), false,
                    new Vector3(0, 0, angle));
            }
            // A rocky spur rewards exploration with a different silhouette on the eastern loop.
            for (int i = 0; i < 5; i++)
                WeatheredSlab(basin, "SaltSpire", c + new Vector3(103 + i * 2.5f, 0, 97 + i % 2 * 3),
                    new Vector2(0.8f, 0.9f), R(2, 5), salt, R(-15, 15), topScale: 0.3f);
        }

        private void BasinMesa(Transform parent, Vector3 at, Vector2 radii, float height, Material rock, Material salt)
        {
            // These stand entirely in inaccessible space; the shape owns collision at their foot.
            for (int layer = 0; layer < 3; layer++)
                WeatheredSlab(parent, "RedMesaStratum", at + Vector3.up * (layer * height / 3f - 0.1f),
                    radii * (1f - layer * 0.11f), height / 3f + 0.15f, rock, 0, false, 0.9f);
            WeatheredSlab(parent, "PaleMesaCap", at + Vector3.up * (height - 0.1f),
                radii * 0.70f, 0.3f, salt, 0, false);
        }

        private void BasinSaltworks(Transform parent, Vector3 at, Material stone, Material salt)
        {
            // Evaporation beds sit beside the route with wide gaps between each low enclosure.
            for (int bed = 0; bed < 3; bed++)
            {
                Vector3 p = at + new Vector3((bed - 1) * 10, 0, 9);
                WeatheredSlab(parent, "EvaporatedSaltBed", p + Vector3.up * 0.03f,
                    new Vector2(3.8f, 5), 0.04f, salt, 0, false);
                for (int side = -1; side <= 1; side += 2)
                    Box(parent, p + new Vector3(side * 4, 0.23f, 0), new Vector3(0.45f, 0.46f, 10), stone);
                WeatheredSlab(parent, "HarvestedSalt", p + new Vector3(0, 0, 2),
                    new Vector2(1.6f, 1.4f), 0.8f, salt, R(0, 45), false, 0.3f);
            }
            Vector3 rack = at + new Vector3(-9, 0, -7);
            for (int side = -1; side <= 1; side += 2)
                Box(parent, rack + new Vector3(side * 3, 1.8f, 0), new Vector3(0.25f, 3.6f, 0.25f), kit.Mat("Wood"));
            Box(parent, rack + new Vector3(0, 3.5f, 0), new Vector3(6.5f, 0.3f, 0.3f), kit.Mat("Wood"), false);
            Crate(parent, rack + new Vector3(1, 0, 1), 18);
            Barrel(parent, rack + new Vector3(3, 0, 2));
        }
    }
}
