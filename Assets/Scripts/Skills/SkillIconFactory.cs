using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Skills
{
    /// <summary>Small, readable silhouettes for every skill, generated once and tinted by the UI.</summary>
    public static class SkillIconFactory
    {
        private const int Size = 64;
        private delegate bool Shape(float x, float y);
        private static readonly Dictionary<SkillId, Sprite> Cache = new Dictionary<SkillId, Sprite>();

        public static Sprite Get(SkillId id)
        {
            if (Cache.TryGetValue(id, out Sprite sprite) && sprite != null)
                return sprite;

            var ink = new List<Shape>();
            var cut = new List<Shape>();
            Draw(id, ink, cut);
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "SkillIcon_" + id;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 3f) / Size;
                            float v = (y + (sy + 0.5f) / 3f) / Size;
                            if (Contains(ink, u, v) && !Contains(cut, u, v)) hits++;
                        }
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            Cache[id] = sprite;
            return sprite;
        }

        private static bool Contains(List<Shape> shapes, float x, float y)
        {
            foreach (Shape shape in shapes)
                if (shape(x, y)) return true;
            return false;
        }

        private static Shape Disc(float x, float y, float r) =>
            (u, v) => (u - x) * (u - x) + (v - y) * (v - y) <= r * r;

        private static Shape Ring(float x, float y, float r, float width) =>
            (u, v) =>
            {
                float d = (u - x) * (u - x) + (v - y) * (v - y);
                return d <= r * r && d >= (r - width) * (r - width);
            };

        private static Shape Line(float ax, float ay, float bx, float by, float width) =>
            (u, v) =>
            {
                float dx = bx - ax, dy = by - ay;
                float t = Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / (dx * dx + dy * dy));
                float x = u - ax - t * dx, y = v - ay - t * dy;
                return x * x + y * y <= width * width * 0.25f;
            };

        private static Shape Poly(params float[] p) =>
            (u, v) =>
            {
                bool inside = false;
                for (int i = 0, j = p.Length / 2 - 1; i < p.Length / 2; j = i++)
                {
                    float xi = p[i * 2], yi = p[i * 2 + 1];
                    float xj = p[j * 2], yj = p[j * 2 + 1];
                    if ((yi > v) != (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi)
                        inside = !inside;
                }
                return inside;
            };

        private static void Arrow(List<Shape> a, float x0, float y0, float x1, float y1)
        {
            a.Add(Line(x0, y0, x1, y1, 0.055f));
            float dx = x1 - x0, dy = y1 - y0;
            a.Add(Poly(x1, y1, x1 - dx * 0.34f - dy * 0.19f, y1 - dy * 0.34f + dx * 0.19f,
                x1 - dx * 0.34f + dy * 0.19f, y1 - dy * 0.34f - dx * 0.19f));
        }

        private static void Sword(List<Shape> a, float x0, float y0, float x1, float y1)
        {
            a.Add(Line(x0, y0, x1, y1, 0.095f));
            a.Add(Disc(x0, y0, 0.07f));
            a.Add(Line(x0 - 0.13f, y0 + 0.13f, x0 + 0.13f, y0 - 0.13f, 0.065f));
            a.Add(Poly(x1, y1 + 0.09f, x1 + 0.09f, y1, x1 + 0.12f, y1 + 0.12f));
        }

        private static void Skull(List<Shape> a, List<Shape> c, float x, float y, float scale)
        {
            a.Add(Disc(x, y + scale * 0.1f, scale * 0.27f));
            a.Add(Poly(x - scale * 0.17f, y, x + scale * 0.17f, y,
                x + scale * 0.13f, y - scale * 0.28f, x - scale * 0.13f, y - scale * 0.28f));
            c.Add(Disc(x - scale * 0.1f, y + scale * 0.1f, scale * 0.055f));
            c.Add(Disc(x + scale * 0.1f, y + scale * 0.1f, scale * 0.055f));
        }

        private static void Draw(SkillId id, List<Shape> a, List<Shape> c)
        {
            switch (id)
            {
                case SkillId.Cleave:
                    Sword(a, 0.31f, 0.24f, 0.7f, 0.72f);
                    a.Add(Ring(0.5f, 0.49f, 0.38f, 0.065f));
                    c.Add(Poly(0.07f, 0.05f, 0.5f, 0.05f, 0.07f, 0.5f));
                    break;
                case SkillId.FireBolt:
                    a.Add(Poly(0.14f, 0.18f, 0.68f, 0.32f, 0.87f, 0.77f, 0.58f, 0.6f, 0.46f, 0.8f));
                    a.Add(Disc(0.72f, 0.68f, 0.16f));
                    break;
                case SkillId.Dash:
                    Arrow(a, 0.23f, 0.5f, 0.85f, 0.5f);
                    a.Add(Line(0.12f, 0.69f, 0.43f, 0.69f, 0.045f));
                    a.Add(Line(0.08f, 0.31f, 0.37f, 0.31f, 0.045f));
                    break;
                case SkillId.FrostNova:
                    a.Add(Ring(0.5f, 0.5f, 0.34f, 0.07f));
                    a.Add(Disc(0.5f, 0.5f, 0.09f));
                    for (int i = 0; i < 8; i++)
                    {
                        float t = i * Mathf.PI / 4f;
                        a.Add(Line(0.5f + Mathf.Cos(t) * 0.3f, 0.5f + Mathf.Sin(t) * 0.3f,
                            0.5f + Mathf.Cos(t) * 0.46f, 0.5f + Mathf.Sin(t) * 0.46f, 0.065f));
                    }
                    break;
                case SkillId.Rejuvenate:
                    a.Add(Disc(0.34f, 0.64f, 0.2f));
                    a.Add(Disc(0.66f, 0.64f, 0.2f));
                    a.Add(Poly(0.15f, 0.64f, 0.85f, 0.64f, 0.5f, 0.13f));
                    c.Add(Poly(0.46f, 0.77f, 0.54f, 0.77f, 0.54f, 0.62f, 0.68f, 0.62f,
                        0.68f, 0.54f, 0.54f, 0.54f, 0.54f, 0.4f, 0.46f, 0.4f,
                        0.46f, 0.54f, 0.32f, 0.54f, 0.32f, 0.62f, 0.46f, 0.62f));
                    break;
                case SkillId.ChainLightning:
                    a.Add(Poly(0.56f, 0.94f, 0.22f, 0.46f, 0.45f, 0.48f, 0.32f, 0.07f,
                        0.83f, 0.61f, 0.59f, 0.58f, 0.77f, 0.94f));
                    break;
                case SkillId.IceShard:
                    a.Add(Poly(0.5f, 0.94f, 0.31f, 0.47f, 0.5f, 0.17f, 0.69f, 0.47f));
                    a.Add(Poly(0.12f, 0.77f, 0.16f, 0.32f, 0.39f, 0.14f, 0.34f, 0.5f));
                    a.Add(Poly(0.88f, 0.77f, 0.66f, 0.5f, 0.61f, 0.14f, 0.84f, 0.32f));
                    break;
                case SkillId.Teleport:
                    a.Add(Ring(0.31f, 0.38f, 0.2f, 0.055f));
                    a.Add(Ring(0.7f, 0.65f, 0.2f, 0.055f));
                    Arrow(a, 0.38f, 0.44f, 0.72f, 0.73f);
                    break;
                case SkillId.RaiseSkeletons:
                    Skull(a, c, 0.33f, 0.53f, 0.72f);
                    Skull(a, c, 0.7f, 0.43f, 0.57f);
                    break;
                case SkillId.DeathMark:
                    a.Add(Ring(0.5f, 0.5f, 0.36f, 0.06f));
                    Skull(a, c, 0.5f, 0.52f, 0.82f);
                    break;
                case SkillId.SkeletonMages:
                    Skull(a, c, 0.45f, 0.4f, 0.9f);
                    a.Add(Poly(0.33f, 0.65f, 0.55f, 0.93f, 0.72f, 0.58f));
                    a.Add(Disc(0.75f, 0.78f, 0.095f));
                    break;
                case SkillId.SpiritWolves:
                    a.Add(Poly(0.15f, 0.84f, 0.36f, 0.62f, 0.5f, 0.72f, 0.64f, 0.62f,
                        0.85f, 0.84f, 0.79f, 0.36f, 0.5f, 0.16f, 0.21f, 0.36f));
                    c.Add(Disc(0.37f, 0.49f, 0.045f));
                    c.Add(Disc(0.63f, 0.49f, 0.045f));
                    break;
                case SkillId.BoneGolem:
                    a.Add(Disc(0.5f, 0.65f, 0.21f));
                    a.Add(Poly(0.16f, 0.45f, 0.35f, 0.54f, 0.65f, 0.54f, 0.84f, 0.45f,
                        0.75f, 0.14f, 0.25f, 0.14f));
                    a.Add(Disc(0.2f, 0.44f, 0.105f));
                    a.Add(Disc(0.8f, 0.44f, 0.105f));
                    c.Add(Disc(0.43f, 0.68f, 0.035f));
                    c.Add(Disc(0.57f, 0.68f, 0.035f));
                    break;
                case SkillId.GraveRot:
                    a.Add(Poly(0.5f, 0.89f, 0.85f, 0.69f, 0.76f, 0.27f, 0.5f, 0.12f,
                        0.24f, 0.27f, 0.15f, 0.69f));
                    c.Add(Disc(0.39f, 0.54f, 0.065f));
                    c.Add(Disc(0.62f, 0.54f, 0.065f));
                    c.Add(Poly(0.5f, 0.45f, 0.42f, 0.32f, 0.58f, 0.32f));
                    break;
                case SkillId.SplitShot:
                    Arrow(a, 0.18f, 0.2f, 0.34f, 0.85f);
                    Arrow(a, 0.5f, 0.17f, 0.5f, 0.9f);
                    Arrow(a, 0.82f, 0.2f, 0.66f, 0.85f);
                    break;
                case SkillId.PiercingShot:
                    Arrow(a, 0.16f, 0.5f, 0.88f, 0.5f);
                    a.Add(Ring(0.53f, 0.5f, 0.28f, 0.055f));
                    break;
                case SkillId.RainOfArrows:
                    Arrow(a, 0.25f, 0.88f, 0.25f, 0.23f);
                    Arrow(a, 0.5f, 0.83f, 0.5f, 0.13f);
                    Arrow(a, 0.75f, 0.88f, 0.75f, 0.23f);
                    break;
                case SkillId.BurningArrow:
                    Arrow(a, 0.16f, 0.19f, 0.76f, 0.78f);
                    a.Add(Poly(0.7f, 0.65f, 0.66f, 0.9f, 0.84f, 0.75f, 0.88f, 0.93f, 0.93f, 0.58f));
                    break;
                case SkillId.FangStrike:
                    a.Add(Poly(0.13f, 0.84f, 0.37f, 0.86f, 0.32f, 0.23f, 0.23f, 0.1f));
                    a.Add(Poly(0.63f, 0.86f, 0.87f, 0.84f, 0.77f, 0.1f, 0.68f, 0.23f));
                    break;
                case SkillId.VenomArrow:
                    Arrow(a, 0.16f, 0.17f, 0.78f, 0.78f);
                    a.Add(Disc(0.25f, 0.62f, 0.075f));
                    a.Add(Disc(0.47f, 0.81f, 0.055f));
                    a.Add(Disc(0.73f, 0.27f, 0.085f));
                    break;
                case SkillId.VenomSpout:
                    a.Add(Poly(0.15f, 0.15f, 0.85f, 0.15f, 0.73f, 0.3f, 0.27f, 0.3f));
                    a.Add(Poly(0.42f, 0.27f, 0.5f, 0.85f, 0.59f, 0.27f));
                    a.Add(Disc(0.29f, 0.69f, 0.085f));
                    a.Add(Disc(0.72f, 0.78f, 0.065f));
                    break;
                case SkillId.SummonViper:
                    a.Add(Ring(0.48f, 0.52f, 0.29f, 0.1f));
                    a.Add(Poly(0.5f, 0.55f, 0.82f, 0.75f, 0.88f, 0.6f, 0.68f, 0.46f));
                    a.Add(Line(0.83f, 0.62f, 0.94f, 0.48f, 0.03f));
                    c.Add(Disc(0.77f, 0.64f, 0.025f));
                    break;
                case SkillId.Pulverize:
                    a.Add(Line(0.25f, 0.18f, 0.67f, 0.71f, 0.085f));
                    a.Add(Poly(0.55f, 0.8f, 0.77f, 0.91f, 0.9f, 0.7f, 0.68f, 0.58f));
                    a.Add(Line(0.12f, 0.16f, 0.39f, 0.16f, 0.055f));
                    a.Add(Line(0.16f, 0.28f, 0.27f, 0.28f, 0.05f));
                    break;
                case SkillId.ReapingArc:
                    a.Add(Line(0.22f, 0.17f, 0.59f, 0.71f, 0.08f));
                    a.Add(Poly(0.55f, 0.76f, 0.77f, 0.9f, 0.87f, 0.56f, 0.73f, 0.64f));
                    a.Add(Ring(0.48f, 0.48f, 0.42f, 0.045f));
                    c.Add(Poly(0.03f, 0.02f, 0.5f, 0.02f, 0.03f, 0.5f));
                    break;
                case SkillId.LungingThrust:
                    Sword(a, 0.18f, 0.16f, 0.73f, 0.72f);
                    a.Add(Line(0.08f, 0.53f, 0.34f, 0.53f, 0.045f));
                    a.Add(Line(0.16f, 0.68f, 0.46f, 0.68f, 0.045f));
                    break;
            }
        }
    }
}
