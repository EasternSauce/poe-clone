using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        private PlayerStats stats;

        /// <summary>Spectator replica: same HUD, but no control hints and the death text addressed to a watcher.</summary>
        public bool SpectatorMode { get; set; }

        // OnGUI always draws above uGUI canvases, so full-screen uGUI overlays (name prompt,
        // session gate) hide the HUD through this instead of just covering it.
        private static readonly System.Collections.Generic.HashSet<object> hiders = new System.Collections.Generic.HashSet<object>();

        public static void SetHiddenBy(object hider, bool hidden)
        {
            if (hidden) hiders.Add(hider);
            else hiders.Remove(hider);
        }

        // Domain reload is off in this project: without this, a hider from the previous play
        // session (destroyed while it was hiding the HUD) would keep it hidden forever.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHiders()
        {
            hiders.Clear();
        }

        // A hider destroyed without un-hiding (scene unload, a crash in its OnDestroy) stops counting.
        private static bool AnyHider()
        {
            hiders.RemoveWhere(h => h is Object unityObject && unityObject == null);
            return hiders.Count > 0;
        }

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle barLabelStyle;
        private GUIStyle barLabelStyleLeft;

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

            barLabelStyle = new GUIStyle(textStyle)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            barLabelStyleLeft = new GUIStyle(textStyle)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
            textStyle.richText = true;

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        private void OnGUI()
        {
            if (stats == null || AnyHider())
                return;

            if (TouchMode.Active)
            {
                DrawTouchHud();
                return;
            }

            if (stats.IsDead)
                DrawDeathOverlay(stats, SpectatorMode, Screen.width, Screen.height);

            GUILayout.BeginArea(
                new Rect(20f, 20f, 760f, 340f)
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

            DrawBar(GUILayoutUtility.GetRect(280f, 16f, GUILayout.ExpandWidth(false)), SafeRatio(stats.CurrentHealth, stats.MaxHealth), new Color(0.75f, 0.15f, 0.15f));

            GUILayout.Space(6f);

            GUILayout.Label(
                $"Mana:   {stats.CurrentMana:0} / {stats.MaxMana:0}",
                textStyle
            );

            DrawBar(GUILayoutUtility.GetRect(280f, 16f, GUILayout.ExpandWidth(false)), SafeRatio(stats.CurrentMana, stats.MaxMana), new Color(0.2f, 0.35f, 0.85f));

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

            if (!SpectatorMode)
                GUILayout.Label(PurseLine(), textStyle);

            if (!SpectatorMode)
            {
                GUILayout.Space(20f);

                GUILayout.Label(
                    "WASD / Arrow Keys - Move | Shift - Sprint | Q E R F - Skills | 1 2 - Potions\nK - Skill list | I - Inventory | C - Character | Enter - Chat",
                    textStyle
                );
            }

            GUILayout.EndArea();
        }

        // Phone layout: just the bars, scaled up from raw screen pixels (see TouchMode.GuiScale).
        // The attributes live on the character page, and there are no keys to list.
        private void DrawTouchHud()
        {
            float scale = TouchMode.GuiScale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (stats.IsDead)
                DrawDeathOverlay(stats, SpectatorMode, width, height);

            const float x = 14f;
            const float barWidth = 210f;
            float y = 12f;

            GUI.Label(new Rect(x, y, barWidth, 30f), $"Level {stats.Level}", titleStyle);
            y += 32f;

            DrawLabeledBar(new Rect(x, y, barWidth, 20f), SafeRatio(stats.CurrentHealth, stats.MaxHealth), new Color(0.75f, 0.15f, 0.15f),
                $"{stats.CurrentHealth:0} / {stats.MaxHealth:0}");
            y += 24f;

            DrawLabeledBar(new Rect(x, y, barWidth, 20f), SafeRatio(stats.CurrentMana, stats.MaxMana), new Color(0.2f, 0.35f, 0.85f),
                $"{stats.CurrentMana:0} / {stats.MaxMana:0}");
            y += 24f;

            DrawBar(new Rect(x, y, barWidth, 6f), SafeRatio(stats.Experience, stats.ExperienceRequiredForNextLevel()), new Color(0.85f, 0.7f, 0.3f));
            y += 10f;

            if (!SpectatorMode)
                GUI.Label(new Rect(x, y, 400f, 22f), PurseLine(), barLabelStyleLeft);

            GUI.matrix = previous;
        }

        // Gold and potion counts: "1 Health x3   2 Mana x1   Gold 120" (keys only on desktop).
        private string PurseLine()
        {
            var inventory = stats.GetComponent<PoeClone.Inventory.PlayerInventory>();
            var potions = stats.GetComponent<PlayerPotions>();
            int gold = inventory != null ? inventory.Gold : 0;
            int health = potions != null ? potions.HealthPotions : 0;
            int mana = potions != null ? potions.ManaPotions : 0;

            string keys1 = TouchMode.Active ? "" : "[1] ";
            string keys2 = TouchMode.Active ? "" : "[2] ";
            return $"<color=#ff7a70>{keys1}Health x{health}</color>   <color=#8fa2ff>{keys2}Mana x{mana}</color>   <color=#ffd34d>Gold {gold}</color>";
        }

        private void DrawLabeledBar(Rect rect, float fraction, Color fillColor, string label)
        {
            DrawBar(rect, fraction, fillColor);
            GUI.Label(rect, label, barLabelStyle);
        }

        // YOU DIED stays up throughout; underneath it, a 3/2/1 countdown counts down to a
        // "press any button" prompt once PlayerStats promotes to AwaitingRevive.
        private static void DrawDeathOverlay(PlayerStats stats, bool spectating, float screenWidth, float screenHeight)
        {
            GUIStyle titleOverlayStyle = new GUIStyle
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleOverlayStyle.normal.textColor = new Color(0.85f, 0.15f, 0.15f);

            GUI.Label(new Rect(0f, screenHeight * 0.32f, screenWidth, 80f), spectating ? "THE PLAYER DIED" : "YOU DIED", titleOverlayStyle);

            GUIStyle subStyle = new GUIStyle
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = Color.white;

            string revivePrompt = TouchMode.Active ? "Tap to revive" : "Press any button to revive";
            string sub = stats.IsAwaitingRevive
                ? (spectating ? "Waiting for them to revive..." : revivePrompt)
                : stats.CountdownSecondsRemaining.ToString();

            GUI.Label(new Rect(0f, screenHeight * 0.32f + 70f, screenWidth, 50f), sub, subStyle);
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
