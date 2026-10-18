using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.UI
{
    /// <summary>
    /// A ring that swells out of a newly confirmed passive (passive or weapon tree), slowing as it
    /// grows and fading away. It lives in the tree's zoomed content, so it is sized to the node and
    /// shrinks with the tree when zoomed out. Unscaled time, since the trees pause gameplay.
    /// </summary>
    public sealed class PassiveShockwave : MonoBehaviour
    {
        private const float Seconds = 0.9f;
        private const float Reach = 2.6f;   // final diameter, in node sizes

        private Image ring;
        private float size;
        private float elapsed;

        public static void Spawn(RectTransform content, Vector2 at, float nodeSize, Color color)
        {
            Image ring = UiKit.NewImage("Shockwave", content, Color.Lerp(color, Color.white, 0.4f));
            ring.sprite = UiKit.Ring;
            ring.raycastTarget = false;
            RectTransform rt = ring.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = Vector2.one * nodeSize;
            var wave = ring.gameObject.AddComponent<PassiveShockwave>();
            wave.ring = ring;
            wave.size = nodeSize;
            wave.Apply(0f);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= Seconds) { Destroy(gameObject); return; }
            Apply(elapsed / Seconds);
        }

        // Closing the tree mid-wave just drops it.
        private void OnDisable() => Destroy(gameObject);

        private void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            ring.rectTransform.sizeDelta = Vector2.one * (size * Mathf.Lerp(1f, Reach, ease));
            Color c = ring.color;
            c.a = 0.85f * (1f - t) * (1f - t);
            ring.color = c;
        }
    }
}
