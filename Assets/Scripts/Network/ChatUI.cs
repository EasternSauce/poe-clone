using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// Minimal chat panel shared by the player and every spectator. Built at runtime like the
    /// rest of this project's UI (see LoadingScreenUI). This is the first genuinely interactive
    /// uGUI in the project, so it also makes sure an EventSystem with the New Input System's UI
    /// module exists (see UiEventSystemBootstrap) - without it, clicks and typed text would
    /// silently do nothing.
    ///
    /// Keys: Enter opens the chat box, Enter sends. The player then gets control back (so WASD moves
    /// again); spectators have nothing else to control, so their box stays focused. Escape leaves it.
    ///
    /// Touch: the player's chat sits behind an on-screen button (TouchControlsUI) at the top right,
    /// since the bottom right is taken by the attack button; spectators keep it open where it is.
    ///
    /// While nobody is typing, the panel's background goes away and each message stays only for a
    /// while, so the chat doesn't sit over the game. Focusing the box (or scrolling the mouse wheel
    /// over it) brings the whole panel back, with the history scrollable. It draws under the
    /// inventory and its tooltips.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        private const int HistoryLimit = 100;
        private const float MessageSeconds = 12f;   // how long a message shows while the chat is idle
        private const float PeekSeconds = 4f;       // how long a scroll over the idle chat opens it
        private static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.06f, 0.65f);

        private InputField inputField;
        private Text logText;
        private RectTransform logViewport;
        private Text fieldText;
        private Text placeholderText;
        private Text sendText;
        private RectTransform panelRect;
        private RectTransform fieldRect;
        private RectTransform sendRect;
        private Image panelImage;
        private Canvas chatCanvas;
        private readonly List<string> history = new List<string>();
        private readonly List<float> historyTimes = new List<float>();
        private int scrollBack;          // lines scrolled up from the newest
        private float peekUntil = -1f;
        private bool shownActive = true;
        private float nextExpiry = float.MaxValue;
        private bool dirty = true;

        private bool touchPanelOpen;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;

        /// <summary>Messages received so far, for the touch chat button's unread dot.</summary>
        public static int MessageCount { get; private set; }

        /// <summary>Whether the panel is showing (always true outside the touch player layout).</summary>
        public static bool PanelVisible => Instance != null && Instance.panelRect != null && Instance.panelRect.gameObject.activeSelf;
        public static bool Enabled => PlayerPrefs.GetInt("PoeClone.ChatEnabled", 0) != 0;
        public static void SetEnabled(bool enabled)
        {
            PlayerPrefs.SetInt("PoeClone.ChatEnabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
            if (Instance != null) Instance.Relayout();
        }

        /// <summary>True while the chat box has keyboard focus, so gameplay input can ignore WASD/click while typing.</summary>
        public static bool IsTyping => Instance != null && Instance.inputField != null && Instance.inputField.isFocused;

        /// <summary>True while there's unsent text in the chat box (arrow keys then belong to it).</summary>
        public static bool HasDraft => Instance != null && Instance.inputField != null && !string.IsNullOrEmpty(Instance.inputField.text);

        private static ChatUI Instance;

        /// <summary>Frame in which some text field already used the Enter press (so it doesn't also open the chat).</summary>
        internal static int EnterHandledFrame = -1;

        private bool keepFocusAfterSubmit;

        private void Awake()
        {
            Instance = this;
            MessageCount = 0;
            UiEventSystemBootstrap.EnsureExists();
            Build();
        }

        private void OnEnable()
        {
            TouchMode.Changed += Relayout;
            var ctrl = GameSessionController.Instance;
            if (ctrl != null)
            {
                ctrl.ChatReceived += HandleChat;
                ctrl.StateChanged += Relayout;
            }
            Relayout();
        }

        private void OnDisable()
        {
            TouchMode.Changed -= Relayout;
            var ctrl = GameSessionController.Instance;
            if (ctrl != null)
            {
                ctrl.ChatReceived -= HandleChat;
                ctrl.StateChanged -= Relayout;
            }
        }

        /// <summary>Touch player layout: shows or hides the panel (the on-screen chat button).</summary>
        public static void SetPanelOpen(bool open)
        {
            if (Instance == null) return;
            Instance.touchPanelOpen = open;
            if (!open && Instance.inputField.isFocused)
                Instance.inputField.DeactivateInputField();
            Instance.Relayout();
        }

        // Desktop: bottom right, always shown. Touch: bigger text; the player's panel moves to the
        // bottom center when opened, clear of the minimap and quest tracker.
        private void Relayout()
        {
            if (panelRect == null) return;

            bool touch = TouchMode.Active;
            bool touchPlayer = touch && !StayInChat;

            panelRect.gameObject.SetActive(Enabled && (!touchPlayer || touchPanelOpen));

            // On a phone the opened chat sits at the bottom center, clear of the minimap and quest
            // tracker. On a computer it sits under the inventory (which shares its corner).
            // A spectator's chat is their main control, so it stays over every menu and overlay
            // (the mirrored skill tree, inventory, touch buttons, patch notes) - only the name
            // prompt (950) and the loading screen draw above it.
            chatCanvas.sortingOrder = SortingOrder;

            Vector2 corner = touchPlayer ? new Vector2(0.5f, 0f) : new Vector2(1f, 0f);
            panelRect.anchorMin = corner;
            panelRect.anchorMax = corner;
            panelRect.pivot = corner;
            panelRect.anchoredPosition = touchPlayer ? new Vector2(0f, 20f) : new Vector2(-20f, 20f);
            panelRect.sizeDelta = touch ? new Vector2(520f, 250f) : new Vector2(460f, 220f);

            int fontSize = touch ? 20 : 16;
            logText.fontSize = fontSize;
            fieldText.fontSize = fontSize;
            placeholderText.fontSize = fontSize;
            sendText.fontSize = fontSize;

            float fieldTop = touch ? 50f : 42f;
            fieldRect.offsetMax = new Vector2(0f, fieldTop);
            sendRect.offsetMax = new Vector2(-10f, fieldTop);
            logViewport.offsetMin = new Vector2(10f, fieldTop + 4f);
        }

        // Typing, a spectator (always in the chat), the touch chat opened, or a scroll just now.
        private bool Active => inputField.isFocused || StayInChat || (TouchMode.Active && touchPanelOpen) || Time.unscaledTime < peekUntil;

        // A spectator's chat is over everything; a phone player's opened chat over the minimap.
        // On a computer it sits under the inventory - until a window covers it (the inventory, the
        // character page, the skill tree, the skills panel): then it comes up over them so the
        // latest messages can still be read (idle, it's only the lines, no panel, no clicks).
        private int SortingOrder
        {
            get
            {
                if (StayInChat) return 945;
                if (TouchMode.Active) return 800;
                if (inventoryUI == null) inventoryUI = FindAnyObjectByType<InventoryUI>();
                if (characterUI == null) characterUI = FindAnyObjectByType<CharacterPageUI>();
                bool covered = (inventoryUI != null && inventoryUI.IsOpen) || (characterUI != null && characterUI.IsOpen) ||
                               PassiveTreeUI.IsOpen || SkillBarUI.IsOpen;
                return covered ? 70 : 30;
            }
        }

        private void Update()
        {
            UpdateScroll();

            if (!Enabled) return;

            int order = SortingOrder;
            if (chatCanvas.sortingOrder != order)
                chatCanvas.sortingOrder = order;

            bool active = Active;
            if (active != shownActive || dirty || Time.unscaledTime >= nextExpiry)
                Redraw(active);

            var keyboard = Keyboard.current;
            if (keyboard == null || UiKit.IsStashNamePromptOpen || Time.frameCount == EnterHandledFrame || Time.frameCount == UiKit.EnterHandledFrame) return;
            if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame) return;

            // Only when nothing else has the UI focus (e.g. not while a menu button is selected).
            // A spectator has nothing else to type into, so Enter always comes back to the chat.
            var es = EventSystem.current;
            if (es == null) return;
            GameObject selected = es.currentSelectedGameObject;
            if (selected != null && !(StayInChat && selected.GetComponent<InputField>() == null)) return;

            inputField.Select();
            inputField.ActivateInputField();
        }

        private void HandleChat(ChatEnvelope msg)
        {
            MessageCount++;
            AppendLine(FormatLine(msg));
        }

        // Readable on the dark panel. Each name keeps one colour (picked from its letters), so
        // who said what is easy to follow; the role tag is gold for players, grey-blue for watchers.
        private static readonly string[] NameColours =
        {
            "F2C14E", "7FD1FF", "9BE37A", "FF9F7A", "D7A2FF",
            "6FE0C8", "FFD27F", "FF8FB8", "A8C7FF", "C9E86B"
        };
        private const string PlayerTagColour = "E0B85A";
        private const string SpectatorTagColour = "8FA3B8";

        private static string FormatLine(ChatEnvelope msg)
        {
            if (msg.Role == "system")
                return $"<color=#8FA3B8>{Escape(msg.Text)}</color>";
            bool player = msg.Role == "player";
            string tag = player ? "[Player]" : "[Watching]";
            string tagColour = player ? PlayerTagColour : SpectatorTagColour;
            return $"<color=#{tagColour}>{tag}</color> <color=#{NameColour(msg.From)}><b>{Escape(msg.From)}</b></color>: {Escape(msg.Text)}";
        }

        private static string NameColour(string name)
        {
            int hash = 0;
            foreach (char c in name ?? string.Empty)
                hash = hash * 31 + c;
            return NameColours[(hash & 0x7fffffff) % NameColours.Length];
        }

        // Names and messages are user text: a zero-width space after every '<' stops any of it
        // being read as a rich-text tag.
        private static string Escape(string text)
        {
            return string.IsNullOrEmpty(text) ? text : text.Replace("<", "<\u200B");
        }

        private void AppendLine(string line)
        {
            history.Add(line);
            historyTimes.Add(Time.unscaledTime);
            if (history.Count > HistoryLimit)
            {
                history.RemoveAt(0);
                historyTimes.RemoveAt(0);
            }
            if (scrollBack > 0)
                scrollBack++; // keep the same lines in view while reading back
            dirty = true;
        }

        // The mouse wheel over the panel scrolls the history (and opens an idle chat for a moment).
        private void UpdateScroll()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || TouchMode.Active || !panelRect.gameObject.activeInHierarchy)
                return;

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f || !RectTransformUtility.RectangleContainsScreenPoint(panelRect, mouse.position.ReadValue(), null))
                return;

            peekUntil = Time.unscaledTime + PeekSeconds;
            scrollBack = Mathf.Clamp(scrollBack + (wheel > 0f ? 1 : -1), 0, Mathf.Max(0, history.Count - 1));
            dirty = true;
        }

        // Fills the log from the newest line shown upwards, as many whole messages as fit. Idle,
        // only recent messages show and the panel itself is see-through (and lets clicks through).
        private void Redraw(bool active)
        {
            shownActive = active;
            dirty = false;
            nextExpiry = float.MaxValue;
            if (!active)
                scrollBack = 0;

            panelImage.color = active ? PanelColor : Color.clear;
            panelImage.raycastTarget = active;

            string footer = active && scrollBack > 0
                ? "<color=#8FA3B8><i>(" + scrollBack + " newer below - scroll down)</i></color>"
                : null;
            var shown = new List<string>();
            if (footer != null)
                shown.Add(footer);

            int newest = history.Count - 1 - scrollBack;
            for (int k = newest; k >= 0; k--)
            {
                if (!active)
                {
                    float expires = historyTimes[k] + MessageSeconds;
                    if (expires <= Time.unscaledTime)
                        break;
                    nextExpiry = Mathf.Min(nextExpiry, expires);
                }

                shown.Insert(0, history[k]);
                logText.text = string.Join("\n", shown);
                // Stop before a message that no longer fits whole, rather than showing half a line.
                if (shown.Count > 1 && logText.preferredHeight > logViewport.rect.height)
                {
                    shown.RemoveAt(0);
                    break;
                }
            }
            logText.text = string.Join("\n", shown);
        }

        private static bool StayInChat => GameSessionController.Instance != null && GameSessionController.Instance.Role == SessionRole.Spectator;

        private void Send()
        {
            string text = inputField.text;
            if (!string.IsNullOrWhiteSpace(text))
                GameSessionController.Instance?.SendChat(text);
            inputField.text = string.Empty;
        }

        // Enter in the box. The field deactivates itself right after this, firing onEndEdit.
        private void HandleSubmit()
        {
            EnterHandledFrame = Time.frameCount;
            Send();
            keepFocusAfterSubmit = StayInChat;
        }

        // Fires whenever the box loses focus: after Enter, Escape, or a click elsewhere.
        private void HandleEndEdit()
        {
            if (keepFocusAfterSubmit)
            {
                // Re-focus next frame ourselves: the UI "submit" for the same Enter press may have
                // reached the field while it was still focused, in which case it did nothing.
                keepFocusAfterSubmit = false;
                inputField.ActivateInputField();
                return;
            }

            // Leave the box entirely so gameplay keys work again (a selected-but-unfocused field
            // still counts as UI focus for movement). Skipped when focus is already moving to
            // another UI object, e.g. the Send button.
            var es = EventSystem.current;
            if (es != null && !es.alreadySelecting && es.currentSelectedGameObject == inputField.gameObject)
                es.SetSelectedGameObject(null);
        }

        private void OnSendClicked()
        {
            Send();
            var es = EventSystem.current;
            if (StayInChat)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
            else if (es != null)
            {
                es.SetSelectedGameObject(null);
            }
        }

        private void Build()
        {
            var canvasGO = new GameObject("ChatCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the inventory (50) and its item tooltips, which share this corner of the screen
            // (see Relayout for phones).
            canvas.sortingOrder = 30;
            chatCanvas = canvas;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<TouchAwareScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelImage = panelGO.AddComponent<Image>();
            panelImage.color = PanelColor;
            UiKit.Grain(panelImage);
            panelRect = panelImage.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.sizeDelta = new Vector2(460f, 220f);
            panelRect.anchoredPosition = new Vector2(-20f, 20f);

            // The log grows upwards from the bottom of a clipping viewport, so when long messages
            // wrap past the top it is the oldest lines that get cut. (Text's own Truncate always
            // drops the last lines, which hid the newest message behind the input box.)
            var viewportGO = new GameObject("LogViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(panelGO.transform, false);
            logViewport = (RectTransform)viewportGO.transform;
            logViewport.anchorMin = new Vector2(0f, 0f);
            logViewport.anchorMax = new Vector2(1f, 1f);
            logViewport.offsetMin = new Vector2(10f, 46f);
            logViewport.offsetMax = new Vector2(-10f, -8f);

            var logGO = new GameObject("Log");
            logGO.transform.SetParent(viewportGO.transform, false);
            logText = logGO.AddComponent<Text>();
            logText.font = UiKit.Font;
            logText.fontSize = 16;
            logText.color = Color.white;
            logText.supportRichText = true; // user text is escaped (see Escape)
            logText.alignment = TextAnchor.LowerLeft;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.raycastTarget = false;
            RuntimeUiUtil.StretchFull(logText.rectTransform);

            var fieldGO = new GameObject("Input");
            fieldGO.transform.SetParent(panelGO.transform, false);
            var fieldImage = fieldGO.AddComponent<Image>();
            fieldImage.color = new Color(1f, 1f, 1f, 0.9f);
            fieldRect = fieldImage.rectTransform;
            fieldRect.anchorMin = new Vector2(0f, 0f);
            fieldRect.anchorMax = new Vector2(0.78f, 0f);
            fieldRect.pivot = new Vector2(0f, 0f);
            fieldRect.offsetMin = new Vector2(10f, 8f);
            fieldRect.offsetMax = new Vector2(0f, 42f);

            inputField = fieldGO.AddComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 200;
            // Enter sends. This must be onSubmit: the field handles Enter itself (submit, then
            // deactivate) before any Update() of ours could see the key, and the same press then
            // reaches it as a UI "submit" that re-focuses it with all its text selected.
            inputField.onSubmit.AddListener(_ => HandleSubmit());
            inputField.onEndEdit.AddListener(_ => HandleEndEdit());

            var fieldTextGO = new GameObject("Text");
            fieldTextGO.transform.SetParent(fieldGO.transform, false);
            fieldText = fieldTextGO.AddComponent<Text>();
            fieldText.font = UiKit.Font;
            fieldText.fontSize = 16;
            fieldText.color = Color.black;
            fieldText.supportRichText = false;
            RuntimeUiUtil.StretchFull(fieldText.rectTransform);
            fieldText.rectTransform.offsetMin = new Vector2(8f, 4f);
            fieldText.rectTransform.offsetMax = new Vector2(-8f, -4f);
            inputField.textComponent = fieldText;

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(fieldGO.transform, false);
            placeholderText = placeholderGO.AddComponent<Text>();
            var placeholder = placeholderText;
            placeholder.font = fieldText.font;
            placeholder.fontSize = 16;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(0f, 0f, 0f, 0.5f);
            placeholder.text = "Say something...";
            RuntimeUiUtil.StretchFull(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(8f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-8f, -4f);
            inputField.placeholder = placeholder;

            var buttonGO = new GameObject("SendButton");
            buttonGO.transform.SetParent(panelGO.transform, false);
            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.5f, 0.9f, 1f);
            sendRect = buttonImage.rectTransform;
            var buttonRect = sendRect;
            buttonRect.anchorMin = new Vector2(0.78f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0f, 0f);
            buttonRect.offsetMin = new Vector2(4f, 8f);
            buttonRect.offsetMax = new Vector2(-10f, 42f);
            var button = buttonGO.AddComponent<Button>();
            button.onClick.AddListener(OnSendClicked);

            var buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);
            sendText = buttonTextGO.AddComponent<Text>();
            var buttonText = sendText;
            buttonText.font = UiKit.Font;
            buttonText.fontSize = 16;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            buttonText.text = "Send";
            RuntimeUiUtil.StretchFull(buttonText.rectTransform);
        }
    }
}
