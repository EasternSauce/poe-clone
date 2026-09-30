using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.UI
{
    /// <summary>
    /// Small screen-space health bar floating above an enemy. Only shown once the enemy
    /// has taken damage, so full-health enemies don't clutter the screen.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyHealthBarUI : MonoBehaviour
    {
        [SerializeField] private float heightOffset = 2.3f;
        [SerializeField] private float barWidth = 60f;
        [SerializeField] private float barHeight = 7f;

        private EnemyHealth health;
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

            Vector3 world = transform.position + Vector3.up * heightOffset;
            Vector3 screen = cam.WorldToScreenPoint(world);

            if (screen.z <= 0f)
                return;

            float x = screen.x - barWidth * 0.5f;
            float y = Screen.height - screen.y;

            float fraction = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;

            DrawBar(new Rect(x, y, barWidth, barHeight), fraction, new Color(0.75f, 0.15f, 0.15f));
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
