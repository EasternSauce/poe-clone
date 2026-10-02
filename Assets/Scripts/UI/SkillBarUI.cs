using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Skills;

namespace PoeClone.UI
{
    /// <summary>
    /// The skill bar (desktop: four squares at the bottom of the screen labelled Q E R F, with the
    /// cooldown sweeping down over them and a blue tint when there isn't enough mana) and the skills
    /// panel (K, or the SKL button on touch), which lists every skill and where it's slotted, with a
    /// button per slot to put it there. On touch the bar itself is TouchControlsUI's round buttons.
    /// Installed by <see cref="GameSessionController"/>; built at runtime.
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        private const float SlotSize = 72f;

        private static SkillBarUI instance;

        private class SlotView
        {
            public Image Back;
            public Image Cooldown;
            public Image NoMana;
            public Text Name;
        }

        private class Row
        {
            public SkillId Id;
            public Image Back;
            public Text Title;
            public Text[] SlotLabels;
        }

        private readonly List<SlotView> slotViews = new List<SlotView>();
        private readonly List<Row> rows = new List<Row>();

        private GameObject barRoot;
        private GameObject panelRoot;
        private PlayerSkills skills;

        public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

        public static void SetOpen(bool open)
        {
            if (instance == null)
                return;
            if (open && PassiveTreeUI.IsOpen)
                PassiveTreeUI.SetOpen(false);
            instance.panelRoot.SetActive(open);
            if (open)
                instance.RefreshRows();
        }

        private void Awake()
        {
            instance = this;
            Build();
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            var session = GameSessionController.Instance;
            bool spectator = session != null && session.Role == SessionRole.Spectator;

            if (skills == null)
            {
                skills = FindAnyObjectByType<PlayerSkills>();
                if (skills != null)
                    skills.Changed += RefreshRows;
            }

            bool show = skills != null && skills.enabled && !spectator;
            barRoot.SetActive(show && !TouchMode.Active);
            if (!show)
            {
                panelRoot.SetActive(false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.kKey.wasPressedThisFrame)
                    SetOpen(!panelRoot.activeSelf);
                else if (panelRoot.activeSelf && keyboard.escapeKey.wasPressedThisFrame)
                    SetOpen(false);
            }

            for (int k = 0; k < slotViews.Count; k++)
                UpdateSlot(slotViews[k], skills.Slot(k));
        }

        private void UpdateSlot(SlotView view, SkillId? id)
        {
            if (id == null)
            {
                view.Back.color = new Color(0.08f, 0.07f, 0.06f, 0.8f);
                view.Name.text = "";
                view.Cooldown.fillAmount = 0f;
                view.NoMana.enabled = false;
                return;
            }

            SkillDefinition skill = SkillBook.Get(id.Value);
            view.Back.color = new Color(skill.Color.r * 0.45f, skill.Color.g * 0.45f, skill.Color.b * 0.45f, 0.95f);
            view.Name.text = skill.Short;
            float left = skills.CooldownLeft(skill.Id);
            view.Cooldown.fillAmount = skill.Cooldown > 0f ? left / skill.Cooldown : 0f;
            view.NoMana.enabled = !skills.CanAfford(skill.Id);
        }

        private void RefreshRows()
        {
            if (skills == null)
                return;

            foreach (Row row in rows)
            {
                SkillDefinition skill = SkillBook.Get(row.Id);
                bool unlocked = skills.IsUnlocked(row.Id);
                row.Back.color = unlocked ? new Color(0.14f, 0.12f, 0.10f, 1f) : new Color(0.08f, 0.07f, 0.07f, 1f);
                row.Title.text = unlocked
                    ? "<color=#" + UiKit.Hex(skill.Color) + "><b>" + skill.Name + "</b></color>   <size=15><color=#" + UiKit.Hex(UiKit.DimText) + ">" +
                      Num(skill.ManaCost) + " mana · " + Num(skill.Cooldown) + "s cooldown</color></size>\n<size=15>" + skill.Description + "</size>"
                    : "<color=#" + UiKit.Hex(UiKit.DimText) + "><b>" + skill.Name + "</b>   <size=15>unlocks at level " + skill.UnlockLevel +
                      "</size>\n<size=15>" + skill.Description + "</size></color>";

                for (int k = 0; k < row.SlotLabels.Length; k++)
                {
                    bool here = skills.Slot(k) == row.Id;
                    row.SlotLabels[k].color = !unlocked ? new Color(1f, 1f, 1f, 0.2f) : here ? UiKit.Gold : UiKit.TextColor;
                    row.SlotLabels[k].transform.parent.GetComponent<Image>().color = here
                        ? new Color(0.45f, 0.35f, 0.15f, 1f)
                        : new Color(0.10f, 0.09f, 0.08f, 1f);
                }
            }
        }

        private static string Num(float value)
        {
            return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("SkillCanvas", transform, 60, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            // The bar: bottom centre.
            RectTransform bar = UiKit.NewRect("Bar", canvas.transform);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 18f);
            bar.sizeDelta = new Vector2(SkillBook.SlotCount * (SlotSize + 8f), SlotSize);
            barRoot = bar.gameObject;

            for (int k = 0; k < SkillBook.SlotCount; k++)
            {
                Image back = UiKit.NewImage("Slot" + k, bar, Color.black);
                UiKit.TopLeft(back.rectTransform, new Vector2(k * (SlotSize + 8f) + 4f, 0f), new Vector2(SlotSize, SlotSize));
                UiKit.AddOutline(back, UiKit.BorderColor, 2f);

                Text name = UiKit.NewText("Name", back.rectTransform, "", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
                name.fontStyle = FontStyle.Bold;
                UiKit.Stretch(name.rectTransform, 0f);

                Image noMana = UiKit.NewImage("NoMana", back.rectTransform, new Color(0.1f, 0.2f, 0.7f, 0.45f));
                UiKit.Stretch(noMana.rectTransform, 0f);

                Image cooldown = UiKit.NewImage("Cooldown", back.rectTransform, new Color(0f, 0f, 0f, 0.65f));
                cooldown.sprite = UiKit.Square;
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Vertical;
                cooldown.fillOrigin = (int)Image.OriginVertical.Top;
                UiKit.Stretch(cooldown.rectTransform, 0f);

                Text key = UiKit.NewText("Key", back.rectTransform, PlayerSkills.KeyLabel(k), 14, UiKit.Gold, TextAnchor.UpperLeft);
                UiKit.Stretch(key.rectTransform, 5f);

                slotViews.Add(new SlotView { Back = back, Cooldown = cooldown, NoMana = noMana, Name = name });
            }

            // The panel: centred list, one row per skill.
            const float rowHeight = 74f;
            const float width = 760f;
            float height = 70f + SkillBook.All.Length * (rowHeight + 6f) + 16f;

            Image panel = UiKit.NewImage("SkillsPanel", canvas.transform, UiKit.PanelColor);
            panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(width, height);
            UiKit.AddOutline(panel, UiKit.BorderColor, 3f);
            panelRoot = panel.gameObject;

            Text title = UiKit.NewText("Title", pr, "SKILLS", 26, UiKit.Gold, TextAnchor.UpperCenter);
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -14f), new Vector2(width, 34f));

            Text sub = UiKit.NewText("Sub", pr, "Pick a slot for each skill", 15, UiKit.DimText, TextAnchor.UpperCenter);
            UiKit.TopLeft(sub.rectTransform, new Vector2(0f, -44f), new Vector2(width, 22f));

            Image close = UiKit.NewImage("Close", pr, new Color(0.25f, 0.1f, 0.08f, 1f));
            close.raycastTarget = true;
            UiKit.TopLeft(close.rectTransform, new Vector2(width - 50f, -12f), new Vector2(38f, 38f));
            Text x = UiKit.NewText("X", close.rectTransform, "X", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(x.rectTransform, 0f);
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ => SetOpen(false);

            float y = -70f;
            foreach (SkillDefinition skill in SkillBook.All)
            {
                Image back = UiKit.NewImage("Row_" + skill.Id, pr, Color.black);
                UiKit.TopLeft(back.rectTransform, new Vector2(16f, y), new Vector2(width - 32f, rowHeight));

                Text text = UiKit.NewText("Text", back.rectTransform, "", 19, UiKit.TextColor, TextAnchor.MiddleLeft);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiKit.TopLeft(text.rectTransform, new Vector2(12f, 0f), new Vector2(width - 32f - 12f - 4f * 52f - 8f, rowHeight));

                var row = new Row { Id = skill.Id, Back = back, Title = text, SlotLabels = new Text[SkillBook.SlotCount] };
                for (int k = 0; k < SkillBook.SlotCount; k++)
                {
                    int slot = k;
                    SkillId id = skill.Id;
                    Image button = UiKit.NewImage("Slot" + k, back.rectTransform, Color.black);
                    button.raycastTarget = true;
                    UiKit.TopLeft(button.rectTransform, new Vector2(width - 32f - 4f * 52f - 4f + k * 52f, -(rowHeight - 44f) * 0.5f), new Vector2(44f, 44f));
                    UiKit.AddOutline(button, UiKit.BorderColor, 1.5f);
                    Text label = UiKit.NewText("Key", button.rectTransform, PlayerSkills.KeyLabel(k), 18, UiKit.TextColor, TextAnchor.MiddleCenter);
                    UiKit.Stretch(label.rectTransform, 0f);
                    row.SlotLabels[k] = label;
                    button.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
                    {
                        if (skills != null)
                            skills.Assign(slot, id);
                    };
                }

                rows.Add(row);
                y -= rowHeight + 6f;
            }
        }
    }
}
