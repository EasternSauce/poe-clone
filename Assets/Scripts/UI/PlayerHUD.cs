using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.UI
{
    /// <summary>
    /// The player's frame, top left (a level badge beside health, mana and experience bars, and
    /// the co-op partner's frame under it), the area title and the death overlay. Attributes live
    /// on the character page, gold in the inventory, and the key list in the settings.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        private PlayerStats stats;
        private InventoryUI inventoryUI;

        /// <summary>Spectator replica: same HUD, but the death text addressed to a watcher.</summary>
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

        private GUIStyle levelStyle;
        private GUIStyle barLabelStyle;
        private GUIStyle smallBarLabelStyle;
        private GUIStyle captionStyle;
        private GUIStyle barFrameStyle;

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

        private void Update()
        {
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
            titleSub = area.isTown ? "Town - you are safe here" : area.monsterLevel > 0 ? "Area level " + area.monsterLevel : "";
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
            var big = new GUIStyle { font = UiKit.TitleFont, fontSize = 36, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(0.95f, 0.85f, 0.6f, alpha);
            var small = new GUIStyle { font = UiKit.Font, fontSize = 19, alignment = TextAnchor.MiddleCenter };
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
            levelStyle = new GUIStyle
            {
                font = UiKit.TitleFont,
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter
            };
            levelStyle.normal.textColor = new Color(1f, 0.9f, 0.62f);

            barLabelStyle = new GUIStyle
            {
                font = UiKit.BoldFont,
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter
            };
            barLabelStyle.normal.textColor = new Color(0.97f, 0.94f, 0.88f);

            smallBarLabelStyle = new GUIStyle(barLabelStyle) { fontSize = 14 };

            captionStyle = new GUIStyle
            {
                font = UiKit.Font,
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            captionStyle.normal.textColor = UiKit.TextColor;

            barFrameStyle = new GUIStyle { border = new RectOffset(6, 6, 6, 6) };
            barFrameStyle.normal.background = UiKit.BarFrameSprite.texture;

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        /// <summary>What the co-op partner's frame shows (kept up to date by CoopPartner).</summary>
        public class PartnerStatus
        {
            public string Name;
            public int Level;
            public float Health, MaxHealth, Mana, MaxMana;
            public bool Dead;
            public bool Here;   // in this player's area
            public string Area; // the name of the area they're in
        }

        /// <summary>Co-op: the partner's frame, drawn under this player's bars; null without a partner.</summary>
        public static PartnerStatus Partner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPartner()
        {
            Partner = null;
        }

        // The frame's layout, in HUD units (see Scale).
        private const float FrameX = 16f;
        private const float FrameY = 14f;
        private const float BadgeSize = 76f;
        // Shorter on a phone, where the round menu buttons start just right of the frame (TouchControlsUI).
        private static float BarWidth => TouchMode.Active ? 200f : 320f;
        private const float FrameBottom = FrameY + BadgeSize + 6f;
        private const float PartnerHeight = 64f;

        /// <summary>
        /// The frame's HUD units to screen pixels. Desktop follows the screen height like the uGUI
        /// canvases (1 at 1080p); on a phone raw pixels are tiny (see TouchMode.GuiScale), but
        /// the frame is drawn a little smaller there to stay clear of the round menu buttons.
        /// </summary>
        private static float Scale => TouchMode.Active ? TouchMode.GuiScale * 0.75f : Mathf.Max(0.75f, Screen.height / 1080f);

        /// <summary>Where the HUD ends, in screen pixels from the top (things placed under it start here).</summary>
        public static float BottomPixels => (FrameBottom + (Partner != null ? PartnerHeight : 0f)) * Scale;

        private static readonly Color HealthColor = new Color(0.86f, 0.16f, 0.13f);
        private static readonly Color ManaColor = new Color(0.24f, 0.44f, 1f);
        private static readonly Color ExperienceColor = new Color(0.95f, 0.76f, 0.32f);

        // Health just lost lingers as a pale strip, then drains away after it.
        private float trailHealth = -1f;
        private float trailHeldUntil;

        // A level badge beside the health, mana and experience bars.
        private void DrawFrame()
        {
            float health = SafeRatio(stats.CurrentHealth, stats.MaxHealth);
            UpdateTrail(health);

            // A soft shadow behind it all, so it reads over bright ground (snow, the plaza).
            DrawTinted(UiKit.Glow.texture, new Rect(FrameX - 80f, FrameY - 70f, BarWidth + 250f, BadgeSize + 140f), new Color(0f, 0f, 0f, 0.7f));

            Rect badge = new Rect(FrameX, FrameY, BadgeSize, BadgeSize);
            DrawTinted(UiKit.Disc.texture, Inset(badge, 3f), new Color(0.09f, 0.07f, 0.05f, 0.95f));
            DrawTinted(UiKit.Ring.texture, badge, UiKit.Gold);
            DrawTinted(UiKit.Ring.texture, Inset(badge, 6f), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.35f));
            DrawShadowedLabel(badge, stats.Level.ToString(), levelStyle);

            float x = FrameX + BadgeSize + 8f;
            Rect healthBar = new Rect(x, FrameY + 6f, BarWidth, 26f);
            if (health < 0.35f && !stats.IsDead)
            {
                // Low life: the bar throbs red.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                DrawTinted(UiKit.Glow.texture, Inset(healthBar, -18f), new Color(1f, 0.1f, 0.05f, 0.25f + 0.35f * pulse));
            }
            DrawBar(healthBar, health, HealthColor, trailHealth);
            DrawShadowedLabel(healthBar, $"{stats.CurrentHealth:0} / {stats.MaxHealth:0}", barLabelStyle);

            Rect manaBar = new Rect(x, FrameY + 36f, BarWidth, 26f);
            DrawBar(manaBar, SafeRatio(stats.CurrentMana, stats.MaxMana), ManaColor);
            DrawShadowedLabel(manaBar, $"{stats.CurrentMana:0} / {stats.MaxMana:0}", barLabelStyle);

            Rect xpBar = new Rect(x, FrameY + 66f, BarWidth, 9f);
            int needed = stats.ExperienceRequiredForNextLevel();
            float xp = SafeRatio(stats.Experience, needed);
            DrawBar(xpBar, xp, ExperienceColor);
            // Desktop: hovering the bar spells out the experience.
            if (!TouchMode.Active && Inset(xpBar, -6f).Contains(Event.current.mousePosition))
                DrawShadowedLabel(new Rect(x, xpBar.yMax + 3f, BarWidth + 200f, 22f),
                    $"Experience {stats.Experience:N0} / {needed:N0}  ({xp * 100f:0}%)", captionStyle);

            DrawPartnerFrame(FrameX + 4f, FrameBottom + 6f, BarWidth - 40f);
        }

        private void UpdateTrail(float health)
        {
            if (trailHealth < 0f || health >= trailHealth)
            {
                trailHealth = health;
                trailHeldUntil = Time.unscaledTime + 0.4f;
                return;
            }
            if (Event.current.type == EventType.Repaint && Time.unscaledTime > trailHeldUntil)
                trailHealth = Mathf.MoveTowards(trailHealth, health, Time.unscaledDeltaTime * 0.6f);
        }

        // A compact frame: the partner's name, level and area over slim health and mana bars.
        private void DrawPartnerFrame(float x, float y, float width)
        {
            PartnerStatus partner = Partner;
            if (partner == null)
                return;
            string where = partner.Dead ? "<color=#ff7a70>dead</color>"
                : $"<color={(partner.Here ? "#d8d8d8" : "#a0a0a0")}>{partner.Area}</color>";
            DrawShadowedLabel(new Rect(x, y, width + 200f, 20f),
                $"<color=#8cd9ff>{partner.Name}</color>   <color=#{UiKit.Hex(UiKit.Gold)}>Lv {partner.Level}</color>   {where}", captionStyle);
            Rect healthBar = new Rect(x, y + 22f, width, 17f);
            DrawBar(healthBar, SafeRatio(partner.Health, partner.MaxHealth), HealthColor);
            DrawShadowedLabel(healthBar, $"{partner.Health:0} / {partner.MaxHealth:0}", smallBarLabelStyle);
            Rect manaBar = new Rect(x, y + 42f, width, 17f);
            DrawBar(manaBar, SafeRatio(partner.Mana, partner.MaxMana), ManaColor);
            DrawShadowedLabel(manaBar, $"{partner.Mana:0} / {partner.MaxMana:0}", smallBarLabelStyle);
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
            if (loading != null && loading.Covering && !stats.IsDead)
                return;

            // The passive tree fills the screen: nothing of the HUD shows over it.
            if (PassiveTreeUI.IsOpen && !stats.IsDead)
                return;

            // On phones, this HUD draws over the stash canvas and can cover its tabs.
            // Give the stash window the full screen while it is open.
            if (TouchMode.Active && StashWindowOpen())
                return;

            Matrix4x4 previous = GUI.matrix;

            // The big centred texts keep the full phone scale; only the corner frame is compacted.
            float scale = TouchMode.Active ? TouchMode.GuiScale : Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (stats.IsDead)
                DrawDeathOverlay(stats, SpectatorMode, Screen.width / scale, Screen.height / scale);
            DrawAreaTitle(Screen.width / scale);

            // OnGUI draws over every uGUI window, so the frame steps aside while the character
            // page (which sits in the same corner) is open.
            if (!CharacterPageOpen())
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
                DrawFrame();
            }

            GUI.matrix = previous;
        }

        private CharacterPageUI characterPage;

        private bool StashWindowOpen()
        {
            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            return inventoryUI != null && inventoryUI.IsStashOpen;
        }

        private bool CharacterPageOpen()
        {
            if (characterPage == null)
                characterPage = FindAnyObjectByType<CharacterPageUI>();
            return characterPage != null && characterPage.IsOpen;
        }

        // When the death overlay started showing (it fades in and slowly grows from there).
        private float deathShownAt, deathLastDrawn = -1f;

        // YOU DIED stays up throughout; underneath it, a 3/2/1 countdown counts down to a
        // "press any button" prompt once PlayerStats promotes to AwaitingRevive.
        private void DrawDeathOverlay(PlayerStats stats, bool spectating, float screenWidth, float screenHeight)
        {
            float now = Time.unscaledTime;
            if (now - deathLastDrawn > 0.5f)
                deathShownAt = now;
            deathLastDrawn = now;
            float age = now - deathShownAt;
            float alpha = Mathf.Clamp01(age / 1.2f);

            // A dark band behind the words, fading out towards the sides.
            float bandY = screenHeight * 0.32f - 20f;
            Color previous = GUI.color;
            for (int k = 0; k < 16; k++)
            {
                float edge = Mathf.Abs(k - 7.5f) / 8f;
                GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha * (1f - edge * edge));
                GUI.DrawTexture(new Rect(screenWidth * k / 16f, bandY, screenWidth / 16f + 1f, 180f), pixel);
            }
            GUI.color = previous;

            GUIStyle titleOverlayStyle = new GUIStyle
            {
                font = UiKit.TitleFont,
                fontSize = Mathf.RoundToInt(Mathf.Lerp(52f, 60f, Mathf.Clamp01(age / 4f))),
                alignment = TextAnchor.MiddleCenter
            };
            titleOverlayStyle.normal.textColor = new Color(0.8f, 0.12f, 0.1f, alpha);

            GUI.Label(new Rect(0f, screenHeight * 0.32f, screenWidth, 80f), spectating ? "THE PLAYER DIED" : "YOU DIED", titleOverlayStyle);

            GUIStyle subStyle = new GUIStyle
            {
                font = UiKit.TitleFont,
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.9f, 0.86f, 0.76f, alpha);

            string revivePrompt = TouchMode.Active ? "Tap to revive" : "Press any button to revive";
            string sub = stats.IsAwaitingRevive
                ? (spectating ? "Waiting for them to revive..." : revivePrompt)
                : stats.CountdownSecondsRemaining.ToString();

            GUI.Label(new Rect(0f, screenHeight * 0.32f + 76f, screenWidth, 50f), sub, subStyle);
        }

        private static float SafeRatio(float current, float max)
        {
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private static Rect Inset(Rect rect, float by)
        {
            return new Rect(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);
        }

        private static void DrawTinted(Texture texture, Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
        {
            Color colour = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            // Rich-text colour tags would tint the shadow too: it is drawn from the stripped text.
            string shadow = style.richText ? System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "") : text;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), shadow, style);
            style.normal.textColor = colour;
            GUI.Label(rect, text, style);
        }

        // A dark track, the glassy fill (and a pale strip for what was just lost), in a gilded rim.
        private void DrawBar(Rect rect, float fraction, Color fillColor, float trail = 0f)
        {
            Rect inner = Inset(rect, 2f);
            DrawTinted(pixel, inner, new Color(0.03f, 0.025f, 0.02f, 0.85f));
            if (trail > fraction)
                DrawTinted(pixel, new Rect(inner.x, inner.y, inner.width * trail, inner.height), new Color(1f, 0.85f, 0.7f, 0.5f));
            DrawTinted(UiKit.BarFillSprite.texture, new Rect(inner.x, inner.y, inner.width * fraction, inner.height), fillColor);
            if (Event.current.type == EventType.Repaint)
                barFrameStyle.Draw(Inset(rect, -1f), GUIContent.none, false, false, false, false);
        }
    }
}
