using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;

namespace PoeClone.UI
{
    /// <summary>
    /// "What's new": shown once per release to each player and spectator, when a version they
    /// haven't closed yet first loads. The notes are Resources/PatchNotes.txt, whose first line is
    /// "version: ..."; closing the window remembers that version (PlayerPrefs), so it stays shut
    /// until the next release changes it. Installed by GameSessionController.
    /// </summary>
    public class PatchNotesUI : MonoBehaviour
    {
        private const string SeenKey = "PoeClone.PatchNotesSeen";
        private const float Width = 760f;
        private const float Height = 620f;

        private GameObject root;
        private RectTransform content;
        private Text body;
        private string version;
        private bool decided;
        private NamePromptUI namePrompt;

        private void Start()
        {
            TextAsset notes = Resources.Load<TextAsset>("PatchNotes");
            if (notes == null)
            {
                decided = true;
                return;
            }

            string text = notes.text.Replace("\r\n", "\n");
            int firstBreak = text.IndexOf('\n');
            string header = firstBreak >= 0 ? text.Substring(0, firstBreak) : text;
            if (!header.StartsWith("version:"))
            {
                decided = true;
                return;
            }

            version = header.Substring("version:".Length).Trim();
            Build(firstBreak >= 0 ? text.Substring(firstBreak + 1).Trim() : "");
            root.SetActive(false);
            namePrompt = GetComponent<NamePromptUI>();
        }

        private void Update()
        {
            if (!decided)
            {
                // After the name prompt, so the two don't fight over the screen.
                if (namePrompt != null && namePrompt.IsShowing)
                    return;
                decided = true;
                if (PlayerPrefs.GetString(SeenKey, "") != version)
                {
                    root.SetActive(true);
                    // Tall enough for the wrapped notes, now that the panel has its real width.
                    Canvas.ForceUpdateCanvases();
                    content.sizeDelta = new Vector2(0f, body.preferredHeight + 12f);
                }
                return;
            }

            if (root != null && root.activeSelf)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !UiKit.IsTypingInTextField())
                    Close();
            }
        }

        private void Close()
        {
            root.SetActive(false);
            PlayerPrefs.SetString(SeenKey, version);
            PlayerPrefs.Save();
        }

        private void Build(string notes)
        {
            Canvas canvas = UiKit.NewCanvas("PatchNotesCanvas", transform, 940, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;
            root = canvas.gameObject;

            // Dims the game behind, and takes the clicks meant for it.
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            shade.raycastTarget = true;
            UiKit.Stretch(shade.rectTransform, 0f);

            Image panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(panel);
            panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(Width, Height);
            UiKit.AddOutline(panel, UiKit.BorderColor, 3f);
            TouchMode.AddBlocker(pr);

            Text title = UiKit.NewText("Title", pr, "WHAT'S NEW", 28, UiKit.Gold, TextAnchor.UpperCenter);
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -16f), new Vector2(Width, 36f));
            Text sub = UiKit.NewText("Version", pr, version, 16, UiKit.DimText, TextAnchor.UpperCenter);
            UiKit.TopLeft(sub.rectTransform, new Vector2(0f, -52f), new Vector2(Width, 22f));

            // Scrollable notes.
            RectTransform viewport = UiKit.NewRect("Viewport", pr);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear; // lets the wheel and drags reach the scroll view
            UiKit.TopLeft(viewport, new Vector2(30f, -86f), new Vector2(Width - 60f, Height - 86f - 76f));

            content = UiKit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            body = UiKit.NewText("Notes", content, notes, 18, UiKit.TextColor, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.1f;
            UiKit.Stretch(body.rectTransform, 0f);
            content.sizeDelta = new Vector2(0f, Height);

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            Image close = UiKit.NewImage("Close", pr, new Color(0.28f, 0.2f, 0.1f, 1f));
            close.raycastTarget = true;
            RectTransform cr = close.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 18f);
            cr.sizeDelta = new Vector2(180f, 44f);
            UiKit.AddOutline(close, UiKit.Gold, 1.5f);
            Text closeText = UiKit.NewText("Text", cr, "Got it", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(closeText.rectTransform, 0f);
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ => Close();
        }
    }
}
