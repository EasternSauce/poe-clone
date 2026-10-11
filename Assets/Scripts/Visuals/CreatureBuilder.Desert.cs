using UnityEngine;

namespace PoeClone.Visuals
{
    public static partial class CreatureBuilder
    {
        private static void BuildScorpion(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Body", new Vector3(0, 0.42f, 0));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.85f, 0.42f, 1.1f));
            for (int i = 0; i < 4; i++)
                for (int side = -1; side <= 1; side += 2)
                    AddInsectLeg(c, body, side, new Vector3(side * 0.28f, 0, 0.32f - i * 0.21f),
                        -30 + i * 23, 0.48f, 0.65f, 25, -95, (i + (side > 0 ? 0 : 1)) % 2);
            for (int i = 0; i < 4; i++)
                Ico(c, body, c.Colors.Second, new Vector3(0, 0.23f, 0.3f - i * 0.22f), new Vector3(0.78f - i * 0.06f, 0.18f, 0.23f));
            for (int side = -1; side <= 1; side += 2)
            {
                Transform claw = Pivot(body, side < 0 ? "FangL" : "FangR", new Vector3(side * 0.48f, 0.05f, 0.49f));
                if (side < 0) c.Anim.FangL = claw; else c.Anim.FangR = claw;
                Ico(c, claw, c.Colors.Main, new Vector3(side * 0.15f, 0, 0.3f), new Vector3(0.28f, 0.2f, 0.7f));
                for (int jaw = -1; jaw <= 1; jaw += 2)
                    Cone(c, claw, c.Colors.Second, new Vector3(side * 0.15f + jaw * 0.1f, 0, 0.72f),
                        new Vector3(0.12f, 0.4f, 0.12f), new Vector3(90, jaw * -18, 0));
            }
            Transform tail = Pivot(body, "ScorpionTail", new Vector3(0, 0, -0.55f));
            c.Anim.Tail = tail;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 0.4f;
                Ico(c, tail, i % 2 == 0 ? c.Colors.Main : c.Colors.Second,
                    new Vector3(0, 0.1f + Mathf.Sin(a) * 1.1f, -Mathf.Sin(a * 0.7f) * 0.5f + i * 0.1f),
                    new Vector3(0.24f - i * 0.015f, 0.28f, 0.27f));
            }
            Cone(c, tail, c.Colors.Accent, new Vector3(0, 1.1f, 0.18f), new Vector3(0.1f, 0.43f, 0.1f), new Vector3(135, 0, 0));
            SpiderEyes(c, body, new Vector3(0, 0.17f, 0.49f));
        }

        private static void BuildSandlurker(Transform model, Ctx c)
        {
            BuildWolf(model, c);
            // A broad plated burrowing reptile: a flattened skull, dorsal shields and shovel feet.
            for (int i = 0; i < 6; i++)
                Ico(c, c.Anim.BodyPivot, c.Colors.Second, new Vector3(0, 0.24f, 0.42f - i * 0.19f),
                    new Vector3(0.6f - i * 0.035f, 0.18f, 0.3f));
            if (c.Anim.Head != null)
                c.Anim.Head.localScale = new Vector3(1.6f, 0.62f, 1.4f);
            foreach (var leg in c.Anim.Legs)
                Part(leg.Knee, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0, -0.32f, 0.14f),
                    new Vector3(0.24f, 0.1f, 0.38f), Vector3.zero);
        }
    }
}
