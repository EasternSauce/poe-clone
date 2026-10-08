using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.World;

namespace PoeClone.UI
{
    /// <summary>
    /// The ground under the minimap's dots: each area's layout baked once into a small texture
    /// (walkable ground in the area's colour with a little grain, anything solid darker, the
    /// outline of the open ground drawn lighter), shown only where the player has been. What has
    /// been seen is kept per area at a coarser grid and saved with the character (see
    /// <see cref="Export"/>), so dead ends stay on the map between visits. Haven is always fully shown.
    /// </summary>
    public static class MinimapTerrain
    {
        public const int Resolution = 240;        // preserve corridor detail across the larger maps
        public const int FogResolution = 80;      // seen/unseen cells across an area
        private const float RevealRadius = 22f;   // metres around the player that count as seen

        private sealed class AreaMap
        {
            public Texture2D Texture;
            public Color32[] Base;
            public Color32[] Shown;
            public bool[] Seen;
            public bool Dirty;
        }

        private static readonly Dictionary<int, AreaMap> maps = new Dictionary<int, AreaMap>();
        private static readonly Dictionary<int, bool[]> pendingFog = new Dictionary<int, bool[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            maps.Clear();
            pendingFog.Clear();
        }

        private static float AreaSize => AreaShape.MaxRadius * 2f;

        /// <summary>The area's map texture, baked the first time it's asked for.</summary>
        public static Texture2D TextureFor(int area)
        {
            return Get(area).Texture;
        }

        /// <summary>Marks everything near the player as seen; redraws the texture if that changed anything.</summary>
        public static void Reveal(int area, Vector3 position)
        {
            AreaMap map = Get(area);
            Vector3 centre = WorldBuilder.Center(area);
            float cell = AreaSize / FogResolution;
            int cx = Mathf.FloorToInt((position.x - centre.x + AreaSize * 0.5f) / cell);
            int cy = Mathf.FloorToInt((position.z - centre.z + AreaSize * 0.5f) / cell);
            int r = Mathf.CeilToInt(RevealRadius / cell);

            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= FogResolution || y >= FogResolution)
                        continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r)
                        continue;
                    int i = y * FogResolution + x;
                    if (!map.Seen[i])
                    {
                        map.Seen[i] = true;
                        map.Dirty = true;
                    }
                }
            }

            if (map.Dirty)
                Redraw(map, area);
        }

        /// <summary>Whether the player has seen this spot of the area.</summary>
        public static bool IsSeen(int area, Vector3 position)
        {
            if (!maps.TryGetValue(area, out AreaMap map))
                return false;
            Vector3 centre = WorldBuilder.Center(area);
            float cell = AreaSize / FogResolution;
            int x = Mathf.FloorToInt((position.x - centre.x + AreaSize * 0.5f) / cell);
            int y = Mathf.FloorToInt((position.z - centre.z + AreaSize * 0.5f) / cell);
            return x >= 0 && y >= 0 && x < FogResolution && y < FogResolution && map.Seen[y * FogResolution + x];
        }

        // ------------------------------------------------------------------ saving

        /// <summary>The seen cells of every area that has any, as base64 bitmaps ("area:data").</summary>
        public static List<string> Export()
        {
            var result = new List<string>();
            var all = new Dictionary<int, bool[]>(pendingFog);
            foreach (KeyValuePair<int, AreaMap> pair in maps)
                all[pair.Key] = pair.Value.Seen;

            foreach (KeyValuePair<int, bool[]> pair in all)
            {
                var bytes = new byte[(pair.Value.Length + 7) / 8];
                bool any = false;
                for (int i = 0; i < pair.Value.Length; i++)
                {
                    if (pair.Value[i])
                    {
                        bytes[i >> 3] |= (byte)(1 << (i & 7));
                        any = true;
                    }
                }
                if (any)
                    result.Add(pair.Key + ":" + Convert.ToBase64String(bytes));
            }
            return result;
        }

        /// <summary>Restores what was seen, from <see cref="Export"/>'s strings (bad entries are skipped).</summary>
        public static void Import(List<string> saved)
        {
            if (saved == null)
                return;
            foreach (string entry in saved)
            {
                int colon = entry != null ? entry.IndexOf(':') : -1;
                if (colon <= 0 || !int.TryParse(entry.Substring(0, colon), out int area))
                    continue;
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(entry.Substring(colon + 1));
                }
                catch (FormatException)
                {
                    continue;
                }

                var seen = new bool[FogResolution * FogResolution];
                for (int i = 0; i < seen.Length && (i >> 3) < bytes.Length; i++)
                    seen[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;

                if (maps.TryGetValue(area, out AreaMap map))
                {
                    for (int i = 0; i < seen.Length; i++)
                        map.Seen[i] |= seen[i];
                    Redraw(map, area);
                }
                else
                {
                    pendingFog[area] = seen;
                }
            }
        }

        // ------------------------------------------------------------------ baking

        private static AreaMap Get(int area)
        {
            if (maps.TryGetValue(area, out AreaMap map) && map.Texture != null)
                return map;

            map = Bake(area);
            if (pendingFog.TryGetValue(area, out bool[] seen))
            {
                Array.Copy(seen, map.Seen, Math.Min(seen.Length, map.Seen.Length));
                pendingFog.Remove(area);
            }
            if (area == WorldBuilder.Haven)
            {
                for (int i = 0; i < map.Seen.Length; i++)
                    map.Seen[i] = true;
            }
            maps[area] = map;
            Redraw(map, area);
            return map;
        }

        private enum Cell : byte { Outside, Open, Solid, Water, Bridge }

        private static AreaMap Bake(int area)
        {
            Vector3 centre = WorldBuilder.Center(area);
            AreaShape shape = WorldBuilder.Shape(area);
            float pixel = AreaSize / Resolution;
            var cells = new Cell[Resolution * Resolution];
            float groundY = centre.y;

            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    var p = new Vector3(centre.x - AreaSize * 0.5f + (x + 0.5f) * pixel, 0f, centre.z - AreaSize * 0.5f + (y + 0.5f) * pixel);
                    Cell c = Cell.Outside;
                    if (shape.Contains(p))
                    {
                        // Anything standing up out of the ground (a trunk, a rock, a wall, a house) is solid.
                        c = Cell.Open;
                        if (Physics.Raycast(new Vector3(p.x, groundY + 40f, p.z), Vector3.down, out RaycastHit hit, 60f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                            hit.point.y > groundY + 0.6f)
                            c = Cell.Solid;
                    }
                    if (shape.WaterDistance(p) >= 0f) c = shape.Contains(p) && shape.IsBridge(p) ? Cell.Bridge : Cell.Water;
                    cells[y * Resolution + x] = c;
                }
            }

            Color tint = WorldBuilder.AreaColor(area);
            Color open = Color.Lerp(new Color(0.42f, 0.40f, 0.36f), tint, 0.45f) * 0.85f;
            Color solid = open * 0.38f;
            Color rim = Color.Lerp(open, Color.white, 0.45f);
            var rng = new System.Random(area * 7919 + 13);
            float seedX = (float)rng.NextDouble() * 100f, seedY = (float)rng.NextDouble() * 100f;

            var pixels = new Color32[cells.Length];
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int i = y * Resolution + x;
                    Cell c = cells[i];
                    if (c == Cell.Outside)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Grain: two octaves of noise so the ground isn't a flat fill.
                    float n = Mathf.PerlinNoise(seedX + x * 0.09f, seedY + y * 0.09f) * 0.7f +
                              Mathf.PerlinNoise(seedX + x * 0.35f, seedY + y * 0.35f) * 0.3f;
                    float shade = 0.82f + 0.3f * n;
                    Color col;
                    if (c == Cell.Water)
                        col = (area == WorldBuilder.Frozen ? new Color(0.40f, 0.65f, 0.77f) : new Color(0.12f, 0.36f, 0.47f)) * shade;
                    else if (c == Cell.Bridge)
                        col = new Color(0.66f, 0.48f, 0.27f) * shade;
                    else if (c == Cell.Solid)
                        col = solid * shade;
                    else
                        col = IsEdge(cells, x, y) ? rim : open * shade;
                    col.a = c == Cell.Solid ? 0.9f : 0.85f;
                    pixels[i] = col;
                }
            }

            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "Minimap_" + area,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return new AreaMap
            {
                Texture = texture,
                Base = pixels,
                Shown = new Color32[pixels.Length],
                Seen = new bool[FogResolution * FogResolution],
                Dirty = true
            };
        }

        // Open ground next to something solid or the outside: the line that shows where you can't go.
        private static bool IsEdge(Cell[] cells, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Resolution || ny >= Resolution)
                        return true;
                    if (cells[ny * Resolution + nx] != Cell.Open)
                        return true;
                }
            }
            return false;
        }

        // Unseen parts are hidden; the border of what's seen fades out rather than stopping hard.
        private static void Redraw(AreaMap map, int area)
        {
            map.Dirty = false;
            int ratio = Resolution / FogResolution;
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int i = y * Resolution + x;
                    int fx = x / ratio, fy = y / ratio;
                    float seen = 0f;
                    int samples = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = fx + dx, sy = fy + dy;
                            if (sx < 0 || sy < 0 || sx >= FogResolution || sy >= FogResolution)
                                continue;
                            samples++;
                            if (map.Seen[sy * FogResolution + sx])
                                seen += 1f;
                        }
                    }
                    float visible = samples > 0 ? seen / samples : 0f;
                    Color32 c = map.Base[i];
                    c.a = (byte)(c.a * visible);
                    map.Shown[i] = c;
                }
            }
            map.Texture.SetPixels32(map.Shown);
            map.Texture.Apply(false);
        }
    }
}
