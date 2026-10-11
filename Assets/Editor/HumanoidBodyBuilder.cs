using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PoeClone.Inventory;

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
            Dictionary<string, Mesh> m = LowPolyMeshes.BuildShared();
            Mesh limb = m["BodyLimb"], head = m["BodyHead"];

            foreach (string side in new[] { "L", "R" })
            {
                float s = side == "L" ? -1f : 1f;

                // Legs: a sturdy thigh tapering to the knee.
                Transform leg = model.Find("Leg" + side);
                leg.localPosition = new Vector3(0.15f * s, 0.85f, 0f);
                Part(leg, "Thigh", limb, new Vector3(0f, -0.21f, 0f), new Vector3(0.26f, 0.5f, 0.27f));
                Transform knee = leg.Find("Knee");
                Part(knee, "Shin", limb, new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.44f, 0.21f));
                Part(knee, "Foot", head, new Vector3(0f, -0.38f, 0.06f), new Vector3(0.175f, 0.1f, 0.33f));
                Part(knee, "Boot", m["BodyBoot"], new Vector3(0f, -0.2f, 0.02f), new Vector3(0.24f, 0.46f, 0.37f));
                Part(knee, "BootCuff", limb, new Vector3(0f, 0.02f, -0.01f), new Vector3(0.27f, 0.08f, 0.27f));

                // Arms: shoulders set in to the narrower chest, a solid upper arm and forearm.
                // The elbow and the hand/weapon sockets keep their places, so held items don't move.
                Transform arm = model.Find("UpperBody/UpperOffset/Arm" + side);
                arm.localPosition = new Vector3(0.4f * s, 1.5f, 0f);
                Part(arm, "Upper", limb, new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.48f, 0.21f));
                Transform elbow = arm.Find("Elbow");
                Part(elbow, "Forearm", limb, new Vector3(0f, -0.13f, 0f), new Vector3(0.17f, 0.32f, 0.18f));
                Part(elbow, "Hand", limb, new Vector3(0f, -0.3f, 0f), new Vector3(0.125f, 0.15f, 0.15f));

                // Shoulder pads and sleeves hang on the shoulder joint, so they swing with the arm.
                Socket(arm, "Socket_Shoulder" + side, Vector3.zero);
            }

            Transform u = model.Find("UpperBody/UpperOffset");
            Part(u, "Torso", m["BodyTorso"], new Vector3(0f, 1.22f, 0f), new Vector3(0.66f, 0.72f, 0.36f));
            Part(u, "TunicSkirt", m["BodySkirt"], new Vector3(0f, 0.76f, 0f), new Vector3(0.6f, 0.42f, 0.44f));
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
            // Helmets are built round the head's centre.
            Socket(u, "Socket_Head", new Vector3(0f, 1.8f, 0.03f));

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
                Part(u, "Hair", head, new Vector3(0f, 1.85f, 0.01f), new Vector3(0.37f, 0.33f, 0.39f));

                // The face, in flat bold marks that read from a distance: thick brows angled down
                // towards the nose, small dark eyes and a mouth line. Low enough to stay clear of
                // the hood's rim and of the hair once the hood comes off.
                Material skin = headPart.GetComponent<Renderer>().sharedMaterial;
                Material brow = Tinted(u.Find("Hair").GetComponent<Renderer>().sharedMaterial, "Brow", new Color(0.14f, 0.09f, 0.06f));
                foreach (string side in new[] { "L", "R" })
                {
                    float s = side == "L" ? -1f : 1f;
                    FacePart(u, headPart, "Brow" + side, brow, 0.064f * s, 1.79f, 0.002f, new Vector3(0.08f, 0.022f, 0.03f), -10f * s);
                    FacePart(u, headPart, "Eye" + side, null, 0.06f * s, 1.764f, 0.001f, new Vector3(0.03f, 0.02f, 0.02f), 0f);
                }
                FacePart(u, headPart, "Mouth", Tinted(skin, "Lip", new Color(0.45f, 0.2f, 0.16f)), 0f, 1.675f, 0.002f, new Vector3(0.07f, 0.016f, 0.02f), 0f);

                // Bare feet would poke out of the front of worn boots.
                foreach (string side in new[] { "L", "R" })
                {
                    Transform foot = model.Find("Leg" + side + "/Knee/Foot");
                    if (foot != null)
                        (foot.GetComponent<BaseGear>() ?? foot.gameObject.AddComponent<BaseGear>()).HiddenBy = EquipSlot.Boots;
                }

                // Starting clothes: a soft cap with a brim, a short shoulder cape over a long back
                // panel, and one round steel pauldron on the left shoulder.
                Part(u, "Hood", head, new Vector3(0f, 1.87f, 0f), new Vector3(0.47f, 0.33f, 0.48f));
                Part(u, "HoodBrim", m["GearBand"], new Vector3(0f, 1.9f, 0.01f), new Vector3(0.5f, 0.035f, 0.5f));
                Part(u, "Mantle", m["GearFlare"], new Vector3(0f, 1.47f, -0.02f), new Vector3(0.9f, 0.2f, 0.46f));
                Part(u, "CloakBack", m["BodySkirt"], new Vector3(0f, 1.0f, -0.21f), new Vector3(0.66f, 1.12f, 0.07f));
                Transform pauldron = u.Find("Pauldron") ?? model.Find("UpperBody/UpperOffset/ArmL/Pauldron");
                if (pauldron != null)
                {
                    pauldron.SetParent(model.Find("UpperBody/UpperOffset/ArmL"), false);
                    pauldron.localRotation = Quaternion.identity;
                    Part(pauldron.parent, "Pauldron", m["GearDome"], new Vector3(0f, 0.03f, 0f), new Vector3(0.22f, 0.14f, 0.24f));
                }
            }
        }

        // A small mark on the face (null material keeps the part's own), sitting on the head's
        // surface at x, y and turned with it, made from a copy of the head if the rig lacks it.
        private static void FacePart(Transform u, Transform head, string name, Material mat, float x, float y, float lift, Vector3 scale, float roll)
        {
            Transform t = u.Find(name);
            if (t == null)
            {
                t = Object.Instantiate(head.gameObject, u).transform;
                t.name = name;
            }
            if (mat != null)
                t.GetComponent<Renderer>().sharedMaterial = mat;
            t.GetComponent<MeshFilter>().sharedMesh = head.GetComponent<MeshFilter>().sharedMesh;

            Vector3 c = head.localPosition, r = head.localScale * 0.5f;
            float dx = x / r.x, dy = (y - c.y) / r.y;
            float z = c.z + r.z * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx - dy * dy));
            t.localPosition = new Vector3(x, y, z + lift);
            t.localRotation = Quaternion.Euler(0f, Mathf.Asin(Mathf.Clamp(dx, -1f, 1f)) * Mathf.Rad2Deg * 0.8f, roll);
            t.localScale = scale;
        }

        // A flat-coloured copy of a body material, saved beside it.
        private static Material Tinted(Material from, string name, Color c)
        {
            string path = "Assets/Materials/Stylized/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(from);
                AssetDatabase.CreateAsset(m, path);
            }
            m.CopyPropertiesFromMaterial(from);
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_ShadowColor"))
            {
                m.SetColor("_ShadowColor", new Color(c.r * 0.5f, c.g * 0.5f, Mathf.Min(1f, c.b * 0.62f), 1f));
                m.SetColor("_RimColor", Color.Lerp(c, Color.white, 0.6f));
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // An empty attachment point for gear, created if the rig doesn't have it yet.
        private static void Socket(Transform parent, string name, Vector3 position)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            t.localPosition = position;
            t.localRotation = Quaternion.identity;
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
    }
}
