using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>Small helpers for building uGUI at runtime, plus the shared colour scheme.</summary>
    public static partial class UiKit
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.96f);
        public static readonly Color BorderColor = new Color(0.55f, 0.43f, 0.22f, 1f);
        public static readonly Color Gold = new Color(0.86f, 0.72f, 0.42f, 1f);
        public static readonly Color TextColor = new Color(0.90f, 0.86f, 0.76f, 1f);

        /// <summary>True while a text field (e.g. the chat box) has keyboard focus, so hotkeys must not fire.</summary>
        /// <summary>Frame in which a text box already used the Enter press (so the chat doesn't open on it).</summary>
        public static int EnterHandledFrame = -1;

        public static int TextEditEndedFrame = -1;

        // Shared across the inventory and gameplay assemblies.
        public static int ClickConsumedFrame = -1;

        // The full-screen name prompt is supplied by the session UI (a separate assembly).
        public static System.Action<string, System.Action<string>> StashTabNamePrompt;
        public static bool IsStashNamePromptOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStashNamePrompt()
        {
            StashTabNamePrompt = null;
            IsStashNamePromptOpen = false;
        }

        public static bool IsTypingInTextField()
        {
            if (IsStashNamePromptOpen) return true;
            if (TextEditEndedFrame == Time.frameCount) return true;
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
        public static readonly Color UniqueOrange = new Color(0.90f, 0.50f, 0.18f, 1f);

        /// <summary>PoE's item name colours: white Normal, blue Magic, yellow Rare, orange Unique.</summary>
        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return MagicBlue;
                case ItemRarity.Rare: return RareYellow;
                case ItemRarity.Unique: return UniqueOrange;
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

        private static Sprite panelGrain;
        private static Sprite slotInset;

        /// <summary>
        /// A tileable grey grain (scuffed leather / stone) for panel backgrounds: tints like a
        /// plain Image, see <see cref="Grain"/>.
        /// </summary>
        public static Sprite PanelGrain => panelGrain != null ? panelGrain : panelGrain = MakeGrain();

        /// <summary>
        /// A sliced, recessed slot: grainy middle, shadowed top and left inner edges, a light rim at
        /// the bottom and right. For item slots, bag cells, skill slots and buttons; see <see cref="Inset"/>.
        /// </summary>
        public static Sprite SlotInset => slotInset != null ? slotInset : slotInset = MakeInset();

        /// <summary>Gives a panel the grain texture (its colour still tints it).</summary>
        public static void Grain(Image image)
        {
            image.sprite = PanelGrain;
            image.type = Image.Type.Tiled;
        }

        /// <summary>Makes an image a recessed slot (its colour still tints it).</summary>
        public static void Inset(Image image)
        {
            image.sprite = SlotInset;
            image.type = Image.Type.Sliced;
        }

        private const int GrainSize = 128;
        private const int InsetSize = 64;
        private const int InsetBorder = 12;

        // Brightness 0.4-1: a multiplier, so even the darkest panel colours show it.
        private static Sprite MakeGrain()
        {
            var random = new System.Random(7);
            var pixels = new Color32[GrainSize * GrainSize];
            for (int y = 0; y < GrainSize; y++)
            {
                for (int x = 0; x < GrainSize; x++)
                {
                    float v = GrainValue(x, y, random);
                    pixels[y * GrainSize + x] = Grey(v);
                }
            }

            // A few faint scratches.
            for (int k = 0; k < 40; k++)
            {
                int x = random.Next(GrainSize);
                int y = random.Next(GrainSize);
                float angle = (float)random.NextDouble() * Mathf.PI;
                int length = 6 + random.Next(14);
                bool light = random.NextDouble() < 0.5;
                for (int i = 0; i < length; i++)
                {
                    int px = ((x + Mathf.RoundToInt(Mathf.Cos(angle) * i)) % GrainSize + GrainSize) % GrainSize;
                    int py = ((y + Mathf.RoundToInt(Mathf.Sin(angle) * i)) % GrainSize + GrainSize) % GrainSize;
                    int index = py * GrainSize + px;
                    float v = pixels[index].r / 255f;
                    pixels[index] = Grey(light ? Mathf.Min(1f, v + 0.12f) : v - 0.12f);
                }
            }

            return MakeSprite(pixels, GrainSize, TextureWrapMode.Repeat, Vector4.zero);
        }

        private static Sprite MakeInset()
        {
            var random = new System.Random(11);
            var pixels = new Color32[InsetSize * InsetSize];
            for (int y = 0; y < InsetSize; y++)
            {
                for (int x = 0; x < InsetSize; x++)
                {
                    // Texture rows run bottom-up: y = 0 is the bottom edge.
                    float fromLeft = x + 0.5f;
                    float fromTop = InsetSize - y - 0.5f;
                    float fromRight = InsetSize - x - 0.5f;
                    float fromBottom = y + 0.5f;

                    float v = 0.9f + (GrainValue(x, y, random) - 0.72f) * 0.6f;
                    float shadow = Mathf.Min(fromLeft, fromTop);
                    if (shadow < InsetBorder)
                        v *= Mathf.Lerp(0.45f, 1f, Mathf.SmoothStep(0f, 1f, shadow / InsetBorder));
                    float rim = Mathf.Min(fromRight, fromBottom);
                    if (rim < 2.5f)
                        v = Mathf.Lerp(v, 1f, 0.6f * (1f - rim / 2.5f));
                    pixels[y * InsetSize + x] = Grey(v);
                }
            }

            return MakeSprite(pixels, InsetSize, TextureWrapMode.Clamp, Vector4.one * InsetBorder);
        }

        // Tileable layered noise plus per-pixel grain, around 0.72.
        private static float GrainValue(int x, int y, System.Random random)
        {
            float u = x / (float)GrainSize;
            float v = y / (float)GrainSize;
            float n = TileNoise(u, v, 4f, 3.1f) * 0.6f + TileNoise(u, v, 12f, 17.7f) * 0.4f;
            float speck = (float)random.NextDouble() - 0.5f;
            return Mathf.Clamp(0.50f + n * 0.45f + speck * 0.16f, 0.40f, 1f);
        }

        private static float TileNoise(float u, float v, float period, float offset)
        {
            float a = Mathf.PerlinNoise(offset + u * period, offset + v * period);
            float b = Mathf.PerlinNoise(offset + (u - 1f) * period, offset + v * period);
            float c = Mathf.PerlinNoise(offset + u * period, offset + (v - 1f) * period);
            float d = Mathf.PerlinNoise(offset + (u - 1f) * period, offset + (v - 1f) * period);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static Color32 Grey(float v)
        {
            byte b = (byte)(Mathf.Clamp01(v) * 255f);
            return new Color32(b, b, b, 255);
        }

        private static Sprite MakeSprite(Color32[] pixels, int size, TextureWrapMode wrap, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
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

        /// <summary>
        /// An "X" in a panel's top-right corner that calls <paramref name="onClose"/> when clicked
        /// or tapped. Registered as a touch blocker so a tap on it isn't also read as a tap on the panel.
        /// </summary>
        public static RectTransform CloseButton(RectTransform panel, System.Action onClose)
        {
            Image close = NewImage("Close", panel, Color.white);
            close.raycastTarget = true;
            RectTransform rt = close.rectTransform;
            rt.anchorMin = Vector2.one;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.one;
            rt.anchoredPosition = new Vector2(-12f, -12f);
            rt.sizeDelta = new Vector2(38f, 38f);
            Text x = NewText("X", rt, "X", 20, TextColor, TextAnchor.MiddleCenter);
            Stretch(x.rectTransform, 0f);
            StyleButton(close, x, DangerTint);
            close.gameObject.AddComponent<ClickRelay>().Clicked += onClose;
            TouchMode.AddBlocker(rt);
            return rt;
        }

        /// <summary>Calls back when the graphic is clicked or tapped.</summary>
        public static void OnClick(Graphic graphic, System.Action onClick)
        {
            graphic.raycastTarget = true;
            graphic.gameObject.AddComponent<ClickRelay>().Clicked += onClick;
        }

        private sealed class ClickRelay : MonoBehaviour, IPointerClickHandler
        {
            public event System.Action Clicked;

            public void OnPointerClick(PointerEventData eventData)
            {
                Clicked?.Invoke();
            }
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
