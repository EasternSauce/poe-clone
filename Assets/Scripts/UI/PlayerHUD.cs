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

        // The area title shown for a few seconds on arriving somewhere.
        private const float TitleSeconds = 3.5f;
        private string titleText;
        private string titleSub;
        private float titleShownAt = -100f;
        private bool titlePending;
        private PoeClone.World.AreaManager subscribedAreas;

        // The key list under the attributes (and the passive tree's how-to line) can be hidden
        // (H, or its button); remembered between plays.
        private const string ControlsHiddenKey = "PoeClone.HudControlsHidden";
        private static bool controlsHidden;
        private static bool controlsLoaded;

        public static bool ControlsHidden
        {
            get
            {
                if (!controlsLoaded)
                {
                    controlsLoaded = true;
                    controlsHidden = PlayerPrefs.GetInt(ControlsHiddenKey, 0) == 1;
                }
                return controlsHidden;
            }
            set
            {
                controlsHidden = value;
                controlsLoaded = true;
                PlayerPrefs.SetInt(ControlsHiddenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Update()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (!SpectatorMode && keyboard != null && keyboard.hKey.wasPressedThisFrame && !UiKit.IsTypingInTextField())
                ControlsHidden = !ControlsHidden;

            var areas = PoeClone.World.AreaManager.Instance;
            if (areas != null && areas != subscribedAreas)
            {
                if (subscribedAreas != null)
                    subscribedAreas.AreaChanged -= OnAreaChanged;
                subscribedAreas = areas;
                areas.AreaChanged += OnAreaChanged;
                if (areas.CurrentAreaIndex >= 0)
                    OnAreaChanged(areas.CurrentAreaIndex);
            }
        }

        private void OnDestroy()
        {
            if (subscribedAreas != null)
                subscribedAreas.AreaChanged -= OnAreaChanged;
        }

        private void OnAreaChanged(int index)
        {
            var area = subscribedAreas != null ? subscribedAreas.Current : null;
            if (area == null)
                return;
            titleText = area.areaName;
            titleSub = area.isTown ? "Town - you are safe here" : area.monsterLevel > 0 ? "Monster level " + area.monsterLevel : "";
            // Starts when the HUD is actually on screen (not behind the name prompt or a loading fade).
            titlePending = true;
        }

        // Big centred title that fades out, under the top edge.
        private void DrawAreaTitle(float width)
        {
            float age = Time.unscaledTime - titleShownAt;
            if (titleText == null || age > TitleSeconds)
                return;

            float alpha = age < 0.4f ? age / 0.4f : age > TitleSeconds - 1f ? (TitleSeconds - age) : 1f;
            var big = new GUIStyle { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(0.95f, 0.85f, 0.6f, alpha);
            var small = new GUIStyle { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            small.normal.textColor = new Color(0.85f, 0.82f, 0.75f, alpha);
            var shadow = new GUIStyle(big);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);

            GUI.Label(new Rect(2f, 92f, width, 50f), titleText, shadow);
            GUI.Label(new Rect(0f, 90f, width, 50f), titleText, big);
            GUI.Label(new Rect(0f, 132f, width, 26f), titleSub, small);
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

            var loading = PoeClone.World.AreaManager.Instance != null ? PoeClone.World.AreaManager.Instance.loadingScreen : null;
            if (titlePending && (loading == null || !loading.IsShowing))
            {
                titlePending = false;
                titleShownAt = Time.unscaledTime;
            }

            // The passive tree fills the screen: nothing of the HUD shows over it.
            if (PassiveTreeUI.IsOpen && !stats.IsDead)
                return;

            // OnGUI draws over every uGUI window, so the sheet steps aside while the character
            // page (which sits in the same corner) is open.
            if (CharacterPageOpen())
            {
                DrawAreaTitle(TouchMode.Active ? Screen.width / TouchMode.GuiScale : Screen.width);
                return;
            }

            if (TouchMode.Active)
            {
                DrawTouchHud();
                return;
            }

            if (stats.IsDead)
                DrawDeathOverlay(stats, SpectatorMode, Screen.width, Screen.height);

            DrawAreaTitle(Screen.width);

            // A soft dark backing, so the white text reads over bright ground (snow, the plaza).
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.32f);
            bool showControls = !SpectatorMode && !ControlsHidden;
            GUI.DrawTexture(new Rect(10f, 10f, showControls ? 750f : 400f, SpectatorMode ? 290f : showControls ? 368f : 316f), pixel);
            GUI.color = previousColor;

            GUILayout.BeginArea(
                new Rect(20f, 20f, 760f, 360f)
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

            if (showControls)
            {
                GUILayout.Space(20f);

                GUILayout.Label(
                    "WASD - Move | Shift - Sprint | Left click - Attack | Q E R F, 1 2 3 4, spare mouse buttons - Skills\n1 2 - Potions by default | T - Town portal | K - Skills and potion keys | P - Passives | I - Inventory\nC - Character | M - Map | Enter - Chat | H - Hide this",
                    textStyle
                );
            }
            else if (!SpectatorMode)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("Show controls (H)", GUILayout.Width(170f)))
                    ControlsHidden = false;
            }

            GUILayout.EndArea();
        }

        private CharacterPageUI characterPage;

        private bool CharacterPageOpen()
        {
            if (characterPage == null)
                characterPage = FindAnyObjectByType<CharacterPageUI>();
            return characterPage != null && characterPage.IsOpen;
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

            DrawAreaTitle(width);

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

            string keys1 = TouchMode.Active ? "" : "[" + PlayerPotions.HealthPotionKeyLabel + "] ";
            string keys2 = TouchMode.Active ? "" : "[" + PlayerPotions.ManaPotionKeyLabel + "] ";
            string line = $"<color=#ff7a70>{keys1}Health x{health}</color>   <color=#8fa2ff>{keys2}Mana x{mana}</color>   <color=#ffd34d>Gold {gold}</color>";

            var passives = stats.GetComponent<PlayerPassives>();
            int points = passives != null && passives.enabled ? passives.Unspent : 0;
            if (points > 0)
                line += $"   <color=#ffd040>+{points} passive point{(points > 1 ? "s" : "")}{(TouchMode.Active ? "" : " (P)")}</color>";
            return line;
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
