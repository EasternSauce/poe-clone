#if UNITY_EDITOR
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.EditorTools
{
    /// <summary>Preview-only loot rolls using the live game's base and modifier tables.</summary>
    public class LootSimulatorUI : MonoBehaviour
    {
        private sealed class Filter { public ItemType Type; public WeaponType Weapon; public bool Selected = true; public Text Label; public string Name; }
        private readonly List<Filter> filters = new List<Filter>();
        private readonly List<ItemData> items = new List<ItemData>();
        private readonly System.Random rng = new System.Random();
        private GameObject root;
        private RectTransform grid, tip;
        private Text tipText, status, pages;
        private InputField level, count;
        private int page;
        private const int PageSize = 48;
        public bool IsOpen => root != null && root.activeSelf;

        public void Open()
        {
            if (root == null) Build();
            root.SetActive(true);
        }
        public void Close() { UnityEditor.EditorApplication.isPlaying = false; }
        private static string Human(string value) => Regex.Replace(value, "(?<=.)([A-Z])", " $1");
        private Text Label(Transform parent, string name, string text, Vector2 at, Vector2 size, int font = 18)
        {
            Text t = UiKit.NewText(name, parent, text, font, UiKit.TextColor, TextAnchor.MiddleLeft);
            UiKit.TopLeft(t.rectTransform, at, size); t.raycastTarget = false; return t;
        }
        private Text Button(Transform parent, string text, Vector2 at, Vector2 size, Action action)
        {
            Image bg = UiKit.NewImage(text, parent, UiKit.PanelColor); bg.raycastTarget = true;
            UiKit.TopLeft(bg.rectTransform, at, size); UiKit.AddOutline(bg, UiKit.BorderColor, 1f);
            var button = bg.gameObject.AddComponent<Button>(); button.targetGraphic = bg; button.onClick.AddListener(() => action());
            Text label = Label(bg.transform, "Label", text, Vector2.zero, size); label.alignment = TextAnchor.MiddleCenter;
            return label;
        }
        private InputField Field(Transform parent, string name, string initial, Vector2 at)
        {
            Image bg = UiKit.NewImage(name, parent, new Color(.04f,.04f,.05f)); bg.raycastTarget = true;
            UiKit.TopLeft(bg.rectTransform, at, new Vector2(130,36));
            Text text = Label(bg.transform, "Value", initial, new Vector2(8,0), new Vector2(114,36));
            InputField input = bg.gameObject.AddComponent<InputField>(); input.textComponent = text;
            input.contentType = InputField.ContentType.IntegerNumber; input.characterLimit = 4; input.text = initial;
            return input;
        }
        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("LootSimulator", transform, 980, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>(); group.interactable = group.blocksRaycasts = true; root = canvas.gameObject;
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0,0,0,.8f)); UiKit.Stretch(shade.rectTransform,0); shade.raycastTarget = true;
            Image panel = UiKit.NewImage("Panel",canvas.transform,UiKit.PanelColor); panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(.5f,.5f); pr.sizeDelta = new Vector2(1140,830);
            TouchMode.AddBlocker(pr); UiKit.AddOutline(panel,UiKit.BorderColor,2);
            Label(pr,"Title","LOOT SIMULATOR",new Vector2(24,-14),new Vector2(900,42),28);
            Label(pr,"Hint","Preview loot only. Hover an icon for its item tooltip. T1 is the strongest tier.",new Vector2(24,-60),new Vector2(1080,32));
            UiKit.CloseButton(pr,Close);
            Label(pr,"AreaLabel","Area level (1?100)",new Vector2(24,-110),new Vector2(190,30));
            level = Field(pr,"AreaLevel","1",new Vector2(24,-145));
            Label(pr,"CountLabel","Items (1?1000)",new Vector2(214,-110),new Vector2(175,30));
            count = Field(pr,"ItemCount","48",new Vector2(214,-145));
            Button(pr,"Randomize Loot",new Vector2(420,-140),new Vector2(200,42),Randomize);
            Button(pr,"Select all",new Vector2(24,-205),new Vector2(150,34),() => SelectAll(true));
            Button(pr,"Clear all",new Vector2(186,-205),new Vector2(150,34),() => SelectAll(false));
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                if (type == ItemType.Gold || type == ItemType.Potion || type == ItemType.Consumable) continue;
                if (type == ItemType.Weapon)
                {
                    foreach (WeaponType weapon in Enum.GetValues(typeof(WeaponType)))
                        if (weapon != WeaponType.Unarmed) AddFilter(pr,type,weapon,Human(weapon.ToString()));
                }
                else AddFilter(pr,type,WeaponType.Unarmed,Human(type.ToString()));
            }
            grid = UiKit.NewRect("Results",pr); UiKit.TopLeft(grid,new Vector2(390,-205),new Vector2(720,550));
            Button(pr,"Previous",new Vector2(390,-770),new Vector2(120,36),() => { page = Mathf.Max(0,page-1); Render(); });
            Button(pr,"Next",new Vector2(970,-770),new Vector2(120,36),() => { page = Mathf.Min(Mathf.Max(0,(items.Count-1)/PageSize),page+1); Render(); });
            pages = Label(pr,"Pages","",new Vector2(530,-770),new Vector2(420,36));
            status = Label(pr,"Status","Select item types, then randomize.",new Vector2(24,-655),new Vector2(350,100)); status.horizontalOverflow = HorizontalWrapMode.Wrap;
            Image tipBg = UiKit.NewImage("Tooltip",canvas.transform,new Color(.07f,.07f,.08f,.98f)); tip = tipBg.rectTransform;
            tip.anchorMin = tip.anchorMax = Vector2.zero; tip.pivot = new Vector2(0,1);
            tipText = UiKit.NewText("Text",tip,"",17,UiKit.TextColor,TextAnchor.UpperLeft); UiKit.Stretch(tipText.rectTransform,10);
            tipBg.raycastTarget = false; tipText.raycastTarget = false; tip.gameObject.SetActive(false);
        }
        private void AddFilter(Transform parent, ItemType type, WeaponType weapon, string name)
        {
            int index = filters.Count;
            var f = new Filter { Type = type, Weapon = weapon, Name = name };
            f.Label = Button(parent,"[x] " + name,new Vector2(24+(index%2)*172,-255-(index/2)*34),new Vector2(164,30),() => { f.Selected = !f.Selected; UpdateFilter(f); });
            filters.Add(f);
        }
        private static void UpdateFilter(Filter f) { f.Label.text = (f.Selected ? "[x] " : "[ ] ") + f.Name; }
        private void SelectAll(bool value) { foreach (Filter f in filters) { f.Selected = value; UpdateFilter(f); } }
        private bool Allowed(ItemData item) => filters.Exists(f => f.Selected && f.Type == item.Type && (f.Type != ItemType.Weapon || f.Weapon == item.WeaponType));
        private void Randomize()
        {
            if (!int.TryParse(level.text,out int ilvl) || ilvl < 1 || ilvl > 100 || !int.TryParse(count.text,out int amount) || amount < 1 || amount > 1000)
            { status.text = "Enter an area level from 1 to 100 and an item count from 1 to 1000."; return; }
            var bases = new List<string>(); var weights = new List<float>(); float total = 0;
            foreach (var pair in ItemGenerator.DropChances(ilvl))
            {
                ItemData display = ItemGenerator.Display(pair.Key,null,ItemRarity.Normal);
                if (display == null || !Allowed(display)) continue;
                bases.Add(pair.Key); weights.Add(pair.Value); total += pair.Value;
            }
            if (bases.Count == 0) { status.text = "Select at least one item type available at this area level."; return; }
            items.Clear(); int normal = 0, magic = 0, rare = 0;
            for (int i = 0; i < amount; i++)
            {
                double roll = rng.NextDouble()*total; int pick = bases.Count-1;
                for (int k = 0; k < weights.Count; k++) { roll -= weights[k]; if (roll < 0) { pick = k; break; } }
                ItemData item = ItemGenerator.Generate(rng,bases[pick],ilvl,ItemGenerator.RollRarity(rng)); items.Add(item);
                if (item.Rarity == ItemRarity.Normal) normal++; else if (item.Rarity == ItemRarity.Magic) magic++; else rare++;
            }
            page = 0; status.text = $"{amount} items ? area level {ilvl}\n{normal} Normal / {magic} Magic / {rare} Rare"; Render();
        }
        private void Render()
        {
            tip.gameObject.SetActive(false);
            foreach (Transform child in grid) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int start = page*PageSize, end = Mathf.Min(items.Count,start+PageSize);
            for (int i = start; i < end; i++)
            {
                ItemData item = items[i]; int cell = i-start;
                Image frame = UiKit.NewImage("Item_"+i,grid,UiKit.RarityColor(item.Rarity)); frame.raycastTarget = true;
                UiKit.TopLeft(frame.rectTransform,new Vector2((cell%8)*90,-(cell/8)*90),new Vector2(84,84));
                Image face = UiKit.NewImage("Face",frame.transform,new Color(.06f,.06f,.07f)); UiKit.Stretch(face.rectTransform,2);
                Image icon = UiKit.NewImage("Icon",frame.transform,item.IconTint); icon.sprite = ItemArt.Icon(item); icon.preserveAspect = true; UiKit.Stretch(icon.rectTransform,5);
                face.raycastTarget = icon.raycastTarget = false;
                var trigger = frame.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; enter.callback.AddListener(e => ShowTip(item,((PointerEventData)e).position)); trigger.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit }; exit.callback.AddListener(e => tip.gameObject.SetActive(false)); trigger.triggers.Add(exit);
            }
            pages.text = items.Count == 0 ? "No items yet" : $"{start+1}?{end} of {items.Count} ? page {page+1}/{(items.Count+PageSize-1)/PageSize}";
        }
        private void Update()
        {
            if (IsOpen && tip.gameObject.activeSelf && Mouse.current != null)
            { tip.position = Mouse.current.position.ReadValue() + new Vector2(18,-18); InventoryUI.ClampTooltip(tip); }
        }
        private void ShowTip(ItemData item, Vector2 at)
        {
            tipText.text = InventoryUI.BuildTooltipText(item,out int lines);
            tip.sizeDelta = InventoryUI.FitTooltip(tipText,270,24+lines*23);
            tip.gameObject.SetActive(true); tip.SetAsLastSibling(); tip.position = at+new Vector2(18,-18); InventoryUI.ClampTooltip(tip);
        }
    }
}

#endif
