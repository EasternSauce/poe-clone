using System;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.UI
{
    /// <summary>ESC settings and a browsable, bundled archive of every recovered patch note release.</summary>
    [DefaultExecutionOrder(-1000)]
    public class EscapeMenuUI : MonoBehaviour
    {
        [Serializable] private class Entry { public string version; public string notes; }
        [Serializable] private class Archive { public Entry[] entries; }
        private static float W => TouchMode.Active ? 900f : 760f;
        // The panel fills the screen's height, less this margin top and bottom; the lists stretch with it.
        private const float Margin = 24f;
        private GameObject root;
        private RectTransform viewport, content, notesViewport, notesContent;
        private ScrollRect listScroll, notesScroll;
        private GameObject settingsButton, historyButton;
        private Text title, body;
        private RectTransform panelRect, resumeRect;
        private GameObject newerButton, olderButton;
        private bool showingHistory;
        private Vector2 layoutSize;
        private Entry[] entries = Array.Empty<Entry>();
        private int selected;
        private bool pausedByMenu;
        private float timeScaleBeforeMenu;
        private bool audioPausedBeforeMenu;

        public bool IsOpen => root != null && root.activeSelf;

        public static void StoreRelease(string version, string notes)
        {
            var merged = new System.Collections.Generic.List<Entry>();
            AddArchive(merged, Resources.Load<TextAsset>("PatchNotesHistory")?.text);
            AddArchive(merged, PlayerPrefs.GetString("PoeClone.PatchNotesArchive", ""));
            AddEntry(merged, new Entry { version = version, notes = notes });
            AddReleaseText(merged, Resources.Load<TextAsset>("PatchNotes")?.text);
            PlayerPrefs.SetString("PoeClone.PatchNotesArchive", JsonUtility.ToJson(new Archive { entries = merged.ToArray() }));
            PlayerPrefs.Save();
        }

        private static void AddArchive(System.Collections.Generic.List<Entry> into, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                Archive archive = JsonUtility.FromJson<Archive>(json);
                if (archive == null || archive.entries == null) return;
                foreach (Entry entry in archive.entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.version))
                        AddReleaseText(into, "version: " + entry.version + "\n" + entry.notes);
            }
            catch { }
        }

        private static void AddReleaseText(List<Entry> into, string text)
        {
            foreach (var release in PatchNotesUI.ParseReleases(text))
                AddEntry(into, new Entry { version = release.Key, notes = release.Value });
        }

        private static void AddEntry(System.Collections.Generic.List<Entry> into, Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.version)) return;
            int existing = into.FindIndex(e => e.version == entry.version);
            if (existing >= 0) into[existing] = entry; else into.Add(entry);
        }

        private void Awake()
        {
            TextAsset asset = Resources.Load<TextAsset>("PatchNotesHistory");
            if (asset != null)
            {
                try { Archive a = JsonUtility.FromJson<Archive>(asset.text); if (a != null && a.entries != null) entries = a.entries; }
                catch (Exception e) { Debug.LogWarning("Could not load patch note archive: " + e.Message); }
            }
            Build();
            root.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && layoutSize != new Vector2(Screen.width, Screen.height))
            {
                Layout();
                if (showingHistory) ShowHistory(); else ShowSettings();
            }
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || UiKit.IsTypingInTextField()) return;
            if (PatchNotesUI.IsShowing) return;
            if (CoopPartnerUI.IsOpen) return;
            if (root.activeSelf) { Close(); return; }
            if (GetComponent<NamePromptUI>()?.IsShowing == true) return;
            if (WeaponSkillTreeUI.IsOpen || DialogueUI.IsOpen || PassiveTreeUI.IsOpen || SkillBarUI.IsOpen || SkillBarUI.PickerOpen ||
                AnyOpen<InventoryUI>() || AnyOpen<CharacterPageUI>()) return;
            OpenSettings();
        }

        private static bool AnyOpen<T>() where T : MonoBehaviour
        {
            foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component is InventoryUI inventory && inventory.IsOpen) return true;
                if (component is CharacterPageUI character && character.IsOpen) return true;
            }
            return false;
        }

        public void OpenSettings()
        {
            if (PatchNotesUI.IsShowing || (!IsOpen && Time.timeScale <= 0f)) return;
            // Active first: the list's height comes from the canvas, which only scales while active.
            root.SetActive(true);
            Layout();
            ShowSettings();
            // The HUD is drawn with OnGUI, over every canvas: it would show through the shade.
            PlayerHUD.SetHiddenBy(this, true);
            // Co-op: the world is shared, so the menu opens over the running game.
            if (pausedByMenu || PoeClone.Combat.Party.Active) return;
            timeScaleBeforeMenu = Time.timeScale;
            audioPausedBeforeMenu = AudioListener.pause;
            pausedByMenu = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void Close()
        {
            root.SetActive(false);
            PlayerHUD.SetHiddenBy(this, false);
            if (!pausedByMenu) return;
            pausedByMenu = false;
            GameSessionController session = GameSessionController.Instance;
            if (session != null)
            {
                session.RefreshWorldPause();
                return;
            }
            Time.timeScale = timeScaleBeforeMenu;
            AudioListener.pause = audioPausedBeforeMenu;
        }

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("EscapeMenuCanvas", transform, 960, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>(); group.interactable = true; group.blocksRaycasts = true; root = canvas.gameObject;
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f,0f,0f,0.6f)); shade.raycastTarget = true; UiKit.Stretch(shade.rectTransform, 0f);
            Image panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor); UiKit.Grain(panel); panel.raycastTarget = true;
            RectTransform pr=panel.rectTransform; panelRect=pr; pr.anchorMin=new Vector2(.5f,0); pr.anchorMax=new Vector2(.5f,1); pr.sizeDelta=new Vector2(W,-Margin*2); UiKit.Frame(pr); pr.gameObject.AddComponent<UiAppear>().offset = Vector2.zero; TouchMode.AddBlocker(pr);
            title=UiKit.Heading(UiKit.NewText("Title",pr,"MENU",30,UiKit.Gold,TextAnchor.MiddleLeft)); UiKit.TopLeft(title.rectTransform,new Vector2(30,-16),new Vector2(W-100,40));
            settingsButton=Button("Settings",pr,"Settings",new Vector2(30,-68),new Vector2(180,42),ShowSettings);
            historyButton=Button("History",pr,"Patch History",new Vector2(222,-68),new Vector2(180,42),ShowHistory);
            GameObject resume=Button("Resume",pr,"Resume",Vector2.zero,new Vector2(180,42),Close); RectTransform rr=(RectTransform)resume.transform; rr.anchorMin=rr.anchorMax=rr.pivot=Vector2.zero; rr.anchoredPosition=new Vector2(W-210,24); resumeRect=rr;
            RectTransform divider=UiKit.DividerLine(pr,Vector2.zero,W-60).rectTransform; divider.anchorMin=divider.anchorMax=new Vector2(.5f,1); divider.anchoredPosition=new Vector2(0,-116);
            viewport=UiKit.NewRect("Viewport",pr); viewport.gameObject.AddComponent<RectMask2D>(); Image catcher=viewport.gameObject.AddComponent<Image>(); catcher.color=Color.clear;
            PlaceList(viewport,30,W-60);
            content=UiKit.NewRect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero;
            listScroll=viewport.gameObject.AddComponent<ScrollRect>(); listScroll.content=content; listScroll.viewport=viewport; listScroll.horizontal=false; listScroll.movementType=ScrollRect.MovementType.Clamped; listScroll.scrollSensitivity=30;
            notesViewport=UiKit.NewRect("NotesViewport",pr); notesViewport.gameObject.AddComponent<RectMask2D>(); Image notesCatcher=notesViewport.gameObject.AddComponent<Image>(); notesCatcher.color=Color.clear;
            PlaceList(notesViewport,300,W-330);
            notesContent=UiKit.NewRect("NotesContent",notesViewport); notesContent.anchorMin=new Vector2(0,1); notesContent.anchorMax=new Vector2(1,1); notesContent.pivot=new Vector2(.5f,1); notesContent.anchoredPosition=Vector2.zero;
            notesScroll=notesViewport.gameObject.AddComponent<ScrollRect>(); notesScroll.content=notesContent; notesScroll.viewport=notesViewport; notesScroll.horizontal=false; notesScroll.movementType=ScrollRect.MovementType.Clamped; notesScroll.scrollSensitivity=30;
            body=UiKit.NewText("Body",notesContent,"",18,UiKit.TextColor,TextAnchor.UpperLeft); body.horizontalOverflow=HorizontalWrapMode.Wrap; body.verticalOverflow=VerticalWrapMode.Overflow; body.raycastTarget=false;
            UiKit.TopLeft(body.rectTransform,new Vector2(8,-4),new Vector2(W-350,0));
            UiKit.CloseButton(pr, Close);
            newerButton = Button("NewerRelease", pr, "Newer", new Vector2(30,-132), new Vector2(390,72), () => SelectEntry(Mathf.Max(0,selected-1)));
            olderButton = Button("OlderRelease", pr, "Older", new Vector2(450,-132), new Vector2(420,72), () => SelectEntry(Mathf.Min(entries.Length-1,selected+1)));
            newerButton.SetActive(false);
            olderButton.SetActive(false);
        }

        // A list area under the tabs, above the Resume button, as tall as the panel allows.
        private static void PlaceList(RectTransform rt, float x, float width)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(x, 94);
            rt.offsetMax = new Vector2(x + width, -126);
        }

        private void ShowSettings()
        {
            showingHistory = false;
            title.text = "SETTINGS";
            newerButton.SetActive(false);
            olderButton.SetActive(false);
            viewport.gameObject.SetActive(true);
            ClearEntries();
            SelectTab(true);
            notesViewport.gameObject.SetActive(false);
            PlaceList(viewport, 30, W-60);
            if (TouchMode.Active) { viewport.offsetMin = new Vector2(30,TouchMode.Portrait ? 132 : 100); viewport.offsetMax = new Vector2(W-30,TouchMode.Portrait ? -200 : -158); }
            float y = 0f;
            RectTransform volumeRow = SettingsRow("Audio", "Volume", "Overall volume of all game sounds.", ref y);
            VolumeSlider(volumeRow, Audio.AudioManager.MasterVolume, v => Audio.AudioManager.MasterVolume = v);
            RectTransform musicRow = SettingsRow(null, "Music", "Menu, area and boss fight music.", ref y);
            VolumeSlider(musicRow, Audio.AudioManager.MusicVolume, v => Audio.AudioManager.MusicVolume = v);
            RectTransform effectsRow = SettingsRow(null, "Sound Effects", "Combat, world, ambience and interface sounds.", ref y);
            VolumeSlider(effectsRow, Audio.AudioManager.EffectsVolume, v => Audio.AudioManager.EffectsVolume = v);
            RectTransform chatRow = SettingsRow("Communication", "Chat", "Show chat messages and chat controls.", ref y);
            Text chatLabel = null;
            GameObject chatButton = Button("ChatToggle", chatRow, ChatUI.Enabled ? "On" : "Off",
                new Vector2(W-294, -15), new Vector2(216, 48), () =>
                {
                    ChatUI.SetEnabled(!ChatUI.Enabled);
                    chatLabel.text = ChatUI.Enabled ? "On" : "Off";
                });
            chatLabel = chatButton.GetComponentInChildren<Text>();
            if (TouchMode.Active)
            {
                RectTransform tooltipRow = SettingsRow("Interface", "Automatic ground tooltips", "Preview nearby ground items automatically.", ref y);
                Text tooltipLabel = null;
                GameObject tooltipButton = Button("AutomaticGroundTooltips", tooltipRow, LootPicker.AutomaticTooltips ? "On" : "Off",
                    new Vector2(W-294, -15), new Vector2(216, 48), () =>
                    {
                        LootPicker.AutomaticTooltips = !LootPicker.AutomaticTooltips;
                        tooltipLabel.text = LootPicker.AutomaticTooltips ? "On" : "Off";
                    });
                tooltipLabel = tooltipButton.GetComponentInChildren<Text>();
            }
            if (!TouchMode.Active)
            {
                RectTransform dashRow = SettingsRow("Controls", "Dash direction", "Choose how to aim Dash on desktop.", ref y);
                Text dashLabel = null;
                GameObject dashButton = Button("DashDirection", dashRow, PlayerSkills.DashTowardsCursor ? "Cursor" : "Movement",
                    new Vector2(W-294, -15), new Vector2(216, 48), () =>
                    {
                        PlayerSkills.DashTowardsCursor = !PlayerSkills.DashTowardsCursor;
                        dashLabel.text = PlayerSkills.DashTowardsCursor ? "Cursor" : "Movement";
                    });
                dashLabel = dashButton.GetComponentInChildren<Text>();
                KeyList(ref y);
            }
            var session = GameSessionController.Instance;
            if (session != null && session.Role == SessionRole.Player && session.PlayGranted)
            {
                RectTransform menuRow = SettingsRow("Session", "Main menu", "Save your character and go back to the start menu.", ref y);
                Button("MainMenu", menuRow, "Exit to Main Menu", new Vector2(W-334, -15),
                    new Vector2(256, 48), () => session.ReturnToMainMenu());
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, y-16));
            listScroll.StopMovement();
            listScroll.verticalNormalizedPosition = 1f;
        }

        // A null section continues the one above without a heading of its own.
        private RectTransform SettingsRow(string section, string label, string description, ref float y)
        {
            if (section != null)
            {
                Text heading = UiKit.NewText(section + "Heading", content, section.ToUpperInvariant(), 16, UiKit.Gold, TextAnchor.MiddleLeft);
                heading.font = UiKit.TitleFont;
                heading.fontSize = TouchMode.Active ? 32 : 16;
                UiKit.TopLeft(heading.rectTransform, new Vector2(0, -y), new Vector2(W-60, TouchMode.Active ? 48 : 22));
                y += TouchMode.Active ? 60f : 30f;
            }

            Image row = UiKit.NewImage(label + "Row", content, new Color(.2f, .17f, .13f, 1f));
            UiKit.Inset(row);
            UiKit.TopLeft(row.rectTransform, new Vector2(0, -y), new Vector2(W-60, 78));
            Text name = UiKit.NewText("Name", row.transform, label, 20, UiKit.TextColor, TextAnchor.MiddleLeft);
            UiKit.TopLeft(name.rectTransform, new Vector2(18, -10), new Vector2(420, 26));
            Text detail = UiKit.NewText("Description", row.transform, description, 16, UiKit.DimText, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.TopLeft(detail.rectTransform, new Vector2(18, -40), new Vector2(420, 32));
            if (TouchMode.Active)
            {
                row.rectTransform.sizeDelta = new Vector2(W-60, 230f);
                name.fontSize = 40;
                detail.fontSize = TouchMode.Portrait ? 36 : 28;
                name.rectTransform.sizeDelta = new Vector2(W-100, 54f);
                UiKit.TopLeft(detail.rectTransform, new Vector2(18,-72), new Vector2(W-100,78f));
            }
            y += TouchMode.Active ? 246f : 94f;
            return row.rectTransform;
        }

        private static readonly string[,] Keys =
        {
            { "W A S D", "Move" },
            { "Shift", "Sprint" },
            { "Left click", "Attack, pick up, talk" },
            { "Q E R F  1 2 3 4", "Skills and potions" },
            { "RMB  M4  M5", "More skill buttons" },
            { "B", "Town portal" },
            { "I", "Inventory" },
            { "C", "Character" },
            { "P", "Passive tree" },
            { "M", "Show or hide the map" },
            { "Enter", "Chat" },
            { "Esc", "This menu" },
        };

        // The desktop keys, two to a line, under the Controls settings.
        private void KeyList(ref float y)
        {
            const float lineHeight = 30f;
            int lines = (Keys.GetLength(0) + 1) / 2;
            Image box = UiKit.NewImage("KeysRow", content, new Color(.2f, .17f, .13f, 1f));
            UiKit.Inset(box);
            float height = 54f + lines * lineHeight + 10f;
            UiKit.TopLeft(box.rectTransform, new Vector2(0, -y), new Vector2(W - 60, height));
            Text name = UiKit.NewText("Name", box.transform, "Keys", 20, UiKit.TextColor, TextAnchor.MiddleLeft);
            UiKit.TopLeft(name.rectTransform, new Vector2(18, -10), new Vector2(420, 26));
            Text hint = UiKit.NewText("Hint", box.transform, "Click a square on the skill bar to choose its skill or potion.",
                16, UiKit.DimText, TextAnchor.MiddleLeft);
            UiKit.TopLeft(hint.rectTransform, new Vector2(18, -34), new Vector2(W - 100, 22));
            float column = (W - 60) * 0.5f;
            for (int k = 0; k < Keys.GetLength(0); k++)
            {
                float x = 18 + (k % 2) * column;
                float top = -60 - (k / 2) * lineHeight;
                Text key = UiKit.NewText("Key", box.transform, Keys[k, 0], 15, UiKit.Gold, TextAnchor.MiddleLeft);
                key.font = UiKit.TitleFont;
                UiKit.TopLeft(key.rectTransform, new Vector2(x, top), new Vector2(150, lineHeight));
                Text action = UiKit.NewText("Action", box.transform, Keys[k, 1], 17, UiKit.TextColor, TextAnchor.MiddleLeft);
                UiKit.TopLeft(action.rectTransform, new Vector2(x + 156, top), new Vector2(column - 180, lineHeight));
            }
            y += height + 16f;
        }

        private void VolumeSlider(RectTransform row, float current, System.Action<float> apply)
        {
            Image track = UiKit.NewImage("VolumeTrack", row, new Color(.3f, .26f, .2f, 1f));
            UiKit.Inset(track);
            UiKit.TopLeft(track.rectTransform, new Vector2(W-374, -33), new Vector2(220, 12));
            track.raycastTarget = true;
            RectTransform fillArea = UiKit.NewRect("FillArea", track.transform);
            UiKit.Stretch(fillArea, 0f);
            Image fill = UiKit.NewImage("Fill", fillArea, new Color(.78f, .45f, .16f, 1f));
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = UiKit.NewRect("HandleArea", track.transform);
            UiKit.Stretch(handleArea, 0f);
            Image handle = UiKit.NewImage("Handle", handleArea, new Color(1f, .86f, .55f, 1f));
            handle.sprite = UiKit.Diamond;
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(20, 18); // Added to the track height by the slider anchors.
            UiKit.AddOutline(handle, new Color(0f, 0f, 0f, .8f), 1f);

            Text value = UiKit.NewText("VolumeValue", row, "", 18, UiKit.TextColor, TextAnchor.MiddleRight);
            UiKit.TopLeft(value.rectTransform, new Vector2(W-144, -24), new Vector2(66, 30));

            if (TouchMode.Active)
            {
                UiKit.TopLeft(track.rectTransform, new Vector2(42,-186), new Vector2(W-240,20));
                handle.rectTransform.sizeDelta = new Vector2(46,38);
                value.fontSize = 32;
                UiKit.TopLeft(value.rectTransform, new Vector2(W-184,-164), new Vector2(100,58));
            }
            Slider slider = track.gameObject.AddComponent<ScrollFriendlySlider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = current;
            value.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            slider.onValueChanged.AddListener(v =>
            {
                apply(v);
                value.text = Mathf.RoundToInt(v * 100f) + "%";
            });
        }

        private void SelectTab(bool settings)
        {
            SetTab(settingsButton, settings);
            SetTab(historyButton, !settings);
        }

        private static void SetTab(GameObject tab, bool active)
        {
            tab.GetComponent<UnityEngine.UI.Button>().interactable = !active;
            tab.GetComponent<Image>().color = active ? Color.white : UiKit.MutedTint * .8f;
            tab.GetComponentInChildren<Text>().color = active ? UiKit.Gold : UiKit.TextColor;
        }
        private void ShowHistory()
        {
            var combined = new System.Collections.Generic.List<Entry>();
            AddReleaseText(combined, Resources.Load<TextAsset>("PatchNotes")?.text);
            AddArchive(combined, Resources.Load<TextAsset>("PatchNotesHistory")?.text);
            var bundledOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < combined.Count; i++) bundledOrder[combined[i].version] = i;
            AddArchive(combined, PlayerPrefs.GetString("PoeClone.PatchNotesArchive", ""));
            // Correct previously saved entries that contained the entire multi-release file.
            AddReleaseText(combined, Resources.Load<TextAsset>("PatchNotes")?.text);
            var mergedOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < combined.Count; i++) mergedOrder[combined[i].version] = i;
            combined.Sort((a, b) => CompareReleases(a, b, bundledOrder, mergedOrder));
            entries = combined.ToArray();
            showingHistory = true;
            title.text="PATCH HISTORY"; ClearEntries(); SelectTab(false); notesViewport.gameObject.SetActive(true);
            body.transform.SetParent(notesContent,false); UiKit.TopLeft(body.rectTransform,new Vector2(8,-4),new Vector2(W-350,0));
            PlaceList(viewport,30,250);
            float y=-4;
            if(entries.Length==0) body.text="No patch history is available.";
            for (int i = 0; i < entries.Length; i++)
            {
                int index = i;
                string label = entries[i].version;
                if (TryReleaseDate(label, out _) && label.Length > 11 && label[10] == '-')
                    label = label.Substring(0, 10) + "\n" + label.Substring(11).Replace('-', ' ');
                GameObject row = Button("Release" + i, content, label, new Vector2(0, y), new Vector2(250, 38), () => SelectEntry(index));
                Text rowLabel = row.GetComponentInChildren<Text>();
                rowLabel.supportRichText = false;
                rowLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiKit.Stretch(rowLabel.rectTransform, 8f);
                float height = Mathf.Max(48f, rowLabel.preferredHeight + 16f);
                ((RectTransform)row.transform).sizeDelta = new Vector2(250f, height);
                y -= height + 6f;
            }
            content.sizeDelta=new Vector2(0,Mathf.Max(100,-y+10));
            if(entries.Length>0)
            {
                selected=0;
                SelectEntry(selected);
            }
            listScroll.verticalNormalizedPosition=1;
            if (TouchMode.Active)
            {
                viewport.gameObject.SetActive(false);
                newerButton.SetActive(true);
                olderButton.SetActive(true);
                notesViewport.anchorMin = Vector2.zero;
                notesViewport.anchorMax = Vector2.one;
                notesViewport.offsetMin = new Vector2(30,TouchMode.Portrait ? 132 : 100);
                notesViewport.offsetMax = new Vector2(-30,TouchMode.Portrait ? -294 : -238);
                body.fontSize = TouchMode.ReadingFontSize;
                UiKit.TopLeft(body.rectTransform, new Vector2(8,-4), new Vector2(W-76,0));
                SelectEntry(selected);
            }
        }
        private static int CompareReleases(Entry a, Entry b, Dictionary<string, int> bundledOrder, Dictionary<string, int> mergedOrder)
        {
            bool aDated = TryReleaseDate(a.version, out DateTime aDate);
            bool bDated = TryReleaseDate(b.version, out DateTime bDate);
            if (aDated && bDated && aDate != bDate) return bDate.CompareTo(aDate);

            bool aBundled = bundledOrder.TryGetValue(a.version, out int aIndex);
            bool bBundled = bundledOrder.TryGetValue(b.version, out int bIndex);
            if (aBundled && bBundled) return aIndex.CompareTo(bIndex);
            if (aBundled != bBundled) return aBundled ? 1 : -1;
            // Device-only releases were appended as they were seen, oldest first.
            return mergedOrder[b.version].CompareTo(mergedOrder[a.version]);
        }
        private static bool TryReleaseDate(string version, out DateTime date)
        {
            date = default;
            return version != null && version.Length >= 10 &&
                DateTime.TryParseExact(version.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date);
        }
        private void SelectEntry(int index)
        {
            selected=Mathf.Clamp(index,0,entries.Length-1); if(entries.Length==0)return;
            body.text="<b>"+Escape(entries[selected].version)+"</b>\n\n"+entries[selected].notes;
            Canvas.ForceUpdateCanvases();
            float textHeight=body.preferredHeight;
            body.rectTransform.sizeDelta=new Vector2(body.rectTransform.sizeDelta.x,textHeight);
            notesContent.sizeDelta=new Vector2(0,Mathf.Max(notesViewport.rect.height,textHeight+12));
            notesScroll.verticalNormalizedPosition=1;
            if (newerButton != null)
            {
                SetHistoryButtonEnabled(newerButton, selected > 0);
                SetHistoryButtonEnabled(olderButton, selected + 1 < entries.Length);
            }
        }
        private static void SetHistoryButtonEnabled(GameObject root, bool enabled)
        {
            root.GetComponent<UnityEngine.UI.Button>().interactable = enabled;
            root.GetComponent<Image>().color = enabled ? Color.white : new Color(0.38f, 0.38f, 0.38f, 1f);
            root.GetComponentInChildren<Text>().color = enabled ? UiKit.TextColor : new Color(0.42f, 0.42f, 0.42f, 1f);
        }

        private void ClearEntries()
        {
            body.transform.SetParent(notesContent,false);
            for(int i=content.childCount-1;i>=0;i--) Destroy(content.GetChild(i).gameObject);
            body.text="";
        }
        private GameObject Button(string name,Transform parent,string label,Vector2 pos,Vector2 size,Action action)
        {
            Image image=UiKit.NewImage(name,parent,Color.white); image.raycastTarget=true; RectTransform r=image.rectTransform;
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=pos;r.sizeDelta=size;
            Text t=UiKit.NewText("Label",r,label,18,UiKit.TextColor,TextAnchor.MiddleCenter);UiKit.Stretch(t.rectTransform,0);
            UnityEngine.UI.Button b=image.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());
            UiKit.StyleButton(image,t);
            if (TouchMode.Active)
            {
                t.fontSize = 32;
                if (parent.name.EndsWith("Row"))
                    UiKit.TopLeft(r, new Vector2(18,-152), new Vector2(W-96,64));
            }
            return image.gameObject;
        }
        private void Layout()
        {
            layoutSize = new Vector2(Screen.width, Screen.height);
            panelRect.GetComponent<Image>().color = UiKit.ReadablePanelColor;
            panelRect.sizeDelta = new Vector2(W,-Margin*2);
            var divider = (RectTransform)panelRect.Find("Divider");
            divider.anchoredPosition = new Vector2(0f,TouchMode.Active ? (TouchMode.Portrait ? -188f : -150f) : -116f);
            title.fontSize = TouchMode.Active ? (TouchMode.Portrait ? 44 : 36) : 30;
            title.rectTransform.sizeDelta = new Vector2(W-160,TouchMode.Active ? 66 : 40);
            if (!TouchMode.Active) return;
            UiKit.TopLeft((RectTransform)settingsButton.transform,new Vector2(30,-94),new Vector2(360,86));
            UiKit.TopLeft((RectTransform)historyButton.transform,new Vector2(410,-94),new Vector2(360,86));
            resumeRect.anchoredPosition = new Vector2(W-310,24);
            resumeRect.sizeDelta = new Vector2(280,86);
            foreach (GameObject button in new[] {settingsButton,historyButton,resumeRect.gameObject,newerButton,olderButton})
                button.GetComponentInChildren<Text>().fontSize = 32;
            UiKit.TopLeft((RectTransform)newerButton.transform,new Vector2(30,-202),new Vector2(400,72));
            UiKit.TopLeft((RectTransform)olderButton.transform,new Vector2(450,-202),new Vector2(420,72));
            if (!TouchMode.Portrait)
            {
                UiKit.TopLeft((RectTransform)settingsButton.transform,new Vector2(30,-76),new Vector2(360,66));
                UiKit.TopLeft((RectTransform)historyButton.transform,new Vector2(410,-76),new Vector2(360,66));
                UiKit.TopLeft((RectTransform)newerButton.transform,new Vector2(30,-158),new Vector2(400,64));
                UiKit.TopLeft((RectTransform)olderButton.transform,new Vector2(450,-158),new Vector2(420,64));
                resumeRect.sizeDelta = new Vector2(280,64);
            }
        }

        private static string Escape(string s) => (s??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");

        /// <summary>
        /// A slider in the scrolling settings list. A plain Slider takes every drag that starts on
        /// it, so on a phone a finger landing on one of the volume sliders could not scroll the
        /// list. Here a mostly vertical drag scrolls the list instead; a tap or a sideways drag
        /// sets the value.
        /// </summary>
        private sealed class ScrollFriendlySlider : Slider, IBeginDragHandler, IEndDragHandler
        {
            private ScrollRect scroll;
            private bool dragged, scrolling;

            private ScrollRect Scroll => scroll != null ? scroll : scroll = GetComponentInParent<ScrollRect>();

            public override void OnInitializePotentialDrag(PointerEventData eventData)
            {
                // Keep the drag threshold, so the direction is known before anything moves.
                eventData.useDragThreshold = true;
                if (Scroll != null) Scroll.OnInitializePotentialDrag(eventData);
            }

            // The value waits until it's clear whether this is a tap, a sideways drag or a scroll.
            public override void OnPointerDown(PointerEventData eventData)
            {
                dragged = scrolling = false;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                dragged = true;
                Vector2 moved = eventData.position - eventData.pressPosition;
                scrolling = Scroll != null && Mathf.Abs(moved.y) > Mathf.Abs(moved.x);
                if (scrolling) Scroll.OnBeginDrag(eventData);
                else base.OnPointerDown(eventData);
            }

            public override void OnDrag(PointerEventData eventData)
            {
                if (scrolling) Scroll.OnDrag(eventData);
                else base.OnDrag(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (scrolling) Scroll.OnEndDrag(eventData);
                scrolling = false;
            }

            public override void OnPointerUp(PointerEventData eventData)
            {
                if (!dragged) base.OnPointerDown(eventData);
                base.OnPointerUp(eventData);
            }
        }
    }
}
