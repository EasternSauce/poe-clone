using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// Procedural ground textures for the areas WorldBuilder adds, so each has its own floor
    /// (town grass, graveyard mud, ruins ash) instead of the forest's green grass tinted darker,
    /// plus the packed dirt of roads and yards and the flagstones of Haven's square. All tileable
    /// and deterministic.
    /// </summary>
    public static class GroundTextures
    {
        private const int Size = 512;

        /// <param name="grain">How much each pixel's brightness varies on its own (0 = smooth, which looks plastic).</param>
        /// <param name="blades">How many grass blade strokes to scatter (0 for bare ground).</param>
        public static Texture2D Make(int seed, Color low, Color high, Color speck, float speckAmount, float scale,
            float grain = 0.08f, int blades = 0)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "Ground_" + seed
            };

            var random = new System.Random(seed);
            float ox = (float)random.NextDouble() * 100f;
            float oy = (float)random.NextDouble() * 100f;
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Layered(x, y, Size, scale, ox, oy);
                    Color c = Color.Lerp(low, high, Mathf.SmoothStep(0.2f, 0.8f, n));
                    c *= 1f + ((float)random.NextDouble() * 2f - 1f) * grain;

                    if (speckAmount > 0f && random.NextDouble() < speckAmount)
                        c = Color.Lerp(c, speck, 0.7f);

                    pixels[y * Size + x] = c;
                }
            }

            for (int i = 0; i < blades; i++)
                Blade(pixels, random, low, high);

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>
        /// Packed earth for roads, yards and clearings: broad mottling, fine grain and small
        /// pebbles lit from one side, so it reads as ground rather than painted cloth.
        /// </summary>
        public static Texture2D Dirt(int seed, Color low, Color high, Color pebble, int pebbles)
        {
            const int size = 256;
            var random = new System.Random(seed);
            float ox = (float)random.NextDouble() * 100f;
            float oy = (float)random.NextDouble() * 100f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Layered(x, y, size, 4f, ox, oy);
                    Color c = Color.Lerp(low, high, Mathf.SmoothStep(0.25f, 0.75f, n));
                    pixels[y * size + x] = c * (1f + ((float)random.NextDouble() * 2f - 1f) * 0.07f);
                }
            }

            for (int i = 0; i < pebbles; i++)
            {
                int cx = random.Next(size), cy = random.Next(size);
                float r = 0.8f + (float)random.NextDouble() * 1.8f;
                Color tone = pebble * (0.8f + (float)random.NextDouble() * 0.4f);
                int reach = Mathf.CeilToInt(r) + 1;
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int index = Wrap(cy + dy, size) * size + Wrap(cx + dx, size);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= r)
                            // Lighter on one side, so every pebble has the same light.
                            pixels[index] = tone * (1f + 0.25f * dy / reach);
                        else if (d <= r + 1f && dy < 0)
                            pixels[index] *= 0.75f; // contact shadow
                    }
                }
            }

            return Finish(pixels, size, "Dirt_" + seed);
        }

        /// <summary>
        /// Irregular laid stones with dark mortar joints: each stone has its own tone, a worn
        /// darker rim and faint mottling.
        /// </summary>
        public static Texture2D Flagstones(int seed, Color stone, Color mortar, int cells)
        {
            const int size = 256;
            var random = new System.Random(seed);
            float cell = size / (float)cells;
            var points = new Vector2[cells, cells];
            var tones = new Color[cells, cells];
            for (int i = 0; i < cells; i++)
            {
                for (int j = 0; j < cells; j++)
                {
                    points[i, j] = new Vector2(i + 0.2f + (float)random.NextDouble() * 0.6f,
                        j + 0.2f + (float)random.NextDouble() * 0.6f) * cell;
                    float shade = 0.86f + (float)random.NextDouble() * 0.2f;
                    float warm = ((float)random.NextDouble() - 0.5f) * 0.08f;
                    tones[i, j] = new Color(stone.r * (shade + warm), stone.g * shade, stone.b * (shade - warm));
                }
            }

            float ox = (float)random.NextDouble() * 100f;
            float oy = (float)random.NextDouble() * 100f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Nearest and second-nearest stone centres, wrapping so the texture tiles.
                    int ci = (int)(x / cell), cj = (int)(y / cell);
                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    Color tone = stone;
                    for (int di = -1; di <= 1; di++)
                    {
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int i = ci + di, j = cj + dj;
                            Vector2 p = points[Wrap(i, cells), Wrap(j, cells)]
                                + new Vector2(Mathf.Floor(i / (float)cells), Mathf.Floor(j / (float)cells)) * size;
                            float d = Vector2.Distance(p, new Vector2(x, y));
                            if (d < d1) { d2 = d1; d1 = d; tone = tones[Wrap(i, cells), Wrap(j, cells)]; }
                            else if (d < d2) d2 = d;
                        }
                    }

                    float edge = d2 - d1;
                    float rim = Mathf.SmoothStep(3.5f, 11f, edge);
                    float mottle = 0.93f + 0.14f * Layered(x, y, size, 6f, ox, oy);
                    Color c = tone * (0.8f + 0.2f * rim) * mottle;
                    c *= 1f + ((float)random.NextDouble() * 2f - 1f) * 0.04f;
                    pixels[y * size + x] = Color.Lerp(c, mortar, 1f - Mathf.SmoothStep(1.6f, 3.6f, edge));
                }
            }

            return Finish(pixels, size, "Flagstones_" + seed);
        }

        private static Texture2D Finish(Color[] pixels, int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
                name = name
            };
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        private static int Wrap(int v, int size) => (v % size + size) % size;

        // Three octaves of tileable noise, broad shapes weighted most.
        private static float Layered(int x, int y, int size, float scale, float ox, float oy)
        {
            return Tileable(x, y, size, scale, ox, oy) * 0.55f
                + Tileable(x, y, size, scale * 3f, ox + 31f, oy + 17f) * 0.30f
                + Tileable(x, y, size, scale * 9f, ox + 57f, oy + 83f) * 0.15f;
        }

        // A short stroke, 3-7 px, in a random direction, a bit lighter or darker than the ground
        // under it; wraps at the edges so the texture still tiles.
        private static void Blade(Color32[] pixels, System.Random random, Color low, Color high)
        {
            int x = random.Next(Size);
            int y = random.Next(Size);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float dx = Mathf.Cos(angle);
            float dy = Mathf.Sin(angle);
            int length = 3 + random.Next(5);
            bool light = random.NextDouble() < 0.55;
            Color tone = light ? Color.Lerp(high, Color.white, 0.12f) : low * 0.72f;
            float strength = 0.35f + (float)random.NextDouble() * 0.35f;

            for (int i = 0; i < length; i++)
            {
                int px = ((x + Mathf.RoundToInt(dx * i)) % Size + Size) % Size;
                int py = ((y + Mathf.RoundToInt(dy * i)) % Size + Size) % Size;
                int index = py * Size + px;
                // Fades towards the tip.
                float t = strength * (1f - i / (float)length * 0.6f);
                pixels[index] = Color.Lerp(pixels[index], tone, t);
            }
        }

        // Perlin noise that wraps at the texture's edges (blend of four offset samples).
        private static float Tileable(int x, int y, int size, float scale, float ox, float oy)
        {
            float u = x / (float)size;
            float v = y / (float)size;
            float period = scale;
            float a = Mathf.PerlinNoise(ox + u * period, oy + v * period);
            float b = Mathf.PerlinNoise(ox + (u - 1f) * period, oy + v * period);
            float c = Mathf.PerlinNoise(ox + u * period, oy + (v - 1f) * period);
            float d = Mathf.PerlinNoise(ox + (u - 1f) * period, oy + (v - 1f) * period);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
