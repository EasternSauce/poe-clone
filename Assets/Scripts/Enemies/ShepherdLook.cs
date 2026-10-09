using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// The act boss's first-phase look, built on the humanoid rig: a long robe down to the ground
    /// (no legs: he glides), a hood with only shadow inside, a stoop, a crook that is a dried snake
    /// (its head is the hook), and a lantern with a live flame. Rig positions are the model's, at
    /// scale 1 (feet at y 0, head centre at 1.87).
    /// </summary>
    public static class ShepherdLook
    {
        private static readonly Color Robe = new Color(0.34f, 0.33f, 0.31f);
        private static readonly Color RobeDark = new Color(0.24f, 0.23f, 0.22f);
        private static readonly Color Shadow = new Color(0.02f, 0.02f, 0.03f);
        private static readonly Color SnakeHide = new Color(0.36f, 0.33f, 0.20f);
        private static readonly Color SnakeBelly = new Color(0.55f, 0.50f, 0.32f);
        private static readonly Color Iron = new Color(0.16f, 0.15f, 0.14f);
        private static readonly Color Flame = new Color(1.0f, 0.72f, 0.30f);

        public const float Stoop = 24f;

        // The phase-1 parts that come off when he drops the disguise (hood, mantle, sleeves).
        public const string Disguise = "Disguise";

        private static readonly Color Flesh = new Color(0.60f, 0.62f, 0.54f);
        private static readonly Color FleshDark = new Color(0.36f, 0.38f, 0.32f);
        private static readonly Color SlitEyes = new Color(1f, 0.82f, 0.18f);
        private static readonly Color HoodScales = new Color(0.26f, 0.28f, 0.17f);

        /// <summary>His size in phase 1 (the kind's Scale): what his moves' reach and root motion are measured at.</summary>
        public const float BaseScale = 1.5f;

        /// <summary>His size once he stands up out of the disguise: twice phase 1, and every reach and warning with it.</summary>
        public const float Phase2Scale = 3f;

        public static void Build(Transform model)
        {
            Transform upper = Find(model, "UpperOffset");
            if (upper == null)
                return;

            // No legs, no tunic, no eyes: the robe hides the first two, the hood swallows the face.
            foreach (string part in new[] { "LegL", "LegR", "TunicSkirt", "EyeL", "EyeR" })
            {
                Transform t = Find(model, part);
                if (t != null)
                    t.gameObject.SetActive(false);
            }

            BuildRobe(model, upper);
            BuildHood(upper);

            Transform root = model.parent != null ? model.parent : model;
            var anim = model.gameObject.AddComponent<ShepherdAnimator>();
            anim.Root = root;
            anim.UpperBody = Find(model, "UpperBody");
            Transform armR = Find(model, "ArmR"), armL = Find(model, "ArmL");
            anim.ArmR = armR;
            anim.ArmL = armL;
            anim.ElbowR = armR != null ? Find(armR, "Elbow") : null;
            anim.ElbowL = armL != null ? Find(armL, "Elbow") : null;

            Transform main = Find(model, "Socket_MainHand");
            if (main != null)
                anim.Crook = BuildCrook(main, root, anim);
            Transform off = Find(model, "Socket_OffHand");
            if (off != null)
                anim.Lantern = BuildLantern(off, root);

            // An old man's arms hang at his sides, not raised like a monster's.
            CharacterWalkAnimator walk = model.GetComponent<CharacterWalkAnimator>();
            if (walk != null)
            {
                walk.Hunch = Stoop;
                walk.SetArmRestAngle(0f);
            }
        }

        /// <summary>
        /// Phase 2: the disguise comes off. Hood, mantle and sleeves go, showing a gaunt grey body
        /// and a bald, faceless head split by a seam, with slit eyes and a cobra's hood behind it.
        /// The lantern arm is gone; in its place grows a living snake (the crook, grafted on), and
        /// three more snakes sprout from his back. He stands straight and taller. The lantern is
        /// left lying where he stood, still burning.
        /// </summary>
        public static void ToPhase2(Transform root)
        {
            Transform model = root.Find("Model") ?? root;
            Transform upper = Find(model, "UpperOffset");
            if (upper == null)
                return;

            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == Disguise)
                    t.gameObject.SetActive(false);
            }

            ShepherdAnimator anim = model.GetComponent<ShepherdAnimator>();
            if (anim != null)
            {
                if (anim.Crook != null)
                    anim.Crook.gameObject.SetActive(false);
                if (anim.Lantern != null)
                    DropLantern(anim.Lantern, root);
                anim.Lantern = null;
            }
            Transform armL = Find(model, "ArmL");
            if (armL != null)
                armL.gameObject.SetActive(false);

            // Bare grey flesh where the robe was.
            Paint(Find(upper, "Torso"), Flesh);
            Transform armR = Find(model, "ArmR");
            if (armR != null)
                Paint(Find(armR, "Forearm"), Flesh);
            Transform head = Find(upper, "Head");
            if (head != null)
            {
                Paint(head, Flesh);
                head.localPosition = new Vector3(0f, 1.93f, 0f);
                head.localScale = new Vector3(0.46f, 0.66f, 0.50f);
            }
            Part(PrimitiveType.Cube, upper, Shadow, new Vector3(0f, 1.88f, 0.245f), new Vector3(0.04f, 0.46f, 0.03f));
            Part(PrimitiveType.Cube, upper, SlitEyes, new Vector3(0.11f, 1.99f, 0.215f), new Vector3(0.035f, 0.12f, 0.03f));
            Part(PrimitiveType.Cube, upper, SlitEyes, new Vector3(-0.11f, 1.99f, 0.215f), new Vector3(0.035f, 0.12f, 0.03f));
            // The cobra's hood flaring behind the head: dark scales behind, paler underside in front.
            Part(PrimitiveType.Sphere, upper, HoodScales, new Vector3(0f, 1.86f, -0.16f), new Vector3(1.05f, 0.95f, 0.12f));
            Part(PrimitiveType.Sphere, upper, SnakeBelly, new Vector3(0f, 1.84f, -0.10f), new Vector3(0.80f, 0.74f, 0.06f));

            // The graft: a snake growing out of the torn shoulder, out to the side and a little up,
            // curling forward round in front of him at shoulder height (raised like a cobra, so even
            // coiled tight it never reaches down into the ground).
            Part(PrimitiveType.Sphere, upper, FleshDark, new Vector3(-0.46f, 1.52f, 0f), Vector3.one * 0.40f);
            SnakeLimb arm = SnakeLimb.Build(upper, "SnakeArm", new Vector3(-0.52f, 1.50f, 0f),
                Quaternion.LookRotation(Vector3.forward, new Vector3(-1f, 0.35f, 0f).normalized),
                36, 3.4f, 0.34f, 0.18f, SnakeHide, SnakeBelly, SlitEyes);
            // Long enough to strike far, held coiled in an S at his side until it does.
            arm.Bend = new Vector3(3.6f, 0f, -1.6f);
            arm.Coil = 14f;
            arm.Sway = 3.6f;

            // Snakes sprouting from his back, fanning up over his shoulders.
            for (int i = 0; i < 3; i++)
            {
                SnakeLimb back = SnakeLimb.Build(upper, "BackSnake" + i, new Vector3(-0.22f + 0.22f * i, 1.42f, -0.22f),
                    Quaternion.Euler(-35f, 0f, 35f - 35f * i), 16, 1.15f, 0.21f, 0.12f, SnakeHide, SnakeBelly, SlitEyes);
                back.Bend = new Vector3(4.4f, 0f, 0f);
                back.Sway = 8.8f;
                back.SwaySpeed = 3.5f + i * 0.4f;
            }

            CharacterWalkAnimator walk = model.GetComponent<CharacterWalkAnimator>();
            if (walk != null)
                walk.Hunch = 0f;

            // New parts (snakes, hood, slit eyes): the highlight outline is made again to cover them.
            Outline outline = root.GetComponentInChildren<Outline>();
            if (outline != null)
                outline.Rebuild();

            // Taller, standing on the same ground (the model's feet are 1 below the root at scale 1).
            float grow = Phase2Scale - root.localScale.y;
            root.localScale = Vector3.one * Phase2Scale;
            root.position += Vector3.up * grow;
        }

        // Out of his hand and onto the ground beside him, tipped on its side, still alight.
        private static void DropLantern(Transform lantern, Transform root)
        {
            float feet = root.position.y - root.localScale.y;
            Vector3 at = root.position - root.right * 1.0f * root.localScale.x + root.forward * 0.4f;
            lantern.SetParent(root.parent, true);
            UprightStaff upright = lantern.GetComponent<UprightStaff>();
            if (upright != null)
                Object.Destroy(upright);
            lantern.position = new Vector3(at.x, feet + 0.2f, at.z);
            lantern.rotation = Quaternion.Euler(0f, root.eulerAngles.y + 60f, 90f);
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static void Paint(Transform t, Color color)
        {
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null)
                return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
        }

        // A skirt from the waist to the floor, flaring at the hem, on the model (so the stoop leans
        // the chest over it rather than tipping it); sleeves over the upper arms; a mantle over the
        // shoulders.
        private static void BuildRobe(Transform model, Transform upper)
        {
            GameObject skirt = Part(PrimitiveType.Cylinder, model, Robe, new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.8f));
            skirt.GetComponent<MeshFilter>().sharedMesh = SkirtMesh;

            foreach (string arm in new[] { "ArmL", "ArmR" })
            {
                Transform a = Find(upper, arm);
                if (a != null)
                    Part(PrimitiveType.Cube, a, Robe, new Vector3(0f, -0.20f, 0f), new Vector3(0.30f, 0.46f, 0.32f)).name = Disguise;
            }

            Part(PrimitiveType.Sphere, upper, RobeDark, new Vector3(0f, 1.52f, -0.02f), new Vector3(1.12f, 0.40f, 0.62f)).name = Disguise;
        }

        private static Mesh skirtMesh;

        // An open-ended truncated cone: radius 0.32 at the waist (y 1.05) to 0.56 at the hem (y 0).
        // One-sided: a doubled skin would fill the outline's inverted hull and light the robe up.
        private static Mesh SkirtMesh
        {
            get
            {
                if (skirtMesh != null)
                    return skirtMesh;
                const int Sides = 20;
                const float Top = 1.05f, TopRadius = 0.32f, HemRadius = 0.56f;
                var vertices = new Vector3[(Sides + 1) * 2];
                var normals = new Vector3[vertices.Length];
                var triangles = new int[Sides * 6];
                for (int i = 0; i <= Sides; i++)
                {
                    float a = 2f * Mathf.PI * i / Sides;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    vertices[i * 2] = dir * HemRadius;
                    vertices[i * 2 + 1] = dir * TopRadius + Vector3.up * Top;
                    Vector3 n = (dir * Top + Vector3.up * (HemRadius - TopRadius)).normalized;
                    normals[i * 2] = normals[i * 2 + 1] = n;
                }
                for (int i = 0; i < Sides; i++)
                {
                    int b = i * 2, t = i * 2 + 1, b2 = i * 2 + 2, t2 = i * 2 + 3, k = i * 6;
                    triangles[k] = b; triangles[k + 1] = b2; triangles[k + 2] = t;
                    triangles[k + 3] = b2; triangles[k + 4] = t2; triangles[k + 5] = t;
                }
                skirtMesh = new Mesh { name = "ShepherdSkirt", vertices = vertices, normals = normals, triangles = triangles };
                skirtMesh.RecalculateBounds();
                return skirtMesh;
            }
        }

        // A deep hood over the head, and a dark oval in its opening where a face should be.
        private static void BuildHood(Transform upper)
        {
            Part(PrimitiveType.Sphere, upper, Robe, new Vector3(0f, 1.90f, -0.06f), new Vector3(0.66f, 0.66f, 0.66f)).name = Disguise;
            // Its back falls onto the mantle.
            Part(PrimitiveType.Sphere, upper, Robe, new Vector3(0f, 1.70f, -0.20f), new Vector3(0.56f, 0.50f, 0.40f)).name = Disguise;
            Part(PrimitiveType.Sphere, upper, Shadow, new Vector3(0f, 1.86f, 0.24f), new Vector3(0.38f, 0.44f, 0.10f)).name = Disguise;
        }

        // A staff of dried snake, held upright: the body runs down as the shaft, and at the top it
        // curls forward and down into the hook, ending in the head. The curl is a chain of joints
        // (base first), so the animator can straighten it into a striking snake or make it writhe.
        private const int CurlJoints = 12;
        private const float CurlArc = 207f;
        private const float CurlLength = 0.80f;

        private static Transform BuildCrook(Transform hand, Transform body, ShepherdAnimator anim)
        {
            var crook = new GameObject("Crook");
            crook.transform.SetParent(hand, false);
            crook.AddComponent<UprightStaff>().Bind(body);
            Transform t = crook.transform;

            Part(PrimitiveType.Cylinder, t, SnakeHide, new Vector3(0f, 0.30f, 0f), new Vector3(0.08f, 1.0f, 0.08f));

            float step = CurlLength / CurlJoints;
            var joints = new Transform[CurlJoints];
            Transform parent = t;
            for (int i = 0; i < CurlJoints; i++)
            {
                var joint = new GameObject("Curl" + i).transform;
                joint.SetParent(parent, false);
                joint.localPosition = i == 0 ? new Vector3(0f, 1.28f, 0f) : new Vector3(0f, step, 0f);
                joint.localRotation = Quaternion.Euler(CurlArc / CurlJoints, 0f, 0f);
                float size = Mathf.Lerp(0.12f, 0.09f, i / (float)(CurlJoints - 1));
                Part(PrimitiveType.Sphere, joint, SnakeHide, new Vector3(0f, step * 0.5f, 0f), Vector3.one * size);
                joints[i] = joint;
                parent = joint;
            }

            // The head on the end of the curl, snout leading; pale eyes on its sides.
            Part(PrimitiveType.Sphere, parent, SnakeHide, new Vector3(0f, step + 0.07f, 0f), new Vector3(0.12f, 0.18f, 0.10f));
            Part(PrimitiveType.Sphere, parent, SnakeBelly, new Vector3(0f, step + 0.14f, 0.02f), new Vector3(0.07f, 0.07f, 0.06f));
            Part(PrimitiveType.Sphere, parent, SnakeBelly, new Vector3(0.055f, step + 0.09f, 0f), Vector3.one * 0.03f);
            Part(PrimitiveType.Sphere, parent, SnakeBelly, new Vector3(-0.055f, step + 0.09f, 0f), Vector3.one * 0.03f);

            anim.CurlJoints = joints;
            anim.CurlStep = CurlArc / CurlJoints;
            return t;
        }

        // An iron lantern on a short handle, its flame a bright core with a flickering light.
        private static Transform BuildLantern(Transform hand, Transform body)
        {
            var lantern = new GameObject("Lantern");
            lantern.transform.SetParent(hand, false);
            lantern.AddComponent<UprightStaff>().Bind(body);
            Transform t = lantern.transform;

            Part(PrimitiveType.Cube, t, Iron, new Vector3(0f, -0.04f, 0f), new Vector3(0.04f, 0.10f, 0.04f));
            Part(PrimitiveType.Cube, t, Iron, new Vector3(0f, -0.12f, 0f), new Vector3(0.24f, 0.04f, 0.24f));
            Part(PrimitiveType.Cube, t, Iron, new Vector3(0f, -0.44f, 0f), new Vector3(0.26f, 0.05f, 0.26f));
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? 1f : -1f) * 0.10f;
                float z = (i < 2 ? 1f : -1f) * 0.10f;
                Part(PrimitiveType.Cube, t, Iron, new Vector3(x, -0.28f, z), new Vector3(0.025f, 0.30f, 0.025f));
            }
            LivingFlame.Attach(Part(PrimitiveType.Sphere, t, Flame, new Vector3(0f, -0.28f, 0f), new Vector3(0.15f, 0.20f, 0.15f)), 0.2f);

            var glow = new GameObject("LanternGlow");
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Flame;
            light.range = 7f;
            light.intensity = 6f;
            light.shadows = LightShadows.None;
            TorchFlicker flicker = glow.AddComponent<TorchFlicker>();
            flicker.torchLight = light;
            flicker.baseIntensity = light.intensity;
            flicker.flickerAmount = light.intensity * 0.2f;
            flicker.flickerSpeed = 7f;
            ManualPointLightManager.Refresh();
            return t;
        }

        private static GameObject Part(PrimitiveType type, Transform parent, Color color, Vector3 position, Vector3 scale)
        {
            GameObject go = RuntimePrimitives.Create(type, parent, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            return go;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }
    }
}
