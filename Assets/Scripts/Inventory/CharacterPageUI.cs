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

        private const float RowHeight = 26f;
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
            new Group("Offence", StatType.PhysicalDamage, StatType.AttackSpeed),
            new Group("Resistances", StatType.FireResistance, StatType.ColdResistance, StatType.LightningResistance),
            new Group("Movement", StatType.MovementSpeed)
        };

        private readonly Dictionary<StatType, Text> valueTexts = new Dictionary<StatType, Text>();
        private readonly Dictionary<StatType, Text> labelTexts = new Dictionary<StatType, Text>();

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform panel;
        private Text levelText;
        private Text specialText;
        private const int SpecialRows = 5;
        private bool isOpen;
        private bool warming;
        private bool ownOpen; // spectators: opened by the spectator themselves (see SpectatorMirror)

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
            bool remote = SpectatorMirror.CharacterOpen;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.cKey.wasPressedThisFrame && !remote)
                    ownOpen = !ownOpen;
                else if (keyboard.escapeKey.wasPressedThisFrame)
                    ownOpen = false;
            }

            bool open = remote || ownOpen;
            if (open != isOpen)
                SetOpen(open);
        }

        // The panel's X: a spectator closes only the copy they opened themselves.
        private void Close()
        {
            if (SpectatorMirror.Active)
                ownOpen = false;
            else
                SetOpen(false);
        }

        /// <summary>Opens or closes the page (C key, or the on-screen button on touch).</summary>
        public void SetOpen(bool open)
        {
            isOpen = open;
            panel.gameObject.SetActive(open);
            if (open)
                Refresh();
        }

        private void BuildUI()
        {
            canvas = UiKit.NewCanvas("CharacterCanvas", transform, 49, out canvasGroup);
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
            panel.anchoredPosition = new Vector2(30f, 0f);
            panel.sizeDelta = new Vector2(PanelWidth, panelHeight);
            UiKit.AddOutline(bg, UiKit.BorderColor, 3f);
            TouchMode.AddMenuBlocker(panel);

            Text title = UiKit.NewText("Title", panel, "CHARACTER", 26, UiKit.Gold, TextAnchor.UpperCenter);
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -14f), new Vector2(PanelWidth, 34f));

            UiKit.CloseButton(panel, Close);

            levelText = UiKit.NewText("Level", panel, "", 18, UiKit.DimText, TextAnchor.UpperCenter);
            UiKit.TopLeft(levelText.rectTransform, new Vector2(0f, -48f), new Vector2(PanelWidth, 26f));

            float y = -headerHeight;
            foreach (Group g in Groups)
            {
                Text header = UiKit.NewText("Header_" + g.Title, panel, g.Title.ToUpperInvariant(), 15, UiKit.Gold, TextAnchor.MiddleLeft);
                UiKit.TopLeft(header.rectTransform, new Vector2(26f, y), new Vector2(PanelWidth - 52f, RowHeight));
                y -= RowHeight;

                foreach (StatType stat in g.Stats)
                {
                    Text label = UiKit.NewText("Label_" + stat, panel, LabelFor(stat), 17, UiKit.TextColor, TextAnchor.MiddleLeft);
                    UiKit.TopLeft(label.rectTransform, new Vector2(40f, y), new Vector2(PanelWidth * 0.5f, RowHeight));
                    labelTexts[stat] = label;

                    Text value = UiKit.NewText("Value_" + stat, panel, "", 17, UiKit.TextColor, TextAnchor.MiddleRight);
                    UiKit.TopLeft(value.rectTransform, new Vector2(PanelWidth * 0.45f, y), new Vector2(PanelWidth * 0.55f - 40f, RowHeight));
                    valueTexts[stat] = value;

                    y -= RowHeight;
                }
            }

            Text specialHeader = UiKit.NewText("Header_Special", panel, "SPECIAL", 15, UiKit.Gold, TextAnchor.MiddleLeft);
            UiKit.TopLeft(specialHeader.rectTransform, new Vector2(26f, y), new Vector2(PanelWidth - 52f, RowHeight));
            y -= RowHeight;
            specialText = UiKit.NewText("Special", panel, "", 15, UiKit.MagicBlue, TextAnchor.UpperLeft);
            specialText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.TopLeft(specialText.rectTransform, new Vector2(40f, y - 4f), new Vector2(PanelWidth - 70f, SpecialRows * RowHeight));

            panel.gameObject.SetActive(false);
        }

        private static string LabelFor(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxLife: return "Life";
                case StatType.MaxMana: return "Mana";
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
                string text = ValueFor(stat, sheet.Total(stat));

                float bonus = sheet.FromGear(stat);
                if (Mathf.Abs(bonus) > 0.001f)
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
                if (!StatFormatter.IsSpecial(stat) || Mathf.Abs(total) < 0.001f)
                    continue;
                if (special.Length > 0)
                    special.Append('\n');
                special.Append(StatFormatter.ItemLine(new StatModifier(stat, total)));
            }
            specialText.text = special.Length > 0
                ? special.ToString()
                : "<color=#" + UiKit.Hex(UiKit.DimText) + ">None yet: keystones and unique items grant these.</color>";
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
                    hint = Percent(DefenceMath.EvadeChance(sheet.Total(StatType.Evasion))) + " to evade";
                    break;
                case StatType.MaxMana:
                    float regen = DefenceMath.ManaRegenPerSecond(sheet.Total(StatType.MaxMana), sheet.Total(StatType.Intelligence),
                        sheet.Total(StatType.ManaRegen));
                    hint = Percent(DefenceMath.TotalManaAbsorbShare(sheet.Total(StatType.ManaAbsorb))) + " of hits, +" +
                           regen.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "/s";
                    break;
                default:
                    return "";
            }

            return "  <size=13><color=#" + UiKit.Hex(UiKit.DimText) + ">" + hint + "</color></size>";
        }

        private static string Percent(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f) + "%";
        }
    }
}
