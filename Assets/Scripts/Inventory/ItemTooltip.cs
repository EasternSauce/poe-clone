using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>Shared item text and tooltip sizing, independent of inventory input and panel state.</summary>
    internal static class ItemTooltip
    {
        private const string TooltipDividerMarker = "<color=#00000000>-</color>";

        /// <summary>With the character's <paramref name="stats"/>, requirements they don't meet show red.</summary>
        public static string BuildTooltipText(ItemData item, out int lineCount, StatSheet stats = null)
        {
            // Weapons name their kind ("Bow", "Axe"); everything else its slot type ("Body Armour").
            string typeName = item.Type == ItemType.Weapon ? item.WeaponType.ToString() : item.Type.ToString();
            string type = Regex.Replace(typeName, "(?<=.)([A-Z])", " $1");

            StringBuilder sb = new StringBuilder();
            string baseName = item.BaseName;
            sb.Append("<b><color=#").Append(UiKit.Hex(UiKit.RarityColor(item.Rarity))).Append(">").Append(item.Name);
            // Normal and magic names already contain their base; rare and unique names do not.
            if (!string.IsNullOrEmpty(baseName) && item.Name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) < 0)
                sb.Append(" ").Append(baseName);
            sb.Append("</color></b>\n");
            sb.Append("<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">").Append(type).Append("</color>");
            lineCount = 2;
            if (!string.IsNullOrEmpty(baseName))
            {
                sb.Append("\n").Append(item.Requirements.Describe(stats));
                lineCount++;
            }
            if (item.ItemLevel > 0 && item.Type != ItemType.Gold && item.Type != ItemType.Potion)
            {
                sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">Item level: ").Append(item.ItemLevel).Append("</color>");
                lineCount++;
            }
            if (item.StackCount > 1)
            {
                sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">Stack: ")
                    .Append(item.StackCount).Append("/").Append(item.MaxStack).Append("</color>");
                lineCount++;
            }
            if (!string.IsNullOrEmpty(item.Description))
            {
                sb.Append("\n\n<color=#").Append(UiKit.Hex(UiKit.TextColor)).Append(">")
                    .Append(item.Description).Append("</color>");
                lineCount += 3;
            }

            // Concise handedness labels for equipment.
            string handNote = null;
            if (item.Type == ItemType.Weapon && item.WeaponType == WeaponType.Bow)
                handNote = "Two-handed";
            else if (SlotRules.IsTwoHanded(item))
                handNote = "Two-handed";
            else if (item.Type == ItemType.Quiver)
                handNote = "Off hand";
            else if (item.Type == ItemType.Grimoire)
                handNote = "Off hand";
            else if (item.Type == ItemType.Weapon && item.WeaponType == WeaponType.Sceptre)
                handNote = "Its main skill summons and commands minions";
            if (handNote != null)
            {
                sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">").Append(handNote).Append("</color>");
                lineCount++;
            }

            // A weapon's own damage and pace, then its implicit stats, then the rolled ones.
            bool weapon = item.Type == ItemType.Weapon;
            float baseDamage = 0f;
            bool hasImplicits = false;
            bool hasExplicits = false;
            foreach (StatModifier m in item.Modifiers)
            {
                if (m.IsBase) baseDamage += m.Value;
                else if (m.Tier == StatModifier.ImplicitTier) hasImplicits = true;
                else hasExplicits = true;
            }

            if (weapon || hasImplicits || hasExplicits)
            {
                sb.Append("\n");
                lineCount++;
                bool sectionAbove = false;
                if (weapon)
                {
                    if (baseDamage > 0f)
                        AppendTooltipProperty(sb, "Physical Damage", StatFormatter.Number(baseDamage), ref lineCount);
                    AppendTooltipProperty(sb, "Attacks per Second",
                        SlotRules.BaseAttacksPerSecond(item.WeaponType).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), ref lineCount);
                    sectionAbove = true;
                }
                if (hasImplicits)
                {
                    AppendTooltipDivider(sb, sectionAbove, ref lineCount);
                    AppendTooltipModifiers(sb, item, true, ref lineCount);
                    sectionAbove = true;
                }
                if (hasExplicits)
                {
                    AppendTooltipDivider(sb, sectionAbove, ref lineCount);
                    AppendTooltipModifiers(sb, item, false, ref lineCount);
                }
            }

            string flavour = UniqueItems.FlavourFor(item);
            if (flavour != null)
            {
                // Wrapped by hand: the tooltip sizes itself to its longest line.
                sb.Append("\n\n<i><color=#").Append(UiKit.Hex(UiKit.UniqueOrange)).Append(">");
                lineCount += 2;
                int column = 0;
                foreach (string word in flavour.Split(' '))
                {
                    if (column > 0 && column + word.Length > 34)
                    {
                        sb.Append('\n');
                        lineCount++;
                        column = 0;
                    }
                    else if (column > 0)
                    {
                        sb.Append(' ');
                        column++;
                    }
                    sb.Append(word);
                    column += word.Length;
                }
                sb.Append("</color></i>");
            }

            return sb.ToString();
        }

        private static void AppendTooltipProperty(StringBuilder sb, string label, string value, ref int lineCount)
        {
            sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">").Append(label).Append(": </color><color=#")
                .Append(UiKit.Hex(UiKit.TextColor)).Append(">").Append(value).Append("</color>");
            lineCount++;
        }

        private static void AppendTooltipDivider(StringBuilder sb, bool sectionAbove, ref int lineCount)
        {
            if (!sectionAbove)
                return;
            sb.Append("\n").Append(TooltipDividerMarker);
            lineCount++;
        }

        private static void AppendTooltipModifiers(StringBuilder sb, ItemData item, bool implicits, ref int lineCount)
        {
            // Keep granted skills first within each modifier section.
            for (int pass = 0; pass < 2; pass++)
            {
                bool skills = pass == 0;
                foreach (StatModifier m in item.Modifiers)
                {
                    if (m.IsBase || (m.Tier == StatModifier.ImplicitTier) != implicits || SkillGrants.IsGrant(m.Stat) != skills)
                        continue;
                    if (skills && item.SkillProgress != null && item.MainSkillGrant == m.Stat)
                    {
                        sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.Gold)).Append(">")
                            .Append(SkillGrants.SkillName(m.Stat)).Append(" · Level ").Append(item.SkillProgress.level)
                            .Append("/10 · ").Append(item.SkillProgress.Available).Append(" tree points available</color>");
                        lineCount++;
                        continue;
                    }
                    string tier = item.Rarity == ItemRarity.Unique || m.Tier == StatModifier.ImplicitTier ? null :
                        m.Tier > 0 ? "T" + m.Tier : "Tier unknown";
                    sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.MagicBlue)).Append(">")
                        .Append(StatFormatter.ItemLine(m));
                    if (tier != null)
                        sb.Append(" [").Append(tier).Append("]");
                    sb.Append("</color>");
                    lineCount++;
                }
            }
        }

        // A tooltip's size for its text: this width, and as tall as the text really lays out (long
        // stat lines wrap, which a count of lines misses), never less than the estimate. The text
        // sits 10 px in from every edge (see BuildTooltip).
        public static void ClampTooltip(RectTransform rect)
        {
            float scale = rect.GetComponentInParent<Canvas>().scaleFactor;
            Vector2 size = rect.sizeDelta * scale;
            Vector3 at = rect.position;
            at.x = Mathf.Clamp(at.x, size.x * rect.pivot.x, Mathf.Max(size.x * rect.pivot.x, Screen.width - size.x * (1f - rect.pivot.x)));
            at.y = Mathf.Clamp(at.y, size.y * rect.pivot.y, Mathf.Max(size.y * rect.pivot.y, Screen.height - size.y * (1f - rect.pivot.y)));
            rect.position = at;
        }

        public static Vector2 FitTooltip(Text text, float width, float estimatedHeight)
        {
            float scale = text.canvas != null ? text.canvas.scaleFactor : 1f;
            float maxWidth = Mathf.Max(40f, Screen.width / Mathf.Max(.01f, scale) - 24f);
            var unwrapped = text.GetGenerationSettings(Vector2.zero);
            float preferred = text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text, unwrapped) / Mathf.Max(.01f, text.pixelsPerUnit) + 24f;
            width = Mathf.Min(maxWidth, Mathf.Max(width, Mathf.Min(TouchMode.Active ? maxWidth : 620f, preferred)));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width - 20f, 0f));
            float laidOut = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / Mathf.Max(0.01f, text.pixelsPerUnit);
            UpdateTooltipDividers(text, settings, width - 20f);
            return new Vector2(width, Mathf.Max(estimatedHeight * 0.6f, laidOut + 26f));
        }

        // Draws a line at every section marker in the tooltip (base, implicit and rolled stats).
        private static void UpdateTooltipDividers(Text text, TextGenerationSettings settings, float width)
        {
            int marker = text.text.IndexOf(TooltipDividerMarker, System.StringComparison.Ordinal);
            int count = 0;
            for (; marker >= 0; count++)
            {
                string name = "ModifierDivider" + count;
                Transform existing = text.transform.Find(name);
                Image divider = existing != null ? existing.GetComponent<Image>() :
                    UiKit.NewImage(name, text.transform, UiKit.TextColor);
                divider.gameObject.SetActive(true);
                divider.color = UiKit.TextColor;
                // Measure the prefix with the same wrapping as the complete tooltip, so long
                // modifier lines and the larger touch font move the divider with their section.
                int end = marker + TooltipDividerMarker.Length;
                string prefix = text.text.Substring(0, end);
                float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(prefix, settings) /
                    Mathf.Max(0.01f, text.pixelsPerUnit);
                float rowHeight = text.fontSize * text.lineSpacing;
                // A third of the width, centred a third of the way across: under the left-aligned lines.
                UiKit.TopLeft(divider.rectTransform, new Vector2(width / 6f, -height + rowHeight * 0.5f),
                    new Vector2(width / 3f, 2f));
                marker = text.text.IndexOf(TooltipDividerMarker, end, System.StringComparison.Ordinal);
            }
            for (Transform spare; (spare = text.transform.Find("ModifierDivider" + count)) != null; count++)
                spare.gameObject.SetActive(false);
        }
    }
}
