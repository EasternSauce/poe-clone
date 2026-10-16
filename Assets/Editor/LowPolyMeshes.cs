using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Mesh generators for character bodies and worn gear: solid lofts (stacked cross-sections),
    /// hollow shells with a wall thickness (cuffs, sleeves, hoods, helmet guards) and extruded
    /// outlines (shields). Saved as assets under Assets/Meshes/Stylized, updated in place so
    /// existing references survive a rebuild.
    /// </summary>
    public static class LowPolyMeshes
    {
        public const string MeshDir = "Assets/Meshes/Stylized";

        /// <summary>One cross-section: height, half-width (x), half-depth (z) and a forward (z) shift.</summary>
        public struct Ring
        {
            public float Y, X, Z, Forward;
            public Ring(float y, float x, float z, float forward = 0f) { Y = y; X = x; Z = z; Forward = forward; }
        }

        public static Ring R(float y, float radius, float forward = 0f) => new Ring(y, radius, radius, forward);

        /// <summary>
        /// Builds (or updates) every shared body and gear shape and returns them by name. All are
        /// unit-sized (about a 1 x 1 x 1 box round the origin) so a part's scale is its size.
        /// </summary>
        public static Dictionary<string, Mesh> BuildShared()
        {
            var m = new Dictionary<string, Mesh>();
            void Put(Mesh mesh) => m[mesh.name] = Save(mesh.name, mesh);

            // ---- Body
            // A capsule tapering towards its lower end (thighs, arms, hands, neck, belt).
            Put(Loft("BodyLimb", 10, 2f, false,
                R(0.5f, 0f), R(0.47f, 0.31f), R(0.4f, 0.46f), R(0.25f, 0.5f), R(0f, 0.47f),
                R(-0.25f, 0.42f), R(-0.4f, 0.38f), R(-0.47f, 0.28f), R(-0.5f, 0f)));
            // Rounded shoulders, a chest, a narrower waist and the hips.
            Put(Loft("BodyTorso", 16, 2.3f, false,
                new Ring(0.5f, 0.2f, 0.24f), new Ring(0.46f, 0.36f, 0.4f), new Ring(0.36f, 0.48f, 0.46f),
                new Ring(0.22f, 0.5f, 0.49f, 0.02f), new Ring(0.02f, 0.44f, 0.48f, 0.02f), new Ring(-0.2f, 0.36f, 0.42f),
                new Ring(-0.4f, 0.39f, 0.41f), new Ring(-0.48f, 0.34f, 0.35f), new Ring(-0.5f, 0f, 0f)));
            // A skirt flaring towards the hem.
            Put(Loft("BodySkirt", 14, 2.2f, false,
                new Ring(0.5f, 0f, 0f), new Ring(0.5f, 0.38f, 0.38f), new Ring(0f, 0.44f, 0.44f),
                new Ring(-0.5f, 0.5f, 0.5f), new Ring(-0.5f, 0f, 0f)));
            // A boot: a shaft down to the ankle, then the foot reaching forwards.
            Put(Loft("BodyBoot", 12, 2.4f, false,
                new Ring(0.5f, 0f, 0f, -0.08f), new Ring(0.47f, 0.36f, 0.3f, -0.08f), new Ring(0.3f, 0.42f, 0.33f, -0.1f),
                new Ring(-0.1f, 0.38f, 0.3f, -0.12f), new Ring(-0.28f, 0.42f, 0.4f, -0.04f), new Ring(-0.4f, 0.46f, 0.5f, 0.04f),
                new Ring(-0.5f, 0.42f, 0.46f, 0.04f), new Ring(-0.5f, 0f, 0f, 0.04f)));
            Put(Sphere("BodyHead", 14, 10));

            // ---- Gear
            // A dome with a flat base (y -0.5): helmet tops, pauldrons, knee cops, shield bosses.
            var dome = new List<Ring>();
            for (int k = 0; k <= 6; k++)
            {
                float phi = Mathf.PI * 0.5f * k / 6f;
                dome.Add(R(-0.5f + Mathf.Cos(phi), Mathf.Sin(phi) * 0.5f));
            }
            dome.Add(R(-0.5f, 0f));
            Put(Loft("GearDome", 16, 2f, false, dome.ToArray()));
            // Hollow rings: a straight band (belts, circlets, collars, trims), a cuff widening
            // upwards (gloves, boots) and a flare widening downwards (skirts, cowls, sleeves).
            Put(Shell("GearBand", 20, 0.08f, 0f, 360f, false, R(0.5f, 0.5f), R(-0.5f, 0.5f)));
            Put(Shell("GearCuff", 18, 0.07f, 0f, 360f, false, R(0.5f, 0.5f), R(-0.5f, 0.4f)));
            Put(Shell("GearFlare", 18, 0.05f, 0f, 360f, false, R(0.5f, 0.33f), R(0.1f, 0.42f), R(-0.5f, 0.5f)));
            // A flat round plate (shield faces, bezels).
            Put(Loft("GearDisc", 20, 2f, true, R(0.5f, 0f), R(0.5f, 0.5f), R(-0.5f, 0.5f), R(-0.5f, 0f)));
            // A helmet's lower half: round the sides and back, open at the face.
            Put(Shell("GearHelmGuard", 16, 0.1f, 140f, 400f, false, R(0.5f, 0.5f), R(0f, 0.49f), R(-0.5f, 0.43f)));
            // A hood, open at the face, coming to a soft point at the back of the crown.
            Put(Shell("GearHood", 18, 0.06f, 148f, 392f, false,
                R(0.5f, 0f, -0.08f), R(0.44f, 0.24f, -0.05f), R(0.3f, 0.42f, -0.02f), R(0.08f, 0.5f), R(-0.2f, 0.49f), R(-0.5f, 0.45f)));
            // Shields: a kite (round top, pointed foot) and a tall tower with an arched top.
            Put(Extrude("GearKite", 0.1f,
                new Vector2(0f, -0.5f), new Vector2(0.5f, 0.22f), new Vector2(0.44f, 0.4f), new Vector2(0.25f, 0.49f),
                new Vector2(0f, 0.52f), new Vector2(-0.25f, 0.49f), new Vector2(-0.44f, 0.4f), new Vector2(-0.5f, 0.22f)));
            Put(Extrude("GearTower", 0.1f,
                new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.38f), new Vector2(0.33f, 0.47f),
                new Vector2(0f, 0.5f), new Vector2(-0.33f, 0.47f), new Vector2(-0.5f, 0.38f)));
            // A pointed leaf (buckles, emblems, circlet points).
            Put(Extrude("GearLeaf", 0.2f,
                new Vector2(0f, -0.5f), new Vector2(0.3f, -0.22f), new Vector2(0.36f, 0.08f), new Vector2(0.2f, 0.34f),
                new Vector2(0f, 0.5f), new Vector2(-0.2f, 0.34f), new Vector2(-0.36f, 0.08f), new Vector2(-0.3f, -0.22f)));
            return m;
        }

        /// <summary>
        /// A closed solid of superellipse rings (exponent 2 is an ellipse, higher is boxier), top to
        /// bottom. A ring of zero size is a single pole vertex (a closed end). Smooth unless flat.
        /// </summary>
        public static Mesh Loft(string name, int sides, float exponent, bool flat, params Ring[] rings)
        {
            var b = new Builder();
            var starts = new int[rings.Length];
            for (int r = 0; r < rings.Length; r++)
            {
                Ring ring = rings[r];
                starts[r] = b.Count;
                if (ring.X <= 0f)
                {
                    b.Add(new Vector3(0f, ring.Y, ring.Forward));
                    continue;
                }
                for (int i = 0; i < sides; i++)
                {
                    float a = Mathf.PI * 2f * i / sides;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / exponent);
                    float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / exponent);
                    b.Add(new Vector3(x * ring.X, ring.Y, z * ring.Z + ring.Forward));
                }
            }

            // Centre for winding: faces point away from the axis at their own height.
            for (int r = 0; r + 1 < rings.Length; r++)
            {
                bool topPole = rings[r].X <= 0f, bottomPole = rings[r + 1].X <= 0f;
                int a = starts[r], c = starts[r + 1];
                float fwd = (rings[r].Forward + rings[r + 1].Forward) * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    if (topPole)
                        b.Tri(a, c + i, c + j, Radial(b, fwd, a, c + i, c + j, rings[r].Y > rings[r + 1].Y ? 1f : -1f));
                    else if (bottomPole)
                        b.Tri(a + i, a + j, c, Radial(b, fwd, a + i, a + j, c, rings[r].Y > rings[r + 1].Y ? -1f : 1f));
                    else
                        b.Quad(a + i, a + j, c + j, c + i, Radial(b, fwd, a + i, a + j, c + j, 0f));
                }
            }
            return b.ToMesh(name, flat);
        }

        /// <summary>A UV sphere of radius 0.5.</summary>
        public static Mesh Sphere(string name, int sides, int stacks, bool flat = false)
        {
            var rings = new Ring[stacks + 1];
            for (int k = 0; k <= stacks; k++)
            {
                float phi = Mathf.PI * k / stacks;
                float radius = k == 0 || k == stacks ? 0f : Mathf.Sin(phi) * 0.5f;
                rings[k] = new Ring(Mathf.Cos(phi) * 0.5f, radius, radius);
            }
            return Loft(name, sides, 2f, flat, rings);
        }

        /// <summary>
        /// A hollow surface of rings (top to bottom) with a wall <paramref name="thickness"/> thick,
        /// spanning the angles arcFrom..arcTo in degrees (0 is +x, 90 the front, +z). Open ends and
        /// the cut edges of a partial arc are closed, so it reads solid from every side.
        /// </summary>
        public static Mesh Shell(string name, int sides, float thickness, float arcFrom, float arcTo, bool flat, params Ring[] rings)
        {
            var b = new Builder();
            bool full = Mathf.Abs(arcTo - arcFrom - 360f) < 0.01f;
            int cols = full ? sides : sides + 1;
            int rows = rings.Length;
            var outer = new int[rows, cols];
            var inner = new int[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                Ring g = rings[r];
                bool pole = g.X <= 0f;
                float ix = Mathf.Max(0f, g.X - thickness), iz = Mathf.Max(0f, g.Z - thickness);
                float iy = pole ? g.Y + (r == 0 ? -thickness : thickness) : g.Y;
                for (int i = 0; i < cols; i++)
                {
                    float a = (arcFrom + (arcTo - arcFrom) * i / sides) * Mathf.Deg2Rad;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    outer[r, i] = b.Add(new Vector3(c * g.X, g.Y, s * g.Z + g.Forward));
                    inner[r, i] = b.Add(new Vector3(c * ix, iy, s * iz + g.Forward));
                }
            }

            for (int r = 0; r + 1 < rows; r++)
            {
                float fwd = (rings[r].Forward + rings[r + 1].Forward) * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    int j = full ? (i + 1) % cols : i + 1;
                    b.Quad(outer[r, i], outer[r, j], outer[r + 1, j], outer[r + 1, i], Radial(b, fwd, outer[r, i], outer[r, j], outer[r + 1, j], 0f));
                    b.Quad(inner[r, i], inner[r, j], inner[r + 1, j], inner[r + 1, i], -Radial(b, fwd, inner[r, i], inner[r, j], inner[r + 1, j], 0f));
                }
            }

            // Rims: the top and bottom edges (unless they close in a pole), and the cut ends.
            for (int i = 0; i < sides; i++)
            {
                int j = full ? (i + 1) % cols : i + 1;
                if (rings[0].X > 0f)
                    b.OwnQuad(outer[0, i], outer[0, j], inner[0, j], inner[0, i], Vector3.up);
                if (rings[rows - 1].X > 0f)
                    b.OwnQuad(outer[rows - 1, i], outer[rows - 1, j], inner[rows - 1, j], inner[rows - 1, i], Vector3.down);
            }
            if (!full)
            {
                float a0 = arcFrom * Mathf.Deg2Rad, a1 = arcTo * Mathf.Deg2Rad;
                Vector3 away0 = new Vector3(Mathf.Sin(a0), 0f, -Mathf.Cos(a0));
                Vector3 away1 = new Vector3(-Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                for (int r = 0; r + 1 < rows; r++)
                {
                    b.OwnQuad(outer[r, 0], outer[r + 1, 0], inner[r + 1, 0], inner[r, 0], away0);
                    b.OwnQuad(outer[r, sides], outer[r + 1, sides], inner[r + 1, sides], inner[r, sides], away1);
                }
            }
            return b.ToMesh(name, flat);
        }

        /// <summary>
        /// A convex outline in the XY plane extruded along z (from -depth/2 to +depth/2), flat shaded:
        /// shields and plates.
        /// </summary>
        public static Mesh Extrude(string name, float depth, params Vector2[] outline)
        {
            var b = new Builder();
            Vector2 centre = Vector2.zero;
            foreach (Vector2 p in outline)
                centre += p;
            centre /= outline.Length;

            float h = depth * 0.5f;
            int n = outline.Length;
            int frontC = b.Add(new Vector3(centre.x, centre.y, h));
            int backC = b.Add(new Vector3(centre.x, centre.y, -h));
            var front = new int[n];
            var back = new int[n];
            for (int i = 0; i < n; i++)
            {
                front[i] = b.Add(new Vector3(outline[i].x, outline[i].y, h));
                back[i] = b.Add(new Vector3(outline[i].x, outline[i].y, -h));
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.Tri(frontC, front[i], front[j], Vector3.forward);
                b.Tri(backC, back[i], back[j], Vector3.back);
                Vector2 mid = (outline[i] + outline[j]) * 0.5f - centre;
                b.Quad(front[i], front[j], back[j], back[i], new Vector3(mid.x, mid.y, 0f));
            }
            return b.ToMesh(name, true);
        }

        /// <summary>Updates an existing asset in place so its GUID (and every reference to it) is kept.</summary>
        public static Mesh Save(string name, Mesh mesh)
        {
            string path = MeshDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Replacing anatomical assets rebuilds Unity's native mesh/LOD buffers as well as
            // the serialized data. CreateAsset preserves the existing .meta GUID and references.
            if (name.StartsWith("Anatomy", System.StringComparison.Ordinal))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                AssetDatabase.CreateAsset(mesh, path);
                Debug.Assert(AssetDatabase.AssetPathToGUID(path) == guid, "Mesh GUID changed: " + path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // Outward from the axis at the face's height (plus a vertical lean for pole fans).
        private static Vector3 Radial(Builder b, float forward, int i0, int i1, int i2, float lean)
        {
            Vector3 c = (b.V(i0) + b.V(i1) + b.V(i2)) / 3f;
            return new Vector3(c.x, lean * 0.001f, c.z - forward);
        }

        private sealed class Builder
        {
            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<int> tris = new List<int>();

            public int Count => verts.Count;
            public Vector3 V(int i) => verts[i];

            public int Add(Vector3 v)
            {
                verts.Add(v);
                return verts.Count - 1;
            }

            // Wound so the face's normal points along `outward`; degenerate triangles are skipped.
            public void Tri(int a, int b, int c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (n.sqrMagnitude < 1e-12f)
                    return;
                if (Vector3.Dot(n, outward) < 0f)
                {
                    int t = b;
                    b = c;
                    c = t;
                }
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }

            public void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            // A quad on its own vertices, so its hard edge doesn't bend the neighbours' smooth normals.
            public void OwnQuad(int a, int b, int c, int d, Vector3 outward)
            {
                Quad(Add(verts[a]), Add(verts[b]), Add(verts[c]), Add(verts[d]), outward);
            }

            public Mesh ToMesh(string name, bool flat)
            {
                var m = new Mesh { name = name };
                if (flat)
                {
                    var fv = new List<Vector3>(tris.Count);
                    var ft = new List<int>(tris.Count);
                    foreach (int i in tris)
                    {
                        ft.Add(fv.Count);
                        fv.Add(verts[i]);
                    }
                    m.SetVertices(fv);
                    m.SetTriangles(ft, 0);
                }
                else
                {
                    m.SetVertices(verts);
                    m.SetTriangles(tris, 0);
                }
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
