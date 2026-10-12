using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.UI
{
    /// <summary>
    /// Floating numbers and words over characters: damage dealt and taken (coloured by damage
    /// type), and "Evaded" / "Blocked" / "Chilled", so the defensive stats can be seen working.
    /// One self-creating OnGUI drawer; texts rise and fade over about a second.
    /// </summary>
    public class CombatText : MonoBehaviour
    {
        private const float Lifetime = 0.9f;
        private const float RiseMetres = 1.2f;

        public static readonly Color PhysicalColor = new Color(1f, 1f, 1f);
        public static readonly Color FireColor = new Color(1f, 0.55f, 0.2f);
        public static readonly Color ColdColor = new Color(0.55f, 0.85f, 1f);
        public static readonly Color LightningColor = new Color(1f, 0.95f, 0.35f);
        public static readonly Color PlayerHurtColor = new Color(1f, 0.35f, 0.3f);
        public static readonly Color AvoidColor = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color BlockColor = new Color(0.95f, 0.8f, 0.4f);
        public static readonly Color PoisonColor = new Color(0.55f, 0.95f, 0.3f);

        private struct Entry
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
            public float Size;
            public long Number;
        }

        private static CombatText instance;
        private readonly List<Entry> entries = new List<Entry>();
        private GUIStyle style;
        private GUIStyle shadow;

        public static void Show(Vector3 world, string text, Color color, float size = 1f)
        {
            if (!Visuals.WorldActivity.NearView(world, 1.3f)) return;
            if (instance == null)
            {
                var go = new GameObject("CombatText");
                instance = go.AddComponent<CombatText>();
            }

            long.TryParse(text, out long number);
            if (number > 0 && Visuals.AdaptiveQuality.DetailLevel > 0)
            {
                for (int i = instance.entries.Count - 1; i >= 0; i--)
                {
                    Entry entry = instance.entries[i];
                    if (entry.Number <= 0 || entry.Color != color || Mathf.Abs(entry.Size - size) > 0.01f ||
                        Time.unscaledTime - entry.Born > 0.15f || (entry.World - world).sqrMagnitude > 0.5f) continue;
                    entry.Number += number;
                    entry.Text = entry.Number.ToString();
                    instance.entries[i] = entry;
                    return;
                }
            }

            while (instance.entries.Count >= Visuals.AdaptiveQuality.TextBudget)
                instance.entries.RemoveAt(0);

            // A little sideways jitter so quick repeated hits don't print on top of each other.
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            instance.entries.Add(new Entry { World = world + jitter, Text = text, Color = color, Born = Time.unscaledTime, Size = size, Number = number });
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnGUI()
        {
            if (entries.Count == 0)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            if (style == null)
            {
                style = new GUIStyle { font = UiKit.BoldFont, alignment = TextAnchor.MiddleCenter };
                shadow = new GUIStyle(style);
                shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }

            float scale = TouchMode.GuiScale;
            float now = Time.unscaledTime;

            for (int k = entries.Count - 1; k >= 0; k--)
            {
                Entry e = entries[k];
                float age = now - e.Born;
                if (age >= Lifetime)
                {
                    entries.RemoveAt(k);
                    continue;
                }

                float t = age / Lifetime;
                Vector3 screen = cam.WorldToScreenPoint(e.World + Vector3.up * RiseMetres * t);
                if (screen.z <= 0f || screen.x < -100f || screen.x > Screen.width + 100f || screen.y < -40f || screen.y > Screen.height + 40f)
                    continue;

                int fontSize = Mathf.RoundToInt(20f * e.Size * scale);
                style.fontSize = fontSize;
                shadow.fontSize = fontSize;

                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                style.normal.textColor = new Color(e.Color.r, e.Color.g, e.Color.b, alpha);
                shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);

                var rect = new Rect(screen.x - 100f, Screen.height - screen.y - 20f, 200f, 40f);
                float offset = Mathf.Max(1f, scale);
                GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), e.Text, shadow);
                GUI.Label(rect, e.Text, style);
            }
        }

        public static Color ColorFor(Combat.DamageType type)
        {
            switch (type)
            {
                case Combat.DamageType.Fire: return FireColor;
                case Combat.DamageType.Cold: return ColdColor;
                case Combat.DamageType.Lightning: return LightningColor;
                default: return PhysicalColor;
            }
        }
    }
}
