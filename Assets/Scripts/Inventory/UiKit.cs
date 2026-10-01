using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>Small helpers for building uGUI at runtime, plus the shared colour scheme.</summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.96f);
        public static readonly Color BorderColor = new Color(0.55f, 0.43f, 0.22f, 1f);
        public static readonly Color Gold = new Color(0.86f, 0.72f, 0.42f, 1f);
        public static readonly Color TextColor = new Color(0.90f, 0.86f, 0.76f, 1f);

        /// <summary>True while a text field (e.g. the chat box) has keyboard focus, so hotkeys must not fire.</summary>
        public static bool IsTypingInTextField()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }
        public static readonly Color DimText = new Color(0.62f, 0.59f, 0.52f, 1f);
        public static readonly Color MagicBlue = new Color(0.53f, 0.53f, 1f, 1f);
        public static readonly Color BonusGreen = new Color(0.50f, 0.84f, 0.50f, 1f);
        public static readonly Color NormalWhite = new Color(0.90f, 0.88f, 0.84f, 1f);
        public static readonly Color RareYellow = new Color(1f, 1f, 0.47f, 1f);

        /// <summary>PoE's item name colours: white Normal, blue Magic, yellow Rare.</summary>
        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return MagicBlue;
                case ItemRarity.Rare: return RareYellow;
                default: return NormalWhite;
            }
        }

        private static Sprite disc;
        private static Sprite ring;
        private static Sprite square;

        /// <summary>A plain white square (Filled images, e.g. cooldown sweeps, need some sprite to fill).</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var pixels = new Color32[16];
                    for (int k = 0; k < pixels.Length; k++)
                        pixels[k] = new Color32(255, 255, 255, 255);
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    square = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
                }
                return square;
            }
        }

        /// <summary>An antialiased white disc, for round buttons and glows.</summary>
        public static Sprite Disc => disc != null ? disc : disc = MakeCircle(128, 0f);

        /// <summary>An antialiased white ring (a disc's outline).</summary>
        public static Sprite Ring => ring != null ? ring : ring = MakeCircle(128, 0.07f);

        // Filled, or a ring whose thickness is a fraction of the size.
        private static Sprite MakeCircle(int size, float ringThickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float radius = size * 0.5f;
            float inner = radius - ringThickness * size;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - d);
                    if (ringThickness > 0f)
                        alpha *= Mathf.Clamp01(d - inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null)
                        font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }

        public static string Hex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text NewText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
        {
            RectTransform rt = NewRect(name, parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = content;
            return t;
        }

        public static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void AddOutline(Graphic g, Color color, float distance)
        {
            Outline o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>A full-screen overlay canvas that scales with the screen and can be faded as a whole.</summary>
        public static Canvas NewCanvas(string name, Transform parent, int sortingOrder, out CanvasGroup group)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<TouchAwareScaler>();

            group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return canvas;
        }
    }
}
