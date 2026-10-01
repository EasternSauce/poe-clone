using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Draws simple white silhouette icons for each item type at runtime (no art assets needed).
    /// Shapes are built from circles, rectangles, capsules and polygons in a 0..1 square (y up),
    /// then rasterised with 3x3 supersampling for smooth edges.
    /// </summary>
    public static class IconFactory
    {
        public const int Size = 64;

        private delegate bool Shape(float x, float y);

        private static readonly Dictionary<ItemType, Sprite> Cache = new Dictionary<ItemType, Sprite>();

        public static Sprite Get(ItemType type)
        {
            Sprite sprite;
            if (Cache.TryGetValue(type, out sprite) && sprite != null)
                return sprite;

            sprite = Build(type);
            Cache[type] = sprite;
            return sprite;
        }

        /// <summary>Fraction of the icon's pixels that are opaque. Used by tests to catch empty/degenerate icons.</summary>
        public static float Coverage(Sprite sprite)
        {
            Color32[] px = sprite.texture.GetPixels32();
            int filled = 0;
            foreach (Color32 c in px)
            {
                if (c.a > 127)
                    filled++;
            }
            return filled / (float)px.Length;
        }

        // ------------------------------------------------------------------ rasteriser

        private static Sprite Build(ItemType type)
        {
            List<Shape> add = new List<Shape>();
            List<Shape> subtract = new List<Shape>();
            List<Shape> top = new List<Shape>();
            Describe(type, add, subtract, top);

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.name = "Icon_" + type;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            const int ss = 3;
            Color32[] px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / Size;
                            float v = (y + (sy + 0.5f) / ss) / Size;
                            bool inside = (Any(add, u, v) && !Any(subtract, u, v)) || Any(top, u, v);
                            if (inside)
                                hits++;
                        }
                    }
                    byte a = (byte)(255 * hits / (ss * ss));
                    px[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool Any(List<Shape> shapes, float x, float y)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i](x, y))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ shape primitives

        private static Shape Circle(float cx, float cy, float r)
        {
            return (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        }

        private static Shape RingShape(float cx, float cy, float r, float thickness)
        {
            return (x, y) =>
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                return d <= r && d >= r - thickness;
            };
        }

        private static Shape Rect(float x0, float y0, float x1, float y1)
        {
            return (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        private static Shape And(Shape a, Shape b)
        {
            return (x, y) => a(x, y) && b(x, y);
        }

        private static Shape Capsule(float ax, float ay, float bx, float by, float width)
        {
            float half = width * 0.5f;
            return (x, y) =>
            {
                float abx = bx - ax, aby = by - ay;
                float apx = x - ax, apy = y - ay;
                float lenSq = abx * abx + aby * aby;
                float t = lenSq > 0f ? Mathf.Clamp01((apx * abx + apy * aby) / lenSq) : 0f;
                float dx = x - (ax + abx * t);
                float dy = y - (ay + aby * t);
                return dx * dx + dy * dy <= half * half;
            };
        }

        // Even-odd polygon fill. Points are x0, y0, x1, y1, ...
        private static Shape Poly(params float[] p)
        {
            int n = p.Length / 2;
            return (x, y) =>
            {
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float xi = p[2 * i], yi = p[2 * i + 1];
                    float xj = p[2 * j], yj = p[2 * j + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                        inside = !inside;
                }
                return inside;
            };
        }

        // ------------------------------------------------------------------ the icons

        private static void Describe(ItemType type, List<Shape> add, List<Shape> sub, List<Shape> top)
        {
            switch (type)
            {
                case ItemType.Helmet:
                    // Dome, face guard with cheek plates, and a visor slit.
                    add.Add(And(Circle(0.5f, 0.5f, 0.32f), Rect(0f, 0.5f, 1f, 1f)));
                    add.Add(Rect(0.18f, 0.22f, 0.82f, 0.5f));
                    add.Add(Rect(0.18f, 0.12f, 0.34f, 0.3f));
                    add.Add(Rect(0.66f, 0.12f, 0.82f, 0.3f));
                    sub.Add(Rect(0.34f, 0.34f, 0.66f, 0.42f));
                    break;

                case ItemType.BodyArmour:
                    // Torso with rounded pauldrons, neck hole, centre seam and flared hem.
                    add.Add(Poly(0.3f, 0.86f, 0.7f, 0.86f, 0.76f, 0.3f, 0.24f, 0.3f));
                    add.Add(Circle(0.2f, 0.76f, 0.13f));
                    add.Add(Circle(0.8f, 0.76f, 0.13f));
                    add.Add(Poly(0.24f, 0.3f, 0.76f, 0.3f, 0.82f, 0.16f, 0.18f, 0.16f));
                    sub.Add(Circle(0.5f, 0.9f, 0.09f));
                    sub.Add(Rect(0.485f, 0.2f, 0.515f, 0.76f));
                    break;

                case ItemType.Gloves:
                    // Palm, four fingers, thumb and cuff.
                    add.Add(Rect(0.3f, 0.24f, 0.7f, 0.56f));
                    add.Add(Rect(0.3f, 0.56f, 0.39f, 0.82f));
                    add.Add(Rect(0.4f, 0.56f, 0.49f, 0.9f));
                    add.Add(Rect(0.5f, 0.56f, 0.59f, 0.9f));
                    add.Add(Rect(0.6f, 0.56f, 0.7f, 0.8f));
                    add.Add(Poly(0.3f, 0.3f, 0.13f, 0.46f, 0.2f, 0.57f, 0.36f, 0.46f));
                    add.Add(Rect(0.27f, 0.1f, 0.73f, 0.26f));
                    break;

                case ItemType.Boots:
                    // Tall shaft with a cuff, foot with a rounded toe, and a sole.
                    add.Add(Rect(0.34f, 0.3f, 0.62f, 0.88f));
                    add.Add(Rect(0.3f, 0.8f, 0.66f, 0.9f));
                    add.Add(Rect(0.34f, 0.2f, 0.78f, 0.4f));
                    add.Add(Circle(0.76f, 0.3f, 0.1f));
                    add.Add(Rect(0.32f, 0.14f, 0.86f, 0.2f));
                    break;

                case ItemType.Belt:
                    // Band with a framed buckle.
                    add.Add(Rect(0.06f, 0.42f, 0.94f, 0.58f));
                    add.Add(Rect(0.38f, 0.32f, 0.62f, 0.68f));
                    sub.Add(Rect(0.44f, 0.4f, 0.56f, 0.6f));
                    break;

                case ItemType.Amulet:
                    // V-shaped chain with a round pendant.
                    add.Add(Capsule(0.16f, 0.94f, 0.3f, 0.64f, 0.05f));
                    add.Add(Capsule(0.3f, 0.64f, 0.5f, 0.52f, 0.05f));
                    add.Add(Capsule(0.84f, 0.94f, 0.7f, 0.64f, 0.05f));
                    add.Add(Capsule(0.7f, 0.64f, 0.5f, 0.52f, 0.05f));
                    add.Add(Circle(0.5f, 0.34f, 0.17f));
                    sub.Add(Circle(0.5f, 0.34f, 0.08f));
                    break;

                case ItemType.Ring:
                    // Band with a diamond gem on top.
                    add.Add(RingShape(0.5f, 0.36f, 0.28f, 0.09f));
                    add.Add(Poly(0.5f, 0.86f, 0.64f, 0.7f, 0.5f, 0.52f, 0.36f, 0.7f));
                    sub.Add(Poly(0.5f, 0.76f, 0.56f, 0.7f, 0.5f, 0.62f, 0.44f, 0.7f));
                    break;

                case ItemType.Weapon:
                    // Sword laid diagonally: blade, crossguard, grip, pommel.
                    add.Add(Capsule(0.3f, 0.3f, 0.86f, 0.86f, 0.1f));
                    add.Add(Capsule(0.18f, 0.44f, 0.44f, 0.18f, 0.07f));
                    add.Add(Capsule(0.28f, 0.28f, 0.16f, 0.16f, 0.06f));
                    add.Add(Circle(0.12f, 0.12f, 0.06f));
                    break;

                case ItemType.Shield:
                    // Heater shield with a raised rim and a central boss.
                    add.Add(Poly(0.2f, 0.86f, 0.8f, 0.86f, 0.8f, 0.52f, 0.5f, 0.1f, 0.2f, 0.52f));
                    sub.Add(Poly(0.29f, 0.77f, 0.71f, 0.77f, 0.71f, 0.55f, 0.5f, 0.24f, 0.29f, 0.55f));
                    top.Add(Circle(0.5f, 0.56f, 0.1f));
                    break;

                case ItemType.Quiver:
                    // A tall tube with three fletched arrows standing out of the top.
                    add.Add(Rect(0.32f, 0.08f, 0.62f, 0.66f));
                    add.Add(Capsule(0.38f, 0.66f, 0.3f, 0.9f, 0.025f));
                    add.Add(Capsule(0.47f, 0.66f, 0.47f, 0.94f, 0.025f));
                    add.Add(Capsule(0.56f, 0.66f, 0.66f, 0.9f, 0.025f));
                    add.Add(Poly(0.24f, 0.86f, 0.34f, 0.86f, 0.3f, 0.96f));
                    add.Add(Poly(0.42f, 0.9f, 0.52f, 0.9f, 0.47f, 1f));
                    add.Add(Poly(0.62f, 0.86f, 0.72f, 0.86f, 0.66f, 0.96f));
                    sub.Add(Rect(0.32f, 0.56f, 0.62f, 0.6f));
                    break;
            }
        }
    }
}
