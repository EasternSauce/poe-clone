using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Player;

namespace PoeClone.UI
{
    /// <summary>ESC settings and a browsable, bundled archive of every recovered patch note release.</summary>
    public class EscapeMenuUI : MonoBehaviour
    {
        [Serializable] private class Entry { public string version; public string notes; }
        [Serializable] private class Archive { public Entry[] entries; }
        private const float W = 760f, H = 620f;
        private GameObject root;
        private RectTransform viewport, content;
        private Text title, body;
        private Entry[] entries = Array.Empty<Entry>();
        private int selected;

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
            root.SetActive(!root.activeSelf);
            if (root.activeSelf) ShowSettings();
        }

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("EscapeMenuCanvas", transform, 960, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>(); group.interactable = true; group.blocksRaycasts = true; root = canvas.gameObject;
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f,0f,0f,0.6f)); shade.raycastTarget = true; UiKit.Stretch(shade.rectTransform, 0f);
            Image panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor); UiKit.Grain(panel); panel.raycastTarget = true;
            RectTransform pr=panel.rectTransform; pr.anchorMin=pr.anchorMax=new Vector2(.5f,.5f); pr.sizeDelta=new Vector2(W,H); UiKit.AddOutline(panel,UiKit.BorderColor,3f); TouchMode.AddBlocker(pr);
            title=UiKit.NewText("Title",pr,"MENU",28,UiKit.Gold,TextAnchor.UpperCenter); UiKit.TopLeft(title.rectTransform,new Vector2(0,-16),new Vector2(W,40));
            Button("Settings",pr,"Settings",new Vector2(30,-68),new Vector2(150,42),ShowSettings);
            Button("History",pr,"Patch History",new Vector2(190,-68),new Vector2(180,42),ShowHistory);
            Button("Close",pr,"Close",new Vector2(-150,18),new Vector2(120,42),()=>root.SetActive(false),true);
            viewport=UiKit.NewRect("Viewport",pr); viewport.gameObject.AddComponent<RectMask2D>(); Image catcher=viewport.gameObject.AddComponent<Image>(); catcher.color=Color.clear;
            UiKit.TopLeft(viewport,new Vector2(30,-126),new Vector2(W-60,H-152));
            content=UiKit.NewRect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero;
            ScrollRect scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.content=content; scroll.viewport=viewport; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=30;
            body=UiKit.NewText("Body",content,"",18,UiKit.TextColor,TextAnchor.UpperLeft); body.horizontalOverflow=HorizontalWrapMode.Wrap; body.verticalOverflow=VerticalWrapMode.Overflow; UiKit.Stretch(body.rectTransform,0);
            body.raycastTarget=false;
        }

        private void ShowSettings()
        {
            title.text="SETTINGS"; ClearEntries();
            body.text="Chat\n\nShow chat messages and chat controls";
            Button("ChatToggle",content,"Chat: "+(ChatUI.Enabled?"ON":"OFF"),new Vector2(0,-100),new Vector2(220,48),()=>{ChatUI.SetEnabled(!ChatUI.Enabled);ShowSettings();});
            Button("HealthPotionKey",content,"Health potion: "+PlayerPotions.HealthPotionKeyLabel,new Vector2(0,-164),new Vector2(300,48),()=>{PlayerPotions.CyclePotionKey(true);ShowSettings();});
            Button("ManaPotionKey",content,"Mana potion: "+PlayerPotions.ManaPotionKeyLabel,new Vector2(0,-222),new Vector2(300,48),()=>{PlayerPotions.CyclePotionKey(false);ShowSettings();});
            Button("SettingsHint",content,"Press ESC to close this menu",new Vector2(0,-286),new Vector2(300,42),()=>root.SetActive(false));
            ResizeBody(360);
        }
        private void ShowHistory()
        {
            var combined = new System.Collections.Generic.List<Entry>();
            AddArchive(combined, Resources.Load<TextAsset>("PatchNotesHistory")?.text);
            AddArchive(combined, PlayerPrefs.GetString("PoeClone.PatchNotesArchive", ""));
            entries = combined.ToArray();
            title.text="PATCH HISTORY"; ClearEntries();
            float y=-4;
            if(entries.Length==0) body.text="No patch history is available.";
            for(int i=0;i<entries.Length;i++) { int index=i; Button("Release"+i,content,entries[i].version,new Vector2(0,y),new Vector2(250,38),()=>SelectEntry(index)); y-=44; }
            if(entries.Length>0) SelectEntry(selected);
            ResizeBody(Mathf.Max(100,-y+10));
        }
        private void SelectEntry(int index)
        {
            selected=Mathf.Clamp(index,0,entries.Length-1); if(entries.Length==0)return;
            body.text="<b>"+Escape(entries[selected].version)+"</b>\n\n"+entries[selected].notes;
            body.rectTransform.anchorMin=body.rectTransform.anchorMax=new Vector2(0,1); body.rectTransform.pivot=new Vector2(0,1);
            body.rectTransform.anchoredPosition=new Vector2(270,-4); body.rectTransform.sizeDelta=new Vector2(370,0);
            ResizeBody(Mathf.Max(520,entries.Length*44+20));
        }
        private void ClearEntries()
        {
            for(int i=content.childCount-1;i>=0;i--) Destroy(content.GetChild(i).gameObject);
            body=UiKit.NewText("Body",content,"",18,UiKit.TextColor,TextAnchor.UpperLeft); body.supportRichText=true; body.raycastTarget=false; body.horizontalOverflow=HorizontalWrapMode.Wrap; body.verticalOverflow=VerticalWrapMode.Overflow;
            UiKit.TopLeft(body.rectTransform,new Vector2(0,-4),new Vector2(W-80,0));
        }
        private void ResizeBody(float height)
        {
            Canvas.ForceUpdateCanvases(); content.sizeDelta=new Vector2(0,Mathf.Max(height,body.preferredHeight+16));
        }
        private void Button(string name,Transform parent,string label,Vector2 pos,Vector2 size,Action action,bool bottom=false)
        {
            Image image=UiKit.NewImage(name,parent,new Color(.25f,.19f,.11f,1)); image.raycastTarget=true; RectTransform r=image.rectTransform;
            if(bottom){r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);} else {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);}
            r.anchoredPosition=pos;r.sizeDelta=size;UiKit.AddOutline(image,UiKit.Gold,1.5f);
            Text t=UiKit.NewText("Label",r,label,18,UiKit.TextColor,TextAnchor.MiddleCenter);UiKit.Stretch(t.rectTransform,0);
            UnityEngine.UI.Button b=image.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());
        }
        private static string Escape(string s) => (s??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
    }
}
