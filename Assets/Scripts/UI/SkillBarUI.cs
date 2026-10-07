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
    /// mana) and the skills panel (SKL button on touch), which lists every skill, its
    /// level and the gear it comes from, with a separate entry per granting item and buttons to put it
    /// there. Clicking a square on the desktop bar opens a list of skills and potions above it.
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
        private const float SkillsPanelWidth = 980f;
        private readonly Text[,] potionButtons = new Text[2, SkillBook.SlotCount];

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
            public Text[] SlotLabels;
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

            for (int k = 0; k < slotViews.Count; k++)
            {
                slotViews[k].Back.gameObject.SetActive(true);
                UpdateSlot(slotViews[k], skills.Slot(k), k);
                int potion = Player.PlayerPotions.PotionAt(k);
                if (potion != 0 && skills.Slot(k) == null)
                    UpdatePotionSlot(slotViews[k], potion == 1);
            }
            UpdateAttack();
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
            ItemData weapon = skills.GetComponent<PlayerInventory>()?.Equipment.Get(EquipSlot.MainHand);
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
                attackIcon.color = weapon.ArtTint;
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
                panelRoot.SetActive(open);
                if (open)
                    RefreshRows();
            }
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
                Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.uiItemPlace, 0.4f);
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
            if (skills == null)
                return;
            BuildRows();

            for (int potion = 0; potion < 2; potion++)
                for (int binding = 0; binding < SkillBook.SlotCount; binding++)
                {
                    Text label = potionButtons[potion,binding];
                    if (label == null) continue;
                    bool assigned = Player.PlayerPotions.PotionAt(binding) == potion + 1 && skills.Slot(binding) == null;
                    label.color = assigned ? UiKit.Gold : UiKit.TextColor;
                    label.transform.parent.GetComponent<Image>().color = assigned
                        ? new Color(.45f,.35f,.15f,1f) : new Color(.10f,.09f,.08f,1f);
                }

            SkillId? main = skills.MainSkill;
            foreach (Row row in rows)
            {
                SkillDefinition skill = SkillBook.Get(row.Id);
                bool unlocked = row.Grant.HasValue;
                bool isAttack = main == row.Id && !row.Grant.HasValue;
                int level = unlocked ? skills.GrantLevelWithBonuses(row.Grant.Value) : skills.Level(row.Id);
                string dim = UiKit.Hex(UiKit.DimText);
                row.Back.color = unlocked || isAttack ? new Color(0.14f, 0.12f, 0.10f, 1f) : new Color(0.08f, 0.07f, 0.07f, 1f);

                if (level > 0)
                {
                    ItemData source = unlocked ? row.Grant.Value.Item : skills.Source(row.Id);
                    string from = source != null && !isAttack ? " · from " + source.Name +
                        (unlocked ? " (" + row.Grant.Value.Source + ")" : "") : "";
                    float cooldown = unlocked ? skills.CooldownForGrant(row.Grant.Value) : skills.Cooldown(row.Id);
                    string timing = isAttack ? Num(cooldown) + "s per cast" : Num(cooldown) + "s cooldown";
                    string minions = skills.MinionSummary(row.Id, level);
                    if (minions != null)
                        timing += " · " + minions;
                    string resourceCost = Num(unlocked ? skills.ManaCostForGrant(row.Grant.Value) : skills.ManaCost(row.Id)) + " " + skills.CostResource;
                    string cost = skill.Bow ? "toggle · " + resourceCost + " per shot" : resourceCost + " · " + timing;
                    row.Title.text = "<color=#" + UiKit.Hex(skill.Color) + "><b>" + skill.Name + "</b></color>  <color=#" + UiKit.Hex(UiKit.Gold) + ">Level " + level +
                                     (isAttack ? " · your attack" : "") + "</color>   <size=14><color=#" + dim + ">" +
                                     cost + from + "</color></size>\n<size=14>" + skill.Description + "</size>";
                }
                else
                {
                    row.Title.text = "<color=#" + dim + "><b>" + skill.Name + "</b>   <size=14>not on your gear · rolls on " + skill.RollsOn +
                                     "</size>\n<size=14>" + skill.Description + "</size></color>";
                }

                for (int k = 0; k < row.SlotLabels.Length; k++)
                {
                    bool here = unlocked && skills.MatchesGrant(k, row.Grant.Value);
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
            Canvas canvas = UiKit.NewCanvas("SkillCanvas", transform, 60, out CanvasGroup group);
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

            // The panel: a scrollable list with a row for each equipped grant.
            const float rowHeight = 74f;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Canvas.ForceUpdateCanvases();
            panelWidth = Mathf.Min(SkillsPanelWidth, Mathf.Max(320f, canvasRect.rect.width - 32f));
            float width = panelWidth;
            float rowsHeight = SkillBook.All.Length * (rowHeight + 6f);
            // Taller than the screen once there are many skills: the rows scroll.
            float height = Mathf.Min(1000f, 220f + rowsHeight + 16f, canvasRect.rect.height - 32f);

            Image panel = UiKit.NewImage("SkillsPanel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(panel);
            panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = new Vector2(0f, Mathf.Min(0f, (canvasRect.rect.height - height) * 0.5f - 16f));
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
            // A spectator shuts only their own copy (the player's stays open for the player).
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                if (SpectatorMirror.Active)
                    SpectatorMirror.Close(SpectatorMirror.Menu.Skills);
                else
                    SetOpen(false);
            };

            for (int potion = 0; potion < 2; potion++)
            {
                bool health = potion == 0;
                float top = -78f - potion * 44f;
                Text heading = UiKit.NewText(health ? "HealthBinding" : "ManaBinding",pr,health ? "Health potion" : "Mana potion",17,UiKit.TextColor,TextAnchor.MiddleLeft);
                UiKit.TopLeft(heading.rectTransform,new Vector2(18f,top),new Vector2(170f,36f));
                for (int binding = 0; binding < SkillBook.SlotCount; binding++)
                {
                    int selectedBinding = binding;
                    Image button = UiKit.NewImage("PotionKey"+potion+"_"+binding,pr,Color.black);
                    button.raycastTarget=true;
                    UiKit.TopLeft(button.rectTransform,new Vector2(196f+binding*48f,top),new Vector2(45f,36f));
                    Text label=UiKit.NewText("Key",button.rectTransform,PlayerSkills.KeyLabel(binding),13,UiKit.TextColor,TextAnchor.MiddleCenter);
                    UiKit.Stretch(label.rectTransform,0f);
                    potionButtons[potion,binding]=label;
                    button.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
                    {
                        if (SpectatorMirror.Active) return;
                        if (skills != null) skills.ClearSlot(selectedBinding);
                        Player.PlayerPotions.SetPotionAt(selectedBinding, health ? 1 : 2);
                        RefreshRows();
                    };
                }
            }

            Text hint = UiKit.NewText("BindingHint",pr,"Each key holds one skill or potion. Assigning a key replaces its current use.",14,UiKit.DimText,TextAnchor.MiddleLeft);
            UiKit.TopLeft(hint.rectTransform,new Vector2(18f,-162f),new Vector2(width-36f,20f));

            RectTransform viewport = UiKit.NewRect("Viewport", pr);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear; // lets the wheel and drags reach the scroll view
            UiKit.TopLeft(viewport, new Vector2(0f, -220f), new Vector2(width, height - 220f - 16f));

            RectTransform content = UiKit.NewRect("Content", viewport);
            panelContent = content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, rowsHeight);

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            BuildRows();
        }

        private void BuildRows()
        {
            if (skills == null || panelContent == null) return;
            foreach (Row old in rows)
                if (old.Back != null) Destroy(old.Back.gameObject);
            rows.Clear();
            var grants = skills.AvailableGrants();
            float y = 0f;
            foreach (SkillDefinition skill in SkillBook.All)
            {
                bool found = false;
                foreach (var grant in grants)
                {
                    if (grant.Id != skill.Id) continue;
                    AddRow(skill, grant, ref y);
                    found = true;
                }
                if (!found) AddRow(skill, null, ref y);
            }
            panelContent.sizeDelta = new Vector2(0f, -y);
        }

        private void AddRow(SkillDefinition skill, PlayerSkills.SkillGrant? grant, ref float y)
        {
                float width = panelWidth;
                const float rowHeight = 74f;
                const float slotButton = 38f;
                Image back = UiKit.NewImage("Row_" + skill.Id, panelContent, Color.black);
                UiKit.Grain(back);
                UiKit.TopLeft(back.rectTransform, new Vector2(16f, y), new Vector2(width - 32f, rowHeight));

                Image icon = UiKit.NewImage("SkillIcon", back.rectTransform, grant.HasValue ? skill.Color : UiKit.DimText);
                icon.sprite = SkillIconFactory.Get(skill.Id);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiKit.TopLeft(icon.rectTransform, new Vector2(12f, -19f), new Vector2(36f, 36f));

                Text text = UiKit.NewText("Text", back.rectTransform, "", 18, UiKit.TextColor, TextAnchor.MiddleLeft);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                float buttonsWidth = SkillBook.SlotCount * (slotButton + 4f);
                UiKit.TopLeft(text.rectTransform, new Vector2(56f, 0f), new Vector2(width - 32f - 56f - buttonsWidth - 8f, rowHeight));

                var row = new Row { Id = skill.Id, Grant = grant, Back = back, Title = text, SlotLabels = new Text[SkillBook.SlotCount] };
                for (int k = 0; k < SkillBook.SlotCount; k++)
                {
                    int slot = k;
                    SkillId id = skill.Id;
                    Image button = UiKit.NewImage("Slot" + k, back.rectTransform, Color.black);
                    UiKit.Inset(button);
                    button.raycastTarget = true;
                    UiKit.TopLeft(button.rectTransform, new Vector2(width - 32f - buttonsWidth - 4f + k * (slotButton + 4f), -(rowHeight - slotButton) * 0.5f), new Vector2(slotButton, slotButton));
                    UiKit.AddOutline(button, UiKit.BorderColor, 1.5f);
                    Text label = UiKit.NewText("Key", button.rectTransform, PlayerSkills.KeyLabel(k), k < SkillBook.TouchSlotCount ? 18 : 13, UiKit.TextColor, TextAnchor.MiddleCenter);
                    UiKit.Stretch(label.rectTransform, 0f);
                    row.SlotLabels[k] = label;
                    button.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
                    {
                        if (skills != null && !SpectatorMirror.Active && grant.HasValue)
                        {
                            var chosen = grant.Value;
                            skills.Assign(slot, id, chosen.Source, chosen.GrantLevel);
                        }
                    };
                }

                rows.Add(row);
                y -= rowHeight + 6f;
        }
    }
}
