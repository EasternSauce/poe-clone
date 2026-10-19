using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Builds the 3D look of every piece of gear as a prefab in Resources/Equipment/{model}.prefab,
    /// using the same low-poly meshes and palette as the rest of the game, and styled after each item's icon.
    /// Each direct child of a prefab is named after the character socket it attaches to
    /// (Socket_Head, Socket_HandL, ...). "Socket_Ring" lands on whichever hand the ring slot uses.
    /// Worn pieces are shaped round the body of <see cref="HumanoidBodyBuilder"/>; see the socket
    /// notes above each group. Re-runnable: PoeClone > Build Equipment.
    /// </summary>
    public static partial class EquipmentBuilder
    {
        private const string OutDir = "Assets/Resources/Equipment";
        private const string MatDir = "Assets/Materials/Stylized";

        private static readonly string[] AllIds =
        {
            "iron_helmet", "bronze_helmet", "hunter_hood", "sage_circlet",
            "studded_vest", "chain_hauberk", "silk_robe",
            "leather_gloves", "iron_gauntlets", "silk_gloves",
            "leather_boots", "iron_greaves", "silk_slippers",
            "rope_belt", "jade_amulet", "iron_ring", "ruby_ring", "sapphire_ring",
            "wooden_shield", "kite_shield", "tower_shield", "buckler", "spiked_buckler", "spirit_focus",
            "rusty_sword", "hand_axe", "iron_mace", "steel_dagger", "short_bow", "crude_bow", "leather_quiver",
            "bastard_sword", "woodsplitter", "great_mallet", "sledgehammer", "bone_sceptre", "grimoire"
        };

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        [MenuItem("PoeClone/Refit Worn Armour")]
        public static void RefitWornArmour()
        {
            LoadMeshes();
            Mats.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Mats[material.name] = material;
            }
            Helmets();
            BodyArmours();
            Gloves();
            Boots();
            AnatomyRigBuilder.UpgradeGloves();
            Belt();
            JadeAmulet();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("PoeClone/Build Equipment")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(OutDir);
            LoadMeshes();
            MakeMaterials();

            Helmets();
            BodyArmours();
            Gloves();
            Boots();
            Belt();
            JadeAmulet();
            Ring("iron_ring", "SteelDark", "RustSteel");
            Ring("ruby_ring", "Steel", "GemRed");
            Ring("sapphire_ring", "Steel", "GemBlue");
            Shields();
            RustySword();
            WeaponModels();
            VarietyModels();
            SummonerModels();

            AnatomyRigBuilder.UpgradeGloves();

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
            foreach (KeyValuePair<string, Mesh> pair in LowPolyMeshes.BuildShared())
                Meshes[pair.Key] = pair.Value;
            var rings = new List<LowPolyMeshes.Ring>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                rings.Add(LowPolyMeshes.R(.5f - t, Mathf.Lerp(.39f, .52f, Mathf.Min(1f, t * 4f))));
            }
            Meshes["FittedSkirt"] = LowPolyMeshes.Save("FittedSkirt",
                LowPolyMeshes.Shell("FittedSkirt", 32, .035f, 0, 360, false, rings.ToArray()));
            rings.Clear();
            for (int i = 0; i <= 8; i++) rings.Add(LowPolyMeshes.R(.5f - i / 8f, .5f));
            Meshes["FittedHem"] = LowPolyMeshes.Save("FittedHem",
                LowPolyMeshes.Shell("FittedHem", 32, .035f, 0, 360, false, rings.ToArray()));
            foreach (string name in new[] { "Cone", "Cylinder", "IcoHead" })
            {
                Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(LowPolyMeshes.MeshDir + "/" + name + ".asset");
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

        // A gear material in the toon style. A new one starts as a copy of `template` (so it gets
        // the same shader, texture and tiling); its colour, shadow and rim follow from `hex`, and
        // `texture` (0..1, if given) sets how strongly the texture shows.
        private static void Mat(string name, string hex, float smoothness, string template = null, float texture = -1f)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Material from = template != null ? AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + template + ".mat") : null;
                m = from != null ? new Material(from) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }

            Color c = H(hex);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_ShadowColor"))
            {
                m.SetColor("_ShadowColor", new Color(c.r * 0.5f, c.g * 0.5f, Mathf.Min(1f, c.b * 0.62f), 1f));
                m.SetColor("_RimColor", Color.Lerp(c, Color.white, 0.6f));
            }

            if (texture >= 0f && m.HasProperty("_TexInfluence"))
                m.SetFloat("_TexInfluence", texture);

            // No emission: it needs its own shader variant, which the editor compiles asynchronously
            // and shows as magenta until it finishes. Gems shine through smoothness instead.
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);

            EditorUtility.SetDirty(m);
            Mats[name] = m;
        }

        // Colours are chosen to match the painted icons. Higher tiers tint these (ItemData.ArtTint),
        // so the cloth and hood colours are pale enough to take a tier's colour.
        private static void MakeMaterials()
        {
            Mats.Clear();
            Mat("Tan", "#c2a274", 0.05f);
            Mat("TanDark", "#8a6a44", 0.05f);
            Mat("TanLight", "#d8c197", 0.05f);
            Mat("Steel", "#8b929c", 0.35f);
            Mat("SteelDark", "#5d626b", 0.2f);
            Mat("Bronze", "#b07b3c", 0.3f);
            Mat("BronzeLight", "#d9a45a", 0.3f);
            Mat("Gold", "#c9a227", 0.35f);
            Mat("Rope", "#b89a63", 0.05f);
            Mat("RopeDark", "#7d6640", 0.05f);
            Mat("Leather", "#3b2a1d", 0.05f);
            Mat("GemGreen", "#35d17f", 0.5f);
            Mat("GemRed", "#e03a4a", 0.5f);
            Mat("GemBlue", "#3f6ff0", 0.5f);
            Mat("RustSteel", "#8a7e73", 0.1f);
            Mat("Rust", "#9b4f2a", 0.05f);
            Mat("ShieldWood", "#8a5e34", 0.05f);
            Mat("ShieldWoodDark", "#5e3f22", 0.05f);
            Mat("Silver", "#c9ced6", 0.45f);
            Mat("PaleWood", "#d6c79a", 0.05f);
            Mat("BowString", "#ece6d6", 0.05f);
            Mat("GearCloth", "#f2ead6", 0.1f, "TanLight", 0.35f);
            Mat("GearClothTrim", "#2f8f86", 0.05f, "TanLight");
            Mat("HoodCloth", "#b4b4a8", 0.05f, "Tan");
            Mat("Crystal", "#cfe0ff", 0.6f, "Silver");
            Mat("Visor", "#1c1712", 0.05f, "Leather");
            Mat("BookLeather", "#6e2f2c", 0.05f, "Leather");
        }

        // ------------------------------------------------------------------ builders

        private static GameObject NewRoot(string id)
        {
            return new GameObject(id);
        }

        // Legacy hand shapes; UpgradeGloves replaces this with the anatomical hand's own size.
        private const float LimbWidth = 1.25f;

        // A direct child of the prefab root, named for the character socket it attaches to.
        private static Transform Node(GameObject root, string socketName, Vector3 pos, Vector3 euler)
        {
            GameObject go = new GameObject(socketName);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        // A socket node on a limb (hands, feet, shoulders, rings), widened to fit the thicker arms and legs.
        private static Transform LimbNode(GameObject root, string socketName)
        {
            Transform t = Node(root, socketName, Vector3.zero, Vector3.zero);
            t.localScale = new Vector3(LimbWidth, 1f, LimbWidth);
            return t;
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
            FitCurrentAnatomy(root, id);
            TailorWearable(root, id);
            PrefabUtility.SaveAsPrefabAsset(root, OutDir + "/" + id + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static void FitCurrentAnatomy(GameObject root, string id)
        {
            if (id == "studded_vest" || id == "chain_hauberk" || id == "silk_robe")
            {
                Transform chest = root.transform.Find("Socket_Chest");
                foreach (Transform part in chest)
                {
                    part.localPosition = Vector3.Scale(part.localPosition, new Vector3(.93f, 1f, .91f));
                    part.localScale = Vector3.Scale(part.localScale, new Vector3(.93f, 1f, .91f));
                }
                // A continuous overlap at the waist, with the same tailored silhouette as the chest.
                foreach (string name in new[] { "Hem", "Fauld", "FauldTrim", "Skirt" })
                {
                    Transform part = chest.Find(name);
                    if (part == null) continue;
                    Vector3 scale = part.localScale;
                    if (name == "Hem" && id == "studded_vest") { scale.y = .33f; part.localPosition = new Vector3(0, -.40f, 0); }
                    if (name == "Fauld") { scale.y = .34f; part.localPosition = new Vector3(0, -.40f, 0); }
                    if (name == "FauldTrim") part.localPosition = new Vector3(0, -.56f, 0);
                    if (name == "Skirt") { scale.y = .65f; part.localPosition = new Vector3(0, -.56f, 0); }
                    part.localScale = scale;
                }
                if (id == "silk_robe") chest.Find("Hem").localPosition = new Vector3(0, -.865f, 0);
                Transform skirt = chest.Find(id == "chain_hauberk" ? "Fauld" : "Skirt");
                Transform hem = chest.Find(id == "chain_hauberk" ? "FauldTrim" : "Hem");
                if (skirt != null && hem != null)
                    hem.localScale = new Vector3(skirt.localScale.x * 1.065f, hem.localScale.y, skirt.localScale.z * 1.065f);
                foreach (string side in new[] { "L", "R" })
                {
                    Transform shoulder = root.transform.Find("Socket_Shoulder" + side);
                    foreach (Transform part in shoulder)
                    {
                        part.localPosition = Vector3.Scale(part.localPosition, new Vector3(.84f, 1f, .88f));
                        part.localScale = Vector3.Scale(part.localScale, new Vector3(.84f, 1f, .88f));
                    }
                    // The connected arm already provides a fitted, animated fabric sleeve.
                    // Old open cylinders floated above it and exposed hollow shoulder holes.
                    Transform sleeve = shoulder.Find("Sleeve");
                    if (sleeve != null) Object.DestroyImmediate(sleeve.gameObject);
                    Transform trim = shoulder.Find("SleeveTrim");
                    if (trim != null)
                    {
                        trim.localPosition = new Vector3(0, -.32f, 0);
                        trim.localScale = new Vector3(.14f, .018f, .15f);
                    }

                }
            }
            if (id == "iron_helmet" || id == "bronze_helmet" || id == "sage_circlet" || id == "hunter_hood")
            {
                Transform head = root.transform.Find("Socket_Head");
                head.localScale = new Vector3(.86f, .91f, .86f);
                if (id == "hunter_hood")
                {
                    // The cowl rests on the shoulders, independently of head glances and stretches.
                    Transform cowl = head.Find("Cowl");
                    Transform neck = Node(root, "Socket_Neck", Vector3.zero, Vector3.zero);
                    cowl.SetParent(neck, false);
                    cowl.localPosition = new Vector3(0, -.005f, -.01f);
                    cowl.localScale = new Vector3(.45f, .12f, .34f);
                }
            }
        }

        // ------------------------------------------------------------------ helmets

        // Socket_Head is the centre of the head (radius about 0.18). The player's hair reaches 0.215
        // above it and 0.215 behind, so every helmet covers that.
        private static void Helmets()
        {
            // Silver, open-faced: a dome, cheek guards round the sides and back, a nose guard,
            // swept-back ear fins and a green gem on the brow.
            OpenHelm("iron_helmet", "Steel", "SteelDark", "Steel");
            ClosedHelm();
            Hood();
            SageCirclet();
        }

        private static void HelmDome(Transform head, string mat, string trim)
        {
            Shape(head, "Dome", "GearDome", mat, new Vector3(0f, 0.15f, -0.02f), new Vector3(0.45f, 0.2f, 0.47f));
            Shape(head, "Brow", "GearBand", trim, new Vector3(0f, 0.055f, -0.02f), new Vector3(0.47f, 0.04f, 0.49f));
        }

        private static void EarFins(Transform head, string mat)
        {
            foreach (float s in new[] { -1f, 1f })
                Shape(head, s < 0f ? "EarL" : "EarR", "Cone", mat, new Vector3(0.222f * s, 0.01f, -0.04f), new Vector3(0.018f, 0.15f, 0.055f), new Vector3(-55f, 0f, -12f * s));
        }

        private static void OpenHelm(string id, string mat, string trim, string fins)
        {
            GameObject root = NewRoot(id);
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            HelmDome(head, mat, trim);
            Shape(head, "Guard", "GearHelmGuard", mat, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.45f, 0.22f, 0.47f));
            Shape(head, "NoseGuard", "GearLeaf", mat, new Vector3(0f, -0.005f, 0.222f), new Vector3(0.05f, 0.15f, 0.03f));
            EarFins(head, fins);
            Shape(head, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.09f, 0.22f), new Vector3(0.06f, 0.075f, 0.04f));

            Save(root, id);
        }

        // Bronze, closed-faced: the guard closes round the face, with a dark eye slit and nose slit
        // (a T), a crest along the top and the same fins and gem.
        private static void ClosedHelm()
        {
            GameObject root = NewRoot("bronze_helmet");
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            HelmDome(head, "Bronze", "Gold");
            Shape(head, "Face", "GearCuff", "Bronze", new Vector3(0f, -0.06f, -0.02f), new Vector3(0.45f, 0.22f, 0.47f));
            // The eye slit lines up with the face's eyes, which sit just behind it.
            Cube(head, "EyeSlit", "Visor", new Vector3(0f, -0.022f, 0.226f), new Vector3(0.24f, 0.056f, 0.02f));
            Cube(head, "MouthSlit", "Visor", new Vector3(0f, -0.08f, 0.2f), new Vector3(0.03f, 0.11f, 0.02f));
            Cube(head, "Crest", "BronzeLight", new Vector3(0f, 0.225f, -0.03f), new Vector3(0.025f, 0.045f, 0.3f));
            EarFins(head, "BronzeLight");
            Shape(head, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.09f, 0.22f), new Vector3(0.06f, 0.075f, 0.04f));

            Save(root, "bronze_helmet");
        }

        // A cloth hood open at the face, falling into a cowl round the neck and shoulders.
        private static void Hood()
        {
            GameObject root = NewRoot("hunter_hood");
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            Shape(head, "Hood", "GearHood", "HoodCloth", new Vector3(0f, 0.04f, -0.03f), new Vector3(0.47f, 0.5f, 0.5f));
            Shape(head, "Cowl", "GearFlare", "HoodCloth", new Vector3(0f, -0.25f, -0.03f), new Vector3(0.58f, 0.2f, 0.48f));

            Save(root, "hunter_hood");
        }

        // A gold band round the brow over a teal one, gold leaves rising from it and a green gem
        // over the forehead.
        private static void SageCirclet()
        {
            GameObject root = NewRoot("sage_circlet");
            Transform head = Node(root, "Socket_Head", Vector3.zero, Vector3.zero);

            Shape(head, "Band", "GearBand", "Gold", new Vector3(0f, 0.075f, -0.02f), new Vector3(0.42f, 0.035f, 0.45f));
            Shape(head, "Underband", "GearBand", "GearClothTrim", new Vector3(0f, 0.05f, -0.02f), new Vector3(0.41f, 0.025f, 0.44f));
            Shape(head, "LeafC", "GearLeaf", "Gold", new Vector3(0f, 0.13f, 0.2f), new Vector3(0.07f, 0.12f, 0.025f));
            foreach (float s in new[] { -1f, 1f })
            {
                Shape(head, "Leaf", "GearLeaf", "Gold", new Vector3(0.12f * s, 0.11f, 0.16f), new Vector3(0.05f, 0.085f, 0.02f), new Vector3(0f, 35f * s, -18f * s));
                Shape(head, "LeafSmall", "GearLeaf", "Gold", new Vector3(0.188f * s, 0.1f, 0.06f), new Vector3(0.04f, 0.065f, 0.02f), new Vector3(0f, 70f * s, -25f * s));
            }
            Shape(head, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.11f, 0.222f), new Vector3(0.05f, 0.065f, 0.035f));

            Save(root, "sage_circlet");
        }

        // ------------------------------------------------------------------ body armour

        // Socket_Chest is the centre of the torso (0.66 wide, 0.72 tall, 0.36 deep, BodyTorso);
        // a shell of the same shape fits over it. Socket_ShoulderL/R are the shoulder joints, so
        // pads and sleeves there swing with the arms.
        private static void BodyArmours()
        {
            StuddedVest();
            PlateArmour();
            SilkRobe();
        }

        private static void Torso(Transform chest, string mat, Vector3 scale)
        {
            Shape(chest, "Body", "BodyTorso", mat, new Vector3(0f, -0.005f, 0f), scale);
        }

        private static Transform[] Shoulders(GameObject root)
        {
            return new[]
            {
                Node(root, "Socket_ShoulderL", Vector3.zero, Vector3.zero),
                Node(root, "Socket_ShoulderR", Vector3.zero, Vector3.zero)
            };
        }

        // Pale leather jerkin: puffed shoulders over short sleeves, a collar, toggles down the front,
        // dark leaf stitching, a waist band and a flared hem.
        private static void StuddedVest()
        {
            GameObject root = NewRoot("studded_vest");
            Transform chest = Node(root, "Socket_Chest", Vector3.zero, Vector3.zero);

            Torso(chest, "TanLight", new Vector3(0.71f, 0.7f, 0.41f));
            Shape(chest, "Collar", "GearCuff", "Tan", new Vector3(0f, 0.34f, 0f), new Vector3(0.19f, 0.08f, 0.18f));
            Shape(chest, "WaistBand", "GearBand", "Tan", new Vector3(0f, -0.3f, 0f), new Vector3(0.56f, 0.05f, 0.4f));
            Shape(chest, "Hem", "FittedSkirt", "TanLight", new Vector3(0f, -0.42f, 0f), new Vector3(0.62f, 0.16f, 0.44f));
            float[] ys = { 0.15f, 0.02f, -0.12f };
            float[] zs = { 0.207f, 0.208f, 0.183f };
            for (int i = 0; i < ys.Length; i++)
                Cube(chest, "Toggle", "Gold", new Vector3(0f, ys[i], zs[i]), new Vector3(0.07f, 0.025f, 0.02f));
            foreach (float s in new[] { -1f, 1f })
            {
                Shape(chest, "Stitch", "GearLeaf", "TanDark", new Vector3(0.14f * s, 0.12f, 0.2f), new Vector3(0.06f, 0.11f, 0.01f), new Vector3(0f, 18f * s, -20f * s));
                Shape(chest, "Stud", "IcoHead", "Gold", new Vector3(0.21f * s, 0.24f, 0.15f), new Vector3(0.03f, 0.03f, 0.03f));
            }
            Shape(chest, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.29f, 0.165f), new Vector3(0.045f, 0.05f, 0.03f));

            foreach (Transform shoulder in Shoulders(root))
            {
                Shape(shoulder, "ShoulderPuff", "GearDome", "Tan", new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.13f, 0.24f));
                Shape(shoulder, "Sleeve", "GearFlare", "TanLight", new Vector3(0f, -0.1f, 0f), new Vector3(0.26f, 0.16f, 0.27f));
            }

            Save(root, "studded_vest");
        }

        // Polished plate: a breastplate with a ridge, leaf engraving and a gem, a gorget, layered
        // pauldrons trimmed in gold, a steel belt and a flared fauld.
        private static void PlateArmour()
        {
            GameObject root = NewRoot("chain_hauberk");
            Transform chest = Node(root, "Socket_Chest", Vector3.zero, Vector3.zero);

            Torso(chest, "Silver", new Vector3(0.72f, 0.7f, 0.42f));
            Cube(chest, "Ridge", "Steel", new Vector3(0f, 0.04f, 0.208f), new Vector3(0.025f, 0.4f, 0.02f));
            Shape(chest, "Gorget", "GearCuff", "Silver", new Vector3(0f, 0.35f, 0f), new Vector3(0.205f, 0.09f, 0.18f));
            Shape(chest, "Belt", "GearBand", "SteelDark", new Vector3(0f, -0.3f, 0f), new Vector3(0.57f, 0.05f, 0.41f));
            Shape(chest, "Fauld", "FittedSkirt", "Silver", new Vector3(0f, -0.43f, 0f), new Vector3(0.62f, 0.18f, 0.45f));
            Shape(chest, "FauldTrim", "FittedHem", "Gold", new Vector3(0f, -0.515f, 0f), new Vector3(0.63f, 0.02f, 0.46f));
            foreach (float s in new[] { -1f, 1f })
                Shape(chest, "Engraving", "GearLeaf", "Steel", new Vector3(0.12f * s, 0.12f, 0.205f), new Vector3(0.06f, 0.13f, 0.01f), new Vector3(0f, 16f * s, -22f * s));
            Shape(chest, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.22f, 0.205f), new Vector3(0.065f, 0.08f, 0.04f));

            Transform[] shoulders = Shoulders(root);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Shape(shoulders[i], "ShoulderPlate", "GearDome", "Silver", new Vector3(0f, 0.03f, 0f), new Vector3(0.29f, 0.17f, 0.31f));
                Shape(shoulders[i], "ShoulderTrim", "GearBand", "Gold", new Vector3(0f, -0.045f, 0f), new Vector3(0.3f, 0.025f, 0.32f));
                Shape(shoulders[i], "ShoulderLame", "GearDome", "Silver", new Vector3(0.02f * s, -0.08f, 0f), new Vector3(0.24f, 0.12f, 0.27f), new Vector3(0f, 0f, 14f * s));
            }

            Save(root, "chain_hauberk");
        }

        // A long pale robe: a stand collar, wide short sleeves edged in teal, a sash, leaf stitching
        // down the front and a skirt to mid-shin with a teal hem.
        private static void SilkRobe()
        {
            GameObject root = NewRoot("silk_robe");
            Transform chest = Node(root, "Socket_Chest", Vector3.zero, Vector3.zero);

            Torso(chest, "GearCloth", new Vector3(0.7f, 0.7f, 0.4f));
            Shape(chest, "Collar", "GearCuff", "GearCloth", new Vector3(0f, 0.35f, 0f), new Vector3(0.19f, 0.09f, 0.18f));
            Shape(chest, "Skirt", "FittedSkirt", "GearCloth", new Vector3(0f, -0.6f, 0f), new Vector3(0.66f, 0.5f, 0.5f));
            Shape(chest, "Hem", "FittedHem", "GearClothTrim", new Vector3(0f, -0.835f, 0f), new Vector3(0.68f, 0.04f, 0.52f));
            Shape(chest, "Sash", "GearBand", "GearCloth", new Vector3(0f, -0.3f, 0f), new Vector3(0.56f, 0.07f, 0.4f));
            Shape(chest, "SashTrim", "GearBand", "GearClothTrim", new Vector3(0f, -0.335f, 0f), new Vector3(0.565f, 0.015f, 0.405f));
            float[] ys = { 0.18f, 0.04f, -0.1f };
            float[] zs = { 0.205f, 0.21f, 0.188f };
            for (int i = 0; i < ys.Length; i++)
                Shape(chest, "Stitch", "GearLeaf", "GearClothTrim", new Vector3(0f, ys[i], zs[i]), new Vector3(0.05f, 0.08f, 0.01f));

            foreach (Transform shoulder in Shoulders(root))
            {
                Shape(shoulder, "Sleeve", "GearFlare", "GearCloth", new Vector3(0f, -0.11f, 0f), new Vector3(0.29f, 0.25f, 0.31f));
                Shape(shoulder, "SleeveTrim", "GearBand", "GearClothTrim", new Vector3(0f, -0.23f, 0f), new Vector3(0.3f, 0.025f, 0.32f));
            }

            Save(root, "silk_robe");
        }

        // ------------------------------------------------------------------ gloves

        // Socket_HandL/R is the centre of the hand (0.1 x 0.15 x 0.12); the forearm runs up from it
        // (+y) to the elbow at +0.3. The back of each hand faces away from the body (out along x).
        private static void Gloves()
        {
            GloveSet("leather_gloves", "TanLight", "TanLight", "Gold", null);
            GloveSet("iron_gauntlets", "Silver", "Silver", "Gold", "GemGreen");
            GloveSet("silk_gloves", "GearCloth", "GearCloth", "GearClothTrim", "GemGreen");
        }

        private static void GloveSet(string id, string glove, string cuff, string trim, string gem)
        {
            GameObject root = NewRoot(id);
            bool plate = id == "iron_gauntlets";

            foreach (string socket in new[] { "Socket_HandL", "Socket_HandR" })
            {
                float s = socket == "Socket_HandL" ? -1f : 1f;
                Transform hand = LimbNode(root, socket);
                Shape(hand, "Glove", "BodyLimb", glove, new Vector3(0f, -0.01f, 0f), plate ? new Vector3(0.135f, 0.2f, 0.16f) : new Vector3(0.125f, 0.19f, 0.15f));
                Shape(hand, "Cuff", "GearCuff", cuff, new Vector3(0f, 0.14f, 0f), plate ? new Vector3(0.22f, 0.18f, 0.23f) : new Vector3(0.19f, 0.16f, 0.2f));
                Shape(hand, "CuffTrim", "GearBand", trim, plate ? new Vector3(0f, 0.225f, 0f) : new Vector3(0f, 0.075f, 0f),
                    plate ? new Vector3(0.225f, 0.02f, 0.235f) : new Vector3(0.165f, 0.025f, 0.175f));
                if (plate)
                {
                    Shape(hand, "Knuckles", "GearDome", "Silver", new Vector3(0.05f * s, -0.02f, 0f), new Vector3(0.13f, 0.05f, 0.14f), new Vector3(0f, 0f, -90f * s));
                    Shape(hand, "Fingers", "GearBand", "SteelDark", new Vector3(0f, -0.065f, 0f), new Vector3(0.13f, 0.02f, 0.155f));
                }
                else
                {
                    Shape(hand, "Stud", "IcoHead", trim == "Gold" ? "Gold" : "TanDark", new Vector3(0.088f * s, 0.15f, 0.02f), new Vector3(0.025f, 0.025f, 0.025f));
                }
                if (gem != null)
                    Shape(hand, "Gem", "IcoHead", gem, new Vector3((plate ? 0.105f : 0.095f) * s, 0.16f, 0f), new Vector3(0.025f, 0.05f, 0.04f));
            }

            Save(root, id);
        }

        // Footwear is built in knee space: the ankle is at -.38 and the sole at -.43.
        // Dense cross-sections around the ankle let the foot flex without folding a long shin face.
        private static void Boots()
        {
            Meshes["FittedBoot"] = LowPolyMeshes.Save("FittedBoot", LowPolyMeshes.Loft("FittedBoot", 24, 2.2f, false,
                new LowPolyMeshes.Ring(.005f, 0, 0),
                new LowPolyMeshes.Ring(0, .085f, .09f, .012f),
                new LowPolyMeshes.Ring(-.06f, .092f, .10f, 0),
                new LowPolyMeshes.Ring(-.13f, .098f, .106f, -.012f),
                new LowPolyMeshes.Ring(-.20f, .092f, .10f, -.018f),
                new LowPolyMeshes.Ring(-.26f, .081f, .095f, -.013f),
                new LowPolyMeshes.Ring(-.30f, .071f, .088f, -.008f),
                new LowPolyMeshes.Ring(-.33f, .068f, .087f, -.005f),
                new LowPolyMeshes.Ring(-.36f, .075f, .13f, .04f),
                new LowPolyMeshes.Ring(-.39f, .086f, .177f, .055f),
                new LowPolyMeshes.Ring(-.425f, .083f, .175f, .055f),
                new LowPolyMeshes.Ring(-.43f, 0, 0, .055f)));
            BootSet("leather_boots", "TanLight", "TanDark", false);
            BootSet("iron_greaves", "Silver", "Gold", true);
            BootSet("silk_slippers", "GearCloth", "GearClothTrim", false);
        }

        private static void BootSet(string id, string material, string trim, bool plate)
        {
            GameObject root = NewRoot(id);
            foreach (string side in new[] { "L", "R" })
            {
                Transform foot = Node(root, "Socket_Foot" + side, Vector3.zero, Vector3.zero);
                // Cancel the socket's old centre; the mesh itself is measured from the knee.
                Shape(foot, "Boot", "FittedBoot", material, new Vector3(0, .20f, -.02f), Vector3.one);
                Shape(foot, "Cuff", "GearCuff", material, new Vector3(0, .175f, -.012f), new Vector3(.185f, .055f, .197f));
                Shape(foot, "CuffTrim", "GearBand", trim, new Vector3(0, .20f, -.008f), new Vector3(.188f, .014f, .20f));
                if (plate)
                {
                    Cube(foot, "Ridge", "SteelDark", new Vector3(0, .025f, .083f), new Vector3(.014f, .22f, .012f));
                    Shape(foot, "KneeCop", "GearDome", material, new Vector3(0, .16f, .078f), new Vector3(.13f, .04f, .11f), new Vector3(90, 0, 0));
                    Shape(foot, "ToeCap", "GearDome", "SteelDark", new Vector3(0, -.19f, .16f), new Vector3(.145f, .035f, .09f), new Vector3(90, 0, 0));
                }
                else
                {
                    Shape(foot, "Strap", "GearBand", trim, new Vector3(0, .015f, -.035f), new Vector3(.18f, .025f, .195f));
                    Cube(foot, "Buckle", "Gold", new Vector3(side == "L" ? -.09f : .09f, .015f, -.035f), new Vector3(.012f, .03f, .035f));
                }
            }
            Save(root, id);
        }

        // ------------------------------------------------------------------ belt, amulet, rings

        // Socket_Waist is at the waist (torso about 0.25 x 0.15 there, more over body armour): a
        // band with a leaf-shaped buckle in a gold rim and a small gem either side.
        private static void Belt()
        {
            GameObject root = NewRoot("rope_belt");
            Transform waist = Node(root, "Socket_Waist", Vector3.zero, Vector3.zero);
            waist.localScale = new Vector3(.9f, 1f, .87f);

            Shape(waist, "Band", "GearBand", "TanLight", Vector3.zero, new Vector3(0.56f, 0.075f, 0.41f));
            Shape(waist, "BuckleRim", "GearLeaf", "Gold", new Vector3(0f, 0f, 0.205f), new Vector3(0.115f, 0.15f, 0.02f), new Vector3(0f, 0f, 90f));
            Shape(waist, "Buckle", "GearLeaf", "Tan", new Vector3(0f, 0f, 0.212f), new Vector3(0.09f, 0.125f, 0.02f), new Vector3(0f, 0f, 90f));
            Cube(waist, "Vein", "TanDark", new Vector3(0f, 0f, 0.223f), new Vector3(0.1f, 0.006f, 0.006f));
            foreach (float s in new[] { -1f, 1f })
                Shape(waist, "Gem", "IcoHead", "GemGreen", new Vector3(0.09f * s, 0f, 0.198f), new Vector3(0.025f, 0.025f, 0.02f));

            Save(root, "rope_belt");
        }

        // Socket_Neck is at the base of the neck: a chain down to a round pendant on the chest, low
        // enough to show below the starting shoulder cape.
        private static void JadeAmulet()
        {
            GameObject root = NewRoot("jade_amulet");
            Transform neck = Node(root, "Socket_Neck", Vector3.zero, Vector3.zero);

            neck.localScale = new Vector3(.9f, 1f, .91f);

            Vector3 pendant = new Vector3(0f, -0.16f, 0.205f);
            Bar(neck, "ChainL", "Steel", new Vector3(-0.08f, 0.07f, 0.0f), pendant, 0.015f);
            Bar(neck, "ChainR", "Steel", new Vector3(0.08f, 0.07f, 0.0f), pendant, 0.015f);
            Shape(neck, "Back", "GearDisc", "Silver", new Vector3(0f, -0.2f, 0.205f), new Vector3(0.075f, 0.015f, 0.075f), new Vector3(90f, 0f, 0f));
            Shape(neck, "Bezel", "GearBand", "Steel", new Vector3(0f, -0.2f, 0.212f), new Vector3(0.08f, 0.02f, 0.08f), new Vector3(90f, 0f, 0f));
            Shape(neck, "Gem", "BodyHead", "GemGreen", new Vector3(0f, -0.2f, 0.216f), new Vector3(0.055f, 0.065f, 0.03f));

            Save(root, "jade_amulet");
        }

        // Socket_Ring is the hand's centre: a band round the hand (wide enough to show over a glove)
        // with its stone on the back of the hand.
        private static void Ring(string id, string bandMat, string gemMat)
        {
            GameObject root = NewRoot(id);
            Transform ring = LimbNode(root, "Socket_Ring");

            Shape(ring, "Band", "GearBand", bandMat, new Vector3(0f, -0.05f, 0f), new Vector3(0.13f, 0.02f, 0.155f));
            Shape(ring, "Gem", "IcoHead", gemMat, new Vector3(0f, -0.05f, 0.08f), new Vector3(0.03f, 0.03f, 0.022f));

            Save(root, id);
        }

        // ------------------------------------------------------------------ shields

        // Socket_OffHand is just past the off hand. Shields are strapped to the forearm, turned 40
        // degrees outward, face forward (+z) and centred about 0.3 up the arm.
        private static void Shields()
        {
            WoodenShield();
            KiteShield();
            TowerShield();
            Buckler("buckler", false);
            Buckler("spiked_buckler", true);
            SpiritFocus();
        }

        private static Transform ShieldNode(GameObject root)
        {
            return Node(root, "Socket_OffHand", Vector3.zero, new Vector3(0f, -40f, 0f));
        }

        // Leaves set in a ring round a centre, lying flat on a shield's face.
        private static void Wreath(Transform shield, Vector3 centre, float radius, int count, float size, string mat)
        {
            for (int i = 0; i < count; i++)
            {
                float a = 360f * i / count;
                Vector3 p = centre + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * radius, Mathf.Sin(a * Mathf.Deg2Rad) * radius, 0f);
                Shape(shield, "Leaf", "GearLeaf", mat, p, new Vector3(size * 0.55f, size, 0.02f), new Vector3(0f, 0f, a));
            }
        }

        // Round, wooden, rimmed in steel, a carved wreath and a gem in a steel boss.
        private static void WoodenShield()
        {
            GameObject root = NewRoot("wooden_shield");
            Transform shield = ShieldNode(root);

            Vector3 c = new Vector3(0f, 0.32f, 0.15f);
            Shape(shield, "Face", "GearDisc", "ShieldWood", c, new Vector3(0.7f, 0.06f, 0.7f), new Vector3(90f, 0f, 0f));
            Shape(shield, "Rim", "GearBand", "Steel", c, new Vector3(0.74f, 0.08f, 0.74f), new Vector3(90f, 0f, 0f));
            Wreath(shield, c + new Vector3(0f, 0f, 0.035f), 0.22f, 10, 0.1f, "ShieldWoodDark");
            Shape(shield, "Boss", "GearDome", "Steel", c + new Vector3(0f, 0f, 0.055f), new Vector3(0.2f, 0.05f, 0.2f), new Vector3(90f, 0f, 0f));
            Shape(shield, "Gem", "BodyHead", "GemGreen", c + new Vector3(0f, 0f, 0.09f), new Vector3(0.08f, 0.08f, 0.04f));

            Save(root, "wooden_shield");
        }

        // A silver kite with a dark rim, engraved leaves and a green gem near the top.
        private static void KiteShield()
        {
            GameObject root = NewRoot("kite_shield");
            Transform shield = ShieldNode(root);

            Vector3 c = new Vector3(0f, 0.3f, 0.16f);
            Shape(shield, "Rim", "GearKite", "SteelDark", c - new Vector3(0f, 0f, 0.008f), new Vector3(0.56f, 0.8f, 0.5f));
            Shape(shield, "Face", "GearKite", "Silver", c, new Vector3(0.5f, 0.72f, 0.6f));
            foreach (float s in new[] { -1f, 1f })
                Shape(shield, "Leaf", "GearLeaf", "Steel", c + new Vector3(0.09f * s, -0.04f, 0.035f), new Vector3(0.07f, 0.2f, 0.02f), new Vector3(0f, 0f, 22f * s));
            Shape(shield, "Setting", "GearDisc", "Gold", c + new Vector3(0f, 0.09f, 0.032f), new Vector3(0.1f, 0.02f, 0.12f), new Vector3(90f, 0f, 0f));
            Shape(shield, "Gem", "BodyHead", "GemGreen", c + new Vector3(0f, 0.09f, 0.045f), new Vector3(0.07f, 0.09f, 0.04f));

            Save(root, "kite_shield");
        }

        // A tall shield of pale planks with an arched top, studs down the edges and a ring emblem.
        private static void TowerShield()
        {
            GameObject root = NewRoot("tower_shield");
            Transform shield = ShieldNode(root);

            Vector3 c = new Vector3(0f, 0.3f, 0.16f);
            Shape(shield, "Face", "GearTower", "PaleWood", c, new Vector3(0.5f, 0.84f, 0.6f));
            foreach (float x in new[] { -0.125f, 0f, 0.125f })
                Cube(shield, "Seam", "ShieldWoodDark", c + new Vector3(x, -0.02f, 0.031f), new Vector3(0.01f, 0.76f, 0.006f));
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float y in new[] { 0.3f, 0.05f, -0.2f, -0.36f })
                    Shape(shield, "Stud", "IcoHead", "Steel", c + new Vector3(0.2f * s, y, 0.033f), new Vector3(0.03f, 0.03f, 0.02f));
            }
            Shape(shield, "Emblem", "GearBand", "Steel", c + new Vector3(0f, 0.06f, 0.035f), new Vector3(0.16f, 0.02f, 0.16f), new Vector3(90f, 0f, 0f));

            Save(root, "tower_shield");
        }

        // A small round metal shield: leaf filigree round a domed boss and gem; the spiked one has
        // spikes round the rim and one at the centre.
        private static void Buckler(string id, bool spiked)
        {
            GameObject root = NewRoot(id);
            Transform shield = ShieldNode(root);

            Vector3 c = new Vector3(0f, 0.3f, 0.15f);
            Shape(shield, "Face", "GearDisc", "Silver", c, new Vector3(0.46f, 0.05f, 0.46f), new Vector3(90f, 0f, 0f));
            Shape(shield, "Rim", "GearBand", "SteelDark", c, new Vector3(0.49f, 0.065f, 0.49f), new Vector3(90f, 0f, 0f));
            Wreath(shield, c + new Vector3(0f, 0f, 0.03f), 0.14f, 6, 0.08f, "SteelDark");
            Shape(shield, "Boss", "GearDome", "Silver", c + new Vector3(0f, 0f, 0.045f), new Vector3(0.16f, 0.05f, 0.16f), new Vector3(90f, 0f, 0f));
            if (spiked)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = 360f * i / 8f;
                    Vector3 p = c + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.23f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.23f, 0f);
                    Shape(shield, "Spike", "Cone", "Silver", p, new Vector3(0.03f, 0.09f, 0.03f), new Vector3(0f, 0f, a - 90f));
                }
                Shape(shield, "CentreSpike", "Cone", "Silver", c + new Vector3(0f, 0f, 0.07f), new Vector3(0.035f, 0.1f, 0.035f), new Vector3(90f, 0f, 0f));
            }
            else
            {
                Shape(shield, "Gem", "BodyHead", "GemGreen", c + new Vector3(0f, 0f, 0.08f), new Vector3(0.07f, 0.07f, 0.04f));
            }

            Save(root, id);
        }

        // A spellcaster's focus: a jagged crystal with two smaller shards on a short wrapped grip,
        // held up like a wand in the off hand.
        private static void SpiritFocus()
        {
            GameObject root = NewRoot("spirit_focus");
            Transform focus = Node(root, "Socket_OffHand", new Vector3(0f, 0.1f, 0.03f), new Vector3(30f, 0f, 0f));

            Shape(focus, "Grip", "BodyLimb", "Leather", new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.18f, 0.05f));
            Shape(focus, "Collar", "GearBand", "Gold", new Vector3(0f, 0.15f, 0f), new Vector3(0.075f, 0.03f, 0.075f));
            Shape(focus, "Crystal", "IcoHead", "Crystal", new Vector3(0f, 0.3f, 0f), new Vector3(0.11f, 0.3f, 0.11f), new Vector3(0f, 25f, 0f));
            Shape(focus, "ShardA", "IcoHead", "Crystal", new Vector3(0.055f, 0.22f, 0.02f), new Vector3(0.05f, 0.15f, 0.05f), new Vector3(0f, 0f, -28f));
            Shape(focus, "ShardB", "IcoHead", "Crystal", new Vector3(-0.045f, 0.24f, -0.03f), new Vector3(0.045f, 0.13f, 0.045f), new Vector3(10f, 0f, 24f));

            Save(root, "spirit_focus");
        }

        // ------------------------------------------------------------------ weapons

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

        // A thick tome bound in red-brown leather, held at the side: bone-white page edges, gold corners and
        // clasp, a green gem set in the cover.
        private static void Grimoire()
        {
            GameObject root = NewRoot("grimoire");
            Transform book = Node(root, "Socket_OffHand", Vector3.zero, new Vector3(0f, -40f, 0f));

            Cube(book, "Pages", "BowString", new Vector3(0f, 0.24f, 0.14f), new Vector3(0.30f, 0.40f, 0.10f));
            Cube(book, "CoverFront", "BookLeather", new Vector3(0f, 0.24f, 0.20f), new Vector3(0.34f, 0.44f, 0.025f));
            Cube(book, "CoverBack", "BookLeather", new Vector3(0f, 0.24f, 0.08f), new Vector3(0.34f, 0.44f, 0.025f));
            Cube(book, "Spine", "BookLeather", new Vector3(-0.16f, 0.24f, 0.14f), new Vector3(0.04f, 0.44f, 0.14f));
            Cube(book, "Band", "Gold", new Vector3(-0.16f, 0.36f, 0.14f), new Vector3(0.045f, 0.03f, 0.145f));
            Cube(book, "Band2", "Gold", new Vector3(-0.16f, 0.12f, 0.14f), new Vector3(0.045f, 0.03f, 0.145f));
            Cube(book, "Clasp", "Gold", new Vector3(0.17f, 0.24f, 0.14f), new Vector3(0.03f, 0.07f, 0.15f));
            Cube(book, "CornerA", "Gold", new Vector3(0.15f, 0.44f, 0.21f), new Vector3(0.05f, 0.05f, 0.01f));
            Cube(book, "CornerB", "Gold", new Vector3(0.15f, 0.04f, 0.21f), new Vector3(0.05f, 0.05f, 0.01f));
            Shape(book, "Sigil", "IcoHead", "TanLight", new Vector3(0f, 0.26f, 0.215f), new Vector3(0.12f, 0.13f, 0.02f));
            Shape(book, "Gem", "IcoHead", "GemGreen", new Vector3(0f, 0.26f, 0.225f), new Vector3(0.06f, 0.06f, 0.03f));

            Save(root, "grimoire");
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

        /// <summary>The great (two-handed) weapons, leaving the other prefabs untouched.</summary>
        [MenuItem("PoeClone/Build Great Weapons")]
        public static void BuildVariety()
        {
            EnsureFolder(OutDir);
            LoadMeshes();
            MakeMaterials();
            VarietyModels();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("EquipmentBuilder: built the great weapon prefabs");
        }

        private static void VarietyModels()
        {
            BastardSword();
            Woodsplitter();
            GreatMallet();
            Sledgehammer();
        }

        // A war hammer: a round striking face on one side of the head, a curved pick on the other,
        // a gold collar and a gem where the head meets the haft.
        private static void GreatMallet()
        {
            GameObject root = NewRoot("great_mallet");
            Transform maul = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), GreatGrip);

            Cube(maul, "Haft", "PaleWood", new Vector3(0f, 0.42f, 0f), new Vector3(0.07f, 1.28f, 0.07f));
            Cube(maul, "Wrap", "Leather", new Vector3(0f, 0.08f, 0f), new Vector3(0.08f, 0.34f, 0.08f));
            Shape(maul, "Pommel", "IcoHead", "Gold", new Vector3(0f, -0.24f, 0f), new Vector3(0.1f, 0.1f, 0.1f));
            Shape(maul, "Collar", "GearBand", "Gold", new Vector3(0f, 1.02f, 0f), new Vector3(0.11f, 0.1f, 0.11f));
            Cube(maul, "Head", "Silver", new Vector3(0.02f, 1.12f, 0f), new Vector3(0.2f, 0.17f, 0.15f));
            Shape(maul, "Face", "GearDisc", "Silver", new Vector3(0.15f, 1.12f, 0f), new Vector3(0.22f, 0.12f, 0.22f), new Vector3(0f, 0f, 90f));
            Shape(maul, "FaceRim", "GearBand", "Steel", new Vector3(0.2f, 1.12f, 0f), new Vector3(0.235f, 0.03f, 0.235f), new Vector3(0f, 0f, 90f));
            Shape(maul, "Pick", "Cone", "Silver", new Vector3(-0.07f, 1.12f, 0f), new Vector3(0.06f, 0.3f, 0.045f), new Vector3(0f, 0f, 100f));
            Shape(maul, "Gem", "IcoHead", "GemGreen", new Vector3(0.02f, 1.12f, 0.08f), new Vector3(0.06f, 0.06f, 0.04f));

            Save(root, "great_mallet");
        }

        // A plain sledge: a heavy steel block with banded faces and a spike on top, on a long haft.
        private static void Sledgehammer()
        {
            GameObject root = NewRoot("sledgehammer");
            Transform maul = Node(root, "Socket_MainHand", new Vector3(0f, 0f, 0.03f), GreatGrip);

            Cube(maul, "Haft", "PaleWood", new Vector3(0f, 0.42f, 0f), new Vector3(0.075f, 1.28f, 0.075f));
            Cube(maul, "Wrap", "Leather", new Vector3(0f, 0.08f, 0f), new Vector3(0.085f, 0.34f, 0.085f));
            Shape(maul, "Pommel", "IcoHead", "Steel", new Vector3(0f, -0.24f, 0f), new Vector3(0.11f, 0.11f, 0.11f));
            Cube(maul, "Head", "Silver", new Vector3(0f, 1.12f, 0f), new Vector3(0.5f, 0.26f, 0.26f));
            Cube(maul, "FaceR", "Steel", new Vector3(0.26f, 1.12f, 0f), new Vector3(0.05f, 0.31f, 0.31f));
            Cube(maul, "FaceL", "Steel", new Vector3(-0.26f, 1.12f, 0f), new Vector3(0.05f, 0.31f, 0.31f));
            Shape(maul, "Spike", "Cone", "Steel", new Vector3(0f, 1.24f, 0f), new Vector3(0.05f, 0.16f, 0.05f));

            Save(root, "sledgehammer");
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

        private static void WeaponModels()
        {
            HandAxe();
            IronMace();
            SteelDagger();
            ShortBow("short_bow", "Silver");
            ShortBow("crude_bow", "ShieldWood");
            LeatherQuiver();
        }

        // Held in the off (left) hand, like a real archer: the right hand draws. Built upright along
        // +y with the belly towards +z, then turned so that with the arm raised at the target (the
        // bow attack) the bow stands upright, belly to the target and string to the archer. With
        // the arm hanging at rest that leaves it carried level at the hip, pointing forward.
        private static void ShortBow(string id, string limbs)
        {
            GameObject root = NewRoot(id);
            // The grip sits at model z=0.22; offset it onto the palm after the 90-degree turn.
            Transform bow = Node(root, "Socket_OffHand", new Vector3(0f, 0.32f, -0.05f), new Vector3(90f, 0f, 0f));

            Cube(bow, "Grip", "Leather", new Vector3(0f, 0f, 0.22f), new Vector3(0.06f, 0.16f, 0.06f));
            Bar(bow, "UpperInner", limbs, new Vector3(0f, 0.07f, 0.22f), new Vector3(0f, 0.30f, 0.16f), 0.05f);
            Bar(bow, "UpperOuter", limbs, new Vector3(0f, 0.30f, 0.16f), new Vector3(0f, 0.52f, 0f), 0.04f);
            Bar(bow, "LowerInner", limbs, new Vector3(0f, -0.07f, 0.22f), new Vector3(0f, -0.30f, 0.16f), 0.05f);
            Bar(bow, "LowerOuter", limbs, new Vector3(0f, -0.30f, 0.16f), new Vector3(0f, -0.52f, 0f), 0.04f);
            Shape(bow, "TipTop", "Cone", "Gold", new Vector3(0f, 0.55f, 0f), new Vector3(0.03f, 0.07f, 0.03f));
            Shape(bow, "TipBottom", "Cone", "Gold", new Vector3(0f, -0.55f, 0f), new Vector3(0.03f, 0.07f, 0.03f), new Vector3(180f, 0f, 0f));
            Bar(bow, "String", "BowString", new Vector3(0f, 0.52f, 0f), new Vector3(0f, -0.52f, 0f), 0.01f);
            Shape(bow, "GemTop", "IcoHead", "GemGreen", new Vector3(0f, 0.14f, 0.22f), new Vector3(0.05f, 0.05f, 0.04f));
            Shape(bow, "GemBottom", "IcoHead", "GemGreen", new Vector3(0f, -0.14f, 0.22f), new Vector3(0.05f, 0.05f, 0.04f));

            Save(root, id);
        }

        // Worn on the back, tilted over the right shoulder so the arrows are within the drawing
        // hand's reach: a leather tube with gold bands, fletched arrows standing out of the top.
        private static void LeatherQuiver()
        {
            GameObject root = NewRoot("leather_quiver");
            Transform quiver = Node(root, "Socket_Chest", new Vector3(0.06f, 0.05f, -0.31f), new Vector3(0f, 0f, -22f));

            Shape(quiver, "Tube", "GearDisc", "Leather", new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.6f, 0.2f));
            Shape(quiver, "Bottom", "GearDisc", "TanDark", new Vector3(0f, -0.25f, 0f), new Vector3(0.215f, 0.04f, 0.215f));
            Shape(quiver, "BandTop", "GearBand", "Gold", new Vector3(0f, 0.32f, 0f), new Vector3(0.215f, 0.03f, 0.215f));
            Shape(quiver, "BandMid", "GearBand", "Gold", new Vector3(0f, -0.05f, 0f), new Vector3(0.21f, 0.025f, 0.21f));

            float[] xs = { -0.05f, 0.0f, 0.05f };
            float[] zs = { 0.02f, -0.03f, 0.02f };
            for (int k = 0; k < 3; k++)
            {
                Cube(quiver, "Shaft" + k, "PaleWood", new Vector3(xs[k], 0.42f, zs[k]), new Vector3(0.018f, 0.24f, 0.018f));
                Cube(quiver, "Fletch" + k, k == 1 ? "GemRed" : "BowString", new Vector3(xs[k], 0.52f, zs[k]), new Vector3(0.05f, 0.09f, 0.012f));
            }

            Save(root, "leather_quiver");
        }
    }
}
