using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// Procedural ground textures for the areas WorldBuilder adds, so each has its own floor
    /// (town grass, graveyard mud, ruins ash) instead of the forest's green grass tinted darker.
    /// Two colours blended by layered noise, plus optional speckles; tileable and deterministic.
    /// </summary>
    public static class GroundTextures
    {
        private const int Size = 256;

        public static Texture2D Make(int seed, Color low, Color high, Color speck, float speckAmount, float scale)
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
                    float n = Tileable(x, y, scale, ox, oy) * 0.65f + Tileable(x, y, scale * 3f, ox + 31f, oy + 17f) * 0.35f;
                    Color c = Color.Lerp(low, high, Mathf.SmoothStep(0.2f, 0.8f, n));

                    if (speckAmount > 0f && random.NextDouble() < speckAmount)
                        c = Color.Lerp(c, speck, 0.7f);

                    pixels[y * Size + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
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
