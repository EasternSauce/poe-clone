using System.IO;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Builds the item icons from the painted source art in assets_for_inspiration.
    /// The source files are only ever read. Each icon is recoloured to match the gear's 3D model,
    /// given the same outline / contrast / saturation as every other icon, fitted to the item's
    /// cell size, and written to Assets/Resources/ItemIcons as a new PNG.
    /// Re-runnable: PoeClone > Build Item Icons.
    /// </summary>
    public static class ItemIconBuilder
    {
        private const string SourceRoot = "Assets/assets_for_inspiration/38_free_fantasy_icons";
        private const string OutDir = "Assets/Resources/ItemIcons";
        private const int PxPerCell = 96;

        private static readonly Color OutlineColor = new Color(0.06f, 0.045f, 0.035f, 1f);

        private sealed class Bitmap
        {
            public int W;
            public int H;
            public Color[] P;

            public Bitmap(int w, int h)
            {
                W = w;
                H = h;
                P = new Color[w * h];
            }
        }

        private delegate Bitmap Recolor(Bitmap b);

        [MenuItem("PoeClone/Build Item Icons")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), OutDir));

            Color bronzeDark = new Color(0.16f, 0.09f, 0.04f);
            Color bronzeMid = new Color(0.66f, 0.42f, 0.18f);
            Color bronzeLight = new Color(0.98f, 0.80f, 0.46f);
            Color ropeDark = new Color(0.20f, 0.14f, 0.07f);
            Color ropeMid = new Color(0.66f, 0.52f, 0.32f);
            Color ropeLight = new Color(0.95f, 0.86f, 0.66f);

            Make("iron_helmet", "Armor/512x512/medium_head_armor_03.png", 2, 2, b => b);
            Make("bronze_helmet", "Armor/512x512/heavy_head_armor_03.png", 2, 2, b => GradientMap(b, bronzeDark, bronzeMid, bronzeLight, true));
            Make("studded_vest", "Armor/512x512/medium_body_armor_03.png", 2, 3, b => b);
            Make("leather_gloves", "Armor/512x512/medium_hand_armor_03.png", 2, 2, b => b);
            Make("leather_boots", "Armor/512x512/medium_foot_armor_03.png", 2, 2, b => b);
            Make("rope_belt", "Armor/512x512/medium_belt_03.png", 2, 1, b => GradientMap(b, ropeDark, ropeMid, ropeLight, true));
            Make("wooden_shield", "Armor/512x512/light_shield_03.png", 2, 2, b => b);

            // The sword art lies diagonally (tip up-right). Stand it upright for a 1x3 item, then rust the blade.
            Make("rusty_sword", "Weapons/512x512/one-handed_sword_03.png", 1, 3, b => Rustify(b), 45f);

            // One ring band, three gems.
            Make("iron_ring", "Armor/512x512/heavy_belt_03.png", 1, 1, b => ShiftGem(b, 0.60f, 0.06f, 0.55f, 0.72f));
            Make("ruby_ring", "Armor/512x512/heavy_belt_03.png", 1, 1, b => ShiftGem(b, 0.985f, 1.05f, 1.0f, 1.0f));
            Make("sapphire_ring", "Armor/512x512/heavy_belt_03.png", 1, 1, b => ShiftGem(b, 0.60f, 1.0f, 1.0f, 1.0f));

            MakeAmulet();

            AssetDatabase.Refresh();
            Debug.Log("ItemIconBuilder: icons written to " + OutDir);
        }

        // ------------------------------------------------------------------ pipeline

        private static void Make(string id, string sourceRel, int cellsW, int cellsH, Recolor recolor, float rotateDegrees = 0f)
        {
            Bitmap b = Load(Path.Combine(SourceRoot, sourceRel));
            if (Mathf.Abs(rotateDegrees) > 0.01f)
                b = Rotate(b, rotateDegrees);
            b = Trim(b);
            b = recolor(b);
            Finish(id, b, cellsW, cellsH);
        }

        private static void Finish(string id, Bitmap b, int cellsW, int cellsH)
        {
            Unify(b);

            int outW = cellsW * PxPerCell;
            int outH = cellsH * PxPerCell;
            int outline = Mathf.Max(3, outH / 60);
            b = FitTo(b, outW, outH, outline + 3);
            b = AddOutline(b, outline, OutlineColor);
            Save(id, b);
        }

        // Shared look for every icon so the set reads as one family.
        private static void Unify(Bitmap b)
        {
            for (int i = 0; i < b.P.Length; i++)
            {
                Color c = b.P[i];
                if (c.a <= 0f)
                    continue;

                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                s = Mathf.Clamp01(s * 0.92f);
                v = Mathf.Clamp01((v - 0.5f) * 1.08f + 0.5f);
                Color o = Color.HSVToRGB(h, s, v);
                o.a = c.a;
                b.P[i] = o;
            }
        }

        // ------------------------------------------------------------------ recoloring

        private static bool IsGem(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            return h >= 0.25f && h <= 0.55f && s > 0.35f && v > 0.2f;
        }

        private static float Luma(Color c)
        {
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }

        // Replaces colours with a three-stop ramp driven by brightness, keeping the painted shading.
        private static Bitmap GradientMap(Bitmap b, Color dark, Color mid, Color light, bool keepGems)
        {
            for (int i = 0; i < b.P.Length; i++)
            {
                Color c = b.P[i];
                if (c.a <= 0f)
                    continue;
                if (keepGems && IsGem(c))
                    continue;

                float t = Mathf.InverseLerp(0.10f, 0.95f, Luma(c));
                Color o = t < 0.5f ? Color.Lerp(dark, mid, t * 2f) : Color.Lerp(mid, light, (t - 0.5f) * 2f);
                o.a = c.a;
                b.P[i] = o;
            }
            return b;
        }

        // Moves the gem's hue (and optionally mutes it), leaving the metal untouched.
        private static Bitmap ShiftGem(Bitmap b, float hue, float satMul, float valMul, float bandDim)
        {
            for (int i = 0; i < b.P.Length; i++)
            {
                Color c = b.P[i];
                if (c.a <= 0f)
                    continue;

                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);

                if (IsGem(c))
                {
                    float nh = hue + (h - 0.40f) * 0.25f;
                    nh -= Mathf.Floor(nh);
                    Color o = Color.HSVToRGB(nh, Mathf.Clamp01(s * satMul), Mathf.Clamp01(v * valMul));
                    o.a = c.a;
                    b.P[i] = o;
                }
                else if (bandDim < 0.99f)
                {
                    // Iron: a slightly darker, duller band.
                    Color o = Color.HSVToRGB(h, s * 0.6f, v * bandDim);
                    o.a = c.a;
                    b.P[i] = o;
                }
            }
            return b;
        }

        // Dulls the steel and blooms patches of rust over it. Gold hilt and gem are left alone.
        private static Bitmap Rustify(Bitmap b)
        {
            Color rust = new Color(0.62f, 0.30f, 0.14f);

            for (int y = 0; y < b.H; y++)
            {
                for (int x = 0; x < b.W; x++)
                {
                    Color c = b.P[y * b.W + x];
                    if (c.a <= 0f || IsGem(c))
                        continue;

                    float h, s, v;
                    Color.RGBToHSV(c, out h, out s, out v);
                    bool metal = s < 0.22f && Luma(c) > 0.22f;
                    if (!metal)
                        continue;

                    float n = Fbm(x / (float)b.W * 4f, y / (float)b.H * 9f, 7);
                    float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, n));
                    float l = Luma(c);
                    Color dull = Color.Lerp(new Color(l, l, l), new Color(0.58f, 0.52f, 0.46f) * (0.5f + l * 0.7f), 0.55f);
                    Color rusted = rust * (0.55f + l * 0.65f);
                    Color o = Color.Lerp(dull, rusted, mask);
                    o.a = c.a;
                    b.P[y * b.W + x] = o;
                }
            }
            return b;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(x0, y0, seed);
            float bb = Hash01(x0 + 1, y0, seed);
            float c = Hash01(x0, y0 + 1, seed);
            float d = Hash01(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, bb, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float x, float y, int seed)
        {
            return 0.5f * ValueNoise(x, y, seed) + 0.3f * ValueNoise(x * 2f, y * 2f, seed + 1) + 0.2f * ValueNoise(x * 4f, y * 4f, seed + 2);
        }

        // ------------------------------------------------------------------ the amulet

        // No amulet art exists in the source pack, so build one from its own parts:
        // the gem from the ring band, set in a silver bezel, hanging on a silver chain.
        private static void MakeAmulet()
        {
            Bitmap band = Load(Path.Combine(SourceRoot, "Armor/512x512/heavy_belt_03.png"));

            // Find the gem.
            int minX = band.W, minY = band.H, maxX = -1, maxY = -1;
            for (int y = 0; y < band.H; y++)
            {
                for (int x = 0; x < band.W; x++)
                {
                    Color c = band.P[y * band.W + x];
                    if (c.a > 0.5f && IsGem(c))
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            const int size = 480;
            Bitmap canvas = new Bitmap(size, size);

            Vector2 pendant = new Vector2(size * 0.5f, size * 0.66f);
            float gemRadius = 62f;
            float bezelOuter = 96f;
            float bezelInner = 74f;

            // Chain: a catenary from each top corner down to the top of the pendant.
            Vector2 anchorL = new Vector2(size * 0.10f, size - 20f);
            Vector2 anchorR = new Vector2(size * 0.90f, size - 20f);
            Vector2 chainEnd = new Vector2(pendant.x, pendant.y + bezelOuter - 4f);
            DrawChain(canvas, anchorL, chainEnd, 9f);
            DrawChain(canvas, anchorR, chainEnd, 9f);

            // Bezel: shaded silver ring around a dark well.
            Vector2 light = new Vector2(-0.6f, 0.8f).normalized;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - pendant;
                    float r = d.magnitude;
                    if (r > bezelOuter)
                        continue;

                    Color col;
                    if (r > bezelInner)
                    {
                        float lit = 0.5f + 0.5f * Vector2.Dot(d.normalized, light);
                        float edge = Mathf.InverseLerp(bezelInner, bezelOuter, r);
                        col = Color.Lerp(new Color(0.42f, 0.44f, 0.50f), new Color(0.92f, 0.94f, 0.98f), lit * (1f - 0.35f * edge));
                    }
                    else
                    {
                        col = new Color(0.10f, 0.46f, 0.28f);
                    }
                    col.a = 1f;
                    canvas.P[y * size + x] = col;
                }
            }

            // Paste the real gem art into the bezel.
            if (maxX > minX && maxY > minY)
            {
                float gw = maxX - minX + 1;
                float gh = maxY - minY + 1;
                float scale = gemRadius * 2f / Mathf.Max(gw, gh);
                Vector2 gemCenter = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - pendant;
                        if (d.magnitude > gemRadius)
                            continue;

                        float sx = gemCenter.x + d.x / scale;
                        float sy = gemCenter.y + d.y / scale;
                        Color g = SampleStraight(band, sx, sy);
                        if (g.a < 0.2f || !IsGem(g))
                            continue;

                        Color under = canvas.P[y * size + x];
                        canvas.P[y * size + x] = new Color(g.r, g.g, g.b, 1f) * g.a + under * (1f - g.a);
                        canvas.P[y * size + x].a = 1f;
                    }
                }
            }

            Finish("jade_amulet", Trim(canvas), 1, 1);
        }

        private static void DrawChain(Bitmap canvas, Vector2 from, Vector2 to, float linkRadius)
        {
            const int links = 11;
            for (int i = 0; i <= links; i++)
            {
                float t = i / (float)links;
                // Slight sag for a hanging-chain look.
                Vector2 p = Vector2.Lerp(from, to, t) + new Vector2(0f, -Mathf.Sin(t * Mathf.PI) * 26f);
                bool flat = i % 2 == 0;
                float rx = flat ? linkRadius * 1.25f : linkRadius * 0.7f;
                float ry = flat ? linkRadius * 0.7f : linkRadius * 1.25f;
                DrawLink(canvas, p, rx, ry);
            }
        }

        private static void DrawLink(Bitmap canvas, Vector2 center, float rx, float ry)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - rx - 2f));
            int x1 = Mathf.Min(canvas.W - 1, Mathf.CeilToInt(center.x + rx + 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - ry - 2f));
            int y1 = Mathf.Min(canvas.H - 1, Mathf.CeilToInt(center.y + ry + 2f));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - center.x) / rx;
                    float dy = (y + 0.5f - center.y) / ry;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 1f || r < 0.45f)
                        continue;

                    float lit = 0.5f - 0.5f * (dx * 0.6f - dy * 0.8f);
                    Color col = Color.Lerp(new Color(0.40f, 0.42f, 0.48f), new Color(0.90f, 0.92f, 0.96f), Mathf.Clamp01(lit));
                    col.a = 1f;
                    canvas.P[y * canvas.W + x] = col;
                }
            }
        }

        // ------------------------------------------------------------------ bitmap plumbing

        private static Bitmap Load(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(full));

            Bitmap b = new Bitmap(t.width, t.height);
            b.P = t.GetPixels();
            Object.DestroyImmediate(t);
            return b;
        }

        private static Color PxPremul(Bitmap b, int x, int y)
        {
            if (x < 0 || y < 0 || x >= b.W || y >= b.H)
                return Color.clear;
            Color c = b.P[y * b.W + x];
            return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
        }

        // Bilinear sample returning a straight (un-premultiplied) colour.
        private static Color SampleStraight(Bitmap b, float x, float y)
        {
            x -= 0.5f;
            y -= 0.5f;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            Color p = Color.Lerp(
                Color.Lerp(PxPremul(b, x0, y0), PxPremul(b, x0 + 1, y0), fx),
                Color.Lerp(PxPremul(b, x0, y0 + 1), PxPremul(b, x0 + 1, y0 + 1), fx),
                fy);

            if (p.a <= 0.0001f)
                return Color.clear;
            return new Color(p.r / p.a, p.g / p.a, p.b / p.a, p.a);
        }

        private static Bitmap Rotate(Bitmap src, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            int nw = Mathf.CeilToInt(Mathf.Abs(src.W * cos) + Mathf.Abs(src.H * sin));
            int nh = Mathf.CeilToInt(Mathf.Abs(src.W * sin) + Mathf.Abs(src.H * cos));

            Bitmap dst = new Bitmap(nw, nh);
            for (int y = 0; y < nh; y++)
            {
                for (int x = 0; x < nw; x++)
                {
                    float dx = x + 0.5f - nw * 0.5f;
                    float dy = y + 0.5f - nh * 0.5f;
                    float sx = cos * dx + sin * dy + src.W * 0.5f;
                    float sy = -sin * dx + cos * dy + src.H * 0.5f;
                    dst.P[y * nw + x] = SampleStraight(src, sx, sy);
                }
            }
            return dst;
        }

        private static Bitmap Trim(Bitmap b)
        {
            int minX = b.W, minY = b.H, maxX = -1, maxY = -1;
            for (int y = 0; y < b.H; y++)
            {
                for (int x = 0; x < b.W; x++)
                {
                    if (b.P[y * b.W + x].a > 0.05f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
                return b;

            Bitmap o = new Bitmap(maxX - minX + 1, maxY - minY + 1);
            for (int y = 0; y < o.H; y++)
            {
                for (int x = 0; x < o.W; x++)
                    o.P[y * o.W + x] = b.P[(y + minY) * b.W + (x + minX)];
            }
            return o;
        }

        // Halve with a 2x2 box filter in premultiplied space.
        private static Bitmap Half(Bitmap b)
        {
            Bitmap o = new Bitmap(Mathf.Max(1, b.W / 2), Mathf.Max(1, b.H / 2));
            for (int y = 0; y < o.H; y++)
            {
                for (int x = 0; x < o.W; x++)
                {
                    Color sum = PxPremul(b, x * 2, y * 2) + PxPremul(b, x * 2 + 1, y * 2) + PxPremul(b, x * 2, y * 2 + 1) + PxPremul(b, x * 2 + 1, y * 2 + 1);
                    sum *= 0.25f;
                    o.P[y * o.W + x] = sum.a <= 0.0001f ? Color.clear : new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a);
                }
            }
            return o;
        }

        // Scales to fit inside outW x outH (keeping aspect), centred, with padding.
        private static Bitmap FitTo(Bitmap b, int outW, int outH, int pad)
        {
            float scale = Mathf.Min((outW - 2f * pad) / b.W, (outH - 2f * pad) / b.H);
            while (scale < 0.5f)
            {
                b = Half(b);
                scale *= 2f;
            }

            int w = Mathf.Max(1, Mathf.RoundToInt(b.W * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(b.H * scale));
            int ox = (outW - w) / 2;
            int oy = (outH - h) / 2;

            Bitmap o = new Bitmap(outW, outH);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + (sx + 0.5f) * 0.5f) / scale;
                            float v = (y + (sy + 0.5f) * 0.5f) / scale;
                            Color s = SampleStraight(b, u, v);
                            sum += new Color(s.r * s.a, s.g * s.a, s.b * s.a, s.a);
                        }
                    }
                    sum *= 0.25f;
                    o.P[(y + oy) * outW + (x + ox)] = sum.a <= 0.0001f ? Color.clear : new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a);
                }
            }
            return o;
        }

        // Dark outline around the silhouette, the same on every icon.
        private static Bitmap AddOutline(Bitmap b, int radius, Color color)
        {
            Bitmap o = new Bitmap(b.W, b.H);
            float r2 = radius * radius;

            for (int y = 0; y < b.H; y++)
            {
                for (int x = 0; x < b.W; x++)
                {
                    Color c = b.P[y * b.W + x];
                    if (c.a >= 0.98f)
                    {
                        o.P[y * b.W + x] = c;
                        continue;
                    }

                    // Nearest opaque neighbour within the radius decides how strong the outline is here.
                    float best = 0f;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= b.H)
                            continue;
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            float d2 = dx * dx + dy * dy;
                            if (d2 > r2)
                                continue;
                            int xx = x + dx;
                            if (xx < 0 || xx >= b.W)
                                continue;

                            float a = b.P[yy * b.W + xx].a;
                            if (a > 0.5f)
                            {
                                float fall = 1f - Mathf.Sqrt(d2) / (radius + 0.75f);
                                if (fall > best)
                                    best = fall;
                            }
                        }
                    }

                    float outlineAlpha = Mathf.Clamp01(best * 1.6f);
                    if (c.a > 0f)
                    {
                        // Partially covered edge pixel: blend the art over the outline.
                        Color under = new Color(color.r, color.g, color.b, Mathf.Max(outlineAlpha, 0.9f));
                        float a = c.a + under.a * (1f - c.a);
                        Color mixed = (c * c.a + under * under.a * (1f - c.a)) / Mathf.Max(a, 0.0001f);
                        mixed.a = a;
                        o.P[y * b.W + x] = mixed;
                    }
                    else
                    {
                        o.P[y * b.W + x] = new Color(color.r, color.g, color.b, outlineAlpha);
                    }
                }
            }
            return o;
        }

        private static void Save(string id, Bitmap b)
        {
            Texture2D tex = new Texture2D(b.W, b.H, TextureFormat.RGBA32, false);
            tex.SetPixels(b.P);
            tex.Apply();

            string assetPath = OutDir + "/" + id + ".png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spritePixelsPerUnit = 100f;
                ti.SaveAndReimport();
            }
        }
    }
}
