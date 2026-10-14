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
    /// "What's new": shown on the main menu once per release. Background update resumes
    /// acknowledge the release silently. The notes are
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
        private Text body, title, sub, closeText;
        private RectTransform panelRect, viewport, closeRect, divider;
        private Canvas canvas;
        private Vector2 layoutSize;
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
            PlayerHUD.SetHiddenBy(this, true);
            PoeClone.Network.GameSessionController.Instance?.RefreshWorldPause();
            Canvas.ForceUpdateCanvases();
            Layout();
        }

        private void OnDestroy()
        {
            IsShowing = false;
            PlayerHUD.SetHiddenBy(this, false);
        }

        private void Update()
        {
            if (root != null && root.activeSelf)
            {
                Vector2 size = ((RectTransform)canvas.transform).rect.size;
                if (layoutSize != size) Layout();
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !UiKit.IsTypingInTextField())
                    Close();
            }
        }

        private void Close()
        {
            root.SetActive(false);
            IsShowing = false;
            PlayerHUD.SetHiddenBy(this, false);
            PoeClone.Network.GameSessionController.Instance?.RefreshWorldPause();
            AcknowledgeCurrentRelease();
        }

        /// <summary>Background updates announce completion without interrupting play with notes.</summary>
        public void AcknowledgeCurrentRelease()
        {
            if (string.IsNullOrEmpty(version)) return;
            PlayerPrefs.SetString(SeenKey, version);
            PlayerPrefs.Save();
        }

        private void Build(string notes)
        {
            canvas = UiKit.NewCanvas("PatchNotesCanvas", transform, 960, out CanvasGroup group);
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
            panelRect = pr;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(Width, Height);
            UiKit.Frame(pr);
            pr.gameObject.AddComponent<UiAppear>().offset = Vector2.zero;
            TouchMode.AddBlocker(pr);

            title = UiKit.Heading(UiKit.NewText("Title", pr, "WHAT'S NEW", 30, UiKit.Gold, TextAnchor.UpperCenter));
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -16f), new Vector2(Width, 36f));
            sub = UiKit.NewText("Version", pr, version, 24, UiKit.TextColor, TextAnchor.UpperCenter);
            sub.font = UiKit.BoldFont;
            UiKit.TopLeft(sub.rectTransform, new Vector2(0f, -52f), new Vector2(Width, 22f));
            divider = UiKit.DividerLine(pr, new Vector2(0f, Height * 0.5f - 80f), Width - 120f).rectTransform;

            // Scrollable notes.
            viewport = UiKit.NewRect("Viewport", pr);
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
            closeRect = cr;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 18f);
            cr.sizeDelta = new Vector2(180f, 44f);
            closeText = UiKit.NewText("Text", cr, "Got it", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(closeText.rectTransform, 0f);
            UiKit.StyleButton(close, closeText);
            UiKit.OnClick(close, Close);
        }

        private void Layout()
        {
            layoutSize = ((RectTransform)canvas.transform).rect.size;
            bool touch = TouchMode.Active;
            float width = touch ? Mathf.Min(960f, layoutSize.x - 48f) : Width;
            float height = touch ? layoutSize.y - 48f : Height;
            panelRect.GetComponent<Image>().color = UiKit.ReadablePanelColor;
            panelRect.sizeDelta = new Vector2(width, height);
            title.fontSize = touch ? TouchMode.ReadingFontSize + 4 : 30;
            sub.fontSize = touch ? (TouchMode.Portrait ? 36 : 30) : 24;
            body.fontSize = touch ? TouchMode.ReadingFontSize : 18;
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -16f), new Vector2(width, touch ? 60f : 36f));
            float versionTop = touch ? 80f : 60f;
            UiKit.TopLeft(sub.rectTransform, new Vector2(24f, -versionTop), new Vector2(width - 48f, 60f));
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            Canvas.ForceUpdateCanvases();
            float versionHeight = sub.preferredHeight;
            sub.rectTransform.sizeDelta = new Vector2(width - 48f, versionHeight);
            float dividerTop = versionTop + versionHeight + (touch ? 20f : 16f);
            float top = dividerTop + (touch ? 32f : 24f);
            float bottom = touch ? 148f : 76f;
            divider.anchorMin = divider.anchorMax = divider.pivot = new Vector2(0.5f,1f);
            divider.anchoredPosition = new Vector2(0f, -dividerTop);
            divider.sizeDelta = new Vector2(width-120f,divider.sizeDelta.y);
            UiKit.TopLeft(viewport, new Vector2(30f, -top), new Vector2(width - 60f, Mathf.Max(100f, height - top - bottom)));
            closeRect.sizeDelta = touch ? new Vector2(280f, 108f) : new Vector2(180f, 44f);
            closeText.fontSize = touch ? TouchMode.ReadingFontSize : 20;
            Canvas.ForceUpdateCanvases();
            content.sizeDelta = new Vector2(0f, body.preferredHeight + 12f);
        }
    }
}
