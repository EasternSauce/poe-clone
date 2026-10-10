using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.World;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// PoeClone > Build Area Kit: gathers the stylized prefabs, meshes and materials the runtime
    /// WorldBuilder uses into Resources/AreaKit.asset, and makes the few extra themed materials
    /// (tombstone stone, dead wood, embers, market cloth...) as copies of existing ones, so they
    /// share the project's stylized shader. Re-runnable.
    /// </summary>
    public static class AreaKitBuilder
    {
        private const string MatDir = "Assets/Materials/Stylized";
        private const string MeshDir = "Assets/Meshes/Stylized";
        private const string PrefabDir = "Assets/Prefabs/Environment";
        private const string KitPath = "Assets/Resources/AreaKit.asset";

        // Existing materials the kit hands out by name.
        private static readonly string[] Existing =
        {
            "Bark", "PineA", "PineB", "OakA", "OakB", "Rock", "RockDark", "Wall", "Roof", "Wood", "Stone",
            "Gold", "Steel", "SteelDark", "Bone", "Leather", "Tan", "TanDark", "Rope", "Cloth", "GroundStylized"
        };

        // New themed materials: (name, copied from, colour).
        private static readonly (string name, string from, string hex)[] Themed =
        {
            ("Tombstone", "Stone", "#7d7f86"),
            ("TombstoneDark", "Stone", "#55575e"),
            ("DeadWood", "Bark", "#3b3530"),
            ("Moss", "OakA", "#3f5236"),
            ("Ember", "Gold", "#ff7a1f"),
            ("Lava", "Gold", "#e8451a"),
            ("Ash", "Rock", "#4a4440"),
            ("Charred", "Bark", "#241f1c"),
            ("Sandstone", "Stone", "#b59a72"),
            ("ClothRed", "Cloth", "#9a2f2a"),
            ("ClothBlue", "Cloth", "#2f4f9a"),
            ("ClothGreen", "Cloth", "#3f7a3a"),
            ("ClothYellow", "Cloth", "#c9a43a"),
            ("Lantern", "Gold", "#ffd36a"),
            ("Water", "Steel", "#4d7fa8"),
            ("Iron", "SteelDark", "#3a3c42"),
            ("Candle", "Bone", "#efe4c4"),
            ("Pumpkin", "Gold", "#d86e1e"),
            ("Ice", "Steel", "#9fd3ee"),
            ("Snow", "Stone", "#e9eff4"),
            ("Thatch", "Bark", "#a3854a"),
            ("ThatchDark", "Bark", "#8a7042"),
            ("RoofDark", "Roof", "#5c2a21"),
            ("Slate", "Stone", "#5a6372"),
            ("SlateDark", "Stone", "#3e4552"),
            ("Plaster", "Bone", "#d8cbab"),
        };

        [MenuItem("PoeClone/Build Area Kit")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AreaKit kit = AssetDatabase.LoadAssetAtPath<AreaKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<AreaKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            kit.pine = Prefab("Pine");
            kit.oak = Prefab("Oak");
            kit.bush = Prefab("Bush");
            kit.bushSmall = Prefab("BushSmall");
            kit.rock = Prefab("Rock");
            kit.rockSmall = Prefab("RockSmall");
            kit.house = Prefab("House");
            kit.pillar = Prefab("Pillar");

            kit.cone = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Cone.asset");
            kit.cylinder = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Cylinder.asset");
            kit.icoHead = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/IcoHead.asset");

            var materials = new List<AreaKit.NamedMaterial>();
            foreach (string name in Existing)
                materials.Add(new AreaKit.NamedMaterial { name = name, material = Mat(name) });

            foreach (var (name, from, hex) in Themed)
            {
                string path = MatDir + "/" + name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Mat(from));
                    AssetDatabase.CreateAsset(m, path);
                }

                ColorUtility.TryParseHtmlString(hex, out Color c);
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", c);
                EditorUtility.SetDirty(m);
                materials.Add(new AreaKit.NamedMaterial { name = name, material = m });
            }

            kit.materials = materials.ToArray();
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log("AreaKitBuilder: area kit written to " + KitPath + " with " + kit.materials.Length + " materials");
        }

        private static GameObject Prefab(string name)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + name + ".prefab");
            if (p == null)
                Debug.LogError("AreaKitBuilder: missing prefab " + name);
            return p;
        }

        private static Material Mat(string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");
            if (m == null)
                Debug.LogError("AreaKitBuilder: missing material " + name);
            return m;
        }
    }
}
