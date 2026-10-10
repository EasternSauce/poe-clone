using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// Procedural ground textures, so each area has its own floor (forest and town grass,
    /// graveyard mud, ruins ash), plus the packed dirt of roads and yards and the flagstones of
    /// Haven's square. All tileable and deterministic.
    /// </summary>
    public static class GroundTextures
    {
        private const int Size = 512;

        /// <param name="grain">How much each pixel's brightness varies on its own (0 = smooth, which looks plastic).</param>
        public static Texture2D Make(int seed, Color low, Color high, Color speck, float speckAmount, float scale,
            float grain = 0.08f)
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

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>
        /// Grass seen from above: tufts of blades, dark at the root and catching the light at the
        /// tip, over darker undergrowth. It drifts between lush and dry in broad patches, with
        /// the odd bald patch of soil.
        /// </summary>
        /// <param name="bareness">How much bare soil shows through (0 for none).</param>
        public static Texture2D Grass(int seed, Color under, Color lush, Color dry, Color soil, float bareness)
        {
            const int size = 1024;
            // The broad maps are made coarse and sampled smoothly: their shapes span 40+ texels,
            // so nothing is lost and most of the noise cost is saved.
            const int coarse = 128;
            const float toCoarse = coarse / (float)size;
            var random = new System.Random(seed);
            float ox = (float)random.NextDouble() * 100f;
            float oy = (float)random.NextDouble() * 100f;
            var dryMap = new float[coarse * coarse];
            var bareMap = new float[coarse * coarse];
            for (int y = 0; y < coarse; y++)
            {
                for (int x = 0; x < coarse; x++)
                {
                    float broad = Tileable(x, y, coarse, 3f, ox, oy) * 0.65f
                        + Tileable(x, y, coarse, 9f, ox + 31f, oy + 17f) * 0.35f;
                    float patches = Tileable(x, y, coarse, 5f, ox + 71f, oy + 5f) * 0.75f
                        + Tileable(x, y, coarse, 24f, ox + 13f, oy + 41f) * 0.25f;
                    dryMap[y * coarse + x] = Smooth(0.35f, 0.65f, broad);
                    bareMap[y * coarse + x] = Smooth(0.56f, 0.7f, patches) * bareness;
                }
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dryness = Sample(dryMap, coarse, x * toCoarse, y * toCoarse);
                    float bare = Sample(bareMap, coarse, x * toCoarse, y * toCoarse);
                    Color ground = Color.Lerp(Color.Lerp(under, Color.Lerp(under, dry, 0.35f), dryness), soil, bare * 2f);
                    pixels[y * size + x] = ground * (1f + ((float)random.NextDouble() * 2f - 1f) * 0.07f);
                }
            }

            // Scattered at random (a grid shows as rows) and drawn from the top of the texture
            // down, so each tuft overlaps the ones behind it as the camera sees them.
            int tufts = size * size / 70;
            var xs = new float[tufts];
            var ys = new float[tufts];
            for (int i = 0; i < tufts; i++)
            {
                xs[i] = (float)random.NextDouble() * size;
                ys[i] = (float)random.NextDouble() * size;
            }
            System.Array.Sort(ys, xs);
            for (int i = tufts - 1; i >= 0; i--)
            {
                if (random.NextDouble() < Sample(bareMap, coarse, xs[i] * toCoarse, ys[i] * toCoarse) * 2f)
                    continue;
                float shade = (float)random.NextDouble();
                float dryness = Sample(dryMap, coarse, xs[i] * toCoarse, ys[i] * toCoarse);
                Color tone = Color.Lerp(lush, dry, Mathf.Clamp01(dryness * 0.75f + (shade - 0.5f) * 0.35f))
                    * (0.88f + 0.24f * shade);
                Tuft(pixels, size, random, xs[i], ys[i], tone);
            }

            return Finish(pixels, size, "Grass_" + seed);
        }

        // 5-10 blades fanning up from a shared root, the outer ones leaning outwards.
        private static void Tuft(Color[] pixels, int size, System.Random random, float x, float y, Color tone)
        {
            int blades = 5 + random.Next(6);
            for (int b = 0; b < blades; b++)
            {
                float offset = (float)random.NextDouble() - 0.5f;
                float bx = x + offset * 7f;
                float by = y + ((float)random.NextDouble() - 0.5f) * 2f;
                float lean = ((float)random.NextDouble() - 0.5f) * 1.3f + offset * 0.9f;
                float dx = Mathf.Sin(lean), dy = Mathf.Cos(lean);
                float length = 5f + (float)random.NextDouble() * 7f;
                Color blade = tone * (0.88f + 0.24f * (float)random.NextDouble());
                Color root = blade * 0.6f;
                Color tip = new Color(blade.r * 1.3f + 0.02f, blade.g * 1.22f + 0.015f, blade.b);
                for (float k = 0f; k < length; k += 0.7f)
                {
                    float f = k / length;
                    int index = Wrap(Mathf.RoundToInt(by + dy * k), size) * size + Wrap(Mathf.RoundToInt(bx + dx * k), size);
                    pixels[index] = Color.Lerp(pixels[index], Color.Lerp(root, tip, f), 0.8f - f * 0.3f);
                }
            }
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

        // Shader-style smoothstep: 0 below from, 1 above to (Mathf.SmoothStep interpolates
        // between its first two arguments instead).
        private static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // Bilinear lookup in a square map that wraps at its edges.
        private static float Sample(float[] map, int size, float x, float y)
        {
            x -= 0.5f;
            y -= 0.5f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            int r0 = Wrap(y0, size) * size, r1 = Wrap(y0 + 1, size) * size;
            int c0 = Wrap(x0, size), c1 = Wrap(x0 + 1, size);
            return Mathf.Lerp(Mathf.Lerp(map[r0 + c0], map[r0 + c1], tx), Mathf.Lerp(map[r1 + c0], map[r1 + c1], tx), ty);
        }

        // Three octaves of tileable noise, broad shapes weighted most.
        private static float Layered(int x, int y, int size, float scale, float ox, float oy)
        {
            return Tileable(x, y, size, scale, ox, oy) * 0.55f
                + Tileable(x, y, size, scale * 3f, ox + 31f, oy + 17f) * 0.30f
                + Tileable(x, y, size, scale * 9f, ox + 57f, oy + 83f) * 0.15f;
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
