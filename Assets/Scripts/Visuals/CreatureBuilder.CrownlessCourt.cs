using UnityEngine;

namespace PoeClone.Visuals
{
    public static partial class CreatureBuilder
    {
        private static void BuildHarvestMother(Transform model, Ctx c)
        {
            Transform body = BuildWaterRouteBody(model, c, "MotherOfTheLastHarvest");
            // Branches, roots and hanging jars make a walking orchard shrine.
            for (int side = -1; side <= 1; side += 2)
            {
                Transform branch = Pivot(body, "JarBranch", new Vector3(side * 0.4f, 0.5f, -0.2f));
                Part(branch, PrimitiveType.Cube, c.Colors.Main, new Vector3(side * 0.4f, 0.4f, 0),
                    new Vector3(0.16f, 1.4f, 0.16f), new Vector3(0, 0, -side * 40));
                Part(branch, PrimitiveType.Cube, c.Colors.Main, new Vector3(side * 0.75f, 0.9f, 0),
                    new Vector3(0.8f, 0.12f, 0.12f), Vector3.zero);
                Part(branch, PrimitiveType.Cylinder, Wood, new Vector3(side * 0.9f, 0.5f, 0),
                    new Vector3(0.015f, 0.3f, 0.015f), Vector3.zero);
                Part(branch, PrimitiveType.Sphere, c.Colors.Second, new Vector3(side * 0.9f, 0.15f, 0),
                    new Vector3(0.4f, 0.5f, 0.4f), Vector3.zero);
                Part(branch, PrimitiveType.Cylinder, Dark, new Vector3(side * 0.9f, 0.42f, 0),
                    new Vector3(0.1f, 0.04f, 0.1f), Vector3.zero);
                for (int root = 0; root < 3; root++)
                    Cone(c, body, c.Colors.Main, new Vector3(side * (0.35f + root * 0.18f), -0.7f, -0.2f + root * 0.25f),
                        new Vector3(0.25f, 1.3f, 0.25f), new Vector3(160, 0, side * 30));
            }
            for (int i = -2; i <= 2; i++)
                Cone(c, c.Anim.Head, c.Colors.Main, new Vector3(i * 0.11f, 0.2f, -0.03f),
                    new Vector3(0.12f, 0.5f + (2 - Mathf.Abs(i)) * 0.12f, 0.12f), new Vector3(0, 0, -i * 12));
        }

        private static void BuildIshara(Transform model, Ctx c)
        {
            Transform body = BuildWaterRouteBody(model, c, "Ishara");
            for (int panel = 0; panel < 8; panel++)
            {
                float angle = panel * 45;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * new Vector3(0, -0.3f, 0.35f);
                Cone(c, body, c.Colors.Second, offset, new Vector3(0.3f, 1.2f, 0.12f), new Vector3(180, angle, 0));
            }
            // A broken bronze halo is a court insignia, with its upper crown deliberately absent.
            for (int i = 0; i < 7; i++)
            {
                float angle = 70 + i * 36;
                Part(c.Anim.Head, PrimitiveType.Cube, c.Colors.Accent,
                    new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.45f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.45f, -0.2f),
                    new Vector3(0.24f, 0.08f, 0.08f), new Vector3(0, 0, -angle));
            }
            Transform staff = Pivot(Arm(c, 1).Knee, "RiverSeal", new Vector3(0, -0.5f, 0));
            c.Anim.Prop = staff;
            Part(staff, PrimitiveType.Cylinder, c.Colors.Accent, Vector3.zero, new Vector3(0.07f, 0.9f, 0.07f), Vector3.zero);
            Ico(c, staff, c.Colors.Eyes, new Vector3(0, 0.95f, 0), Vector3.one * 0.4f);
            for (int side = -1; side <= 1; side += 2)
                Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(side * 0.43f, 0.35f, 0),
                    new Vector3(0.42f, 0.16f, 0.5f), new Vector3(0, 0, side * 15));
        }
    }
}
