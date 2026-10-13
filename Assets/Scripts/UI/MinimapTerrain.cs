using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
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
        public const int Resolution = 640;        // desktop detail; mobile uses a smaller bake below
        private const int MobileResolution = 256;
        private const int MobileSampleResolution = 128;
        public const int FogResolution = 80;      // seen/unseen cells across an area
        private const float RevealRadius = 22f;   // metres around the player that count as seen

        private sealed class AreaMap
        {
            public Texture2D Texture;
            public Color32[] Base;
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
                Redraw(map);
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
                    Redraw(map);
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

            IEnumerator bake = Bake(area, map = new AreaMap(), float.PositiveInfinity);
            while (bake.MoveNext()) { }
            return Store(area, map);
        }

        /// <summary>
        /// Bakes the area's map a slice per frame, for a loading screen to run so it keeps
        /// animating instead of stalling on the first frame the map is shown.
        /// </summary>
        public static IEnumerator Prepare(int area)
        {
            if (maps.TryGetValue(area, out AreaMap baked) && baked.Texture != null)
                yield break;

            Physics.SyncTransforms();
            var map = new AreaMap();
            IEnumerator bake = Bake(area, map, SliceMs);
            while (bake.MoveNext())
                yield return null;
            if (!maps.TryGetValue(area, out baked) || baked.Texture == null)
                Store(area, map);
        }

        private static AreaMap Store(int area, AreaMap map)
        {
            if (pendingFog.TryGetValue(area, out bool[] seen))
            {
                Array.Copy(seen, map.Seen, Math.Min(seen.Length, map.Seen.Length));
                pendingFog.Remove(area);
            }
            if (area == WorldBuilder.Haven || area == WorldBuilder.Sahret)
            {
                for (int i = 0; i < map.Seen.Length; i++)
                    map.Seen[i] = true;
            }
            maps[area] = map;
            Redraw(map);
            return map;
        }

        private enum Cell : byte { Outside, Open, Solid, Water, Bridge }

        // The ground is sampled (shape distances, a raycast for anything standing up) on a coarser
        // grid than the texture, then interpolated per texel, so edges come out smooth, not blocky.
        private const int SampleResolution = 320;
        private const float FieldClamp = 4f;      // metres: distances past this don't move an edge
        private const int RimWidth = 2;           // texels of lighter outline along the open ground
        private const float SliceMs = 12f;        // work per frame when baking behind a loading screen

        // Fills in the map, yielding between rows once a slice has used up its time.
        private static IEnumerator Bake(int area, AreaMap map, float sliceMs)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Vector3 centre = WorldBuilder.Center(area);
            AreaShape shape = WorldBuilder.Shape(area);
            // Match the existing phone/tablet detection, including iPads with desktop user agents.
            // Keep these choices local to the bake: toggling touch simulation afterwards must not
            // change the dimensions used to redraw an already cached map.
            bool mobile = Inventory.TouchMode.Active;
            int resolution = mobile ? MobileResolution : Resolution;
            int n = mobile ? MobileSampleResolution : SampleResolution;
            int rimWidth = Mathf.Max(1, Mathf.RoundToInt(RimWidth * (float)resolution / Resolution));
            float grainScale = 240f / resolution; // same grain size in world metres on both tiers
            float step = AreaSize / n;
            float groundY = centre.y;
            var inside = new float[n * n];
            var water = new float[n * n];
            var bridge = new float[n * n];
            var solid = new float[n * n];

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    var p = new Vector3(centre.x - AreaSize * 0.5f + (x + 0.5f) * step, 0f, centre.z - AreaSize * 0.5f + (y + 0.5f) * step);
                    inside[i] = Mathf.Clamp(shape.InsideDistance(p), -FieldClamp, FieldClamp);
                    water[i] = Mathf.Clamp(shape.WaterDistance(p), -FieldClamp, FieldClamp);
                    bridge[i] = Mathf.Clamp(shape.BridgeDistance(p), -FieldClamp, FieldClamp);
                    // Anything standing up out of the ground (a trunk, a rock, a wall, a house) is solid.
                    if (inside[i] > -step &&
                        Physics.Raycast(new Vector3(p.x, groundY + 40f, p.z), Vector3.down, out RaycastHit hit, 60f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        hit.point.y > groundY + 0.6f)
                        solid[i] = 1f;
                }
                if (clock.Elapsed.TotalMilliseconds > sliceMs)
                {
                    yield return null;
                    clock.Restart();
                }
            }

            var cells = new Cell[resolution * resolution];
            float toSample = (float)n / resolution;
            for (int y = 0; y < resolution; y++)
            {
                float v = (y + 0.5f) * toSample - 0.5f;
                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) * toSample - 0.5f;
                    bool within = Sample(inside, n, u, v) >= 0f;
                    Cell c = within ? (Sample(solid, n, u, v) >= 0.5f ? Cell.Solid : Cell.Open) : Cell.Outside;
                    if (Sample(water, n, u, v) >= 0f)
                        c = within && Sample(bridge, n, u, v) >= 0f ? Cell.Bridge : Cell.Water;
                    cells[y * resolution + x] = c;
                }
                if (clock.Elapsed.TotalMilliseconds > sliceMs)
                {
                    yield return null;
                    clock.Restart();
                }
            }
            bool[] rims = Rims(cells, resolution, rimWidth);

            Color tint = WorldBuilder.AreaColor(area);
            Color open = Color.Lerp(new Color(0.42f, 0.40f, 0.36f), tint, 0.45f) * 0.85f;
            Color solidColor = open * 0.38f;
            Color rim = Color.Lerp(open, Color.white, 0.45f);
            var rng = new System.Random(area * 7919 + 13);
            float seedX = (float)rng.NextDouble() * 100f, seedY = (float)rng.NextDouble() * 100f;

            var pixels = new Color32[cells.Length];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * resolution + x;
                    Cell c = cells[i];
                    if (c == Cell.Outside)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Grain: two octaves of noise so the ground isn't a flat fill.
                    float gx = x * grainScale, gy = y * grainScale;
                    float noise = Mathf.PerlinNoise(seedX + gx * 0.09f, seedY + gy * 0.09f) * 0.7f +
                                  Mathf.PerlinNoise(seedX + gx * 0.35f, seedY + gy * 0.35f) * 0.3f;
                    float shade = 0.82f + 0.3f * noise;
                    Color col;
                    if (c == Cell.Water)
                        col = (area == WorldBuilder.Frozen ? new Color(0.40f, 0.65f, 0.77f) : new Color(0.12f, 0.36f, 0.47f)) * shade;
                    else if (c == Cell.Bridge)
                        col = new Color(0.66f, 0.48f, 0.27f) * shade;
                    else if (c == Cell.Solid)
                        col = solidColor * shade;
                    else
                        col = rims[i] ? rim : open * shade;
                    col.a = c == Cell.Solid ? 0.9f : 0.85f;
                    pixels[i] = col;
                }
                if (clock.Elapsed.TotalMilliseconds > sliceMs)
                {
                    yield return null;
                    clock.Restart();
                }
            }

            map.Texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Minimap_" + area,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            map.Base = pixels;
            map.Seen = new bool[FogResolution * FogResolution];
            map.Dirty = true;
        }

        // Bilinear read of a square sample grid, clamped at its edges.
        private static float Sample(float[] field, int n, float u, float v)
        {
            u = Mathf.Clamp(u, 0f, n - 1);
            v = Mathf.Clamp(v, 0f, n - 1);
            int x = Mathf.Min((int)u, n - 2), y = Mathf.Min((int)v, n - 2);
            float fx = u - x, fy = v - y;
            int i = y * n + x;
            return Mathf.Lerp(Mathf.Lerp(field[i], field[i + 1], fx), Mathf.Lerp(field[i + n], field[i + n + 1], fx), fy);
        }

        // Open ground within RimWidth texels of something solid or the outside: the line that shows
        // where you can't go. A square neighbourhood, checked along rows and then along columns.
        private static bool[] Rims(Cell[] cells, int resolution, int rimWidth)
        {
            var across = new bool[cells.Length];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * resolution + x;
                    for (int d = -rimWidth; d <= rimWidth && !across[i]; d++)
                    {
                        int nx = x + d;
                        across[i] = nx < 0 || nx >= resolution || cells[i + d] != Cell.Open;
                    }
                }
            }

            var rims = new bool[cells.Length];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * resolution + x;
                    if (cells[i] != Cell.Open)
                        continue;
                    for (int d = -rimWidth; d <= rimWidth && !rims[i]; d++)
                    {
                        int ny = y + d;
                        rims[i] = ny < 0 || ny >= resolution || across[ny * resolution + x];
                    }
                }
            }
            return rims;
        }

        // Unseen parts are hidden; the border of what's seen fades out smoothly rather than stopping hard.
        private static void Redraw(AreaMap map)
        {
            map.Dirty = false;
            const int n = FogResolution;
            var visible = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float seen = 0f;
                    int samples = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= n || sy >= n)
                                continue;
                            samples++;
                            if (map.Seen[sy * n + sx])
                                seen += 1f;
                        }
                    }
                    visible[y * n + x] = seen / samples;
                }
            }

            int resolution = map.Texture.width;
            NativeArray<Color32> shown = map.Texture.GetPixelData<Color32>(0);
            float toFog = (float)n / resolution;
            for (int y = 0; y < resolution; y++)
            {
                float v = (y + 0.5f) * toFog - 0.5f;
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * resolution + x;
                    Color32 c = map.Base[i];
                    c.a = (byte)(c.a * Sample(visible, n, (x + 0.5f) * toFog - 0.5f, v));
                    shown[i] = c;
                }
            }
            map.Texture.Apply(false);
        }
    }
}
