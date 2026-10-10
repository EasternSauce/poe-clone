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
    /// The skill bar (desktop: squares at the bottom of the screen - first the attack (left click:
    /// the staff's spell, or the weapon's icon), then Q E R F, 1-4, right and side mouse
    /// buttons - with the cooldown sweeping down over them and a blue tint when there isn't enough
    /// mana) and the skills panel (SKL button on touch), which assigns usable equipment skills to Button 1 through Button 4.
    /// Each granting item gets its own choice; inaccessible skills are omitted. Clicking a square on the desktop bar opens a list of skills and potions above it.
    /// Hovering a square (or a skill in that list) shows the skill's tooltip: level, damage, cost, cooldown and description.
    /// On touch the bar itself is TouchControlsUI's round buttons.
    /// Installed by <see cref="GameSessionController"/>; built at runtime.
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        private const float SlotSize = 52f;
        private const float AttackGap = 12f;
        private const float TopRowY = -24f;
        private const float BottomRowY = -100f;
        private const float BarHeight = 152f;
        private const float SkillsPanelWidth = 760f;
        private readonly Image[] mobileButtons = new Image[SkillBook.TouchSlotCount];
        private readonly Text[] mobileLabels = new Text[SkillBook.TouchSlotCount];
        private int selectedMobileButton;
        private Text selectionHint;
        private Text emptySkillsText;

        private static SkillBarUI instance;

        private class SlotView
        {
            public Image Back;
            public Image Icon;
            public Image Cooldown;
            public Image NoMana;
            public Text Name;
            public Image Ring;   // spins round a bow skill that's toggled on
        }

        private class Row
        {
            public SkillId Id;
            public PlayerSkills.SkillGrant? Grant;
            public Image Back;
            public Text Title;
            public Text Assignment;
        }

        private readonly List<SlotView> slotViews = new List<SlotView>();
        private Sprite healthPotionIcon;
        private Sprite manaPotionIcon;
        private SlotView attackView;
        private Image attackIcon;
        private readonly List<Row> rows = new List<Row>();

        private GameObject barRoot;
        private GameObject panelRoot;
        private RectTransform barRect;
        private RectTransform picker;
        private RectTransform pickerContent;
        private RectTransform panelContent;
        private float panelWidth = SkillsPanelWidth;
        private ScrollRect pickerScroll;
        private readonly List<GameObject> pickerRows = new List<GameObject>();
        private readonly List<PlayerSkills.SkillGrant> pickerGrants = new List<PlayerSkills.SkillGrant>();
        private int pickerSlot = -1;
        private PlayerSkills skills;
        private bool rowsDirty = true;
        private Canvas canvas;
        private RectTransform tooltip;
        private Text tooltipText;
        private List<PlayerSkills.SkillGrant> builtGrants;

        public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

        /// <summary>The bind list over the bar is open (other things over the bar make way).</summary>
        public static bool PickerOpen => instance != null && instance.pickerSlot >= 0;

        public static void SetOpen(bool open)
        {
            if (instance == null)
                return;
            if (!TouchMode.Active)
                open = false;
            if (open && PassiveTreeUI.IsOpen)
            {
                PassiveTreeUI.SetOpen(false);
                if (PassiveTreeUI.IsOpen) return;
            }
            if (open) instance.EnsureMobilePanel();
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

        private void OnSkillsChanged()
        {
            // XP updates also raise this event. Keep closed menus out of the killing frame.
            rowsDirty = true;
        }

        private void OnDestroy()
        {
            if (skills != null) skills.Changed -= OnSkillsChanged;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            var session = GameSessionController.Instance;
            bool spectator = session != null && session.Role == SessionRole.Spectator;

            if (skills == null)
            {
                skills = FindAnyObjectByType<PlayerSkills>();
                if (skills != null)
                {
                    skills.Changed += OnSkillsChanged;
                    rowsDirty = true;
                }
            }

            if (spectator && skills != null)
            {
                UpdateMirror();
                return;
            }

            bool show = skills != null && skills.enabled && !spectator;
            barRoot.SetActive(show && !TouchMode.Active);
            if (!TouchMode.Active && panelRoot.activeSelf)
                panelRoot.SetActive(false);
            if (!show)
            {
                panelRoot.SetActive(false);
                ClosePicker();
                HideTooltip();
                return;
            }

            UpdatePicker();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (TouchMode.Active && keyboard.kKey.wasPressedThisFrame)
                    SetOpen(!panelRoot.activeSelf);
                else if (panelRoot.activeSelf && keyboard.escapeKey.wasPressedThisFrame)
                    SetOpen(false);
            }

            if (panelRoot.activeSelf && rowsDirty)
                RefreshRows();

            // TouchControlsUI draws the mobile slots; these desktop views stay hidden.
            if (TouchMode.Active)
            {
                HideTooltip();
                return;
            }

            for (int k = 0; k < slotViews.Count; k++)
            {
                slotViews[k].Back.gameObject.SetActive(true);
                UpdateSlot(slotViews[k], skills.Slot(k), k);
                int potion = Player.PlayerPotions.PotionAt(k);
                if (potion != 0 && skills.Slot(k) == null)
                    UpdatePotionSlot(slotViews[k], potion == 1);
            }
            UpdateAttack();
            UpdateTooltip();
        }

        // The attack square: the staff's spell, the bow skill that's on, an arrow for a plain bow,
        // else the icon of whatever is in the main hand.
        private void UpdateAttack()
        {
            SkillId? main = skills.MainSkill ?? skills.ActiveBowSkill;
            if (main != null)
            {
                attackIcon.enabled = false;
                UpdateSlot(attackView, main);
                attackView.Ring.enabled = false;
                return;
            }

            attackView.Cooldown.fillAmount = 0f;
            attackView.NoMana.enabled = false;
            attackView.Ring.enabled = false;
            attackView.Back.color = new Color(0.10f, 0.09f, 0.08f, 0.9f);
            attackView.Icon.enabled = false;
            ItemData weapon = skills.GetComponent<PlayerInventory>()?.Equipment.GetActive(EquipSlot.MainHand);
            if (weapon != null && weapon.WeaponType == WeaponType.Bow)
            {
                attackIcon.enabled = true;
                attackIcon.sprite = IconFactory.Arrow;
                attackIcon.color = new Color(0.92f, 0.86f, 0.72f, 1f);
                attackView.Name.text = "";
            }
            else if (weapon != null)
            {
                attackIcon.enabled = true;
                attackIcon.sprite = ItemArt.Icon(weapon);
                attackIcon.color = weapon.IconTint;
                attackView.Name.text = "";
            }
            else
            {
                attackIcon.enabled = false;
                attackView.Name.alignment = TextAnchor.MiddleCenter;
                attackView.Name.text = "<size=13>FIST</size>";
            }
        }

        private float SlotX(int slot)
        {
            int column = slot >= 8 ? slot - 4 : slot % 4;
            return SlotSize + AttackGap + column * (SlotSize + 6f) + 4f;
        }

        // A spectator's copy of the watched player's skills panel (read-only): open while theirs
        // is, or while the spectator opened it on touch. The bar itself stays hidden.
        private void UpdateMirror()
        {
            if (barRoot.activeSelf)
                barRoot.SetActive(false);
            ClosePicker();
            HideTooltip();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (TouchMode.Active && keyboard.kKey.wasPressedThisFrame)
                    SpectatorMirror.Toggle(SpectatorMirror.Menu.Skills);
                else if (keyboard.escapeKey.wasPressedThisFrame)
                    SpectatorMirror.Close(SpectatorMirror.Menu.Skills);
            }

            bool open = TouchMode.Active && SpectatorMirror.Shown(SpectatorMirror.Menu.Skills);
            if (panelRoot.activeSelf != open)
            {
                if (open) EnsureMobilePanel();
                panelRoot.SetActive(open);
                if (open)
                    RefreshRows();
            }
            if (open && rowsDirty)
                RefreshRows();
        }

        // ------------------------------------------------------------------ picker

        private const float PickerRowHeight = 40f;
        private const float PickerWidth = 280f;

        private void OpenPicker(int slot)
        {
            if (skills == null || TouchMode.Active)
                return;
            if (pickerSlot == slot)
            {
                ClosePicker();
                return;
            }

            pickerSlot = slot;
            RebuildPickerRows();
            for (int k = 0; k < pickerRows.Count; k++)
            {
                var rt = (RectTransform)pickerRows[k].transform;
                UiKit.TopLeft(rt, new Vector2(4f, -4f - k * PickerRowHeight), new Vector2(PickerWidth - 8f, PickerRowHeight - 4f));
                bool here = k < pickerGrants.Count ? skills.MatchesGrant(slot, pickerGrants[k]) :
                    k == pickerGrants.Count ? Player.PlayerPotions.PotionAt(slot) == 1 && skills.Slot(slot) == null :
                    k == pickerGrants.Count + 1 ? Player.PlayerPotions.PotionAt(slot) == 2 && skills.Slot(slot) == null :
                    skills.Slot(slot) == null && Player.PlayerPotions.PotionAt(slot) == 0;
                pickerRows[k].GetComponent<Image>().color = here ? new Color(0.45f, 0.35f, 0.15f, 1f) : new Color(0.12f, 0.10f, 0.09f, 1f);
            }

            picker.sizeDelta = new Vector2(PickerWidth, Mathf.Min(440f, pickerRows.Count * PickerRowHeight + 8f));
            pickerContent.sizeDelta = new Vector2(0f, pickerRows.Count * PickerRowHeight + 8f);
            pickerScroll.verticalNormalizedPosition = 1f;
            // Above the clicked square.
            float slotCentre = SlotX(slot) + SlotSize * 0.5f - barRect.sizeDelta.x * 0.5f;
            float maxX = Mathf.Max(0f, (Screen.width / picker.GetComponentInParent<Canvas>().scaleFactor - PickerWidth) * 0.5f - 8f);
            picker.anchoredPosition = new Vector2(Mathf.Clamp(slotCentre, -maxX, maxX), 18f + BarHeight + 10f);
            picker.gameObject.SetActive(true);
        }

        private void ClosePicker()
        {
            pickerSlot = -1;
            if (picker != null)
                picker.gameObject.SetActive(false);
        }

        // A click anywhere but the list (or the bar, which toggles it itself) closes it, as does Escape.
        private void UpdatePicker()
        {
            if (pickerSlot < 0)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePicker();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                Vector2 at = mouse.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint(picker, at, null) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(barRect, at, null))
                    ClosePicker();
            }
        }

        private void Pick(int row)
        {
            if (skills == null || pickerSlot < 0)
                return;
            if (row < pickerGrants.Count)
            {
                var grant = pickerGrants[row];
                skills.Assign(pickerSlot, grant.Id, grant.Source, grant.GrantLevel);
            }
            else
            {
                skills.ClearSlot(pickerSlot);
                Player.PlayerPotions.SetPotionAt(pickerSlot, row == pickerGrants.Count ? 1 :
                    row == pickerGrants.Count + 1 ? 2 : 0);
            }
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.Sfx("ui_click"));
            ClosePicker();
        }

        private void BuildPicker(Transform canvas)
        {
            Image back = UiKit.NewImage("SkillPicker", canvas, UiKit.PanelColor);
            UiKit.Grain(back);
            back.raycastTarget = true;
            picker = back.rectTransform;
            picker.anchorMin = picker.anchorMax = new Vector2(0.5f, 0f);
            picker.pivot = new Vector2(0.5f, 0f);
            UiKit.AddOutline(back, UiKit.BorderColor, 2f);

            RectTransform viewport = UiKit.NewRect("Viewport", picker);
            UiKit.Stretch(viewport, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            pickerContent = UiKit.NewRect("Content", viewport);
            pickerContent.anchorMin = new Vector2(0f, 1f);
            pickerContent.anchorMax = new Vector2(1f, 1f);
            pickerContent.pivot = new Vector2(0.5f, 1f);
            pickerContent.anchoredPosition = Vector2.zero;
            pickerScroll = viewport.gameObject.AddComponent<ScrollRect>();
            pickerScroll.content = pickerContent;
            pickerScroll.viewport = viewport;
            pickerScroll.horizontal = false;
            pickerScroll.movementType = ScrollRect.MovementType.Clamped;
            pickerScroll.scrollSensitivity = 32f;

            picker.gameObject.SetActive(false);
        }

        private void RebuildPickerRows()
        {
            foreach (GameObject row in pickerRows)
                if (row != null) Destroy(row);
            pickerRows.Clear();
            pickerGrants.Clear();
            if (skills != null) pickerGrants.AddRange(skills.AvailableGrants());
            for (int k = 0; k < pickerGrants.Count + 3; k++)
            {
                int row = k;
                string label;
                if (k < pickerGrants.Count)
                {
                    var grant = pickerGrants[k];
                    SkillDefinition skill = SkillBook.Get(grant.Id);
                    label = skill.Name +
                        " <size=14>Lv " + skills.GrantLevelWithBonuses(grant) + " · " + grant.Item.Name + " (" + grant.Source + ")</size>";
                }
                else if (k == pickerGrants.Count)
                    label = "<color=#ff7777><b>HP</b></color>  Health Potion";
                else if (k == pickerGrants.Count + 1)
                    label = "<color=#809aff><b>MP</b></color>  Mana Potion";
                else
                {
                    label = "<color=#" + UiKit.Hex(UiKit.DimText) + ">(empty)</color>";
                }

                Image item = UiKit.NewImage("Pick_" + k, pickerContent, Color.black);
                item.raycastTarget = true;
                Text text = UiKit.NewText("Text", item.rectTransform, label, 18, UiKit.TextColor, TextAnchor.MiddleLeft);
                UiKit.Stretch(text.rectTransform, 10f);
                if (k < pickerGrants.Count + 2)
                    text.rectTransform.offsetMin = new Vector2(42f, 10f);
                if (k < pickerGrants.Count)
                {
                    SkillDefinition skill = SkillBook.Get(pickerGrants[k].Id);
                    Image icon = UiKit.NewImage("SkillIcon", item.rectTransform, skill.Color);
                    icon.sprite = SkillIconFactory.Get(skill.Id);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    UiKit.TopLeft(icon.rectTransform, new Vector2(8f, -5f), new Vector2(28f, 28f));
                }
                else if (k < pickerGrants.Count + 2)
                {
                    Image icon = UiKit.NewImage("PotionIcon", item.rectTransform, Color.white);
                    icon.sprite = k == pickerGrants.Count ? healthPotionIcon : manaPotionIcon;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    UiKit.TopLeft(icon.rectTransform, new Vector2(8f, -5f), new Vector2(28f, 28f));
                }
                item.gameObject.AddComponent<TouchPointerRelay>().Up += _ => Pick(row);
                pickerRows.Add(item.gameObject);
            }

        }

        private void UpdateSlot(SlotView view, SkillId? id, int slot = -1)
        {
            view.Ring.enabled = false;
            if (id == null)
            {
                view.Back.color = new Color(0.08f, 0.07f, 0.06f, 0.8f);
                view.Icon.enabled = false;
                view.Name.text = "";
                view.Cooldown.fillAmount = 0f;
                view.NoMana.enabled = false;
                return;
            }

            SkillDefinition skill = SkillBook.Get(id.Value);
            view.Icon.enabled = true;
            view.Icon.sprite = SkillIconFactory.Get(skill.Id);
            view.Name.alignment = TextAnchor.LowerRight;
            if (slot >= 0 ? skills.IsToggledOnAt(slot) : skills.IsToggledOn(skill.Id))
            {
                view.Ring.enabled = true;
                SpinRing(view.Ring, skill.Color);
            }
            int level = slot >= 0 ? skills.LevelAt(slot) : skills.Level(skill.Id);
            if (level <= 0)
            {
                // Slotted, but no worn gear grants it right now: waits there, greyed out.
                view.Back.color = new Color(0.10f, 0.09f, 0.08f, 0.8f);
                view.Icon.color = new Color(1f, 1f, 1f, 0.25f);
                view.Name.text = "";
                view.Cooldown.fillAmount = 0f;
                view.NoMana.enabled = false;
                return;
            }

            view.Back.color = new Color(skill.Color.r * 0.45f, skill.Color.g * 0.45f, skill.Color.b * 0.45f, 0.95f);
            view.Icon.color = Color.white;
            view.Name.text = "<size=9>lvl </size><size=12>" + level + "</size>";
            float left = slot >= 0 ? skills.CooldownLeftAt(slot) : skills.CooldownLeft(skill.Id);
            float total = slot >= 0 ? skills.CooldownTotalAt(slot) : skills.CooldownTotal(skill.Id);
            view.Cooldown.fillAmount = total > 0f ? left / total : 0f;
            view.NoMana.enabled = slot >= 0 ? !skills.CanAffordAt(slot) : !skills.CanAfford(skill.Id);
        }

        private void UpdatePotionSlot(SlotView view, bool health)
        {
            var inventory = skills.GetComponent<PlayerInventory>();
            int count = inventory != null ? inventory.Potions(health) : 0;
            view.Back.color = health ? new Color(.42f,.10f,.10f,.95f) : new Color(.10f,.18f,.43f,.95f);
            view.Icon.enabled = true;
            view.Icon.sprite = health ? healthPotionIcon : manaPotionIcon;
            view.Icon.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            view.Name.alignment = TextAnchor.LowerRight;
            view.Name.text = "<size=12>" + count + "</size>";
            view.Cooldown.fillAmount = 0f;
            view.NoMana.enabled = false;
            view.Ring.enabled = false;
        }

        /// <summary>A bow skill's "on" ring: a broken circle turning round the button, gently pulsing.</summary>
        public static void SpinRing(Image ring, Color color)
        {
            float t = Time.unscaledTime;
            ring.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * 220f);
            Color c = Color.Lerp(color, Color.white, 0.35f);
            c.a = 0.75f + 0.25f * Mathf.Sin(t * 6f);
            ring.color = c;
        }

        /// <summary>The ring itself (see <see cref="SpinRing"/>), this far outside the button's edge.</summary>
        public static Image NewRing(RectTransform button, float margin)
        {
            Image ring = UiKit.NewImage("BowSkillOn", button, Color.white);
            ring.sprite = UiKit.Ring;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillAmount = 0.72f;
            ring.raycastTarget = false;
            UiKit.Stretch(ring.rectTransform, -margin);
            ring.enabled = false;
            return ring;
        }

        private void RefreshRows()
        {
            rowsDirty = true;
            if (skills == null || panelRoot == null || !panelRoot.activeSelf)
                return;
            BuildRows();
            rowsDirty = false;

            for (int k = 0; k < SkillBook.TouchSlotCount; k++)
            {
                SkillId? id = skills.Slot(k);
                string name = id.HasValue && skills.LevelAt(k) > 0 ? SkillBook.Get(id.Value).Name :
                    "Empty";
                mobileLabels[k].text = "<b>Button " + (k + 1) + "</b>\n" + name;
                mobileButtons[k].color = k == selectedMobileButton
                    ? new Color(0.42f, 0.31f, 0.13f, 1f) : new Color(0.14f, 0.12f, 0.10f, 1f);
            }
            selectionHint.text = SpectatorMirror.Active ? "Viewing skill buttons" :
                "Choose a skill for Button " + (selectedMobileButton + 1);
            emptySkillsText.gameObject.SetActive(rows.Count == 0);

            float y = 0f;
            foreach (Row row in rows)
            {
                var grant = row.Grant.Value;
                var skill = SkillBook.Get(row.Id);
                int level = skills.GrantLevelWithBonuses(grant);
                string cost = Num(skills.ManaCostForGrant(grant)) + " " + skills.CostResource;
                string timing = skill.Bow ? "per shot" : Num(skills.CooldownForGrant(grant)) + "s cooldown";
                string damage = skills.DamageSummary(skill.Id, level);
                string bindings = "";
                for (int k = 0; k < SkillBook.TouchSlotCount; k++)
                    if (skills.MatchesGrant(k, grant))
                        bindings += (bindings.Length == 0 ? "Button " : ", ") + (k + 1);
                row.Title.text = "<b>" + skill.Name + "</b>  <color=#" + UiKit.Hex(UiKit.Gold) +
                    ">Level " + level + "</color>\n<size=14>From " + grant.Item.Name + " (" + grant.Source +
                    ") · " + cost + " · " + timing + "</size>\n<size=14>" +
                    (damage == null ? "" : (skill.Summon ? "Minions: " : "Damage: ") + damage + "\n") + skill.Description +
                    (bindings.Length == 0 ? "" : "\n<color=#" + UiKit.Hex(UiKit.Gold) + ">" + bindings + "</color>") + "</size>";
                bool assigned = skills.MatchesGrant(selectedMobileButton, grant);
                row.Back.color = assigned ? new Color(0.24f, 0.20f, 0.12f, 1f) : new Color(0.14f, 0.12f, 0.10f, 1f);
                row.Assignment.text = SpectatorMirror.Active ? "" : assigned ? "Assigned" : "Assign";
                float height = Mathf.Max(100f, row.Title.preferredHeight + 20f);
                UiKit.TopLeft(row.Back.rectTransform, new Vector2(8f, y), new Vector2(panelWidth - 32f, height));
                row.Title.rectTransform.sizeDelta = new Vector2(panelWidth - 208f, height - 16f);
                var action = (RectTransform)row.Assignment.transform.parent;
                UiKit.TopLeft(action, new Vector2(panelWidth - 142f, -(height - 48f) * 0.5f), new Vector2(102f, 48f));
                y -= height + 8f;
            }
            panelContent.sizeDelta = new Vector2(0f, Mathf.Max(rows.Count == 0 ? 80f : 0f, -y));
        }

        // ------------------------------------------------------------------ tooltip

        private const float TooltipWidth = 360f;

        private void BuildTooltip(Transform parent)
        {
            Image back = UiKit.NewImage("SkillTooltip", parent, new Color(0.07f, 0.07f, 0.08f, 0.97f));
            UiKit.AddOutline(back, UiKit.BorderColor, 2f);
            tooltip = back.rectTransform;
            tooltip.anchorMin = tooltip.anchorMax = tooltip.pivot = Vector2.zero;
            tooltipText = UiKit.NewText("Text", tooltip, "", 16, UiKit.TextColor, TextAnchor.UpperLeft);
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.rectTransform.anchorMin = tooltipText.rectTransform.anchorMax = new Vector2(0f, 1f);
            tooltipText.rectTransform.pivot = new Vector2(0f, 1f);
            tooltipText.rectTransform.anchoredPosition = new Vector2(12f, -10f);
            tooltipText.rectTransform.sizeDelta = new Vector2(TooltipWidth - 24f, 0f);
            tooltip.gameObject.SetActive(false);
        }

        private void HideTooltip()
        {
            if (tooltip != null && tooltip.gameObject.activeSelf)
                tooltip.gameObject.SetActive(false);
        }

        // The square (or bind-list skill) under the mouse: above the square, beside the list.
        private void UpdateTooltip()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                HideTooltip();
                return;
            }
            Vector2 at = mouse.position.ReadValue();
            string text = null;
            RectTransform over = null;
            bool beside = false;

            if (pickerSlot >= 0 && RectTransformUtility.RectangleContainsScreenPoint(picker, at, null))
            {
                for (int k = 0; k < pickerGrants.Count && k < pickerRows.Count; k++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)pickerRows[k].transform, at, null))
                        continue;
                    var grant = pickerGrants[k];
                    text = SkillTooltip(SkillBook.Get(grant.Id), skills.GrantLevelWithBonuses(grant),
                        skills.ManaCostForGrant(grant), skills.CooldownForGrant(grant), grant.Item.Name, false);
                    over = picker;
                    beside = true;
                    break;
                }
            }
            else
            {
                for (int k = 0; k < slotViews.Count && text == null; k++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(slotViews[k].Back.rectTransform, at, null))
                    {
                        text = SlotTooltip(k);
                        over = slotViews[k].Back.rectTransform;
                    }
                if (text == null && RectTransformUtility.RectangleContainsScreenPoint(attackView.Back.rectTransform, at, null))
                {
                    text = AttackTooltip();
                    over = attackView.Back.rectTransform;
                }
            }

            if (text == null)
            {
                HideTooltip();
                return;
            }
            tooltipText.text = text;
            float height = tooltipText.preferredHeight + 20f;
            tooltip.sizeDelta = new Vector2(TooltipWidth, height);
            PlaceTooltip(over, beside, height);
            if (!tooltip.gameObject.activeSelf)
                tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
        }

        private void PlaceTooltip(RectTransform over, bool beside, float height)
        {
            // The canvas is screen-space overlay: world corners are screen pixels.
            var corners = new Vector3[4];
            over.GetWorldCorners(corners);
            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 min = corners[0] / scale;
            Vector2 max = corners[2] / scale;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            Vector2 position;
            if (beside)
            {
                position = new Vector2(max.x + 8f, min.y);
                if (position.x + TooltipWidth > screenW - 8f)
                    position.x = min.x - 8f - TooltipWidth;
            }
            else
                position = new Vector2((min.x + max.x - TooltipWidth) * 0.5f, max.y + 26f);
            position.x = Mathf.Clamp(position.x, 8f, Mathf.Max(8f, screenW - TooltipWidth - 8f));
            position.y = Mathf.Clamp(position.y, 8f, Mathf.Max(8f, screenH - height - 8f));
            tooltip.anchoredPosition = position;
        }

        private string SlotTooltip(int slot)
        {
            SkillId? id = skills.Slot(slot);
            if (id == null)
            {
                int potion = Player.PlayerPotions.PotionAt(slot);
                if (potion == 0) return null;
                var inventory = skills.GetComponent<PlayerInventory>();
                int count = inventory != null ? inventory.Potions(potion == 1) : 0;
                return "<size=20><b>" + (potion == 1 ? "Health Potion" : "Mana Potion") + "</b></size>\n" +
                       count + " carried\n<color=#" + UiKit.Hex(UiKit.DimText) + ">Click to change this button.</color>";
            }
            int level = skills.LevelAt(slot);
            string source = null;
            int sourceSlot = skills.SourceSlotAt(slot);
            var inv = skills.GetComponent<PlayerInventory>();
            if (level > 0 && sourceSlot >= 0 && inv != null)
                source = inv.Equipment.GetActive((EquipSlot)sourceSlot)?.Name;
            return SkillTooltip(SkillBook.Get(id.Value), level, skills.ManaCostAt(slot), skills.CooldownAt(slot), source, false);
        }

        private string AttackTooltip()
        {
            SkillId? main = skills.MainSkill;
            if (main != null)
                return SkillTooltip(SkillBook.Get(main.Value), skills.Level(main.Value), skills.ManaCost(main.Value),
                    skills.Cooldown(main.Value), null, true);
            SkillId? bow = skills.ActiveBowSkill;
            if (bow != null)
            {
                SkillDefinition skill = SkillBook.Get(bow.Value);
                return SkillTooltip(skill, skills.ActiveBowLevel, skill.ManaCostAt(Mathf.Max(1, skills.ActiveBowLevel)), 0f, null, true);
            }
            ItemData weapon = skills.GetComponent<PlayerInventory>()?.Equipment.GetActive(EquipSlot.MainHand);
            return "<size=20><b>" + (weapon != null ? weapon.Name : "Unarmed") + "</b></size>\n<color=#" +
                   UiKit.Hex(UiKit.DimText) + ">Basic attack</color>\nDamage: " + skills.BasicAttackSummary() +
                   "\n\n<color=#" + UiKit.Hex(UiKit.DimText) + ">Attack with the left mouse button.</color>";
        }

        private string SkillTooltip(SkillDefinition skill, int level, float cost, float cooldown, string source, bool attack)
        {
            string gold = UiKit.Hex(UiKit.Gold);
            string dim = UiKit.Hex(UiKit.DimText);
            var text = new System.Text.StringBuilder();
            text.Append("<size=20><b>").Append(skill.Name).Append("</b></size>\n");
            if (level > 0)
                text.Append("<color=#").Append(gold).Append(">Level ").Append(level).Append("</color>  ");
            text.Append("<color=#").Append(dim).Append('>').Append(Tags(skill)).Append("</color>\n");
            if (level <= 0)
                text.Append("<color=#ff7777>No worn gear grants this skill.</color>\n");
            else
            {
                string damage = skills.DamageSummary(skill.Id, level);
                if (damage != null)
                    text.Append(skill.Summon ? "Minions: " : "Damage: ").Append(damage).Append('\n');
                if (cost > 0f)
                    text.Append("Cost: ").Append(Num(cost)).Append(' ').Append(skills.CostResource)
                        .Append(skill.Bow ? " per shot" : "").Append('\n');
                if (cooldown > 0f)
                    text.Append(attack ? "Cast time: " : "Cooldown: ").Append(Num(cooldown)).Append("s\n");
                if (!string.IsNullOrEmpty(source))
                    text.Append("<color=#").Append(dim).Append(">From ").Append(source).Append("</color>\n");
            }
            text.Append('\n').Append(skill.Description);
            if (skill.Bow && !attack)
                text.Append("\n<color=#").Append(dim).Append(">Press to toggle; while on, it replaces your bow attack.</color>");
            return text.ToString();
        }

        private static string Tags(SkillDefinition skill)
        {
            string tags = skill.Bow ? "Attack, Bow" : skill.WeaponMultiplierAt(1) > 0f ? "Attack, Melee" :
                skill.Summon ? "Spell, Summon" : skill.Spell ? "Spell" : "Movement";
            switch (skill.DamageElement)
            {
                case StatType.FireDamage: return tags + ", Fire";
                case StatType.ColdDamage: return tags + ", Cold";
                case StatType.LightningDamage: return tags + ", Lightning";
                case StatType.PoisonDamage: return tags + ", Poison";
                default: return tags;
            }
        }

        private static string Num(float value)
        {
            return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ building

        // One square on the bar: name, the no-mana tint, the cooldown sweep and its key above.
        private SlotView NewSlotView(RectTransform bar, string name, float x, float y, string keyLabel, System.Action onClick)
        {
            Image back = UiKit.NewImage(name, bar, Color.black);
            UiKit.Inset(back);
            back.raycastTarget = onClick != null;
            if (onClick != null)
                back.gameObject.AddComponent<TouchPointerRelay>().Up += _ => onClick();
            UiKit.TopLeft(back.rectTransform, new Vector2(x, y), new Vector2(SlotSize, SlotSize));
            UiKit.AddOutline(back, onClick != null ? UiKit.BorderColor : UiKit.Gold, 2f);

            Text label = UiKit.NewText("Name", back.rectTransform, "", 19, UiKit.TextColor, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            UiKit.Stretch(label.rectTransform, 0f);
            Shadow labelShadow = label.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = Color.black;
            labelShadow.effectDistance = new Vector2(1f, -1f);

            Image icon = UiKit.NewImage("SkillIcon", back.rectTransform, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UiKit.Stretch(icon.rectTransform, 9f);
            icon.enabled = false;
            label.transform.SetAsLastSibling();
            label.alignment = TextAnchor.LowerRight;

            Image noMana = UiKit.NewImage("NoMana", back.rectTransform, new Color(0.1f, 0.2f, 0.7f, 0.45f));
            UiKit.Stretch(noMana.rectTransform, 0f);

            Image cooldown = UiKit.NewImage("Cooldown", back.rectTransform, new Color(0f, 0f, 0f, 0.65f));
            cooldown.sprite = UiKit.Square;
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Vertical;
            cooldown.fillOrigin = (int)Image.OriginVertical.Top;
            UiKit.Stretch(cooldown.rectTransform, 0f);

            Text key = UiKit.NewText("Key", bar, keyLabel, keyLabel.Length > 1 ? 11 : 14, UiKit.Gold, TextAnchor.MiddleCenter);
            UiKit.TopLeft(key.rectTransform, new Vector2(x, y + 20f), new Vector2(SlotSize, 18f));
            key.raycastTarget = false;

            Image ring = NewRing(back.rectTransform, -3f);

            return new SlotView { Back = back, Icon = icon, Cooldown = cooldown, NoMana = noMana, Name = label, Ring = ring };
        }

        private void Build()
        {
            healthPotionIcon = Resources.Load<Sprite>("ItemIcons/" + ItemGenerator.HealthPotionId);
            manaPotionIcon = Resources.Load<Sprite>("ItemIcons/" + ItemGenerator.ManaPotionId);
            canvas = UiKit.NewCanvas("SkillCanvas", transform, 60, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            // The bar: bottom centre.
            RectTransform bar = UiKit.NewRect("Bar", canvas.transform);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 18f);
            bar.sizeDelta = new Vector2(SlotX(7), BarHeight);
            barRoot = bar.gameObject;
            barRect = bar;

            attackView = NewSlotView(bar, "Attack", 4f, BottomRowY, "LMB", null);
            attackIcon = UiKit.NewImage("Icon", attackView.Back.rectTransform, Color.white);
            attackIcon.preserveAspect = true;
            attackIcon.raycastTarget = false;
            UiKit.Stretch(attackIcon.rectTransform, 8f);
            attackIcon.transform.SetSiblingIndex(0);
            attackIcon.enabled = false;

            for (int k = 0; k < SkillBook.SlotCount; k++)
            {
                int clicked = k;
                float slotY = k >= 4 && k < 8 ? TopRowY : BottomRowY;
                slotViews.Add(NewSlotView(bar, "Slot" + k, SlotX(k), slotY, PlayerSkills.KeyLabel(k), () => OpenPicker(clicked)));
            }

            BuildPicker(canvas.transform);
            BuildTooltip(canvas.transform);

            // The mobile menu is populated only on its first open.
            panelRoot = UiKit.NewRect("MobileSkillsPanel", canvas.transform).gameObject;
            panelRoot.SetActive(false);
            barRoot.SetActive(!TouchMode.Active);
        }

        private void EnsureMobilePanel()
        {
            if (panelContent != null) return;
            var parent = (RectTransform)panelRoot.transform.parent;
            Canvas.ForceUpdateCanvases();
            panelWidth = Mathf.Min(SkillsPanelWidth, Mathf.Max(320f, parent.rect.width - 32f));
            float height = Mathf.Min(850f, parent.rect.height - 32f);
            var pr = (RectTransform)panelRoot.transform;
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(panelWidth, height);
            var panel = panelRoot.AddComponent<Image>();
            panel.color = UiKit.PanelColor;
            panel.raycastTarget = true;
            UiKit.Grain(panel);
            UiKit.Frame(pr);
            pr.gameObject.AddComponent<UiAppear>().offset = Vector2.zero;

            Text title = UiKit.Heading(UiKit.NewText("Title", pr, "SKILL BUTTONS", 26, UiKit.Gold, TextAnchor.MiddleLeft));
            UiKit.TopLeft(title.rectTransform, new Vector2(18f, -12f), new Vector2(panelWidth - 90f, 40f));
            Image close = UiKit.NewImage("Close", pr, Color.white);
            close.raycastTarget = true;
            UiKit.TopLeft(close.rectTransform, new Vector2(panelWidth - 62f, -12f), new Vector2(48f, 48f));
            Text closeText = UiKit.NewText("Label", close.rectTransform, "X", 22, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(closeText.rectTransform, 0f);
            UiKit.StyleButton(close, closeText, UiKit.DangerTint);
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                if (SpectatorMirror.Active) SpectatorMirror.Close(SpectatorMirror.Menu.Skills);
                else SetOpen(false);
            };

            float cardWidth = (panelWidth - 44f) * 0.5f;
            for (int k = 0; k < SkillBook.TouchSlotCount; k++)
            {
                int slot = k;
                Image button = UiKit.NewImage("Button" + (k + 1), pr, Color.black);
                button.raycastTarget = true;
                UiKit.TopLeft(button.rectTransform, new Vector2(16f + (k % 2) * (cardWidth + 12f), -72f - (k / 2) * 70f),
                    new Vector2(cardWidth, 60f));
                UiKit.AddOutline(button, UiKit.BorderColor, 2f);
                Text label = UiKit.NewText("Label", button.rectTransform, "", 18, UiKit.TextColor, TextAnchor.MiddleCenter);
                UiKit.Stretch(label.rectTransform, 5f);
                mobileButtons[k] = button;
                mobileLabels[k] = label;
                button.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
                {
                    selectedMobileButton = slot;
                    RefreshRows();
                };
            }

            selectionHint = UiKit.NewText("SelectedButton", pr, "", 17, UiKit.Gold, TextAnchor.MiddleLeft);
            UiKit.TopLeft(selectionHint.rectTransform, new Vector2(16f, -214f), new Vector2(panelWidth - 154f, 42f));
            Image clear = UiKit.NewImage("ClearButton", pr, new Color(0.25f, 0.10f, 0.08f, 1f));
            clear.raycastTarget = true;
            UiKit.TopLeft(clear.rectTransform, new Vector2(panelWidth - 126f, -212f), new Vector2(110f, 44f));
            Text clearText = UiKit.NewText("Label", clear.rectTransform, "Clear", 18, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(clearText.rectTransform, 0f);
            clear.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                if (skills == null || SpectatorMirror.Active) return;
                skills.ClearSlot(selectedMobileButton);
                RefreshRows();
            };
            clear.gameObject.SetActive(!SpectatorMirror.Active);

            RectTransform viewport = UiKit.NewRect("Viewport", pr);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 16f);
            viewport.offsetMax = new Vector2(-8f, -266f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.15f);
            viewport.gameObject.AddComponent<RectMask2D>();
            panelContent = UiKit.NewRect("Content", viewport);
            panelContent.anchorMin = new Vector2(0f, 1f);
            panelContent.anchorMax = new Vector2(1f, 1f);
            panelContent.pivot = new Vector2(0.5f, 1f);
            panelContent.anchoredPosition = Vector2.zero;
            panelContent.sizeDelta = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = panelContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            emptySkillsText = UiKit.NewText("NoSkills", panelContent,
                "Your equipped gear grants no assignable skills.\nEquip gear with a skill to add it here.",
                18, UiKit.DimText, TextAnchor.MiddleCenter);
            UiKit.TopLeft(emptySkillsText.rectTransform, new Vector2(8f, 0f), new Vector2(panelWidth - 32f, 80f));
        }

        private void BuildRows()
        {
            if (skills == null || panelContent == null) return;
            var grants = skills.AvailableGrants();
            // Stats, cooldowns and bindings update row values, but only a changed list of
            // equipment grants requires destroying and recreating its UI hierarchy.
            if (SameGrants(builtGrants, grants)) return;
            foreach (Row old in rows)
                if (old.Back != null)
                {
                    old.Back.gameObject.SetActive(false);
                    Destroy(old.Back.gameObject);
                }
            rows.Clear();
            builtGrants = grants;
            float y = 0f;
            foreach (var grant in grants)
                AddRow(SkillBook.Get(grant.Id), grant, ref y);
            panelContent.sizeDelta = new Vector2(0f, -y);
        }

        private static bool SameGrants(List<PlayerSkills.SkillGrant> previous, List<PlayerSkills.SkillGrant> current)
        {
            if (previous == null || previous.Count != current.Count) return false;
            for (int k = 0; k < current.Count; k++)
            {
                var a = previous[k];
                var b = current[k];
                if (a.Id != b.Id || a.Source != b.Source || a.GrantLevel != b.GrantLevel ||
                    !ReferenceEquals(a.Item, b.Item)) return false;
            }
            return true;
        }

        private void AddRow(SkillDefinition skill, PlayerSkills.SkillGrant grant, ref float y)
        {
            Image back = UiKit.NewImage("Row_" + skill.Id + "_" + grant.Source, panelContent, Color.black);
            back.raycastTarget = true;
            UiKit.TopLeft(back.rectTransform, new Vector2(8f, y), new Vector2(panelWidth - 32f, 100f));
            Image icon = UiKit.NewImage("Icon", back.rectTransform, skill.Color);
            icon.sprite = SkillIconFactory.Get(skill.Id);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UiKit.TopLeft(icon.rectTransform, new Vector2(8f, -12f), new Vector2(40f, 40f));
            Text text = UiKit.NewText("Details", back.rectTransform, "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.TopLeft(text.rectTransform, new Vector2(56f, -8f), new Vector2(panelWidth - 208f, 84f));
            Image action = UiKit.NewImage("Assign", back.rectTransform, new Color(0.25f, 0.21f, 0.13f, 1f));
            action.raycastTarget = true;
            UiKit.TopLeft(action.rectTransform, new Vector2(panelWidth - 142f, -26f), new Vector2(102f, 48f));
            Text label = UiKit.NewText("Label", action.rectTransform, "Assign", 17, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(label.rectTransform, 0f);
            System.Action assign = () =>
            {
                if (skills == null || SpectatorMirror.Active) return;
                skills.Assign(selectedMobileButton, grant.Id, grant.Source, grant.GrantLevel);
                RefreshRows();
            };
            // Click handlers let a drag reach the ScrollRect without assigning on release.
            UiKit.OnClick(action, assign);
            UiKit.OnClick(back, assign);
            rows.Add(new Row { Id = skill.Id, Grant = grant, Back = back, Title = text, Assignment = label });
            y -= 108f;
        }
    }
}
