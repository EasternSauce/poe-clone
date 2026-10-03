using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;

namespace PoeClone.UI
{
    /// <summary>
    /// Small screen-space health bar floating above an enemy. Only shown once the enemy
    /// has taken damage, so full-health enemies don't clutter the screen.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyHealthBarUI : MonoBehaviour
    {
        [SerializeField] private float barWidth = 60f;
        [SerializeField] private float barHeight = 7f;

        private EnemyHealth health;

        /// <summary>One of the player's minions (a spectator's copy of it): green, not red.</summary>
        public bool Friendly { get; set; }
        private bool everDamaged;
        private static Texture2D pixel;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            health.Damaged += OnDamaged;

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        private void OnDamaged()
        {
            everDamaged = true;
        }

        private void OnGUI()
        {
            if (!everDamaged || health.IsDead)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            // Bigger kinds are scaled up as a whole; keep the bar above their heads.
            Vector3 world = transform.position + Vector3.up * health.BarHeight * transform.localScale.y;
            Vector3 screen = cam.WorldToScreenPoint(world);

            if (screen.z <= 0f)
                return;

            // Raw screen pixels: grow the bar on a phone so it stays readable.
            float scale = TouchMode.GuiScale;
            float width = barWidth * scale;
            float height = barHeight * scale;

            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y;

            float fraction = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;

            DrawBar(new Rect(x, y, width, height), fraction, Friendly ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.75f, 0.15f, 0.15f));
        }

        private static void DrawBar(Rect rect, float fraction, Color fillColor)
        {
            Color previous = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, pixel);

            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), pixel);

            GUI.color = previous;
        }
    }
}
