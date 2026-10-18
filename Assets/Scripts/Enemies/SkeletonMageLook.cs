using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Dresses a skeleton (see <see cref="SkeletonLook"/>) as the Skeleton Mage: a deep, peaked
    /// hood with the skull and its glowing eyes looking out of the shadow inside, its hem falling
    /// over the shoulders, strips of black and brown cloth hanging down the back (moving like the
    /// player's cape), and a staff of dark wood and bone with bone prongs cradling its orb.
    /// </summary>
    public static class SkeletonMageLook
    {
        private static readonly Color Bone = new Color(0.86f, 0.82f, 0.70f);
        private static readonly Color Wood = new Color(0.20f, 0.13f, 0.09f);
        private static readonly Color Wrap = new Color(0.10f, 0.08f, 0.08f);
        private static readonly Color HoodInside = new Color(0.025f, 0.02f, 0.03f);

        private static Mesh hoodOuter, hoodInner;

        public static void Build(Transform model, EnemyKind kind)
        {
            Transform upper = Find(model, "UpperOffset");
            if (upper == null)
                return;

            // The hood: a shell round the skull, open at the face, its hem falling onto the
            // shoulders as a cowl, dark inside.
            BuildHoodMeshes();
            GameObject outer = RuntimePrimitives.CreateMesh(hoodOuter, upper, kind.Cloth, "MageHood");
            outer.transform.localPosition = HoodCentre;
            GameObject inner = RuntimePrimitives.CreateMesh(hoodInner, upper, HoodInside, "MageHoodInside");
            inner.transform.localPosition = HoodCentre;

            // Rags down the back, of different lengths, hanging from under the cowl.
            Rag(upper, new Vector3(-0.14f, 1.62f, -0.19f), 0.15f, 0.82f, -5f, new Color(0.22f, 0.15f, 0.10f));
            Rag(upper, new Vector3(0.01f, 1.64f, -0.21f), 0.2f, 1.02f, 1f, kind.Pants);
            Rag(upper, new Vector3(0.15f, 1.62f, -0.19f), 0.13f, 0.68f, 6f, new Color(0.15f, 0.10f, 0.07f));

            // The eyes burn a little larger, so they read from inside the hood's shadow.
            foreach (string eye in new[] { "EyeL", "EyeR" })
            {
                Transform t = Find(upper, eye);
                if (t != null)
                    t.localScale = new Vector3(.04f, .032f, .024f);
            }
        }

        /// <summary>The Skeleton Mage's staff, built onto an empty held staff (its +Y up the shaft from the grip).</summary>
        public static void BuildStaff(Transform staff, EnemyKind kind)
        {
            Part(PrimitiveType.Cylinder, staff, Wood, new Vector3(0f, 0.25f, 0f), new Vector3(0.07f, 0.8f, 0.07f), "Shaft");
            // Bound with black cloth where the hands go, an iron-dark cap at the foot.
            Part(PrimitiveType.Cylinder, staff, Wrap, new Vector3(0f, -0.08f, 0f), new Vector3(0.085f, 0.26f, 0.085f), "Wrap");
            Part(PrimitiveType.Sphere, staff, Bone, new Vector3(0f, -0.56f, 0f), new Vector3(0.09f, 0.07f, 0.09f), "Foot");
            // A stack of vertebrae below the head.
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Sphere, staff, Bone, new Vector3(0f, 0.78f + i * 0.075f, 0f), new Vector3(0.12f - i * 0.012f, 0.05f, 0.12f - i * 0.012f), "Vertebra");
            // Four curved bone prongs, like ribs, closing round the orb.
            for (int i = 0; i < 4; i++)
            {
                var prong = new GameObject("Prong").transform;
                prong.SetParent(staff, false);
                prong.localPosition = new Vector3(0f, 0.98f, 0f);
                prong.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Part(PrimitiveType.Capsule, prong, Bone, new Vector3(0f, 0.08f, 0.08f), new Vector3(0.035f, 0.1f, 0.035f), "Rib")
                    .transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
                Part(PrimitiveType.Capsule, prong, Bone, new Vector3(0f, 0.22f, 0.095f), new Vector3(0.03f, 0.08f, 0.03f), "RibTip")
                    .transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            }
            GameObject orb = Part(PrimitiveType.Sphere, staff, kind.StaffOrb, new Vector3(0f, 1.14f, 0f), Vector3.one * 0.2f, EnemyKinds.StaffOrbName);
            Part(PrimitiveType.Sphere, orb.transform, Color.Lerp(kind.StaffOrb, Color.white, 0.6f), Vector3.zero, Vector3.one * 0.55f, "OrbCore");
        }

        // A strip of cloth hanging from its top edge at top, tilted a little (roll).
        private static void Rag(Transform upper, Vector3 top, float width, float length, float roll, Color color)
        {
            GameObject rag = Part(PrimitiveType.Cube, upper, color, top + Vector3.down * length * 0.5f, new Vector3(width, length, 0.01f), "MageRag");
            rag.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            SimpleCapePhysics cloth = rag.AddComponent<SimpleCapePhysics>();
            cloth.cape = rag.transform;
            cloth.foldDepth = 0.025f;
            cloth.restPitch = 6f;
        }

        // The hood, in the upper body's space: centred a little behind the skull and larger than it.
        private static readonly Vector3 HoodCentre = new Vector3(0f, 1.92f, -0.035f);
        private static readonly Vector3 HoodRadii = new Vector3(0.2f, 0.22f, 0.23f);

        private static void BuildHoodMeshes()
        {
            if (hoodOuter != null)
                return;

            const int rings = 20, segments = 32;
            Vector3 face = new Vector3(0f, -0.3f, 1f).normalized;
            Vector3 peak = new Vector3(0f, 0.55f, -0.83f).normalized;
            float open = Mathf.Cos(46f * Mathf.Deg2Rad);

            var vertices = new List<Vector3>();
            for (int r = 0; r <= rings; r++)
            {
                float theta = Mathf.PI * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    var dir = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi));
                    Vector3 p = Vector3.Scale(dir, HoodRadii);
                    // Drawn up into a point at the back, the brow jutting out over the face, and the
                    // lower half flaring out and down over the shoulders, lower behind than in front.
                    p += peak * 0.15f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, peak)), 5f);
                    p.z += 0.07f * Mathf.Max(0f, dir.y) * Mathf.Max(0f, dir.z);
                    float below = Mathf.Max(0f, -dir.y);
                    Vector3 outward = new Vector3(dir.x, 0f, dir.z * 0.6f);
                    if (outward.sqrMagnitude > 0.0001f)
                        p += outward.normalized * 0.24f * Mathf.Pow(below, 1.5f);
                    p.y -= (0.12f + 0.06f * Mathf.Max(0f, -dir.z)) * below * below;
                    vertices.Add(p);
                }
            }

            var outside = new List<int>();
            var inside = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float theta = Mathf.PI * (r + 0.5f) / rings, phi = 2f * Mathf.PI * (s + 0.5f) / segments;
                    var dir = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi));
                    // The face opening, and the neck.
                    if (Vector3.Dot(dir, face) > open || dir.y < -0.9f)
                        continue;
                    int a = r * (segments + 1) + s, b = a + 1, c = a + segments + 1, d = c + 1;
                    outside.AddRange(new[] { a, b, c, b, d, c });
                    inside.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            hoodOuter = new Mesh { name = "MageHood" };
            hoodOuter.SetVertices(vertices);
            hoodOuter.SetTriangles(outside, 0);
            hoodOuter.RecalculateNormals();
            hoodOuter.RecalculateBounds();

            // The lining, just inside, facing in.
            var lining = new List<Vector3>(vertices.Count);
            foreach (Vector3 v in vertices)
                lining.Add(v * 0.96f);
            hoodInner = new Mesh { name = "MageHoodInside" };
            hoodInner.SetVertices(lining);
            hoodInner.SetTriangles(inside, 0);
            hoodInner.RecalculateNormals();
            hoodInner.RecalculateBounds();
        }

        private static GameObject Part(PrimitiveType type, Transform parent, Color color, Vector3 position, Vector3 scale, string name)
        {
            GameObject go = RuntimePrimitives.Create(type, parent, color);
            go.name = name;
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
