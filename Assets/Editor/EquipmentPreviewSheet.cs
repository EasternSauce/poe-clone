using System.Collections.Generic;
using System.IO;
using PoeClone.Inventory;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Renders the character wearing gear, to PNGs, without Play mode: a copy of the scene's
    /// Player model (or the enemy prefab's) dressed through <see cref="EquipmentVisuals"/> itself,
    /// so the pictures show exactly what the game attaches. For comparing worn gear with its icon.
    /// From code: EquipmentPreviewSheet.Render("iron_helmet,chain_hauberk+iron_gauntlets", "C:/out").
    /// </summary>
    public static class EquipmentPreviewSheet
    {
        /// <summary>
        /// One picture per comma-separated set and view; "+" joins items worn together. Views are
        /// yaw angles round the character (0 = from the front); height and distance frame the camera
        /// (1.8 and 1.4 for a close look at the head). Returns the files written.
        /// </summary>
        public static string Render(string sets, string outDir, string views = "25,205", int size = 360, bool enemy = false,
            float height = 1.05f, float distance = 4.6f)
        {
            Transform source = SourceModel(enemy);
            if (source == null)
                return "no source model (open the Game scene)";
            Directory.CreateDirectory(outDir);

            var preview = new PreviewRenderUtility();
            var written = new List<string>();
            try
            {
                Camera cam = preview.camera;
                cam.fieldOfView = 26f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 30f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.22f, 0.23f, 0.27f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                preview.lights[1].intensity = 0.7f;
                preview.lights[1].transform.rotation = Quaternion.Euler(20f, 160f, 0f);
                preview.ambientColor = new Color(0.4f, 0.4f, 0.45f);

                foreach (string set in sets.Split(','))
                {
                    GameObject model = Object.Instantiate(source.gameObject);
                    model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    preview.AddSingleGO(model);
                    Dress(model, set.Split('+'));

                    foreach (string view in views.Split(','))
                    {
                        float yaw = float.Parse(view) * Mathf.Deg2Rad;
                        Vector3 target = new Vector3(0f, height, 0f);
                        cam.transform.position = target + new Vector3(Mathf.Sin(yaw) * distance, distance * 0.2f, Mathf.Cos(yaw) * distance);
                        cam.transform.LookAt(target);

                        preview.BeginStaticPreview(new Rect(0f, 0f, size, size));
                        preview.Render(true);
                        Texture2D tex = preview.EndStaticPreview();
                        string file = Path.Combine(outDir, set.Replace('+', '_') + "_" + view.Replace("-", "m") + ".png");
                        File.WriteAllBytes(file, tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                        written.Add(file);
                    }
                    Object.DestroyImmediate(model);
                }
            }
            finally
            {
                preview.Cleanup();
            }
            return written.Count + " images in " + outDir;
        }

        private static Transform SourceModel(bool enemy)
        {
            if (enemy)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
                return prefab != null ? prefab.transform.Find("Model") : null;
            }
            GameObject player = GameObject.Find("Player");
            return player != null ? player.transform.Find("Model") : null;
        }

        private static void Dress(GameObject model, string[] ids)
        {
            var set = new EquipmentSet();
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || id == "none")
                    continue;
                ItemData item = ItemGenerator.Display(id, null, ItemRarity.Normal);
                if (item == null)
                {
                    Debug.LogWarning("EquipmentPreviewSheet: no item " + id);
                    continue;
                }
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    if (set.Get(slot) == null && set.TryEquip(slot, item, out _))
                        break;
                }
            }

            EquipmentVisuals visuals = model.GetComponent<EquipmentVisuals>() ?? model.AddComponent<EquipmentVisuals>();
            visuals.Bind(set);
            visuals.Unbind();
        }
    }
}
