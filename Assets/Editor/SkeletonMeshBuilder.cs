using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Bakes the skeletons' bones (see <see cref="SkeletonMeshes"/>) as smooth implicit surfaces,
    /// each in the space of the humanoid template's joint that carries it: the skull, jaw, spine and
    /// ribcage on the upper body, the pelvis and a ragged loincloth on the model, and each limb's
    /// bones on its shoulder, elbow, hip or knee. Coordinates are the model's at scale 1 (feet at
    /// y 0). Crevices are darkened in the vertex colours; the hands reuse the fleshed hand's finger
    /// paths, open or closed round a grip. Included in PoeClone > Maintenance > Equipment > Reshape Humanoids.
    /// </summary>
    public static class SkeletonMeshBuilder
    {
        private const string MeshDir = "Assets/Meshes/Skeleton";
        private const string AssetPath = "Assets/Resources/SkeletonMeshes.asset";
        private const string MaterialPath = "Assets/Materials/Stylized/SkeletonBone.mat";

        // The template's joints (AnatomyRigBuilder): shoulders, elbows and wrists, hips and knees.
        private const float ShoulderX = .36f, ShoulderY = 1.5f;

        // The eye sockets: carved from the skull, and where the kind's glowing eyes sit.
        public static readonly Vector3 OrbitL = new Vector3(-.052f, 1.878f, .118f), OrbitR = new Vector3(.052f, 1.878f, .118f);
        private static readonly Vector3 OrbitSize = new Vector3(.041f, .036f, .05f);

        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(MeshDir))
                AssetDatabase.CreateFolder("Assets/Meshes", "Skeleton");

            var set = AssetDatabase.LoadAssetAtPath<SkeletonMeshes>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SkeletonMeshes>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            set.Bone = BoneMaterial();
            set.Skull = Save(Skull());
            set.Jaw = Save(Jaw());
            set.Spine = Save(Spine());
            set.Ribcage = Save(Ribcage());
            set.Pelvis = Save(Pelvis());
            set.Loincloth = Save(Loincloth());
            set.HumerusL = Save(Humerus(-1));
            set.HumerusR = Save(Humerus(1));
            set.FemurL = Save(Femur(-1));
            set.FemurR = Save(Femur(1));
            set.ShinL = Save(Shin(-1));
            set.ShinR = Save(Shin(1));
            set.ForearmL = Save(Forearm(true, false));
            set.ForearmR = Save(Forearm(false, false));
            set.ForearmLGrip = Save(Forearm(true, true));
            set.ForearmRGrip = Save(Forearm(false, true));
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ head

        private static Mesh Skull()
        {
            var v = new AnatomyVolume(.02f);
            // Cranium: long front to back, with a rounded occiput and flat temples.
            v.Ellipsoid(new Vector3(0, 1.915f, -.015f), new Vector3(.128f, .128f, .15f));
            v.Ellipsoid(new Vector3(0, 1.9f, .045f), new Vector3(.118f, .112f, .1f));
            v.Ellipsoid(new Vector3(0, 1.86f, -.095f), new Vector3(.095f, .08f, .07f));
            // Brow ridge over the sockets.
            v.Capsule(new Vector3(-.085f, 1.912f, .118f), new Vector3(.085f, 1.912f, .118f), .02f, .02f);
            // Cheekbones and the arches running back to the ears.
            foreach (float s in new[] { -1f, 1f })
            {
                v.Ellipsoid(new Vector3(s * .078f, 1.838f, .1f), new Vector3(.034f, .028f, .036f));
                v.Capsule(new Vector3(s * .095f, 1.842f, .085f), new Vector3(s * .118f, 1.848f, -.005f), .012f, .01f);
                // Mastoid behind the jaw joint.
                v.Ellipsoid(new Vector3(s * .095f, 1.8f, -.035f), new Vector3(.018f, .03f, .02f));
            }
            // Upper jaw; its teeth are baked apart (below), crisp rather than blended into it.
            v.Ellipsoid(new Vector3(0, 1.79f, .085f), new Vector3(.066f, .045f, .058f));
            // Sockets, the nasal opening and the hollow temples.
            v.Carve(OrbitL, OrbitSize);
            v.Carve(OrbitR, OrbitSize);
            v.Carve(new Vector3(0, 1.822f, .152f), new Vector3(.016f, .027f, .04f));
            v.Carve(new Vector3(-.142f, 1.875f, .03f), new Vector3(.03f, .04f, .05f));
            v.Carve(new Vector3(.142f, 1.875f, .03f), new Vector3(.03f, .04f, .05f));
            Mesh skull = Bake("SkeletonSkull", v, new Vector3(-.17f, 1.7f, -.2f), new Vector3(.17f, 2.07f, .2f), .005f, .0036f, Hollows);
            return Combine(skull, TeethRow("SkeletonUpperTeeth", 1.758f, .061f, .051f, .071f, 1));
        }

        // Darker inside the sockets and nose, where no light reaches.
        private static float Hollows(Vector3 p)
        {
            float Inside(Vector3 c, Vector3 r)
            {
                Vector3 d = new Vector3((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z);
                return d.magnitude;
            }
            float socket = Mathf.Min(Inside(OrbitL, OrbitSize), Inside(OrbitR, OrbitSize));
            float nose = Inside(new Vector3(0, 1.822f, .152f), new Vector3(.016f, .027f, .04f));
            return Mathf.Clamp01(Mathf.InverseLerp(1.15f, .9f, Mathf.Min(socket, nose))) * .85f;
        }

        // A row of teeth round an arc in front of the jaw (incisors at the front, molars behind),
        // set in a thin gum ridge and blended only slightly, so each tooth stands apart.
        private static Mesh TeethRow(string name, float y, float radiusX, float radiusZ, float frontZ, float down)
        {
            var v = new AnatomyVolume(.002f);
            const int count = 14;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(-82f, 82f, i / (float)(count - 1)) * Mathf.Deg2Rad;
                Vector3 c = new Vector3(Mathf.Sin(a) * radiusX, y, frontZ - radiusZ + Mathf.Cos(a) * radiusZ);
                float molar = Mathf.Abs(Mathf.Sin(a));
                v.Ellipsoid(c - new Vector3(0, down * .007f, 0), new Vector3(Mathf.Lerp(.0062f, .0095f, molar), .0125f, Mathf.Lerp(.0055f, .0095f, molar)));
            }
            return Bake(name, v, new Vector3(-radiusX - .02f, y - .03f, frontZ - radiusZ * 2 - .02f),
                new Vector3(radiusX + .02f, y + .03f, frontZ + .02f), .0022f, .0012f, null);
        }

        private static Mesh Combine(Mesh a, Mesh b)
        {
            var mesh = new Mesh { name = a.name };
            mesh.CombineMeshes(new[] { new CombineInstance { mesh = a, transform = Matrix4x4.identity }, new CombineInstance { mesh = b, transform = Matrix4x4.identity } }, true, true);
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Jaw()
        {
            var v = new AnatomyVolume(.012f);
            var path = new List<Vector3>();
            // Up each ramus to the joint under the ear, along the jaw line and round the chin.
            path.Add(new Vector3(-.1f, 1.835f, -.02f));
            path.Add(new Vector3(-.098f, 1.77f, -.005f));
            path.Add(new Vector3(-.09f, 1.735f, .03f));
            path.Add(new Vector3(-.055f, 1.712f, .095f));
            path.Add(new Vector3(0, 1.704f, .122f));
            path.Add(new Vector3(.055f, 1.712f, .095f));
            path.Add(new Vector3(.09f, 1.735f, .03f));
            path.Add(new Vector3(.098f, 1.77f, -.005f));
            path.Add(new Vector3(.1f, 1.835f, -.02f));
            v.Tube(Smooth(path, 6), t => .01f + .003f * Mathf.Sin(t * Mathf.PI));
            foreach (float s in new[] { -1f, 1f })
                v.Capsule(new Vector3(s * .096f, 1.77f, .0f), new Vector3(s * .088f, 1.815f, .03f), .008f, .005f);
            v.Ellipsoid(new Vector3(0, 1.712f, .112f), new Vector3(.024f, .016f, .014f));
            v.Ellipsoid(new Vector3(0, 1.727f, .075f), new Vector3(.055f, .012f, .04f));
            Mesh jaw = Bake("SkeletonJaw", v, new Vector3(-.13f, 1.67f, -.05f), new Vector3(.13f, 1.86f, .16f), .0045f, .003f, null);
            return Combine(jaw, TeethRow("SkeletonLowerTeeth", 1.742f, .056f, .049f, .067f, -1));
        }

        // ------------------------------------------------------------------ trunk

        // The spine's depth by height: forward at the neck, back over the ribs, forward at the waist.
        private static float SpineZ(float y)
        {
            float[] ys = { .92f, 1.12f, 1.35f, 1.58f, 1.8f };
            float[] zs = { -.06f, -.095f, -.115f, -.07f, -.035f };
            if (y <= ys[0]) return zs[0];
            for (int i = 0; i < ys.Length - 1; i++)
                if (y <= ys[i + 1])
                    return Mathf.Lerp(zs[i], zs[i + 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ys[i], ys[i + 1], y)));
            return zs[zs.Length - 1];
        }

        private static Mesh Spine()
        {
            var v = new AnatomyVolume(.008f);
            var core = new List<Vector3>();
            for (float y = .93f; y <= 1.79f; y += .02f)
                core.Add(new Vector3(0, y, SpineZ(y)));
            v.Tube(core, t => Mathf.Lerp(.016f, .011f, t));
            for (float y = .95f; y <= 1.77f; y += .043f)
            {
                float t = Mathf.InverseLerp(.95f, 1.77f, y), z = SpineZ(y);
                // Vertebral bodies shrink up the column; each has a spinous process angled down and back.
                Vector3 body = new Vector3(0, y, z);
                v.Ellipsoid(body, new Vector3(Mathf.Lerp(.034f, .02f, t), .012f, Mathf.Lerp(.028f, .017f, t)));
                v.Capsule(body - new Vector3(0, 0, .015f), body + new Vector3(0, -.022f, -.05f + .015f * t), .007f, .005f);
                foreach (float s in new[] { -1f, 1f })
                    v.Capsule(body - new Vector3(0, 0, .012f), body + new Vector3(s * Mathf.Lerp(.045f, .028f, t), .003f, -.02f), .006f, .005f);
            }
            return Bake("SkeletonSpine", v, new Vector3(-.07f, .9f, -.19f), new Vector3(.07f, 1.81f, .02f), .005f, .0042f, null);
        }

        private static Mesh Ribcage()
        {
            var v = new AnatomyVolume(.008f);
            const float sternumZ = .148f;
            float[] widths = { .115f, .165f, .2f, .222f, .236f, .242f, .238f, .228f, .212f, .19f };
            for (int k = 0; k < widths.Length; k++)
            {
                float y = 1.53f - k * .046f, spine = SpineZ(y);
                bool floating = k >= 8, falseRib = k >= 7;
                // Ends at the breastbone, short of it (false ribs) or free at the side (floating).
                float end = floating ? 75f : falseRib ? 38f : 9f;
                float frontY = Mathf.Max(1.2f, 1.47f - k * .038f) - (falseRib ? .02f * (k - 6) : 0);
                if (floating) frontY = y - .07f;
                float z0 = (spine + sternumZ) * .5f, depth = (sternumZ - spine) * .5f;
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new List<Vector3> { new Vector3(s * .012f, y + .01f, spine - .01f) };
                    for (int j = 0; j <= 14; j++)
                    {
                        float t = j / 14f, a = Mathf.Lerp(162f, end, t) * Mathf.Deg2Rad;
                        float back = Mathf.Cos(a) < 0 ? depth + .028f : depth;
                        float drop = Mathf.Pow(t, 1.4f);
                        path.Add(new Vector3(s * Mathf.Sin(a) * widths[k], Mathf.Lerp(y, frontY, drop), z0 + Mathf.Cos(a) * back));
                    }
                    v.Tube(Smooth(path, 2), t => Mathf.Lerp(.0115f, floating ? .006f : .0085f, t));
                }
            }
            // Cartilage sweeping the false ribs up to the bottom of the breastbone.
            foreach (float s in new[] { -1f, 1f })
                v.Tube(Smooth(new List<Vector3> {
                    new Vector3(s * Mathf.Sin(38f * Mathf.Deg2Rad) * widths[8], 1.13f, .11f),
                    new Vector3(s * .09f, 1.17f, .135f), new Vector3(s * .03f, 1.21f, sternumZ) }, 4), t => .0075f);
            // Breastbone.
            v.Ellipsoid(new Vector3(0, 1.47f, sternumZ - .002f), new Vector3(.038f, .03f, .013f));
            v.Capsule(new Vector3(0, 1.44f, sternumZ), new Vector3(0, 1.22f, sternumZ + .006f), .02f, .014f);
            v.Capsule(new Vector3(0, 1.22f, sternumZ + .006f), new Vector3(0, 1.18f, sternumZ - .004f), .009f, .005f);
            foreach (float s in new[] { -1f, 1f })
            {
                // Collarbones out to the shoulders, and the shoulder blades on the back.
                v.Tube(Smooth(new List<Vector3> {
                    new Vector3(s * .028f, 1.505f, sternumZ - .005f), new Vector3(s * .13f, 1.52f, .12f),
                    new Vector3(s * .24f, 1.53f, .03f), new Vector3(s * (ShoulderX - .05f), 1.535f, -.01f) }, 4),
                    t => .012f - .002f * Mathf.Sin(t * Mathf.PI));
                v.Ellipsoid(new Vector3(s * .16f, 1.39f, -.148f), new Vector3(.075f, .1f, .012f));
                v.Ellipsoid(new Vector3(s * .2f, 1.44f, -.135f), new Vector3(.05f, .05f, .014f));
                v.Capsule(new Vector3(s * .09f, 1.45f, -.16f), new Vector3(s * (ShoulderX - .05f), 1.52f, -.04f), .011f, .013f);
            }
            return Bake("SkeletonRibcage", v, new Vector3(-.36f, 1.08f, -.2f), new Vector3(.36f, 1.6f, .19f), .005f, .0045f, null);
        }

        private static Mesh Pelvis()
        {
            var v = new AnatomyVolume(.014f);
            // Sacrum between the hip bones, tail tucked under.
            v.Ellipsoid(new Vector3(0, .93f, -.075f), new Vector3(.05f, .07f, .022f));
            v.Capsule(new Vector3(0, .87f, -.08f), new Vector3(0, .83f, -.06f), .012f, .006f);
            foreach (float s in new[] { -1f, 1f })
            {
                // The flared wing of each hip bone, rimmed by its crest.
                v.Ellipsoid(new Vector3(s * .12f, .96f, -.025f), new Vector3(.07f, .06f, .022f));
                v.Ellipsoid(new Vector3(s * .145f, .95f, .01f), new Vector3(.03f, .055f, .04f));
                v.Tube(Smooth(new List<Vector3> {
                    new Vector3(s * .045f, .975f, -.08f), new Vector3(s * .12f, 1.01f, -.06f),
                    new Vector3(s * .175f, 1.0f, .0f), new Vector3(s * .16f, .955f, .065f) }, 4), t => .014f);
                // The hip socket round the femur's head, and the ring below it to the front.
                v.Ellipsoid(new Vector3(s * .135f, .86f, 0), new Vector3(.034f, .034f, .034f));
                v.Tube(Smooth(new List<Vector3> {
                    new Vector3(s * .12f, .85f, .03f), new Vector3(s * .07f, .81f, .075f), new Vector3(s * .01f, .8f, .085f) }, 4), t => .016f);
                v.Tube(Smooth(new List<Vector3> {
                    new Vector3(s * .13f, .845f, -.01f), new Vector3(s * .1f, .775f, -.015f),
                    new Vector3(s * .06f, .78f, .04f), new Vector3(s * .03f, .8f, .075f) }, 4), t => .015f);
            }
            return Bake("SkeletonPelvis", v, new Vector3(-.22f, .74f, -.13f), new Vector3(.22f, 1.04f, .12f), .005f, .0036f, null);
        }

        // ------------------------------------------------------------------ limbs

        private static Mesh Humerus(float side)
        {
            var v = new AnatomyVolume(.012f);
            v.Ellipsoid(new Vector3(-side * .006f, -.005f, -.006f), new Vector3(.036f, .036f, .036f));
            v.Ellipsoid(new Vector3(side * .02f, -.02f, .012f), new Vector3(.02f, .028f, .02f));
            v.Capsule(new Vector3(0, -.04f, 0), new Vector3(0, -.37f, -.004f), .02f, .018f);
            // Elbow end: the two epicondyles either side of the joint.
            v.Ellipsoid(new Vector3(0, -.4f, 0), new Vector3(.04f, .022f, .024f));
            v.Ellipsoid(new Vector3(-side * .035f, -.395f, -.005f), new Vector3(.014f, .016f, .014f));
            return Bake("SkeletonHumerus" + (side < 0 ? "L" : "R"), v, new Vector3(-.07f, -.44f, -.06f), new Vector3(.07f, .05f, .06f), .0045f, .0036f, null);
        }

        private static Mesh Forearm(bool left, bool grip)
        {
            var v = new AnatomyVolume(.006f);
            // The ulna (behind the little finger, with the elbow point) and the radius (thumb side,
            // flaring to the wrist).
            v.Ellipsoid(new Vector3(0, .01f, -.022f), new Vector3(.016f, .026f, .016f));
            v.Capsule(new Vector3(0, -.01f, -.016f), new Vector3(0, -.325f, -.022f), .012f, .009f);
            v.Capsule(new Vector3(0, -.03f, .01f), new Vector3(0, -.32f, .02f), .01f, .014f);
            v.Ellipsoid(new Vector3(0, -.03f, .01f), new Vector3(.016f, .012f, .016f));
            // Carpals: a knot of small bones at the wrist.
            Vector3 wrist = AnatomyMeshes.HandToElbow(new Vector3(0, .22f, 0));
            v.Ellipsoid(wrist + new Vector3(0, .012f, 0), new Vector3(.02f, .018f, .03f));
            List<Vector3>[] digits = AnatomyMeshes.Digits(left, grip);
            for (int finger = 0; finger < 4; finger++)
            {
                List<Vector3> path = digits[finger];
                Vector3 knuckle = AnatomyMeshes.HandToElbow(path[0]);
                // Metacarpal from the wrist to the knuckle, then three phalanges with knobbly joints.
                Vector3 root = AnatomyMeshes.HandToElbow(new Vector3(0, .2f, path[0].z * .55f));
                v.Capsule(root, knuckle, .0068f, .0072f);
                var bones = new List<Vector3>();
                foreach (Vector3 p in path)
                    bones.Add(AnatomyMeshes.HandToElbow(p));
                float radius = finger == 3 ? .0052f : .006f;
                v.Tube(bones, t => radius * Mathf.Lerp(1, .7f, t));
                foreach (int joint in new[] { 0, 3, 5 })
                    v.Ellipsoid(bones[joint], Vector3.one * radius * (joint == 0 ? 1.45f : 1.25f));
            }
            var thumb = new List<Vector3>();
            foreach (Vector3 p in digits[4])
                thumb.Add(AnatomyMeshes.HandToElbow(p));
            v.Tube(thumb, t => Mathf.Lerp(.0075f, .0055f, t));
            v.Ellipsoid(thumb[8], Vector3.one * .0085f);
            v.Ellipsoid(thumb[13], Vector3.one * .0075f);
            string name = "SkeletonForearm" + (left ? "L" : "R") + (grip ? "Grip" : "");
            return Bake(name, v, new Vector3(-.075f, -.6f, -.075f), new Vector3(.075f, .045f, .12f), .003f, .0018f, null);
        }

        private static Mesh Femur(float side)
        {
            var v = new AnatomyVolume(.012f);
            // Head in the hip socket, a neck out to the trochanter, the shaft angling in to the knee.
            v.Ellipsoid(new Vector3(-side * .012f, .0f, 0), Vector3.one * .036f);
            v.Capsule(new Vector3(-side * .01f, -.005f, 0), new Vector3(side * .035f, -.045f, 0), .02f, .022f);
            v.Ellipsoid(new Vector3(side * .05f, -.03f, -.006f), new Vector3(.024f, .03f, .024f));
            v.Capsule(new Vector3(side * .035f, -.06f, 0), new Vector3(0, -.37f, .004f), .022f, .02f);
            foreach (float s in new[] { -1f, 1f })
                v.Ellipsoid(new Vector3(s * .022f, -.4f, -.004f), new Vector3(.024f, .026f, .032f));
            return Bake("SkeletonFemur" + (side < 0 ? "L" : "R"), v, new Vector3(-.08f, -.45f, -.06f), new Vector3(.08f, .05f, .06f), .005f, .0036f, null);
        }

        private static Mesh Shin(float side)
        {
            var v = new AnatomyVolume(.01f);
            // Kneecap in front of the joint, the tibia's plateau and shaft, the thin fibula outside.
            v.Ellipsoid(new Vector3(0, .005f, .045f), new Vector3(.024f, .028f, .012f));
            v.Ellipsoid(new Vector3(0, -.03f, .0f), new Vector3(.046f, .02f, .036f));
            v.Capsule(new Vector3(0, -.04f, .005f), new Vector3(-side * .004f, -.35f, 0), .02f, .016f);
            v.Ellipsoid(new Vector3(-side * .028f, -.37f, .002f), new Vector3(.013f, .02f, .016f));
            v.Capsule(new Vector3(side * .033f, -.05f, -.012f), new Vector3(side * .03f, -.355f, -.006f), .009f, .008f);
            v.Ellipsoid(new Vector3(side * .034f, -.372f, -.006f), new Vector3(.011f, .02f, .013f));
            // Foot: ankle bone, heel, the arch and five metatarsals out to the toes.
            v.Ellipsoid(new Vector3(0, -.39f, .005f), new Vector3(.026f, .02f, .032f));
            v.Ellipsoid(new Vector3(0, -.405f, -.04f), new Vector3(.025f, .022f, .04f));
            v.Ellipsoid(new Vector3(-side * .004f, -.405f, .045f), new Vector3(.038f, .016f, .034f));
            for (int i = 0; i < 5; i++)
            {
                float x = Mathf.Lerp(-.028f, .03f, i / 4f) * side;
                float length = i == 0 ? .1f : Mathf.Lerp(.095f, .075f, (i - 1) / 3f);
                Vector3 a = new Vector3(x * .7f, -.405f, .065f), b = new Vector3(x * 1.15f, -.422f, .065f + length);
                v.Capsule(a, b, i == 0 ? .0085f : .0065f, i == 0 ? .0085f : .006f);
                Vector3 tip = b + new Vector3(x * .1f, -.004f, i == 0 ? .04f : .03f);
                v.Capsule(b, tip, i == 0 ? .008f : .0055f, .0045f);
                v.Ellipsoid(b, Vector3.one * (i == 0 ? .01f : .0075f));
            }
            return Bake("SkeletonShin" + (side < 0 ? "L" : "R"), v, new Vector3(-.075f, -.445f, -.09f), new Vector3(.075f, .045f, .23f), .0035f, .0024f, null);
        }

        // ------------------------------------------------------------------ cloth

        /// <summary>A rotted wrap round the hips: long front and back flaps, cut high at the sides
        /// for the legs, ending in a torn edge. Both faces, as the inside shows.</summary>
        private static Mesh Loincloth()
        {
            const int columns = 64, rows = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int j = 0; j <= rows; j++)
                for (int i = 0; i <= columns; i++)
                {
                    float a = i / (float)columns * Mathf.PI * 2, v = j / (float)rows;
                    float side = Mathf.Abs(Mathf.Sin(a));
                    float tear = .035f * Mathf.Sin(a * 13 + 1.3f) + .02f * Mathf.Sin(a * 29) + .015f * Mathf.Sin(a * 47 + 2f);
                    float bottom = Mathf.Lerp(.66f, .86f, Mathf.Pow(side, 2.5f)) + tear;
                    float y = Mathf.Lerp(.99f, bottom, v);
                    float flare = 1 + .18f * v * v;
                    float sag = .012f * Mathf.Sin(v * Mathf.PI) * Mathf.Sin(a * 5);
                    vertices.Add(new Vector3(Mathf.Sin(a) * (.19f + sag) * flare, y, Mathf.Cos(a) * (.14f + sag) * flare - .01f));
                }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < columns; i++)
                {
                    int a = j * (columns + 1) + i, b = a + 1, c = a + columns + 1, d = c + 1;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            // The inner face: the same surface, wound the other way, on its own vertices.
            int count = vertices.Count, outer = triangles.Count;
            vertices.AddRange(vertices.ToArray());
            for (int t = 0; t < outer; t += 3)
                triangles.AddRange(new[] { triangles[t] + count, triangles[t + 2] + count, triangles[t + 1] + count });
            var mesh = new Mesh { name = "SkeletonLoincloth" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ baking

        private static List<Vector3> Smooth(List<Vector3> points, int steps)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    result.Add(.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t));
                }
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        // Bakes the volume, simplifies it, then paints ambient occlusion into the vertex colours: a
        // point whose surroundings sit close in front of it (between ribs, under the brow) is
        // shaded. Painting after simplifying lets flat stretches collapse (the simplifier keeps
        // vertices whose colours differ).
        private static Mesh Bake(string name, AnatomyVolume v, Vector3 min, Vector3 max, float spacing, float error, Func<Vector3, float> darken)
        {
            Mesh mesh = v.Bake(name, min, max, spacing, p => Color.white);
            if (error > 0)
            {
                // The simplifier's degenerate-triangle thresholds suit the unit-sized anatomy; bones
                // in metres are simplified at that size and scaled back.
                Scale(mesh, SimplifyScale);
                Mesh simplified = AnatomySimplifier.Simplify(mesh, error * SimplifyScale);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = simplified;
                mesh.name = name;
                Scale(mesh, 1 / SimplifyScale);
            }
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            var colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                const float reach = .022f;
                Vector3 p = vertices[i], n = normals[i];
                float open = Mathf.Clamp01((v.Distance(p + n * reach) + v.Distance(p + n * reach * .5f) * 2) / (reach * 2));
                float shade = Mathf.Lerp(.42f, 1f, Mathf.Pow(open, .8f));
                // Old bone: faint stains rather than an even white.
                float stain = .93f + .07f * Mathf.Sin(p.x * 61 + p.y * 37) * Mathf.Sin(p.z * 53 - p.y * 29);
                Color c = new Color(1, .97f, .92f) * shade * stain;
                if (darken != null)
                    c = Color.Lerp(c, new Color(.1f, .08f, .07f), darken(p));
                c.a = 1;
                colors[i] = c;
            }
            mesh.colors = colors;
            return mesh;
        }

        private const float SimplifyScale = 20;

        private static void Scale(Mesh mesh, float factor)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] *= factor;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        // Recentres the mesh on itself and saves it, keeping its asset (and GUID) if it exists.
        private static SkeletonMeshes.Piece Save(Mesh mesh)
        {
            Vector3 centre = mesh.bounds.center;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= centre;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))
                mesh.colors32 = mesh.colors32;
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal) && !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))
                mesh.RecalculateTangents();
            MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Low);
            string path = MeshDir + "/" + mesh.name + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            return new SkeletonMeshes.Piece { Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path), Centre = centre };
        }

        private static Material BoneMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find("PoeClone/AnatomySkin");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            // Porous, dry and matte: the skin's pore relief reads as weathered bone.
            material.SetColor("_BaseColor", new Color(.9f, .88f, .8f));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Anatomy/SkinPores.png"));
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Anatomy/SkinRoughness.png"));
            material.SetFloat("_BumpScale", .45f);
            material.SetFloat("_DetailTiling", 5);
            material.SetFloat("_Smoothness", .2f);
            material.SetFloat("_Subsurface", .06f);
            material.SetFloat("_FiberStrength", 0);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
