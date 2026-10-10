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
            "jade_amulet", "iron_ring", "ruby_ring", "sapphire_ring", "rusty_sword", "wooden_shield",
            "hand_axe", "iron_mace", "steel_dagger", "short_bow", "leather_quiver",
            "sage_circlet", "bastard_sword", "woodsplitter", "great_mallet", "bone_sceptre", "grimoire"
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
            WeaponModels();
            VarietyModels();
            SummonerModels();

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
            foreach (string name in new[] { "Cone", "Cylinder", "IcoHead" })
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
            Mat("Silver", "#c9ced6", 0.45f, false);
            Mat("PaleWood", "#d6c79a", 0.05f, false);
            Mat("BowString", "#ece6d6", 0.05f, false);
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

        /// <summary>
        /// Just the weapons added after the starter set (axe, mace, dagger, bow, quiver), leaving the
        /// other prefabs untouched. Build Equipment also builds these.
        /// </summary>
        [MenuItem("PoeClone/Build Weapons")]
        public static void BuildWeapons()
        {
            EnsureFolder(OutDir);
            LoadMeshes();
            MakeMaterials();
            WeaponModels();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("EquipmentBuilder: built the weapon prefabs");
        }

        /// <summary>The circlet and the great (two-handed) weapons, leaving the other prefabs untouched.</summary>
        [MenuItem("PoeClone/Build Great Weapons")]
        public static void BuildVariety()
        {
            EnsureFolder(OutDir);
            LoadMeshes();
            MakeMaterials();
            VarietyModels();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("EquipmentBuilder: built the circlet and great weapon prefabs");
        }

        private static void VarietyModels()
        {
            SageCirclet();
            BastardSword();
            Woodsplitter();
            GreatMallet();
        }

        // A thin gold band around the brow with a gem over the forehead and three small points.
        private static void SageCirclet()
        {
            GameObject root = NewRoot("sage_circlet");
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            Shape(head, "Band", "Cylinder", "Gold", new Vector3(0f, 0.11f, -0.04f), new Vector3(0.57f, 0.035f, 0.57f));
            Shape(head, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.13f, 0.245f), new Vector3(0.07f, 0.08f, 0.04f));
            Shape(head, "PointC", "Cone", "Gold", new Vector3(0f, 0.20f, 0.235f), new Vector3(0.035f, 0.09f, 0.02f));
            Shape(head, "PointL", "Cone", "Gold", new Vector3(-0.12f, 0.16f, 0.21f), new Vector3(0.025f, 0.06f, 0.02f), new Vector3(0f, -30f, 12f));
            Shape(head, "PointR", "Cone", "Gold", new Vector3(0.12f, 0.16f, 0.21f), new Vector3(0.025f, 0.06f, 0.02f), new Vector3(0f, 30f, -12f));

            Save(root, "sage_circlet");
        }

        // Great weapons are built like the one-handed ones (along +y from the hand), with a grip long
        // enough for both hands and much more weapon past it.
        private static readonly Vector3 GreatGrip = new Vector3(108f, 0f, 0f);

        private static void BastardSword()
        {
            GameObject root = NewRoot("bastard_sword");
            Transform sword = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), GreatGrip);

            Cube(sword, "Grip", "Leather", new Vector3(0f, 0.08f, 0f), new Vector3(0.065f, 0.40f, 0.065f));
            Shape(sword, "Pommel", "IcoHead", "Gold", new Vector3(0f, -0.15f, 0f), new Vector3(0.11f, 0.11f, 0.11f));
            Cube(sword, "Guard", "Gold", new Vector3(0f, 0.30f, 0f), new Vector3(0.48f, 0.06f, 0.10f));
            Shape(sword, "GuardGemL", "IcoHead", "GemGreen", new Vector3(-0.25f, 0.30f, 0f), new Vector3(0.07f, 0.07f, 0.07f));
            Shape(sword, "GuardGemR", "IcoHead", "GemGreen", new Vector3(0.25f, 0.30f, 0f), new Vector3(0.07f, 0.07f, 0.07f));
            Cube(sword, "Ricasso", "Steel", new Vector3(0f, 0.40f, 0f), new Vector3(0.11f, 0.14f, 0.04f));
            Cube(sword, "Blade", "Silver", new Vector3(0f, 0.97f, 0f), new Vector3(0.15f, 1.02f, 0.03f));
            Cube(sword, "Fuller", "SteelDark", new Vector3(0f, 0.92f, 0f), new Vector3(0.035f, 0.85f, 0.034f));
            Shape(sword, "Tip", "Cone", "Silver", new Vector3(0f, 1.56f, 0f), new Vector3(0.075f, 0.17f, 0.015f));

            Save(root, "bastard_sword");
        }

        private static void Woodsplitter()
        {
            GameObject root = NewRoot("woodsplitter");
            Transform axe = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), GreatGrip);

            Cube(axe, "Haft", "PaleWood", new Vector3(0f, 0.48f, 0f), new Vector3(0.07f, 1.40f, 0.07f));
            Cube(axe, "Wrap", "Leather", new Vector3(0f, 0.10f, 0f), new Vector3(0.078f, 0.36f, 0.078f));
            Shape(axe, "Pommel", "IcoHead", "Gold", new Vector3(0f, -0.24f, 0f), new Vector3(0.10f, 0.10f, 0.10f));
            Cube(axe, "Collar", "Gold", new Vector3(0f, 1.02f, 0f), new Vector3(0.12f, 0.16f, 0.10f));
            Cube(axe, "BladeR", "Silver", new Vector3(0.19f, 1.02f, 0f), new Vector3(0.28f, 0.26f, 0.035f));
            Cube(axe, "EdgeR", "Steel", new Vector3(0.35f, 1.02f, 0f), new Vector3(0.07f, 0.48f, 0.037f));
            Cube(axe, "BladeL", "Silver", new Vector3(-0.19f, 1.02f, 0f), new Vector3(0.28f, 0.26f, 0.035f));
            Cube(axe, "EdgeL", "Steel", new Vector3(-0.35f, 1.02f, 0f), new Vector3(0.07f, 0.48f, 0.037f));
            Shape(axe, "Spike", "Cone", "Silver", new Vector3(0f, 1.24f, 0f), new Vector3(0.05f, 0.18f, 0.05f));
            Shape(axe, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 1.02f, 0.055f), new Vector3(0.07f, 0.07f, 0.04f));

            Save(root, "woodsplitter");
        }

        private static void GreatMallet()
        {
            GameObject root = NewRoot("great_mallet");
            Transform maul = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), GreatGrip);

            Cube(maul, "Haft", "PaleWood", new Vector3(0f, 0.42f, 0f), new Vector3(0.075f, 1.28f, 0.075f));
            Cube(maul, "Wrap", "Leather", new Vector3(0f, 0.08f, 0f), new Vector3(0.085f, 0.34f, 0.085f));
            Shape(maul, "Pommel", "IcoHead", "Gold", new Vector3(0f, -0.24f, 0f), new Vector3(0.11f, 0.11f, 0.11f));
            Cube(maul, "Head", "SteelDark", new Vector3(0f, 1.12f, 0f), new Vector3(0.50f, 0.28f, 0.28f));
            Cube(maul, "FaceR", "Steel", new Vector3(0.26f, 1.12f, 0f), new Vector3(0.05f, 0.34f, 0.34f));
            Cube(maul, "FaceL", "Steel", new Vector3(-0.26f, 1.12f, 0f), new Vector3(0.05f, 0.34f, 0.34f));
            Cube(maul, "BandA", "Gold", new Vector3(0.11f, 1.12f, 0f), new Vector3(0.04f, 0.30f, 0.30f));
            Cube(maul, "BandB", "Gold", new Vector3(-0.11f, 1.12f, 0f), new Vector3(0.04f, 0.30f, 0.30f));
            Shape(maul, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 1.27f, 0f), new Vector3(0.08f, 0.08f, 0.08f));

            Save(root, "great_mallet");
        }

        /// <summary>Just the summoner's sceptre and grimoire, leaving every other prefab untouched.</summary>
        [MenuItem("PoeClone/Build Summoner Equipment")]
        public static void BuildSummoner()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(OutDir);
            LoadMeshes();
            MakeMaterials();
            SummonerModels();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (string id in new[] { "bone_sceptre", "grimoire" })
                AssetDatabase.ImportAsset(OutDir + "/" + id + ".prefab", ImportAssetOptions.ForceUpdate);
            Debug.Log("EquipmentBuilder: built the summoner's sceptre and grimoire");
        }

        private static void SummonerModels()
        {
            BoneSceptre();
            Grimoire();
        }

        // A short rod of bone topped with a little horned skull, a green gem for an eye.
        private static void BoneSceptre()
        {
            GameObject root = NewRoot("bone_sceptre");
            Transform rod = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), new Vector3(112f, 0f, 0f));

            Cube(rod, "Shaft", "BowString", new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.62f, 0.05f));
            Cube(rod, "Wrap", "Leather", new Vector3(0f, 0.06f, 0f), new Vector3(0.062f, 0.20f, 0.062f));
            Shape(rod, "Knuckle", "IcoHead", "BowString", new Vector3(0f, -0.06f, 0f), new Vector3(0.08f, 0.08f, 0.08f));
            Cube(rod, "Collar", "Gold", new Vector3(0f, 0.58f, 0f), new Vector3(0.09f, 0.05f, 0.09f));
            Shape(rod, "Skull", "IcoHead", "BowString", new Vector3(0f, 0.71f, 0f), new Vector3(0.20f, 0.21f, 0.22f));
            Cube(rod, "Jaw", "TanLight", new Vector3(0f, 0.63f, 0.03f), new Vector3(0.12f, 0.05f, 0.12f));
            Shape(rod, "EyeL", "IcoHead", "GemGreen", new Vector3(-0.045f, 0.73f, 0.095f), new Vector3(0.05f, 0.05f, 0.03f));
            Shape(rod, "EyeR", "IcoHead", "GemGreen", new Vector3(0.045f, 0.73f, 0.095f), new Vector3(0.05f, 0.05f, 0.03f));
            Shape(rod, "HornL", "Cone", "TanLight", new Vector3(-0.10f, 0.83f, -0.01f), new Vector3(0.035f, 0.12f, 0.035f), new Vector3(0f, 0f, 30f));
            Shape(rod, "HornR", "Cone", "TanLight", new Vector3(0.10f, 0.83f, -0.01f), new Vector3(0.035f, 0.12f, 0.035f), new Vector3(0f, 0f, -30f));

            Save(root, "bone_sceptre");
        }

        // A thick leather-bound tome held at the side: bone-white page edges, gold corners and
        // clasp, a green gem set in the cover.
        private static void Grimoire()
        {
            GameObject root = NewRoot("grimoire");
            Transform book = Node(root, "Socket_OffHand", Vector3.zero, new Vector3(0f, -40f, 0f));

            Cube(book, "Pages", "BowString", new Vector3(0f, 0.24f, 0.14f), new Vector3(0.30f, 0.40f, 0.10f));
            Cube(book, "CoverFront", "Leather", new Vector3(0f, 0.24f, 0.20f), new Vector3(0.34f, 0.44f, 0.025f));
            Cube(book, "CoverBack", "Leather", new Vector3(0f, 0.24f, 0.08f), new Vector3(0.34f, 0.44f, 0.025f));
            Cube(book, "Spine", "Leather", new Vector3(-0.16f, 0.24f, 0.14f), new Vector3(0.04f, 0.44f, 0.14f));
            Cube(book, "Band", "Gold", new Vector3(-0.16f, 0.36f, 0.14f), new Vector3(0.045f, 0.03f, 0.145f));
            Cube(book, "Band2", "Gold", new Vector3(-0.16f, 0.12f, 0.14f), new Vector3(0.045f, 0.03f, 0.145f));
            Cube(book, "Clasp", "Gold", new Vector3(0.17f, 0.24f, 0.14f), new Vector3(0.03f, 0.07f, 0.15f));
            Cube(book, "CornerA", "Gold", new Vector3(0.15f, 0.44f, 0.21f), new Vector3(0.05f, 0.05f, 0.01f));
            Cube(book, "CornerB", "Gold", new Vector3(0.15f, 0.04f, 0.21f), new Vector3(0.05f, 0.05f, 0.01f));
            Shape(book, "Sigil", "IcoHead", "TanLight", new Vector3(0f, 0.26f, 0.215f), new Vector3(0.12f, 0.13f, 0.02f));
            Shape(book, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.26f, 0.225f), new Vector3(0.06f, 0.06f, 0.03f));

            Save(root, "grimoire");
        }

        private static void WeaponModels()
        {
            HandAxe();
            IronMace();
            SteelDagger();
            ShortBow();
            LeatherQuiver();
        }

        // Like the sword: built along +y from the grip, tipped to point forward and slightly down.
        private static void HandAxe()
        {
            GameObject root = NewRoot("hand_axe");
            Transform axe = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), new Vector3(112f, 0f, 0f));

            Cube(axe, "Haft", "PaleWood", new Vector3(0f, 0.30f, 0f), new Vector3(0.06f, 0.72f, 0.06f));
            Shape(axe, "Pommel", "IcoHead", "GemGreen", new Vector3(0f, -0.05f, 0f), new Vector3(0.08f, 0.08f, 0.08f));
            Cube(axe, "Collar", "Gold", new Vector3(0f, 0.60f, 0f), new Vector3(0.10f, 0.10f, 0.08f));
            Cube(axe, "Blade", "Silver", new Vector3(0.13f, 0.60f, 0f), new Vector3(0.20f, 0.20f, 0.03f));
            Cube(axe, "Edge", "Steel", new Vector3(0.245f, 0.60f, 0f), new Vector3(0.05f, 0.34f, 0.032f));
            Cube(axe, "Spike", "Silver", new Vector3(-0.09f, 0.62f, 0f), new Vector3(0.10f, 0.07f, 0.03f));
            Shape(axe, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.60f, 0.045f), new Vector3(0.06f, 0.06f, 0.04f));

            Save(root, "hand_axe");
        }

        private static void IronMace()
        {
            GameObject root = NewRoot("iron_mace");
            Transform mace = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), new Vector3(112f, 0f, 0f));

            Cube(mace, "Handle", "PaleWood", new Vector3(0f, 0.27f, 0f), new Vector3(0.055f, 0.62f, 0.055f));
            Shape(mace, "Pommel", "IcoHead", "Gold", new Vector3(0f, -0.05f, 0f), new Vector3(0.08f, 0.08f, 0.08f));
            Cube(mace, "Collar", "Gold", new Vector3(0f, 0.58f, 0f), new Vector3(0.10f, 0.05f, 0.10f));
            Shape(mace, "Head", "IcoHead", "Silver", new Vector3(0f, 0.72f, 0f), new Vector3(0.22f, 0.28f, 0.22f));
            Cube(mace, "FlangeX", "Steel", new Vector3(0f, 0.72f, 0f), new Vector3(0.30f, 0.22f, 0.04f));
            Cube(mace, "FlangeZ", "Steel", new Vector3(0f, 0.72f, 0f), new Vector3(0.04f, 0.22f, 0.30f));
            Shape(mace, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.88f, 0f), new Vector3(0.07f, 0.07f, 0.07f));

            Save(root, "iron_mace");
        }

        private static void SteelDagger()
        {
            GameObject root = NewRoot("steel_dagger");
            Transform dagger = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), new Vector3(112f, 0f, 0f));

            Cube(dagger, "Grip", "PaleWood", new Vector3(0f, 0.06f, 0f), new Vector3(0.045f, 0.14f, 0.045f));
            Shape(dagger, "Pommel", "IcoHead", "GemBlue", new Vector3(0f, -0.03f, 0f), new Vector3(0.06f, 0.06f, 0.06f));
            Cube(dagger, "Guard", "Gold", new Vector3(0f, 0.145f, 0f), new Vector3(0.18f, 0.035f, 0.06f));
            Cube(dagger, "Blade", "Silver", new Vector3(0f, 0.33f, 0f), new Vector3(0.07f, 0.34f, 0.02f));
            Shape(dagger, "Tip", "Cone", "Silver", new Vector3(0f, 0.54f, 0f), new Vector3(0.035f, 0.09f, 0.01f));

            Save(root, "steel_dagger");
        }

        // Held in the off (left) hand, like a real archer: the right hand draws. Built upright along
        // +y with the belly towards +z, then turned so that with the arm raised at the target (the
        // bow attack) the bow stands upright, belly to the target and string to the archer. With
        // the arm hanging at rest that leaves it carried level at the hip, pointing forward.
        private static void ShortBow()
        {
            GameObject root = NewRoot("short_bow");
            // The grip sits at model z=0.22; offset it onto the palm after the 90-degree turn.
            Transform bow = Node(root, "Socket_OffHand", new Vector3(0f, 0.32f, -0.05f), new Vector3(90f, 0f, 0f));

            Cube(bow, "Grip", "Leather", new Vector3(0f, 0f, 0.22f), new Vector3(0.06f, 0.16f, 0.06f));
            Bar(bow, "UpperInner", "Silver", new Vector3(0f, 0.07f, 0.22f), new Vector3(0f, 0.30f, 0.16f), 0.05f);
            Bar(bow, "UpperOuter", "Silver", new Vector3(0f, 0.30f, 0.16f), new Vector3(0f, 0.52f, 0f), 0.04f);
            Bar(bow, "LowerInner", "Silver", new Vector3(0f, -0.07f, 0.22f), new Vector3(0f, -0.30f, 0.16f), 0.05f);
            Bar(bow, "LowerOuter", "Silver", new Vector3(0f, -0.30f, 0.16f), new Vector3(0f, -0.52f, 0f), 0.04f);
            Shape(bow, "TipTop", "Cone", "Gold", new Vector3(0f, 0.55f, 0f), new Vector3(0.03f, 0.07f, 0.03f));
            Shape(bow, "TipBottom", "Cone", "Gold", new Vector3(0f, -0.55f, 0f), new Vector3(0.03f, 0.07f, 0.03f), new Vector3(180f, 0f, 0f));
            Bar(bow, "String", "BowString", new Vector3(0f, 0.52f, 0f), new Vector3(0f, -0.52f, 0f), 0.01f);
            Shape(bow, "GemTop", "IcoHead", "GemGreen", new Vector3(0f, 0.14f, 0.22f), new Vector3(0.05f, 0.05f, 0.04f));
            Shape(bow, "GemBottom", "IcoHead", "GemGreen", new Vector3(0f, -0.14f, 0.22f), new Vector3(0.05f, 0.05f, 0.04f));

            Save(root, "short_bow");
        }

        // Worn on the back, tilted over the right shoulder so the arrows are within the drawing
        // hand's reach: a leather tube with gold bands, fletched arrows standing out of the top.
        private static void LeatherQuiver()
        {
            GameObject root = NewRoot("leather_quiver");
            Transform quiver = Node(root, "Socket_Chest", new Vector3(0.06f, 0.05f, -0.30f), new Vector3(0f, 0f, -22f));

            Shape(quiver, "Tube", "Cylinder", "Leather", new Vector3(0f, 0.05f, 0f), new Vector3(0.20f, 0.30f, 0.20f));
            Shape(quiver, "Bottom", "Cylinder", "TanDark", new Vector3(0f, -0.26f, 0f), new Vector3(0.21f, 0.03f, 0.21f));
            Shape(quiver, "BandTop", "Cylinder", "Gold", new Vector3(0f, 0.33f, 0f), new Vector3(0.215f, 0.03f, 0.215f));
            Shape(quiver, "BandMid", "Cylinder", "Gold", new Vector3(0f, -0.05f, 0f), new Vector3(0.21f, 0.025f, 0.21f));

            float[] xs = { -0.05f, 0.0f, 0.05f };
            float[] zs = { 0.02f, -0.03f, 0.02f };
            for (int k = 0; k < 3; k++)
            {
                Cube(quiver, "Shaft" + k, "PaleWood", new Vector3(xs[k], 0.42f, zs[k]), new Vector3(0.018f, 0.24f, 0.018f));
                Cube(quiver, "Fletch" + k, k == 1 ? "GemRed" : "BowString", new Vector3(xs[k], 0.52f, zs[k]), new Vector3(0.05f, 0.09f, 0.012f));
            }

            Save(root, "leather_quiver");
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
