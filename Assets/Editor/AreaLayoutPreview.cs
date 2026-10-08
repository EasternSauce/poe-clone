using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PoeClone.World;

namespace PoeClone.EditorTools
{
    /// <summary>Inspect authored geometry without entering Play or altering the scene.</summary>
    public sealed class AreaLayoutPreview : EditorWindow
    {
        private int area;
        private Texture2D preview;
        private string report;
        private const int Resolution = 360;

        [MenuItem("PoeClone/World/Area Layout Preview")]
        public static void Open() { GetWindow<AreaLayoutPreview>("Area Layouts"); }

        private void OnDisable() { if (preview != null) DestroyImmediate(preview); }

        private void OnGUI()
        {
            int selected = EditorGUILayout.Popup("Area", area, WorldBuilder.AreaNames);
            if (selected != area || preview == null)
            {
                area = selected;
                if (preview != null) DestroyImmediate(preview);
                preview = Render(area);
            }
            AreaShape shape = AreaLayouts.Create(area, Vector3.zero);
            EditorGUILayout.LabelField($"Bounds: {shape.Size.x} × {shape.Size.y} m | {(shape.IsCave ? "Cave" : shape.IsCliff ? "Cliffs" : "Open terrain")}");
            EditorGUILayout.HelpBox("Colored ground = walkable, teal = water/ice, brown = bridges, dark = inaccessible, purple = gates, blue = spawn/waystone, gold = existing boss site, orange = existing quest props. Edit AreaLayouts.cs to author rooms, routes and holes.", MessageType.Info);
            Rect rect = GUILayoutUtility.GetAspectRect(shape.Size.x / shape.Size.y);
            GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill);
            if (rect.Contains(Event.current.mousePosition))
            {
                Vector2 mouse = Event.current.mousePosition;
                float x = (mouse.x - rect.x) / rect.width * shape.Size.x - shape.Size.x * 0.5f;
                float z = (1f - (mouse.y - rect.y) / rect.height) * shape.Size.y - shape.Size.y * 0.5f;
                GUI.Label(new Rect(rect.x + 8, rect.y + 8, 250, 22), $"Local: ({x:F1}, {z:F1})");
                Repaint();
            }
            if (GUILayout.Button("Check layout connectivity and placements")) report = ValidateAll();
            if (GUILayout.Button("Export all previews to Library/AreaLayoutPreviews")) report = ExportAll();
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
        }

        public static Texture2D Render(int area)
        {
            AreaShape shape = AreaLayouts.Create(area, Vector3.zero);
            int height = Mathf.RoundToInt(Resolution * shape.Size.y / shape.Size.x);
            var texture = new Texture2D(Resolution, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[Resolution * height];
            Color32 open = WorldBuilder.AreaColor(area);
            Color32 outside = new Color32(19, 23, 28, 255);
            for (int z = 0; z < height; z++)
                for (int x = 0; x < Resolution; x++)
                {
                    Vector3 p = new Vector3((x + 0.5f) / Resolution * shape.Size.x - shape.Size.x * 0.5f,
                        0, (z + 0.5f) / height * shape.Size.y - shape.Size.y * 0.5f);
                    pixels[z * Resolution + x] = shape.WaterDistance(p) >= 0f ?
                        (shape.Contains(p) && shape.IsBridge(p) ? new Color32(169, 122, 69, 255) :
                        area == WorldBuilder.Frozen ? new Color32(102, 166, 196, 255) : new Color32(31, 92, 120, 255)) :
                        shape.Contains(p) ? open : outside;
                }
            texture.SetPixels32(pixels);
            Dot(texture, shape, new Vector3(0, 0, area == WorldBuilder.ActArena ? -31 : -6), Color.cyan);
            if (area != WorldBuilder.ActArena) Dot(texture, shape, new Vector3(0, 0, -11), Color.blue);
            if (area != WorldBuilder.ActArena)
            {
                Dot(texture, shape, AreaLayouts.GateLocal(area, false), new Color(0.9f, 0.4f, 1));
                Dot(texture, shape, AreaLayouts.GateLocal(area, true), new Color(0.9f, 0.4f, 1));
            }
            if (area == WorldBuilder.Graveyard || area == WorldBuilder.Ruins || area == WorldBuilder.Frozen)
                Dot(texture, shape, AreaLayouts.BossLocal(area), Color.yellow);
            foreach (string group in AreaLayouts.QuestGroups(area))
                foreach (Vector3 anchor in AreaLayouts.QuestAnchors[group])
                    Dot(texture, shape, shape.NearestOpen(anchor, 5f), new Color(1f, 0.5f, 0.1f));
            texture.Apply();
            return texture;
        }

        private static void Dot(Texture2D texture, AreaShape shape, Vector3 local, Color color)
        {
            int px = Mathf.RoundToInt((local.x / shape.Size.x + 0.5f) * texture.width);
            int pz = Mathf.RoundToInt((local.z / shape.Size.y + 0.5f) * texture.height);
            for (int z = -3; z <= 3; z++)
                for (int x = -3; x <= 3; x++)
                    if (px + x >= 0 && pz + z >= 0 && px + x < texture.width && pz + z < texture.height)
                        texture.SetPixel(px + x, pz + z, color);
        }

        public static string ExportAll()
        {
            string directory = Path.GetFullPath("Library/AreaLayoutPreviews");
            Directory.CreateDirectory(directory);
            for (int area = 0; area < WorldBuilder.AreaNames.Length; area++)
            {
                Texture2D texture = Render(area);
                File.WriteAllBytes(Path.Combine(directory, area + ".png"), texture.EncodeToPNG());
                DestroyImmediate(texture);
            }
            return directory;
        }

        public static string ValidateAll()
        {
            var report = new List<string>();
            for (int area = 0; area < WorldBuilder.AreaNames.Length; area++)
            {
                AreaShape shape = AreaLayouts.Create(area, Vector3.zero);
                const float step = 2f;
                int width = Mathf.CeilToInt(shape.Size.x / step), height = Mathf.CeilToInt(shape.Size.y / step);
                var cells = new bool[width * height];
                var seen = new bool[cells.Length];
                int open = 0, components = 0;
                for (int z = 0; z < height; z++)
                    for (int x = 0; x < width; x++)
                    {
                        Vector3 p = new Vector3((x + 0.5f) * step - shape.Size.x * 0.5f, 0, (z + 0.5f) * step - shape.Size.y * 0.5f);
                        if (shape.Contains(p, 0.8f)) { cells[z * width + x] = true; open++; }
                    }
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!cells[i] || seen[i]) continue;
                    components++;
                    var queue = new Queue<int>(); queue.Enqueue(i); seen[i] = true;
                    while (queue.Count > 0)
                    {
                        int at = queue.Dequeue(), x = at % width, z = at / width;
                        foreach (int next in new[] { x > 0 ? at - 1 : -1, x + 1 < width ? at + 1 : -1, z > 0 ? at - width : -1, z + 1 < height ? at + width : -1 })
                            if (next >= 0 && cells[next] && !seen[next]) { seen[next] = true; queue.Enqueue(next); }
                    }
                }
                bool placements = shape.Contains(new Vector3(0, 0, -6), 2) && shape.Contains(new Vector3(0, 0, -11), 3);
                if (area != WorldBuilder.ActArena)
                    placements &= shape.Contains(AreaLayouts.GateLocal(area, false), 5) && shape.Contains(AreaLayouts.GateLocal(area, true), 5);
                if (area == WorldBuilder.Graveyard || area == WorldBuilder.Ruins || area == WorldBuilder.Frozen)
                    placements &= shape.Contains(AreaLayouts.BossLocal(area), 12);
                foreach (string group in AreaLayouts.QuestGroups(area))
                    foreach (Vector3 anchor in AreaLayouts.QuestAnchors[group])
                        placements &= shape.Contains(shape.NearestOpen(anchor, 5f), 5f);
                report.Add($"{WorldBuilder.AreaNames[area]}: {components} connected region(s), {open * step * step:F0} m² clear; placements {(placements ? "OK" : "INVALID")}");
            }
            return string.Join("\n", report);
        }
    }
}
