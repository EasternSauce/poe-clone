using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>The shape of an enemy's body. Everything but Humanoid is built here, not by the prefab.</summary>
    public enum CreatureBody
    {
        Humanoid,
        Spider,   // eight jointed legs, a fat abdomen, fangs
        Wolf,     // four legs, a snout with a working jaw, a tail
        Slime,    // a hopping, wobbling blob
        Bat,      // flies: flapping wings, hangs in the air
        Beetle    // six short legs under a heavy shell, a horn; spits
    }

    /// <summary>
    /// Builds a non-humanoid body out of primitives under an enemy's "Model" (feet at its origin,
    /// facing +Z), as a hierarchy of named pivots that <see cref="CreatureAnimator"/> then drives.
    /// Colours: Main (body/fur/skin), Second (abdomen, shell, mane, wings), Accent (markings, lava
    /// cracks, belly), Eyes. Sizes are for scale 1; the enemy's own scale does the rest.
    /// </summary>
    public static class CreatureBuilder
    {
        private static readonly Color Bone = new Color(0.90f, 0.86f, 0.74f);
        private static readonly Color Dark = new Color(0.08f, 0.06f, 0.06f);

        public struct Palette
        {
            public Color Main;
            public Color Second;
            public Color Accent;
            public Color Eyes;
        }

        private sealed class Ctx
        {
            public CreatureAnimator Anim;
            public Palette Colors;
            public Mesh Ico;   // the faceted head mesh of the humanoid rig: reads as a stylised body
            public Mesh Cone;  // the horn mesh: fangs, ears, claws
        }

        /// <param name="meshes">Meshes taken from the humanoid rig before it was removed (by name).</param>
        public static CreatureAnimator Build(Transform model, CreatureBody body, Palette colors, Dictionary<string, Mesh> meshes)
        {
            var ctx = new Ctx
            {
                Anim = model.gameObject.AddComponent<CreatureAnimator>(),
                Colors = colors
            };
            meshes.TryGetValue("IcoHead", out ctx.Ico);
            meshes.TryGetValue("Cone", out ctx.Cone);

            ctx.Anim.Body = body;
            switch (body)
            {
                case CreatureBody.Spider: BuildSpider(model, ctx); break;
                case CreatureBody.Wolf: BuildWolf(model, ctx); break;
                case CreatureBody.Slime: BuildSlime(model, ctx); break;
                case CreatureBody.Bat: BuildBat(model, ctx); break;
                case CreatureBody.Beetle: BuildBeetle(model, ctx); break;
            }
            ctx.Anim.CaptureRest();
            return ctx.Anim;
        }

        // ------------------------------------------------------------------ spider

        private static void BuildSpider(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Body", new Vector3(0f, 0.55f, 0f));
            c.Anim.BodyPivot = body;

            Ico(c, body, c.Colors.Main, new Vector3(0f, 0.02f, 0.22f), new Vector3(0.55f, 0.38f, 0.62f));

            Transform abdomen = Pivot(body, "Abdomen", new Vector3(0f, 0.06f, -0.18f));
            c.Anim.Abdomen = abdomen;
            Ico(c, abdomen, c.Colors.Second, new Vector3(0f, 0.14f, -0.45f), new Vector3(0.88f, 0.72f, 1.05f));
            // A marking on its back, and a spinneret.
            Part(abdomen, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.47f, -0.4f), new Vector3(0.2f, 0.06f, 0.36f), new Vector3(-8f, 0f, 0f));
            Part(abdomen, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.41f, -0.68f), new Vector3(0.32f, 0.06f, 0.1f), new Vector3(-34f, 0f, 0f));

            SpiderEyes(c, body, new Vector3(0f, 0.12f, 0.5f));
            Fangs(c, body, new Vector3(0f, -0.04f, 0.5f), 0.2f);

            // Four legs a side: front pair reaching forward, back pair trailing.
            float[] z = { 0.36f, 0.2f, 0.04f, -0.12f };
            float[] yaw = { -48f, -16f, 14f, 42f };
            for (int i = 0; i < 4; i++)
            {
                AddInsectLeg(c, body, +1, new Vector3(0.2f, -0.02f, z[i]), yaw[i], 0.62f, 0.98f, 34f, -100f, i);
                AddInsectLeg(c, body, -1, new Vector3(-0.2f, -0.02f, z[i]), yaw[i], 0.62f, 0.98f, 34f, -100f, i + 1);
            }
        }

        private static void SpiderEyes(Ctx c, Transform body, Vector3 at)
        {
            Vector3 s = new Vector3(0.07f, 0.07f, 0.05f);
            Part(body, PrimitiveType.Sphere, c.Colors.Eyes, at + new Vector3(-0.07f, 0.02f, 0f), s, Vector3.zero, false);
            Part(body, PrimitiveType.Sphere, c.Colors.Eyes, at + new Vector3(0.07f, 0.02f, 0f), s, Vector3.zero, false);
            Part(body, PrimitiveType.Sphere, c.Colors.Eyes, at + new Vector3(-0.15f, -0.01f, -0.05f), s * 0.75f, Vector3.zero, false);
            Part(body, PrimitiveType.Sphere, c.Colors.Eyes, at + new Vector3(0.15f, -0.01f, -0.05f), s * 0.75f, Vector3.zero, false);
        }

        // Two curved fangs (or mandibles) on their own pivots, so a bite can open and snap them shut.
        private static void Fangs(Ctx c, Transform parent, Vector3 at, float length)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Transform pivot = Pivot(parent, side < 0 ? "FangL" : "FangR", at + new Vector3(side * 0.08f, 0f, 0f));
                Cone(c, pivot, Bone, Vector3.zero, new Vector3(0.05f, length, 0.05f), new Vector3(150f, 0f, side * 12f));
                if (side < 0) c.Anim.FangL = pivot; else c.Anim.FangR = pivot;
            }
        }

        /// <summary>
        /// A jointed insect leg: the hip turns it out from the body (yaw), the femur rises to the
        /// knee (lift), and the tibia comes down to the ground (knee). side +1 is the right.
        /// </summary>
        private static void AddInsectLeg(Ctx c, Transform body, int side, Vector3 at, float yaw, float femur, float tibia,
            float lift, float knee, int gaitGroup)
        {
            // The leg is modelled along +X; the left side points it the other way round.
            float baseYaw = side > 0 ? yaw : 180f - yaw;
            Transform hip = Pivot(body, "Hip", at);
            hip.localRotation = Quaternion.Euler(0f, baseYaw, lift);
            Part(hip, PrimitiveType.Cube, c.Colors.Main, new Vector3(femur * 0.5f, 0f, 0f), new Vector3(femur, 0.08f, 0.08f), Vector3.zero);
            Transform kneePivot = Pivot(hip, "Knee", new Vector3(femur, 0f, 0f));
            kneePivot.localRotation = Quaternion.Euler(0f, 0f, knee);
            Part(kneePivot, PrimitiveType.Sphere, c.Colors.Second, Vector3.zero, Vector3.one * 0.11f, Vector3.zero);
            Part(kneePivot, PrimitiveType.Cube, c.Colors.Main, new Vector3(tibia * 0.5f, 0f, 0f), new Vector3(tibia, 0.06f, 0.06f), Vector3.zero);

            c.Anim.Legs.Add(new CreatureAnimator.Leg
            {
                Hip = hip,
                Knee = kneePivot,
                Yaw = baseYaw,
                Lift = lift,
                Bend = knee,
                Side = side,
                Group = gaitGroup % 2
            });
        }

        // ------------------------------------------------------------------ wolf

        private static void BuildWolf(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Body", new Vector3(0f, 0.84f, 0f));
            c.Anim.BodyPivot = body;

            // Barrel of a body, a shaggy chest/mane, a pale belly.
            Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0f, -0.05f), new Vector3(0.42f, 0.4f, 1.0f), Vector3.zero);
            Part(body, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0.05f, 0.34f), new Vector3(0.5f, 0.52f, 0.42f), new Vector3(-8f, 0f, 0f));
            Part(body, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0.22f, 0.02f), new Vector3(0.3f, 0.12f, 0.6f), Vector3.zero);
            Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, -0.2f, 0f), new Vector3(0.32f, 0.06f, 0.7f), Vector3.zero);

            Transform neck = Pivot(body, "Neck", new Vector3(0f, 0.16f, 0.5f));
            c.Anim.Head = neck;
            Ico(c, neck, c.Colors.Main, new Vector3(0f, 0.1f, 0.16f), new Vector3(0.36f, 0.34f, 0.4f));
            Part(neck, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0.04f, 0.4f), new Vector3(0.2f, 0.15f, 0.34f), new Vector3(6f, 0f, 0f));
            Part(neck, PrimitiveType.Cube, Dark, new Vector3(0f, 0.09f, 0.58f), new Vector3(0.09f, 0.07f, 0.06f), Vector3.zero, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Cone(c, neck, c.Colors.Second, new Vector3(side * 0.11f, 0.24f, 0.08f), new Vector3(0.1f, 0.2f, 0.06f), new Vector3(-10f, 0f, side * -12f));
                Part(neck, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.1f, 0.15f, 0.31f), new Vector3(0.07f, 0.04f, 0.04f), new Vector3(0f, 0f, side * 12f), false);
            }

            Transform jaw = Pivot(neck, "Jaw", new Vector3(0f, -0.04f, 0.26f));
            c.Anim.Jaw = jaw;
            Part(jaw, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.02f, 0.13f), new Vector3(0.17f, 0.06f, 0.3f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
                Cone(c, neck, Bone, new Vector3(side * 0.06f, -0.04f, 0.5f), new Vector3(0.035f, 0.08f, 0.035f), new Vector3(180f, 0f, 0f));

            Transform tail = Pivot(body, "Tail", new Vector3(0f, 0.12f, -0.52f));
            tail.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            c.Anim.Tail = tail;
            Part(tail, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0f, -0.28f), new Vector3(0.14f, 0.14f, 0.56f), Vector3.zero);
            Part(tail, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0f, -0.58f), new Vector3(0.1f, 0.1f, 0.12f), Vector3.zero);

            // Legs: hip pivots under the shoulders and haunches; hind knees bend the other way (hocks).
            for (int side = -1; side <= 1; side += 2)
            {
                AddWolfLeg(c, body, new Vector3(side * 0.15f, -0.12f, 0.36f), side, front: true);
                AddWolfLeg(c, body, new Vector3(side * 0.15f, -0.12f, -0.4f), side, front: false);
            }
        }

        private static void AddWolfLeg(Ctx c, Transform body, Vector3 at, int side, bool front)
        {
            Transform hip = Pivot(body, "Hip", at);
            Part(hip, PrimitiveType.Cube, front ? c.Colors.Second : c.Colors.Main, new Vector3(0f, -0.18f, 0f), new Vector3(0.14f, 0.42f, front ? 0.16f : 0.22f), Vector3.zero);
            Transform knee = Pivot(hip, "Knee", new Vector3(0f, -0.37f, 0f));
            Part(knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.16f, 0f), new Vector3(0.1f, 0.34f, 0.11f), Vector3.zero);
            Part(knee, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, -0.33f, 0.04f), new Vector3(0.13f, 0.07f, 0.18f), Vector3.zero);

            c.Anim.Legs.Add(new CreatureAnimator.Leg
            {
                Hip = hip,
                Knee = knee,
                Side = side,
                Front = front,
                // Trot: diagonal pairs move together.
                Group = (front ? 0 : 1) ^ (side > 0 ? 0 : 1)
            });
        }

        // ------------------------------------------------------------------ slime

        private static void BuildSlime(Transform model, Ctx c)
        {
            Transform blob = Pivot(model, "Blob", Vector3.zero);
            c.Anim.BodyPivot = blob;

            Ico(c, blob, c.Colors.Main, new Vector3(0f, 0.5f, 0f), new Vector3(1.15f, 1.0f, 1.15f));
            // A darker core showing through at the base, a gloss highlight, and bits it has swallowed.
            Ico(c, blob, c.Colors.Second, new Vector3(0f, 0.22f, 0f), new Vector3(1.22f, 0.42f, 1.22f));
            Part(blob, PrimitiveType.Sphere, Color.Lerp(c.Colors.Main, Color.white, 0.55f), new Vector3(-0.2f, 0.86f, 0.18f), new Vector3(0.24f, 0.14f, 0.2f), Vector3.zero, false);
            Part(blob, PrimitiveType.Cube, Bone, new Vector3(0.28f, 0.45f, -0.3f), new Vector3(0.08f, 0.32f, 0.08f), new Vector3(30f, 20f, 40f));
            Part(blob, PrimitiveType.Sphere, c.Colors.Accent, new Vector3(-0.3f, 0.35f, -0.2f), Vector3.one * 0.14f, Vector3.zero);

            for (int side = -1; side <= 1; side += 2)
                Part(blob, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.17f, 0.66f, 0.5f), new Vector3(0.12f, 0.16f, 0.06f), Vector3.zero, false);
            Transform mouth = Pivot(blob, "Mouth", new Vector3(0f, 0.42f, 0.55f));
            c.Anim.Jaw = mouth;
            Part(mouth, PrimitiveType.Cube, Dark, Vector3.zero, new Vector3(0.28f, 0.08f, 0.05f), Vector3.zero, false);
        }

        // ------------------------------------------------------------------ bat

        private static void BuildBat(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Body", new Vector3(0f, 1.55f, 0f));
            c.Anim.BodyPivot = body;

            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.36f, 0.4f, 0.44f));
            Transform head = Pivot(body, "Head", new Vector3(0f, 0.12f, 0.2f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Main, new Vector3(0f, 0.04f, 0.06f), new Vector3(0.28f, 0.26f, 0.26f));
            Part(head, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.0f, 0.2f), new Vector3(0.12f, 0.08f, 0.08f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Cone(c, head, c.Colors.Second, new Vector3(side * 0.09f, 0.14f, 0.02f), new Vector3(0.09f, 0.22f, 0.05f), new Vector3(-10f, 0f, side * -18f));
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.07f, 0.08f, 0.17f), Vector3.one * 0.055f, Vector3.zero, false);
                Cone(c, head, Bone, new Vector3(side * 0.035f, -0.06f, 0.21f), new Vector3(0.025f, 0.07f, 0.025f), new Vector3(180f, 0f, 0f));
            }

            // Each wing: an inner membrane on the shoulder and an outer one on the wrist, which
            // lags the shoulder's flap so the wing whips rather than swinging like a board.
            for (int side = -1; side <= 1; side += 2)
            {
                Transform shoulder = Pivot(body, side < 0 ? "WingL" : "WingR", new Vector3(side * 0.14f, 0.06f, 0.02f));
                if (side < 0) shoulder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Part(shoulder, PrimitiveType.Cube, c.Colors.Second, new Vector3(0.24f, 0f, -0.06f), new Vector3(0.48f, 0.025f, 0.42f), Vector3.zero);
                Part(shoulder, PrimitiveType.Cube, c.Colors.Main, new Vector3(0.24f, 0.01f, 0.14f), new Vector3(0.5f, 0.05f, 0.05f), Vector3.zero);
                Transform wrist = Pivot(shoulder, "Wrist", new Vector3(0.48f, 0f, 0.1f));
                // Three long fingers fanning back from the wrist, with the membrane between them
                // cut into points along the trailing edge.
                float[] fingers = { 5f, 40f, 75f };
                float[] lengths = { 0.55f, 0.5f, 0.38f };
                for (int f = 0; f < fingers.Length; f++)
                {
                    Quaternion turn = Quaternion.Euler(0f, fingers[f], 0f);
                    Part(wrist, PrimitiveType.Cube, c.Colors.Main, turn * new Vector3(lengths[f] * 0.5f, 0.012f, 0f), new Vector3(lengths[f], 0.035f, 0.035f), new Vector3(0f, fingers[f], 0f));
                    if (f > 0)
                    {
                        float mid = (fingers[f] + fingers[f - 1]) * 0.5f;
                        float len = Mathf.Min(lengths[f], lengths[f - 1]);
                        Part(wrist, PrimitiveType.Cube, c.Colors.Second, Quaternion.Euler(0f, mid, 0f) * new Vector3(len * 0.45f, 0f, 0f),
                            new Vector3(len * 0.9f, 0.02f, len * 0.55f), new Vector3(0f, mid, 0f));
                    }
                }
                // Membrane from the last finger back to the body.
                Part(wrist, PrimitiveType.Cube, c.Colors.Second, new Vector3(-0.08f, 0f, -0.2f), new Vector3(0.3f, 0.02f, 0.3f), new Vector3(0f, 45f, 0f));
                Cone(c, wrist, Bone, new Vector3(0.02f, 0.04f, 0.02f), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(-20f, 0f, 0f));
                if (side < 0) { c.Anim.WingL = shoulder; c.Anim.WristL = wrist; }
                else { c.Anim.WingR = shoulder; c.Anim.WristR = wrist; }
            }

            // Little clawed feet tucked under.
            for (int side = -1; side <= 1; side += 2)
                Cone(c, body, c.Colors.Accent, new Vector3(side * 0.07f, -0.24f, -0.12f), new Vector3(0.05f, 0.12f, 0.05f), new Vector3(160f, 0f, 0f));
        }

        // ------------------------------------------------------------------ beetle

        private static void BuildBeetle(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Body", new Vector3(0f, 0.46f, 0f));
            c.Anim.BodyPivot = body;

            Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.06f, 0f), new Vector3(0.66f, 0.24f, 1.0f), Vector3.zero);
            Transform shell = Pivot(body, "Abdomen", new Vector3(0f, 0.04f, -0.05f));
            c.Anim.Abdomen = shell;
            Ico(c, shell, c.Colors.Second, new Vector3(0f, 0.12f, -0.05f), new Vector3(0.95f, 0.6f, 1.25f));
            // Glowing cracks across the wing cases.
            Vector3[] cracks =
            {
                new Vector3(0.22f, 0.38f, 0.15f), new Vector3(-0.25f, 0.36f, -0.1f), new Vector3(0.18f, 0.34f, -0.38f),
                new Vector3(-0.15f, 0.3f, -0.5f), new Vector3(0.3f, 0.26f, -0.2f)
            };
            for (int i = 0; i < cracks.Length; i++)
                Part(shell, PrimitiveType.Cube, c.Colors.Accent, cracks[i], new Vector3(0.05f, 0.03f, 0.22f), new Vector3(0f, 25f * (i % 2 == 0 ? 1 : -1) + i * 10f, 0f), false);

            Transform head = Pivot(body, "Head", new Vector3(0f, 0f, 0.55f));
            c.Anim.Head = head;
            Part(head, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0.02f, 0.1f), new Vector3(0.44f, 0.3f, 0.32f), Vector3.zero);
            Cone(c, head, c.Colors.Second, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.12f, 0.36f, 0.12f), new Vector3(55f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.16f, 0.07f, 0.24f), Vector3.one * 0.08f, Vector3.zero, false);
            Fangs(c, head, new Vector3(0f, -0.06f, 0.26f), 0.18f);
            Transform mouth = Pivot(head, CreatureAnimator.MouthName, new Vector3(0f, 0f, 0.38f));
            c.Anim.Mouth = mouth;

            float[] z = { 0.28f, 0.0f, -0.28f };
            float[] yaw = { -38f, 0f, 36f };
            for (int i = 0; i < 3; i++)
            {
                AddInsectLeg(c, body, +1, new Vector3(0.28f, -0.08f, z[i]), yaw[i], 0.32f, 0.5f, 28f, -96f, i);
                AddInsectLeg(c, body, -1, new Vector3(-0.28f, -0.08f, z[i]), yaw[i], 0.32f, 0.5f, 28f, -96f, i + 1);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Color color, Vector3 position, Vector3 scale, Vector3 euler, bool shadow = true)
        {
            GameObject go = RuntimePrimitives.Create(type, parent, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            if (shadow)
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private static GameObject WithMesh(GameObject go, Mesh mesh)
        {
            if (mesh != null)
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return go;
        }

        private static GameObject Ico(Ctx c, Transform parent, Color color, Vector3 position, Vector3 scale)
        {
            return WithMesh(Part(parent, PrimitiveType.Sphere, color, position, scale, Vector3.zero), c.Ico);
        }

        private static GameObject Cone(Ctx c, Transform parent, Color color, Vector3 position, Vector3 scale, Vector3 euler)
        {
            // Without the cone mesh a thin capsule stands in.
            GameObject go = Part(parent, c.Cone != null ? PrimitiveType.Cube : PrimitiveType.Capsule, color, position, scale, euler, false);
            return WithMesh(go, c.Cone);
        }
    }
}
