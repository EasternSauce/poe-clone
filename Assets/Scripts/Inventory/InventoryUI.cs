using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using PoeClone.Audio;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Path of Exile style inventory + equipment screen. Press I to open/close.
    /// Click an item to pick it up onto the cursor, click a slot or grid spot to put it down.
    /// A live preview of the character (facing you) sits beside the panel and shows worn gear.
    /// Built entirely at runtime with uGUI. Slots show a silhouette icon instead of a text label.
    ///
    /// Touch: tap to pick up / put down, or drag an item where it should go; press and hold an
    /// item for its tooltip. The preview is dropped there to make room for the on-screen buttons.
    ///
    /// Putting an item down outside the panels (click or tap there, or drag it out) throws it on
    /// the ground next to the character.
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

        // The bag grid is cool slate against the warm brown panel, with lighter lines between
        // the cells, so it reads at a glance.
        private static readonly Color CellColor = new Color(0.10f, 0.11f, 0.15f, 1f);
        private static readonly Color GridLineColor = new Color(0.30f, 0.32f, 0.40f, 1f);
        private static readonly Color SlotColor = new Color(0.11f, 0.10f, 0.10f, 1f);
        private static readonly Color SlotBorder = new Color(0.30f, 0.25f, 0.15f, 1f);
        private static readonly Color SlotHint = new Color(0.62f, 0.60f, 0.55f, 0.32f);
        private static readonly Color PlateColor = new Color(0.11f, 0.10f, 0.13f, 1f);
        private static readonly Color PlateBorder = new Color(0.40f, 0.36f, 0.30f, 1f);
        private static readonly Color MagicBorder = new Color(0.42f, 0.42f, 0.85f, 1f);
        private static readonly Color RareBorder = new Color(0.85f, 0.80f, 0.35f, 1f);
        private static readonly Color UniqueBorder = new Color(0.85f, 0.45f, 0.15f, 1f);
        private static readonly Color GoodSoft = new Color(0.25f, 0.80f, 0.35f, 0.16f);
        private static readonly Color GoodStrong = new Color(0.25f, 0.85f, 0.35f, 0.40f);
        private static readonly Color Bad = new Color(0.90f, 0.20f, 0.20f, 0.40f);
        private static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.10f);

        // Touch layout: the panel moves left to clear the on-screen button column (TouchControlsUI).
        private const float DesktopRightInset = 30f;
        private const float TouchRightInset = 150f;
        private const float LongPressSeconds = 0.4f;
        private const float DragThreshold = 14f; // canvas units

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
            public Vector2 Screen;
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
        private bool gridDirty;

        // The touch currently being followed (see UpdateTouch).
        private bool touchTracking;
        private bool touchHadItem;
        private bool touchPickupTried;
        private bool touchInspecting;
        private Vector2 touchStart;
        private float touchStartTime;
        private Vector2 lastTouchPos;

        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>An item is on the cursor (so a click outside the panel throws it, rather than attacking).</summary>
        public bool IsHoldingItem => cursorItem != null;

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

            inventory.PlayerDied += OnPlayerDied;
            inventory.Grid.Changed += OnGridChanged;
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.PlayerDied -= OnPlayerDied;
                inventory.Grid.Changed -= OnGridChanged;
            }
        }

        // Can't loot/reroll gear once dead: force the panel shut and leave it locked (see the
        // Update guard below, which stops I/Escape from reopening it).
        private void OnPlayerDied()
        {
            SetOpen(false);
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

            bool dead = inventory.IsPlayerDead;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !dead && !UiKit.IsTypingInTextField())
            {
                if (keyboard.iKey.wasPressedThisFrame)
                    SetOpen(!isOpen);
                else if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
                    SetOpen(false);
            }

            if (!isOpen)
                return;

            if (gridDirty)
                Refresh();

            if (TouchMode.Active)
            {
                UpdateTouch();
                return;
            }

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

        // Follows one finger. With nothing held: a tap picks the item up (it then waits where it
        // was until the next tap puts it down), dragging picks it up and drops it where the finger
        // lifts, and holding still shows the tooltip without picking anything up. With an item
        // already held, lifting the finger puts it down there, whether that was a tap or a drag.
        private void UpdateTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            TouchControl touch = screen.primaryTouch;
            Vector2 pos = touch.position.ReadValue();

            if (touch.press.wasPressedThisFrame)
            {
                touchTracking = !TouchMode.IsOverBlocker(pos);
                touchHadItem = cursorItem != null;
                touchPickupTried = false;
                touchInspecting = false;
                touchStart = pos;
                touchStartTime = Time.unscaledTime;
            }

            if (touchTracking)
                lastTouchPos = pos;

            if (cursorView != null)
                cursorView.position = lastTouchPos;

            if (!touchTracking)
            {
                UpdateHighlights(new Hover());
                return;
            }

            bool moved = (pos - touchStart).magnitude > DragThreshold * canvas.scaleFactor;
            bool held = Time.unscaledTime - touchStartTime >= LongPressSeconds;

            if (touch.press.isPressed && !touchHadItem && cursorItem == null)
            {
                // Dragging still works after the tooltip came up: hold to read, then pull it out.
                if (moved && !touchPickupTried)
                {
                    touchPickupTried = true;
                    tooltipRect.gameObject.SetActive(false);
                    HandleClick(Hit(touchStart));
                }
                else if (held && !moved && !touchInspecting)
                {
                    touchInspecting = true;
                    UpdateTooltip(Hit(touchStart), touchStart);
                }
            }

            UpdateHighlights(cursorItem != null && touch.press.isPressed ? Hit(pos) : new Hover());

            if (!touch.press.wasReleasedThisFrame && touch.press.isPressed)
                return;

            touchTracking = false;
            tooltipRect.gameObject.SetActive(false);

            bool dragPickedUp = touchPickupTried && cursorItem != null;
            bool tap = !moved && !held;

            if (touchHadItem || dragPickedUp || (tap && !touchInspecting))
                HandleClick(Hit(pos));
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>Opens or closes the panel (I key, or the on-screen bag button on touch).</summary>
        public void SetOpen(bool open)
        {
            isOpen = open;
            touchTracking = false;
            bool touch = TouchMode.Active;

            panel.gameObject.SetActive(open);
            panel.anchoredPosition = new Vector2(-(touch ? TouchRightInset : DesktopRightInset), 0f);

            // No room for the character preview beside the panel on a phone.
            previewPanel.gameObject.SetActive(open && !touch);

            if (preview != null)
                preview.SetActive(open && !touch);

            if (cursorView != null)
                cursorView.gameObject.SetActive(open);

            if (!open)
                tooltipRect.gameObject.SetActive(false);
            else
                Refresh();

            // Prewarm() also calls this (invisibly, to warm up layout/fonts) -- that pass must stay silent.
            if (!warming && AudioManager.Instance != null)
            {
                AudioClip toggleClip = open ? AudioManager.Instance.uiInventoryOpen : AudioManager.Instance.uiInventoryClose;
                AudioManager.Instance.PlayUI(toggleClip, AudioManager.Instance.inventoryToggleVolume);
            }
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

            // The gaps between the cells show this backing through as grid lines.
            Image gridBacking = UiKit.NewImage("GridLines", gridArea, GridLineColor);
            UiKit.Stretch(gridBacking.rectTransform, -Gap);
            UiKit.AddOutline(gridBacking, UiKit.BorderColor, 2f);

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
                border = item.Rarity == ItemRarity.Unique ? UniqueBorder : item.Rarity == ItemRarity.Rare ? RareBorder : item.Rarity == ItemRarity.Magic ? MagicBorder : PlateBorder;
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
                icon.color = new Color(item.ArtTint.r, item.ArtTint.g, item.ArtTint.b, alpha);
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

        // Something landed in the bag from outside this screen (a picked-up drop): redraw once,
        // next frame, rather than on every change of a click that redraws anyway.
        private void OnGridChanged()
        {
            gridDirty = true;
        }

        private void Refresh()
        {
            gridDirty = false;

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
            Hover h = new Hover { Screen = screenPos };

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
            // Weapons name their kind ("Bow", "Axe"); everything else its slot type ("Body Armour").
            string typeName = item.Type == ItemType.Weapon ? item.WeaponType.ToString() : item.Type.ToString();
            string type = Regex.Replace(typeName, "(?<=.)([A-Z])", " $1");
            string magic = UiKit.Hex(UiKit.MagicBlue);

            StringBuilder sb = new StringBuilder();
            sb.Append("<b><color=#").Append(UiKit.Hex(UiKit.RarityColor(item.Rarity))).Append(">").Append(item.Name).Append("</color></b>\n");
            sb.Append("<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">").Append(type).Append("</color>");
            lineCount = 2;

            // Which hands it takes, for the gear where that limits what else can be worn.
            string handNote = null;
            if (item.Type == ItemType.Weapon && item.WeaponType == WeaponType.Bow)
                handNote = "Two-handed: no shield (a quiver goes in the off hand)";
            else if (item.Type == ItemType.Quiver)
                handNote = "Off hand, worn with a bow";
            if (handNote != null)
            {
                sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append("><size=14>").Append(handNote).Append("</size></color>");
                lineCount++;
            }

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

            string flavour = UniqueItems.FlavourFor(item);
            if (flavour != null)
            {
                // Wrapped by hand: the tooltip sizes itself to its longest line.
                sb.Append("\n\n<i><color=#").Append(UiKit.Hex(UiKit.UniqueOrange)).Append(">");
                lineCount += 2;
                int column = 0;
                foreach (string word in flavour.Split(' '))
                {
                    if (column > 0 && column + word.Length > 34)
                    {
                        sb.Append('\n');
                        lineCount++;
                        column = 0;
                    }
                    else if (column > 0)
                    {
                        sb.Append(' ');
                        column++;
                    }
                    sb.Append(word);
                    column += word.Length;
                }
                sb.Append("</color></i>");
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

            // Larger on touch: the text is read on a phone, at arm's length.
            bool touch = TouchMode.Active;
            tooltipText.fontSize = touch ? 22 : 17;
            float lineHeight = touch ? 26f : 23f;

            int lines;
            tooltipText.text = BuildTooltipText(item, out lines);
            tooltipRect.sizeDelta = new Vector2(touch ? 340f : 270f, 24f + lines * lineHeight);

            tooltipRect.gameObject.SetActive(true);
            tooltipRect.SetAsLastSibling();

            Vector2 size = tooltipRect.sizeDelta * canvas.scaleFactor;

            if (touch)
            {
                // Left of the finger, so the finger doesn't cover it (the panel is on the right).
                float offset = 40f * canvas.scaleFactor;
                float y = Mathf.Clamp(mousePos.y, size.y * 0.5f, Screen.height - size.y * 0.5f);
                tooltipRect.pivot = new Vector2(1f, 0.5f);
                tooltipRect.position = new Vector2(Mathf.Max(mousePos.x - offset, size.x), y);
                return;
            }

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
            else if (cursorItem != null && !OverInventory(h.Screen))
                ThrowCursorItem();
            else
                return;

            Refresh();
        }

        // Anywhere outside the inventory and character-preview panels (and any other on-screen
        // panel, like chat) counts as the world: putting the item there drops it on the ground.
        private bool OverInventory(Vector2 screen)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(panel, screen, null))
                return true;
            if (previewPanel.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(previewPanel, screen, null))
                return true;
            if (!TouchMode.Active && UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return true;
            return false;
        }

        private void ThrowCursorItem()
        {
            ItemData item = cursorItem;
            cursorItem = null;
            inventory.ThrowAway(item);
            PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiItemPlace : null);
        }

        private void ClickSlot(SlotView s)
        {
            if (cursorItem == null)
            {
                // Take off whatever is worn.
                cursorItem = inventory.Equipment.Unequip(s.Slot);
                if (cursorItem != null)
                    PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiItemPickup : null);
                return;
            }

            // Only the matching type of gear fits this slot.
            ItemData replaced;
            if (!inventory.Equipment.TryEquip(s.Slot, cursorItem, out replaced))
            {
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                return;
            }

            PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiItemPlace : null);
            cursorItem = replaced;
        }

        private void ClickGrid(Vector2 pos)
        {
            if (cursorItem == null)
            {
                PlacedItem p = inventory.Grid.GetAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
                if (p != null && inventory.Grid.Remove(p.Item))
                {
                    cursorItem = p.Item;
                    PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiItemPickup : null);
                }
                return;
            }

            Vector2Int o = FootprintOrigin(cursorItem, pos);
            ItemData replaced;
            if (inventory.Grid.TryPlaceOrSwap(cursorItem, o.x, o.y, out replaced))
            {
                cursorItem = replaced;
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiItemPlace : null);
            }
            else
            {
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
            }
        }

        private void PlayUISound(AudioClip clip)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(clip);
        }
    }
}
