using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Phase-three visual rig. The robe hid a beast's folded hindquarters and grafted human arms.
    /// Named pivots are ready for the animation pass; this does not enable phase-three combat.
    /// The colossal serpents are separate attack rigs, hidden until summoned or previewed.
    /// </summary>
    public static class CarrionSaintLook
    {
        public const string RigName = "CarrionSaint";
        public const string SerpentsName = "ColossalSerpents";
        // World diameter 7.2m at phase-three scale: four times phase two's diving snake.
        public const float SerpentThickness = 2.4f;
        public const float SerpentLength = 16f;
        private static readonly Color Skin = new Color(0.63f, 0.62f, 0.52f);
        private static readonly Color Bruise = new Color(0.31f, 0.25f, 0.25f);
        private static readonly Color Beast = new Color(0.24f, 0.22f, 0.19f);
        private static readonly Color Wound = new Color(0.23f, 0.045f, 0.055f);
        private static readonly Color Black = new Color(0.035f, 0.018f, 0.025f);
        private static readonly Color Bone = new Color(0.86f, 0.81f, 0.65f);
        private static readonly Color Pale = new Color(0.73f, 0.68f, 0.65f);
        private static readonly Color Scales = new Color(0.22f, 0.28f, 0.18f);
        private static readonly Color ScaleEdge = new Color(0.40f, 0.43f, 0.25f);

        public static Transform Build(Transform root)
        {
            Transform model = root.Find("Model") ?? root;
            Transform existing = model.Find(RigName);
            if (existing != null)
                return existing;

            // Hide the whole old silhouette, including robe and phase-two snake arm.
            foreach (Transform child in model)
                child.gameObject.SetActive(false);
            var walk = model.GetComponent<CharacterWalkAnimator>();
            if (walk != null) walk.enabled = false;
            var animator = model.GetComponent<ShepherdAnimator>();
            if (animator != null) { animator.Stop(); animator.enabled = false; }
            float growth = ShepherdLook.Phase2Scale - root.localScale.y;
            root.localScale = Vector3.one * ShepherdLook.Phase2Scale;
            root.position += Vector3.up * growth;

            Transform rig = Joint(model, RigName, Vector3.zero);
            Transform trunk = Joint(rig, "Trunk", new Vector3(0f, 1.22f, -0.22f));
            Part(trunk, "Hindquarters", Beast, new Vector3(0f, -0.05f, -0.85f), new Vector3(1.25f, 0.95f, 1.65f));
            Part(trunk, "StretchedTorso", Skin, new Vector3(0f, 0.25f, 0.18f), new Vector3(1.30f, 1.20f, 1.05f));
            Part(trunk, "ShoulderHump", Bruise, new Vector3(0f, 0.68f, -0.25f), new Vector3(1.50f, 0.66f, 0.85f));
            for (int i = 0; i < 7; i++)
                Part(trunk, "Vertebra" + i, Bone, new Vector3(0f, 0.39f - i * 0.05f, -0.40f - i * 0.19f), new Vector3(0.22f, 0.23f, 0.22f));

            for (int side = -1; side <= 1; side += 2)
            {
                HindLeg(rig, side);
                HumanArm(rig, side, 0, 0.68f);
                HumanArm(rig, side, 1, -0.42f);
                // Pulled-open ribs flank the vertical belly mouth.
                for (int i = 0; i < 5; i++)
                    Link(trunk, "Rib" + side + "_" + i, Bone,
                        new Vector3(side * 0.60f, 0.64f - i * 0.18f, 0.27f),
                        new Vector3(side * 0.32f, 0.53f - i * 0.18f, 0.71f), 0.055f);
            }
            BellyMaw(trunk);
            SplitHead(trunk);
            SerpentFeatures(rig, trunk);
            Tentacles(trunk);
            BuildSerpents(rig);

            // The old humanoid model origin is below its controller centre. Ground the new
            // feet by their visible bounds, including when previewing directly from phase one.
            float lowest = float.MaxValue;
            foreach (Transform limb in rig)
                if (limb.name.StartsWith("BeastHip") || limb.name.StartsWith("VictimArm"))
                    foreach (Renderer renderer in limb.GetComponentsInChildren<Renderer>())
                        lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            if (lowest < float.MaxValue)
                rig.position += Vector3.up * (Debris.GroundBelow(root.position + Vector3.up * 5f) - lowest);

            Outline outline = root.GetComponentInChildren<Outline>();
            if (outline != null) outline.Rebuild();
            rig.gameObject.AddComponent<CarrionSaintAnimator>();
            return rig;
        }

        private static void HindLeg(Transform rig, int side)
        {
            Transform hip = Joint(rig, "BeastHip" + side, new Vector3(side * 0.50f, 1.23f, -1.04f));
            Vector3 knee = new Vector3(side * 0.32f, -0.35f, 0.22f);
            Link(hip, "Thigh", Beast, Vector3.zero, knee, 0.36f);
            Transform bend = Joint(hip, "Knee", knee);
            Vector3 hock = new Vector3(side * 0.08f, -0.46f, -0.65f);
            Link(bend, "Shin", Skin, Vector3.zero, hock, 0.18f);
            Transform ankle = Joint(bend, "BackwardHock", hock);
            Vector3 foot = new Vector3(side * 0.10f, -0.31f, 0.28f);
            Link(ankle, "Ankle", Beast, Vector3.zero, foot, 0.13f);
            Transform toes = Joint(ankle, "Foot", foot);
            Part(toes, "Hoof", Black, Vector3.zero, new Vector3(0.32f, 0.17f, 0.48f));
            for (int i = -1; i <= 1; i++)
                Link(toes, "Claw" + i, Bone, new Vector3(i * 0.11f, 0f, 0.12f), new Vector3(i * 0.14f, -0.02f, 0.40f), 0.045f);
        }

        private static void HumanArm(Transform rig, int side, int index, float z)
        {
            Transform shoulder = Joint(rig, "VictimArm" + side + "_" + index, new Vector3(side * 0.56f, 1.38f, z));
            Part(shoulder, "GraftScar", Wound, Vector3.zero, new Vector3(0.36f, 0.39f, 0.38f));
            Vector3 elbow = new Vector3(side * (0.55f + index * 0.12f), -0.45f, -0.18f);
            Link(shoulder, "UpperArm", Skin, Vector3.zero, elbow, 0.22f);
            Transform forearm = Joint(shoulder, "Elbow", elbow);
            Vector3 wrist = new Vector3(side * 0.10f, -0.74f, 0.47f);
            Link(forearm, "Forearm", Pale, Vector3.zero, wrist, 0.16f);
            Transform hand = Joint(forearm, "Wrist", wrist);
            Part(hand, "Palm", Skin, new Vector3(0f, -0.03f, 0.08f), new Vector3(0.27f, 0.12f, 0.35f));
            for (int finger = 0; finger < 5; finger++)
            {
                float x = (finger - 2) * 0.072f;
                Vector3 knuckle = new Vector3(x, -0.02f, 0.18f);
                Vector3 tip = new Vector3(x * 1.65f, -0.08f, 0.45f - Mathf.Abs(finger - 2) * 0.05f);
                Link(hand, "Finger" + finger, Pale, knuckle, tip, 0.052f);
                Part(hand, "Nail" + finger, Black, tip, new Vector3(0.045f, 0.035f, 0.10f));
            }
        }

        private static void BellyMaw(Transform trunk)
        {
            Transform maw = Joint(trunk, "BellyMaw", new Vector3(0f, 0.13f, 0.65f));
            Part(maw, "TornLip", Wound, Vector3.zero, new Vector3(0.68f, 1.12f, 0.21f));
            Part(maw, "Cavity", Black, new Vector3(0f, 0f, 0.09f), new Vector3(0.47f, 0.96f, 0.15f));
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 at = new Vector3(side * 0.24f, -0.40f + i * 0.115f, 0.17f);
                    Link(maw, "Tooth" + side + "_" + i, Bone, at,
                        at + new Vector3(-side * (i % 2 == 0 ? 0.16f : 0.11f), -0.04f, 0.025f), 0.045f);
                }
        }

        private static void SplitHead(Transform trunk)
        {
            Transform neck = Joint(trunk, "Neck", new Vector3(0f, 0.78f, 0.40f));
            Part(neck, "NeckFlesh", Skin, Vector3.zero, new Vector3(0.38f, 0.53f, 0.38f));
            Part(neck, "OpenSeam", Wound, new Vector3(0f, 0.34f, 0.13f), new Vector3(0.50f, 0.57f, 0.34f));
            for (int side = -1; side <= 1; side += 2)
            {
                Transform half = Joint(neck, "SplitSkull" + side, new Vector3(side * 0.24f, 0.35f, 0.05f));
                half.localRotation = Quaternion.Euler(0f, side * 28f, -side * 23f);
                Part(half, "SkullSkin", Skin, Vector3.zero, new Vector3(0.28f, 0.62f, 0.43f));
                Part(half, "EyeSocket", Black, new Vector3(side * 0.045f, 0.065f, 0.19f), new Vector3(0.10f, 0.13f, 0.08f));
                Part(half, "SlitEye", new Color(1f, 0.72f, 0.12f), new Vector3(side * 0.045f, 0.065f, 0.23f), new Vector3(0.027f, 0.10f, 0.028f));
                for (int i = 0; i < 5; i++)
                    Link(half, "SeamTooth" + i, Bone, new Vector3(-side * 0.11f, -0.22f + i * 0.10f, 0.12f),
                        new Vector3(-side * 0.23f, -0.25f + i * 0.10f, 0.19f), 0.038f);
            }
        }

        private static void SerpentFeatures(Transform rig, Transform trunk)
        {
            // Two living serpents remain visible beside the seam tendrils between attacks.
            // Their animated curves replace the broad plates that read as folded wings.
            for (int side = -1; side <= 1; side += 2)
            {
                SnakeLimb viper = SnakeLimb.Build(trunk, "ShoulderViper" + side,
                    new Vector3(side * 0.70f, 0.45f, -0.28f), Quaternion.Euler(-55f, side * 35f, -side * 30f),
                    24, 3.6f, 0.32f, 0.15f, Scales, ScaleEdge, new Color(1f, 0.72f, 0.12f));
                viper.transform.localScale = Vector3.one * 0.75f;
                viper.Bend = new Vector3(12f, 0f, side * 5f);
                viper.Coil = 14f;
                viper.Sway = 8f;
                viper.SwaySpeed = 1.2f;
                viper.JawOpen = 18f;
            }
            Transform armour = Joint(trunk, "DorsalScales", Vector3.zero);
            for (int i = 0; i < 8; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform plate = Joint(armour, "Plate" + side + "_" + i, new Vector3(side * 0.32f, 0.67f - i * 0.045f, -0.26f - i * 0.19f));
                    plate.localRotation = Quaternion.Euler(0f, side * 18f, side * 22f);
                    Part(plate, "Scale", i % 2 == 0 ? Scales : ScaleEdge, Vector3.zero, new Vector3(0.60f, 0.20f, 0.42f));
                }
            Transform tail = Joint(rig, "SerpentTail", new Vector3(0f, 1.18f, -1.48f));
            Vector3 prior = Vector3.zero;
            for (int i = 1; i <= 24; i++)
            {
                float u = i / 24f;
                Vector3 next = new Vector3(Mathf.Sin(u * 5f) * u * 0.95f, -0.88f * Mathf.Sin(u * Mathf.PI * 0.5f), -u * 3.6f);
                Link(tail, "TailSegment" + i, i % 4 == 0 ? ScaleEdge : Scales, prior, next, Mathf.Lerp(0.55f, 0.035f, u));
                prior = next;
            }
        }

        private static void Tentacles(Transform trunk)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    Transform seam = Joint(trunk, "SeamTentacle" + side + "_" + i, new Vector3(side * 0.57f, 0.40f - i * 0.23f, -0.15f));
                    Part(seam, "Seam", Wound, Vector3.zero, new Vector3(0.13f, 0.36f, 0.18f));
                    Vector3 prior = Vector3.zero;
                    for (int j = 1; j <= 12; j++)
                    {
                        float u = j / 12f;
                        Vector3 next = new Vector3(side * u * (1.5f + i * 0.22f), Mathf.Sin(u * 5f + i * 0.7f) * u * 0.48f, u * (0.35f + i * 0.20f));
                        Link(seam, "Segment" + j, Pale, prior, next, Mathf.Lerp(0.12f, 0.018f, u));
                        prior = next;
                    }
                    // Stitches across the wound where the tendril broke through.
                    for (int stitch = 0; stitch < 3; stitch++)
                        Link(seam, "Stitch" + stitch, Black, new Vector3(-0.08f, -0.12f + stitch * 0.10f, 0.10f),
                            new Vector3(0.08f, -0.08f + stitch * 0.10f, 0.10f), 0.018f);
                }
        }

        private static void BuildSerpents(Transform rig)
        {
            Transform attacks = Joint(rig, SerpentsName, Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                SnakeLimb snake = SnakeLimb.Build(attacks, "ColossalSerpent" + side,
                    new Vector3(side * 0.56f, 1.64f, -0.72f), Quaternion.Euler(-24f, side * 58f, -side * 35f),
                    48, SerpentLength, SerpentThickness, 1.8f,
                    new Color(0.20f, 0.23f, 0.17f), new Color(0.45f, 0.42f, 0.29f), new Color(1f, 0.60f, 0.10f), true);
                snake.Bend = new Vector3(2.4f, 0f, side * 0.9f);
                snake.Coil = 5f;
                snake.Sway = 0.4f;
                snake.SwaySpeed = 0.7f;
                snake.JawOpen = 24f;
            }
            attacks.gameObject.SetActive(false);
        }

        public static void ShowSerpents(Transform root, bool show)
        {
            Transform model = root.Find("Model") ?? root;
            Transform group = model.Find(RigName + "/" + SerpentsName);
            if (group != null) group.gameObject.SetActive(show);
        }

        private static Transform Joint(Transform parent, string name, Vector3 at)
        {
            Transform joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = at;
            return joint;
        }

        private static void Part(Transform parent, string name, Color color, Vector3 at, Vector3 size)
        {
            GameObject part = RuntimePrimitives.Create(PrimitiveType.Sphere, parent, color);
            part.name = name;
            part.transform.localPosition = at;
            part.transform.localScale = size;
        }

        private static void Link(Transform parent, string name, Color color, Vector3 a, Vector3 b, float width)
        {
            GameObject part = RuntimePrimitives.Create(PrimitiveType.Capsule, parent, color);
            part.name = name;
            part.transform.localPosition = (a + b) * 0.5f;
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            part.transform.localScale = new Vector3(width, (b - a).magnitude * 0.5f, width);
        }
    }
}
