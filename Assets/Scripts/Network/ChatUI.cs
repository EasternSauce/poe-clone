using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        private const int MaxVisibleMessages = 8;

        private InputField inputField;
        private Text logText;
        private readonly Queue<string> lines = new Queue<string>();

        /// <summary>True while the chat box has keyboard focus, so gameplay input can ignore WASD/click while typing.</summary>
        public static bool IsTyping => Instance != null && Instance.inputField != null && Instance.inputField.isFocused;

        private static ChatUI Instance;

        /// <summary>Frame in which some text field already used the Enter press (so it doesn't also open the chat).</summary>
        internal static int EnterHandledFrame = -1;

        private bool keepFocusAfterSubmit;

        private void Awake()
        {
            Instance = this;
            UiEventSystemBootstrap.EnsureExists();
            Build();
        }

        private void OnEnable()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl != null) ctrl.ChatReceived += HandleChat;
        }

        private void OnDisable()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl != null) ctrl.ChatReceived -= HandleChat;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Time.frameCount == EnterHandledFrame) return;
            if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame) return;

            // Only when nothing else has the UI focus (e.g. not while a menu button is selected).
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return;

            inputField.Select();
            inputField.ActivateInputField();
        }

        private void HandleChat(ChatEnvelope msg)
        {
            string prefix = msg.Role == "player" ? "[Player]" : "[Watching]";
            AppendLine($"{prefix} {msg.From}: {msg.Text}");
        }

        private void AppendLine(string line)
        {
            lines.Enqueue(line);
            while (lines.Count > MaxVisibleMessages)
                lines.Dequeue();

            var sb = new StringBuilder();
            foreach (var l in lines)
                sb.AppendLine(l);
            logText.text = sb.ToString();
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
            canvas.sortingOrder = 800;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.55f);
            var panelRect = panelImage.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.sizeDelta = new Vector2(460f, 220f);
            panelRect.anchoredPosition = new Vector2(-20f, 20f);

            var logGO = new GameObject("Log");
            logGO.transform.SetParent(panelGO.transform, false);
            logText = logGO.AddComponent<Text>();
            logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            logText.fontSize = 16;
            logText.color = Color.white;
            logText.supportRichText = false; // names and messages are user text
            logText.alignment = TextAnchor.LowerLeft;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            var logRect = logText.rectTransform;
            logRect.anchorMin = new Vector2(0f, 0f);
            logRect.anchorMax = new Vector2(1f, 1f);
            logRect.offsetMin = new Vector2(10f, 46f);
            logRect.offsetMax = new Vector2(-10f, -8f);

            var fieldGO = new GameObject("Input");
            fieldGO.transform.SetParent(panelGO.transform, false);
            var fieldImage = fieldGO.AddComponent<Image>();
            fieldImage.color = new Color(1f, 1f, 1f, 0.9f);
            var fieldRect = fieldImage.rectTransform;
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
            var fieldText = fieldTextGO.AddComponent<Text>();
            fieldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fieldText.fontSize = 16;
            fieldText.color = Color.black;
            fieldText.supportRichText = false;
            RuntimeUiUtil.StretchFull(fieldText.rectTransform);
            fieldText.rectTransform.offsetMin = new Vector2(8f, 4f);
            fieldText.rectTransform.offsetMax = new Vector2(-8f, -4f);
            inputField.textComponent = fieldText;

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(fieldGO.transform, false);
            var placeholder = placeholderGO.AddComponent<Text>();
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
            var buttonRect = buttonImage.rectTransform;
            buttonRect.anchorMin = new Vector2(0.78f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0f, 0f);
            buttonRect.offsetMin = new Vector2(4f, 8f);
            buttonRect.offsetMax = new Vector2(-10f, 42f);
            var button = buttonGO.AddComponent<Button>();
            button.onClick.AddListener(OnSendClicked);

            var buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);
            var buttonText = buttonTextGO.AddComponent<Text>();
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 16;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            buttonText.text = "Send";
            RuntimeUiUtil.StretchFull(buttonText.rectTransform);
        }
    }
}
