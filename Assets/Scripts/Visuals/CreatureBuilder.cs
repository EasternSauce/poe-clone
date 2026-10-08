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
        Beetle,   // six short legs under a heavy shell, a horn; spits
        Briarbound,
        GraveSiren,
        RimeStalker,
        CinderPenitent,
        Hollowmaw,
        BarrowCastellan
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
                case CreatureBody.Briarbound: BuildBriarbound(model, ctx); break;
                case CreatureBody.GraveSiren: BuildGraveSiren(model, ctx); break;
                case CreatureBody.RimeStalker: BuildRimeStalker(model, ctx); break;
                case CreatureBody.CinderPenitent: BuildCinderPenitent(model, ctx); break;
                case CreatureBody.Hollowmaw: BuildHollowmaw(model, ctx); break;
                case CreatureBody.BarrowCastellan: BuildBarrowCastellan(model, ctx); break;
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

        // Dark fantasy bodies with their own proportions, joints and silhouettes.
        private static void BuildBriarbound(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Briarbound", new Vector3(0f, 1.25f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.55f, 0.85f, 0.38f));
            Ico(c, body, c.Colors.Second, new Vector3(0f, -0.38f, -0.02f), new Vector3(0.36f, 0.4f, 0.3f));
            Transform head = Pivot(body, "Head", new Vector3(0f, 0.62f, 0.08f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Second, Vector3.zero, new Vector3(0.32f, 0.46f, 0.3f));
            // The face is split by a bark ridge; only two small pale eyes shine through.
            Cone(c, head, c.Colors.Main, new Vector3(0f, -0.14f, 0.16f), new Vector3(0.035f, 0.32f, 0.04f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.09f, 0.04f, 0.145f), new Vector3(0.07f, 0.035f, 0.025f), new Vector3(0f, 0f, side * 15f), false);
                float crown = side < 0 ? 0.85f : 0.65f;
                Cone(c, head, c.Colors.Main, new Vector3(side * 0.11f, 0.14f, -0.06f), new Vector3(0.075f, crown, 0.075f), new Vector3(-10f, 0f, side * -28f));
                Cone(c, head, c.Colors.Second, new Vector3(side * 0.30f, 0.49f, -0.12f), new Vector3(0.045f, crown * 0.5f, 0.045f), new Vector3(0f, 0f, side * -65f));
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.57f, 0.62f);
                AddRevenantLimb(c, body, side, true, c.Colors.Second, 0.5f, 0.58f);
                for (int i = 0; i < 3; i++)
                {
                    // Bark plates protrude through the torso like ribs, with moss in the seams.
                    Part(body, PrimitiveType.Cube, c.Colors.Second, new Vector3(side * 0.17f, 0.23f - i * 0.19f, 0.18f), new Vector3(0.23f, 0.07f, 0.07f), new Vector3(0f, side * 22f, side * -18f));
                    Cone(c, body, c.Colors.Accent, new Vector3(side * 0.28f, 0.12f - i * 0.18f, -0.08f), new Vector3(0.09f, 0.24f, 0.08f), new Vector3(0f, 0f, side * -65f));
                }
            }
            c.Anim.Mouth = Pivot(head, CreatureAnimator.MouthName, new Vector3(0f, -0.08f, 0.22f));
        }

        private static void BuildGraveSiren(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "GraveSiren", new Vector3(0f, 1.35f, 0f));
            c.Anim.BodyPivot = body;
            Color bone = new Color(0.64f, 0.65f, 0.58f);
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.32f, 0.66f, 0.23f));
            Transform head = Pivot(body, "Head", new Vector3(0f, 0.54f, 0.08f));
            c.Anim.Head = head;
            Ico(c, head, bone, new Vector3(0f, 0.05f, 0f), new Vector3(0.28f, 0.36f, 0.26f));
            Part(head, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, -0.12f, 0.135f), new Vector3(0.13f, 0.21f, 0.045f), Vector3.zero);
            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.13f, 0.06f));
            c.Anim.Jaw = jaw;
            Ico(c, jaw, bone, new Vector3(0f, -0.15f, 0.025f), new Vector3(0.21f, 0.23f, 0.16f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.08f, 0.06f, 0.13f), new Vector3(0.055f, 0.025f, 0.025f), Vector3.zero, false);
                Cone(c, head, c.Colors.Accent, new Vector3(side * 0.12f, 0.14f, -0.03f), new Vector3(0.1f, 0.78f, 0.12f), new Vector3(180f, 0f, side * -12f));
                AddRevenantLimb(c, body, side, true, bone, 0.43f, 0.54f);
                for (int i = 0; i < 4; i++)
                    Part(body, PrimitiveType.Cube, bone, new Vector3(side * 0.11f, 0.19f - i * 0.11f, 0.13f), new Vector3(0.19f, 0.04f, 0.05f), new Vector3(0f, side * 25f, side * -15f));
            }
            Transform shroud = Pivot(body, "BurialShroud", new Vector3(0f, -0.25f, 0f));
            c.Anim.Tail = shroud;
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Cone(c, shroud, i % 2 == 0 ? c.Colors.Second : c.Colors.Accent, new Vector3(Mathf.Cos(a) * 0.14f, -0.02f, Mathf.Sin(a) * 0.12f), new Vector3(0.16f, 0.85f + (i % 3) * 0.1f, 0.1f), new Vector3(175f, i * 51f, Mathf.Cos(a) * 14f));
            }
            c.Anim.Mouth = Pivot(head, CreatureAnimator.MouthName, new Vector3(0f, -0.17f, 0.22f));
        }

        private static void BuildRimeStalker(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "RimeStalker", new Vector3(0f, 1.15f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, new Vector3(0f, 0f, -0.05f), new Vector3(0.5f, 0.74f, 0.38f));
            Transform head = Pivot(body, "Head", new Vector3(0f, 0.39f, 0.33f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Main, Vector3.zero, new Vector3(0.32f, 0.32f, 0.4f));
            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.12f, 0.09f));
            c.Anim.Jaw = jaw;
            Ico(c, jaw, c.Colors.Accent, new Vector3(0f, -0.03f, 0.1f), new Vector3(0.25f, 0.12f, 0.3f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.12f, 0.04f, 0.17f), new Vector3(0.065f, 0.035f, 0.03f), new Vector3(0f, 0f, side * 25f), false);
                Cone(c, head, c.Colors.Accent, new Vector3(side * 0.12f, 0.2f, -0.04f), new Vector3(0.09f, 0.44f, 0.14f), new Vector3(-35f, 0f, side * -20f));
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.48f, 0.56f);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.48f, 0.64f);
                Cone(c, jaw, c.Colors.Second, new Vector3(side * 0.085f, 0.04f, 0.21f), new Vector3(0.04f, 0.14f, 0.04f), Vector3.zero);
            }
            for (int i = 0; i < 6; i++)
                Cone(c, body, c.Colors.Second, new Vector3(0.12f + (i % 2) * 0.18f, 0.32f - i * 0.09f, -0.2f), new Vector3(0.16f, 0.6f - i * 0.045f, 0.18f), new Vector3(-40f - i * 6f, 0f, -24f));
            c.Anim.Mouth = Pivot(head, CreatureAnimator.MouthName, new Vector3(0f, -0.08f, 0.3f));
        }

        private static void BuildCinderPenitent(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "CinderPenitent", new Vector3(0f, 0.72f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, new Vector3(0f, 0.04f, -0.06f), new Vector3(0.48f, 0.65f, 0.35f));
            Transform head = Pivot(body, "BoundHead", new Vector3(0f, 0.39f, 0.18f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Main, Vector3.zero, new Vector3(0.27f, 0.35f, 0.27f));
            for (int i = 0; i < 3; i++)
                Part(head, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0.12f - i * 0.11f, 0.12f), new Vector3(0.3f, 0.04f, 0.08f), new Vector3(0f, 0f, i % 2 == 0 ? 12f : -15f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.08f, 0.025f, 0.15f), new Vector3(0.035f, 0.022f, 0.025f), Vector3.zero, false);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.48f, 0.58f);
                var arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                arm.Hip.localPosition = new Vector3(side * 0.28f, 0.15f, 0.04f);
                arm.Hip.localRotation = Quaternion.Euler(-35f, 0f, side * 12f);
                arm.Knee.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                Ico(c, arm.Knee, c.Colors.Main, new Vector3(0f, -0.58f, 0.04f), new Vector3(0.25f, 0.12f, 0.28f));
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.24f, 0.27f);
                var leg = c.Anim.Legs[c.Anim.Legs.Count - 1];
                leg.Hip.localRotation = Quaternion.Euler(-65f, 0f, side * 10f);
                leg.Knee.localRotation = Quaternion.Euler(110f, 0f, 0f);
                // Glowing flesh visible between charred ribs and snapped ritual bands.
                for (int i = 0; i < 4; i++)
                {
                    Part(body, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.1f, 0.20f - i * 0.105f, 0.17f), new Vector3(0.13f, 0.028f, 0.045f), new Vector3(0f, side * 20f, side * 15f), false);
                    Part(body, PrimitiveType.Cube, c.Colors.Second, new Vector3(side * 0.18f, 0.21f - i * 0.105f, 0.13f), new Vector3(0.11f, 0.045f, 0.07f), new Vector3(0f, 0f, side * 18f));
                }
            }
            Cone(c, body, c.Colors.Second, new Vector3(0f, -0.28f, -0.12f), new Vector3(0.29f, 0.46f, 0.09f), new Vector3(135f, 0f, 0f));
        }

        private static void BuildHollowmaw(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Hollowmaw", new Vector3(0f, 1.1f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, new Vector3(0f, 0.12f, -0.12f), new Vector3(0.72f, 0.78f, 0.55f));
            Transform head = Pivot(body, "EyelessHead", new Vector3(0f, 0.45f, 0.18f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Main, Vector3.zero, new Vector3(0.42f, 0.4f, 0.38f));
            Ico(c, head, c.Colors.Second, new Vector3(0f, 0.07f, 0.17f), new Vector3(0.43f, 0.14f, 0.12f));
            // A continuous vertical throat from the chin into the chest, bordered by teeth.
            Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.04f, 0.27f), new Vector3(0.18f, 0.86f, 0.06f), Vector3.zero);
            Transform jaw = Pivot(body, "SplitJaw", new Vector3(0.1f, 0.18f, 0.24f));
            c.Anim.Jaw = jaw;
            for (int side = -1; side <= 1; side += 2)
            {
                Transform parent = side > 0 ? jaw : body;
                Vector3 offset = side > 0 ? -jaw.localPosition : Vector3.zero;
                Ico(c, parent, c.Colors.Main, offset + new Vector3(side * 0.12f, 0.03f, 0.27f), new Vector3(0.15f, 0.85f, 0.14f));
                for (int i = 0; i < 6; i++)
                    Cone(c, parent, Bone, offset + new Vector3(side * 0.095f, 0.35f - i * 0.13f, 0.34f), new Vector3(0.018f, 0.1f, 0.025f), new Vector3(0f, 0f, side * 75f));
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.4f, 0.48f);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.45f, 0.6f);
                var arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                arm.Hip.localPosition = new Vector3(side * 0.42f, 0.25f, -0.02f);
                Ico(c, arm.Knee, c.Colors.Second, new Vector3(0f, -0.58f, 0.04f), new Vector3(0.34f, 0.22f, 0.26f));
            }
            for (int i = 0; i < 4; i++)
                Ico(c, body, c.Colors.Second, new Vector3(0f, 0.4f - i * 0.17f, -0.37f), new Vector3(0.14f, 0.19f, 0.12f));
            c.Anim.Mouth = Pivot(body, CreatureAnimator.MouthName, new Vector3(0f, 0.12f, 0.38f));
        }

        private static void BuildBarrowCastellan(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "BarrowCastellan", new Vector3(0f, 1.25f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.6f, 0.72f, 0.36f));
            Ico(c, body, c.Colors.Second, new Vector3(0f, 0.03f, 0.17f), new Vector3(0.5f, 0.62f, 0.15f));
            Transform head = Pivot(body, "CollapsedHelm", new Vector3(0f, 0.56f, 0f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Second, Vector3.zero, new Vector3(0.38f, 0.44f, 0.34f));
            Part(head, PrimitiveType.Cube, Dark, new Vector3(0f, -0.01f, 0.17f), new Vector3(0.26f, 0.12f, 0.035f), Vector3.zero);
            Ico(c, head, Bone, new Vector3(0.035f, -0.14f, 0.18f), new Vector3(0.14f, 0.12f, 0.075f));
            Part(head, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(-0.06f, 0.01f, 0.2f), new Vector3(0.045f, 0.022f, 0.025f), Vector3.zero, false);
            Part(head, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.17f, -0.03f), new Vector3(0.07f, 0.19f, 0.34f), new Vector3(-8f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddRevenantLimb(c, body, side, false, c.Colors.Accent, 0.55f, 0.58f);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.44f, 0.5f);
                var arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                Ico(c, arm.Hip, c.Colors.Second, new Vector3(0f, -0.06f, 0f), new Vector3(0.4f, 0.24f, 0.4f));
                Part(arm.Knee, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, -0.25f, 0.07f), new Vector3(0.16f, 0.36f, 0.13f), Vector3.zero);
                Cone(c, body, c.Colors.Accent, new Vector3(side * 0.16f, -0.3f, 0f), new Vector3(0.18f, 0.65f, 0.16f), new Vector3(180f, 0f, side * 10f));
                if (side > 0)
                {
                    Transform weapon = Pivot(arm.Knee, "GravePolearm", new Vector3(0f, -0.45f, 0.12f));
                    Part(weapon, PrimitiveType.Cylinder, c.Colors.Accent, new Vector3(0f, 0.4f, 0f), new Vector3(0.055f, 1.05f, 0.055f), new Vector3(0f, 0f, -8f));
                    Cone(c, weapon, c.Colors.Second, new Vector3(0.15f, 1.4f, 0f), new Vector3(0.16f, 0.6f, 0.045f), new Vector3(0f, 0f, -65f));
                    Cone(c, weapon, c.Colors.Main, new Vector3(0.12f, 1.42f, 0f), new Vector3(0.06f, 0.28f, 0.06f), Vector3.zero);
                    Part(weapon, PrimitiveType.Cube, c.Colors.Second, new Vector3(0.12f, 1.33f, 0f), new Vector3(0.35f, 0.065f, 0.12f), Vector3.zero);
                    Cone(c, weapon, c.Colors.Accent, new Vector3(0.15f, 1.15f, 0f), new Vector3(0.08f, 0.58f, 0.035f), new Vector3(180f, 0f, -20f));
                }
            }
            Transform cloak = Pivot(body, "TombMantle", new Vector3(0f, 0.24f, -0.22f));
            c.Anim.Tail = cloak;
            for (int i = 0; i < 3; i++)
                Cone(c, cloak, c.Colors.Accent, new Vector3((i - 1) * 0.18f, 0f, 0f), new Vector3(0.16f, 0.95f + i * 0.06f, 0.05f), new Vector3(160f, 0f, (i - 1) * 8f));
        }

        private static void AddRevenantLimb(Ctx c, Transform body, int side, bool arm, Color color, float upper, float lower)
        {
            Transform hip = Pivot(body, arm ? "Shoulder" : "Hip", new Vector3(side * (arm ? 0.3f : 0.16f), arm ? 0.25f : -0.35f, 0f));
            hip.localRotation = Quaternion.Euler(arm ? -12f : -10f, 0f, arm ? side * 22f : side * 7f);
            Ico(c, hip, color, Vector3.zero, Vector3.one * (arm ? 0.18f : 0.16f));
            Ico(c, hip, color, new Vector3(0f, -upper * 0.5f, 0f), new Vector3(arm ? 0.13f : 0.17f, upper, 0.14f));
            Transform knee = Pivot(hip, arm ? "Elbow" : "Knee", new Vector3(0f, -upper, 0f));
            knee.localRotation = Quaternion.Euler(arm ? -20f : 25f, 0f, 0f);
            Ico(c, knee, color, Vector3.zero, Vector3.one * 0.13f);
            Ico(c, knee, color, new Vector3(0f, -lower * 0.5f, 0f), new Vector3(0.1f, lower, 0.12f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 tip = new Vector3((i - 1) * 0.06f, -lower, 0.06f);
                Cone(c, knee, c.Colors.Second, tip, new Vector3(0.035f, arm ? 0.25f : 0.2f, 0.045f), new Vector3(arm ? 150f : 110f, 0f, (i - 1) * 15f));
            }
            c.Anim.Legs.Add(new CreatureAnimator.Leg { Hip = hip, Knee = knee, Side = side, Front = arm, Group = side < 0 ? 0 : 1,
                Lift = arm ? side * 22f : side * 7f, Bend = arm ? -20f : 25f });
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
