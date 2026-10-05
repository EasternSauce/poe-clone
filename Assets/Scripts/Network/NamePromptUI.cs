using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.Inventory;
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
        private Action afterCharacter;
        private float timeScaleBeforeStashRename;

        /// <summary>Use the character creation input as a full-screen, required stash tab name form.</summary>
        public void ShowStashTabRename(string currentName, Action<string> done)
        {
            if (IsShowing || UiKit.IsStashNamePromptOpen) return;

            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") child.gameObject.SetActive(false);
            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") Destroy(child.gameObject);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MakeText("Name your stash tab", font, 30, new Vector2(0, 100), new Vector2(800, 60));
            inputField = MakeInput(font);
            inputField.characterLimit = PlayerInventory.StashTabNameLimit;
            inputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 10);
            inputField.text = currentName ?? string.Empty;

            Action confirm = () =>
            {
                string name = inputField.text.Trim();
                if (name.Length == 0)
                {
                    StartCoroutine(FocusStashNameInput());
                    return;
                }
                UiKit.IsStashNamePromptOpen = false;
                Time.timeScale = timeScaleBeforeStashRename;
                canvasRoot.SetActive(false);
                PlayerHUD.SetHiddenBy(this, false);
                UiKit.EnterHandledFrame = Time.frameCount;
                UiKit.TextEditEndedFrame = Time.frameCount;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                done?.Invoke(name);
            };
            inputField.onSubmit.AddListener(_ => confirm());
            MakeButton("Confirm", font, new Vector2(0, -70), new Vector2(240, 54), confirm);

            timeScaleBeforeStashRename = Time.timeScale;
            UiKit.IsStashNamePromptOpen = true;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            Time.timeScale = 0f;
            StartCoroutine(FocusStashNameInput());
        }

        private IEnumerator FocusStashNameInput()
        {
            // The tab's double-click must finish before this field claims keyboard focus.
            yield return null;
            if (!UiKit.IsStashNamePromptOpen) yield break;
            inputField.Select();
            inputField.ActivateInputField();
            inputField.selectionAnchorPosition = 0;
            inputField.selectionFocusPosition = inputField.text.Length;
        }

        private void OnDestroy()
        {
            UiKit.StashTabNamePrompt = null;
            if (UiKit.IsStashNamePromptOpen)
            {
                UiKit.IsStashNamePromptOpen = false;
                Time.timeScale = timeScaleBeforeStashRename;
            }
        }

        public void ShowCharacters(Action done)
        {
            afterCharacter = done;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            // Replace the name form with saved characters and their equipped models.
            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") Destroy(child.gameObject);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MakeText("Choose Character", font, 34, new Vector2(0, 330), new Vector2(1000, 60));
            var profiles = PoeClone.Player.SaveSystem.Profiles();
            var inventories = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var inventory = inventories.Length > 0 ? inventories[0] : null;
            var source = inventory != null ? inventory.GetComponentInChildren<EquipmentVisuals>(true) : null;
            float y = 220;
            foreach (var profile in profiles)
            {
                string id = profile.id;
                var row = MakeButton(profile.name + "   ·   Level " + PoeClone.Player.SaveSystem.ProfileLevel(profile), font, new Vector2(-70, y), new Vector2(560, 112), () =>
                {
                    PoeClone.Player.SaveSystem.SelectProfile(id);
                    PlayerName = PoeClone.Player.SaveSystem.ActiveCharacterName;
                    FinishCharacters();
                });
                var label = row.transform.Find("Label").GetComponent<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(116, 0);
                if (source != null)
                    AddPortrait(row.transform, source, PoeClone.Player.SaveSystem.ProfileSave(profile));
                var remove = MakeButton("Remove", font, new Vector2(300, y), new Vector2(140, 64), () => ShowRemoveConfirmation(id, profile.name, font));
                remove.GetComponent<Image>().color = new Color(.55f, .2f, .2f, .98f);
                y -= 126;
            }
            MakeButton("Create New Character", font, new Vector2(0, y - 8), new Vector2(300, 54), () => ShowCreateCharacter(font));
        }

        private void ShowRemoveConfirmation(string id, string name, Font font)
        {
            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") Destroy(child.gameObject);
            MakeText("Remove " + name + "?", font, 32, new Vector2(0, 100), new Vector2(900, 70));
            MakeText("This permanently deletes this character and its saved progress.", font, 22, new Vector2(0, 20), new Vector2(1000, 60));
            var remove = MakeButton("Remove Character", font, new Vector2(-130, -90), new Vector2(240, 54), () =>
            {
                PoeClone.Player.SaveSystem.DeleteProfile(id);
                ShowCharacters(afterCharacter);
            });
            remove.GetComponent<Image>().color = new Color(.55f, .2f, .2f, .98f);
            MakeButton("Cancel", font, new Vector2(130, -90), new Vector2(180, 54), () => ShowCharacters(afterCharacter));
        }

        private string PlayerName;
        private void ShowCreateCharacter(Font font)
        {
            foreach (Transform child in canvasRoot.transform) if (child.name != "Background") Destroy(child.gameObject);
            MakeText("Name your character", font, 30, new Vector2(0, 100), new Vector2(800, 60));
            inputField = MakeInput(font);
            inputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 10);
            MakeButton("Create and Play", font, new Vector2(0, -70), new Vector2(240, 54), () =>
            {
                string name = inputField.text.Trim();
                if (name.Length == 0) return;
                PoeClone.Player.SaveSystem.CreateProfile(name);
                PlayerName = name;
                FinishCharacters();
            });
            MakeButton("Back", font, new Vector2(0, -140), new Vector2(140, 44), () => ShowCharacters(afterCharacter));
        }

        private InputField MakeInput(Font font)
        {
            var go = new GameObject("CharacterName"); go.transform.SetParent(canvasRoot.transform, false);
            var image = go.AddComponent<Image>(); image.color = Color.white;
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(420, 52);
            var field = go.AddComponent<InputField>(); field.characterLimit = MaxNameLength; field.lineType = InputField.LineType.SingleLine;
            var textGo = new GameObject("Text"); textGo.transform.SetParent(go.transform, false); var text = textGo.AddComponent<Text>(); text.font = font; text.fontSize = 24; text.color = Color.black; text.alignment = TextAnchor.MiddleLeft; RuntimeUiUtil.StretchFull(text.rectTransform); text.rectTransform.offsetMin = new Vector2(12, 4); text.rectTransform.offsetMax = new Vector2(-12, -4); field.textComponent = text;
            return field;
        }

        private Text MakeText(string value, Font font, int size, Vector2 at, Vector2 dimensions)
        {
            var go = new GameObject(value); go.transform.SetParent(canvasRoot.transform, false); var t = go.AddComponent<Text>(); t.font = font; t.fontSize = size; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.text = value;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = dimensions; return t;
        }

        private void AddPortrait(Transform row, EquipmentVisuals source, SaveData save)
        {
            var equipment = new EquipmentSet();
            if (save != null && save.equipped != null)
            {
                foreach (var record in save.equipped)
                {
                    if (record == null || record.item == null) continue;
                    try { equipment.TryEquip((EquipSlot)record.slot, record.item.ToItem(), out _); }
                    catch (Exception e) { Debug.LogWarning("Character portrait: skipped invalid gear. " + e.Message); }
                }
            }
            var portrait = new GameObject("CharacterPortrait", typeof(RectTransform), typeof(RawImage));
            portrait.transform.SetParent(row, false);
            portrait.GetComponent<RawImage>().raycastTarget = false;
            var rect = portrait.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(12f, 0f);
            rect.sizeDelta = new Vector2(88f, 108f);
            var preview = portrait.AddComponent<CharacterPreview>();
            if (preview.Build(source, equipment))
            {
                portrait.GetComponent<RawImage>().texture = preview.Texture;
                preview.SetActive(true);
            }
        }

        private GameObject MakeButton(string label, Font font, Vector2 at, Vector2 dimensions, Action click)
        {
            var go = new GameObject("ProfileButton"); go.transform.SetParent(canvasRoot.transform, false); var image = go.AddComponent<Image>(); image.color = new Color(.2f, .4f, .65f, .98f); var r = image.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = dimensions;
            var b = go.AddComponent<Button>(); b.onClick.AddListener(() => click()); var tgo = new GameObject("Label"); tgo.transform.SetParent(go.transform, false); var t = tgo.AddComponent<Text>(); t.font = font; t.fontSize = 22; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.text = label; RuntimeUiUtil.StretchFull(t.rectTransform);
            return go;
        }

        private void FinishCharacters()
        {
            canvasRoot.SetActive(false); HasConfirmed = true; PlayerHUD.SetHiddenBy(this, false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            var done = afterCharacter; afterCharacter = null; done?.Invoke();
        }

        public bool IsShowing => canvasRoot != null && canvasRoot.activeSelf;

        /// <summary>The name has been given (the game is starting), at least once.</summary>
        public bool HasConfirmed { get; private set; }

        private void Awake()
        {
            UiEventSystemBootstrap.EnsureExists();
            Build();
            canvasRoot.SetActive(false);
            UiKit.StashTabNamePrompt = ShowStashTabRename;
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
            HasConfirmed = true;
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
