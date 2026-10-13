using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.World;

namespace PoeClone.UI
{
    public sealed class DialogueOption
    {
        public string Label;
        public Action Pick;
        public bool Enabled = true;

        public DialogueOption(string label, Action pick, bool enabled = true)
        {
            Label = label;
            Pick = pick;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// The conversation panel: who's talking, what they say, and a column of big answer buttons
    /// (click or tap). Bottom centre of the screen; on touch the combat controls hide while it's
    /// open. What's said comes from <see cref="NpcDialogues"/>. Installed by GameSessionController.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        private const float Width = 900f;
        private const float OptionHeight = 50f;
        private const int MaxOptions = 8;

        private static DialogueUI instance;

        private Canvas canvas;
        private RectTransform panel, content;
        private ScrollRect scroll;
        private Vector2 layoutSize;
        private bool layoutTouch, layoutPortrait;
        private Text title;
        private Text body;
        private readonly List<Image> buttons = new List<Image>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<DialogueOption> current = new List<DialogueOption>();
        private Npc speaker;

        public static bool IsOpen => instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;
        public static Npc Speaker => instance != null ? instance.speaker : null;

        private static readonly Color ButtonColor = new Color(0.16f, 0.13f, 0.10f, 1f);

        public static void Show(Npc who, string text, List<DialogueOption> options)
        {
            if (instance != null)
                instance.Present(who, text, options);
        }

        public static void Close()
        {
            if (instance != null && instance.panel != null && instance.panel.gameObject.activeSelf)
            {
                instance.panel.gameObject.SetActive(false);
                instance.speaker = null;
            }
        }

        private void Awake()
        {
            instance = this;
            Build();
            panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Close();

            // Above the desktop skill bar; on a phone the combat buttons are hidden while talking.
            panel.anchoredPosition = new Vector2(0f, TouchMode.Active ? 16f : 104f);
            if (layoutSize != ((RectTransform)canvas.transform).rect.size ||
                layoutTouch != TouchMode.Active || layoutPortrait != TouchMode.Portrait)
                LayoutConversation(false);
        }

        private void Present(Npc who, string text, List<DialogueOption> options)
        {
            // The middle-of-the-screen panels would sit under the conversation.
            SkillBarUI.SetOpen(false);
            PassiveTreeUI.SetOpen(false);

            speaker = who;
            current.Clear();
            current.AddRange(options);

            title.text = who != null ? who.DisplayName : "";
            body.text = text;

            LayoutConversation(true);
            panel.gameObject.SetActive(true);
        }

        private void LayoutConversation(bool resetScroll)
        {
            layoutSize = ((RectTransform)canvas.transform).rect.size;
            layoutTouch = TouchMode.Active;
            layoutPortrait = TouchMode.Portrait;
            float width = Mathf.Min(Width, layoutSize.x - 32f);
            float textWidth = width - 48f;
            int fontSize = TouchMode.Active ? TouchMode.ReadingFontSize : 21;
            title.fontSize = TouchMode.Active ? fontSize + 4 : 26;
            title.rectTransform.sizeDelta = new Vector2(width - 100f, 48f);
            body.fontSize = fontSize;
            body.rectTransform.sizeDelta = new Vector2(textWidth, 0f);
            float bodyHeight = Mathf.Ceil(body.preferredHeight) + 6f;
            body.rectTransform.sizeDelta = new Vector2(textWidth, bodyHeight);
            float y = bodyHeight + 12f;
            for (int k = 0; k < buttons.Count; k++)
            {
                bool used = k < current.Count;
                buttons[k].gameObject.SetActive(used);
                if (!used) continue;
                DialogueOption option = current[k];
                labels[k].fontSize = TouchMode.Active ? fontSize : 20;
                labels[k].text = option.Label;
                labels[k].color = option.Enabled ? UiKit.TextColor : UiKit.DimText;
                buttons[k].rectTransform.sizeDelta = new Vector2(textWidth, OptionHeight);
                float height = Mathf.Max(TouchMode.Active ? fontSize * 2f : OptionHeight,
                    labels[k].preferredHeight + 24f);
                UiKit.TopLeft(buttons[k].rectTransform, new Vector2(0f, -y), new Vector2(textWidth, height));
                buttons[k].color = ButtonColor;
                y += height + 6f;
            }
            content.sizeDelta = new Vector2(0f, y);
            float bottom = TouchMode.Active ? 16f : 104f;
            panel.sizeDelta = new Vector2(width, Mathf.Min(y + 88f, layoutSize.y - bottom - 16f));
            if (resetScroll)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
                content.anchoredPosition = Vector2.zero;
            }
        }

        private void Pick(int index)
        {
            if (index >= current.Count || !current[index].Enabled)
                return;
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.Sfx("ui_click"));
            current[index].Pick?.Invoke();
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            canvas = UiKit.NewCanvas("DialogueCanvas", transform, 800, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            Image back = UiKit.NewImage("DialoguePanel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(back);
            back.raycastTarget = true;
            panel = back.rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(Width, 300f);
            UiKit.Frame(panel);
            panel.gameObject.AddComponent<UiAppear>().offset = Vector2.zero;
            TouchMode.AddBlocker(panel);

            title = UiKit.Heading(UiKit.NewText("Name", panel, "", 26, UiKit.Gold, TextAnchor.UpperLeft));
            UiKit.TopLeft(title.rectTransform, new Vector2(24f, -14f), new Vector2(Width - 100f, 34f));

            Image close = UiKit.NewImage("Close", panel, Color.white);
            close.raycastTarget = true;
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = Vector2.one;
            close.rectTransform.anchoredPosition = new Vector2(-12f, -12f);
            close.rectTransform.sizeDelta = new Vector2(48f, 48f);
            Text x = UiKit.NewText("X", close.rectTransform, "X", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(x.rectTransform, 0f);
            UiKit.StyleButton(close, x, UiKit.DangerTint);
            UiKit.OnClick(close, Close);

            Image viewportImage = UiKit.NewImage("ConversationViewport", panel, Color.clear);
            viewportImage.raycastTarget = true;
            RectTransform viewport = viewportImage.rectTransform;
            UiKit.Stretch(viewport, 24f);
            viewport.offsetMax = new Vector2(-24f, -70f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiKit.NewRect("ConversationContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            body = UiKit.NewText("Body", content, "", 21, UiKit.TextColor, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.supportRichText = true;
            UiKit.TopLeft(body.rectTransform, Vector2.zero, new Vector2(Width - 48f, 100f));

            for (int k = 0; k < MaxOptions; k++)
            {
                int index = k;
                Image button = UiKit.NewImage("Option" + k, content, ButtonColor);
                UiKit.Inset(button);
                button.raycastTarget = true;
                UiKit.TopLeft(button.rectTransform, new Vector2(24f, 0f), new Vector2(Width - 48f, OptionHeight));
                UiKit.AddOutline(button, new Color(0.40f, 0.31f, 0.16f, 1f), 1.5f);

                Text label = UiKit.NewText("Label", button.rectTransform, "", 20, UiKit.TextColor, TextAnchor.MiddleLeft);
                label.supportRichText = true;
                UiKit.Stretch(label.rectTransform, 12f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;

                // Click handlers let the parent ScrollRect own drags, so swiping a choice
                // scrolls the list without activating it on release.
                UiKit.OnClick(button, () => Pick(index));

                buttons.Add(button);
                labels.Add(label);
            }
        }
    }
}
