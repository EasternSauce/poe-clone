using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// The field bosses' bodies, each its own silhouette rather than a scaled-up monster.
    /// Colours (from the kind): Main, Second, Accent, Eyes as each body describes; sizes at scale 1.
    /// Named parts a boss's moves use: "Coffin", "Greatsword", "Bell", "Hammer", "Spear", "SunOrb".
    /// </summary>
    public static partial class CreatureBuilder
    {
        private static readonly Color Wood = new Color(0.32f, 0.22f, 0.14f);
        private static readonly Color Iron = new Color(0.26f, 0.26f, 0.28f);
        private static readonly Color Steel = new Color(0.62f, 0.64f, 0.68f);

        private static void BuildBoss(Transform model, CreatureBody body, Ctx c)
        {
            switch (body)
            {
                case CreatureBody.Gravedigger: BuildGravedigger(model, c); break;
                case CreatureBody.HollowArmor: BuildHollowArmor(model, c); break;
                case CreatureBody.FrostHeart: BuildFrostHeart(model, c); break;
                case CreatureBody.Boar: BuildBoar(model, c); break;
                case CreatureBody.TunnelKing: BuildTunnelKing(model, c); break;
                case CreatureBody.BellRinger: BuildBellRinger(model, c); break;
                case CreatureBody.SunIdol: BuildSunIdol(model, c); break;
                case CreatureBody.Huntress: BuildHuntress(model, c); break;
            }
        }

        private static CreatureAnimator.Leg Arm(Ctx c, int side) => c.Anim.Legs.Find(l => l.Front && l.Side == side);

        // ------------------------------------------------------------------ Gravelord Mortis

        private static readonly Color Stole = new Color(0.28f, 0.2f, 0.3f);
        private static readonly Color Leather = new Color(0.14f, 0.1f, 0.08f);

        // The yard's old priest, turned gravedigger: a tall, stooped man in a long split greatcoat
        // and a priest's shoulder cape, heavy boots, bony hands, a spade in his right. Every part
        // overlaps the one it hangs from. Main: coat. Second: waistcoat and trousers. Accent:
        // grey-green skin. Eyes: grave-light.
        private static void BuildGravedigger(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Gravedigger", new Vector3(0f, 1.45f, 0f));
            c.Anim.BodyPivot = body;
            // Chest and belly in the coat, the waistcoat showing down the front, the hips below.
            Ico(c, body, c.Colors.Main, new Vector3(0f, 0.08f, 0f), new Vector3(0.56f, 0.82f, 0.36f));
            Ico(c, body, c.Colors.Second, new Vector3(0f, 0.08f, 0.1f), new Vector3(0.3f, 0.64f, 0.2f));
            Ico(c, body, c.Colors.Second, new Vector3(0f, -0.32f, 0f), new Vector3(0.44f, 0.3f, 0.3f));
            for (int i = 0; i < 4; i++)
                Part(body, PrimitiveType.Sphere, Bone, new Vector3(0f, 0.26f - i * 0.12f, 0.2f), Vector3.one * 0.035f, Vector3.zero, false);
            Part(body, PrimitiveType.Cylinder, Leather, new Vector3(0f, -0.24f, 0f), new Vector3(0.5f, 0.035f, 0.36f), Vector3.zero);
            Part(body, PrimitiveType.Cube, Iron, new Vector3(0f, -0.24f, 0.18f), new Vector3(0.08f, 0.07f, 0.02f), Vector3.zero, false);

            // Neck, then a long gaunt head: sunken cheeks, grave-light in deep sockets.
            Ico(c, body, c.Colors.Accent, new Vector3(0f, 0.5f, 0.06f), new Vector3(0.13f, 0.18f, 0.13f));
            Transform head = Pivot(body, "Head", new Vector3(0f, 0.66f, 0.1f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Accent, Vector3.zero, new Vector3(0.24f, 0.32f, 0.26f));
            Ico(c, head, c.Colors.Accent, new Vector3(0f, -0.02f, 0.12f), new Vector3(0.06f, 0.1f, 0.06f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Sphere, Dark, new Vector3(side * 0.055f, 0.04f, 0.11f), new Vector3(0.07f, 0.055f, 0.04f), Vector3.zero, false);
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.055f, 0.04f, 0.125f), new Vector3(0.03f, 0.025f, 0.02f), Vector3.zero, false);
                Part(head, PrimitiveType.Sphere, Color.Lerp(c.Colors.Accent, Dark, 0.45f), new Vector3(side * 0.075f, -0.045f, 0.095f), new Vector3(0.05f, 0.07f, 0.03f), new Vector3(0f, side * 25f, 0f), false);
                // Lank grey hair hanging from under the hat.
                Part(head, PrimitiveType.Cube, new Color(0.5f, 0.52f, 0.48f), new Vector3(side * 0.11f, -0.04f, -0.04f), new Vector3(0.04f, 0.2f, 0.12f), new Vector3(0f, 0f, side * -6f), false);
            }
            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.1f, 0.04f));
            c.Anim.Jaw = jaw;
            Ico(c, jaw, c.Colors.Accent, new Vector3(0f, -0.05f, 0.03f), new Vector3(0.17f, 0.12f, 0.16f));
            for (int i = 0; i < 4; i++)
                Part(jaw, PrimitiveType.Cube, Bone, new Vector3(-0.045f + i * 0.03f, 0.0f, 0.1f), new Vector3(0.02f, 0.035f, 0.02f), Vector3.zero, false);
            GravediggerHat(c, head);

            // What is left of the priest: a short cape over the shoulders, a white collar, the
            // burial stole and its blessed stone, gone grave-green.
            Ico(c, body, Color.Lerp(c.Colors.Main, Dark, 0.4f), new Vector3(0f, 0.33f, -0.02f), new Vector3(0.84f, 0.14f, 0.5f));
            Ico(c, body, c.Colors.Main, new Vector3(0f, 0.38f, -0.02f), new Vector3(0.78f, 0.24f, 0.46f));
            Part(body, PrimitiveType.Cylinder, Bone, new Vector3(0f, 0.47f, 0.05f), new Vector3(0.22f, 0.03f, 0.2f), Vector3.zero, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(body, PrimitiveType.Cube, Stole, new Vector3(side * 0.08f, 0.02f, 0.205f), new Vector3(0.07f, 0.84f, 0.02f), new Vector3(0f, 0f, side * -4f), false);
                // The stole goes on over his shoulders and down his back, with a frayed fringe at its ends.
                Part(body, PrimitiveType.Cube, Stole, new Vector3(side * 0.1f, 0.49f, -0.02f), new Vector3(0.075f, 0.025f, 0.46f), new Vector3(0f, side * 6f, 0f), false);
                for (int f = 0; f < 3; f++)
                    Part(body, PrimitiveType.Cube, Bone, new Vector3(side * 0.08f + (f - 1) * 0.02f, -0.42f, 0.205f), new Vector3(0.012f, 0.06f, 0.01f), Vector3.zero, false);
            }
            Part(body, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(0f, 0.33f, 0.22f), Vector3.one * 0.06f, Vector3.zero, false);

            // The greatcoat's skirt, split at the front: side panels hang from the hips, the back
            // ones from the tails (which swing as he walks).
            Transform tails = Pivot(body, "CoatTails", new Vector3(0f, -0.28f, -0.12f));
            c.Anim.Tail = tails;
            for (int i = 0; i < 3; i++)
                Part(tails, PrimitiveType.Cube, c.Colors.Main, new Vector3((i - 1) * 0.15f, -0.38f, -0.02f), new Vector3(0.17f, 0.8f, 0.04f), new Vector3(-6f, (i - 1) * 25f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(side * 0.24f, -0.62f, 0.02f), new Vector3(0.04f, 0.78f, 0.26f), new Vector3(0f, 0f, side * 6f));
                Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(side * 0.17f, -0.6f, 0.15f), new Vector3(0.12f, 0.74f, 0.04f), new Vector3(8f, side * -30f, side * 4f));
            }

            for (int side = -1; side <= 1; side += 2)
            {
                GravediggerLeg(c, body, side);
                GravediggerArm(c, body, side);
            }

            // The spade, in both fists. At rest he carries it upright before him like a crozier,
            // the blade over his head, so it never trails through the ground.
            CreatureAnimator.Leg right = Arm(c, 1);
            Transform shovel = Pivot(right.Knee, "Shovel", SpadeGrip);
            shovel.localRotation = Quaternion.Euler(ShovelRest, 0f, 0f);
            c.Anim.Tool = shovel;
            BuildSpade(shovel);
            // The left hand takes the D-grip.
            c.Anim.OffHand = Pivot(shovel, "OffHandGrip", new Vector3(0f, 0.3f, 0f));
            c.Anim.OffHandGrip = SpadeGrip;
            c.Anim.AttackClips = GravediggerClips.Attacks;

            // The coffin slung slantwise across his back, its head up over his left shoulder, on a
            // strap running from that shoulder down across his chest to his right hip.
            Transform coffin = Pivot(body, "Coffin", new Vector3(-0.06f, -0.04f, -0.36f));
            coffin.localRotation = Quaternion.Euler(10f, 8f, 28f);
            c.Anim.Prop = coffin;
            BuildCoffin(coffin, c);
            Part(body, PrimitiveType.Cube, Leather, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.05f, 0.95f, 0.42f), new Vector3(0f, 0f, 38f));
            Part(body, PrimitiveType.Cube, Iron, new Vector3(-0.1f, 0.25f, 0.215f), new Vector3(0.07f, 0.07f, 0.015f), new Vector3(0f, 0f, 38f), false);
            Part(body, PrimitiveType.Cube, Leather, new Vector3(0f, -0.14f, -0.06f), new Vector3(0.58f, 0.045f, 0.46f), Vector3.zero);
        }

        // A trousered leg in a heavy boot, the hip ball sunk into the pelvis.
        private static void GravediggerLeg(Ctx c, Transform body, int side)
        {
            const float upper = 0.58f, lower = 0.56f;
            Transform hip = Pivot(body, "Hip", new Vector3(side * 0.13f, -0.36f, 0f));
            hip.localRotation = Quaternion.Euler(-10f, 0f, side * 3f);
            Ico(c, hip, c.Colors.Second, Vector3.zero, Vector3.one * 0.22f);
            Ico(c, hip, c.Colors.Second, new Vector3(0f, -upper * 0.5f, 0f), new Vector3(0.21f, upper + 0.08f, 0.21f));
            Transform knee = Pivot(hip, "Knee", new Vector3(0f, -upper, 0f));
            knee.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Ico(c, knee, c.Colors.Second, Vector3.zero, Vector3.one * 0.19f);
            Ico(c, knee, c.Colors.Second, new Vector3(0f, -lower * 0.45f, 0f), new Vector3(0.17f, lower * 0.9f, 0.17f));
            // Boot: the shaft over the shin, the foot, a turned-down cuff.
            Part(knee, PrimitiveType.Cylinder, Leather, new Vector3(0f, -lower + 0.17f, 0f), new Vector3(0.2f, 0.13f, 0.2f), Vector3.zero);
            Part(knee, PrimitiveType.Cylinder, Leather, new Vector3(0f, -lower + 0.31f, 0f), new Vector3(0.22f, 0.025f, 0.22f), Vector3.zero, false);
            Part(knee, PrimitiveType.Cube, Leather, new Vector3(0f, -lower - 0.02f, 0.07f), new Vector3(0.19f, 0.13f, 0.36f), Vector3.zero);
            c.Anim.Legs.Add(new CreatureAnimator.Leg { Hip = hip, Knee = knee, Side = side, Front = false, Group = side < 0 ? 0 : 1, Lift = side * 3f, Bend = 12f });
        }

        // A patched coat sleeve with a buttoned turn-back cuff, ending in a worn leather glove; the
        // shoulder ball sits inside the shoulder cape. The right forearm has a strapped bracer
        // (the spade arm); the left wrist is wound with the priest's rosary, a little grave-bell on it.
        private static void GravediggerArm(Ctx c, Transform body, int side)
        {
            const float upper = 0.55f, lower = 0.5f;
            Color patch = Color.Lerp(c.Colors.Main, Wood, 0.35f);
            Transform shoulder = Pivot(body, "Shoulder", new Vector3(side * 0.27f, 0.3f, 0f));
            shoulder.localRotation = Quaternion.Euler(-12f, 0f, side * 12f);
            Ico(c, shoulder, c.Colors.Main, Vector3.zero, Vector3.one * 0.24f);
            Ico(c, shoulder, c.Colors.Main, new Vector3(0f, -upper * 0.5f, 0f), new Vector3(0.18f, upper + 0.08f, 0.18f));
            // A square patch on the outside of the sleeve, stitched round.
            Part(shoulder, PrimitiveType.Cube, patch, new Vector3(side * 0.088f, -0.3f, 0f), new Vector3(0.02f, 0.13f, 0.11f), Vector3.zero, false);
            for (int k = 0; k < 4; k++)
                Part(shoulder, PrimitiveType.Cube, Bone, new Vector3(side * 0.1f, -0.25f - k * 0.033f, 0.058f), new Vector3(0.008f, 0.015f, 0.008f), Vector3.zero, false);

            Transform elbow = Pivot(shoulder, "Elbow", new Vector3(0f, -upper, 0f));
            elbow.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Ico(c, elbow, Leather, new Vector3(0f, 0f, -0.02f), new Vector3(0.18f, 0.17f, 0.16f));
            Ico(c, elbow, c.Colors.Main, new Vector3(0f, -lower * 0.42f, 0f), new Vector3(0.16f, lower * 0.9f, 0.16f));
            // The cuff: wide, turned back, three bone buttons.
            Part(elbow, PrimitiveType.Cylinder, c.Colors.Second, new Vector3(0f, -lower + 0.06f, 0f), new Vector3(0.21f, 0.07f, 0.21f), Vector3.zero);
            for (int k = 0; k < 3; k++)
                Part(elbow, PrimitiveType.Sphere, Bone, new Vector3(side * 0.1f, -lower + 0.03f + k * 0.03f, 0.03f), Vector3.one * 0.022f, Vector3.zero, false);
            if (side > 0)
            {
                Part(elbow, PrimitiveType.Cylinder, Leather, new Vector3(0f, -lower * 0.55f, 0f), new Vector3(0.18f, 0.08f, 0.18f), Vector3.zero);
                for (int k = -1; k <= 1; k += 2)
                    Part(elbow, PrimitiveType.Cylinder, Iron, new Vector3(0f, -lower * 0.55f + k * 0.05f, 0f), new Vector3(0.185f, 0.012f, 0.185f), Vector3.zero, false);
            }
            else
            {
                Color bead = new Color(0.55f, 0.5f, 0.42f);
                for (int k = 0; k < 9; k++)
                {
                    float a = k * Mathf.PI * 2f / 9f;
                    Part(elbow, PrimitiveType.Sphere, bead, new Vector3(Mathf.Cos(a) * 0.1f, -lower - 0.01f + Mathf.Sin(a * 2f) * 0.01f, Mathf.Sin(a) * 0.1f), Vector3.one * 0.03f, Vector3.zero, false);
                }
                for (int k = 0; k < 3; k++)
                    Part(elbow, PrimitiveType.Sphere, bead, new Vector3(0.03f, -lower - 0.05f - k * 0.035f, 0.1f), Vector3.one * 0.028f, Vector3.zero, false);
                Cone(c, elbow, Iron, new Vector3(0.03f, -lower - 0.17f, 0.1f), new Vector3(0.05f, 0.05f, 0.05f), Vector3.zero);
            }
            GravediggerHand(c, elbow, side, lower);
            c.Anim.Legs.Add(new CreatureAnimator.Leg { Hip = shoulder, Knee = elbow, Side = side, Front = true, Group = side < 0 ? 0 : 1, Lift = side * 12f, Bend = -20f });
        }

        // Where his right fist holds the spade, in the forearm's space (the grip hand's socket,
        // as on the player's rig, grown to his hand's size).
        private static readonly Vector3 SpadeGrip = new Vector3(0f, -0.65f, 0.07f);

        // The player's gripping hand at his size, grey and dead, closed round the spade's handle.
        private static void GravediggerHand(Ctx c, Transform forearm, int side, float lower)
        {
            BossMeshes meshes = BossMeshes.Get();
            if (meshes == null)
                return;
            Mesh mesh = side > 0 ? meshes.HandRGrip : meshes.HandLGrip;
            GameObject hand = RuntimePrimitives.CreateMesh(mesh, forearm, c.Colors.Accent, "Hand");
            hand.transform.localPosition = new Vector3(0f, -lower - 0.02f, 0f);
            hand.transform.localScale = new Vector3(0.2f, 0.235f, 0.235f);
        }

        public const float ShovelRest = -35f;

        // A gravedigger's shovel: D-grip, ash handle, a riveted iron socket, a round-pointed blade
        // caked with grave-dirt.
        private static void BuildSpade(Transform shovel)
        {
            Part(shovel, PrimitiveType.Cylinder, Wood, new Vector3(0f, -0.2f, 0f), new Vector3(0.055f, 0.45f, 0.055f), Vector3.zero);
            Part(shovel, PrimitiveType.Cube, Wood, new Vector3(0f, 0.33f, 0f), new Vector3(0.17f, 0.045f, 0.055f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
                Part(shovel, PrimitiveType.Cube, Wood, new Vector3(side * 0.065f, 0.29f, 0f), new Vector3(0.035f, 0.1f, 0.045f), new Vector3(0f, 0f, side * 12f));
            // The iron socket the shaft goes into, tapering out onto the back of the blade.
            Part(shovel, PrimitiveType.Cylinder, Iron, new Vector3(0f, -0.66f, 0f), new Vector3(0.075f, 0.06f, 0.075f), Vector3.zero);
            Part(shovel, PrimitiveType.Cylinder, Iron, new Vector3(0f, -0.73f, -0.005f), new Vector3(0.1f, 0.03f, 0.07f), Vector3.zero);
            Part(shovel, PrimitiveType.Sphere, Iron, new Vector3(0f, -0.8f, -0.012f), new Vector3(0.1f, 0.16f, 0.03f), Vector3.zero, false);
            for (int k = -1; k <= 1; k += 2)
                Part(shovel, PrimitiveType.Sphere, Steel, new Vector3(0f, -0.66f + k * 0.022f, 0.038f), Vector3.one * 0.018f, Vector3.zero, false);
            // The blade, dished forward, its top edge rolled over into a tread for his boot.
            GameObject blade = RuntimePrimitives.CreateMesh(SpadeBlade(), shovel, Iron, "Blade");
            blade.transform.localPosition = new Vector3(0f, -0.74f, 0f);
            Part(shovel, PrimitiveType.Cylinder, Iron, new Vector3(0f, -0.745f, 0.01f), new Vector3(0.028f, 0.15f, 0.028f), new Vector3(0f, 0f, 90f), false);
            Part(shovel, PrimitiveType.Sphere, new Color(0.24f, 0.18f, 0.12f), new Vector3(0.05f, -0.98f, 0.03f), new Vector3(0.12f, 0.09f, 0.03f), Vector3.zero, false);
        }

        private static Mesh spadeBlade;

        // A round-point shovel blade hanging down from its top edge at the origin: straight sides,
        // then curving in to a soft point, dished towards +Z, drawn from both sides.
        private static Mesh SpadeBlade()
        {
            if (spadeBlade != null)
                return spadeBlade;
            const float length = 0.42f, half = 0.15f, straight = 0.5f, dish = 0.035f;
            const int rows = 10, columns = 6;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                for (int i = 0; i <= rows; i++)
                {
                    float along = i / (float)rows;
                    float curve = along <= straight ? 1f : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((along - straight) / (1f - straight), 2f)));
                    float width = Mathf.Max(0.004f, half * curve);
                    for (int j = 0; j <= columns; j++)
                    {
                        float x = Mathf.Lerp(-width, width, j / (float)columns);
                        float z = dish * (x / half) * (x / half) + (face == 0 ? -0.004f : 0.004f);
                        vertices.Add(new Vector3(x, -along * length, z));
                    }
                }
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < columns; j++)
                    {
                        int a = start + i * (columns + 1) + j, b = a + 1, cc = a + columns + 1, d = cc + 1;
                        // One face looks along -Z, the other along +Z.
                        if (face == 0)
                            triangles.AddRange(new[] { a, b, d, a, d, cc });
                        else
                            triangles.AddRange(new[] { a, d, b, a, cc, d });
                    }
                }
            }
            spadeBlade = new Mesh { name = "SpadeBlade" };
            spadeBlade.SetVertices(vertices);
            spadeBlade.SetTriangles(triangles, 0);
            spadeBlade.RecalculateNormals();
            spadeBlade.RecalculateBounds();
            return spadeBlade;
        }

        // A tall undertaker's hat, seen from above most of the fight: a worn, dusty-edged brim with
        // a grave-candle stuck to it (wax run down the brim, a grave-light flame), a crepe band
        // with an iron buckle and the mourner's long crepe "weepers" hanging down his back.
        private static void GravediggerHat(Ctx c, Transform head)
        {
            Color crepe = new Color(0.05f, 0.05f, 0.06f);
            Color dusty = new Color(0.22f, 0.2f, 0.19f);
            Color wax = new Color(0.86f, 0.84f, 0.74f);
            Part(head, PrimitiveType.Cylinder, dusty, new Vector3(0f, 0.13f, 0f), new Vector3(0.68f, 0.015f, 0.64f), new Vector3(-6f, 0f, 0f));
            Part(head, PrimitiveType.Cylinder, Dark, new Vector3(0f, 0.136f, 0.002f), new Vector3(0.58f, 0.016f, 0.54f), new Vector3(-6f, 0f, 0f), false);
            // The brim's edge droops at the front and back, with a nick torn out of it.
            for (int k = -1; k <= 1; k += 2)
                Part(head, PrimitiveType.Cube, dusty, new Vector3(0f, 0.11f + k * 0.012f, k * 0.31f), new Vector3(0.3f, 0.014f, 0.06f), new Vector3(k * 14f, 0f, 0f));
            Part(head, PrimitiveType.Cube, Dark, new Vector3(-0.3f, 0.135f, -0.1f), new Vector3(0.05f, 0.02f, 0.05f), new Vector3(0f, 30f, 0f), false);

            // A tall crown, dented on top.
            Part(head, PrimitiveType.Cylinder, Dark, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.3f, 0.14f, 0.3f), new Vector3(-8f, 0f, 4f));
            Part(head, PrimitiveType.Cylinder, dusty, new Vector3(0.01f, 0.405f, -0.03f), new Vector3(0.27f, 0.006f, 0.27f), new Vector3(-8f, 0f, 4f), false);
            Part(head, PrimitiveType.Cube, Dark, new Vector3(0.01f, 0.4f, -0.03f), new Vector3(0.2f, 0.02f, 0.05f), new Vector3(-8f, 20f, 4f), false);
            Part(head, PrimitiveType.Cylinder, crepe, new Vector3(0f, 0.175f, -0.005f), new Vector3(0.315f, 0.04f, 0.315f), new Vector3(-8f, 0f, 4f), false);
            Part(head, PrimitiveType.Cube, Iron, new Vector3(0.01f, 0.178f, 0.15f), new Vector3(0.07f, 0.05f, 0.015f), new Vector3(-8f, 0f, 4f), false);
            Part(head, PrimitiveType.Cube, Dark, new Vector3(0.01f, 0.178f, 0.157f), new Vector3(0.04f, 0.025f, 0.01f), new Vector3(-8f, 0f, 4f), false);
            for (int k = -1; k <= 1; k += 2)
                Part(head, PrimitiveType.Cube, crepe, new Vector3(k * 0.05f, -0.06f, -0.2f), new Vector3(0.07f, 0.48f, 0.012f), new Vector3(18f, k * 6f, k * 4f), false);

            // The grave-candle on the brim, right of the crown.
            Part(head, PrimitiveType.Cylinder, wax, new Vector3(0.21f, 0.18f, 0.08f), new Vector3(0.045f, 0.05f, 0.045f), Vector3.zero, false);
            Part(head, PrimitiveType.Cylinder, wax, new Vector3(0.21f, 0.143f, 0.08f), new Vector3(0.09f, 0.008f, 0.08f), Vector3.zero, false);
            for (int k = 0; k < 3; k++)
                Part(head, PrimitiveType.Sphere, wax, new Vector3(0.24f + k * 0.03f, 0.14f - k * 0.006f, 0.1f + k * 0.02f), Vector3.one * (0.03f - k * 0.006f), Vector3.zero, false);
            Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(0.21f, 0.255f, 0.08f), new Vector3(0.035f, 0.06f, 0.035f), Vector3.zero, false);
        }

        /// <summary>A plain coffin (Mortis carries one, and throws it), its foot at the origin's -Y.</summary>
        public static void BuildCoffin(Transform root, Color? wood = null)
        {
            Color w = wood ?? new Color(0.24f, 0.17f, 0.12f);
            Color dark = new Color(0.12f, 0.09f, 0.07f);
            Color iron = new Color(0.3f, 0.3f, 0.32f);
            // The box, its lid (out on the -Z face) a little proud of it, a raised panel on the lid
            // and a cross on that.
            RuntimePrimitives.CreateMesh(CoffinShape(1f, 0.1f, 0f), root, w, "CoffinBox");
            RuntimePrimitives.CreateMesh(CoffinShape(1.06f, 0.025f, -0.12f), root, Color.Lerp(w, dark, 0.35f), "CoffinLid");
            RuntimePrimitives.CreateMesh(CoffinShape(0.8f, 0.012f, -0.15f), root, Color.Lerp(w, Color.white, 0.08f), "CoffinPanel");
            PartPublic(root, PrimitiveType.Cube, dark, new Vector3(0f, 0.05f, -0.165f), new Vector3(0.05f, 0.62f, 0.015f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cube, dark, new Vector3(0f, 0.25f, -0.165f), new Vector3(0.26f, 0.05f, 0.015f), Vector3.zero);
            // Iron handles down both long sides.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    float y = k == 0 ? 0.12f : -0.3f;
                    float x = side * (k == 0 ? 0.265f : 0.21f) + side * 0.03f;
                    PartPublic(root, PrimitiveType.Cube, iron, new Vector3(x, y, 0f), new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0f, 0f, side * (k == 0 ? -10f : -6f)));
                }
            }
        }

        // The coffin's six-sided outline (narrow head, widest at the shoulders, tapering to the
        // foot), scaled and pushed out into a slab this thick, centred at depth z.
        private static readonly System.Collections.Generic.Dictionary<Vector3, Mesh> coffinShapes = new System.Collections.Generic.Dictionary<Vector3, Mesh>();

        private static Mesh CoffinShape(float scale, float halfDepth, float z)
        {
            var key = new Vector3(scale, halfDepth, z);
            if (coffinShapes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;
            Vector2[] outline =
            {
                new Vector2(0.15f, 0.58f), new Vector2(0.28f, 0.3f), new Vector2(0.17f, -0.6f),
                new Vector2(-0.17f, -0.6f), new Vector2(-0.28f, 0.3f), new Vector2(-0.15f, 0.58f)
            };
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            void Face(Vector3 normal, params Vector3[] corners)
            {
                int start = vertices.Count;
                vertices.AddRange(corners);
                for (int i = 1; i + 1 < corners.Length; i++)
                {
                    Vector3 a = corners[0], b = corners[i], cc = corners[i + 1];
                    // Unity draws the face whose winding's cross product points along its normal.
                    if (Vector3.Dot(Vector3.Cross(b - a, cc - a), normal) >= 0f)
                        triangles.AddRange(new[] { start, start + i, start + i + 1 });
                    else
                        triangles.AddRange(new[] { start, start + i + 1, start + i });
                }
            }
            int n = outline.Length;
            var front = new Vector3[n];
            var back = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                front[i] = new Vector3(outline[i].x * scale, outline[i].y * scale, z + halfDepth);
                back[i] = new Vector3(outline[i].x * scale, outline[i].y * scale, z - halfDepth);
            }
            Face(Vector3.forward, front);
            Face(Vector3.back, back);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 edge = back[j] - back[i];
                Vector3 outward = new Vector3(edge.y, -edge.x, 0f).normalized;
                if (Vector3.Dot(outward, new Vector3(back[i].x, back[i].y, 0f)) < 0f)
                    outward = -outward;
                Face(outward, back[i], back[j], front[j], front[i]);
            }
            var mesh = new Mesh { name = "Coffin" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            coffinShapes[key] = mesh;
            return mesh;
        }

        private static void BuildCoffin(Transform root, Ctx c) => BuildCoffin(root);

        // ------------------------------------------------------------------ the Ashen Warlord

        // Main: blackened plate. Second: the burnt cape. Accent: embers. Eyes: the flame's heart.
        private static void BuildHollowArmor(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "HollowArmor", new Vector3(0f, 1.42f, 0f));
            c.Anim.BodyPivot = body;
            Color edge = Color.Lerp(c.Colors.Main, c.Colors.Second, 0.5f);
            // Breastplate, its ridge, and the plates below with the fire showing between them.
            Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0.14f, 0f), new Vector3(0.68f, 0.5f, 0.42f), Vector3.zero);
            Part(body, PrimitiveType.Cube, edge, new Vector3(0f, 0.14f, 0.21f), new Vector3(0.09f, 0.48f, 0.06f), Vector3.zero);
            Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0.38f, -0.02f), new Vector3(0.72f, 0.08f, 0.44f), Vector3.zero);
            Part(body, PrimitiveType.Sphere, c.Colors.Accent, new Vector3(0f, -0.14f, 0f), new Vector3(0.44f, 0.24f, 0.32f), Vector3.zero, false);
            for (int i = 0; i < 3; i++)
                Part(body, PrimitiveType.Cube, i == 1 ? edge : c.Colors.Main, new Vector3(0f, -0.24f - i * 0.13f, 0.01f), new Vector3(0.6f - i * 0.04f, 0.08f, 0.38f), Vector3.zero);
            Part(body, PrimitiveType.Sphere, c.Colors.Accent, new Vector3(0f, -0.56f, 0f), new Vector3(0.36f, 0.12f, 0.28f), Vector3.zero, false);
            // Rivets.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                    Part(body, PrimitiveType.Sphere, edge, new Vector3(side * 0.26f, 0.28f - i * 0.14f, 0.21f), Vector3.one * 0.045f, Vector3.zero, false);

            // No head: a gorget with fire rising out of it.
            Part(body, PrimitiveType.Cylinder, c.Colors.Main, new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.06f, 0.34f), Vector3.zero);
            Transform flame = Pivot(body, "Flame", new Vector3(0f, 0.48f, 0f));
            c.Anim.Head = flame;
            Cone(c, flame, c.Colors.Accent, new Vector3(0f, 0.22f, 0f), new Vector3(0.24f, 0.55f, 0.24f), Vector3.zero);
            Cone(c, flame, c.Colors.Eyes, new Vector3(0f, 0.18f, 0.02f), new Vector3(0.13f, 0.42f, 0.13f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
                Cone(c, flame, c.Colors.Accent, new Vector3(side * 0.1f, 0.12f, -0.03f), new Vector3(0.09f, 0.3f, 0.09f), new Vector3(0f, 0f, side * -18f));

            for (int side = -1; side <= 1; side += 2)
            {
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.6f, 0.62f);
                CreatureAnimator.Leg leg = c.Anim.Legs[c.Anim.Legs.Count - 1];
                Part(leg.Knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.3f, 0.02f), new Vector3(0.2f, 0.46f, 0.22f), Vector3.zero);
                Part(leg.Knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.6f, 0.08f), new Vector3(0.2f, 0.1f, 0.34f), Vector3.zero);
                Part(leg.Knee, PrimitiveType.Sphere, c.Colors.Accent, Vector3.zero, Vector3.one * 0.15f, Vector3.zero, false);
                Part(leg.Hip, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.3f, 0f), new Vector3(0.22f, 0.42f, 0.24f), Vector3.zero);

                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.5f, 0.56f);
                CreatureAnimator.Leg arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                arm.Hip.localPosition = new Vector3(side * 0.42f, 0.28f, 0f);
                // Pauldrons with spikes, the ember of the elbow, a gauntlet.
                Ico(c, arm.Hip, c.Colors.Main, new Vector3(0f, 0.04f, 0f), new Vector3(0.44f, 0.3f, 0.42f));
                Part(arm.Hip, PrimitiveType.Cube, edge, new Vector3(0f, -0.05f, 0f), new Vector3(0.45f, 0.05f, 0.43f), Vector3.zero);
                for (int k = 0; k < 2; k++)
                    Cone(c, arm.Hip, c.Colors.Main, new Vector3(side * 0.06f, 0.16f, (k - 0.5f) * 0.18f), new Vector3(0.08f, 0.3f, 0.08f), new Vector3(0f, 0f, side * -25f));
                Part(arm.Knee, PrimitiveType.Sphere, c.Colors.Accent, Vector3.zero, Vector3.one * 0.14f, Vector3.zero, false);
                Part(arm.Knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.3f, 0f), new Vector3(0.18f, 0.4f, 0.2f), Vector3.zero);
                Part(arm.Knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.58f, 0.02f), new Vector3(0.2f, 0.2f, 0.22f), Vector3.zero);
            }

            // The greatsword, held in the right gauntlet and trailing down in front.
            CreatureAnimator.Leg right = Arm(c, 1);
            Transform sword = Pivot(right.Knee, "Greatsword", new Vector3(0f, -0.6f, 0.06f));
            sword.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            Part(sword, PrimitiveType.Cylinder, Dark, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.14f, 0.05f), Vector3.zero);
            Part(sword, PrimitiveType.Sphere, c.Colors.Main, new Vector3(0f, 0.2f, 0f), Vector3.one * 0.09f, Vector3.zero);
            Part(sword, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.12f, 0f), new Vector3(0.4f, 0.06f, 0.08f), Vector3.zero);
            Part(sword, PrimitiveType.Cube, Iron, new Vector3(0f, -0.82f, 0f), new Vector3(0.15f, 1.3f, 0.035f), Vector3.zero);
            Part(sword, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, -0.8f, 0f), new Vector3(0.03f, 1.2f, 0.045f), Vector3.zero, false);
            Cone(c, sword, Iron, new Vector3(0f, -1.55f, 0f), new Vector3(0.15f, 0.2f, 0.035f), new Vector3(180f, 0f, 0f));

            // A burnt, tattered cape.
            Transform cape = Pivot(body, "Cape", new Vector3(0f, 0.36f, -0.24f));
            c.Anim.Tail = cape;
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.17f;
                Cone(c, cape, c.Colors.Second, new Vector3(x, -0.45f, 0f), new Vector3(0.17f, 1.05f - (i % 2) * 0.2f, 0.04f), new Vector3(178f, 0f, x * 12f));
                Part(cape, PrimitiveType.Sphere, c.Colors.Accent, new Vector3(x, -0.92f + (i % 2) * 0.18f, 0f), new Vector3(0.06f, 0.04f, 0.04f), Vector3.zero, false);
            }
        }

        // ------------------------------------------------------------------ Rimeheart

        // Main: the heart's ice. Second: the cage of shards. Accent: deep ice. Eyes: the beating core.
        private static void BuildFrostHeart(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "FrostHeart", new Vector3(0f, 2.1f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.62f, 0.74f, 0.56f));
            Ico(c, body, c.Colors.Accent, new Vector3(0f, -0.08f, 0f), new Vector3(0.66f, 0.42f, 0.6f));
            // Two lobes on top, like a heart.
            for (int side = -1; side <= 1; side += 2)
                Ico(c, body, c.Colors.Main, new Vector3(side * 0.16f, 0.26f, 0f), new Vector3(0.36f, 0.36f, 0.4f));
            Transform core = Pivot(body, "Core", new Vector3(0f, 0.04f, 0.2f));
            c.Anim.Jaw = core;
            Ico(c, core, c.Colors.Eyes, Vector3.zero, new Vector3(0.3f, 0.34f, 0.24f));
            Ico(c, body, c.Colors.Eyes, new Vector3(0f, 0.04f, -0.2f), new Vector3(0.24f, 0.28f, 0.2f));
            c.Anim.Mouth = Pivot(body, CreatureAnimator.MouthName, new Vector3(0f, 0.04f, 0.42f));

            // The cage: shards round it like ribs, above and below, turning slowly.
            Transform cage = Pivot(body, "Cage", Vector3.zero);
            c.Anim.Abdomen = cage;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float yaw = -a * Mathf.Rad2Deg + 90f;
                Cone(c, cage, c.Colors.Second, dir * 0.52f + Vector3.up * 0.42f, new Vector3(0.1f, 0.8f + (i % 2) * 0.25f, 0.1f), new Vector3(-14f, yaw, 0f) + new Vector3(0f, 0f, 0f));
                Cone(c, cage, i % 2 == 0 ? c.Colors.Second : c.Colors.Accent, dir * 0.48f - Vector3.up * 0.4f, new Vector3(0.09f, 0.6f, 0.09f), new Vector3(194f, yaw, 0f));
            }
            Transform crown = Pivot(body, "Crown", new Vector3(0f, 0.5f, 0f));
            c.Anim.Head = crown;
            for (int i = 0; i < 5; i++)
                Cone(c, crown, c.Colors.Second, new Vector3((i - 2) * 0.09f, 0.12f, 0f), new Vector3(0.06f, 0.3f - Mathf.Abs(i - 2) * 0.06f, 0.06f), new Vector3(0f, 0f, (i - 2) * -14f));
            Transform tail = Pivot(body, "Icicles", new Vector3(0f, -0.42f, 0f));
            c.Anim.Tail = tail;
            Cone(c, tail, c.Colors.Accent, new Vector3(0f, -0.32f, 0f), new Vector3(0.14f, 0.62f, 0.14f), new Vector3(180f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
                Cone(c, tail, c.Colors.Main, new Vector3(side * 0.12f, -0.2f, 0.04f), new Vector3(0.08f, 0.4f, 0.08f), new Vector3(180f, 0f, side * 10f));
            for (int i = 0; i < 5; i++)
            {
                Transform shard = Pivot(body, "OrbitShard", Vector3.zero);
                Cone(c, shard, i % 2 == 0 ? c.Colors.Main : c.Colors.Second, Vector3.zero, new Vector3(0.09f, 0.42f, 0.09f), new Vector3(0f, 0f, 90f));
                c.Anim.Orbit.Add(shard);
            }
        }

        // ------------------------------------------------------------------ Bramblesow

        // Main: bristly hide. Second: the dark mane. Accent: moss and leaves. Eyes: small, red.
        private static void BuildBoar(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Boar", new Vector3(0f, 0.92f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, new Vector3(0f, 0f, -0.08f), new Vector3(0.86f, 0.8f, 1.4f));
            Ico(c, body, c.Colors.Second, new Vector3(0f, 0.12f, 0.36f), new Vector3(0.92f, 0.86f, 0.72f));
            Ico(c, body, Color.Lerp(c.Colors.Main, Bone, 0.25f), new Vector3(0f, -0.26f, 0.02f), new Vector3(0.6f, 0.34f, 1.1f));
            // A ridge of bristles down the spine.
            for (int i = 0; i < 7; i++)
                Cone(c, body, c.Colors.Second, new Vector3(0f, 0.46f - Mathf.Abs(i - 2) * 0.02f, 0.62f - i * 0.17f), new Vector3(0.1f, 0.34f - i * 0.02f, 0.06f), new Vector3(-35f, 0f, 0f));

            // The thicket growing on its back: bushes, thorns, two saplings, a few flowers.
            Transform thicket = Pivot(body, "Thicket", new Vector3(0f, 0.36f, -0.22f));
            c.Anim.Abdomen = thicket;
            Vector3[] bushes =
            {
                new Vector3(-0.2f, 0.06f, 0.05f), new Vector3(0.22f, 0.05f, -0.05f), new Vector3(0f, 0.12f, -0.3f),
                new Vector3(-0.15f, 0.02f, -0.45f), new Vector3(0.18f, 0.04f, -0.42f)
            };
            for (int i = 0; i < bushes.Length; i++)
                Ico(c, thicket, i % 2 == 0 ? c.Colors.Accent : Color.Lerp(c.Colors.Accent, Dark, 0.3f), bushes[i], Vector3.one * (0.32f + (i % 3) * 0.06f));
            Color thorn = new Color(0.45f, 0.36f, 0.22f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.4f;
                Cone(c, thicket, thorn, new Vector3(Mathf.Cos(a) * 0.3f, 0.2f, -0.2f + Mathf.Sin(a) * 0.32f), new Vector3(0.05f, 0.3f, 0.05f),
                    new Vector3(Mathf.Sin(a) * 40f, 0f, -Mathf.Cos(a) * 40f));
            }
            for (int k = -1; k <= 1; k += 2)
            {
                Part(thicket, PrimitiveType.Cylinder, Wood, new Vector3(k * 0.12f, 0.35f, -0.15f + k * 0.12f), new Vector3(0.04f, 0.3f, 0.04f), new Vector3(0f, 0f, k * 12f));
                Ico(c, thicket, c.Colors.Accent, new Vector3(k * 0.18f, 0.66f, -0.15f + k * 0.12f), new Vector3(0.24f, 0.2f, 0.24f));
            }
            for (int i = 0; i < 4; i++)
                Part(thicket, PrimitiveType.Sphere, i % 2 == 0 ? new Color(0.95f, 0.55f, 0.7f) : new Color(0.98f, 0.9f, 0.5f),
                    bushes[i] + new Vector3(0.08f, 0.18f, 0.06f), Vector3.one * 0.06f, Vector3.zero, false);

            // Head: a heavy wedge, a snout, tusks curling up.
            Transform neck = Pivot(body, "Neck", new Vector3(0f, 0.02f, 0.72f));
            c.Anim.Head = neck;
            Ico(c, neck, c.Colors.Main, new Vector3(0f, 0f, 0.16f), new Vector3(0.52f, 0.5f, 0.58f));
            Ico(c, neck, c.Colors.Second, new Vector3(0f, 0.14f, 0.02f), new Vector3(0.46f, 0.3f, 0.4f));
            Color snout = Color.Lerp(c.Colors.Main, new Color(0.75f, 0.5f, 0.45f), 0.55f);
            Part(neck, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.04f, 0.42f), new Vector3(0.3f, 0.26f, 0.22f), Vector3.zero);
            Part(neck, PrimitiveType.Cylinder, snout, new Vector3(0f, -0.06f, 0.54f), new Vector3(0.26f, 0.04f, 0.22f), new Vector3(90f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(neck, PrimitiveType.Sphere, Dark, new Vector3(side * 0.05f, -0.06f, 0.575f), new Vector3(0.05f, 0.06f, 0.02f), Vector3.zero, false);
                Part(neck, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.17f, 0.1f, 0.33f), Vector3.one * 0.06f, Vector3.zero, false);
                Cone(c, neck, c.Colors.Second, new Vector3(side * 0.18f, 0.26f, 0.08f), new Vector3(0.12f, 0.22f, 0.05f), new Vector3(-30f, 0f, side * -35f));
                Cone(c, neck, Bone, new Vector3(side * 0.16f, -0.12f, 0.44f), new Vector3(0.07f, 0.4f, 0.07f), new Vector3(-30f, 0f, side * -28f));
                Cone(c, neck, Bone, new Vector3(side * 0.11f, -0.12f, 0.5f), new Vector3(0.04f, 0.16f, 0.04f), new Vector3(-10f, 0f, side * -15f));
            }
            Transform jaw = Pivot(neck, "Jaw", new Vector3(0f, -0.16f, 0.22f));
            c.Anim.Jaw = jaw;
            Part(jaw, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.04f, 0.14f), new Vector3(0.26f, 0.09f, 0.32f), Vector3.zero);

            Transform tail = Pivot(body, "Tail", new Vector3(0f, 0.18f, -0.76f));
            tail.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            c.Anim.Tail = tail;
            Part(tail, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.08f, -0.08f), new Vector3(0.05f, 0.05f, 0.24f), new Vector3(30f, 0f, 0f));
            Cone(c, tail, c.Colors.Second, new Vector3(0f, -0.18f, -0.2f), new Vector3(0.08f, 0.16f, 0.06f), new Vector3(150f, 0f, 0f));

            for (int side = -1; side <= 1; side += 2)
            {
                AddBoarLeg(c, body, new Vector3(side * 0.28f, -0.2f, 0.45f), side, true);
                AddBoarLeg(c, body, new Vector3(side * 0.28f, -0.2f, -0.52f), side, false);
            }
        }

        private static void AddBoarLeg(Ctx c, Transform body, Vector3 at, int side, bool front)
        {
            Transform hip = Pivot(body, "Hip", at);
            Part(hip, PrimitiveType.Cube, front ? c.Colors.Second : c.Colors.Main, new Vector3(0f, -0.16f, 0f), new Vector3(0.22f, 0.44f, 0.26f), Vector3.zero);
            Transform knee = Pivot(hip, "Knee", new Vector3(0f, -0.38f, 0f));
            Part(knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.13f, 0f), new Vector3(0.15f, 0.28f, 0.16f), Vector3.zero);
            Part(knee, PrimitiveType.Cube, Dark, new Vector3(0f, -0.29f, 0.03f), new Vector3(0.18f, 0.08f, 0.2f), Vector3.zero);
            c.Anim.Legs.Add(new CreatureAnimator.Leg
            {
                Hip = hip,
                Knee = knee,
                Side = side,
                Front = front,
                Group = (front ? 0 : 1) ^ (side > 0 ? 0 : 1)
            });
        }

        // ------------------------------------------------------------------ Vex, the Tunnel King

        // Main: olive skin. Second: the ragged cloak. Accent: the copper of his crown. Eyes: yellow.
        private static void BuildTunnelKing(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "TunnelKing", new Vector3(0f, 0.86f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Second, new Vector3(0f, 0.02f, -0.02f), new Vector3(0.42f, 0.46f, 0.34f));
            Ico(c, body, c.Colors.Main, new Vector3(0f, -0.04f, 0.1f), new Vector3(0.28f, 0.3f, 0.18f));
            Part(body, PrimitiveType.Cylinder, Dark, new Vector3(0f, -0.18f, 0f), new Vector3(0.4f, 0.03f, 0.34f), Vector3.zero);
            // Bombs on his belt, and a pouch.
            for (int i = 0; i < 3; i++)
            {
                Part(body, PrimitiveType.Sphere, Dark, new Vector3(-0.17f + i * 0.08f, -0.24f, 0.13f - i * 0.04f), Vector3.one * 0.09f, Vector3.zero);
                Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(-0.17f + i * 0.08f, -0.18f, 0.13f - i * 0.04f), new Vector3(0.015f, 0.04f, 0.015f), Vector3.zero, false);
            }
            Part(body, PrimitiveType.Cube, Wood, new Vector3(0.16f, -0.26f, 0.06f), new Vector3(0.12f, 0.12f, 0.08f), new Vector3(0f, 20f, 0f));

            Transform head = Pivot(body, "Head", new Vector3(0f, 0.42f, 0.08f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Main, Vector3.zero, new Vector3(0.42f, 0.36f, 0.38f));
            Cone(c, head, c.Colors.Main, new Vector3(0f, -0.03f, 0.24f), new Vector3(0.07f, 0.26f, 0.08f), new Vector3(105f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                Cone(c, head, c.Colors.Main, new Vector3(side * 0.24f, 0.06f, -0.03f), new Vector3(0.1f, 0.44f, 0.05f), new Vector3(-15f, side * 10f, side * -78f));
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.09f, 0.06f, 0.17f), new Vector3(0.08f, 0.07f, 0.04f), Vector3.zero, false);
                Part(head, PrimitiveType.Sphere, Dark, new Vector3(side * 0.09f, 0.06f, 0.19f), new Vector3(0.025f, 0.05f, 0.01f), Vector3.zero, false);
            }
            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.1f, 0.12f));
            c.Anim.Jaw = jaw;
            Part(jaw, PrimitiveType.Cube, Dark, new Vector3(0f, -0.02f, 0.05f), new Vector3(0.2f, 0.04f, 0.04f), Vector3.zero, false);
            for (int i = 0; i < 4; i++)
                Cone(c, jaw, Bone, new Vector3(-0.07f + i * 0.045f, 0f, 0.07f), new Vector3(0.02f, 0.05f, 0.02f), new Vector3(180f, 0f, 0f));
            // A crown of bent nails.
            Part(head, PrimitiveType.Cylinder, c.Colors.Accent, new Vector3(0f, 0.16f, -0.01f), new Vector3(0.28f, 0.025f, 0.26f), Vector3.zero, false);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Cone(c, head, c.Colors.Accent, new Vector3(Mathf.Cos(a) * 0.13f, 0.24f, Mathf.Sin(a) * 0.12f - 0.01f), new Vector3(0.03f, 0.16f + (i % 2) * 0.05f, 0.03f),
                    new Vector3(Mathf.Sin(a) * 18f, 0f, -Mathf.Cos(a) * 18f + (i % 3 - 1) * 10f));
            }
            Transform cloak = Pivot(body, "Cloak", new Vector3(0f, 0.2f, -0.16f));
            c.Anim.Tail = cloak;
            for (int i = 0; i < 4; i++)
                Cone(c, cloak, c.Colors.Second, new Vector3((i - 1.5f) * 0.12f, -0.32f, 0f), new Vector3(0.13f, 0.66f - (i % 2) * 0.14f, 0.04f), new Vector3(176f, 0f, (i - 1.5f) * 9f));

            for (int side = -1; side <= 1; side += 2)
            {
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.32f, 0.34f);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.3f, 0.34f);
                CreatureAnimator.Leg arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                arm.Hip.localPosition = new Vector3(side * 0.24f, 0.14f, 0.02f);
                Transform knife = Pivot(arm.Knee, side > 0 ? "KnifeR" : "KnifeL", new Vector3(0f, -0.34f, 0.04f));
                knife.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                Part(knife, PrimitiveType.Cube, Wood, new Vector3(0f, 0.04f, 0f), new Vector3(0.04f, 0.1f, 0.04f), Vector3.zero);
                Cone(c, knife, Steel, new Vector3(0f, -0.14f, 0f), new Vector3(0.06f, 0.3f, 0.02f), new Vector3(180f, 0f, 0f));
            }
        }

        // ------------------------------------------------------------------ the Bell-Ringer

        // Main: the black robe. Second: bell bronze. Accent: verdigris. Eyes: the light in the crack.
        private static void BuildBellRinger(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "BellRinger", new Vector3(0f, 1.18f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.44f, 0.8f, 0.4f));
            Transform skirt = Pivot(body, "Robe", new Vector3(0f, -0.36f, 0f));
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Cone(c, skirt, i % 3 == 0 ? Color.Lerp(c.Colors.Main, Dark, 0.4f) : c.Colors.Main, new Vector3(Mathf.Cos(a) * 0.17f, -0.25f, Mathf.Sin(a) * 0.15f),
                    new Vector3(0.18f, 0.72f, 0.12f), new Vector3(180f + Mathf.Sin(a) * 10f, -a * Mathf.Rad2Deg, Mathf.Cos(a) * 10f));
            }

            Transform head = Pivot(body, "Hood", new Vector3(0f, 0.5f, 0.16f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Main, Vector3.zero, new Vector3(0.36f, 0.4f, 0.4f));
            Cone(c, head, c.Colors.Main, new Vector3(0f, 0.18f, -0.1f), new Vector3(0.16f, 0.32f, 0.16f), new Vector3(-40f, 0f, 0f));
            Part(head, PrimitiveType.Cube, Dark, new Vector3(0f, -0.03f, 0.17f), new Vector3(0.22f, 0.26f, 0.06f), Vector3.zero, false);
            for (int side = -1; side <= 1; side += 2)
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.05f, 0.0f, 0.2f), new Vector3(0.04f, 0.03f, 0.02f), Vector3.zero, false);

            for (int side = -1; side <= 1; side += 2)
            {
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.48f, 0.5f);
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.5f, 0.6f);
            }
            CreatureAnimator.Leg right = Arm(c, 1);
            Transform hammer = Pivot(right.Knee, "Hammer", new Vector3(0f, -0.6f, 0.04f));
            hammer.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            Part(hammer, PrimitiveType.Cylinder, Wood, new Vector3(0f, -0.15f, 0f), new Vector3(0.045f, 0.32f, 0.045f), Vector3.zero);
            Part(hammer, PrimitiveType.Cylinder, c.Colors.Second, new Vector3(0f, -0.48f, 0f), new Vector3(0.16f, 0.13f, 0.16f), new Vector3(0f, 0f, 90f));

            // The yoke on his shoulders, and the great bell hanging behind and above him.
            Part(body, PrimitiveType.Cube, Wood, new Vector3(0f, 0.36f, -0.12f), new Vector3(1.1f, 0.08f, 0.1f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
                Part(body, PrimitiveType.Cube, Wood, new Vector3(side * 0.5f, 0.75f, -0.42f), new Vector3(0.07f, 0.85f, 0.07f), new Vector3(-20f, 0f, side * 6f));
            Part(body, PrimitiveType.Cube, Wood, new Vector3(0f, 1.15f, -0.56f), new Vector3(1.1f, 0.08f, 0.08f), Vector3.zero);
            Transform bell = Pivot(body, "Bell", new Vector3(0f, 1.12f, -0.56f));
            bell.localScale = Vector3.one * 1.35f;
            c.Anim.Prop = bell;
            BuildBell(bell, c.Colors.Second, c.Colors.Accent, c.Colors.Eyes);
            c.Anim.Mouth = Pivot(bell, CreatureAnimator.MouthName, new Vector3(0f, -0.4f, 0f));
        }

        /// <summary>A great bell, mouth down, hanging from the origin; a glowing crack down its back.</summary>
        public static void BuildBell(Transform root, Color bronze, Color verdigris, Color glow)
        {
            Color inside = new Color(0.06f, 0.05f, 0.04f);
            PartPublic(root, PrimitiveType.Cube, bronze, new Vector3(0f, -0.02f, 0f), new Vector3(0.14f, 0.12f, 0.05f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cylinder, bronze, new Vector3(0f, -0.12f, 0f), new Vector3(0.5f, 0.07f, 0.5f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cylinder, bronze, new Vector3(0f, -0.36f, 0f), new Vector3(0.68f, 0.2f, 0.68f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cylinder, bronze, new Vector3(0f, -0.64f, 0f), new Vector3(0.9f, 0.1f, 0.9f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cylinder, bronze, new Vector3(0f, -0.76f, 0f), new Vector3(1.02f, 0.04f, 1.02f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cylinder, inside, new Vector3(0f, -0.81f, 0f), new Vector3(0.94f, 0.01f, 0.94f), Vector3.zero);
            PartPublic(root, PrimitiveType.Sphere, inside, new Vector3(0f, -0.78f, 0f), Vector3.one * 0.18f, Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.7f;
                PartPublic(root, PrimitiveType.Cube, verdigris, new Vector3(Mathf.Cos(a) * 0.36f, -0.3f - (i % 3) * 0.12f, Mathf.Sin(a) * 0.36f),
                    new Vector3(0.12f, 0.1f, 0.03f), new Vector3(0f, -a * Mathf.Rad2Deg + 90f, 0f));
            }
            // The crack zig-zags down the back, light showing through.
            Vector3[] crack = { new Vector3(0f, -0.16f, -0.26f), new Vector3(0.05f, -0.3f, -0.33f), new Vector3(-0.02f, -0.46f, -0.39f), new Vector3(0.04f, -0.62f, -0.44f) };
            for (int i = 0; i < crack.Length; i++)
                PartPublic(root, PrimitiveType.Cube, glow, crack[i], new Vector3(0.035f, 0.17f, 0.03f), new Vector3(-20f, 0f, i % 2 == 0 ? 25f : -25f));
        }

        // ------------------------------------------------------------------ the Sunforged Idol

        // Main: sandstone. Second: dark stone (its plinth). Accent: gold leaf. Eyes: the sun's fire.
        private static void BuildSunIdol(Transform model, Ctx c)
        {
            // The plinth and the legs are stone set fast: they don't move.
            Part(model, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0.06f, 0f), new Vector3(1.6f, 0.12f, 1.6f), Vector3.zero);
            Part(model, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0.25f, 0f), new Vector3(1.36f, 0.3f, 1.36f), Vector3.zero);
            Part(model, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.41f, 0f), new Vector3(1.4f, 0.04f, 1.4f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(model, PrimitiveType.Cube, c.Colors.Main, new Vector3(side * 0.18f, 0.72f, 0f), new Vector3(0.26f, 0.62f, 0.3f), Vector3.zero);
                Part(model, PrimitiveType.Cube, c.Colors.Accent, new Vector3(side * 0.18f, 0.48f, 0.02f), new Vector3(0.28f, 0.08f, 0.34f), Vector3.zero);
            }

            Transform body = Pivot(model, "SunIdol", new Vector3(0f, 1.3f, 0f));
            c.Anim.BodyPivot = body;
            Part(body, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0.1f, 0f), new Vector3(0.8f, 0.66f, 0.46f), Vector3.zero);
            Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.28f, 0f), new Vector3(0.82f, 0.07f, 0.48f), Vector3.zero);
            Part(body, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, -0.27f, 0f), new Vector3(0.7f, 0.12f, 0.44f), Vector3.zero);
            Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, -0.48f, 0.2f), new Vector3(0.32f, 0.36f, 0.04f), Vector3.zero);
            Part(body, PrimitiveType.Cylinder, c.Colors.Eyes, new Vector3(0f, 0.08f, 0.235f), new Vector3(0.24f, 0.015f, 0.24f), new Vector3(90f, 0f, 0f), false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Vector3 ray = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                Part(body, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.08f, 0.235f) + ray * 0.17f, new Vector3(0.03f, 0.08f, 0.01f), new Vector3(0f, 0f, a), false);
            }
            // Cracks in the stone.
            Part(body, PrimitiveType.Cube, Dark, new Vector3(0.25f, 0.0f, 0.232f), new Vector3(0.02f, 0.3f, 0.01f), new Vector3(0f, 0f, 25f), false);
            Part(body, PrimitiveType.Cube, Dark, new Vector3(-0.3f, -0.12f, 0.232f), new Vector3(0.02f, 0.2f, 0.01f), new Vector3(0f, 0f, -35f), false);

            Transform head = Pivot(body, "Head", new Vector3(0f, 0.66f, 0f));
            c.Anim.Head = head;
            Part(head, PrimitiveType.Cube, c.Colors.Main, Vector3.zero, new Vector3(0.4f, 0.44f, 0.4f), Vector3.zero);
            Part(head, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, 0.09f, 0.2f), new Vector3(0.44f, 0.07f, 0.08f), Vector3.zero);
            Part(head, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.02f, 0.22f), new Vector3(0.06f, 0.14f, 0.06f), Vector3.zero);
            Part(head, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, -0.2f, 0.2f), new Vector3(0.26f, 0.22f, 0.1f), Vector3.zero);
            Part(head, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, 0.2f, 0f), new Vector3(0.44f, 0.06f, 0.44f), Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(head, PrimitiveType.Cube, c.Colors.Eyes, new Vector3(side * 0.09f, 0.04f, 0.205f), new Vector3(0.1f, 0.03f, 0.02f), Vector3.zero, false);
                Part(head, PrimitiveType.Cube, c.Colors.Accent, new Vector3(side * 0.22f, -0.05f, 0f), new Vector3(0.05f, 0.34f, 0.24f), Vector3.zero);
            }

            // The halo of golden rays behind the head, turning.
            Transform halo = Pivot(body, "Halo", new Vector3(0f, 0.72f, -0.3f));
            c.Anim.Tail = halo;
            Part(halo, PrimitiveType.Cylinder, c.Colors.Accent, Vector3.zero, new Vector3(0.95f, 0.015f, 0.95f), new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                Vector3 dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                Cone(c, halo, i % 2 == 0 ? c.Colors.Accent : c.Colors.Eyes, dir * (0.6f + (i % 2) * 0.06f), new Vector3(0.09f, 0.32f + (i % 2) * 0.14f, 0.04f), new Vector3(0f, 0f, a));
            }

            // Four arms: the upper pair holds the sun overhead, the lower pair strikes.
            for (int pair = 0; pair < 2; pair++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    AddRevenantLimb(c, body, side, true, c.Colors.Main, pair == 0 ? 0.48f : 0.42f, pair == 0 ? 0.48f : 0.5f);
                    CreatureAnimator.Leg arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                    arm.Hip.localPosition = new Vector3(side * 0.46f, pair == 0 ? 0.32f : 0.02f, pair == 0 ? -0.04f : 0.06f);
                    Part(arm.Hip, PrimitiveType.Cube, c.Colors.Accent, new Vector3(0f, -0.2f, 0f), new Vector3(0.18f, 0.06f, 0.18f), Vector3.zero);
                    Part(arm.Knee, PrimitiveType.Cube, c.Colors.Main, new Vector3(0f, -0.5f, 0.04f), new Vector3(0.22f, 0.12f, 0.26f), Vector3.zero);
                }
            }
            Transform orb = Pivot(body, "SunOrb", new Vector3(0f, 1.42f, 0.05f));
            c.Anim.Prop = orb;
            Part(orb, PrimitiveType.Sphere, c.Colors.Eyes, Vector3.zero, Vector3.one * 0.62f, Vector3.zero, false);
            Part(orb, PrimitiveType.Sphere, Color.Lerp(c.Colors.Eyes, Color.white, 0.6f), new Vector3(0f, 0f, 0.12f), Vector3.one * 0.4f, Vector3.zero, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Part(orb, PrimitiveType.Sphere, c.Colors.Accent, new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, 0f), Vector3.one * 0.1f, Vector3.zero, false);
            }
            c.Anim.Mouth = Pivot(orb, CreatureAnimator.MouthName, Vector3.zero);
        }

        // ------------------------------------------------------------------ Hrimgar the Huntress

        // Main: grey-blue leathers. Second: white fur. Accent: frost-pale skin. Eyes: ice.
        private static void BuildHuntress(Transform model, Ctx c)
        {
            Transform body = Pivot(model, "Huntress", new Vector3(0f, 1.22f, 0f));
            c.Anim.BodyPivot = body;
            Ico(c, body, c.Colors.Main, Vector3.zero, new Vector3(0.36f, 0.6f, 0.26f));
            Part(body, PrimitiveType.Cylinder, Dark, new Vector3(0f, -0.22f, 0f), new Vector3(0.36f, 0.03f, 0.28f), Vector3.zero);
            for (int i = 0; i < 3; i++)
                Cone(c, body, Bone, new Vector3(-0.1f + i * 0.1f, -0.3f, 0.13f), new Vector3(0.03f, 0.1f, 0.03f), new Vector3(180f, 0f, 0f));
            // A fur mantle.
            Ico(c, body, c.Colors.Second, new Vector3(0f, 0.28f, -0.02f), new Vector3(0.5f, 0.18f, 0.36f));
            // A skirt of hide strips.
            for (int i = 0; i < 5; i++)
                Cone(c, body, c.Colors.Main, new Vector3((i - 2) * 0.08f, -0.42f, 0.04f), new Vector3(0.09f, 0.34f, 0.04f), new Vector3(180f, 0f, (i - 2) * 6f));

            Transform head = Pivot(body, "Head", new Vector3(0f, 0.48f, 0.04f));
            c.Anim.Head = head;
            Ico(c, head, c.Colors.Accent, Vector3.zero, new Vector3(0.22f, 0.28f, 0.24f));
            for (int side = -1; side <= 1; side += 2)
                Part(head, PrimitiveType.Sphere, c.Colors.Eyes, new Vector3(side * 0.05f, 0.02f, 0.11f), new Vector3(0.04f, 0.02f, 0.02f), Vector3.zero, false);
            // The wolf-pelt hood: the wolf's head over her brow, its pelt down her back.
            Ico(c, head, c.Colors.Second, new Vector3(0f, 0.1f, -0.02f), new Vector3(0.3f, 0.22f, 0.32f));
            Part(head, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, 0.14f, 0.17f), new Vector3(0.12f, 0.1f, 0.2f), new Vector3(10f, 0f, 0f));
            Part(head, PrimitiveType.Cube, Dark, new Vector3(0f, 0.15f, 0.275f), new Vector3(0.05f, 0.04f, 0.03f), Vector3.zero, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Cone(c, head, c.Colors.Second, new Vector3(side * 0.09f, 0.24f, 0.02f), new Vector3(0.07f, 0.16f, 0.04f), new Vector3(-10f, 0f, side * -12f));
                Part(head, PrimitiveType.Sphere, Dark, new Vector3(side * 0.05f, 0.18f, 0.2f), Vector3.one * 0.025f, Vector3.zero, false);
                Cone(c, head, Bone, new Vector3(side * 0.03f, 0.09f, 0.26f), new Vector3(0.015f, 0.05f, 0.015f), new Vector3(180f, 0f, 0f));
            }
            Transform pelt = Pivot(body, "Pelt", new Vector3(0f, 0.34f, -0.15f));
            c.Anim.Tail = pelt;
            for (int i = 0; i < 3; i++)
                Cone(c, pelt, c.Colors.Second, new Vector3((i - 1) * 0.14f, -0.4f, 0f), new Vector3(0.16f, 0.85f - Mathf.Abs(i - 1) * 0.15f, 0.04f), new Vector3(177f, 0f, (i - 1) * 8f));
            Part(pelt, PrimitiveType.Cube, c.Colors.Second, new Vector3(0f, -0.85f, -0.05f), new Vector3(0.1f, 0.35f, 0.1f), new Vector3(-15f, 0f, 0f));

            for (int side = -1; side <= 1; side += 2)
            {
                AddRevenantLimb(c, body, side, false, c.Colors.Main, 0.56f, 0.58f);
                CreatureAnimator.Leg leg = c.Anim.Legs[c.Anim.Legs.Count - 1];
                Ico(c, leg.Knee, c.Colors.Second, new Vector3(0f, -0.38f, 0f), new Vector3(0.18f, 0.14f, 0.18f));
                AddRevenantLimb(c, body, side, true, c.Colors.Main, 0.42f, 0.48f);
                CreatureAnimator.Leg arm = c.Anim.Legs[c.Anim.Legs.Count - 1];
                arm.Hip.localPosition = new Vector3(side * 0.26f, 0.22f, 0f);
                Ico(c, arm.Hip, c.Colors.Second, new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.16f, 0.24f));
            }

            // The ice spear, pointing forward from her right hand.
            CreatureAnimator.Leg right = Arm(c, 1);
            Transform spear = Pivot(right.Knee, "Spear", new Vector3(0f, -0.48f, 0.04f));
            spear.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            c.Anim.Prop = spear;
            BuildSpear(spear, c.Colors.Eyes, c.Colors.Second);

            // Javelins in a quiver on her back.
            for (int i = 0; i < 3; i++)
                Part(body, PrimitiveType.Cylinder, Wood, new Vector3(-0.08f + i * 0.07f, 0.12f, -0.18f), new Vector3(0.025f, 0.45f, 0.025f), new Vector3(-12f, 0f, 20f));
            Part(body, PrimitiveType.Cylinder, Wood, new Vector3(0f, -0.05f, -0.18f), new Vector3(0.14f, 0.2f, 0.1f), new Vector3(-12f, 0f, 20f));
        }

        /// <summary>A long spear along -Y from its grip (the origin): shaft, ice head, a fur tassel.</summary>
        public static void BuildSpear(Transform root, Color ice, Color fur)
        {
            PartPublic(root, PrimitiveType.Cylinder, Wood, new Vector3(0f, 0.15f, 0f), new Vector3(0.035f, 0.8f, 0.035f), Vector3.zero);
            PartPublic(root, PrimitiveType.Cube, fur, new Vector3(0f, -0.62f, 0f), new Vector3(0.07f, 0.1f, 0.07f), Vector3.zero);
            GameObject head = PartPublic(root, PrimitiveType.Capsule, ice, new Vector3(0f, -0.86f, 0f), new Vector3(0.09f, 0.22f, 0.05f), Vector3.zero);
            head.transform.localScale = new Vector3(0.09f, 0.22f, 0.05f);
            PartPublic(root, PrimitiveType.Cube, ice, new Vector3(0f, -1.04f, 0f), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0f, 45f, 0f));
        }

        // Part() for the public builders (props a boss's moves throw or drop), which have no context.
        private static GameObject PartPublic(Transform parent, PrimitiveType type, Color color, Vector3 position, Vector3 scale, Vector3 euler)
        {
            return Part(parent, type, color, position, scale, euler);
        }
    }
}
