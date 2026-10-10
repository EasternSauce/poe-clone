using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    // The game's look: fonts, ornate frames and buttons, and the animated menu backdrop.
    // Every sprite is drawn here at runtime like the rest of the kit (see MakeGrain).
    public static partial class UiKit
    {
        public static readonly Color Ember = new Color(1f, 0.55f, 0.22f, 1f);
        public static readonly Color DangerTint = new Color(1f, 0.55f, 0.5f, 1f);
        public static readonly Color MutedTint = new Color(0.72f, 0.72f, 0.76f, 1f);

        private static Font font, boldFont, titleFont, displayFont;

        /// <summary>Body text: Alegreya Sans.</summary>
        public static Font Font => font != null ? font : font = LoadFont("AlegreyaSans-Medium");

        public static Font BoldFont => boldFont != null ? boldFont : boldFont = LoadFont("AlegreyaSans-Bold");

        /// <summary>Headings and buttons: Cinzel.</summary>
        public static Font TitleFont => titleFont != null ? titleFont : titleFont = LoadFont("Cinzel-Bold");

        /// <summary>Big screen titles: Cinzel Decorative.</summary>
        public static Font DisplayFont => displayFont != null ? displayFont : displayFont = LoadFont("CinzelDecorative-Bold");

        private static Font LoadFont(string name)
        {
            Font loaded = Resources.Load<Font>("Fonts/" + name);
            return loaded != null ? loaded : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Turns a panel title into a Cinzel heading with a soft drop shadow.</summary>
        public static Text Heading(Text text)
        {
            text.font = TitleFont;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Sprite frame, thinFrame, barFrame, barFill, button, vignette, glow, divider, diamond;
        private static Texture2D fog;

        private const int FrameSize = 64;
        private const int FrameBorder = 22;
        private const int ButtonSize = 64;
        private const int ButtonBorder = 18;
        private const float ButtonChamfer = 9f;

        /// <summary>A gilded border with diamond studs in the corners (sliced, hollow middle).</summary>
        public static Sprite FrameSprite => frame != null ? frame : frame = MakeFrame();

        /// <summary>A slim gilded border with small corner studs (sliced, hollow), for tooltips and HUD panels.</summary>
        public static Sprite ThinFrameSprite => thinFrame != null ? thinFrame : thinFrame = MakeThinFrame(40, 12, true);

        /// <summary>A slim gilded rim without studs (sliced, hollow), around resource bars and slots.</summary>
        public static Sprite BarFrameSprite => barFrame != null ? barFrame : barFrame = MakeThinFrame(24, 6, false);

        /// <summary>A white vertical sheen (bright top, darker bottom) to tint as a bar's fill.</summary>
        public static Sprite BarFillSprite => barFill != null ? barFill : barFill = MakeBarFill();

        /// <summary>A bevelled, corner-cut button plate (sliced).</summary>
        public static Sprite ButtonSprite => button != null ? button : button = MakeButton();

        /// <summary>Black at the edges, clear in the middle; stretch it over a whole screen.</summary>
        public static Sprite Vignette => vignette != null ? vignette : vignette = MakeVignette();

        /// <summary>A soft round glow (white, fades to clear), for embers and halos.</summary>
        public static Sprite Glow => glow != null ? glow : glow = MakeGlow();

        /// <summary>A gold rule that fades out at both ends, with a diamond in the middle.</summary>
        public static Sprite Divider => divider != null ? divider : divider = MakeDivider();

        /// <summary>A small solid diamond.</summary>
        public static Sprite Diamond => diamond != null ? diamond : diamond = MakeDiamond();

        /// <summary>Tileable soft smoke (white, alpha varies), for scrolling fog layers.</summary>
        public static Texture2D Fog => fog != null ? fog : fog = MakeFog();

        /// <summary>
        /// Gives a panel the gilded frame (instead of a flat outline). The frame sits a little
        /// outside the panel so its corner studs don't crowd the content.
        /// </summary>
        public static Image Frame(RectTransform panel, float outset = 5f)
        {
            Image border = NewImage("Frame", panel, Color.white);
            border.sprite = FrameSprite;
            border.type = Image.Type.Sliced;
            border.fillCenter = false;
            Stretch(border.rectTransform, -outset);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return border;
        }

        /// <summary>The slim frame (see <see cref="ThinFrameSprite"/>) round a tooltip or small HUD panel.</summary>
        public static Image ThinFrame(RectTransform panel, float outset = 2f)
        {
            Image border = NewImage("Frame", panel, Color.white);
            border.sprite = ThinFrameSprite;
            border.type = Image.Type.Sliced;
            border.fillCenter = false;
            Stretch(border.rectTransform, -outset);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return border;
        }

        /// <summary>A slim gilded rim (see <see cref="BarFrameSprite"/>) round a slot or bar, tinted <paramref name="tint"/>.</summary>
        public static Image Rim(RectTransform target, float outset = 2f, Color? tint = null)
        {
            Image rim = NewImage("Rim", target, tint ?? Color.white);
            rim.sprite = BarFrameSprite;
            rim.type = Image.Type.Sliced;
            rim.fillCenter = false;
            Stretch(rim.rectTransform, -outset);
            rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return rim;
        }

        /// <summary>
        /// Dresses a button image (and its label) in the theme: bevelled plate, Cinzel label and
        /// a hover glow with a small grow/press animation. <paramref name="tint"/> recolours the
        /// plate (e.g. <see cref="DangerTint"/> for destructive actions).
        /// </summary>
        public static void StyleButton(Image plate, Text label, Color? tint = null)
        {
            plate.sprite = ButtonSprite;
            plate.type = Image.Type.Sliced;
            plate.color = tint ?? Color.white;
            if (label != null)
            {
                label.font = TitleFont;
                label.color = TextColor;
                if (label.GetComponent<Shadow>() == null)
                {
                    Shadow shadow = label.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
                    shadow.effectDistance = new Vector2(1.5f, -1.5f);
                }
            }

            Button uiButton = plate.GetComponent<Button>();
            if (uiButton != null)
                uiButton.transition = Selectable.Transition.None;

            Image hover = NewImage("Hover", plate.transform, new Color(1f, 0.78f, 0.4f, 0f));
            hover.sprite = ButtonSprite;
            hover.type = Image.Type.Sliced;
            Stretch(hover.rectTransform, 0f);
            hover.transform.SetSiblingIndex(0);

            UiButtonFx fx = plate.GetComponent<UiButtonFx>();
            if (fx == null) fx = plate.gameObject.AddComponent<UiButtonFx>();
            fx.Init(hover, label);
        }

        /// <summary>A centred gold divider under a heading.</summary>
        public static Image DividerLine(Transform parent, Vector2 at, float width)
        {
            Image line = NewImage("Divider", parent, Gold);
            line.sprite = Divider;
            RectTransform rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(width, 18f);
            return line;
        }

        /// <summary>
        /// The animated full-screen backdrop for menus and loading: dark stone, two drifting fog
        /// layers, a slow ember glow from below, rising embers and a vignette. It animates on
        /// unscaled time, so it keeps moving while the game is paused.
        /// </summary>
        public static RectTransform Backdrop(Transform parent, string name = "Background")
        {
            Image baseImage = NewImage(name, parent, new Color(0.075f, 0.062f, 0.055f, 1f));
            Grain(baseImage);
            baseImage.raycastTarget = true;
            RectTransform root = baseImage.rectTransform;
            Stretch(root, 0f);

            Image heat = NewImage("Heat", root, new Color(Ember.r, Ember.g * 0.7f, Ember.b * 0.5f, 0.16f));
            heat.sprite = Glow;
            RectTransform hr = heat.rectTransform;
            hr.anchorMin = new Vector2(-0.2f, -0.55f);
            hr.anchorMax = new Vector2(1.2f, 0.55f);
            hr.offsetMin = hr.offsetMax = Vector2.zero;
            heat.gameObject.AddComponent<UiPulse>().Init(heat, 0.10f, 0.20f, 0.35f);

            FogLayer(root, new Color(0.62f, 0.55f, 0.48f, 0.13f), new Vector2(0.012f, 0.004f), 1.6f);
            FogLayer(root, new Color(0.45f, 0.38f, 0.33f, 0.11f), new Vector2(-0.02f, 0.008f), 0.9f);

            RectTransform embers = NewRect("Embers", root);
            Stretch(embers, 0f);
            embers.gameObject.AddComponent<EmberField>();

            Image edge = NewImage("Vignette", root, Color.white);
            edge.sprite = Vignette;
            Stretch(edge.rectTransform, 0f);
            return root;
        }

        private static void FogLayer(RectTransform parent, Color color, Vector2 drift, float tiles)
        {
            GameObject go = new GameObject("Fog", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage raw = go.GetComponent<RawImage>();
            raw.texture = Fog;
            raw.color = color;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform, 0f);
            go.AddComponent<FogDrift>().Init(raw, drift, tiles);
        }

        /// <summary>
        /// Gives a hollow-ring spinner with a counter-rotating ring of diamond studs inside it,
        /// for loading and waiting screens.
        /// </summary>
        public static RectTransform RuneSpinner(Transform parent, Vector2 at, float size)
        {
            RectTransform root = NewRect("RuneSpinner", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = at;
            root.sizeDelta = new Vector2(size, size);

            Image halo = NewImage("Halo", root, new Color(Ember.r, Ember.g, Ember.b, 0.25f));
            halo.sprite = Glow;
            Stretch(halo.rectTransform, -size * 0.45f);
            halo.gameObject.AddComponent<UiPulse>().Init(halo, 0.12f, 0.3f, 0.8f);

            Image ring = NewImage("Ring", root, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            ring.sprite = Ring;
            Stretch(ring.rectTransform, 0f);

            RectTransform outer = NewRect("Outer", root);
            Stretch(outer, 0f);
            for (int i = 0; i < 8; i++)
                Stud(outer, i * 45f, size * 0.5f, i % 2 == 0 ? size * 0.16f : size * 0.09f, 0.95f);
            outer.gameObject.AddComponent<UiSpin>().speed = -40f;

            RectTransform inner = NewRect("Inner", root);
            Stretch(inner, size * 0.22f);
            Image innerRing = NewImage("InnerRing", inner, new Color(Gold.r, Gold.g, Gold.b, 0.45f));
            innerRing.sprite = Ring;
            Stretch(innerRing.rectTransform, 0f);
            for (int i = 0; i < 6; i++)
                Stud(inner, i * 60f, size * 0.28f, size * 0.07f, 0.7f);
            inner.gameObject.AddComponent<UiSpin>().speed = 70f;

            Image core = NewImage("Core", root, new Color(1f, 0.7f, 0.35f, 0.9f));
            core.sprite = Diamond;
            RectTransform cr = core.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(size * 0.14f, size * 0.2f);
            core.gameObject.AddComponent<UiPulse>().Init(core, 0.5f, 1f, 1.4f);
            return root;
        }

        private static void Stud(RectTransform parent, float angle, float radius, float size, float alpha)
        {
            Image stud = NewImage("Stud", parent, new Color(Gold.r, Gold.g, Gold.b, alpha));
            stud.sprite = Diamond;
            RectTransform rt = stud.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float rad = angle * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
            rt.localEulerAngles = new Vector3(0f, 0f, -angle);
            rt.sizeDelta = new Vector2(size * 0.6f, size);
        }

        // --- Sprite drawing ---

        private static readonly Color DarkEdge = new Color(0.03f, 0.02f, 0.015f, 1f);
        private static readonly Color GoldLight = new Color(0.98f, 0.86f, 0.55f, 1f);
        private static readonly Color GoldDark = new Color(0.42f, 0.30f, 0.13f, 1f);

        private static Sprite MakeFrame()
        {
            const int s = FrameSize;
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dl = px, dr = s - px, db = py, dt = s - py;
                    float d = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                    Color c = Color.clear;

                    if (d < 1.2f) c = DarkEdge;
                    else if (d < 4.5f)
                    {
                        // Metal band: bright in the middle, lit from the top-left.
                        float across = 1f - Mathf.Abs((d - 2.85f) / 1.65f);
                        bool lit = Mathf.Min(dl, dt) <= Mathf.Min(dr, db);
                        c = Color.Lerp(GoldDark, GoldLight, across * (lit ? 0.95f : 0.6f));
                    }
                    else if (d < 5.6f) c = DarkEdge;
                    else if (d >= 8f && d < 9f) c = new Color(GoldDark.r, GoldDark.g, GoldDark.b, 0.75f);

                    // Corner ornaments: a stud on the band corner and a bracket inside it.
                    float cx = Mathf.Min(dl, dr), cy = Mathf.Min(db, dt);
                    if (cx < FrameBorder && cy < FrameBorder)
                    {
                        float bracket = Mathf.Max(Mathf.Abs(cx - 12f), Mathf.Abs(cy - 12f));
                        if (bracket < 0.6f && cx >= 9f && cy >= 9f && cx <= 16f && cy <= 16f)
                            c = Color.Lerp(GoldDark, GoldLight, 0.4f);
                        float diamondDist = Mathf.Abs(cx - 4f) + Mathf.Abs(cy - 4f);
                        if (diamondDist < 8f)
                        {
                            float shade = 1f - diamondDist / 8f;
                            c = diamondDist > 6.6f ? DarkEdge : Color.Lerp(GoldDark, GoldLight, 0.35f + shade * 0.65f);
                            if (diamondDist < 2.2f) c = new Color(0.55f, 0.12f, 0.06f, 1f);
                        }
                    }
                    pixels[y * s + x] = c;
                }
            }
            return MakeColorSprite(pixels, s, Vector4.one * FrameBorder);
        }

        // A dark edge, a gold band lit from the top-left, a dark inner line; studs optional.
        private static Sprite MakeThinFrame(int s, int border, bool studs)
        {
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dl = px, dr = s - px, db = py, dt = s - py;
                    float d = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                    Color c = Color.clear;
                    if (d < 1f) c = DarkEdge;
                    else if (d < 3f)
                    {
                        float across = 1f - Mathf.Abs((d - 2f) / 1f);
                        bool lit = Mathf.Min(dl, dt) <= Mathf.Min(dr, db);
                        c = Color.Lerp(GoldDark, GoldLight, across * (lit ? 0.9f : 0.55f));
                    }
                    else if (d < 4f) c = new Color(DarkEdge.r, DarkEdge.g, DarkEdge.b, 0.8f);

                    if (studs)
                    {
                        float cx = Mathf.Min(dl, dr), cy = Mathf.Min(db, dt);
                        float diamondDist = Mathf.Abs(cx - 3f) + Mathf.Abs(cy - 3f);
                        if (diamondDist < 6f)
                        {
                            float shade = 1f - diamondDist / 6f;
                            c = diamondDist > 4.8f ? DarkEdge : Color.Lerp(GoldDark, GoldLight, 0.35f + shade * 0.65f);
                            if (diamondDist < 1.6f) c = new Color(0.55f, 0.12f, 0.06f, 1f);
                        }
                    }
                    pixels[y * s + x] = c;
                }
            }
            return MakeColorSprite(pixels, s, Vector4.one * border);
        }

        private static Sprite MakeBarFill()
        {
            const int w = 4, h = 32;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float b = Mathf.Lerp(0.55f, 1f, v);
                if (v > 0.62f && v < 0.82f) b = Mathf.Min(1f, b + 0.18f); // a glassy highlight
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = new Color(b, b, b, 1f);
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeButton()
        {
            const int s = ButtonSize;
            var pixels = new Color[s * s];
            Color top = new Color(0.34f, 0.25f, 0.15f, 1f);
            Color bottom = new Color(0.13f, 0.09f, 0.06f, 1f);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float ex = Mathf.Min(px, s - px), ey = Mathf.Min(py, s - py);
                    // Distance inside an octagon (rectangle with cut corners).
                    float e = Mathf.Min(Mathf.Min(ex, ey), (ex + ey - ButtonChamfer) * 0.7071f);
                    if (e <= 0f) { pixels[y * s + x] = Color.clear; continue; }

                    Color c;
                    if (e < 1.3f) c = DarkEdge;
                    else if (e < 3.6f)
                    {
                        float across = 1f - Mathf.Abs((e - 2.45f) / 1.15f);
                        bool lit = py > s * 0.5f;
                        c = Color.Lerp(GoldDark, GoldLight, across * (lit ? 0.85f : 0.45f));
                    }
                    else if (e < 4.6f) c = DarkEdge;
                    else
                    {
                        float v = py / s;
                        c = Color.Lerp(bottom, top, v * v);
                        // A faint inner sheen just under the top rim.
                        if (py > s - 9f) c = Color.Lerp(c, GoldLight, 0.06f);
                    }
                    c.a = Mathf.Clamp01(e * 1.5f);
                    pixels[y * s + x] = c;
                }
            }
            return MakeColorSprite(pixels, s, Vector4.one * ButtonBorder);
        }

        private static Sprite MakeVignette()
        {
            const int s = 128;
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                    pixels[y * s + x] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.92f, (r - 0.45f) / 0.85f));
                }
            }
            return MakeColorSprite(pixels, s, Vector4.zero);
        }

        private static Sprite MakeGlow()
        {
            const int s = 64;
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float r2 = u * u + v * v;
                    float a = r2 >= 1f ? 0f : Mathf.Exp(-r2 * 4f) * (1f - r2);
                    pixels[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return MakeColorSprite(pixels, s, Vector4.zero);
        }

        private static Sprite MakeDivider()
        {
            const int w = 256, h = 18;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fromCentre = Mathf.Abs(x + 0.5f - w * 0.5f);
                    float dy = Mathf.Abs(y + 0.5f - h * 0.5f);
                    float fade = Mathf.Pow(1f - fromCentre / (w * 0.5f), 0.8f);
                    float a = Mathf.Clamp01(1.4f - dy) * fade;
                    float diamondDist = fromCentre / 1.4f + dy;
                    if (diamondDist < 8.5f)
                        a = diamondDist > 7f ? 0.35f : 1f;
                    // Two small studs either side of the centre.
                    float side = Mathf.Abs(fromCentre - 22f) + dy;
                    if (side < 3.2f) a = 1f;
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeDiamond()
        {
            const int s = 32;
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - s * 0.5f) + Mathf.Abs(y + 0.5f - s * 0.5f);
                    pixels[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(s * 0.5f - d));
                }
            }
            return MakeColorSprite(pixels, s, Vector4.zero);
        }

        private static Texture2D MakeFog()
        {
            const int s = 256;
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)s, v = y / (float)s;
                    float n = TileNoise(u, v, 3f, 5.3f) * 0.55f + TileNoise(u, v, 7f, 41.1f) * 0.3f +
                              TileNoise(u, v, 15f, 90.7f) * 0.15f;
                    pixels[y * s + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, (n - 0.3f) / 0.45f));
                }
            }
            var texture = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Sprite MakeColorSprite(Color[] pixels, int size, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
