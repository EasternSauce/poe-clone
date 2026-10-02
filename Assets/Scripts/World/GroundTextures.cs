using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// Procedural ground textures for the areas WorldBuilder adds, so each has its own floor
    /// (town grass, graveyard mud, ruins ash) instead of the forest's green grass tinted darker.
    /// Two colours blended by layered noise, a fine per-pixel grain, optional speckles and, for
    /// grass, short light and dark blade strokes; tileable and deterministic.
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
                    float n = Tileable(x, y, scale, ox, oy) * 0.55f
                        + Tileable(x, y, scale * 3f, ox + 31f, oy + 17f) * 0.30f
                        + Tileable(x, y, scale * 9f, ox + 57f, oy + 83f) * 0.15f;
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
        private static float Tileable(int x, int y, float scale, float ox, float oy)
        {
            float u = x / (float)Size;
            float v = y / (float)Size;
            float period = scale;
            float a = Mathf.PerlinNoise(ox + u * period, oy + v * period);
            float b = Mathf.PerlinNoise(ox + (u - 1f) * period, oy + v * period);
            float c = Mathf.PerlinNoise(ox + u * period, oy + (v - 1f) * period);
            float d = Mathf.PerlinNoise(ox + (u - 1f) * period, oy + (v - 1f) * period);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
