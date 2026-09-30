using UnityEngine;
using PoeClone.Player;

namespace PoeClone.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        private PlayerStats stats;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;

        private static Texture2D pixel;

        private void Start()
        {
            if (stats == null)
            {
                stats = FindAnyObjectByType<PlayerStats>();
            }
        }

        public void SetStats(PlayerStats playerStats)
        {
            stats = playerStats;
        }

        private void Awake()
        {
            titleStyle = new GUIStyle
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };

            titleStyle.normal.textColor = Color.white;

            textStyle = new GUIStyle
            {
                fontSize = 18
            };

            textStyle.normal.textColor = Color.white;

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        private void OnGUI()
        {
            if (stats == null)
                return;

            GUILayout.BeginArea(
                new Rect(20f, 20f, 350f, 340f)
            );

            GUILayout.Label(
                $"Level {stats.Level}",
                titleStyle
            );

            GUILayout.Space(8f);

            GUILayout.Label(
                $"Health: {stats.CurrentHealth:0} / {stats.MaxHealth:0}",
                textStyle
            );

            DrawBar(GUILayoutUtility.GetRect(280f, 16f), SafeRatio(stats.CurrentHealth, stats.MaxHealth), new Color(0.75f, 0.15f, 0.15f));

            GUILayout.Space(6f);

            GUILayout.Label(
                $"Mana:   {stats.CurrentMana:0} / {stats.MaxMana:0}",
                textStyle
            );

            DrawBar(GUILayoutUtility.GetRect(280f, 16f), SafeRatio(stats.CurrentMana, stats.MaxMana), new Color(0.2f, 0.35f, 0.85f));

            GUILayout.Space(12f);

            GUILayout.Label(
                "Attributes",
                titleStyle
            );

            GUILayout.Label(
                $"Strength:     {stats.Strength}",
                textStyle
            );

            GUILayout.Label(
                $"Dexterity:    {stats.Dexterity}",
                textStyle
            );

            GUILayout.Label(
                $"Intelligence: {stats.Intelligence}",
                textStyle
            );

            GUILayout.Space(12f);

            GUILayout.Label(
                $"Experience: {stats.Experience} / " +
                $"{stats.ExperienceRequiredForNextLevel()}",
                textStyle
            );

            GUILayout.Space(20f);

            GUILayout.Label(
                "WASD / Arrow Keys - Move | Shift - Sprint | I - Inventory | C - Character",
                textStyle
            );

            GUILayout.EndArea();
        }

        private static float SafeRatio(float current, float max)
        {
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private static void DrawBar(Rect rect, float fraction, Color fillColor)
        {
            Color previous = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, pixel);

            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), pixel);

            GUI.color = previous;
        }
    }
}
