using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>
    /// The character page (press C): every stat the character has, combined from base stats and gear.
    /// Values that gear is boosting show the bonus in green, and everything updates the moment you equip something.
    /// </summary>
    public class CharacterPageUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;

        private static float RowHeight => TouchMode.Active ? 44f : 26f;
        private const float PanelWidth = 420f;

        private struct Group
        {
            public string Title;
            public StatType[] Stats;

            public Group(string title, params StatType[] stats)
            {
                Title = title;
                Stats = stats;
            }
        }

        private static readonly Group[] Groups =
        {
            new Group("Attributes", StatType.Strength, StatType.Dexterity, StatType.Intelligence),
            new Group("Vitals", StatType.MaxLife, StatType.MaxMana),
            new Group("Defences", StatType.Armour, StatType.Evasion, StatType.BlockChance),
            new Group("Offence", StatType.PhysicalDamage, StatType.AttackSpeed, StatType.CriticalChance),
            new Group("Resistances", StatType.FireResistance, StatType.ColdResistance, StatType.LightningResistance),
            new Group("Movement", StatType.MovementSpeed)
        };

        private readonly Dictionary<StatType, Text> valueTexts = new Dictionary<StatType, Text>();
        private readonly Dictionary<StatType, Text> labelTexts = new Dictionary<StatType, Text>();

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform panel, shell;
        private float fullHeight;
        private bool layoutForTouch;
        private RectTransform closeButton;
        private Text levelText;
        private Text specialText;
        private const int SpecialRows = 5;
        private bool isOpen;
        private bool warming;

        public bool IsOpen
        {
            get { return isOpen; }
        }

        private void Start()
        {
            if (inventory == null)
                inventory = FindAnyObjectByType<PlayerInventory>();

            if (inventory == null)
            {
                Debug.LogError("CharacterPageUI: no PlayerInventory found in the scene.");
                enabled = false;
                return;
            }

            BuildUI();
            inventory.StatsChanged += Refresh;
            StartCoroutine(Prewarm());

            inventory.PlayerDied += OnPlayerDied;
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.StatsChanged -= Refresh;
                inventory.PlayerDied -= OnPlayerDied;
            }
        }

        private void OnPlayerDied()
        {
            SetOpen(false);
        }

        // Same trick as the inventory: build and show once invisibly so the first open is smooth.
        private IEnumerator Prewarm()
        {
            warming = true;
            canvasGroup.alpha = 0f;
            SetOpen(true);
            yield return null;
            yield return null;
            SetOpen(false);
            canvasGroup.alpha = 1f;
            warming = false;
        }

        private void Update()
        {
            if (warming)
                return;

            if (SpectatorMirror.Active)
            {
                UpdateMirror();
                return;
            }

            if (inventory.IsPlayerDead)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || UiKit.IsTypingInTextField())
                return;

            if (keyboard.cKey.wasPressedThisFrame)
                SetOpen(!isOpen);
            else if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
                SetOpen(false);
        }

        // A spectator's copy of the watched player's page: open while theirs is, or while the
        // spectator opened it (C) themselves.
        private void UpdateMirror()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.cKey.wasPressedThisFrame)
                    SpectatorMirror.Toggle(SpectatorMirror.Menu.Character);
                else if (keyboard.escapeKey.wasPressedThisFrame)
                    SpectatorMirror.Close(SpectatorMirror.Menu.Character);
            }

            bool open = SpectatorMirror.Shown(SpectatorMirror.Menu.Character);
            if (open != isOpen)
                SetOpen(open);
        }

        // The panel's X: a spectator closes only the copy they opened themselves.
        private void Close()
        {
            if (SpectatorMirror.Active)
                SpectatorMirror.Close(SpectatorMirror.Menu.Character);
            else
                SetOpen(false);
        }

        /// <summary>Opens or closes the page (C key, or the on-screen button on touch).</summary>
        public void SetOpen(bool open)
        {
            isOpen = open;
            panel.gameObject.SetActive(open);
            shell.gameObject.SetActive(open);
            if (closeButton != null)
                closeButton.gameObject.SetActive(open);
            if (open)
            {
                LayoutPanel();
                Refresh();
            }
        }

        private void BuildUI()
        {
            canvas = UiKit.NewCanvas("CharacterCanvas", transform, 800, out canvasGroup);
            var mobileScaler = canvas.GetComponent<TouchAwareScaler>();
            mobileScaler.MinimumTouchWidth = 480f;
            mobileScaler.PreferredTouchHeight = 400f;
            // The panel catches the pointer, so a click on it never also attacks or walks.
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup.blocksRaycasts = true;

            int lines = 0;
            foreach (Group g in Groups)
                lines += 1 + g.Stats.Length;

            float headerHeight = 84f;
            lines += 1 + SpecialRows; // the "Special" list (keystones and uniques)
            float panelHeight = headerHeight + lines * RowHeight + 30f;

            Image bg = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(bg);
            bg.raycastTarget = true;
            panel = bg.rectTransform;
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(0f, 0.5f);
            panel.pivot = new Vector2(0f, 0.5f);
            float canvasHeight = ((RectTransform)canvas.transform).rect.height;
            float verticalOffset = Mathf.Min(0f, (canvasHeight - panelHeight) * 0.5f - 16f);
            panel.anchoredPosition = new Vector2(30f, verticalOffset);
            panel.sizeDelta = new Vector2(PanelWidth, panelHeight);
            UiKit.Frame(panel);
            panel.gameObject.AddComponent<UiAppear>().offset = Vector2.zero;
            TouchMode.AddMenuBlocker(panel);

            Text title = UiKit.Heading(UiKit.NewText("Title", panel, "CHARACTER", 26, UiKit.Gold, TextAnchor.UpperCenter));
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -14f), new Vector2(PanelWidth, 34f));

            // The stats panel can be taller than a phone viewport. Keep its close control
            // attached to the screen edge instead of the panel's (possibly off-screen) top.
            closeButton = UiKit.CloseButton(panel, Close);
            closeButton.SetParent(canvas.transform, false);
            closeButton.anchorMin = new Vector2(0f, 1f);
            closeButton.anchorMax = new Vector2(0f, 1f);
            closeButton.pivot = new Vector2(1f, 1f);
            closeButton.anchoredPosition = new Vector2(30f + PanelWidth - 12f, -12f);
            closeButton.gameObject.SetActive(false);

            levelText = UiKit.NewText("Level", panel, "", 18, UiKit.DimText, TextAnchor.UpperCenter);
            UiKit.TopLeft(levelText.rectTransform, new Vector2(0f, -48f), new Vector2(PanelWidth, 26f));

            float y = -headerHeight;
            foreach (Group g in Groups)
            {
                Text header = UiKit.NewText("Header_" + g.Title, panel, g.Title.ToUpperInvariant(), TouchMode.Active ? 22 : 15, UiKit.Gold, TextAnchor.MiddleLeft);
                UiKit.TopLeft(header.rectTransform, new Vector2(26f, y), new Vector2(PanelWidth - 52f, RowHeight));
                y -= RowHeight;

                foreach (StatType stat in g.Stats)
                {
                    Text label = UiKit.NewText("Label_" + stat, panel, LabelFor(stat), TouchMode.Active ? 22 : 17, UiKit.TextColor, TextAnchor.MiddleLeft);
                    UiKit.TopLeft(label.rectTransform, new Vector2(40f, y), new Vector2(PanelWidth * 0.5f, RowHeight));
                    labelTexts[stat] = label;

                    Text value = UiKit.NewText("Value_" + stat, panel, "", TouchMode.Active ? 22 : 17, UiKit.TextColor, TextAnchor.MiddleRight);
                    UiKit.TopLeft(value.rectTransform, new Vector2(PanelWidth * 0.45f, y), new Vector2(PanelWidth * 0.55f - 40f, RowHeight));
                    valueTexts[stat] = value;

                    y -= RowHeight;
                }
            }

            Text specialHeader = UiKit.NewText("Header_Special", panel, "SPECIAL", TouchMode.Active ? 22 : 15, UiKit.Gold, TextAnchor.MiddleLeft);
            UiKit.TopLeft(specialHeader.rectTransform, new Vector2(26f, y), new Vector2(PanelWidth - 52f, RowHeight));
            y -= RowHeight;
            specialText = UiKit.NewText("Special", panel, "", TouchMode.Active ? 20 : 15, UiKit.MagicBlue, TextAnchor.UpperLeft);
            specialText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.TopLeft(specialText.rectTransform, new Vector2(40f, y - 4f), new Vector2(PanelWidth - 70f, SpecialRows * RowHeight));

            fullHeight = panelHeight;
            Image shellImage = UiKit.NewImage("CharacterWindow", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(shellImage);
            shellImage.raycastTarget = true;
            shell = shellImage.rectTransform;
            UiKit.Frame(shell);
            RectTransform viewport = UiKit.NewRect("StatsViewport", shell);
            UiKit.Stretch(viewport, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            panel.SetParent(viewport, false);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(0f, panelHeight);
            panel.anchoredPosition = Vector2.zero;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = panel;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            closeButton.SetAsLastSibling();
            LayoutPanel();
            panel.gameObject.SetActive(false);
            shell.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (isOpen) LayoutPanel();
        }

        private void LayoutPanel()
        {
            if (shell == null) return;
            shell.GetComponent<Image>().color = UiKit.ReadablePanelColor;
            float unit = Mathf.Max(0.01f, canvas.scaleFactor);
            float height = Screen.safeArea.height / unit;
            if (layoutForTouch != TouchMode.Active)
            {
                layoutForTouch = TouchMode.Active;
                float y = -84f;
                foreach (Group group in Groups)
                {
                    Text heading = panel.Find("Header_" + group.Title).GetComponent<Text>();
                    heading.fontSize = layoutForTouch ? 22 : 15;
                    UiKit.TopLeft(heading.rectTransform,new Vector2(26f,y),new Vector2(PanelWidth-52f,RowHeight));
                    y -= RowHeight;
                    foreach (StatType stat in group.Stats)
                    {
                        labelTexts[stat].fontSize = valueTexts[stat].fontSize = layoutForTouch ? 22 : 17;
                        UiKit.TopLeft(labelTexts[stat].rectTransform,new Vector2(40f,y),new Vector2(PanelWidth*.5f,RowHeight));
                        UiKit.TopLeft(valueTexts[stat].rectTransform,new Vector2(PanelWidth*.45f,y),new Vector2(PanelWidth*.55f-40f,RowHeight));
                        y -= RowHeight;
                    }
                }
                Text specialHeading=panel.Find("Header_Special").GetComponent<Text>();
                specialHeading.fontSize=layoutForTouch ? 22 : 15;
                UiKit.TopLeft(specialHeading.rectTransform,new Vector2(26f,y),new Vector2(PanelWidth-52f,RowHeight));
                y-=RowHeight;
                specialText.fontSize=layoutForTouch ? 20 : 15;
                UiKit.TopLeft(specialText.rectTransform,new Vector2(40f,y-4f),new Vector2(PanelWidth-70f,SpecialRows*RowHeight));
                fullHeight=-y+SpecialRows*RowHeight+30f;
                panel.sizeDelta=new Vector2(0f,fullHeight);
                Refresh();
            }
            float scale = 1f;
            shell.anchorMin = shell.anchorMax = shell.pivot = TouchMode.Active ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            shell.anchoredPosition = TouchMode.Active ? Vector2.zero : new Vector2(30f, 0f);
            shell.localScale = Vector3.one * scale;
            shell.sizeDelta = new Vector2(PanelWidth, Mathf.Min(fullHeight + 16f, (height - 48f) / scale));
            closeButton.anchorMin = closeButton.anchorMax = closeButton.pivot = TouchMode.Active ? Vector2.one : new Vector2(0f, 1f);
            closeButton.pivot = Vector2.one;
            closeButton.anchoredPosition = TouchMode.Active ? new Vector2(-16f, -16f) : new Vector2(30f + PanelWidth - 12f, -12f);
        }

        private static string LabelFor(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxLife: return "Life";
                case StatType.MaxMana: return "Mana";
                case StatType.CriticalChance: return "Attack Crit Chance";
                default: return StatFormatter.Label(stat);
            }
        }

        private static string ValueFor(StatType stat, float value)
        {
            string text = StatFormatter.Value(stat, value);
            bool increase = stat == StatType.MovementSpeed || stat == StatType.AttackSpeed;
            return increase && value > 0f ? "+" + text : text;
        }

        private void Refresh()
        {
            if (inventory == null || inventory.Stats == null || levelText == null)
                return;

            StatSheet sheet = inventory.Stats;
            levelText.text = "Level " + sheet.Level + "    XP " + sheet.Experience + " / " + sheet.ExperienceRequired;

            string green = UiKit.Hex(UiKit.BonusGreen);

            foreach (KeyValuePair<StatType, Text> pair in valueTexts)
            {
                StatType stat = pair.Key;
                string text = stat == StatType.CriticalChance
                    ? StatFormatter.Number(sheet.AttackCriticalChance) + "%"
                    : ValueFor(stat, sheet.Total(stat));

                float bonus = sheet.FromGear(stat);
                if (stat != StatType.CriticalChance && Mathf.Abs(bonus) > 0.001f)
                {
                    string sign = bonus > 0f ? "+" : "-";
                    text += "  <color=#" + green + ">(" + sign + StatFormatter.Number(Mathf.Abs(bonus)) + ")</color>";
                }

                pair.Value.text = text;
            }

            foreach (KeyValuePair<StatType, Text> pair in labelTexts)
                pair.Value.text = LabelFor(pair.Key) + Hint(pair.Key, sheet);

            // Special stats only show when something grants them.
            var special = new System.Text.StringBuilder();
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                float total = sheet.Total(stat);
                if (!StatFormatter.IsSpecial(stat) || SkillGrants.IsGrant(stat) || stat == StatType.CriticalChance || Mathf.Abs(total) < 0.001f)
                    continue;
                if (special.Length > 0)
                    special.Append('\n');
                special.Append(StatFormatter.ItemLine(new StatModifier(stat, total)));
            }
            specialText.text = special.Length > 0
                ? special.ToString()
                : "<color=#" + UiKit.Hex(UiKit.DimText) + ">None yet: keystones and unique items grant these.</color>";
            float specialHeight = Mathf.Max(SpecialRows * RowHeight, specialText.preferredHeight + RowHeight * 0.5f);
            specialText.rectTransform.sizeDelta = new Vector2(PanelWidth - 70f, specialHeight);
            fullHeight = -specialText.rectTransform.anchoredPosition.y + specialHeight + 30f;
            panel.sizeDelta = new Vector2(0f, fullHeight);
        }

        // What a defensive/resource stat actually does right now, shown small and dim after its name.
        private static string Hint(StatType stat, StatSheet sheet)
        {
            string hint;
            switch (stat)
            {
                case StatType.Armour:
                    float reduction = DefenceMath.ArmourReduction(sheet.Total(StatType.Armour), DefenceMath.ReferenceHit);
                    hint = Percent(reduction) + " less from a " + StatFormatter.Number(DefenceMath.ReferenceHit) + " hit";
                    break;
                case StatType.Evasion:
                    hint = Percent(sheet.EvadeChance) + " to evade attacks";
                    break;
                case StatType.MaxMana:
                    float regen = DefenceMath.ManaRegenPerSecond(sheet.Total(StatType.MaxMana), sheet.Total(StatType.Intelligence),
                        sheet.Total(StatType.ManaRegen), sheet.Total(StatType.PercentManaRegen));
                    hint = Percent(DefenceMath.TotalManaAbsorbShare(sheet.Total(StatType.ManaAbsorb))) + " of hits, +" +
                           regen.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "/s";
                    break;
                default:
                    return "";
            }

            return (TouchMode.Active ? "\n<size=16><color=#" : "  <size=13><color=#") + UiKit.Hex(UiKit.DimText) + ">" + hint + "</color></size>";
        }

        private static string Percent(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f) + "%";
        }
    }
}
