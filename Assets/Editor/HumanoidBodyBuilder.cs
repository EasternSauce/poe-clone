using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Gives the humanoid rig (the scene's Player and the Enemy prefab, which NPCs also use) its
    /// body: smooth tapered limbs, a torso narrowing to the waist, a neck and a smaller head,
    /// instead of boxes and a big round head. Only meshes, positions and scales of the named parts
    /// change; the joints, sockets and animators stay as they are. Meshes are unit-sized like the
    /// cubes they replace, so code that scales a part by name still works.
    /// Re-runnable: PoeClone > Reshape Humanoids.
    /// </summary>
    public static class HumanoidBodyBuilder
    {
        private const string MeshDir = "Assets/Meshes/Stylized";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

        private static Mesh limb, torso, skirt, boot, head;

        [MenuItem("PoeClone/Reshape Humanoids")]
        public static void ReshapeAll()
        {
            GameObject player = GameObject.Find("Player");
            Transform playerModel = player != null ? player.transform.Find("Model") : null;
            if (playerModel != null)
            {
                Shape(playerModel, false);
                EditorSceneManager.MarkSceneDirty(player.scene);
                EditorSceneManager.SaveScene(player.scene);
            }
            else
            {
                Debug.LogWarning("HumanoidBodyBuilder: no Player/Model in the open scene; only the enemy prefab was reshaped.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                Shape(root.transform.Find("Model"), true);
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HumanoidBodyBuilder: reshaped " + (playerModel != null ? "the player and " : "") + "the enemy prefab.");
        }

        /// <summary>Reshapes one rig (a "Model" built by <see cref="StylizedWorldBuilder"/>).</summary>
        public static void Shape(Transform model, bool enemy)
        {
            BuildMeshes();

            foreach (string side in new[] { "L", "R" })
            {
                float s = side == "L" ? -1f : 1f;

                // Legs: narrower hips, a thigh tapering to the knee.
                Transform leg = model.Find("Leg" + side);
                leg.localPosition = new Vector3(0.13f * s, 0.85f, 0f);
                Part(leg, "Thigh", limb, new Vector3(0f, -0.21f, 0f), new Vector3(0.2f, 0.5f, 0.21f));
                Transform knee = leg.Find("Knee");
                Part(knee, "Shin", limb, new Vector3(0f, -0.2f, 0f), new Vector3(0.16f, 0.44f, 0.17f));
                Part(knee, "Foot", head, new Vector3(0f, -0.38f, 0.06f), new Vector3(0.14f, 0.1f, 0.3f));
                Part(knee, "Boot", boot, new Vector3(0f, -0.2f, 0.02f), new Vector3(0.19f, 0.46f, 0.3f));
                Part(knee, "BootCuff", limb, new Vector3(0f, 0.02f, -0.01f), new Vector3(0.22f, 0.08f, 0.22f));

                // Arms: shoulders set in to the narrower chest, slim upper arm and forearm, small hands.
                // The elbow and the hand/weapon sockets keep their places, so held items don't move.
                Transform arm = model.Find("UpperBody/UpperOffset/Arm" + side);
                arm.localPosition = new Vector3(0.38f * s, 1.5f, 0f);
                Part(arm, "Upper", limb, new Vector3(0f, -0.2f, 0f), new Vector3(0.15f, 0.48f, 0.16f));
                Transform elbow = arm.Find("Elbow");
                Part(elbow, "Forearm", limb, new Vector3(0f, -0.13f, 0f), new Vector3(0.13f, 0.32f, 0.14f));
                Part(elbow, "Hand", limb, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.15f, 0.12f));
            }

            Transform u = model.Find("UpperBody/UpperOffset");
            Part(u, "Torso", torso, new Vector3(0f, 1.22f, 0f), new Vector3(0.66f, 0.72f, 0.36f));
            Part(u, "TunicSkirt", skirt, new Vector3(0f, 0.76f, 0f), new Vector3(0.6f, 0.42f, 0.44f));
            Part(u, "Belt", limb, new Vector3(0f, 0.98f, 0f), new Vector3(0.56f, 0.09f, 0.4f));

            // A neck, and a head about a sixth of the height rather than a quarter.
            Transform headPart = u.Find("Head");
            Transform neck = u.Find("Neck");
            if (neck == null)
            {
                neck = Object.Instantiate(headPart.gameObject, u).transform;
                neck.name = "Neck";
                neck.SetSiblingIndex(headPart.GetSiblingIndex());
            }
            Part(u, "Neck", limb, new Vector3(0f, 1.62f, 0.01f), new Vector3(0.13f, 0.16f, 0.13f));

            if (enemy)
            {
                Part(u, "Head", head, new Vector3(0f, 1.8f, 0.02f), new Vector3(0.36f, 0.4f, 0.38f));
                Part(u, "EyeL", head, new Vector3(-0.075f, 1.83f, 0.19f), new Vector3(0.075f, 0.05f, 0.05f));
                Part(u, "EyeR", head, new Vector3(0.075f, 1.83f, 0.19f), new Vector3(0.075f, 0.05f, 0.05f));
                Part(u, "HornL", null, new Vector3(-0.11f, 1.95f, 0.02f), new Vector3(0.07f, 0.18f, 0.07f));
                Part(u, "HornR", null, new Vector3(0.11f, 1.95f, 0.02f), new Vector3(0.07f, 0.18f, 0.07f));
            }
            else
            {
                Part(u, "Head", head, new Vector3(0f, 1.8f, 0.04f), new Vector3(0.34f, 0.38f, 0.36f));
                Part(u, "EyeL", head, new Vector3(-0.07f, 1.82f, 0.212f), new Vector3(0.045f, 0.04f, 0.03f));
                Part(u, "EyeR", head, new Vector3(0.07f, 1.82f, 0.212f), new Vector3(0.045f, 0.04f, 0.03f));
                Part(u, "Hair", head, new Vector3(0f, 1.85f, 0.01f), new Vector3(0.37f, 0.33f, 0.39f));
                Part(u, "Hood", head, new Vector3(0f, 1.87f, 0f), new Vector3(0.47f, 0.33f, 0.48f));
                Part(u, "HoodBrim", null, new Vector3(0f, 1.9f, 0.01f), new Vector3(0.5f, 0.04f, 0.5f));
                Part(u, "Mantle", null, new Vector3(0f, 1.5f, -0.01f), new Vector3(0.84f, 0.18f, 0.48f));
                Part(u, "CloakBack", skirt, new Vector3(0f, 1.0f, -0.21f), new Vector3(0.66f, 1.12f, 0.07f));
                Part(u, "Pauldron", null, new Vector3(-0.39f, 1.62f, 0f), new Vector3(0.3f, 0.12f, 0.3f));
            }
        }

        // Sets a part's mesh (null keeps it), position and scale, if the rig has that part.
        private static void Part(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale)
        {
            Transform t = parent.Find(name);
            if (t == null)
                return;
            if (mesh != null)
                t.GetComponent<MeshFilter>().sharedMesh = mesh;
            t.localPosition = position;
            t.localScale = scale;
        }

        // ------------------------------------------------------------ meshes

        // One ring of a loft: height, half-width and half-depth (of the unit box) and a forward shift.
        private struct Ring
        {
            public float Y, X, Z, Forward;
            public Ring(float y, float x, float z, float forward = 0f) { Y = y; X = x; Z = z; Forward = forward; }
        }

        private static void BuildMeshes()
        {
            // A capsule tapering towards its lower end (thighs, arms, hands, neck, belt).
            limb = Save("BodyLimb", Loft("BodyLimb", 10, 2f,
                R(0.5f, 0f), R(0.47f, 0.31f), R(0.4f, 0.46f), R(0.25f, 0.5f), R(0f, 0.47f),
                R(-0.25f, 0.42f), R(-0.4f, 0.38f), R(-0.47f, 0.28f), R(-0.5f, 0f)));

            // Rounded shoulders, a chest, a narrower waist and the hips (boxier than an ellipse).
            torso = Save("BodyTorso", Loft("BodyTorso", 16, 2.3f,
                new Ring(0.5f, 0.2f, 0.24f), new Ring(0.46f, 0.36f, 0.4f), new Ring(0.36f, 0.48f, 0.46f),
                new Ring(0.22f, 0.5f, 0.49f, 0.02f), new Ring(0.02f, 0.44f, 0.48f, 0.02f), new Ring(-0.2f, 0.36f, 0.42f),
                new Ring(-0.4f, 0.39f, 0.41f), new Ring(-0.48f, 0.34f, 0.35f), new Ring(-0.5f, 0f, 0f)));

            // The tunic's skirt, flaring towards the hem.
            skirt = Save("BodySkirt", Loft("BodySkirt", 14, 2.2f,
                new Ring(0.5f, 0f, 0f), new Ring(0.5f, 0.38f, 0.38f), new Ring(0f, 0.44f, 0.44f),
                new Ring(-0.5f, 0.5f, 0.5f), new Ring(-0.5f, 0f, 0f)));

            // A boot: a shaft down to the ankle, then the foot reaching forwards.
            boot = Save("BodyBoot", Loft("BodyBoot", 12, 2.4f,
                new Ring(0.5f, 0f, 0f, -0.08f), new Ring(0.47f, 0.36f, 0.3f, -0.08f), new Ring(0.3f, 0.42f, 0.33f, -0.1f),
                new Ring(-0.1f, 0.38f, 0.3f, -0.12f), new Ring(-0.28f, 0.42f, 0.4f, -0.04f), new Ring(-0.4f, 0.46f, 0.5f, 0.04f),
                new Ring(-0.5f, 0.42f, 0.46f, 0.04f), new Ring(-0.5f, 0f, 0f, 0.04f)));

            head = Save("BodyHead", Sphere("BodyHead", 14, 10));
        }

        private static Ring R(float y, float radius) => new Ring(y, radius, radius);

        // Rings of a superellipse cross-section stacked bottom to top, with shared vertices so the
        // shading is smooth. A ring of zero size is a single pole vertex (a closed end).
        private static Mesh Loft(string name, int sides, float exponent, params Ring[] rings)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var starts = new int[rings.Length];
            for (int r = 0; r < rings.Length; r++)
            {
                Ring ring = rings[r];
                starts[r] = verts.Count;
                if (ring.X <= 0f)
                {
                    verts.Add(new Vector3(0f, ring.Y, ring.Forward));
                    continue;
                }
                for (int i = 0; i < sides; i++)
                {
                    float a = Mathf.PI * 2f * i / sides;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / exponent);
                    float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / exponent);
                    verts.Add(new Vector3(x * ring.X, ring.Y, z * ring.Z + ring.Forward));
                }
            }

            for (int r = 0; r + 1 < rings.Length; r++)
            {
                bool lowPole = rings[r].X <= 0f, highPole = rings[r + 1].X <= 0f;
                int a = starts[r], b = starts[r + 1];
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    // Rings run from top (+y) to bottom; wind so faces look outwards.
                    if (lowPole)
                        Add(tris, a, b + j, b + i);
                    else if (highPole)
                        Add(tris, a + i, a + j, b);
                    else
                    {
                        Add(tris, a + i, a + j, b + j);
                        Add(tris, a + i, b + j, b + i);
                    }
                }
            }
            return Finish(name, verts, tris);
        }

        // A UV sphere of radius 0.5 with smooth shading.
        private static Mesh Sphere(string name, int sides, int stacks)
        {
            var rings = new Ring[stacks + 1];
            for (int k = 0; k <= stacks; k++)
            {
                float phi = Mathf.PI * k / stacks;
                float radius = k == 0 || k == stacks ? 0f : Mathf.Sin(phi) * 0.5f;
                rings[k] = new Ring(Mathf.Cos(phi) * 0.5f, radius, radius);
            }
            return Loft(name, sides, 2f, rings);
        }

        private static void Add(List<int> tris, int a, int b, int c)
        {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        private static Mesh Finish(string name, List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // Updates an existing asset in place so its GUID (and every reference to it) is kept.
        private static Mesh Save(string name, Mesh mesh)
        {
            string path = MeshDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
