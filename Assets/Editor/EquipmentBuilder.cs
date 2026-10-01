using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Builds the 3D look of every piece of gear as a prefab in Resources/Equipment/{item id}.prefab,
    /// using the same low-poly meshes and palette as the rest of the game, and styled after each item's icon.
    /// Each direct child of a prefab is named after the character socket it attaches to
    /// (Socket_Head, Socket_HandL, ...). "Socket_Ring" lands on whichever hand the ring slot uses.
    /// Re-runnable: PoeClone > Build Equipment.
    /// </summary>
    public static class EquipmentBuilder
    {
        private const string OutDir = "Assets/Resources/Equipment";
        private const string MatDir = "Assets/Materials/Stylized";
        private const string MeshDir = "Assets/Meshes/Stylized";

        private static readonly string[] AllIds =
        {
            "iron_helmet", "bronze_helmet", "studded_vest", "leather_gloves", "leather_boots", "rope_belt",
            "jade_amulet", "iron_ring", "ruby_ring", "sapphire_ring", "rusty_sword", "wooden_shield"
        };

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        [MenuItem("PoeClone/Build Equipment")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(OutDir);
            LoadMeshes();
            MakeMaterials();

            IronHelmet();
            BronzeHelmet();
            StuddedVest();
            LeatherGloves();
            LeatherBoots();
            RopeBelt();
            JadeAmulet();
            Ring("iron_ring", "SteelDark", "RustSteel");
            Ring("ruby_ring", "Steel", "GemRed");
            Ring("sapphire_ring", "Steel", "GemBlue");
            RustySword();
            WoodenShield();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Force a reimport so Unity resolves the mesh references it just saved.
            int missing = 0;
            foreach (string id in AllIds)
            {
                string path = OutDir + "/" + id + ".prefab";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null)
                    {
                        missing++;
                        Debug.LogError("EquipmentBuilder: missing mesh on " + id + "/" + mf.name);
                    }
                }
            }

            Debug.Log("EquipmentBuilder: built " + AllIds.Length + " equipment prefabs, missing mesh refs: " + missing);
        }

        // ------------------------------------------------------------------ setup

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

        private static void LoadMeshes()
        {
            Meshes.Clear();
            foreach (string name in new[] { "Cone", "Cylinder", "IcoHead", "Prism" })
            {
                Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/" + name + ".asset");
                if (m == null)
                    Debug.LogError("EquipmentBuilder: missing mesh " + name + ". Run PoeClone > Build Stylized World first.");
                Meshes[name] = m;
            }
        }

        private static Color H(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        private static void Mat(string name, string hex, float smoothness, bool glow)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }

            Color c = H(hex);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);

            // No emission: it needs its own shader variant, which the editor compiles asynchronously
            // and shows as magenta until it finishes. Gems shine through smoothness instead.
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);

            EditorUtility.SetDirty(m);
            Mats[name] = m;
        }

        // Colours are chosen to match the painted icons.
        private static void MakeMaterials()
        {
            Mats.Clear();
            Mat("Tan", "#c2a274", 0.05f, false);
            Mat("TanDark", "#8a6a44", 0.05f, false);
            Mat("TanLight", "#d8c197", 0.05f, false);
            Mat("Steel", "#8b929c", 0.35f, false);
            Mat("SteelDark", "#5d626b", 0.2f, false);
            Mat("Bronze", "#b07b3c", 0.3f, false);
            Mat("BronzeLight", "#d9a45a", 0.3f, false);
            Mat("Gold", "#c9a227", 0.35f, false);
            Mat("Rope", "#b89a63", 0.05f, false);
            Mat("RopeDark", "#7d6640", 0.05f, false);
            Mat("Leather", "#3b2a1d", 0.05f, false);
            Mat("GemGreen", "#35d17f", 0.5f, true);
            Mat("GemRed", "#e03a4a", 0.5f, true);
            Mat("GemBlue", "#3f6ff0", 0.5f, true);
            Mat("RustSteel", "#8a7e73", 0.1f, false);
            Mat("Rust", "#9b4f2a", 0.05f, false);
            Mat("ShieldWood", "#8a5e34", 0.05f, false);
            Mat("ShieldWoodDark", "#5e3f22", 0.05f, false);
        }

        // ------------------------------------------------------------------ builders

        private static GameObject NewRoot(string id)
        {
            return new GameObject(id);
        }

        // A direct child of the prefab root, named for the character socket it attaches to.
        private static Transform Node(GameObject root, string socketName, Vector3 pos, Vector3 euler)
        {
            GameObject go = new GameObject(socketName);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        private static void Cube(Transform parent, string name, string mat, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3))
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mats[mat];
        }

        private static void Shape(Transform parent, string name, string mesh, string mat, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3))
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Meshes[mesh];
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mats[mat];
            // Shoulder pads sit right where the head/hood shadow edge falls; with toon shading's
            // hard light/shadow step, that edge crawling across them while walking reads as blinking.
            if (name.StartsWith("Shoulder"))
                renderer.receiveShadows = false;
        }

        // A thin bar between two points (used for the chain).
        private static void Bar(Transform parent, string name, string mat, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 dir = b - a;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.LookRotation(dir.normalized);
            go.transform.localScale = new Vector3(thickness, thickness, dir.magnitude);
            go.GetComponent<Renderer>().sharedMaterial = Mats[mat];
        }

        private static void Save(GameObject root, string id)
        {
            PrefabUtility.SaveAsPrefabAsset(root, OutDir + "/" + id + ".prefab");
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ the gear

        // Head socket is the centre of the head. The face sits at about z +0.2, so helmets sit back and up.
        private static void IronHelmet()
        {
            GameObject root = NewRoot("iron_helmet");
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            Shape(head, "Dome", "IcoHead", "Steel", new Vector3(0f, 0.12f, -0.08f), new Vector3(0.60f, 0.50f, 0.60f));
            Shape(head, "Band", "Cylinder", "SteelDark", new Vector3(0f, 0.08f, -0.06f), new Vector3(0.58f, 0.07f, 0.58f));
            Cube(head, "NoseGuard", "Steel", new Vector3(0f, 0f, 0.24f), new Vector3(0.05f, 0.2f, 0.04f));
            Cube(head, "CheekL", "Steel", new Vector3(-0.235f, -0.02f, 0.03f), new Vector3(0.05f, 0.2f, 0.18f));
            Cube(head, "CheekR", "Steel", new Vector3(0.235f, -0.02f, 0.03f), new Vector3(0.05f, 0.2f, 0.18f));
            // Flat fins swept back along the head (like the icon's pointed ear guards), not upright cones.
            Shape(head, "EarL", "Cone", "Steel", new Vector3(-0.27f, 0.03f, -0.03f), new Vector3(0.08f, 0.26f, 0.02f), new Vector3(-105f, 0f, 20f));
            Shape(head, "EarR", "Cone", "Steel", new Vector3(0.27f, 0.03f, -0.03f), new Vector3(0.08f, 0.26f, 0.02f), new Vector3(-105f, 0f, -20f));
            Cube(head, "Gem", "GemGreen", new Vector3(0f, 0.115f, 0.245f), new Vector3(0.07f, 0.07f, 0.04f), new Vector3(0f, 0f, 45f));

            Save(root, "iron_helmet");
        }

        private static void BronzeHelmet()
        {
            GameObject root = NewRoot("bronze_helmet");
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            Shape(head, "Dome", "IcoHead", "Bronze", new Vector3(0f, 0.12f, -0.08f), new Vector3(0.60f, 0.50f, 0.60f));
            Shape(head, "Trim", "Cylinder", "Gold", new Vector3(0f, 0.08f, -0.06f), new Vector3(0.58f, 0.06f, 0.58f));
            Cube(head, "Crest", "BronzeLight", new Vector3(0f, 0.37f, -0.08f), new Vector3(0.04f, 0.12f, 0.42f));
            // Swept-back side fins, flat against the head (no upright horn shapes).
            Shape(head, "WingL", "Cone", "BronzeLight", new Vector3(-0.28f, 0.05f, -0.04f), new Vector3(0.09f, 0.30f, 0.02f), new Vector3(-100f, 0f, 25f));
            Shape(head, "WingR", "Cone", "BronzeLight", new Vector3(0.28f, 0.05f, -0.04f), new Vector3(0.09f, 0.30f, 0.02f), new Vector3(-100f, 0f, -25f));
            Cube(head, "NoseGuard", "Bronze", new Vector3(0f, 0f, 0.24f), new Vector3(0.05f, 0.2f, 0.04f));
            Cube(head, "CheekL", "Bronze", new Vector3(-0.235f, -0.02f, 0.03f), new Vector3(0.05f, 0.2f, 0.18f));
            Cube(head, "CheekR", "Bronze", new Vector3(0.235f, -0.02f, 0.03f), new Vector3(0.05f, 0.2f, 0.18f));
            Cube(head, "Gem", "GemGreen", new Vector3(0f, 0.115f, 0.245f), new Vector3(0.07f, 0.07f, 0.04f), new Vector3(0f, 0f, 45f));

            Save(root, "bronze_helmet");
        }

        // Chest socket is the centre of the torso (0.78 x 0.75 x 0.44).
        private static void StuddedVest()
        {
            GameObject root = NewRoot("studded_vest");
            Transform chest = Node(root, "Socket_Chest", Vector3.zero, Vector3.zero);

            Cube(chest, "Body", "Tan", new Vector3(0f, 0.03f, 0f), new Vector3(0.83f, 0.68f, 0.49f));
            Shape(chest, "Collar", "Cylinder", "TanDark", new Vector3(0f, 0.34f, 0f), new Vector3(0.40f, 0.08f, 0.32f));
            Shape(chest, "ShoulderL", "Cylinder", "Tan", new Vector3(-0.47f, 0.30f, 0f), new Vector3(0.36f, 0.13f, 0.36f));
            Shape(chest, "ShoulderR", "Cylinder", "Tan", new Vector3(0.47f, 0.30f, 0f), new Vector3(0.36f, 0.13f, 0.36f));
            Shape(chest, "ShoulderTrimL", "Cylinder", "Gold", new Vector3(-0.47f, 0.29f, 0f), new Vector3(0.38f, 0.03f, 0.38f));
            Shape(chest, "ShoulderTrimR", "Cylinder", "Gold", new Vector3(0.47f, 0.29f, 0f), new Vector3(0.38f, 0.03f, 0.38f));
            Cube(chest, "StrapL", "TanDark", new Vector3(-0.16f, 0.02f, 0f), new Vector3(0.07f, 0.6f, 0.5f));
            Cube(chest, "StrapR", "TanDark", new Vector3(0.16f, 0.02f, 0f), new Vector3(0.07f, 0.6f, 0.5f));

            foreach (float y in new[] { 0.16f, 0f, -0.16f })
            {
                Cube(chest, "BuckleL", "Gold", new Vector3(-0.16f, y, 0.255f), new Vector3(0.09f, 0.06f, 0.03f));
                Cube(chest, "BuckleR", "Gold", new Vector3(0.16f, y, 0.255f), new Vector3(0.09f, 0.06f, 0.03f));
            }

            // Studs on the chest plates.
            foreach (float y in new[] { 0.2f, -0.06f })
            {
                Cube(chest, "StudL", "Gold", new Vector3(-0.31f, y, 0.255f), new Vector3(0.04f, 0.04f, 0.03f));
                Cube(chest, "StudR", "Gold", new Vector3(0.31f, y, 0.255f), new Vector3(0.04f, 0.04f, 0.03f));
            }

            Cube(chest, "Gem", "GemGreen", new Vector3(0f, 0.27f, 0.255f), new Vector3(0.07f, 0.07f, 0.03f), new Vector3(0f, 0f, 45f));
            Cube(chest, "Hem", "TanDark", new Vector3(0f, -0.3f, 0f), new Vector3(0.85f, 0.06f, 0.5f));

            Save(root, "studded_vest");
        }

        // Hand socket is at the hand; the forearm is above it (+y).
        private static void LeatherGloves()
        {
            GameObject root = NewRoot("leather_gloves");

            foreach (string socket in new[] { "Socket_HandL", "Socket_HandR" })
            {
                Transform hand = Node(root, socket, Vector3.zero, Vector3.zero);
                Cube(hand, "Glove", "Tan", Vector3.zero, new Vector3(0.23f, 0.19f, 0.27f));
                Cube(hand, "Knuckles", "TanDark", new Vector3(0f, -0.03f, 0.13f), new Vector3(0.23f, 0.07f, 0.06f));
                Cube(hand, "Cuff", "TanLight", new Vector3(0f, 0.15f, 0f), new Vector3(0.29f, 0.16f, 0.32f));
                Cube(hand, "CuffTrim", "Gold", new Vector3(0f, 0.235f, 0f), new Vector3(0.30f, 0.03f, 0.33f));
            }

            Save(root, "leather_gloves");
        }

        // Foot socket is the centre of the boot (the shin, 0.46 tall).
        private static void LeatherBoots()
        {
            GameObject root = NewRoot("leather_boots");

            foreach (string socket in new[] { "Socket_FootL", "Socket_FootR" })
            {
                Transform foot = Node(root, socket, Vector3.zero, Vector3.zero);
                Cube(foot, "Boot", "Tan", Vector3.zero, new Vector3(0.31f, 0.47f, 0.37f));
                Cube(foot, "Cuff", "TanLight", new Vector3(0f, 0.235f, 0f), new Vector3(0.37f, 0.11f, 0.42f));
                Cube(foot, "StrapHigh", "TanDark", new Vector3(0f, 0.06f, 0f), new Vector3(0.33f, 0.04f, 0.39f));
                Cube(foot, "StrapLow", "TanDark", new Vector3(0f, -0.07f, 0f), new Vector3(0.33f, 0.04f, 0.39f));
                Cube(foot, "Buckle", "Gold", new Vector3(0.165f, 0.06f, 0.02f), new Vector3(0.03f, 0.06f, 0.06f));
                Cube(foot, "ToeCap", "Tan", new Vector3(0f, -0.16f, 0.2f), new Vector3(0.26f, 0.14f, 0.12f));
                Cube(foot, "Sole", "TanDark", new Vector3(0f, -0.245f, 0.03f), new Vector3(0.32f, 0.05f, 0.42f));
            }

            Save(root, "leather_boots");
        }

        private static void RopeBelt()
        {
            GameObject root = NewRoot("rope_belt");
            Transform waist = Node(root, "Socket_Waist", Vector3.zero, Vector3.zero);

            Cube(waist, "Band", "Rope", Vector3.zero, new Vector3(0.86f, 0.11f, 0.52f));

            // Twisted-rope stripes.
            for (int i = -2; i <= 2; i++)
                Cube(waist, "Twist", "RopeDark", new Vector3(i * 0.16f, 0f, 0f), new Vector3(0.04f, 0.115f, 0.53f), new Vector3(0f, 25f, 0f));

            Shape(waist, "Knot", "IcoHead", "Rope", new Vector3(0.10f, 0f, 0.27f), new Vector3(0.17f, 0.17f, 0.13f));
            Cube(waist, "TailA", "Rope", new Vector3(0.07f, -0.2f, 0.28f), new Vector3(0.05f, 0.32f, 0.05f), new Vector3(8f, 0f, 6f));
            Cube(waist, "TailB", "Rope", new Vector3(0.15f, -0.18f, 0.28f), new Vector3(0.05f, 0.28f, 0.05f), new Vector3(8f, 0f, -10f));
            Cube(waist, "Gem", "GemGreen", new Vector3(0.10f, 0f, 0.34f), new Vector3(0.05f, 0.05f, 0.03f), new Vector3(0f, 0f, 45f));

            Save(root, "rope_belt");
        }

        // Neck socket is at the top of the chest.
        private static void JadeAmulet()
        {
            GameObject root = NewRoot("jade_amulet");
            Transform neck = Node(root, "Socket_Neck", Vector3.zero, Vector3.zero);

            Vector3 pendant = new Vector3(0f, -0.20f, 0.25f);
            Bar(neck, "ChainL", "Steel", new Vector3(-0.17f, 0.04f, 0.02f), pendant, 0.03f);
            Bar(neck, "ChainR", "Steel", new Vector3(0.17f, 0.04f, 0.02f), pendant, 0.03f);
            Shape(neck, "Bezel", "Cylinder", "Steel", new Vector3(0f, -0.235f, 0.235f), new Vector3(0.17f, 0.04f, 0.17f), new Vector3(90f, 0f, 0f));
            Shape(neck, "Gem", "IcoHead", "GemGreen", new Vector3(0f, -0.235f, 0.27f), new Vector3(0.12f, 0.12f, 0.05f));

            Save(root, "jade_amulet");
        }

        private static void Ring(string id, string bandMat, string gemMat)
        {
            GameObject root = NewRoot(id);
            Transform ring = Node(root, "Socket_Ring", Vector3.zero, Vector3.zero);

            Shape(ring, "Band", "Cylinder", bandMat, new Vector3(0f, -0.03f, 0.12f), new Vector3(0.11f, 0.04f, 0.11f), new Vector3(90f, 0f, 0f));
            Cube(ring, "Gem", gemMat, new Vector3(0f, 0.03f, 0.145f), new Vector3(0.055f, 0.055f, 0.05f), new Vector3(0f, 0f, 45f));

            Save(root, id);
        }

        // Built along +y from the grip, then tipped so the blade points forward and slightly down
        // (the character's arms hang at the sides, so the sword is held out in front).
        private static void RustySword()
        {
            GameObject root = NewRoot("rusty_sword");
            Transform sword = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), new Vector3(112f, 0f, 0f));

            Cube(sword, "Grip", "Leather", new Vector3(0f, 0.11f, 0f), new Vector3(0.06f, 0.22f, 0.06f));
            Shape(sword, "Pommel", "IcoHead", "GemGreen", new Vector3(0f, -0.03f, 0f), new Vector3(0.09f, 0.09f, 0.09f));
            Cube(sword, "Guard", "Gold", new Vector3(0f, 0.245f, 0f), new Vector3(0.34f, 0.05f, 0.09f));
            Cube(sword, "Blade", "RustSteel", new Vector3(0f, 0.67f, 0f), new Vector3(0.10f, 0.83f, 0.028f));
            Cube(sword, "RustA", "Rust", new Vector3(0f, 0.50f, 0f), new Vector3(0.102f, 0.16f, 0.03f));
            Cube(sword, "RustB", "Rust", new Vector3(0.02f, 0.80f, 0f), new Vector3(0.06f, 0.20f, 0.032f));
            Shape(sword, "Tip", "Cone", "RustSteel", new Vector3(0f, 1.085f, 0f), new Vector3(0.05f, 0.14f, 0.014f));

            Save(root, "rusty_sword");
        }

        // Strapped to the off forearm and held facing forward.
        private static void WoodenShield()
        {
            GameObject root = NewRoot("wooden_shield");
            // Turned 40 degrees outward so it sits angled on the arm, while its face still points
            // mostly forward (so it stays visible from the front, e.g. in the inventory preview).
            Transform shield = Node(root, "Socket_OffHand", Vector3.zero, new Vector3(0f, -40f, 0f));

            Shape(shield, "Rim", "Cylinder", "Steel", new Vector3(0f, 0.32f, 0.12f), new Vector3(0.76f, 0.05f, 0.76f), new Vector3(90f, 0f, 0f));
            Shape(shield, "Face", "Cylinder", "ShieldWood", new Vector3(0f, 0.32f, 0.15f), new Vector3(0.70f, 0.06f, 0.70f), new Vector3(90f, 0f, 0f));
            Cube(shield, "PlankL", "ShieldWoodDark", new Vector3(-0.17f, 0.32f, 0.212f), new Vector3(0.025f, 0.58f, 0.012f));
            Cube(shield, "PlankR", "ShieldWoodDark", new Vector3(0.17f, 0.32f, 0.212f), new Vector3(0.025f, 0.58f, 0.012f));
            Shape(shield, "Boss", "IcoHead", "Steel", new Vector3(0f, 0.32f, 0.235f), new Vector3(0.20f, 0.20f, 0.10f));
            Shape(shield, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.32f, 0.26f), new Vector3(0.09f, 0.09f, 0.06f));

            Save(root, "wooden_shield");
        }
    }
}
