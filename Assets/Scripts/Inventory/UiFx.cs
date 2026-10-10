using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    // Small UI animations for the theme in UiKit.Theme. All run on unscaled time, so menus keep
    // moving while the game is paused.

    /// <summary>Hover glow, a slight grow on hover and a press dip for a themed button.</summary>
    public sealed class UiButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Image hover;
        private Text label;
        private Selectable selectable;
        private bool over, pressed;
        private float glow;

        public void Init(Image hoverImage, Text labelText)
        {
            hover = hoverImage;
            label = labelText;
            selectable = GetComponent<Selectable>();
        }

        private bool Interactable => selectable == null || selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData) => over = true;
        public void OnPointerExit(PointerEventData eventData) { over = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        private void OnDisable()
        {
            over = pressed = false;
            glow = 0f;
            transform.localScale = Vector3.one;
            if (hover != null) hover.color = new Color(hover.color.r, hover.color.g, hover.color.b, 0f);
        }

        private void Update()
        {
            bool live = Interactable;
            float dt = Time.unscaledDeltaTime;
            float targetGlow = live && over ? 1f : 0f;
            glow = Mathf.MoveTowards(glow, targetGlow, dt * 6f);
            float scale = live && pressed ? 0.96f : 1f + 0.035f * glow;
            transform.localScale = Vector3.one * Mathf.Lerp(transform.localScale.x, scale, 1f - Mathf.Exp(-dt * 18f));

            if (hover != null)
            {
                float shimmer = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
                Color c = hover.color;
                c.a = 0.28f * glow * shimmer;
                hover.color = c;
            }
            if (label != null && live)
                label.color = Color.Lerp(UiKit.TextColor, new Color(1f, 0.9f, 0.62f, 1f), glow);
        }
    }

    /// <summary>Breathes a graphic's alpha between two values.</summary>
    public sealed class UiPulse : MonoBehaviour
    {
        private Graphic graphic;
        private float min, max, speed, phase;

        public void Init(Graphic target, float minAlpha, float maxAlpha, float cyclesPerSecond)
        {
            graphic = target;
            min = minAlpha;
            max = maxAlpha;
            speed = cyclesPerSecond * Mathf.PI * 2f;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (graphic == null) return;
            Color c = graphic.color;
            c.a = Mathf.Lerp(min, max, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed + phase));
            graphic.color = c;
        }
    }

    /// <summary>Spins a UI element (degrees per second).</summary>
    public sealed class UiSpin : MonoBehaviour
    {
        public float speed = 45f;

        private void Update()
        {
            transform.Rotate(0f, 0f, speed * Time.unscaledDeltaTime);
        }
    }

    /// <summary>Scrolls a tiled fog texture slowly across a raw image.</summary>
    public sealed class FogDrift : MonoBehaviour
    {
        private RawImage raw;
        private Vector2 drift;
        private float tiles;

        public void Init(RawImage target, Vector2 uvPerSecond, float tileCount)
        {
            raw = target;
            drift = uvPerSecond;
            tiles = tileCount;
            raw.uvRect = new Rect(Random.value, Random.value, tiles, tiles);
        }

        private void Update()
        {
            if (raw == null) return;
            RectTransform rt = raw.rectTransform;
            float aspect = rt.rect.height > 0f ? rt.rect.width / rt.rect.height : 1.7f;
            Rect uv = raw.uvRect;
            uv.x = Mathf.Repeat(uv.x + drift.x * Time.unscaledDeltaTime, 1f);
            uv.y = Mathf.Repeat(uv.y + drift.y * Time.unscaledDeltaTime, 1f);
            uv.width = tiles * aspect;
            uv.height = tiles;
            raw.uvRect = uv;
        }
    }

    /// <summary>Embers that rise from the bottom of the rect, sway, flicker and fade out.</summary>
    public sealed class EmberField : MonoBehaviour
    {
        public int count = 46;

        private struct Mote
        {
            public RectTransform rect;
            public Image image;
            public Vector2 position;
            public float speed, sway, swayRate, life, age, size, flicker;
        }

        private Mote[] motes;

        private void Awake()
        {
            motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                Image image = UiKit.NewImage("Ember", transform, UiKit.Ember);
                image.sprite = UiKit.Glow;
                RectTransform rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                motes[i].rect = rt;
                motes[i].image = image;
                Respawn(ref motes[i], true);
            }
        }

        private void Respawn(ref Mote m, bool anywhere)
        {
            m.position = new Vector2(Random.value, anywhere ? Random.value : -0.03f);
            m.speed = Random.Range(0.035f, 0.11f);
            m.sway = Random.Range(8f, 30f);
            m.swayRate = Random.Range(0.6f, 1.8f);
            m.life = Random.Range(6f, 14f);
            m.age = anywhere ? Random.value * m.life : 0f;
            m.size = Random.Range(5f, 15f);
            m.flicker = Random.value * 10f;
            m.rect.sizeDelta = new Vector2(m.size, m.size);
            m.image.color = Color.Lerp(UiKit.Ember, new Color(1f, 0.85f, 0.45f, 1f), Random.value);
        }

        private void Update()
        {
            Rect area = ((RectTransform)transform).rect;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            for (int i = 0; i < motes.Length; i++)
            {
                ref Mote m = ref motes[i];
                m.age += dt;
                m.position.y += m.speed * dt;
                if (m.age >= m.life || m.position.y > 1.05f)
                {
                    Respawn(ref m, false);
                    continue;
                }

                float t = m.age / m.life;
                float fade = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 3f);
                float flicker = 0.65f + 0.35f * Mathf.Sin(now * 9f + m.flicker) * Mathf.Sin(now * 3.7f + m.flicker * 2f);
                Color c = m.image.color;
                c.a = 0.85f * fade * flicker;
                m.image.color = c;
                float x = m.position.x * area.width + Mathf.Sin(now * m.swayRate + m.flicker) * m.sway;
                m.rect.anchoredPosition = new Vector2(x, m.position.y * area.height);
            }
        }
    }

    /// <summary>
    /// Fades and lifts an element into place whenever it is enabled, after an optional delay
    /// (staggering the delays makes a screen's items cascade in). With a zero offset it leaves
    /// the position alone, and with a start scale of 1 the scale, for panels that code places or
    /// scales itself.
    /// </summary>
    public sealed class UiAppear : MonoBehaviour
    {
        public float delay;
        public float duration = 0.28f;
        public Vector2 offset = new Vector2(0f, -14f);
        public float startScale = 0.97f;

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 home;
        private float started;
        private bool running;

        private void OnEnable()
        {
            rect = (RectTransform)transform;
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (!running) home = rect.anchoredPosition;
            started = Time.unscaledTime;
            running = true;
            Apply(0f);
        }

        private void OnDisable()
        {
            if (!running) return;
            Apply(1f);
            running = false;
        }

        private void Update()
        {
            if (!running) return;
            float t = Mathf.Clamp01((Time.unscaledTime - started - delay) / duration);
            Apply(t);
            if (t >= 1f) running = false;
        }

        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            group.alpha = eased;
            if (offset != Vector2.zero)
                rect.anchoredPosition = home + offset * (1f - eased);
            if (startScale != 1f)
                rect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, eased);
        }
    }
}
