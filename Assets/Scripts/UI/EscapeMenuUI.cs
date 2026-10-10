using System;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
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
        private const float W = 760f, H = 620f;
        private GameObject root;
        private RectTransform viewport, content, notesViewport, notesContent;
        private ScrollRect listScroll, notesScroll;
        private GameObject settingsButton, historyButton;
        private Text title, body;
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
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || UiKit.IsTypingInTextField()) return;
            if (PatchNotesUI.IsShowing) return;
            if (root.activeSelf) { Close(); return; }
            if (GetComponent<NamePromptUI>()?.IsShowing == true) return;
            if (DialogueUI.IsOpen || PassiveTreeUI.IsOpen || SkillBarUI.IsOpen || SkillBarUI.PickerOpen ||
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
            ShowSettings();
            root.SetActive(true);
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
            if (!pausedByMenu) return;
            pausedByMenu = false;
            GameSessionController session = GameSessionController.Instance;
            if (session != null && session.Role == SessionRole.Player && !session.PlayGranted) return;
            Time.timeScale = timeScaleBeforeMenu;
            AudioListener.pause = audioPausedBeforeMenu;
        }

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("EscapeMenuCanvas", transform, 960, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>(); group.interactable = true; group.blocksRaycasts = true; root = canvas.gameObject;
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f,0f,0f,0.6f)); shade.raycastTarget = true; UiKit.Stretch(shade.rectTransform, 0f);
            Image panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor); UiKit.Grain(panel); panel.raycastTarget = true;
            RectTransform pr=panel.rectTransform; pr.anchorMin=pr.anchorMax=new Vector2(.5f,.5f); pr.sizeDelta=new Vector2(W,H); UiKit.Frame(pr); pr.gameObject.AddComponent<UiAppear>(); TouchMode.AddBlocker(pr);
            title=UiKit.Heading(UiKit.NewText("Title",pr,"MENU",30,UiKit.Gold,TextAnchor.MiddleLeft)); UiKit.TopLeft(title.rectTransform,new Vector2(30,-16),new Vector2(W-100,40));
            settingsButton=Button("Settings",pr,"Settings",new Vector2(30,-68),new Vector2(180,42),ShowSettings);
            historyButton=Button("History",pr,"Patch History",new Vector2(222,-68),new Vector2(180,42),ShowHistory);
            Button("Resume",pr,"Resume",new Vector2(W-210,-H+66),new Vector2(180,42),Close);
            UiKit.DividerLine(pr,new Vector2(0,H*.5f-116),W-60);
            viewport=UiKit.NewRect("Viewport",pr); viewport.gameObject.AddComponent<RectMask2D>(); Image catcher=viewport.gameObject.AddComponent<Image>(); catcher.color=Color.clear;
            UiKit.TopLeft(viewport,new Vector2(30,-126),new Vector2(W-60,H-220));
            content=UiKit.NewRect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero;
            listScroll=viewport.gameObject.AddComponent<ScrollRect>(); listScroll.content=content; listScroll.viewport=viewport; listScroll.horizontal=false; listScroll.movementType=ScrollRect.MovementType.Clamped; listScroll.scrollSensitivity=30;
            notesViewport=UiKit.NewRect("NotesViewport",pr); notesViewport.gameObject.AddComponent<RectMask2D>(); Image notesCatcher=notesViewport.gameObject.AddComponent<Image>(); notesCatcher.color=Color.clear;
            UiKit.TopLeft(notesViewport,new Vector2(300,-126),new Vector2(W-330,H-220));
            notesContent=UiKit.NewRect("NotesContent",notesViewport); notesContent.anchorMin=new Vector2(0,1); notesContent.anchorMax=new Vector2(1,1); notesContent.pivot=new Vector2(.5f,1); notesContent.anchoredPosition=Vector2.zero;
            notesScroll=notesViewport.gameObject.AddComponent<ScrollRect>(); notesScroll.content=notesContent; notesScroll.viewport=notesViewport; notesScroll.horizontal=false; notesScroll.movementType=ScrollRect.MovementType.Clamped; notesScroll.scrollSensitivity=30;
            body=UiKit.NewText("Body",notesContent,"",18,UiKit.TextColor,TextAnchor.UpperLeft); body.horizontalOverflow=HorizontalWrapMode.Wrap; body.verticalOverflow=VerticalWrapMode.Overflow; body.raycastTarget=false;
            UiKit.TopLeft(body.rectTransform,new Vector2(8,-4),new Vector2(W-350,0));
            UiKit.CloseButton(pr, Close);
        }

        private void ShowSettings()
        {
            title.text = "SETTINGS";
            ClearEntries();
            SelectTab(true);
            notesViewport.gameObject.SetActive(false);
            UiKit.TopLeft(viewport, new Vector2(30, -126), new Vector2(W-60, H-220));
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
            }
            var session = GameSessionController.Instance;
            if (session != null && session.Role == SessionRole.Player && session.PlayGranted)
            {
                RectTransform characterRow = SettingsRow("Session", "Characters", "Return to character selection.", ref y);
                Button("Characters", characterRow, "Switch Character", new Vector2(W-294, -15),
                    new Vector2(216, 48), () => session.ReturnToCharacters());
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
                UiKit.TopLeft(heading.rectTransform, new Vector2(0, -y), new Vector2(W-60, 22));
                y += 30f;
            }

            Image row = UiKit.NewImage(label + "Row", content, new Color(.2f, .17f, .13f, 1f));
            UiKit.Inset(row);
            UiKit.TopLeft(row.rectTransform, new Vector2(0, -y), new Vector2(W-60, 78));
            Text name = UiKit.NewText("Name", row.transform, label, 20, UiKit.TextColor, TextAnchor.MiddleLeft);
            UiKit.TopLeft(name.rectTransform, new Vector2(18, -10), new Vector2(420, 26));
            Text detail = UiKit.NewText("Description", row.transform, description, 16, UiKit.DimText, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.TopLeft(detail.rectTransform, new Vector2(18, -40), new Vector2(420, 32));
            y += 94f;
            return row.rectTransform;
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

            Slider slider = track.gameObject.AddComponent<Slider>();
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
            title.text="PATCH HISTORY"; ClearEntries(); SelectTab(false); notesViewport.gameObject.SetActive(true);
            body.transform.SetParent(notesContent,false); UiKit.TopLeft(body.rectTransform,new Vector2(8,-4),new Vector2(W-350,0));
            UiKit.TopLeft(viewport,new Vector2(30,-126),new Vector2(250,H-220));
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
            return image.gameObject;
        }
        private static string Escape(string s) => (s??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
    }
}
