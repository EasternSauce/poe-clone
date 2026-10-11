using UnityEngine;

namespace PoeClone.Visuals
{
    public static partial class CreatureBuilder
    {
        private static Transform BuildWaterRouteBody(Transform model, Ctx c, string name)
        {
            Transform body = Pivot(model, name, new Vector3(0, 1.3f, 0));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.7f, 0.9f, 0.45f));
            Transform head = Pivot(body, "Head", new Vector3(0, 0.65f, 0.08f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Accent, Vector3.zero, new Vector3(0.3f, 0.34f, 0.28f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.08f, 0.04f, 0.14f),
                    Vector3.one * 0.075f, Vector3.zero, false);
                AddRevenantLimb(c, body, side, false, c.Colors.Second, 0.6f, 0.62f);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.56f, 0.64f);
            }
            c.Anim.Mouth = Pivot(head, CreatureAnimator.MouthName, new Vector3(0, -0.08f, 0.18f));
            return body;
        }

        private static void BuildSluiceWarden(Transform model, Ctx c)
        {
            Transform body = BuildWaterRouteBody(model, c, "SluiceWarden");
            // An entire riveted sluice door hangs off the left arm and trails behind him.
            Transform gate = Pivot(Arm(c, -1).Knee, "DraggedSluiceGate", new Vector3(-0.25f, -0.2f, -0.3f));
            gate.localRotation = Quaternion.Euler(-18, 0, 12);
            Part(gate, PrimitiveType.Cube, c.Colors.Second, new Vector3(0, 0.15f, 0), new Vector3(1.15f, 2, 0.18f), Vector3.zero);
            for (int bar = -1; bar <= 1; bar++)
                Part(gate, PrimitiveType.Cube, Iron, new Vector3(bar * 0.42f, 0.15f, 0.12f), new Vector3(0.1f, 2.15f, 0.12f), Vector3.zero);
            for (int end = -1; end <= 1; end += 2)
                Part(gate, PrimitiveType.Cube, Iron, new Vector3(0, 0.15f + end * 0.8f, 0.14f), new Vector3(1.3f, 0.12f, 0.12f), Vector3.zero);
            c.Anim.Prop = gate;
            Transform maul = Pivot(Arm(c, 1).Knee, "Gatebreaker", new Vector3(0, -0.5f, 0));
            Part(maul, PrimitiveType.Cylinder, Wood, Vector3.zero, new Vector3(0.08f, 0.7f, 0.08f), Vector3.zero);
            Part(maul, PrimitiveType.Cube, Iron, new Vector3(0, -0.7f, 0), new Vector3(0.7f, 0.45f, 0.45f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
                Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(side * 0.45f, 0.4f, 0), new Vector3(0.5f, 0.3f, 0.6f), new Vector3(0, 0, side * 15));
        }

        private static void BuildLastEngineer(Transform model, Ctx c)
        {
            Transform body = BuildWaterRouteBody(model, c, "LastEngineer");
            Transform engine = Pivot(body, "BoundPumpEngine", new Vector3(0, 0.1f, -0.35f));
            c.Anim.Prop = engine;
            Part(engine, PrimitiveType.Cube, Iron, Vector3.zero, new Vector3(1.2f, 1.5f, 0.5f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(engine, PrimitiveType.Cylinder, c.Colors.Second, new Vector3(side * 0.6f, 0.4f, 0),
                    new Vector3(0.2f, 0.65f, 0.2f), Vector3.zero);
                Part(engine, PrimitiveType.Cylinder, Iron, new Vector3(side * 0.6f, 1.05f, 0),
                    new Vector3(0.13f, 0.35f, 0.13f), Vector3.zero);
                Part(body, PrimitiveType.Cube, Iron, new Vector3(0, side * 0.26f, 0.18f),
                    new Vector3(0.75f, 0.09f, 0.5f), Vector3.zero);
            }
            Transform wheel = Pivot(engine, "Flywheel", new Vector3(0, 0, -0.4f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45;
                Part(wheel, PrimitiveType.Cube, c.Colors.Second,
                    Quaternion.Euler(0, 0, angle) * Vector3.up * 0.6f,
                    new Vector3(0.5f, 0.15f, 0.2f), new Vector3(0, 0, angle));
            }
            Transform piston = Pivot(Arm(c, 1).Knee, "PistonArm", new Vector3(0, -0.4f, 0));
            Part(piston, PrimitiveType.Cylinder, Iron, Vector3.zero, new Vector3(0.2f, 0.5f, 0.2f), Vector3.zero);
            Part(piston, PrimitiveType.Cube, c.Colors.Second, new Vector3(0, -0.55f, 0), new Vector3(0.55f, 0.25f, 0.55f), Vector3.zero);
        }
    }
}
