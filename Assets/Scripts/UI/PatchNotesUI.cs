using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.UI
{
    /// <summary>
    /// "What's new": shown on the main menu once per release. The notes are
    /// Resources/PatchNotes.txt, whose first line is
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
        public static bool IsShowing { get; private set; }

        internal static List<KeyValuePair<string, string>> ParseReleases(string text)
        {
            var releases = new List<KeyValuePair<string, string>>();
            string currentVersion = null;
            var notes = new StringBuilder();
            foreach (string line in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string header = line.Trim().TrimStart('\uFEFF');
                if (header.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(currentVersion))
                        releases.Add(new KeyValuePair<string, string>(currentVersion, notes.ToString().Trim()));
                    currentVersion = header.Substring("version:".Length).Trim();
                    notes.Clear();
                }
                else if (currentVersion != null) notes.AppendLine(line);
            }
            if (!string.IsNullOrEmpty(currentVersion))
                releases.Add(new KeyValuePair<string, string>(currentVersion, notes.ToString().Trim()));
            return releases;
        }

        private void Awake()
        {
            TextAsset notes = Resources.Load<TextAsset>("PatchNotes");
            if (notes == null) return;

            var releases = ParseReleases(notes.text);
            if (releases.Count == 0) return;

            version = releases[0].Key;
            string releaseNotes = releases[0].Value;
            foreach (var release in releases)
                PlayerPrefs.SetString("PoeClone.PatchNotes." + release.Key, release.Value);
            EscapeMenuUI.StoreRelease(version, releaseNotes);
            PlayerPrefs.Save();
            Build(releaseNotes);
            root.SetActive(false);
        }

        /// <summary>Called after the main menu is built, before choosing a character.</summary>
        public void ShowIfUnseen()
        {
            if (root == null || PlayerPrefs.GetString(SeenKey, "") == version) return;
            root.SetActive(true);
            IsShowing = true;
            Canvas.ForceUpdateCanvases();
            content.sizeDelta = new Vector2(0f, body.preferredHeight + 12f);
        }

        private void OnDestroy()
        {
            IsShowing = false;
        }

        private void Update()
        {
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
            IsShowing = false;
            PlayerPrefs.SetString(SeenKey, version);
            PlayerPrefs.Save();
        }

        private void Build(string notes)
        {
            Canvas canvas = UiKit.NewCanvas("PatchNotesCanvas", transform, 960, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;
            root = canvas.gameObject;

            // Dims the menu behind, and takes the clicks meant for it.
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            shade.raycastTarget = true;
            UiKit.Stretch(shade.rectTransform, 0f);

            Image panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(panel);
            panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(Width, Height);
            UiKit.Frame(pr);
            pr.gameObject.AddComponent<UiAppear>();
            TouchMode.AddBlocker(pr);

            Text title = UiKit.Heading(UiKit.NewText("Title", pr, "WHAT'S NEW", 30, UiKit.Gold, TextAnchor.UpperCenter));
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -16f), new Vector2(Width, 36f));
            Text sub = UiKit.NewText("Version", pr, version, 16, UiKit.DimText, TextAnchor.UpperCenter);
            UiKit.TopLeft(sub.rectTransform, new Vector2(0f, -52f), new Vector2(Width, 22f));
            UiKit.DividerLine(pr, new Vector2(0f, Height * 0.5f - 80f), Width - 120f);

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

            Image close = UiKit.NewImage("Close", pr, Color.white);
            close.raycastTarget = true;
            RectTransform cr = close.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 18f);
            cr.sizeDelta = new Vector2(180f, 44f);
            Text closeText = UiKit.NewText("Text", cr, "Got it", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(closeText.rectTransform, 0f);
            UiKit.StyleButton(close, closeText);
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ => Close();
        }
    }
}
