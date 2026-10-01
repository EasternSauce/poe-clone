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
    /// joystick on the left half of the screen, an attack button (hold to keep swinging) and a run
    /// toggle at the bottom right, and a column of buttons at the top right for the bag, the
    /// character page and chat. They feed <see cref="VirtualInput"/>, which the player scripts read
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
        private static readonly Color RunOnColor = new Color(0.62f, 0.48f, 0.20f, 0.85f);
        private static readonly Color UnreadColor = new Color(0.85f, 0.20f, 0.15f, 1f);

        private static Sprite discSprite;
        private static Sprite ringSprite;

        private GameObject controlsRoot;
        private GameObject combatRoot;
        private GameObject menuRoot;
        private GameObject rotateRoot;

        private RectTransform joystickZone;
        private RectTransform joystickBase;
        private RectTransform joystickKnob;
        private Image joystickBaseImage;
        private Image joystickKnobImage;
        private int joystickPointer = int.MinValue;

        private Image attackImage;
        private Image runImage;
        private GameObject unreadDot;

        private PlayerStats stats;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;
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
            bool show = touch && !spectator && !portrait;
            controlsRoot.SetActive(show);

            if (!show)
            {
                SetCombatShown(false);
                PlayerHUD.SetHiddenBy(this, false);
                return;
            }

            FindGameplay();

            bool dead = stats == null || stats.IsDead;
            bool inventoryOpen = inventoryUI != null && inventoryUI.IsOpen;
            bool characterOpen = characterUI != null && characterUI.IsOpen;

            menuRoot.SetActive(!dead);
            SetCombatShown(!dead && !inventoryOpen && !characterOpen);

            // The character page sits where the HUD is drawn (OnGUI draws over uGUI).
            PlayerHUD.SetHiddenBy(this, characterOpen);

            if (ChatUI.PanelVisible)
                seenMessages = ChatUI.MessageCount;
            unreadDot.SetActive(ChatUI.MessageCount > seenMessages);
        }

        private void FindGameplay()
        {
            if (stats == null)
                stats = FindAnyObjectByType<PlayerStats>();
            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if (characterUI == null)
                characterUI = FindAnyObjectByType<CharacterPageUI>();
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
            attackImage.color = ControlColor;
        }

        // ------------------------------------------------------------------ joystick

        // The stick appears under the thumb wherever it lands in the zone, so there is no fixed
        // spot to find by feel; at rest it waits, faded, in the corner as a hint.
        private void OnJoystickDown(PointerEventData e)
        {
            if (joystickPointer != int.MinValue)
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
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            EnsureSprites();

            Canvas canvas = NewCanvas("TouchControlsCanvas", 700);
            controlsRoot = canvas.gameObject;

            // Combat: joystick zone on the left, attack + run at the bottom right.
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
            joystickBaseImage.sprite = ringSprite;
            joystickBase = joystickBaseImage.rectTransform;
            Place(joystickBase, Vector2.zero, JoystickIdle, new Vector2(220f, 220f));

            joystickKnobImage = UiKit.NewImage("Knob", joystickBase, Color.white);
            joystickKnobImage.sprite = discSprite;
            joystickKnob = joystickKnobImage.rectTransform;
            Place(joystickKnob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));

            // Starts hidden; Refresh shows it once there is a live player to control.
            combatRoot.SetActive(false);

            TouchPointerRelay stick = zone.gameObject.AddComponent<TouchPointerRelay>();
            stick.Down += OnJoystickDown;
            stick.Dragged += OnJoystickDrag;
            stick.Up += OnJoystickUp;

            attackImage = NewRoundButton("Attack", combat, new Vector2(1f, 0f), new Vector2(-150f, 150f), 180f, null);
            Image swordIcon = UiKit.NewImage("Icon", attackImage.rectTransform, new Color(0.92f, 0.86f, 0.72f, 0.9f));
            swordIcon.sprite = IconFactory.Get(ItemType.Weapon);
            swordIcon.preserveAspect = true;
            Place(swordIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 104f));

            TouchPointerRelay attack = attackImage.gameObject.AddComponent<TouchPointerRelay>();
            attack.Down += _ =>
            {
                VirtualInput.AttackHeld = true;
                attackImage.color = ControlPressed;
            };
            attack.Up += _ =>
            {
                VirtualInput.AttackHeld = false;
                attackImage.color = ControlColor;
            };

            runImage = NewRoundButton("Run", combat, new Vector2(1f, 0f), new Vector2(-325f, 92f), 104f, "RUN");
            runImage.gameObject.AddComponent<TouchPointerRelay>().Down += _ =>
            {
                VirtualInput.Sprint = !VirtualInput.Sprint;
                runImage.color = VirtualInput.Sprint ? RunOnColor : ControlColor;
            };

            // Menu: a column at the top right. The inventory panel shifts left to clear it.
            RectTransform menu = UiKit.NewRect("Menu", canvas.transform);
            UiKit.Stretch(menu, 0f);
            menuRoot = menu.gameObject;

            Image bag = NewRoundButton("Bag", menu, Vector2.one, new Vector2(-70f, -70f), 96f, "BAG");
            bag.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleInventory();

            Image character = NewRoundButton("Character", menu, Vector2.one, new Vector2(-70f, -180f), 96f, "CHAR");
            character.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleCharacter();

            Image chat = NewRoundButton("Chat", menu, Vector2.one, new Vector2(-70f, -290f), 96f, "CHAT");
            chat.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ToggleChat();

            Image dot = UiKit.NewImage("Unread", chat.rectTransform, UnreadColor);
            dot.sprite = discSprite;
            Place(dot.rectTransform, new Vector2(0.85f, 0.85f), Vector2.zero, new Vector2(24f, 24f));
            unreadDot = dot.gameObject;

            // The inventory reads raw touches, so it must know these buttons sit on top of it.
            TouchMode.AddBlocker(bag.rectTransform);
            TouchMode.AddBlocker(character.rectTransform);
            TouchMode.AddBlocker(chat.rectTransform);

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

        private static Image NewRoundButton(string name, Transform parent, Vector2 anchor, Vector2 position, float size, string label)
        {
            Image image = UiKit.NewImage(name, parent, ControlColor);
            image.sprite = discSprite;
            image.raycastTarget = true;
            Place(image.rectTransform, anchor, position, new Vector2(size, size));

            Image rim = UiKit.NewImage("Rim", image.rectTransform, new Color(UiKit.BorderColor.r, UiKit.BorderColor.g, UiKit.BorderColor.b, 0.8f));
            rim.sprite = ringSprite;
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

        private static void EnsureSprites()
        {
            if (discSprite == null)
                discSprite = MakeCircle(128, 0f);
            if (ringSprite == null)
                ringSprite = MakeCircle(128, 0.07f);
        }

        // Antialiased white circle: filled, or a ring whose thickness is a fraction of the size.
        private static Sprite MakeCircle(int size, float ringThickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float radius = size * 0.5f;
            float inner = radius - ringThickness * size;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - d);
                    if (ringThickness > 0f)
                        alpha *= Mathf.Clamp01(d - inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
