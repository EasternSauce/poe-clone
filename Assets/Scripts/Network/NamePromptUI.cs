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
    public partial class NamePromptUI : MonoBehaviour
    {
        // Matches MAX_NAME_LENGTH in server/room.js.
        public const int MaxNameLength = 24;

        private GameObject canvasRoot;
        private InputField inputField;
        private Action<string> onDone;
        private Action afterCharacter;
        private Action charactersBack;
        private float timeScaleBeforeStashRename;

        /// <summary>Use the character creation input as a full-screen, required stash tab name form.</summary>
        public void ShowStashTabRename(string currentName, Action<string> done)
        {
            if (IsShowing || UiKit.IsStashNamePromptOpen) return;

            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") child.gameObject.SetActive(false);
            ClearScreen();

            MakeText("Name your stash tab", 30, new Vector2(0, 100), new Vector2(800, 60));
            inputField = MakeInput();
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
            MakeButton("Confirm", new Vector2(0, -70), new Vector2(240, 54), confirm);

            timeScaleBeforeStashRename = Time.timeScale;
            UiKit.IsStashNamePromptOpen = true;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            if (!PoeClone.Combat.Party.Active)
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

        /// <summary>Character select. <paramref name="back"/> returns to the menu before it (kept for the create/remove screens).</summary>
        public void ShowCharacters(Action done, Action back = null)
        {
            afterCharacter = done;
            if (back != null)
                charactersBack = back;
            onSinglePlayer = null;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            // Replace the name form with saved characters and their equipped models.
            ClearScreen();
            MakeText("Choose Character", 34, new Vector2(0, 330), new Vector2(1000, 60));
            var profiles = PoeClone.Player.SaveSystem.Profiles();
            var inventories = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var inventory = inventories.Length > 0 ? inventories[0] : null;
            var source = inventory != null ? inventory.GetComponentInChildren<EquipmentVisuals>(true) : null;
            float y = 220;
            foreach (var profile in profiles)
            {
                string id = profile.id;
                var row = MakeButton(profile.name + "   ·   Level " + PoeClone.Player.SaveSystem.ProfileLevel(profile), new Vector2(-70, y), new Vector2(560, 112), () =>
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
                MakeButton("Remove", new Vector2(300, y), new Vector2(140, 64), () => ShowRemoveConfirmation(id, profile.name), UiKit.DangerTint);
                y -= 126;
            }
            MakeButton("Create New Character", new Vector2(0, y - 8), new Vector2(340, 58), ShowCreateCharacter);
            if (charactersBack != null)
            {
                Action previous = charactersBack;
                MakeBack(() => { afterCharacter = null; previous(); }, y - 78);
            }
        }

        private void ShowRemoveConfirmation(string id, string name)
        {
            ClearScreen();
            MakeText("Remove " + name + "?", 32, new Vector2(0, 100), new Vector2(900, 70));
            MakeText("This permanently deletes this character and its saved progress.", 22, new Vector2(0, 20), new Vector2(1000, 60));
            MakeButton("Remove Character", new Vector2(-140, -90), new Vector2(260, 54), () =>
            {
                PoeClone.Player.SaveSystem.DeleteProfile(id);
                ShowCharacters(afterCharacter);
            }, UiKit.DangerTint);
            MakeButton("Cancel", new Vector2(130, -90), new Vector2(180, 54), () => ShowCharacters(afterCharacter));
        }

        private string PlayerName;
        private void ShowCreateCharacter()
        {
            ClearScreen();
            MakeText("Name your character", 30, new Vector2(0, 100), new Vector2(800, 60));
            inputField = MakeInput();
            inputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 10);
            MakeButton("Create and Play", new Vector2(0, -70), new Vector2(260, 54), () =>
            {
                string name = inputField.text.Trim();
                if (name.Length == 0) return;
                PoeClone.Player.SaveSystem.CreateProfile(name);
                PlayerName = name;
                FinishCharacters();
            });
            MakeButton("Back", new Vector2(0, -140), new Vector2(160, 46), () => ShowCharacters(afterCharacter), UiKit.MutedTint);
        }

        private InputField MakeInput()
        {
            var image = UiKit.NewImage("CharacterName", canvasRoot.transform, new Color(.05f, .04f, .035f, 1f));
            UiKit.Inset(image);
            image.raycastTarget = true;
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(440, 56);
            UiKit.Frame(rect, 3f);
            var field = image.gameObject.AddComponent<InputField>(); field.characterLimit = MaxNameLength; field.lineType = InputField.LineType.SingleLine;
            field.customCaretColor = true; field.caretColor = UiKit.Gold; field.selectionColor = new Color(.86f, .62f, .25f, .45f);
            var text = UiKit.NewText("Text", rect, "", 26, UiKit.TextColor, TextAnchor.MiddleLeft); text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            RuntimeUiUtil.StretchFull(text.rectTransform); text.rectTransform.offsetMin = new Vector2(16, 4); text.rectTransform.offsetMax = new Vector2(-16, -4); field.textComponent = text;
            Appear(rect);
            return field;
        }

        // Each new screen's items fade in one after another.
        private int appearOrder;
        private bool animateScreen;
        private string screenKey;

        private void ClearScreen(string key = null)
        {
            appearOrder = 0;
            animateScreen = key == null || key != screenKey;
            screenKey = key;
            foreach (Transform child in canvasRoot.transform)
                if (child.name != "Background") Destroy(child.gameObject);
        }

        private void Appear(RectTransform rect)
        {
            if (animateScreen)
                rect.gameObject.AddComponent<UiAppear>().delay = 0.05f * appearOrder++;
        }

        // Sizes of 30 and up are screen headings: Cinzel, gold, with a divider underneath.
        private Text MakeText(string value, int size, Vector2 at, Vector2 dimensions)
        {
            bool heading = size >= 30;
            var t = UiKit.NewText(value, canvasRoot.transform, value, size, heading ? UiKit.Gold : UiKit.TextColor, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = dimensions;
            if (heading)
            {
                UiKit.Heading(t);
                t.fontSize = size + 6;
                UiKit.DividerLine(r, new Vector2(0f, -size * 0.95f), Mathf.Min(560f, dimensions.x));
            }
            Appear(r);
            return t;
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

        private GameObject MakeButton(string label, Vector2 at, Vector2 dimensions, Action click, Color? tint = null)
        {
            var image = UiKit.NewImage("ProfileButton", canvasRoot.transform, Color.white); image.raycastTarget = true;
            var r = image.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = dimensions;
            var b = image.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => click());
            var t = UiKit.NewText("Label", r, label, 22, UiKit.TextColor, TextAnchor.MiddleCenter); RuntimeUiUtil.StretchFull(t.rectTransform);
            UiKit.StyleButton(image, t, tint);
            Appear(r);
            return image.gameObject;
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

        /// <summary>The spectator's optional display name form.</summary>
        public void Show(string initialName, string confirmLabel, Action<string> done)
        {
            onDone = done;
            canvasRoot.SetActive(true);
            PlayerHUD.SetHiddenBy(this, true);
            ClearScreen();
            MakeText("Your name (optional)", 34, new Vector2(0f, 130f), new Vector2(900f, 60f));
            MakeText("Shown next to your chat messages", 22, new Vector2(0f, 72f), new Vector2(900f, 34f)).color = UiKit.DimText;
            inputField = MakeInput();
            inputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 10f);
            var placeholder = UiKit.NewText("Placeholder", inputField.transform, "Anonymous", 26, UiKit.DimText, TextAnchor.MiddleLeft);
            placeholder.fontStyle = FontStyle.Italic;
            RuntimeUiUtil.StretchFull(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(16f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-16f, -4f);
            inputField.placeholder = placeholder;
            inputField.text = initialName ?? string.Empty;
            // Enter confirms. onSubmit fires from inside the field's own key handling, before it
            // deactivates itself, so the Enter press can't be lost (see ChatUI).
            inputField.onSubmit.AddListener(_ =>
            {
                ChatUI.EnterHandledFrame = Time.frameCount;
                Confirm();
            });
            MakeButton(confirmLabel, new Vector2(0f, -70f), new Vector2(220f, 54f), Confirm);
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

            UiKit.Backdrop(canvasRoot.transform);
        }
    }
}
