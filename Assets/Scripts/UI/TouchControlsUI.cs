using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Player;

namespace PoeClone.UI
{
    /// <summary>
    /// On-screen controls for phone/tablet play, shown only in <see cref="TouchMode"/>: a floating
    /// joystick on the left half of the screen for movement, an aim stick (drag to aim, hold off
    /// centre to keep attacking/casting that way - see <see cref="VirtualInput.Aim"/>) at the
    /// bottom right, and buttons at the top right for menus and settings. Movement always runs.
    /// They feed <see cref="VirtualInput"/>, which the player scripts read
    /// next to the keyboard and mouse. Also asks for landscape when the phone is held upright.
    ///
    /// Spectators only get chat, which <see cref="ChatUI"/> keeps open for them. Installed by
    /// <see cref="GameSessionController"/> like the rest of the session UI; built at runtime.
    /// </summary>
    public class TouchControlsUI : MonoBehaviour
    {
        private const float JoystickRadius = 90f;   // how far the knob travels, in canvas units
        private const float JoystickDeadZone = 0.15f;
        private static readonly Vector2 JoystickIdle = new Vector2(170f, 170f);

        private static readonly Color ControlColor = new Color(0.08f, 0.07f, 0.06f, 0.55f);
        private static readonly Color ControlPressed = new Color(0.30f, 0.24f, 0.14f, 0.75f);
        private static readonly Color UnreadColor = new Color(0.85f, 0.20f, 0.15f, 1f);

        private GameObject controlsRoot;
        private GameObject combatRoot;
        private GameObject menuRoot;
        private GameObject rotateRoot;
        private GameObject inventoryNavigationRoot;

        private RectTransform joystickZone;
        private RectTransform joystickBase;
        private RectTransform joystickKnob;
        private Image joystickBaseImage;
        private Image joystickKnobImage;
        private int joystickPointer = int.MinValue;

        private Image aimBaseImage;
        private RectTransform aimKnob;
        private Image aimIcon;
        private string aimIconFor;
        private int aimPointer = int.MinValue;
        private Image settingsButton;
        private GameObject unreadDot;

        private class SkillButton
        {
            public Image Back;
            public Image Icon;
            public Image Cooldown;
            public Image Ring;   // spins round a bow skill that's toggled on
        }

        private readonly List<SkillButton> skillButtons = new List<SkillButton>();
        private Image healthPotionImage;
        private Image manaPotionImage;
        private Text healthPotionText;
        private Text manaPotionText;
        private Skills.PlayerSkills skills;

        private PlayerStats stats;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;
        private EscapeMenuUI settingsMenu;
        private int seenMessages;
        private bool combatShown;

        private void Awake()
        {
            UiEventSystemBootstrap.EnsureExists();
            Build();
            ResetCombat();
            Refresh();
        }

        private void OnDestroy()
        {
            PlayerHUD.SetHiddenBy(this, false);
        }

        // Polled: what to show depends on the role, death, which panels are open and the screen
        // orientation, none of which raise one shared event.
        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool touch = TouchMode.Active;
            bool portrait = touch && Screen.height > Screen.width;
            rotateRoot.SetActive(portrait);

            var session = GameSessionController.Instance;
            bool spectator = session != null && session.Role == SessionRole.Spectator;
            bool ready = PoeClone.World.MinimalCombatMode.Enabled || SaveSystem.CharacterLoaded;
            bool show = touch && !spectator && !portrait && ready;
            controlsRoot.SetActive(show);

            if (!show)
            {
                inventoryNavigationRoot.SetActive(false);
                SetCombatShown(false);
                PlayerHUD.SetHiddenBy(this, false);
                return;
            }

            FindGameplay();

            bool dead = stats == null || stats.IsDead;
            bool inventoryOpen = inventoryUI != null && inventoryUI.IsOpen;
            inventoryNavigationRoot.SetActive(inventoryOpen && !dead);
            bool characterOpen = characterUI != null && characterUI.IsOpen;
            if (!PoeClone.World.MinimalCombatMode.Enabled && settingsMenu == null)
                settingsMenu = FindAnyObjectByType<EscapeMenuUI>();
            bool menuOpen = inventoryOpen || characterOpen || SkillBarUI.IsOpen || DialogueUI.IsOpen ||
                            PassiveTreeUI.IsOpen || (settingsMenu != null && settingsMenu.IsOpen);

            // Keep the menu buttons behind the active panel in both draw order and hit testing.
            // The panel's own close control remains available to dismiss it.
            menuRoot.SetActive((!PoeClone.World.MinimalCombatMode.Enabled || PoeClone.World.MinimalCombatMode.MenusEnabled) && !dead && !menuOpen);
            if (settingsButton != null)
                settingsButton.gameObject.SetActive(!PoeClone.World.MinimalCombatMode.Enabled && !dead && !menuOpen);

            SetCombatShown(!dead && !menuOpen);
            UpdateSkillButtons();
            UpdatePotionButtons();

            // The character page sits where the HUD is drawn (OnGUI draws over uGUI).
            PlayerHUD.SetHiddenBy(this, characterOpen);

            UpdateAimIcon();

            if (ChatUI.PanelVisible)
                seenMessages = ChatUI.MessageCount;
            unreadDot.SetActive(ChatUI.MessageCount > seenMessages);
        }

        // The equipped weapon's painted icon, or the plain sword silhouette unarmed. A bow shows
        // an arrow, or the icon of the bow skill that's on.
        private void UpdateAimIcon()
        {
            PlayerInventory inventory = stats != null ? stats.GetComponent<PlayerInventory>() : null;
            ItemData weapon = inventory != null ? inventory.Equipment.Get(EquipSlot.MainHand) : null;
            Skills.SkillId? attackSkill = skills != null ? skills.MainSkill ?? skills.ActiveBowSkill : null;
            string id = (weapon != null ? weapon.Id : "") + "|" + attackSkill;
            if (id == aimIconFor)
                return;

            aimIconFor = id;
            aimIcon.enabled = true;
            if (attackSkill != null)
            {
                Skills.SkillDefinition skill = Skills.SkillBook.Get(attackSkill.Value);
                aimIcon.sprite = Skills.SkillIconFactory.Get(skill.Id);
                aimIcon.color = new Color(skill.Color.r * 0.55f, skill.Color.g * 0.55f, skill.Color.b * 0.55f, 1f);
                return;
            }
            if (weapon != null && weapon.WeaponType == WeaponType.Bow)
            {
                aimIcon.sprite = IconFactory.Arrow;
                aimIcon.color = new Color(0.92f, 0.86f, 0.72f, 0.9f);
                return;
            }
            Sprite painted = weapon != null ? ItemArt.PaintedIcon(weapon) : null;
            aimIcon.sprite = painted != null ? painted : IconFactory.Get(ItemType.Weapon);
            aimIcon.color = painted != null ? weapon.ArtTint : new Color(0.92f, 0.86f, 0.72f, 0.9f);
        }

        private void FindGameplay()
        {
            if (stats == null)
                stats = FindAnyObjectByType<PlayerStats>();
            if ((!PoeClone.World.MinimalCombatMode.Enabled || PoeClone.World.MinimalCombatMode.InventoryMenuEnabled) && inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if ((!PoeClone.World.MinimalCombatMode.Enabled || PoeClone.World.MinimalCombatMode.CharacterMenuEnabled) && characterUI == null)
                characterUI = FindAnyObjectByType<CharacterPageUI>();
            if (skills == null)
                skills = FindAnyObjectByType<Skills.PlayerSkills>();
        }

        // Each round skill button shows its icon, the cooldown sweeping round, and a
        // blue tint when there isn't enough mana; an empty slot is a faint ring.
        private void UpdateSkillButtons()
        {
            for (int k = 0; k < skillButtons.Count; k++)
            {
                SkillButton button = skillButtons[k];
                Skills.SkillId? id = skills != null ? skills.Slot(k) : null;
                bool on = id != null && skills.IsToggledOnAt(k);
                button.Ring.enabled = on;
                if (on)
                    SkillBarUI.SpinRing(button.Ring, Skills.SkillBook.Get(id.Value).Color);
                if (id == null)
                {
                    button.Back.color = new Color(0.08f, 0.07f, 0.06f, 0.3f);
                    button.Icon.enabled = false;
                    button.Cooldown.fillAmount = 0f;
                    continue;
                }

                Skills.SkillDefinition skill = Skills.SkillBook.Get(id.Value);
                button.Icon.enabled = true;
                button.Icon.sprite = Skills.SkillIconFactory.Get(skill.Id);
                if (skills.LevelAt(k) <= 0)
                {
                    // Slotted but not on the gear worn right now.
                    button.Back.color = new Color(0.08f, 0.07f, 0.06f, 0.3f);
                    button.Icon.color = new Color(1f, 1f, 1f, 0.25f);
                    button.Cooldown.fillAmount = 0f;
                    continue;
                }
                bool affordable = skills.CanAffordAt(k);
                Color c = affordable ? skill.Color : Color.Lerp(skill.Color, new Color(0.2f, 0.3f, 0.9f), 0.6f);
                button.Back.color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.8f);
                button.Icon.color = Color.white;
                float total = skills.CooldownTotalAt(k);
                button.Cooldown.fillAmount = total > 0f ? skills.CooldownLeftAt(k) / total : 0f;
            }
        }

        private void UpdatePotionButtons()
        {
            PlayerPotions potions = stats != null ? stats.GetComponent<PlayerPotions>() : null;
            int health = potions != null ? potions.HealthPotions : 0;
            int mana = potions != null ? potions.ManaPotions : 0;
            healthPotionText.text = health.ToString();
            manaPotionText.text = mana.ToString();
            healthPotionImage.color = health > 0 ? new Color(0.45f, 0.1f, 0.1f, 0.8f) : ControlColor;
            manaPotionImage.color = mana > 0 ? new Color(0.12f, 0.18f, 0.5f, 0.8f) : ControlColor;
        }

        private void ToggleSkills()
        {
            bool open = !SkillBarUI.IsOpen;
            if (open)
                CloseOthers();
            SkillBarUI.SetOpen(open);
        }

        private void SetCombatShown(bool shown)
        {
            if (shown == combatShown)
                return;

            combatShown = shown;
            combatRoot.SetActive(shown);

            // A finger held on the joystick or attack button never gets its release once hidden.
            if (!shown)
                ResetCombat();
        }

        private void ResetCombat()
        {
            VirtualInput.Clear();
            joystickPointer = int.MinValue;
            joystickBase.anchoredPosition = JoystickIdle;
            joystickKnob.anchoredPosition = Vector2.zero;
            SetJoystickActive(false);

            aimPointer = int.MinValue;
            aimKnob.anchoredPosition = Vector2.zero;
            aimBaseImage.color = ControlColor;
        }

        // ------------------------------------------------------------------ joystick

        // The stick appears under the thumb wherever it lands in the zone, so there is no fixed
        // spot to find by feel; at rest it waits, faded, in the corner as a hint.
        private void OnJoystickDown(PointerEventData e)
        {
            if (joystickPointer != int.MinValue)
                return;

            // A tap on an item lying in the joystick area picks it up (LootPicker) instead.
            if (LootPicker.PickableAt(e.position) != null || NpcInteractor.TalkableAt(e.position) != null)
                return;

            joystickPointer = e.pointerId;
            float edge = joystickBase.sizeDelta.x * 0.5f + 10f;
            Vector2 local = LocalPoint(e.position);
            joystickBase.anchoredPosition = new Vector2(Mathf.Max(local.x, edge), Mathf.Max(local.y, edge));
            SetJoystickActive(true);
            OnJoystickDrag(e);
        }

        private void OnJoystickDrag(PointerEventData e)
        {
            if (e.pointerId != joystickPointer)
                return;

            Vector2 offset = Vector2.ClampMagnitude(LocalPoint(e.position) - joystickBase.anchoredPosition, JoystickRadius);
            joystickKnob.anchoredPosition = offset;

            Vector2 move = offset / JoystickRadius;
            VirtualInput.Move = move.magnitude < JoystickDeadZone ? Vector2.zero : move;
        }

        private void OnJoystickUp(PointerEventData e)
        {
            if (e.pointerId != joystickPointer)
                return;

            joystickPointer = int.MinValue;
            VirtualInput.Move = Vector2.zero;
            joystickBase.anchoredPosition = JoystickIdle;
            joystickKnob.anchoredPosition = Vector2.zero;
            SetJoystickActive(false);
        }

        private Vector2 LocalPoint(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickZone, screenPos, null, out Vector2 local);
            return local;
        }

        private void SetJoystickActive(bool active)
        {
            joystickBaseImage.color = new Color(1f, 1f, 1f, active ? 0.45f : 0.22f);
            joystickKnobImage.color = new Color(1f, 1f, 1f, active ? 0.75f : 0.35f);
        }

        // ------------------------------------------------------------------ aim stick

        // Fixed in place (unlike the movement joystick, which appears under the thumb) since it
        // shares the attack button's old spot and shouldn't wander into the skill buttons beside
        // it. Dragging past its edge still works: once a finger is down on it, the event system
        // keeps sending drag events to it no matter where the finger goes.
        private void OnAimDown(PointerEventData e)
        {
            if (aimPointer != int.MinValue)
                return;

            aimPointer = e.pointerId;
            aimBaseImage.color = ControlPressed;
            OnAimDrag(e);
        }

        private void OnAimDrag(PointerEventData e)
        {
            if (e.pointerId != aimPointer)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(aimBaseImage.rectTransform, e.position, null, out Vector2 local);
            Vector2 offset = Vector2.ClampMagnitude(local, JoystickRadius);
            aimKnob.anchoredPosition = offset;

            Vector2 direction = offset / JoystickRadius;
            bool held = direction.magnitude >= JoystickDeadZone;
            VirtualInput.Aim = held ? direction : Vector2.zero;
            VirtualInput.AttackHeld = held;
        }

        private void OnAimUp(PointerEventData e)
        {
            if (e.pointerId != aimPointer)
                return;

            aimPointer = int.MinValue;
            VirtualInput.Aim = Vector2.zero;
            VirtualInput.AttackHeld = false;
            aimKnob.anchoredPosition = Vector2.zero;
            aimBaseImage.color = ControlColor;
        }

        // ------------------------------------------------------------------ buttons

        private void ToggleInventory()
        {
            if (inventoryUI == null)
                return;

            bool open = !inventoryUI.IsOpen;
            if (open)
                CloseOthers();
            inventoryUI.SetOpen(open);
        }

        private void ToggleCharacter()
        {
            if (characterUI == null)
                return;

            bool open = !characterUI.IsOpen;
            if (open)
                CloseOthers();
            characterUI.SetOpen(open);
        }

        private void TogglePassives()
        {
            bool open = !PassiveTreeUI.IsOpen;
            if (open)
                CloseOthers();
            PassiveTreeUI.SetOpen(open);
        }

        private void ToggleChat()
        {
            bool open = !ChatUI.PanelVisible;
            if (open)
                CloseOthers();
            ChatUI.SetPanelOpen(open);
        }

        // One panel at a time: on a phone they overlap each other.
        private void CloseOthers()
        {
            if (inventoryUI != null && inventoryUI.IsOpen)
                inventoryUI.SetOpen(false);
            if (characterUI != null && characterUI.IsOpen)
                characterUI.SetOpen(false);
            ChatUI.SetPanelOpen(false);
            SkillBarUI.SetOpen(false);
            PassiveTreeUI.SetOpen(false);
            DialogueUI.Close();
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            Canvas canvas = NewCanvas("TouchControlsCanvas", 700);
            controlsRoot = canvas.gameObject;

            // Combat: joystick zone on the left, aim stick + run at the bottom right.
            RectTransform combat = UiKit.NewRect("Combat", canvas.transform);
            UiKit.Stretch(combat, 0f);
            combatRoot = combat.gameObject;

            Image zone = UiKit.NewImage("JoystickZone", combat, Color.clear);
            zone.raycastTarget = true;
            joystickZone = zone.rectTransform;
            joystickZone.anchorMin = Vector2.zero;
            joystickZone.anchorMax = new Vector2(0.45f, 0.75f);
            joystickZone.pivot = Vector2.zero;
            joystickZone.offsetMin = Vector2.zero;
            joystickZone.offsetMax = Vector2.zero;

            joystickBaseImage = UiKit.NewImage("Base", joystickZone, Color.white);
            joystickBaseImage.sprite = UiKit.Ring;
            joystickBase = joystickBaseImage.rectTransform;
            Place(joystickBase, Vector2.zero, JoystickIdle, new Vector2(220f, 220f));

            joystickKnobImage = UiKit.NewImage("Knob", joystickBase, Color.white);
            joystickKnobImage.sprite = UiKit.Disc;
            joystickKnob = joystickKnobImage.rectTransform;
            Place(joystickKnob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));

            // Starts hidden; Refresh shows it once there is a live player to control.
            combatRoot.SetActive(false);

            TouchPointerRelay stick = zone.gameObject.AddComponent<TouchPointerRelay>();
            stick.Down += OnJoystickDown;
            stick.Dragged += OnJoystickDrag;
            stick.Up += OnJoystickUp;

            // Aim stick: fixed in the attack button's old spot. Dragging it off centre both aims
            // and attacks/casts that way, repeatedly while held - there's no auto-aim any more, so
            // this is the only way to hit anything on touch.
            aimBaseImage = NewRoundButton("Aim", combat, new Vector2(1f, 0f), new Vector2(-150f, 150f), 180f, null);

            Image aimKnobImage = UiKit.NewImage("Knob", aimBaseImage.rectTransform, Color.white);
            aimKnobImage.sprite = UiKit.Disc;
            aimKnob = aimKnobImage.rectTransform;
            Place(aimKnob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 104f));

            aimIcon = UiKit.NewImage("Icon", aimKnob, new Color(0.92f, 0.86f, 0.72f, 0.9f));
            aimIcon.sprite = IconFactory.Get(ItemType.Weapon);
            aimIcon.preserveAspect = true;
            UiKit.Stretch(aimIcon.rectTransform, 16f);

            TouchPointerRelay aim = aimBaseImage.gameObject.AddComponent<TouchPointerRelay>();
            aim.Down += OnAimDown;
            aim.Dragged += OnAimDrag;
            aim.Up += OnAimUp;

            // Skill buttons on an arc around the attack button, in thumb's reach.
            for (int k = 0; k < Skills.SkillBook.TouchSlotCount; k++)
            {
                float angle = (105f + k * 25f) * Mathf.Deg2Rad;
                Vector2 at = new Vector2(-150f + Mathf.Cos(angle) * 185f, 150f + Mathf.Sin(angle) * 185f);
                Image back = NewRoundButton("Skill" + k, combat, new Vector2(1f, 0f), at, 88f, null);

                Image icon = UiKit.NewImage("SkillIcon", back.rectTransform, Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiKit.Stretch(icon.rectTransform, 20f);
                icon.enabled = false;

                Image cooldown = UiKit.NewImage("Cooldown", back.rectTransform, new Color(0f, 0f, 0f, 0.6f));
                cooldown.sprite = UiKit.Disc;
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = (int)Image.Origin360.Top;
                cooldown.fillClockwise = false;
                UiKit.Stretch(cooldown.rectTransform, 0f);

                int slot = k;
                back.gameObject.AddComponent<TouchPointerRelay>().Down += _ => VirtualInput.SkillPressed = slot;
                TouchMode.AddBlocker(back.rectTransform);
                Image ring = SkillBarUI.NewRing(back.rectTransform, 6f);
                Text number = UiKit.NewText("ButtonNumber", back.rectTransform, (k + 1).ToString(),
                    16, UiKit.Gold, TextAnchor.LowerRight);
                number.raycastTarget = false;
                UiKit.Stretch(number.rectTransform, 10f);
                skillButtons.Add(new SkillButton { Back = back, Icon = icon, Cooldown = cooldown, Ring = ring });
            }

            // Larger potion targets, with life nearest the attack stick.
            healthPotionImage = NewRoundButton("HealthPotion", combat, new Vector2(1f, 0f), new Vector2(-295f, 62f), 104f, null);
            AddPotionIcon(healthPotionImage, ItemGenerator.HealthPotionId);
            healthPotionText = AddPotionCount(healthPotionImage, new Color(1f, 0.6f, 0.55f));
            healthPotionImage.gameObject.AddComponent<TouchPointerRelay>().Down += _ => VirtualInput.PotionPresses |= 1;
            TouchMode.AddBlocker(healthPotionImage.rectTransform);

            manaPotionImage = NewRoundButton("ManaPotion", combat, new Vector2(1f, 0f), new Vector2(-415f, 62f), 104f, null);
            AddPotionIcon(manaPotionImage, ItemGenerator.ManaPotionId);
            manaPotionText = AddPotionCount(manaPotionImage, new Color(0.65f, 0.72f, 1f));
            manaPotionImage.gameObject.AddComponent<TouchPointerRelay>().Down += _ => VirtualInput.PotionPresses |= 2;
            TouchMode.AddBlocker(manaPotionImage.rectTransform);

            Canvas navigationCanvas = NewCanvas("InventoryNavigationCanvas", 805);
            inventoryNavigationRoot = navigationCanvas.gameObject;
            Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int k = 0; k < directions.Length; k++)
            {
                Vector2 direction = directions[k];
                Image button = NewRoundButton("InventoryDirection" + k, navigationCanvas.transform, Vector2.zero,
                    JoystickIdle + direction * 85f, 78f, null);
                AddDirectionArrow(button, direction);
                button.gameObject.AddComponent<TouchPointerRelay>().Down += _ => inventoryUI?.Navigate(direction);
                TouchMode.AddBlocker(button.rectTransform);
            }
            inventoryNavigationRoot.SetActive(false);

            // Menus stay at the edges, clear of the combat buttons and the left HUD.
            RectTransform menu = UiKit.NewRect("Menu", canvas.transform);
            UiKit.Stretch(menu, 0f);
            menuRoot = menu.gameObject;

            Image bag = NewRoundButton("Bag", menu, Vector2.one, new Vector2(-70f, -70f), 96f, "BAG");
            bag.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleInventory();

            Image character = NewRoundButton("Character", menu, Vector2.one, new Vector2(-180f, -70f), 96f, "CHAR");
            character.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleCharacter();

            Image skillsButton = NewRoundButton("Skills", menu, Vector2.one, new Vector2(-70f, -180f), 96f, "SKL");
            skillsButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleSkills();
            TouchMode.AddBlocker(skillsButton.rectTransform);

            Image chat = NewRoundButton("Chat", menu, Vector2.one, new Vector2(-180f, -180f), 96f, "CHAT");
            chat.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleChat();

            Image dot = UiKit.NewImage("Unread", chat.rectTransform, UnreadColor);
            dot.sprite = UiKit.Disc;
            Place(dot.rectTransform, new Vector2(0.85f, 0.85f), Vector2.zero, new Vector2(24f, 24f));
            unreadDot = dot.gameObject;

            // The inventory and item pickup read raw touches, so they must know these buttons sit on top.
            TouchMode.AddBlocker(aimBaseImage.rectTransform);
            TouchMode.AddBlocker(bag.rectTransform);
            TouchMode.AddBlocker(character.rectTransform);
            TouchMode.AddBlocker(chat.rectTransform);

            Image tree = NewRoundButton("Tree", menu, new Vector2(0f, 1f), new Vector2(350f, -58f), 96f, "TREE");
            tree.gameObject.AddComponent<TouchPointerRelay>().Up += _ => TogglePassives();
            TouchMode.AddBlocker(tree.rectTransform);

            Image town = NewRoundButton("Town", menu, new Vector2(0f, 1f), new Vector2(460f, -58f), 96f, "TOWN");
            town.gameObject.AddComponent<TouchPointerRelay>().Up += _ => TownPortal.Pressed = true;
            TouchMode.AddBlocker(town.rectTransform);

            if (PoeClone.World.MinimalCombatMode.Enabled)
            {
                chat.gameObject.SetActive(false);
                town.gameObject.SetActive(false);
                bag.gameObject.SetActive(PoeClone.World.MinimalCombatMode.InventoryMenuEnabled);
                character.gameObject.SetActive(PoeClone.World.MinimalCombatMode.CharacterMenuEnabled);
                skillsButton.gameObject.SetActive(PoeClone.World.MinimalCombatMode.SkillMenuEnabled);
                tree.gameObject.SetActive(PoeClone.World.MinimalCombatMode.PassiveMenuEnabled);
            }

            settingsButton = NewRoundButton("Settings", canvas.transform, new Vector2(0f, 1f), new Vector2(570f, -58f), 96f, "SETTINGS");
            settingsButton.GetComponentInChildren<Text>().fontSize = 16;
            settingsButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                CloseOthers();
                if (settingsMenu == null)
                    settingsMenu = FindAnyObjectByType<EscapeMenuUI>();
                settingsMenu?.OpenSettings();
            };
            TouchMode.AddBlocker(settingsButton.rectTransform);

            // Portrait warning, above everything (name prompt included), swallowing touches.
            Canvas rotateCanvas = NewCanvas("RotateDeviceCanvas", 1000);
            rotateRoot = rotateCanvas.gameObject;
            Image blocker = UiKit.NewImage("Background", rotateCanvas.transform, new Color(0f, 0f, 0f, 0.96f));
            blocker.raycastTarget = true;
            UiKit.Stretch(blocker.rectTransform, 0f);
            Text rotateText = UiKit.NewText("Text", blocker.rectTransform, "Turn your device sideways\n<size=26>The game is played in landscape</size>",
                38, UiKit.TextColor, TextAnchor.MiddleCenter);
            rotateText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Stretch(rotateText.rectTransform, 30f);
        }

        // Its own scaler rather than TouchAwareScaler: these canvases only ever show in touch mode.
        private Canvas NewCanvas(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, TouchMode.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void AddPotionIcon(Image button, string potionId)
        {
            Image icon = UiKit.NewImage("PotionIcon", button.rectTransform, Color.white);
            icon.sprite = Resources.Load<Sprite>("ItemIcons/" + potionId);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UiKit.Stretch(icon.rectTransform, 13f);
        }

        private static void AddDirectionArrow(Image button, Vector2 direction)
        {
            // Draw with UI geometry so mobile fonts do not need arrow glyphs.
            RectTransform arrow = UiKit.NewRect("Arrow", button.rectTransform);
            Place(arrow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg);

            Image stem = UiKit.NewImage("Stem", arrow, UiKit.TextColor);
            stem.raycastTarget = false;
            Place(stem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(7f, 24f));
            for (int side = -1; side <= 1; side += 2)
            {
                Image head = UiKit.NewImage("Head" + side, arrow, UiKit.TextColor);
                head.raycastTarget = false;
                Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(side * 6f, 6f), new Vector2(7f, 22f));
                head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, side * 45f);
            }
        }

        private static Text AddPotionCount(Image button, Color color)
        {
            Text count = UiKit.NewText("Count", button.rectTransform, "", 18, color, TextAnchor.LowerRight);
            UiKit.Stretch(count.rectTransform, 5f);
            count.raycastTarget = false;
            Shadow shadow = count.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1f, -1f);
            return count;
        }

        private static Image NewRoundButton(string name, Transform parent, Vector2 anchor, Vector2 position, float size, string label)
        {
            Image image = UiKit.NewImage(name, parent, ControlColor);
            image.sprite = UiKit.Disc;
            image.raycastTarget = true;
            Place(image.rectTransform, anchor, position, new Vector2(size, size));

            Image rim = UiKit.NewImage("Rim", image.rectTransform, new Color(UiKit.BorderColor.r, UiKit.BorderColor.g, UiKit.BorderColor.b, 0.8f));
            rim.sprite = UiKit.Ring;
            UiKit.Stretch(rim.rectTransform, 0f);

            if (label != null)
            {
                Text text = UiKit.NewText("Label", image.rectTransform, label, Mathf.RoundToInt(size * 0.24f), UiKit.TextColor, TextAnchor.MiddleCenter);
                UiKit.Stretch(text.rectTransform, 0f);
            }

            return image;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
