using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Turns the humanoid rig into a skeleton: the flesh parts are hidden and bones go on the same
    /// joints (so walking, swings and deaths still animate them): a skull with dark sockets, a
    /// spine and open ribcage, a pelvis with a rag hanging from it, and limbs of thin bones with
    /// knobbly joints. Each joint's bones are one merged mesh. Rig positions are the model's at
    /// scale 1 (feet at y 0, head centre at 1.87).
    /// </summary>
    public static class SkeletonLook
    {
        private static readonly string[] Flesh = { "Thigh", "Boot", "BootCuff", "Torso", "TunicSkirt", "Belt", "Upper", "Forearm", "Hand" };

        // Every skeleton's bones are the same shapes, so each joint's merged mesh is built once.
        private static readonly Dictionary<string, Mesh> Merged = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Vector3> Centres = new Dictionary<string, Vector3>();
        private static readonly Dictionary<PrimitiveType, Mesh> Primitives = new Dictionary<PrimitiveType, Mesh>();

        private struct Bone
        {
            public PrimitiveType Type;
            public Vector3 Position, Scale, Euler;
        }

        public static void Build(Transform model, EnemyKind kind)
        {
            Transform upper = Find(model, "UpperOffset");
            if (upper == null)
                return;

            // Renderers off rather than objects: the walk animator still reads the hands' positions.
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (System.Array.IndexOf(Flesh, r.name) >= 0)
                    r.enabled = false;
            }

            Color bone = kind.Skin;
            Color dark = Color.Lerp(kind.Skin, Color.black, 0.75f);

            foreach (string side in new[] { "L", "R" })
            {
                Transform leg = Find(model, "Leg" + side);
                if (leg != null)
                {
                    Attach(leg, "Femur", bone, Capsule(new Vector3(0f, -0.21f, 0f), new Vector3(0.09f, 0.2f, 0.09f)));
                    Transform knee = Find(leg, "Knee");
                    if (knee != null)
                        Attach(knee, "Shin", bone,
                            Sphere(Vector3.zero, Vector3.one * 0.13f),
                            Capsule(new Vector3(0f, -0.2f, 0f), new Vector3(0.075f, 0.19f, 0.075f)),
                            Cube(new Vector3(0f, -0.4f, 0.06f), new Vector3(0.12f, 0.06f, 0.24f)));
                }

                Transform arm = Find(model, "Arm" + side);
                if (arm != null)
                {
                    Attach(arm, "Humerus", bone,
                        Sphere(Vector3.zero, Vector3.one * 0.13f),
                        Capsule(new Vector3(0f, -0.21f, 0f), new Vector3(0.075f, 0.2f, 0.075f)));
                    Transform elbow = Find(arm, "Elbow");
                    if (elbow != null)
                        Attach(elbow, "Forearm", bone,
                            Sphere(Vector3.zero, Vector3.one * 0.1f),
                            Capsule(new Vector3(-0.03f, -0.14f, 0f), new Vector3(0.045f, 0.14f, 0.045f)),
                            Capsule(new Vector3(0.03f, -0.14f, 0f), new Vector3(0.045f, 0.14f, 0.045f)),
                            Cube(new Vector3(0f, -0.3f, 0f), new Vector3(0.11f, 0.09f, 0.06f)),
                            Cube(new Vector3(-0.035f, -0.38f, 0f), new Vector3(0.025f, 0.09f, 0.025f)),
                            Cube(new Vector3(0f, -0.385f, 0f), new Vector3(0.025f, 0.1f, 0.025f)),
                            Cube(new Vector3(0.035f, -0.38f, 0f), new Vector3(0.025f, 0.09f, 0.025f)));
                }
            }

            // Pelvis on the model itself (it stays put while the upper body leans), with a rag.
            Attach(model, "Pelvis", bone,
                Cube(new Vector3(0f, 0.9f, -0.02f), new Vector3(0.42f, 0.13f, 0.2f)),
                Sphere(new Vector3(-0.19f, 0.86f, 0f), Vector3.one * 0.14f),
                Sphere(new Vector3(0.19f, 0.86f, 0f), Vector3.one * 0.14f));
            Attach(model, "Rag", kind.Pants,
                Cube(new Vector3(0f, 0.74f, 0.11f), new Vector3(0.3f, 0.32f, 0.03f), new Vector3(-6f, 0f, 0f)),
                Cube(new Vector3(0f, 0.76f, -0.13f), new Vector3(0.34f, 0.28f, 0.03f), new Vector3(5f, 0f, 0f)),
                Cube(new Vector3(0f, 0.9f, 0f), new Vector3(0.46f, 0.06f, 0.26f)));

            Attach(upper, "Ribcage", bone, Ribcage());

            // The skull: the rig's own head, narrower and bone-white, with a jaw and dark sockets
            // round the kind's glowing eyes.
            Transform head = Find(upper, "Head");
            if (head != null)
            {
                head.localPosition = new Vector3(0f, 1.9f, 0f);
                head.localScale = new Vector3(0.42f, 0.44f, 0.46f);
            }
            Attach(upper, "Jaw", bone,
                Cube(new Vector3(0f, 1.72f, 0.07f), new Vector3(0.26f, 0.09f, 0.24f)),
                Cylinder(new Vector3(0f, 1.62f, -0.03f), new Vector3(0.07f, 0.06f, 0.07f)));
            Attach(upper, "Sockets", dark,
                Sphere(new Vector3(-0.085f, 1.91f, 0.19f), new Vector3(0.12f, 0.11f, 0.07f)),
                Sphere(new Vector3(0.085f, 1.91f, 0.19f), new Vector3(0.12f, 0.11f, 0.07f)),
                Cube(new Vector3(0f, 1.83f, 0.215f), new Vector3(0.05f, 0.06f, 0.04f)),
                Cube(new Vector3(0f, 1.765f, 0.19f), new Vector3(0.2f, 0.025f, 0.02f)));
            foreach (string eye in new[] { "EyeL", "EyeR" })
            {
                Transform t = Find(upper, eye);
                if (t == null) continue;
                t.localPosition = new Vector3(eye == "EyeL" ? -0.085f : 0.085f, 1.91f, 0.215f);
                t.localScale = new Vector3(0.045f, 0.045f, 0.03f);
            }
        }

        // Spine, collarbones, breastbone and four ribs curving round from the spine, open at the front.
        private static Bone[] Ribcage()
        {
            var bones = new List<Bone>
            {
                Cylinder(new Vector3(0f, 1.22f, -0.1f), new Vector3(0.075f, 0.38f, 0.075f)),
                Cube(new Vector3(0f, 1.53f, 0.01f), new Vector3(0.88f, 0.055f, 0.07f)),
                Cube(new Vector3(0f, 1.3f, 0.17f), new Vector3(0.06f, 0.3f, 0.04f))
            };
            float[] heights = { 1.44f, 1.32f, 1.2f, 1.08f };
            float[] widths = { 0.27f, 0.3f, 0.28f, 0.23f };
            const int segments = 6;
            for (int r = 0; r < heights.Length; r++)
            {
                foreach (float sign in new[] { -1f, 1f })
                {
                    for (int s = 0; s < segments; s++)
                    {
                        // From beside the breastbone (25 degrees off the front) round to the spine.
                        float a0 = Mathf.Lerp(25f, 175f, s / (float)segments) * Mathf.Deg2Rad;
                        float a1 = Mathf.Lerp(25f, 175f, (s + 1) / (float)segments) * Mathf.Deg2Rad;
                        Vector3 p0 = new Vector3(sign * Mathf.Sin(a0) * widths[r], heights[r], Mathf.Cos(a0) * 0.19f - 0.02f);
                        Vector3 p1 = new Vector3(sign * Mathf.Sin(a1) * widths[r], heights[r] + 0.015f, Mathf.Cos(a1) * 0.19f - 0.02f);
                        Vector3 along = p1 - p0;
                        bones.Add(new Bone
                        {
                            Type = PrimitiveType.Cube,
                            Position = (p0 + p1) * 0.5f,
                            Scale = new Vector3(0.045f, 0.04f, along.magnitude + 0.02f),
                            Euler = Quaternion.LookRotation(along).eulerAngles
                        });
                    }
                }
            }
            return bones.ToArray();
        }

        private static Bone Cube(Vector3 p, Vector3 s, Vector3 euler = default) => new Bone { Type = PrimitiveType.Cube, Position = p, Scale = s, Euler = euler };
        private static Bone Sphere(Vector3 p, Vector3 s) => new Bone { Type = PrimitiveType.Sphere, Position = p, Scale = s };
        private static Bone Capsule(Vector3 p, Vector3 s) => new Bone { Type = PrimitiveType.Capsule, Position = p, Scale = s };
        private static Bone Cylinder(Vector3 p, Vector3 s) => new Bone { Type = PrimitiveType.Cylinder, Position = p, Scale = s };

        // One renderer per joint: the bones merged into a mesh centred on itself (so the hover
        // outline, which scales each part about its pivot, sits evenly round it).
        private static void Attach(Transform joint, string name, Color color, params Bone[] bones)
        {
            if (!Merged.TryGetValue(name, out Mesh mesh))
            {
                var parts = new CombineInstance[bones.Length];
                var bounds = new Bounds();
                for (int i = 0; i < bones.Length; i++)
                {
                    Bone b = bones[i];
                    parts[i] = new CombineInstance
                    {
                        mesh = PrimitiveMesh(b.Type),
                        transform = Matrix4x4.TRS(b.Position, Quaternion.Euler(b.Euler), b.Scale)
                    };
                    if (i == 0) bounds = new Bounds(b.Position, Vector3.zero);
                    else bounds.Encapsulate(b.Position);
                }
                Matrix4x4 recentre = Matrix4x4.Translate(-bounds.center);
                for (int i = 0; i < parts.Length; i++)
                    parts[i].transform = recentre * parts[i].transform;
                mesh = new Mesh { name = "Skeleton" + name };
                mesh.CombineMeshes(parts, true, true);
                mesh.RecalculateBounds();
                Merged[name] = mesh;
                Centres[name] = bounds.center;
            }

            // A primitive gives the shared toon material and the colour block; its mesh is swapped.
            GameObject go = RuntimePrimitives.Create(PrimitiveType.Cube, joint, color);
            go.name = "Bones" + name;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.localPosition = Centres[name];
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (!Primitives.TryGetValue(type, out Mesh mesh))
            {
                GameObject temp = GameObject.CreatePrimitive(type);
                mesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(temp);
                Primitives[type] = mesh;
            }
            return mesh;
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
