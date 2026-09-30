using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Path of Exile style inventory + equipment screen. Press I to open/close.
    /// Click an item to pick it up onto the cursor, click a slot or grid spot to put it down.
    /// A live preview of the character (facing you) sits beside the panel and shows worn gear.
    /// Built entirely at runtime with uGUI. Slots show a silhouette icon instead of a text label.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private float cellSize = 52f;
        [SerializeField] private bool startOpen;

        private const float Gap = 2f;
        private const float SlotGap = 10f;
        private const float Pad = 20f;
        private const int EquipCols = 8;
        private const int EquipRows = 6;

        private static readonly Color CellColor = new Color(0.14f, 0.13f, 0.12f, 1f);
        private static readonly Color SlotColor = new Color(0.11f, 0.10f, 0.10f, 1f);
        private static readonly Color SlotBorder = new Color(0.30f, 0.25f, 0.15f, 1f);
        private static readonly Color SlotHint = new Color(0.62f, 0.60f, 0.55f, 0.32f);
        private static readonly Color PlateColor = new Color(0.11f, 0.10f, 0.13f, 1f);
        private static readonly Color PlateBorder = new Color(0.40f, 0.36f, 0.30f, 1f);
        private static readonly Color MagicBorder = new Color(0.42f, 0.42f, 0.85f, 1f);
        private static readonly Color GoodSoft = new Color(0.25f, 0.80f, 0.35f, 0.16f);
        private static readonly Color GoodStrong = new Color(0.25f, 0.85f, 0.35f, 0.40f);
        private static readonly Color Bad = new Color(0.90f, 0.20f, 0.20f, 0.40f);
        private static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.10f);

        // Equipment layout in cells (column, row, width, height) on an 8x6 board, PoE style:
        // weapons on the sides, helmet + amulet on top, body armour in the middle,
        // gloves and boots at the bottom corners, and rings flanking the belt.
        private struct SlotLayout
        {
            public EquipSlot Slot;
            public int Col, Row, W, H;

            public SlotLayout(EquipSlot slot, int col, int row, int w, int h)
            {
                Slot = slot;
                Col = col;
                Row = row;
                W = w;
                H = h;
            }
        }

        private static readonly SlotLayout[] Layout =
        {
            new SlotLayout(EquipSlot.MainHand, 0, 0, 2, 4),
            new SlotLayout(EquipSlot.Helmet, 3, 0, 2, 2),
            new SlotLayout(EquipSlot.Amulet, 5, 1, 1, 1),
            new SlotLayout(EquipSlot.OffHand, 6, 0, 2, 4),
            new SlotLayout(EquipSlot.BodyArmour, 3, 2, 2, 3),
            new SlotLayout(EquipSlot.Gloves, 0, 4, 2, 2),
            new SlotLayout(EquipSlot.Ring1, 2, 5, 1, 1),
            new SlotLayout(EquipSlot.Belt, 3, 5, 2, 1),
            new SlotLayout(EquipSlot.Ring2, 5, 5, 1, 1),
            new SlotLayout(EquipSlot.Boots, 6, 4, 2, 2)
        };

        private class SlotView
        {
            public EquipSlot Slot;
            public RectTransform Rect;
            public Image Overlay;
            public Image Hint;
        }

        private struct Hover
        {
            public SlotView Slot;
            public bool OverGrid;
            public Vector2 GridPos; // in cells, origin at the grid's top-left
        }

        private readonly List<SlotView> slotViews = new List<SlotView>();
        private readonly List<GameObject> itemViews = new List<GameObject>();

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform panel;
        private RectTransform previewPanel;
        private RectTransform gridArea;
        private RectTransform gridItems;
        private RectTransform tooltipRect;
        private RectTransform cursorView;
        private Image gridHighlight;
        private Text tooltipText;
        private CharacterPreview preview;

        private ItemData cursorItem;
        private bool isOpen;
        private bool warming;

        public bool IsOpen
        {
            get { return isOpen; }
        }

        private void Start()
        {
            if (inventory == null)
                inventory = FindAnyObjectByType<PlayerInventory>();

            if (inventory == null)
            {
                Debug.LogError("InventoryUI: no PlayerInventory found in the scene.");
                enabled = false;
                return;
            }

            // The character copy shown next to the panel.
            preview = gameObject.AddComponent<CharacterPreview>();
            EquipmentVisuals source = inventory.GetComponentInChildren<EquipmentVisuals>(true);
            bool hasPreview = preview.Build(source, inventory.Equipment);

            BuildUI(hasPreview);
            StartCoroutine(Prewarm());
        }

        // Opens everything once, invisibly, so the first real open has nothing left to set up
        // (fonts, textures, render targets). Without this the first open hitched and flashed.
        private IEnumerator Prewarm()
        {
            warming = true;
            canvasGroup.alpha = 0f;

            SetOpen(true);
            tooltipRect.gameObject.SetActive(true);
            tooltipText.text = "warm up";

            yield return null;
            yield return null;

            tooltipRect.gameObject.SetActive(false);
            SetOpen(startOpen);
            canvasGroup.alpha = 1f;
            warming = false;
        }

        private void Update()
        {
            if (warming)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.iKey.wasPressedThisFrame)
                    SetOpen(!isOpen);
                else if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
                    SetOpen(false);
            }

            if (!isOpen)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 mousePos = mouse.position.ReadValue();

            if (cursorView != null)
                cursorView.position = mousePos;

            Hover hover = Hit(mousePos);
            UpdateHighlights(hover);
            UpdateTooltip(hover, mousePos);

            if (mouse.leftButton.wasPressedThisFrame)
                HandleClick(hover);
        }

        // ------------------------------------------------------------------ open / close

        private void SetOpen(bool open)
        {
            isOpen = open;
            panel.gameObject.SetActive(open);
            previewPanel.gameObject.SetActive(open);

            if (preview != null)
                preview.SetActive(open);

            if (cursorView != null)
                cursorView.gameObject.SetActive(open);

            if (!open)
                tooltipRect.gameObject.SetActive(false);
            else
                Refresh();
        }

        // ------------------------------------------------------------------ building the UI

        private Vector2 CellPos(int col, int row)
        {
            return new Vector2(col * cellSize + Gap * 0.5f, -row * cellSize - Gap * 0.5f);
        }

        private Vector2 SlotPos(int col, int row)
        {
            return new Vector2(col * cellSize + SlotGap * 0.5f, -row * cellSize - SlotGap * 0.5f);
        }

        private Vector2 SlotSize(int w, int h)
        {
            return new Vector2(w * cellSize - SlotGap, h * cellSize - SlotGap);
        }

        
private Vector2 CellSize(int w, int h)
        {
            return new Vector2(w * cellSize - Gap, h * cellSize - Gap);
        }

        private void BuildUI(bool hasPreview)
        {
            canvas = UiKit.NewCanvas("InventoryCanvas", transform, 50, out canvasGroup);

            float gridW = inventory.Grid.Width * cellSize;
            float gridH = inventory.Grid.Height * cellSize;
            float equipW = EquipCols * cellSize;
            float equipH = EquipRows * cellSize;
            float panelW = Mathf.Max(gridW, equipW) + Pad * 2f;
            float panelH = equipH + 24f + gridH + Pad * 2f;

            // Inventory panel on the right side of the screen, like PoE.
            Image panelImage = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor);
            panel = panelImage.rectTransform;
            panel.anchorMin = new Vector2(1f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-30f, 0f);
            panel.sizeDelta = new Vector2(panelW, panelH);
            UiKit.AddOutline(panelImage, UiKit.BorderColor, 3f);

            // Character preview panel, just to the left of the inventory.
            Image previewBg = UiKit.NewImage("PreviewPanel", canvas.transform, UiKit.PanelColor);
            previewPanel = previewBg.rectTransform;
            previewPanel.anchorMin = new Vector2(1f, 0.5f);
            previewPanel.anchorMax = new Vector2(1f, 0.5f);
            previewPanel.pivot = new Vector2(1f, 0.5f);
            previewPanel.anchoredPosition = new Vector2(-(30f + panelW + 16f), 0f);
            previewPanel.sizeDelta = new Vector2(panelH * 0.68f, panelH);
            UiKit.AddOutline(previewBg, UiKit.BorderColor, 3f);

            if (hasPreview)
            {
                GameObject rawGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
                rawGo.transform.SetParent(previewPanel, false);
                RawImage raw = rawGo.GetComponent<RawImage>();
                raw.texture = preview.Texture;
                raw.raycastTarget = false;
                UiKit.Stretch((RectTransform)rawGo.transform, 8f);
            }

            // Equipment board (top).
            RectTransform equipArea = UiKit.NewRect("Equipment", panel);
            equipArea.anchorMin = new Vector2(0.5f, 1f);
            equipArea.anchorMax = new Vector2(0.5f, 1f);
            equipArea.pivot = new Vector2(0.5f, 1f);
            equipArea.anchoredPosition = new Vector2(0f, -Pad);
            equipArea.sizeDelta = new Vector2(equipW, equipH);

            foreach (SlotLayout l in Layout)
                BuildSlot(equipArea, l);

            // Inventory grid (bottom).
            gridArea = UiKit.NewRect("Grid", panel);
            gridArea.anchorMin = new Vector2(0.5f, 0f);
            gridArea.anchorMax = new Vector2(0.5f, 0f);
            gridArea.pivot = new Vector2(0.5f, 0f);
            gridArea.anchoredPosition = new Vector2(0f, Pad);
            gridArea.sizeDelta = new Vector2(gridW, gridH);

            for (int y = 0; y < inventory.Grid.Height; y++)
            {
                for (int x = 0; x < inventory.Grid.Width; x++)
                {
                    Image cell = UiKit.NewImage("Cell", gridArea, CellColor);
                    UiKit.TopLeft(cell.rectTransform, CellPos(x, y), CellSize(1, 1));
                }
            }

            gridItems = UiKit.NewRect("Items", gridArea);
            UiKit.Stretch(gridItems, 0f);

            gridHighlight = UiKit.NewImage("Highlight", gridArea, Color.clear);
            gridHighlight.enabled = false;

            BuildTooltip();
        }

        private void BuildSlot(RectTransform parent, SlotLayout l)
        {
            Image bg = UiKit.NewImage("Slot_" + l.Slot, parent, SlotColor);
            RectTransform rt = bg.rectTransform;
            UiKit.TopLeft(rt, SlotPos(l.Col, l.Row), SlotSize(l.W, l.H));
            UiKit.AddOutline(bg, SlotBorder, 1.5f);

            // The icon tells you what goes here (no text label).
            Image hint = UiKit.NewImage("Hint", rt, SlotHint);
            hint.sprite = IconFactory.Get(SlotRules.AcceptedType(l.Slot));
            hint.preserveAspect = true;
            RectTransform hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 0.5f);
            hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = Vector2.zero;
            float hintSize = Mathf.Min(rt.sizeDelta.x, rt.sizeDelta.y) * 0.62f;
            hrt.sizeDelta = new Vector2(hintSize, hintSize);

            Image overlay = UiKit.NewImage("Overlay", rt, Color.clear);
            UiKit.Stretch(overlay.rectTransform, 0f);

            slotViews.Add(new SlotView { Slot = l.Slot, Rect = rt, Overlay = overlay, Hint = hint });
        }

        private void BuildTooltip()
        {
            Image bg = UiKit.NewImage("Tooltip", canvas.transform, new Color(0.05f, 0.05f, 0.06f, 0.97f));
            tooltipRect = bg.rectTransform;
            tooltipRect.anchorMin = Vector2.zero;
            tooltipRect.anchorMax = Vector2.zero;
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.sizeDelta = new Vector2(270f, 100f);
            UiKit.AddOutline(bg, UiKit.BorderColor, 1.5f);

            tooltipText = UiKit.NewText("Text", tooltipRect, "", 17, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.Stretch(tooltipText.rectTransform, 10f);

            tooltipRect.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ item views

        private RectTransform CreateItemView(Transform parent, ItemData item, Vector2 size, float alpha)
        {
            Sprite painted = ItemArt.PaintedIcon(item);
            Color tint = item.Tint;

            Color plate;
            Color border;
            if (painted != null)
            {
                plate = new Color(PlateColor.r, PlateColor.g, PlateColor.b, alpha);
                border = item.Modifiers.Count > 0 ? MagicBorder : PlateBorder;
            }
            else
            {
                plate = new Color(tint.r * 0.30f, tint.g * 0.30f, tint.b * 0.30f, alpha);
                border = tint;
            }

            Image bg = UiKit.NewImage("Item_" + item.Name, parent, plate);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            UiKit.AddOutline(bg, new Color(border.r, border.g, border.b, alpha), 1.5f);

            Image icon = UiKit.NewImage("Icon", rt, Color.white);
            icon.preserveAspect = true;
            RectTransform irt = icon.rectTransform;

            if (painted != null)
            {
                icon.sprite = painted;
                icon.color = new Color(1f, 1f, 1f, alpha);
                UiKit.Stretch(irt, 3f);
            }
            else
            {
                Color bright = Color.Lerp(tint, Color.white, 0.3f);
                icon.sprite = IconFactory.Get(item.Type);
                icon.color = new Color(bright.r, bright.g, bright.b, alpha);
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                float s = Mathf.Min(size.x, size.y) * 0.78f;
                irt.sizeDelta = new Vector2(s, s);
            }

            return rt;
        }

        private void Refresh()
        {
            foreach (GameObject go in itemViews)
                Destroy(go);
            itemViews.Clear();

            // Items in the bag.
            foreach (PlacedItem p in inventory.Grid.Items)
            {
                RectTransform rt = CreateItemView(gridItems, p.Item, CellSize(p.Item.Width, p.Item.Height), 1f);
                rt.anchoredPosition = CellPos(p.X, p.Y);
                itemViews.Add(rt.gameObject);
            }

            // Items being worn.
            foreach (SlotView s in slotViews)
            {
                ItemData item = inventory.Equipment.Get(s.Slot);
                s.Hint.enabled = item == null;

                if (item != null)
                {
                    Vector2 size = s.Rect.sizeDelta - new Vector2(4f, 4f);
                    RectTransform rt = CreateItemView(s.Rect, item, size, 1f);
                    rt.anchoredPosition = new Vector2(2f, -2f);
                    itemViews.Add(rt.gameObject);
                }

                s.Overlay.rectTransform.SetAsLastSibling();
            }

            // Item stuck to the cursor.
            if (cursorView != null)
                Destroy(cursorView.gameObject);
            cursorView = null;

            if (cursorItem != null)
            {
                cursorView = CreateItemView(canvas.transform, cursorItem, CellSize(cursorItem.Width, cursorItem.Height), 0.85f);
                cursorView.pivot = new Vector2(0.5f, 0.5f);
                cursorView.gameObject.SetActive(isOpen);
                cursorView.SetAsLastSibling();
            }
        }

        // ------------------------------------------------------------------ hit testing

        private Hover Hit(Vector2 screenPos)
        {
            Hover h = new Hover();

            foreach (SlotView s in slotViews)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(s.Rect, screenPos, null))
                {
                    h.Slot = s;
                    return h;
                }
            }

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gridArea, screenPos, null, out local))
            {
                Rect r = gridArea.rect;
                float px = local.x - r.xMin;
                float py = r.yMax - local.y;

                if (px >= 0f && py >= 0f && px < r.width && py < r.height)
                {
                    h.OverGrid = true;
                    h.GridPos = new Vector2(px / cellSize, py / cellSize);
                }
            }

            return h;
        }

        // Where an item's top-left cell lands when its centre is under the cursor (kept inside the grid).
        private Vector2Int FootprintOrigin(ItemData item, Vector2 pos)
        {
            int x = Mathf.RoundToInt(pos.x - item.Width * 0.5f);
            int y = Mathf.RoundToInt(pos.y - item.Height * 0.5f);
            x = Mathf.Clamp(x, 0, Mathf.Max(0, inventory.Grid.Width - item.Width));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, inventory.Grid.Height - item.Height));
            return new Vector2Int(x, y);
        }

        // ------------------------------------------------------------------ feedback

        private void UpdateHighlights(Hover h)
        {
            foreach (SlotView s in slotViews)
            {
                Color c = Color.clear;
                bool hovered = h.Slot == s;

                if (cursorItem != null)
                {
                    if (inventory.Equipment.CanEquip(s.Slot, cursorItem))
                        c = hovered ? GoodStrong : GoodSoft;
                    else if (hovered)
                        c = Bad;
                }
                else if (hovered && inventory.Equipment.Get(s.Slot) != null)
                {
                    c = HoverTint;
                }

                s.Overlay.color = c;
            }

            if (!h.OverGrid)
            {
                gridHighlight.enabled = false;
                return;
            }

            if (cursorItem != null)
            {
                Vector2Int o = FootprintOrigin(cursorItem, h.GridPos);
                bool ok = inventory.Grid.InBounds(cursorItem, o.x, o.y) &&
                          inventory.Grid.GetOverlapping(cursorItem, o.x, o.y).Count <= 1;
                ShowGridHighlight(o.x, o.y, cursorItem.Width, cursorItem.Height, ok ? GoodStrong : Bad);
            }
            else
            {
                PlacedItem p = inventory.Grid.GetAt(Mathf.FloorToInt(h.GridPos.x), Mathf.FloorToInt(h.GridPos.y));
                if (p != null)
                    ShowGridHighlight(p.X, p.Y, p.Item.Width, p.Item.Height, HoverTint);
                else
                    gridHighlight.enabled = false;
            }
        }

        private void ShowGridHighlight(int x, int y, int w, int h, Color color)
        {
            gridHighlight.enabled = true;
            gridHighlight.color = color;
            UiKit.TopLeft(gridHighlight.rectTransform, CellPos(x, y), CellSize(w, h));
        }

        // Name, type, then the item's real stats (the ones that change the character when worn).
        private static string BuildTooltipText(ItemData item, out int lineCount)
        {
            string type = Regex.Replace(item.Type.ToString(), "(?<=.)([A-Z])", " $1");
            string magic = UiKit.Hex(UiKit.MagicBlue);

            StringBuilder sb = new StringBuilder();
            sb.Append("<b><color=#").Append(magic).Append(">").Append(item.Name).Append("</color></b>\n");
            sb.Append("<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">").Append(type).Append("</color>");
            lineCount = 2;

            if (item.Modifiers.Count > 0)
            {
                sb.Append("\n");
                lineCount++;

                foreach (StatModifier m in item.Modifiers)
                {
                    sb.Append("\n<color=#").Append(magic).Append(">").Append(StatFormatter.ItemLine(m)).Append("</color>");
                    lineCount++;
                }
            }

            return sb.ToString();
        }

        private void UpdateTooltip(Hover h, Vector2 mousePos)
        {
            ItemData item = null;

            if (cursorItem == null)
            {
                if (h.Slot != null)
                {
                    item = inventory.Equipment.Get(h.Slot.Slot);
                }
                else if (h.OverGrid)
                {
                    PlacedItem p = inventory.Grid.GetAt(Mathf.FloorToInt(h.GridPos.x), Mathf.FloorToInt(h.GridPos.y));
                    if (p != null)
                        item = p.Item;
                }
            }

            if (item == null)
            {
                tooltipRect.gameObject.SetActive(false);
                return;
            }

            int lines;
            tooltipText.text = BuildTooltipText(item, out lines);
            tooltipRect.sizeDelta = new Vector2(270f, 24f + lines * 23f);

            tooltipRect.gameObject.SetActive(true);
            tooltipRect.SetAsLastSibling();

            Vector2 size = tooltipRect.sizeDelta * canvas.scaleFactor;
            bool flipX = mousePos.x + 18f + size.x > Screen.width;
            bool flipY = mousePos.y - 18f - size.y < 0f;
            tooltipRect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
            tooltipRect.position = new Vector2(mousePos.x + (flipX ? -18f : 18f), mousePos.y + (flipY ? 18f : -18f));
        }

        // ------------------------------------------------------------------ clicking

        private void HandleClick(Hover h)
        {
            if (h.Slot != null)
                ClickSlot(h.Slot);
            else if (h.OverGrid)
                ClickGrid(h.GridPos);
            else
                return;

            Refresh();
        }

        private void ClickSlot(SlotView s)
        {
            if (cursorItem == null)
            {
                // Take off whatever is worn.
                cursorItem = inventory.Equipment.Unequip(s.Slot);
                return;
            }

            // Only the matching type of gear fits this slot.
            ItemData replaced;
            if (!inventory.Equipment.TryEquip(s.Slot, cursorItem, out replaced))
                return;

            cursorItem = replaced;
        }

        private void ClickGrid(Vector2 pos)
        {
            if (cursorItem == null)
            {
                PlacedItem p = inventory.Grid.GetAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
                if (p != null && inventory.Grid.Remove(p.Item))
                    cursorItem = p.Item;
                return;
            }

            Vector2Int o = FootprintOrigin(cursorItem, pos);
            ItemData replaced;
            if (inventory.Grid.TryPlaceOrSwap(cursorItem, o.x, o.y, out replaced))
                cursorItem = replaced;
        }
    }
}
