using System;
using UnityEngine;
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
        private GameObject settingsButton;
        private Text title, body;
        private Entry[] entries = Array.Empty<Entry>();
        private int selected;

        public bool IsOpen => root != null && root.activeSelf;

        public static void StoreRelease(string version, string notes)
        {
            var merged = new System.Collections.Generic.List<Entry>();
            AddArchive(merged, Resources.Load<TextAsset>("PatchNotesHistory")?.text);
            AddArchive(merged, PlayerPrefs.GetString("PoeClone.PatchNotesArchive", ""));
            AddEntry(merged, new Entry { version = version, notes = notes });
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
                foreach (Entry entry in archive.entries) AddEntry(into, entry);
            }
            catch { }
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
            if (root.activeSelf) { root.SetActive(false); return; }
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
            if (PatchNotesUI.IsShowing) return;
            ShowSettings();
            root.SetActive(true);
        }

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("EscapeMenuCanvas", transform, 960, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>(); group.interactable = true; group.blocksRaycasts = true; root = canvas.gameObject;
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f,0f,0f,0.6f)); shade.raycastTarget = true; UiKit.Stretch(shade.rectTransform, 0f);
            Image panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor); UiKit.Grain(panel); panel.raycastTarget = true;
            RectTransform pr=panel.rectTransform; pr.anchorMin=pr.anchorMax=new Vector2(.5f,.5f); pr.sizeDelta=new Vector2(W,H); UiKit.AddOutline(panel,UiKit.BorderColor,3f); TouchMode.AddBlocker(pr);
            title=UiKit.NewText("Title",pr,"MENU",28,UiKit.Gold,TextAnchor.UpperCenter); UiKit.TopLeft(title.rectTransform,new Vector2(0,-16),new Vector2(W,40));
            settingsButton=Button("Settings",pr,"Settings",new Vector2(30,-68),new Vector2(150,42),ShowSettings);
            Button("History",pr,"Patch History",new Vector2(190,-68),new Vector2(180,42),ShowHistory);
            viewport=UiKit.NewRect("Viewport",pr); viewport.gameObject.AddComponent<RectMask2D>(); Image catcher=viewport.gameObject.AddComponent<Image>(); catcher.color=Color.clear;
            UiKit.TopLeft(viewport,new Vector2(30,-126),new Vector2(W-60,H-152));
            content=UiKit.NewRect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero;
            listScroll=viewport.gameObject.AddComponent<ScrollRect>(); listScroll.content=content; listScroll.viewport=viewport; listScroll.horizontal=false; listScroll.movementType=ScrollRect.MovementType.Clamped; listScroll.scrollSensitivity=30;
            notesViewport=UiKit.NewRect("NotesViewport",pr); notesViewport.gameObject.AddComponent<RectMask2D>(); Image notesCatcher=notesViewport.gameObject.AddComponent<Image>(); notesCatcher.color=Color.clear;
            UiKit.TopLeft(notesViewport,new Vector2(300,-126),new Vector2(W-330,H-152));
            notesContent=UiKit.NewRect("NotesContent",notesViewport); notesContent.anchorMin=new Vector2(0,1); notesContent.anchorMax=new Vector2(1,1); notesContent.pivot=new Vector2(.5f,1); notesContent.anchoredPosition=Vector2.zero;
            notesScroll=notesViewport.gameObject.AddComponent<ScrollRect>(); notesScroll.content=notesContent; notesScroll.viewport=notesViewport; notesScroll.horizontal=false; notesScroll.movementType=ScrollRect.MovementType.Clamped; notesScroll.scrollSensitivity=30;
            body=UiKit.NewText("Body",notesContent,"",18,UiKit.TextColor,TextAnchor.UpperLeft); body.horizontalOverflow=HorizontalWrapMode.Wrap; body.verticalOverflow=VerticalWrapMode.Overflow; body.raycastTarget=false;
            UiKit.TopLeft(body.rectTransform,new Vector2(8,-4),new Vector2(W-350,0));
            Button("Close",pr,"Close",new Vector2(W-150,-68),new Vector2(120,42),()=>root.SetActive(false));
        }

        private void ShowSettings()
        {
            title.text="SETTINGS"; ClearEntries(); settingsButton.SetActive(false); notesViewport.gameObject.SetActive(false);
            body.transform.SetParent(content,false); UiKit.TopLeft(body.rectTransform,new Vector2(0,-4),new Vector2(W-80,0));
            UiKit.TopLeft(viewport,new Vector2(30,-126),new Vector2(W-60,H-152));
            body.text="Chat\n\nShow chat messages and chat controls";
            Button("ChatToggle",content,"Chat: "+(ChatUI.Enabled?"ON":"OFF"),new Vector2(0,-100),new Vector2(220,48),()=>{ChatUI.SetEnabled(!ChatUI.Enabled);ShowSettings();});
            if (!TouchMode.Active)
            {
                Button("DashDirection",content,"Dash: "+(PlayerSkills.DashTowardsCursor ? "Cursor" : "Movement"),
                    new Vector2(0,-170),new Vector2(240,48),()=>{PlayerSkills.DashTowardsCursor=!PlayerSkills.DashTowardsCursor;ShowSettings();});
                ResizeBody(250);
            }
            else ResizeBody(180);
            var session = GameSessionController.Instance;
            if (session != null && session.Role == SessionRole.Player && session.PlayGranted)
            {
                Button("Characters",content,"Character Selection",new Vector2(0,-240),new Vector2(240,48),()=>session.ReturnToCharacters());
                ResizeBody(330);
            }
        }
        private void ShowHistory()
        {
            var combined = new System.Collections.Generic.List<Entry>();
            AddArchive(combined, Resources.Load<TextAsset>("PatchNotesHistory")?.text);
            AddArchive(combined, PlayerPrefs.GetString("PoeClone.PatchNotesArchive", ""));
            entries = combined.ToArray();
            title.text="PATCH HISTORY"; ClearEntries(); settingsButton.SetActive(true); notesViewport.gameObject.SetActive(true);
            body.transform.SetParent(notesContent,false); UiKit.TopLeft(body.rectTransform,new Vector2(8,-4),new Vector2(W-350,0));
            UiKit.TopLeft(viewport,new Vector2(30,-126),new Vector2(250,H-152));
            float y=-4;
            if(entries.Length==0) body.text="No patch history is available.";
            for(int i=0;i<entries.Length;i++) { int index=i; Button("Release"+i,content,entries[i].version,new Vector2(0,y),new Vector2(250,38),()=>SelectEntry(index)); y-=44; }
            content.sizeDelta=new Vector2(0,Mathf.Max(100,-y+10));
            if(entries.Length>0)
            {
                selected=entries.Length-1;
                SelectEntry(selected);
            }
            listScroll.verticalNormalizedPosition=0;
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
        private void ResizeBody(float height)
        {
            content.sizeDelta=new Vector2(0,height);
        }
        private GameObject Button(string name,Transform parent,string label,Vector2 pos,Vector2 size,Action action)
        {
            Image image=UiKit.NewImage(name,parent,new Color(.25f,.19f,.11f,1)); image.raycastTarget=true; RectTransform r=image.rectTransform;
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=pos;r.sizeDelta=size;UiKit.AddOutline(image,UiKit.Gold,1.5f);
            Text t=UiKit.NewText("Label",r,label,18,UiKit.TextColor,TextAnchor.MiddleCenter);UiKit.Stretch(t.rectTransform,0);
            UnityEngine.UI.Button b=image.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());
            return image.gameObject;
        }
        private static string Escape(string s) => (s??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
    }
}
