using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// Start-up screen asking for an optional display name (shown next to this client's chat
    /// messages). Shown once before connecting; an empty name lets the server pick the default
    /// ("Player" / "Spectator N"). Built at runtime like the rest of this project's UI.
    /// </summary>
    public class NamePromptUI : MonoBehaviour
    {
        // Matches MAX_NAME_LENGTH in server/room.js.
        public const int MaxNameLength = 24;

        private GameObject canvasRoot;
        private InputField inputField;
        private Text buttonText;
        private Action<string> onDone;

        public bool IsShowing => canvasRoot != null && canvasRoot.activeSelf;

        private void Awake()
        {
            UiEventSystemBootstrap.EnsureExists();
            Build();
            canvasRoot.SetActive(false);
        }

        public void Show(string initialName, string confirmLabel, Action<string> done)
        {
            onDone = done;
            inputField.text = initialName ?? string.Empty;
            buttonText.text = confirmLabel;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            inputField.Select();
            inputField.ActivateInputField();
        }

        private void Confirm()
        {
            if (onDone == null) return;

            string name = inputField.text.Trim();
            var done = onDone;
            onDone = null;

            canvasRoot.SetActive(false);
            PlayerHUD.SetHiddenBy(this, false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            done(name);
        }

        private void Build()
        {
            canvasRoot = new GameObject("NamePromptCanvas");
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950; // above SessionGateUI (900) and SpectatorView

            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasRoot.AddComponent<PoeClone.Inventory.TouchAwareScaler>();
            canvasRoot.AddComponent<GraphicRaycaster>();

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasRoot.transform, false);
            var bg = bgGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.95f);
            RuntimeUiUtil.StretchFull(bg.rectTransform);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(canvasRoot.transform, false);
            var title = titleGO.AddComponent<Text>();
            title.font = font;
            title.fontSize = 34;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "Your name (optional)\n<size=22>Shown next to your chat messages</size>";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(900f, 110f);
            titleRect.anchoredPosition = new Vector2(0f, 110f);

            var fieldGO = new GameObject("NameInput");
            fieldGO.transform.SetParent(canvasRoot.transform, false);
            var fieldImage = fieldGO.AddComponent<Image>();
            fieldImage.color = new Color(1f, 1f, 1f, 0.95f);
            var fieldRect = fieldImage.rectTransform;
            fieldRect.anchorMin = fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            fieldRect.sizeDelta = new Vector2(420f, 50f);
            fieldRect.anchoredPosition = new Vector2(0f, 10f);

            inputField = fieldGO.AddComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = MaxNameLength;
            // Enter confirms. onSubmit fires from inside the field's own key handling, before it
            // deactivates itself, so the Enter press can't be lost (see ChatUI).
            inputField.onSubmit.AddListener(_ =>
            {
                ChatUI.EnterHandledFrame = Time.frameCount;
                Confirm();
            });

            var fieldTextGO = new GameObject("Text");
            fieldTextGO.transform.SetParent(fieldGO.transform, false);
            var fieldText = fieldTextGO.AddComponent<Text>();
            fieldText.font = font;
            fieldText.fontSize = 24;
            fieldText.color = Color.black;
            fieldText.alignment = TextAnchor.MiddleLeft;
            fieldText.supportRichText = false;
            RuntimeUiUtil.StretchFull(fieldText.rectTransform);
            fieldText.rectTransform.offsetMin = new Vector2(12f, 4f);
            fieldText.rectTransform.offsetMax = new Vector2(-12f, -4f);
            inputField.textComponent = fieldText;

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(fieldGO.transform, false);
            var placeholder = placeholderGO.AddComponent<Text>();
            placeholder.font = font;
            placeholder.fontSize = 24;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0f, 0f, 0f, 0.5f);
            placeholder.text = "Anonymous";
            RuntimeUiUtil.StretchFull(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(12f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-12f, -4f);
            inputField.placeholder = placeholder;

            var buttonGO = new GameObject("ConfirmButton");
            buttonGO.transform.SetParent(canvasRoot.transform, false);
            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.5f, 0.9f, 1f);
            var buttonRect = buttonImage.rectTransform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(200f, 50f);
            buttonRect.anchoredPosition = new Vector2(0f, -60f);
            var button = buttonGO.AddComponent<Button>();
            button.onClick.AddListener(Confirm);

            var buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);
            buttonText = buttonTextGO.AddComponent<Text>();
            buttonText.font = font;
            buttonText.fontSize = 24;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            RuntimeUiUtil.StretchFull(buttonText.rectTransform);
        }
    }
}
