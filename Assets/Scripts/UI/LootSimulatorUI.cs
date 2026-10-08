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
        private sealed class ItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<Vector2> Enter;
            public Action Exit;
            public void OnPointerEnter(PointerEventData e) => Enter?.Invoke(e.position);
            public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        }
        private sealed class Filter { public ItemType Type; public WeaponType Weapon; public bool Selected = true; public Text Label; public string Name; }
        private readonly List<Filter> filters = new List<Filter>();
        private readonly List<ItemData> items = new List<ItemData>();
        private readonly System.Random rng = new System.Random();
        private GameObject root;
        private RectTransform grid, tip;
        private Text tipText, status, inventorySummary;
        private ScrollRect inventoryScroll;
        private InputField level, count;
        private ItemRarity minimumRarity = ItemRarity.Rare;
        private readonly Dictionary<ItemRarity, Text> rarityButtons = new Dictionary<ItemRarity, Text>();
        private const int GridColumns = 16;
        private const int MinimumRows = 12;
        private const float CellSize = 44f;
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
            Label(pr,"Hint","Preview loot only. Items use their inventory sizes. Scroll for more; hover for details. T1 is strongest.",new Vector2(24,-60),new Vector2(1080,32));
            UiKit.CloseButton(pr,Close);
            Label(pr,"AreaLabel","Area level (1?100)",new Vector2(24,-110),new Vector2(190,30));
            level = Field(pr,"AreaLevel","1",new Vector2(24,-145));
            Label(pr,"CountLabel","Items (1?1000)",new Vector2(214,-110),new Vector2(175,30));
            count = Field(pr,"ItemCount","48",new Vector2(214,-145));
            Button(pr,"Randomize Loot",new Vector2(420,-140),new Vector2(200,42),Randomize);
            Label(pr,"MinimumRarity","Minimum rarity",new Vector2(650,-110),new Vector2(440,30));
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                ItemRarity selected = rarity;
                rarityButtons[rarity] = Button(pr,rarity.ToString(),new Vector2(650+(int)rarity*112,-145),new Vector2(104,36),() => SelectRarity(selected));
            }
            SelectRarity(minimumRarity);
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
            BuildInventory(pr);
            inventorySummary = Label(pr,"InventorySummary","No items yet",new Vector2(390,-770),new Vector2(720,36));
            status = Label(pr,"Status","Select item types, then randomize.",new Vector2(24,-655),new Vector2(350,100)); status.horizontalOverflow = HorizontalWrapMode.Wrap;
            Image tipBg = UiKit.NewImage("Tooltip",canvas.transform,new Color(.07f,.07f,.08f,.98f)); tip = tipBg.rectTransform;
            tip.anchorMin = tip.anchorMax = Vector2.zero; tip.pivot = new Vector2(0,1);
            tipText = UiKit.NewText("Text",tip,"",17,UiKit.TextColor,TextAnchor.UpperLeft); UiKit.Stretch(tipText.rectTransform,10);
            tipBg.raycastTarget = false; tipText.raycastTarget = false; tip.gameObject.SetActive(false);
            Render();
        }
        private void BuildInventory(Transform parent)
        {
            RectTransform area = UiKit.NewRect("Inventory",parent);
            UiKit.TopLeft(area,new Vector2(390,-205),new Vector2(720,MinimumRows*CellSize));
            Image viewport = UiKit.NewImage("Viewport",area,new Color(.10f,.11f,.15f));
            UiKit.TopLeft(viewport.rectTransform,Vector2.zero,new Vector2(GridColumns*CellSize,MinimumRows*CellSize));
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            grid = UiKit.NewRect("Results",viewport.transform);
            UiKit.TopLeft(grid,Vector2.zero,new Vector2(GridColumns*CellSize,MinimumRows*CellSize));

            inventoryScroll = area.gameObject.AddComponent<ScrollRect>();
            inventoryScroll.viewport = viewport.rectTransform;
            inventoryScroll.content = grid;
            inventoryScroll.horizontal = false;
            inventoryScroll.movementType = ScrollRect.MovementType.Clamped;
            inventoryScroll.scrollSensitivity = CellSize*3;
            inventoryScroll.onValueChanged.AddListener(_ => tip.gameObject.SetActive(false));

            Image track = UiKit.NewImage("Scrollbar",area,new Color(.04f,.04f,.05f));
            UiKit.TopLeft(track.rectTransform,new Vector2(GridColumns*CellSize+3,0),new Vector2(13,MinimumRows*CellSize));
            RectTransform sliding = UiKit.NewRect("SlidingArea",track.transform);
            UiKit.Stretch(sliding,2);
            Image handle = UiKit.NewImage("Handle",sliding,UiKit.BorderColor);
            UiKit.Stretch(handle.rectTransform,0);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            inventoryScroll.verticalScrollbar = scrollbar;
        }

        // First-fit packing keeps every roll separate and avoids overlap scans against all
        // previous items when previewing up to 1000 drops.
        private List<PlacedItem> PackItems(out int rows)
        {
            int capacity = MinimumRows;
            foreach (ItemData item in items) capacity += item.Height;
            var occupied = new bool[GridColumns,capacity];
            var placed = new List<PlacedItem>(items.Count);
            rows = MinimumRows;
            foreach (ItemData item in items)
            {
                bool found = false;
                for (int y = 0; y <= capacity-item.Height && !found; y++)
                    for (int x = 0; x <= GridColumns-item.Width && !found; x++)
                    {
                        bool fits = true;
                        for (int dy = 0; dy < item.Height && fits; dy++)
                            for (int dx = 0; dx < item.Width; dx++)
                                if (occupied[x+dx,y+dy]) { fits = false; break; }
                        if (!fits) continue;
                        for (int dy = 0; dy < item.Height; dy++)
                            for (int dx = 0; dx < item.Width; dx++) occupied[x+dx,y+dy] = true;
                        placed.Add(new PlacedItem(item,x,y));
                        rows = Mathf.Max(rows,y+item.Height);
                        found = true;
                    }
            }
            return placed;
        }

        private void SelectRarity(ItemRarity rarity)
        {
            minimumRarity = rarity;
            foreach (var pair in rarityButtons)
            {
                pair.Value.text = (pair.Key == rarity ? "[x] " : "") + pair.Key;
                pair.Value.color = pair.Key == rarity ? UiKit.RarityColor(pair.Key) : UiKit.DimText;
            }
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
            var dropChances = ItemGenerator.DropChances(ilvl);
            foreach (var pair in dropChances)
            {
                ItemData display = ItemGenerator.Display(pair.Key,null,ItemRarity.Normal);
                if (display == null || !Allowed(display)) continue;
                bases.Add(pair.Key); weights.Add(pair.Value); total += pair.Value;
            }
            if (bases.Count == 0) { status.text = "Select at least one item type available at this area level."; return; }
            var uniqueIndices = new List<int>();
            if (minimumRarity == ItemRarity.Unique)
            {
                for (int k = 0; k < UniqueItems.Count; k++)
                {
                    ItemData unique = UniqueItems.Create(k);
                    if (Allowed(unique) && dropChances.ContainsKey(unique.Id)) uniqueIndices.Add(k);
                }
                if (uniqueIndices.Count == 0)
                { status.text = "No unique items match the selected types at this area level."; return; }
            }
            // Build a complete batch before replacing the previous preview. Generator balance
            // edits can leave a base without enough modifiers to meet the requested rarity.
            var generated = new List<ItemData>(amount);
            int normal = 0, magic = 0, rare = 0, uniques = 0;
            for (int i = 0; i < amount; i++)
            {
                ItemData item = null;
                if (minimumRarity == ItemRarity.Unique)
                {
                    item = UniqueItems.Create(uniqueIndices[rng.Next(uniqueIndices.Count)], rng);
                    item.ItemLevel = ilvl;
                }
                else
                {
                    for (int attempt = 0; attempt < 32; attempt++)
                    {
                        double roll = rng.NextDouble()*total; int pick = bases.Count-1;
                        for (int k = 0; k < weights.Count; k++) { roll -= weights[k]; if (roll < 0) { pick = k; break; } }
                        ItemRarity rarity = ItemGenerator.RollRarity(rng);
                        if (rarity < minimumRarity) rarity = minimumRarity;
                        item = ItemGenerator.Generate(rng,bases[pick],ilvl,rarity);
                        if (item != null && item.Rarity >= minimumRarity) break;
                        item = null;
                    }
                    if (item == null)
                    { status.text = "Could not meet the minimum rarity with these types and modifier settings. Try other types or a lower minimum."; return; }
                }
                generated.Add(item);
                if (item.Rarity == ItemRarity.Normal) normal++;
                else if (item.Rarity == ItemRarity.Magic) magic++;
                else if (item.Rarity == ItemRarity.Rare) rare++;
                else uniques++;
            }
            items.Clear(); items.AddRange(generated);
            status.text = $"{amount} items - area level {ilvl}\nMinimum: {minimumRarity}\n{normal} Normal / {magic} Magic\n{rare} Rare / {uniques} Unique"; Render();
        }
        private void Render()
        {
            tip.gameObject.SetActive(false);
            foreach (Transform child in grid) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            List<PlacedItem> placed = PackItems(out int rows);
            grid.sizeDelta = new Vector2(GridColumns*CellSize,rows*CellSize);
            Color lineColor = new Color(.30f,.32f,.40f);
            for (int x = 0; x <= GridColumns; x++)
            {
                Image line = UiKit.NewImage("Column"+x,grid,lineColor); line.raycastTarget = false;
                UiKit.TopLeft(line.rectTransform,new Vector2(Mathf.Min(x*CellSize,grid.sizeDelta.x-1),0),new Vector2(1,grid.sizeDelta.y));
            }
            for (int y = 0; y <= rows; y++)
            {
                Image line = UiKit.NewImage("Row"+y,grid,lineColor); line.raycastTarget = false;
                UiKit.TopLeft(line.rectTransform,new Vector2(0,-Mathf.Min(y*CellSize,grid.sizeDelta.y-1)),new Vector2(grid.sizeDelta.x,1));
            }
            foreach (PlacedItem p in placed)
            {
                ItemData item = p.Item;
                Image frame = UiKit.NewImage("Item_"+item.Name,grid,UiKit.RarityColor(item.Rarity)); frame.raycastTarget = true;
                UiKit.TopLeft(frame.rectTransform,new Vector2(p.X*CellSize+1,-p.Y*CellSize-1),new Vector2(item.Width*CellSize-2,item.Height*CellSize-2));
                Image face = UiKit.NewImage("Face",frame.transform,new Color(.06f,.06f,.07f)); UiKit.Stretch(face.rectTransform,2);
                Image icon = UiKit.NewImage("Icon",frame.transform,item.IconTint); icon.sprite = ItemArt.Icon(item); icon.preserveAspect = true; UiKit.Stretch(icon.rectTransform,5);
                face.raycastTarget = icon.raycastTarget = false;
                // Only handle hover so wheel and drag events reach the parent ScrollRect.
                var hover = frame.gameObject.AddComponent<ItemHover>();
                hover.Enter = at => ShowTip(item,at);
                hover.Exit = () => tip.gameObject.SetActive(false);
            }
            inventoryScroll.StopMovement();
            inventoryScroll.verticalNormalizedPosition = 1f;
            inventorySummary.text = items.Count == 0 ? "No items yet" : $"{items.Count} items in a {GridColumns} x {rows} inventory - scroll to browse";
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
