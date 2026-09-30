using UnityEngine;
using PoeClone.Player;

namespace PoeClone.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        private PlayerStats stats;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;

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
        }

        private void OnGUI()
        {
            if (stats == null)
                return;

            GUILayout.BeginArea(
                new Rect(20f, 20f, 350f, 300f)
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

            GUILayout.Label(
                $"Mana:   {stats.CurrentMana:0} / {stats.MaxMana:0}",
                textStyle
            );

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
    }
}
