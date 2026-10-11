using UnityEngine;

namespace PoeClone.Visuals
{
    public static partial class CreatureBuilder
    {
        private static void BuildOssan(Transform model, Ctx c)
        {
            Transform body = BuildWaterRouteBody(model, c, "Ossan");
            // A cell door on his back and tablets chained to his shoulders identify the gaoler.
            Transform cage = Pivot(body, "MemoryCell", new Vector3(0, 0.2f, -0.38f));
            c.Anim.Prop = cage;
            for (int bar = -2; bar <= 2; bar++)
                Part(cage, PrimitiveType.Cylinder, Iron, new Vector3(bar * 0.22f, 0.2f, 0),
                    new Vector3(0.06f, 0.85f, 0.06f), Vector3.zero);
            for (int end = -1; end <= 1; end += 2)
                Part(cage, PrimitiveType.Cube, c.Colors.Second, new Vector3(0, 0.2f + end * 0.83f, 0),
                    new Vector3(1.2f, 0.16f, 0.2f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(body, PrimitiveType.Cube, c.Colors.Second, new Vector3(side * 0.48f, 0.4f, 0),
                    new Vector3(0.55f, 0.28f, 0.55f), new Vector3(0, 0, side * 18));
                for (int tablet = 0; tablet < 3; tablet++)
                    Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(side * 0.55f, 0.12f - tablet * 0.18f, 0.18f),
                        new Vector3(0.25f, 0.14f, 0.07f), new Vector3(0, 0, side * 12));
            }
            Transform bell = Pivot(Arm(c, 1).Knee, "WitnessBell", new Vector3(0, -0.6f, 0));
            Cone(c, bell, c.Colors.Second, Vector3.zero, new Vector3(0.75f, 0.8f, 0.75f), Vector3.zero);
            Part(bell, PrimitiveType.Sphere, Iron, Vector3.down * 0.4f, Vector3.one * 0.2f, Vector3.zero);
        }

        private static void BuildFirstRoot(Transform model, Ctx c)
        {
            Transform body = BuildWaterRouteBody(model, c, "FirstRoot");
            // Open ribs surround a luminous heart; thorn limbs carry a crown of dead river roots.
            for (int rib = 0; rib < 4; rib++)
                for (int side = -1; side <= 1; side += 2)
                    Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(side * 0.35f, 0.4f - rib * 0.2f, 0.25f),
                        new Vector3(0.5f, 0.09f, 0.23f), new Vector3(0, side * 30, -side * 15));
            Ico(c, body, c.Colors.Eyes, new Vector3(0, 0.08f, 0.41f), new Vector3(0.35f, 0.48f, 0.30f));
            for (int root = 0; root < 9; root++)
            {
                float angle = root * 40;
                Vector3 at = Quaternion.Euler(0, angle, 0) * new Vector3(0, 0.55f, 0.46f);
                Cone(c, body, c.Colors.Main, at, new Vector3(0.2f, 1.5f, 0.2f), new Vector3(35, angle, 0));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Transform arm = Arm(c, side).Knee;
                for (int thorn = 0; thorn < 3; thorn++)
                    Cone(c, arm, c.Colors.Accent, new Vector3(side * 0.15f, -0.2f - thorn * 0.15f, 0.1f),
                        new Vector3(0.16f, 0.6f, 0.16f), new Vector3(0, 0, -side * 60));
            }
            c.Anim.Prop = Pivot(body, "HeartVeins", Vector3.zero);
        }
    }
}
