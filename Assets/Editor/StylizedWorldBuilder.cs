using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PoeClone.Visuals;
using PoeClone.Inventory;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Builds a consistent low-poly look from primitives: one palette, flat-shaded
    /// meshes, prop prefabs and characters. Greenwood's placed scenery is its own prefab
    /// (GreenwoodScenery), edited by hand. Re-runnable: PoeClone > Build Stylized World.
    /// </summary>
    public static class StylizedWorldBuilder
    {
        private const string MatDir = "Assets/Materials/Stylized";
        private const string MeshDir = "Assets/Meshes/Stylized";
        private const string PrefabDir = "Assets/Prefabs/Environment";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        private static readonly string[] PropPrefabs = { "Pine", "Oak", "Bush", "BushSmall", "Rock", "RockSmall", "House", "Pillar" };

        private static Dictionary<string, Material> mats;
        private static Dictionary<string, Mesh> meshes;

        [MenuItem("PoeClone/Build Stylized World")]
        public static void BuildAll()
        {
            mats = new Dictionary<string, Material>();
            meshes = new Dictionary<string, Mesh>();

            EnsureFolder(MatDir);
            EnsureFolder(MeshDir);
            EnsureFolder(PrefabDir);

            BuildMaterials();
            BuildMeshes();

            BuildPine();
            BuildOak();
            BuildBush(true);
            BuildBush(false);
            BuildRock(true);
            BuildRock(false);
            NaturalRockBuilder.BuildAll();
            BuildHouse();
            BuildPillar();

            BuildCharacters();
            // Force a reimport so Unity resolves the freshly-saved mesh references
            // (otherwise it can cache null references in the imported prefab).
            foreach (string p in PropPrefabs)
                AssetDatabase.ImportAsset(PrefabDir + "/" + p + ".prefab", ImportAssetOptions.ForceUpdate);
            EquipmentBuilder.BuildAll();
            ApplyLighting();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            int missing = 0;
            foreach (string p in PropPrefabs)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + p + ".prefab");
                foreach (MeshFilter mf in pf.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null)
                    {
                        missing++;
                        Debug.LogError("Missing mesh reference on " + p + "/" + mf.name);
                    }
                }
            }
            Debug.Log("StylizedWorldBuilder: done. Missing mesh refs: " + missing);
        }

        // ------------------------------------------------------------ utilities

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Color H(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        private static void Kill(Object o)
        {
            if (o != null)
                Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------ materials

        private static Material Mat(string name, Color color, float smoothness)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        private static void BuildMaterials()
        {
            // One muted, slightly desaturated palette shared by everything.
            Mat("Bark", H("#4a3322"), 0.05f);
            Mat("PineA", H("#1f4a34"), 0.05f);
            Mat("PineB", H("#2a5c3f"), 0.05f);
            Mat("OakA", H("#5f7d2a"), 0.05f);
            Mat("OakB", H("#7a9436"), 0.05f);
            Mat("Rock", H("#6b6d73"), 0.1f);
            Mat("RockDark", H("#4d4f56"), 0.1f);
            Mat("Wall", H("#b09a78"), 0.05f);
            Mat("Roof", H("#7a3a2c"), 0.05f);
            Mat("Wood", H("#5a3d26"), 0.05f);
            Mat("Stone", H("#9b9890"), 0.1f);
            // Runtime grounds get a generated texture and a white tint; this colour only shows in the editor.
            Mat("GroundStylized", H("#3f5c2d"), 0f);

            Mat("Skin", H("#d9a884"), 0.15f);
            Mat("Cloth", H("#3a5f9a"), 0.05f);
            Mat("Pants", H("#4a3b30"), 0.05f);
            Mat("Hood", H("#26365a"), 0.05f);
            Mat("Cloak", H("#2c4272"), 0.05f);
            Mat("EyeDark", H("#1c1c1c"), 0.2f);

            // Fantasy adventurer gear + monster bits
            Mat("Tunic", H("#8a6d47"), 0.05f);
            Mat("Leather", H("#3b2a1d"), 0.05f);
            Mat("Gold", H("#c9a227"), 0.35f);
            Mat("Steel", H("#8b929c"), 0.35f);
            Mat("Bone", H("#d8d0b8"), 0.1f);
            Mat("Hair", H("#3a2a1c"), 0.05f);

            Mat("EnemySkin", H("#8a9a78"), 0.1f);
            Mat("EnemyCloth", H("#6a2020"), 0.05f);
            Mat("EnemyPants", H("#3a2a2a"), 0.05f);
            Mat("Eye", H("#ffd23a"), 0.2f);
        }

        // ------------------------------------------------------------ meshes

        private class TriBuilder
        {
            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<int> tris = new List<int>();
            private readonly Vector3 center;

            public TriBuilder(Vector3 center)
            {
                this.center = center;
            }

            // Adds a triangle with unshared vertices (flat shading), wound so it faces away from center.
            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(n, centroid - center) < 0f)
                {
                    Vector3 tmp = b;
                    b = c;
                    c = tmp;
                }
                int i = verts.Count;
                verts.Add(a);
                verts.Add(b);
                verts.Add(c);
                tris.Add(i);
                tris.Add(i + 1);
                tris.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh ToMesh(string name)
            {
                Mesh m = new Mesh();
                m.name = name;
                m.SetVertices(verts);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

private static void SaveMesh(string name, Mesh mesh)
        {
            string path = MeshDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Update in place so the asset keeps its GUID and existing references stay valid.
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            meshes[name] = mesh;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private static Vector3 Jit(Vector3 p, float jitter, int seed)
        {
            if (jitter <= 0f)
                return p;
            float h = Hash(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), seed + Mathf.RoundToInt(p.z * 1000f));
            return p * (1f + (h * 2f - 1f) * jitter);
        }

        // Unit cone: radius 1, height 1, base at y = 0.
        private static Mesh MakeCone(int sides)
        {
            TriBuilder b = new TriBuilder(new Vector3(0f, 0.3f, 0f));
            Vector3 apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides;
                float a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                b.Tri(p0, p1, apex);
                b.Tri(p0, p1, Vector3.zero);
            }
            return b.ToMesh("Cone");
        }

        // Unit cylinder: radius 0.5, height 1, base at y = 0.
        private static Mesh MakeCylinder(int sides)
        {
            TriBuilder b = new TriBuilder(new Vector3(0f, 0.5f, 0f));
            Vector3 bottomC = Vector3.zero;
            Vector3 topC = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides;
                float a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                Vector3 t0 = b0 + Vector3.up;
                Vector3 t1 = b1 + Vector3.up;
                b.Quad(b0, b1, t1, t0);
                b.Tri(b0, b1, bottomC);
                b.Tri(t0, t1, topC);
            }
            return b.ToMesh("Cylinder");
        }

        // Unit icosphere (radius 0.5), optionally subdivided and jittered for a chunky rock/foliage look.
        private static Mesh MakeIco(string name, int subdiv, float jitter, int seed)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            List<Vector3[]> tris = new List<Vector3[]>();
            for (int i = 0; i < f.Length; i += 3)
                tris.Add(new[] { v[f[i]].normalized, v[f[i + 1]].normalized, v[f[i + 2]].normalized });

            for (int s = 0; s < subdiv; s++)
            {
                List<Vector3[]> next = new List<Vector3[]>();
                foreach (Vector3[] tr in tris)
                {
                    Vector3 a = tr[0], b = tr[1], c = tr[2];
                    Vector3 ab = ((a + b) * 0.5f).normalized;
                    Vector3 bc = ((b + c) * 0.5f).normalized;
                    Vector3 ca = ((c + a) * 0.5f).normalized;
                    next.Add(new[] { a, ab, ca });
                    next.Add(new[] { b, bc, ab });
                    next.Add(new[] { c, ca, bc });
                    next.Add(new[] { ab, bc, ca });
                }
                tris = next;
            }

            TriBuilder builder = new TriBuilder(Vector3.zero);
            foreach (Vector3[] tr in tris)
                builder.Tri(Jit(tr[0], jitter, seed) * 0.5f, Jit(tr[1], jitter, seed) * 0.5f, Jit(tr[2], jitter, seed) * 0.5f);
            return builder.ToMesh(name);
        }

        private static void BuildMeshes()
        {
            SaveMesh("Cone", MakeCone(7));
            SaveMesh("Cylinder", MakeCylinder(6));
            SaveMesh("IcoHead", MakeIco("IcoHead", 1, 0f, 0));
            SaveMesh("FoliageA", MakeIco("FoliageA", 0, 0.15f, 11));
            SaveMesh("FoliageB", MakeIco("FoliageB", 0, 0.15f, 23));
            SaveMesh("RockA", MakeIco("RockA", 0, 0.22f, 5));
            SaveMesh("RockB", MakeIco("RockB", 0, 0.22f, 17));

            // Flush to disk and reload so prefabs reference the persisted assets.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string[] names = { "Cone", "Cylinder", "IcoHead", "FoliageA", "FoliageB", "RockA", "RockB" };
            foreach (string n in names)
                meshes[n] = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/" + n + ".asset");
        }

        // ------------------------------------------------------------ prefab helpers

        private static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject AddCube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Kill(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

private static GameObject BuildPine()
        {
            GameObject root = new GameObject("Pine");
            Transform t = root.transform;
            AddMesh(t, "Trunk", meshes["Cylinder"], mats["Bark"], Vector3.zero, new Vector3(0.45f, 1.4f, 0.45f));
            ConvexCollider(AddMesh(t, "Cone1", meshes["Cone"], mats["PineA"], new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 2.0f, 1.6f)));
            ConvexCollider(AddMesh(t, "Cone2", meshes["Cone"], mats["PineB"], new Vector3(0f, 2.0f, 0f), new Vector3(1.25f, 1.8f, 1.25f)));
            ConvexCollider(AddMesh(t, "Cone3", meshes["Cone"], mats["PineA"], new Vector3(0f, 3.1f, 0f), new Vector3(0.85f, 1.5f, 0.85f)));
            CapsuleCollider c = root.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 1.2f, 0f);
            c.radius = 0.35f;
            c.height = 2.4f;
            return SavePrefab(root, "Pine");
        }

        // Gives a foliage piece a collider that matches its visible mesh.
        private static void ConvexCollider(GameObject go)
        {
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
        }

private static GameObject BuildOak()
        {
            GameObject root = new GameObject("Oak");
            Transform t = root.transform;
            AddMesh(t, "Trunk", meshes["Cylinder"], mats["Bark"], Vector3.zero, new Vector3(0.5f, 1.9f, 0.5f));
            ConvexCollider(AddMesh(t, "CrownMain", meshes["FoliageA"], mats["OakA"], new Vector3(0f, 2.4f, 0f), new Vector3(2.5f, 2.0f, 2.5f)));
            ConvexCollider(AddMesh(t, "CrownSideA", meshes["FoliageB"], mats["OakB"], new Vector3(0.8f, 2.0f, 0.4f), new Vector3(1.6f, 1.3f, 1.6f)));
            ConvexCollider(AddMesh(t, "CrownSideB", meshes["FoliageB"], mats["OakB"], new Vector3(-0.7f, 2.8f, -0.5f), new Vector3(1.5f, 1.2f, 1.5f)));
            CapsuleCollider c = root.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 1.1f, 0f);
            c.radius = 0.4f;
            c.height = 2.2f;
            return SavePrefab(root, "Oak");
        }

// Solid props get walls that go straight up past the player's height. A rounded, low collider
        // can be rolled up onto by a character controller; a tall vertical wall cannot.
        private const float WallHeight = 2.4f;

        // Props smaller than this are decoration you can walk through (no collider at all).
        private const float SmallBushScale = 0.75f;
        private const float SmallRockScale = 0.6f;

        private static GameObject BuildBush(bool solid)
        {
            string name = solid ? "Bush" : "BushSmall";
            GameObject root = new GameObject(name);
            Transform t = root.transform;
            AddMesh(t, "BushMain", meshes["FoliageB"], mats["OakA"], new Vector3(0f, 0.35f, 0f), new Vector3(1.3f, 0.85f, 1.3f));
            AddMesh(t, "BushSide", meshes["FoliageA"], mats["PineB"], new Vector3(0.55f, 0.25f, 0.3f), new Vector3(0.8f, 0.6f, 0.8f));

            if (solid)
            {
                AddWallCapsule(root, new Vector3(0f, WallHeight * 0.5f, 0f), 0.65f);
                AddWallCapsule(root, new Vector3(0.55f, WallHeight * 0.5f, 0.3f), 0.4f);
            }

            return SavePrefab(root, name);
        }

        private static void AddWallCapsule(GameObject root, Vector3 center, float radius)
        {
            CapsuleCollider c = root.AddComponent<CapsuleCollider>();
            c.center = center;
            c.radius = radius;
            c.height = WallHeight;
        }

private static GameObject BuildRock(bool solid)
        {
            string name = solid ? "Rock" : "RockSmall";
            GameObject root = new GameObject(name);
            Transform t = root.transform;
            AddMesh(t, "RockMain", meshes["RockA"], mats["Rock"], new Vector3(0f, 0.4f, 0f), new Vector3(1.7f, 1.1f, 1.4f));
            AddMesh(t, "RockSmall", meshes["RockB"], mats["RockDark"], new Vector3(1.0f, 0.2f, 0.4f), new Vector3(0.9f, 0.65f, 0.85f));

            if (solid)
            {
                BoxCollider c = root.AddComponent<BoxCollider>();
                c.center = new Vector3(0.3f, WallHeight * 0.5f, 0.1f);
                c.size = new Vector3(2.2f, WallHeight, 1.6f);
            }

            return SavePrefab(root, name);
        }

        private static GameObject BuildHouse()
        {
            GameObject root = new GameObject("House");
            Transform t = root.transform;
            AddCube(t, "Walls", new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 3.4f), mats["Wall"]);
            // The roof and wall finish are dressed at runtime (WorldBuilder.DressHouse), so each
            // house can differ; the chimney rises from the walls up through that roof.
            AddCube(t, "Door", new Vector3(0f, 0.75f, -1.7f), new Vector3(0.9f, 1.5f, 0.12f), mats["Wood"]);
            AddCube(t, "WindowL", new Vector3(-1.2f, 1.5f, -1.7f), new Vector3(0.6f, 0.6f, 0.12f), mats["Wood"]);
            AddCube(t, "WindowR", new Vector3(1.2f, 1.5f, -1.7f), new Vector3(0.6f, 0.6f, 0.12f), mats["Wood"]);
            AddCube(t, "Chimney", new Vector3(1.3f, 3.8f, 0.6f), new Vector3(0.55f, 2.8f, 0.55f), mats["Stone"]);
            BoxCollider c = root.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, 1.5f, 0f);
            c.size = new Vector3(4f, 3f, 3.4f);
            return SavePrefab(root, "House");
        }

        private static GameObject BuildPillar()
        {
            GameObject root = new GameObject("Pillar");
            Transform t = root.transform;
            AddCube(t, "Base", new Vector3(0f, 0.175f, 0f), new Vector3(1.5f, 0.35f, 1.5f), mats["Stone"]);
            AddMesh(t, "Column", meshes["Cylinder"], mats["Stone"], new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 3.2f, 1.0f));
            AddCube(t, "Cap", new Vector3(0f, 3.7f, 0f), new Vector3(1.3f, 0.3f, 1.3f), mats["Stone"]);
            BoxCollider c = root.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, 1.925f, 0f);
            c.size = new Vector3(1.3f, 3.85f, 1.3f);
            return SavePrefab(root, "Pillar");
        }

        // ------------------------------------------------------------ characters

private static void BuildCharacter(Transform parent, bool enemy)
        {
            Transform old = parent.Find("Model");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            Material skin = enemy ? mats["EnemySkin"] : mats["Skin"];
            Material tunic = enemy ? mats["EnemyCloth"] : mats["Tunic"];
            Material pants = enemy ? mats["EnemyPants"] : mats["Pants"];
            Material leather = mats["Leather"];
            Material eye = enemy ? mats["Eye"] : mats["EyeDark"];

            // CharacterController is 2 tall and centered, so the feet are at local y = -1.
            GameObject model = new GameObject("Model");
            model.transform.SetParent(parent, false);
            model.transform.localPosition = new Vector3(0f, -1f, 0f);
            Transform m = model.transform;

            // Legs: hip pivot -> thigh -> knee pivot -> shin.
            // The player starts with trouser wraps and bare feet: boots are equipment, so wearing
            // them visibly changes the character. Monsters keep their own boots.
            Transform legL = Pivot(m, "LegL", new Vector3(-0.19f, 0.85f, 0f));
            Transform legR = Pivot(m, "LegR", new Vector3(0.19f, 0.85f, 0f));
            Transform[] knees = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                Transform leg = i == 0 ? legL : legR;
                AddCube(leg, "Thigh", new Vector3(0f, -0.21f, 0f), new Vector3(0.27f, 0.42f, 0.3f), pants);
                Transform knee = Pivot(leg, "Knee", new Vector3(0f, -0.42f, 0f));

                if (enemy)
                {
                    AddCube(knee, "Boot", new Vector3(0f, -0.2f, 0.02f), new Vector3(0.3f, 0.46f, 0.36f), leather);
                    AddCube(knee, "BootCuff", new Vector3(0f, 0.02f, 0f), new Vector3(0.35f, 0.08f, 0.35f), leather);
                }
                else
                {
                    AddCube(knee, "Shin", new Vector3(0f, -0.2f, 0f), new Vector3(0.25f, 0.4f, 0.29f), pants);
                    AddCube(knee, "Foot", new Vector3(0f, -0.38f, 0.05f), new Vector3(0.24f, 0.1f, 0.34f), skin);
                }

                Pivot(knee, i == 0 ? "Socket_FootL" : "Socket_FootR", new Vector3(0f, -0.2f, 0.02f));
                knees[i] = knee;
            }

            // Everything above the hips hangs off one pivot so the body can lean when running.
            // "UpperOffset" undoes the pivot's height so the children keep model-space coordinates.
            Transform upper = Pivot(m, "UpperBody", new Vector3(0f, 0.9f, 0f));
            Transform u = Pivot(upper, "UpperOffset", new Vector3(0f, -0.9f, 0f));

            // Attachment points for worn equipment.
            Pivot(u, "Socket_Head", new Vector3(0f, 1.84f, 0.06f));
            Pivot(u, "Socket_Chest", new Vector3(0f, 1.225f, 0f));
            Pivot(u, "Socket_Waist", new Vector3(0f, 0.98f, 0f));
            Pivot(u, "Socket_Neck", new Vector3(0f, 1.55f, 0f));

            // Body: tunic with a flared skirt. Only monsters wear a belt; the player's belt is equipment.
            AddCube(u, "Torso", new Vector3(0f, 1.225f, 0f), new Vector3(0.78f, 0.75f, 0.44f), tunic);
            AddMesh(u, "TunicSkirt", meshes["Cylinder"], tunic, new Vector3(0f, 0.55f, 0f), new Vector3(1.0f, 0.42f, 0.66f));
            if (enemy)
                AddCube(u, "Belt", new Vector3(0f, 0.98f, 0f), new Vector3(0.82f, 0.1f, 0.48f), leather);

            // Arms: shoulder pivot -> sleeve -> elbow pivot -> forearm + hand.
            // Character faces +z, so its right hand (main hand) is on +x.
            float armRest = enemy ? -65f : 0f;
            Transform[] arms = new Transform[2];
            Transform[] elbows = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform arm = Pivot(u, i == 0 ? "ArmL" : "ArmR", new Vector3(0.5f * side, 1.55f, 0f));
                AddCube(arm, "Upper", new Vector3(0f, -0.2f, 0f), new Vector3(0.23f, 0.42f, 0.27f), enemy ? skin : tunic);
                Transform elbow = Pivot(arm, "Elbow", new Vector3(0f, -0.42f, 0f));
                AddCube(elbow, "Forearm", new Vector3(0f, -0.1f, 0f), enemy ? new Vector3(0.26f, 0.26f, 0.3f) : new Vector3(0.22f, 0.26f, 0.26f), enemy ? tunic : skin);
                AddCube(elbow, "Hand", new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 0.16f, 0.24f), skin);

                // Attachment points: weapons in the hands, gloves on the hands, rings on the fingers.
                Pivot(elbow, i == 0 ? "Socket_OffHand" : "Socket_MainHand", new Vector3(0f, -0.4f, 0.05f));
                Pivot(elbow, i == 0 ? "Socket_HandL" : "Socket_HandR", new Vector3(0f, -0.3f, 0f));
                Pivot(elbow, i == 0 ? "Socket_RingL" : "Socket_RingR", new Vector3(0f, -0.3f, 0f));

                arm.localRotation = Quaternion.Euler(armRest, 0f, 0f);
                arms[i] = arm;
                elbows[i] = elbow;
            }

            if (enemy)
            {
                AddMesh(u, "Head", meshes["IcoHead"], skin, new Vector3(0f, 1.87f, 0f), new Vector3(0.52f, 0.5f, 0.52f));
                AddCube(u, "EyeL", new Vector3(-0.12f, 1.9f, 0.23f), new Vector3(0.1f, 0.08f, 0.06f), eye);
                AddCube(u, "EyeR", new Vector3(0.12f, 1.9f, 0.23f), new Vector3(0.1f, 0.08f, 0.06f), eye);
                AddMeshR(u, "HornL", meshes["Cone"], mats["Bone"], new Vector3(-0.17f, 2.06f, 0.02f), new Vector3(0.09f, 0.24f, 0.09f), new Vector3(0f, 0f, 20f));
                AddMeshR(u, "HornR", meshes["Cone"], mats["Bone"], new Vector3(0.17f, 2.06f, 0.02f), new Vector3(0.09f, 0.24f, 0.09f), new Vector3(0f, 0f, -20f));
            }
            else
            {
                AddMesh(u, "Head", meshes["IcoHead"], skin, new Vector3(0f, 1.84f, 0.06f), new Vector3(0.44f, 0.44f, 0.44f));
                // Flush with the head surface rather than poking out - a small dark dot reads
                // fine at this scale and doesn't look like a stuck-on bug eye.
                AddCube(u, "EyeL", new Vector3(-0.1f, 1.87f, 0.235f), new Vector3(0.055f, 0.045f, 0.03f), eye);
                AddCube(u, "EyeR", new Vector3(0.1f, 1.87f, 0.235f), new Vector3(0.055f, 0.045f, 0.03f), eye);

                // Hair, visible once the hood comes off (e.g. when a helmet is worn).
                AddMesh(u, "Hair", meshes["IcoHead"], mats["Hair"], new Vector3(0f, 1.9f, -0.03f), new Vector3(0.47f, 0.42f, 0.47f));

                // The starting headwear hides while a helmet is worn. A simple flattened cap
                // (not a tall pointed hood) that sits close to the head.
                GameObject hood = AddMesh(u, "Hood", meshes["IcoHead"], mats["Hood"], new Vector3(0f, 1.94f, -0.04f), new Vector3(0.62f, 0.42f, 0.62f));
                GameObject brim = AddMesh(u, "HoodBrim", meshes["Cylinder"], mats["Hood"], new Vector3(0f, 1.98f, -0.02f), new Vector3(0.66f, 0.05f, 0.66f));
                TagBaseGear(hood, EquipSlot.Helmet);
                TagBaseGear(brim, EquipSlot.Helmet);

                // Cloak: shoulder mantle plus a long flared back panel (shown only with no body armour, or armour that has a cape).
                TagBaseGear(AddMesh(u, "Mantle", meshes["Cylinder"], mats["Cloak"], new Vector3(0f, 1.55f, -0.02f), new Vector3(1.15f, 0.2f, 0.68f)), EquipSlot.BodyArmour, true);
                TagBaseGear(AddCubeR(u, "CloakBack", new Vector3(0f, 1.05f, -0.3f), new Vector3(0.9f, 1.3f, 0.08f), mats["Cloak"], new Vector3(6f, 0f, 0f)), EquipSlot.BodyArmour, true);

                // One steel pauldron, replaced by the shoulder pads of any body armour.
                GameObject pauldron = AddMesh(u, "Pauldron", meshes["Cylinder"], mats["Steel"], new Vector3(-0.52f, 1.72f, 0f), new Vector3(0.42f, 0.16f, 0.42f));
                // Small and right under the hood's shadow edge: with toon shading's hard light/shadow
                // step, that edge crawling across it as the body bobs read as blinking.
                pauldron.GetComponent<Renderer>().receiveShadows = false;
                TagBaseGear(pauldron, EquipSlot.BodyArmour);
            }

            // The parts above are placeholders; the body's final shapes and proportions live there.
            HumanoidBodyBuilder.Shape(m, enemy);

            CharacterWalkAnimator anim = model.AddComponent<CharacterWalkAnimator>();
            anim.Configure(legL, legR, arms[0], arms[1], armRest);
            anim.ConfigureJoints(upper, knees[0], knees[1], elbows[0], elbows[1]);

            // Shows worn equipment on the player.
            if (!enemy)
                model.AddComponent<EquipmentVisuals>();
        }

private static void TagBaseGear(GameObject go, EquipSlot hiddenBy, bool visibleIfItemHasCape = false)
        {
            BaseGear gear = go.AddComponent<BaseGear>();
            gear.HiddenBy = hiddenBy;
            gear.VisibleIfItemHasCape = visibleIfItemHasCape;
        }

        private static GameObject AddMeshR(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            GameObject go = AddMesh(parent, name, mesh, mat, pos, scale);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        private static GameObject AddCubeR(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler)
        {
            GameObject go = AddCube(parent, name, pos, scale, mat);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        private static void BuildCharacters()
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Kill(player.GetComponent<MeshRenderer>());
                Kill(player.GetComponent<MeshFilter>());
                BuildCharacter(player.transform, false);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                Kill(root.GetComponent<MeshRenderer>());
                Kill(root.GetComponent<MeshFilter>());
                Transform marker = root.transform.Find("FacingMarker");
                if (marker != null)
                    Object.DestroyImmediate(marker.gameObject);
                BuildCharacter(root.transform, true);
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------ lighting

        private static void ApplyLighting()
        {
            GameObject lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                Light l = lightGo.GetComponent<Light>();
                l.color = new Color(1f, 0.94f, 0.82f);
                l.intensity = 1.15f;
                l.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.48f, 0.58f);
        }
    }
}
