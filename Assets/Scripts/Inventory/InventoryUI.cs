using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public partial class InventoryUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private float cellSize = 52f;
        [SerializeField] private bool startOpen;

        private const float Gap = 2f;
        private const float SlotGap = 10f;
        private const float Pad = 20f;
        private const float OpenFade = 0.14f; // seconds to fade in on opening
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
        private static readonly Color UnmetTint = new Color(1f, 0.15f, 0.1f, 0.15f);
        private static readonly Color GoodSoft = new Color(0.25f, 0.80f, 0.35f, 0.16f);
        private static readonly Color GoodStrong = new Color(0.25f, 0.85f, 0.35f, 0.40f);
        private static readonly Color Bad = new Color(0.90f, 0.20f, 0.20f, 0.40f);
        private static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.10f);

        // Touch layout: the panel moves left to clear the on-screen button column (TouchControlsUI).
        private const float DesktopRightInset = 30f;
        private const float TouchRightInset = 16f;
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
            public Image Extra;     // the ExtraSlotButton, when one is set
        }

        /// <summary>
        /// A small button a debug mode adds to the corner of every equipment slot (the gauntlet's
        /// "replace with a random rare"). Set it before the panel is built. Clicks on it never
        /// reach the slot under it; <see cref="Enabled"/> greys it out when it can't be used.
        /// </summary>
        public sealed class SlotButton
        {
            public string Label;
            public string Tooltip;
            public System.Func<EquipSlot, bool> Enabled;
            public System.Action<EquipSlot> Clicked;
        }

        public static SlotButton ExtraSlotButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetExtraSlotButton() => ExtraSlotButton = null;

        private struct Hover
        {
            public Vector2 Screen;
            public SlotView Slot;
            public SlotView Extra;   // over the slot's ExtraSlotButton
            public bool OverGrid;
            public bool OverStash;
            public int Potion;       // 1 health slot, 2 mana slot, 0 neither
            public Vector2 StashPos; // in stash cells, origin at its top-left
            public Vector2 GridPos; // in cells, origin at the grid's top-left
        }

        private readonly List<SlotView> slotViews = new List<SlotView>();
        private ItemData navigationItem;
        private bool navigationEquipment;
        private Vector2 navigationCrossing;

        private struct NavigationTarget
        {
            public ItemData Item;
            public bool Equipment;
            public Hover Hover;
            public Vector2 Center;
        }

        private List<NavigationTarget> NavigationTargets()
        {
            var targets = new List<NavigationTarget>();
            foreach (PlacedItem placed in inventory.Grid.Items)
            {
                Vector2 cell = new Vector2(placed.X + placed.Item.Width * 0.5f, placed.Y + placed.Item.Height * 0.5f);
                Vector3 local = new Vector3(gridArea.rect.xMin + cell.x * cellSize, gridArea.rect.yMax - cell.y * cellSize);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, gridArea.TransformPoint(local));
                targets.Add(new NavigationTarget { Item = placed.Item, Center = screen,
                    Hover = new Hover { OverGrid = true, GridPos = cell, Screen = screen } });
            }
            foreach (SlotView slot in slotViews)
            {
                ItemData item = inventory.Equipment.Get(slot.Slot);
                if (item == null || !slot.Rect.gameObject.activeInHierarchy) continue;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, slot.Rect.TransformPoint(slot.Rect.rect.center));
                targets.Add(new NavigationTarget { Item = item, Equipment = true, Center = screen,
                    Hover = new Hover { Slot = slot, Screen = screen } });
            }
            return targets;
        }

        /// <summary>Inspect items with the mobile directional buttons without moving gear.</summary>
        public void Navigate(Vector2 direction)
        {
            if (!isOpen || !TouchMode.Active || SpectatorMirror.Active || cursorItem != null) return;
            Canvas.ForceUpdateCanvases();
            List<NavigationTarget> targets = NavigationTargets();
            if (targets.Count == 0) return;
            int current = targets.FindIndex(t => t.Item == navigationItem && t.Equipment == navigationEquipment);
            if (current < 0)
            {
                // First occupied inventory position, scanning top to bottom, left to right.
                int first = -1;
                float firstCell = float.MaxValue;
                for (int k = 0; k < targets.Count; k++)
                {
                    if (targets[k].Equipment) continue;
                    PlacedItem placed = inventory.Grid.GetAt((int)targets[k].Hover.GridPos.x, (int)targets[k].Hover.GridPos.y);
                    float cell = placed.Y * inventory.Grid.Width + placed.X;
                    if (cell < firstCell) { firstCell = cell; first = k; }
                }
                SelectNavigation(targets[first >= 0 ? first : 0]);
                return;
            }

            NavigationTarget origin = targets[current];
            int next = FindNavigationTarget(targets, current, direction, origin.Equipment);
            if (next >= 0)
            {
                SelectNavigation(targets[next]);
                return;
            }

            bool crossing = origin.Equipment ? direction == Vector2.down : direction == Vector2.up;
            next = crossing ? FindNavigationTarget(targets, current, direction, !origin.Equipment) : -1;
            if (next < 0) { navigationCrossing = Vector2.zero; return; }
            if (navigationCrossing != direction)
            {
                navigationCrossing = direction;
                return;
            }
            SelectNavigation(targets[next]);
        }

        private static int FindNavigationTarget(List<NavigationTarget> targets, int current, Vector2 direction, bool equipment)
        {
            int best = -1;
            float score = float.MaxValue;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            for (int k = 0; k < targets.Count; k++)
            {
                if (k == current || targets[k].Equipment != equipment) continue;
                Vector2 delta = targets[k].Center - targets[current].Center;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 1f) continue;
                float candidate = forward + Mathf.Abs(Vector2.Dot(delta, perpendicular)) * 3f;
                if (candidate < score) { score = candidate; best = k; }
            }
            return best;
        }

        private void SelectNavigation(NavigationTarget target)
        {
            navigationItem = target.Item;
            navigationEquipment = target.Equipment;
            navigationCrossing = Vector2.zero;
            ShowNavigation();
        }

        private void ShowNavigation()
        {
            if (navigationItem == null) return;
            foreach (NavigationTarget target in NavigationTargets())
            {
                if (target.Item != navigationItem || target.Equipment != navigationEquipment) continue;
                report = target.Hover;
                reportPointer = target.Center;
                UpdateHighlights(target.Hover);
                UpdateTooltip(target.Hover, target.Center);
                return;
            }
            navigationItem = null;
            navigationCrossing = Vector2.zero;
            tooltipRect.gameObject.SetActive(false);
            UpdateHighlights(new Hover());
        }
        private readonly List<GameObject> itemViews = new List<GameObject>();

        private Canvas canvas;
        private Canvas tooltipCanvas;
        private RectTransform mobileSelection;
        private readonly Vector3[] selectionCorners = new Vector3[4];
        private RectTransform inventoryClose;
        private CanvasGroup canvasGroup;
        private RectTransform panel;
        private float regularPanelHeight;
        private RectTransform previewPanel;
        private RectTransform stashPanel;
        private float panelWidth;
        private int layoutScreenWidth;
        private int layoutScreenHeight;
        private float layoutCanvasScale;
        private bool layoutTouchMode;
        private readonly RectTransform[] potionSlots = new RectTransform[2];
        private readonly Text[] potionCounts = new Text[2];
        private readonly Image[] potionOverlays = new Image[2];
        private RectTransform stashArea;
        private RectTransform stashItems;
        private Image stashHighlight;
        private bool stashOpen;
        private Transform stashAt;
        private VendorStock vendor;     // trading: the side panel shows this trader's goods instead of the stash
        private Text sideTitle;
        private Text sideNote;
        private float noteUntil;
        private const float NoteHeight = 30f;

        // The grid in the side panel: the open trader's goods, or the stash.
        private InventoryGrid SideGrid => vendor != null ? vendor.Grid : inventory.Stash;
        private const float StashCell = 44f;
        private const float TabRowHeight = 34f;
        private RectTransform stashTabRow;
        private Image[] stashTabButtons;
        private Text[] stashTabLabels;
        private RectTransform stashSearchRow;
        private string stashQuery = "";
        private const float SearchHeight = 40f;
        private static readonly Color SearchHitLabel = new Color(1f, 0.95f, 0.62f, 1f);
        private static readonly Color SearchMissLabel = new Color(0.62f, 0.59f, 0.52f, 0.4f);
        private const float StashReach = 5f;
        private RectTransform gridArea;
        private RectTransform gridItems;
        private RectTransform tooltipRect;
        private RectTransform cursorView;
        private Image gridHighlight;
        private Text tooltipText;
        private RectTransform heldTooltipRect;
        private Text heldTooltipText;
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

        // What the pointer was on this frame, reported to spectators (see GetPointerReport).
        private Hover report;
        private Vector2 reportPointer;

        // Spectator mirror (see SpectatorMirror): which side panel is showing.
        private int mirrorSide;

        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>The item lying on the ground under the mouse (set by the player's loot picker), or null.</summary>
        public static ItemData GroundHover { get; set; }

        /// <summary>Where <see cref="GroundHover"/> lies (a phone's tooltip sits beside the item, not a finger).</summary>
        public static Vector3 GroundHoverAt { get; set; }

        /// <summary>An item is on the cursor (so a click outside the panel throws it, rather than attacking).</summary>
        public bool IsHoldingItem => cursorItem != null;

        /// <summary>Set before Start to omit the preview in an isolated diagnostic session.</summary>
        public bool EnableCharacterPreview { get; set; } = true;

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
            bool hasPreview = false;
            if (EnableCharacterPreview)
            {
                preview = gameObject.AddComponent<CharacterPreview>();
                EquipmentVisuals source = inventory.GetComponentInChildren<EquipmentVisuals>(true);
                hasPreview = preview.Build(source, inventory.Equipment);
            }

            BuildUI(hasPreview);
            StartCoroutine(Prewarm());

            inventory.PlayerDied += OnPlayerDied;
            inventory.Grid.Changed += OnGridChanged;
            inventory.StatsChanged += OnGridChanged;
            inventory.PotionsChanged += OnGridChanged;
            inventory.StashTabNamesChanged += RefreshStashTabs;
            inventory.GoldChanged += RefreshGold;
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.PlayerDied -= OnPlayerDied;
                inventory.Grid.Changed -= OnGridChanged;
                inventory.StatsChanged -= OnGridChanged;
                inventory.PotionsChanged -= OnGridChanged;
                inventory.StashTabNamesChanged -= RefreshStashTabs;
                inventory.GoldChanged -= RefreshGold;
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
            UpdateTooltipCanvasOrder();
            if (mobileSelection != null && (!isOpen || !TouchMode.Active)) mobileSelection.gameObject.SetActive(false);
            if (isOpen && (layoutScreenWidth != Screen.width || layoutScreenHeight != Screen.height ||
                !Mathf.Approximately(layoutCanvasScale, canvas.scaleFactor) || layoutTouchMode != TouchMode.Active))
                ApplyPanelLayout();
            if (warming)
                return;

            if (UiKit.IsTextPromptOpen)
                return;

            if (SpectatorMirror.Active)
            {
                UpdateMirror();
                return;
            }

            report = new Hover();
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
            {
                UpdateGroundTooltip();
                return;
            }

            if (gridDirty)
                Refresh();

            if (stashOpen && (stashAt == null || FlatDistance(stashAt.position, inventory.transform.position) > StashReach))
                CloseStash();
            if (stashOpen)
                UpdateNote();

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

            RefreshExtraButtons();
            Hover hover = Hit(mousePos);
            report = hover;
            reportPointer = mousePos;
            UpdateHighlights(hover);
            UpdateTooltip(hover, mousePos);
            ShowHeldTooltip(mousePos);
            if (!tooltipRect.gameObject.activeSelf)
                UpdateGroundTooltip();

            // The click that opened the stash or a trader (on the chest, the NPC) isn't also a
            // click on whatever item the window happens to open under the cursor.
            if (mouse.leftButton.wasPressedThisFrame && Time.frameCount != sideOpenedFrame)
                HandleClick(hover);
            else if (mouse.rightButton.wasPressedThisFrame)
                UseConsumable(hover);
        }

        private void UpdateTooltipCanvasOrder()
        {
            if (tooltipCanvas == null) return;

            int order = 820; // Above loot, menus, and desktop chat.
            if (SpectatorMirror.Active)
                order = 946; // Spectator chat is 945; name prompt and loading UI stay above this.
            else if (TouchMode.Active)
                order = 810; // Open touch chat is 800.

            if (tooltipCanvas.sortingOrder != order)
                tooltipCanvas.sortingOrder = order;
        }

        // ------------------------------------------------------------------ spectators

        /// <summary>
        /// For the spectator stream: what the pointer is on (a SpectatorMirror.Hover* kind, the
        /// equip/potion slot, the grid cell) and where it is as a fraction of the screen.
        /// </summary>
        public void GetPointerReport(out int kind, out int index, out Vector2 cell, out Vector2 pointer)
        {
            kind = SpectatorMirror.HoverNone;
            index = 0;
            cell = Vector2.zero;
            pointer = new Vector2(reportPointer.x / Mathf.Max(1, Screen.width), reportPointer.y / Mathf.Max(1, Screen.height));
            if (!isOpen)
                return;

            if (report.Potion > 0)
            {
                kind = SpectatorMirror.HoverPotion;
                index = report.Potion;
            }
            else if (report.Slot != null)
            {
                kind = SpectatorMirror.HoverSlot;
                index = (int)report.Slot.Slot;
            }
            else if (report.OverGrid)
            {
                kind = SpectatorMirror.HoverBag;
                cell = report.GridPos;
            }
            else if (report.OverStash)
            {
                kind = SpectatorMirror.HoverSide;
                cell = report.StashPos;
            }
        }

        /// <summary>The item on the cursor, or null.</summary>
        public ItemData HeldItem => cursorItem;

        /// <summary>Which side panel is showing (SpectatorMirror.Side*), and its goods/contents.</summary>
        public int SideMode => !isOpen || !stashOpen ? SpectatorMirror.SideNone : vendor != null ? SpectatorMirror.SideTrader : SpectatorMirror.SideStash;
        public InventoryGrid SideContents => SideMode == SpectatorMirror.SideNone ? null : SideGrid;
        public string SideName => vendor != null ? vendor.Name : null;

        // A spectator's copy: open while the player has it open (showing the same side panel) or
        // while the spectator opened it themselves; nothing can be moved; the tooltip follows the
        // player's pointer until the spectator moves their own (SpectatorMirror.FollowingPlayer).
        private void UpdateMirror()
        {
            // The player may switch to a tab that looks the same (both empty): keep the highlight on theirs.
            if (stashOpen)
                RefreshStashTabs();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.iKey.wasPressedThisFrame)
                    SpectatorMirror.Toggle(SpectatorMirror.Menu.Inventory);
                else if (keyboard.escapeKey.wasPressedThisFrame)
                    SpectatorMirror.Close(SpectatorMirror.Menu.Inventory);
            }

            bool open = SpectatorMirror.Shown(SpectatorMirror.Menu.Inventory);
            bool remote = SpectatorMirror.ShowsRemote(SpectatorMirror.Menu.Inventory);
            int side = remote ? SpectatorMirror.Side : SpectatorMirror.SideNone;
            VendorStock trader = side == SpectatorMirror.SideTrader ? SpectatorMirror.Trader : null;
            if (side == SpectatorMirror.SideTrader && trader == null)
                side = SpectatorMirror.SideNone;

            if (open != isOpen || (open && (side != mirrorSide || trader != vendor)))
            {
                stashOpen = side != SpectatorMirror.SideNone;
                vendor = trader;
                stashAt = null;
                if (stashOpen)
                    sideTitle.text = trader != null ? trader.Name.ToUpperInvariant() : "STASH";
                SetOpen(open);
                mirrorSide = open ? side : SpectatorMirror.SideNone;
            }

            if (cursorItem != SpectatorMirror.Held)
            {
                cursorItem = SpectatorMirror.Held;
                gridDirty = true;
            }

            if (!isOpen)
            {
                heldTooltipRect.gameObject.SetActive(false);
                UpdateGroundTooltip(); // the spectator's own pointer on an item on the ground
                return;
            }

            if (gridDirty)
                Refresh();
            if (stashOpen)
                UpdateNote();

            Hover remoteHover = RemoteHover();
            Hover hover = remoteHover;
            if (!remote || !SpectatorMirror.FollowingPlayer)
            {
                Mouse mouse = Mouse.current;
                Touchscreen touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.isPressed)
                    hover = Hit(touch.primaryTouch.position.ReadValue());
                else if (mouse != null && !TouchMode.Active)
                    hover = Hit(mouse.position.ReadValue());
                else
                    hover = new Hover { Screen = remoteHover.Screen };
            }

            // The held item stays on the player's pointer, wherever the spectator's is.
            if (cursorView != null)
                cursorView.position = remoteHover.Screen;

            UpdateHighlights(hover);
            UpdateTooltip(hover, hover.Screen);
            ShowHeldTooltip(remoteHover.Screen);
            if (!tooltipRect.gameObject.activeSelf)
                UpdateGroundTooltip();
        }

        // The player's pointer, in this screen's layout: on the same slot or cell when it's over
        // the panels (the two screens can be different sizes), else at the same screen fraction.
        private Hover RemoteHover()
        {
            var h = new Hover();
            Vector2 cell = SpectatorMirror.HoverCell;
            Vector2 fraction = SpectatorMirror.Pointer;
            h.Screen = new Vector2(fraction.x * Screen.width, fraction.y * Screen.height);
            if (!SpectatorMirror.InventoryOpen)
                return h;

            switch (SpectatorMirror.HoverKind)
            {
                case SpectatorMirror.HoverPotion:
                    int potion = SpectatorMirror.HoverIndex;
                    if (potion == 1 || potion == 2)
                    {
                        h.Potion = potion;
                        h.Screen = ScreenCentre(potionSlots[potion - 1]);
                    }
                    break;
                case SpectatorMirror.HoverSlot:
                    foreach (SlotView s in slotViews)
                    {
                        if ((int)s.Slot == SpectatorMirror.HoverIndex)
                        {
                            h.Slot = s;
                            h.Screen = ScreenCentre(s.Rect);
                        }
                    }
                    break;
                case SpectatorMirror.HoverBag:
                    h.OverGrid = true;
                    h.GridPos = cell;
                    h.Screen = GridScreenPoint(gridArea, cell, cellSize);
                    break;
                case SpectatorMirror.HoverSide:
                    if (stashOpen)
                    {
                        h.OverStash = true;
                        h.StashPos = cell;
                        h.Screen = GridScreenPoint(stashArea, cell, StashCell);
                    }
                    break;
            }
            return h;
        }

        private static Vector2 ScreenCentre(RectTransform rt)
        {
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        }

        private static Vector2 GridScreenPoint(RectTransform area, Vector2 cell, float size)
        {
            Rect r = area.rect;
            Vector3 local = new Vector3(r.xMin + cell.x * size, r.yMax - cell.y * size, 0f);
            return RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(local));
        }

        /// <summary>The spectator copy's contents changed (SpectatorReplica): redraw.</summary>
        public void MarkDirty()
        {
            gridDirty = true;
        }

        // Follows one finger. With nothing held: a tap picks the item up (it then waits where it
        // was until the next tap puts it down), dragging picks it up and drops it where the finger
        // lifts, and holding still shows the tooltip without picking anything up. With an item
        // already held, lifting the finger puts it down there, whether that was a tap or a drag.
        private void UpdateTouch()
        {
            if (!touchTracking) ShowNavigation();
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            TouchControl touch = screen.primaryTouch;
            Vector2 pos = touch.position.ReadValue();

            if (touch.press.wasPressedThisFrame)
            {
                touchTracking = !TouchMode.IsOverBlocker(pos) && Time.frameCount != sideOpenedFrame;
                if (touchTracking)
                {
                    navigationItem = null;
                    navigationCrossing = Vector2.zero;
                    tooltipRect.gameObject.SetActive(false);
                }
                touchHadItem = cursorItem != null;
                touchPickupTried = false;
                touchInspecting = false;
                touchStart = pos;
                touchStartTime = Time.unscaledTime;
            }

            if (touchTracking)
                lastTouchPos = pos;
            reportPointer = lastTouchPos;

            if (cursorView != null)
                cursorView.position = lastTouchPos;

            // The held item's own stats stay up the whole time it's on the finger, even between
            // the "tap to pick up" and "tap to place" of a two-tap move, when nothing is touching
            // the screen at all - so this runs before the no-touch early-out below.
            ShowHeldTooltip(lastTouchPos);

            if (!touchTracking)
            {
                if (navigationItem != null) ShowNavigation();
                else UpdateHighlights(new Hover());
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
                if (touchInspecting)
                {
                    report = Hit(touchStart);
                    reportPointer = touchStart;
                }
            }

            Hover dragHover = cursorItem != null && touch.press.isPressed ? Hit(pos) : new Hover();
            if (cursorItem != null && touch.press.isPressed)
                report = dragHover;
            UpdateHighlights(touchInspecting && cursorItem == null ? Hit(touchStart) : dragHover);
            // Whatever the held item would swap with, shown alongside the held tooltip above.
            if (cursorItem != null && touch.press.isPressed)
                UpdateTooltip(dragHover, pos);

            if (!touch.press.wasReleasedThisFrame && touch.press.isPressed)
                return;

            touchTracking = false;
            tooltipRect.gameObject.SetActive(false);
            heldTooltipRect.gameObject.SetActive(false);

            bool dragPickedUp = touchPickupTried && cursorItem != null;
            bool tap = !moved && !held;

            if (!touchHadItem && !touchPickupTried && !moved && Time.unscaledTime - touchStartTime >= 1f)
                UseConsumable(Hit(touchStart));
            else if (touchHadItem || dragPickedUp || (tap && !touchInspecting))
                HandleClick(Hit(pos));
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>Opens or closes the panel (I key, or the on-screen bag button on touch).</summary>
        // The panel's X: a spectator closes only the copy they opened themselves.
        private void Close()
        {
            if (SpectatorMirror.Active)
                SpectatorMirror.Close(SpectatorMirror.Menu.Inventory);
            else
                SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (open != isOpen)
            {
                navigationItem = null;
                navigationCrossing = Vector2.zero;
            }
            isOpen = open;
            touchTracking = false;
            bool touch = TouchMode.Active;

            if (!open)
            {
                stashOpen = false;
                vendor = null;
                tooltipRect.gameObject.SetActive(false);
                if (mobileSelection != null) mobileSelection.gameObject.SetActive(false);
                heldTooltipRect.gameObject.SetActive(false);
            }

            panel.gameObject.SetActive(open);
            inventoryClose.gameObject.SetActive(open);
            ApplyPanelLayout();

            // No room for the character preview beside the panel on a phone.
            previewPanel.gameObject.SetActive(open && !touch && !stashOpen);
            stashPanel.gameObject.SetActive(open && stashOpen);
            if (stashTabRow != null)
                stashTabRow.gameObject.SetActive(open && stashOpen && vendor == null);
            if (stashSearchRow != null)
                stashSearchRow.gameObject.SetActive(open && stashOpen && vendor == null);

            if (preview != null)
                preview.SetActive(open && !touch && !stashOpen);

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
                AudioManager.Instance.PlayUI(toggleClip);
            }
        }

        // ------------------------------------------------------------------ stash

        /// <summary>
        /// Opens the stash (the chest in Haven) beside the bag, in place of the character preview.
        /// It stays open until the bag closes or the player walks away from the chest.
        /// </summary>
        public void OpenStash(Transform chest)
        {
            if (inventory == null || inventory.IsPlayerDead)
                return;
            vendor = null;
            sideTitle.text = "STASH";
            stashAt = chest;
            stashOpen = true;
            sideOpenedFrame = Time.frameCount;
            SetNote(null);
            if (!isOpen)
                SetOpen(true);
            else
                SetOpen(true); // re-applies which side panel shows
        }

        /// <summary>
        /// Trading, PoE style: the trader's goods beside the bag. Click one to buy it; Ctrl+click
        /// something in the bag (or put a held item down on the trader's side) to sell it. Prices
        /// show on the tooltips. Closes like the stash: with the bag, or by walking away.
        /// </summary>
        public void OpenTrade(Transform trader, VendorStock stock)
        {
            if (inventory == null || inventory.IsPlayerDead || stock == null)
                return;
            vendor = stock;
            sideTitle.text = stock.Name.ToUpperInvariant();
            stashAt = trader;
            stashOpen = true;
            sideOpenedFrame = Time.frameCount;
            SetNote(null);
            SetOpen(true);
        }

        public bool IsTrading => isOpen && stashOpen && vendor != null;

        // The frame the stash or trader window last opened (its opening click is ignored).
        private int sideOpenedFrame = -1;

        public void CloseStash()
        {
            if (!stashOpen)
                return;
            // The side panel and bag are one window. Leaving its interaction range closes both.
            SetOpen(false);
        }

        public bool IsStashOpen => isOpen && stashOpen;

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // Tab names and which one is showing (a spectator's follow the player's).
        private void RefreshStashTabs()
        {
            if (stashTabButtons == null || inventory == null)
                return;
            bool searching = IsSearchingStash;
            for (int k = 0; k < stashTabButtons.Length; k++)
            {
                bool current = k == inventory.StashTab;
                bool hit = searching && TabHasMatch(inventory.StashTabs[k]);
                stashTabButtons[k].color = current ? UiKit.Gold * (searching && !hit ? 0.3f : 0.6f)
                    : hit ? new Color(0.30f, 0.26f, 0.12f, 1f) : CellColor;
                stashTabLabels[k].color = !searching ? UiKit.TextColor : hit ? SearchHitLabel : SearchMissLabel;
                string name = inventory.StashTabName(k);
                stashTabLabels[k].text = name.Length > 6 ? name.Substring(0, 6) + ".." : name;
            }
        }

        private bool IsSearchingStash => stashQuery.Length > 0 && vendor == null;

        private bool MatchesStashSearch(ItemData item)
        {
            return UiKit.SearchMatches(BuildTooltipText(item, out _), stashQuery);
        }

        private bool TabHasMatch(InventoryGrid tab)
        {
            foreach (PlacedItem p in tab.Items)
                if (MatchesStashSearch(p.Item))
                    return true;
            return false;
        }

        private void BeginRenameTab(int tab)
        {
            if (!isOpen || !stashOpen || vendor != null || UiKit.IsTextPromptOpen)
                return;
            UiKit.TextPrompt?.Invoke("Name your stash tab", inventory.StashTabCustomName(tab), PlayerInventory.StashTabNameLimit, true, name => inventory.RenameStashTab(tab, name));
        }

        private void ShowStashHighlight(int x, int y, int w, int h, Color color)
        {
            stashHighlight.enabled = true;
            stashHighlight.color = color;
            UiKit.TopLeft(stashHighlight.rectTransform, StashCellPos(x, y), StashCellSize(w, h));
        }

        // The line at the foot of the side panel: a trading message for a few seconds, else the purse.
        private void SetNote(string message)
        {
            noteUntil = message != null ? Time.unscaledTime + 3f : 0f;
            if (message != null)
                sideNote.text = message;
            UpdateNote();
        }

        private void UpdateNote()
        {
            if (sideNote == null || Time.unscaledTime < noteUntil)
                return;
            sideNote.text = vendor == null
                ? (TouchMode.Active ? "Drag items between the stash and your bag" : "Ctrl+click moves items between the stash and your bag")
                : "<color=#FFD34D>Gold: " + inventory.Gold + (TouchMode.Active ? "</color>   Tap to buy - drop here to sell" : "</color>   Click to buy  -  Ctrl+click or drop here to sell");
        }

        private static string Coloured(ItemData item)
        {
            return "<color=#" + UiKit.Hex(UiKit.RarityColor(item.Rarity)) + ">" + item.Name + "</color>";
        }

        private void Buy(ItemData item)
        {
            int price = Vendors.BuyPrice(item);
            if (inventory.Gold < price)
            {
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                SetNote("<color=#FF7060>Not enough gold (" + price + ")</color>");
                return;
            }
            vendor.Grid.Remove(item);
            if (!inventory.Grid.TryAutoPlace(item))
            {
                vendor.Grid.TryAutoPlace(item);
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                SetNote("<color=#FF7060>No room in your bag</color>");
                return;
            }
            inventory.TrySpendGold(price);
            PlayUISound(ItemSounds.Pickup(item));
            SetNote("Bought " + Coloured(item) + " for " + price + " gold");
        }

        // The trader takes it (and keeps it for sale, so a mistake can be bought back).
        private void Sell(ItemData item)
        {
            int price = Vendors.SellPrice(item);
            inventory.AddGold(price);
            vendor.Grid.TryAutoPlace(item);
            PlayUISound(ItemSounds.Place(item));
            SetNote("Sold " + Coloured(item) + " for " + price + " gold");
        }

        private void ClickStash(Vector2 pos)
        {
            if (vendor != null)
            {
                if (cursorItem != null)
                {
                    ItemData held = cursorItem;
                    cursorItem = null;
                    Sell(held);
                    return;
                }
                PlacedItem forSale = vendor.Grid.GetAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
                if (forSale != null)
                    Buy(forSale.Item);
                return;
            }

            InventoryGrid stash = inventory.Stash;
            if (cursorItem == null)
            {
                PlacedItem p = stash.GetAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
                if (p != null && stash.Remove(p.Item))
                {
                    cursorItem = p.Item;
                    PlayUISound(ItemSounds.Pickup(cursorItem));
                }
                return;
            }

            Vector2Int o = FootprintOrigin(cursorItem, pos, stash);
            if (stash.TryPlaceOrSwap(cursorItem, o.x, o.y, out ItemData replaced))
            {
                PlayUISound(ItemSounds.Place(cursorItem));
                cursorItem = replaced;
            }
            else
            {
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
            }
        }

        // Ctrl+click: an item jumps straight between the bag and the open stash.
        private bool TryQuickMove(Hover h)
        {
            Keyboard keyboard = Keyboard.current;
            bool ctrl = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            if (!ctrl || !stashOpen || cursorItem != null)
                return false;

            InventoryGrid from = h.OverGrid ? inventory.Grid : h.OverStash ? SideGrid : null;
            InventoryGrid to = h.OverGrid ? SideGrid : inventory.Grid;
            if (from == null)
                return false;
            Vector2 pos = h.OverGrid ? h.GridPos : h.StashPos;
            PlacedItem p = from.GetAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            if (p == null)
                return false;

            if (vendor != null)
            {
                if (h.OverGrid)
                {
                    inventory.Grid.Remove(p.Item);
                    Sell(p.Item);
                }
                else
                {
                    Buy(p.Item);
                }
                return true;
            }

            if (!to.TryAutoPlace(p.Item))
            {
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                return true;
            }
            from.Remove(p.Item);
            PlayUISound(ItemSounds.Place(p.Item));
            return true;
        }

private Vector2 CellSize(int w, int h)
        {
            return new Vector2(w * cellSize - Gap, h * cellSize - Gap);
        }

        private Text goldText;

        private void RefreshGold()
        {
            if (goldText != null)
                goldText.text = inventory.Gold.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void RefreshPotions()
        {
            for (int k = 0; k < 2; k++)
            {
                int n = inventory.Potions(k == 0);
                potionCounts[k].text = n.ToString();
                potionCounts[k].color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                potionSlots[k].Find("Icon").GetComponent<Image>().color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }
        }

        private static string PotionTooltip(bool health, int count)
        {
            string name = health ? "Health Potion" : "Mana Potion";
            string what = health ? "Heals 40% of your life\nover 5 seconds" : "Restores 40% of maximum mana\nover 5 seconds";
            return "<b>" + name + "</b>\n<color=#" + UiKit.Hex(UiKit.DimText) + ">Potion   " + count + " / " + PlayerInventory.MaxPotions +
                   "</color>\n\n<color=#" + UiKit.Hex(UiKit.MagicBlue) + ">" + what + "</color>\n\n<color=#" + UiKit.Hex(UiKit.DimText) +
                   ">Click or press " + (health ? "1" : "2") + " to drink.\nOnly potions go here.</color>";
        }

        private bool ExtraButtonEnabled(SlotView s) =>
            ExtraSlotButton != null && (ExtraSlotButton.Enabled == null || ExtraSlotButton.Enabled(s.Slot));

        private void RefreshExtraButtons()
        {
            foreach (SlotView s in slotViews)
                if (s.Extra != null)
                {
                    s.Extra.color = ExtraButtonEnabled(s) ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.6f);
                    s.Extra.transform.SetAsLastSibling();
                }
        }

        private static readonly Color TooltipColor = new Color(0.075f, 0.065f, 0.06f, 0.97f);

        // Something landed in the bag from outside this screen (a picked-up drop): redraw once,
        // next frame, rather than on every change of a click that redraws anyway.
        private void OnGridChanged()
        {
            gridDirty = true;
        }

        private void Refresh()
        {
            gridDirty = false;
            RefreshStashTabs();

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

            RefreshPotions();
            RefreshGold();

            // Items in the stash.
            if (stashOpen)
            {
                bool searching = IsSearchingStash;
                foreach (PlacedItem p in SideGrid.Items)
                {
                    RectTransform rt = CreateItemView(stashItems, p.Item, StashCellSize(p.Item.Width, p.Item.Height), 1f);
                    rt.anchoredPosition = StashCellPos(p.X, p.Y);
                    if (searching && !MatchesStashSearch(p.Item))
                        UiKit.Stretch(UiKit.NewImage("SearchMiss", rt, new Color(0.03f, 0.03f, 0.04f, 0.78f)).rectTransform, 0f);
                    itemViews.Add(rt.gameObject);
                }
            }

            // Items being worn.
            foreach (SlotView s in slotViews)
            {
                ItemData item = inventory.Equipment.Get(s.Slot);
                s.Hint.enabled = item == null;

                if (item != null)
                {
                    Vector2 size = s.Rect.sizeDelta - new Vector2(4f, 4f);
                    RectTransform rt = CreateItemView(s.Rect, item, size, 1f, CellSize(item.Width, item.Height));
                    if (!inventory.Equipment.IsActive(s.Slot))
                    {
                        rt.Find("Frame").GetComponent<Image>().color = new Color(1f, 0.15f, 0.12f);
                        rt.Find("Frame/Face").GetComponent<Image>().color = new Color(0.32f, 0.04f, 0.04f, 0.95f);
                        rt.Find("Icon").GetComponent<Image>().color = new Color(1f, 0.28f, 0.25f);
                    }
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
                // Keep the held item's footprint identical to its inventory footprint. The
                // cursor position changes, but picking the item up must not resize it.
                cursorView = CreateItemView(canvas.transform, cursorItem, CellSize(cursorItem.Width, cursorItem.Height), 1f);
                cursorView.localScale = panel.localScale;
                cursorView.pivot = new Vector2(0.5f, 0.5f);
                cursorView.gameObject.SetActive(isOpen);
                cursorView.SetAsLastSibling();
            }
        }

        // ------------------------------------------------------------------ hit testing

        private Hover Hit(Vector2 screenPos)
        {
            Hover h = new Hover { Screen = screenPos };

            for (int k = 0; k < 2; k++)
            {
                if (potionSlots[k] != null && potionSlots[k].gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(potionSlots[k], screenPos, null))
                {
                    h.Potion = k + 1;
                    return h;
                }
            }

            foreach (SlotView s in slotViews)
            {
                if (s.Extra != null && s.Rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(s.Extra.rectTransform, screenPos, null))
                {
                    h.Extra = s;
                    return h;
                }
                if (s.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(s.Rect, screenPos, null))
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

            if (stashOpen && !h.OverGrid &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stashArea, screenPos, null, out local))
            {
                Rect r = stashArea.rect;
                float px = local.x - r.xMin;
                float py = r.yMax - local.y;
                if (px >= 0f && py >= 0f && px < r.width && py < r.height)
                {
                    h.OverStash = true;
                    h.StashPos = new Vector2(px / StashCell, py / StashCell);
                }
            }

            return h;
        }

        // Where an item's top-left cell lands when its centre is under the cursor (kept inside the grid).
        private Vector2Int FootprintOrigin(ItemData item, Vector2 pos)
        {
            return FootprintOrigin(item, pos, inventory.Grid);
        }

        private static Vector2Int FootprintOrigin(ItemData item, Vector2 pos, InventoryGrid grid)
        {
            int x = Mathf.RoundToInt(pos.x - item.Width * 0.5f);
            int y = Mathf.RoundToInt(pos.y - item.Height * 0.5f);
            x = Mathf.Clamp(x, 0, Mathf.Max(0, grid.Width - item.Width));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, grid.Height - item.Height));
            return new Vector2Int(x, y);
        }

        // ------------------------------------------------------------------ feedback

        private void UpdateHighlights(Hover h)
        {
            UpdateSlotHighlights(h);
            if (mobileSelection == null) return;
            RectTransform source = null;
            if (TouchMode.Active && isOpen)
            {
                if (h.Slot != null && inventory.Equipment.Get(h.Slot.Slot) != null) source = h.Slot.Rect;
                else if (h.OverGrid && gridHighlight.enabled) source = gridHighlight.rectTransform;
                else if (h.OverStash && stashHighlight.enabled) source = stashHighlight.rectTransform;
            }
            mobileSelection.gameObject.SetActive(source != null);
            if (source == null) return;
            source.GetWorldCorners(selectionCorners);
            var parent = (RectTransform)mobileSelection.parent;
            Vector2 bottom = parent.InverseTransformPoint(selectionCorners[0]);
            Vector2 top = parent.InverseTransformPoint(selectionCorners[2]);
            mobileSelection.anchoredPosition = (bottom + top) * 0.5f;
            mobileSelection.sizeDelta = top - bottom;
        }

        private void UpdateSlotHighlights(Hover h)
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

            for (int k = 0; k < 2; k++)
                potionOverlays[k].color = h.Potion == k + 1 ? (cursorItem != null ? Bad : HoverTint) : Color.clear;

            stashHighlight.enabled = false;
            if (h.OverStash)
            {
                if (cursorItem != null)
                {
                    Vector2Int so = FootprintOrigin(cursorItem, h.StashPos, SideGrid);
                    bool fits = vendor != null || SideGrid.InBounds(cursorItem, so.x, so.y) &&
                                SideGrid.GetOverlapping(cursorItem, so.x, so.y).Count <= 1;
                    ShowStashHighlight(so.x, so.y, cursorItem.Width, cursorItem.Height, fits ? GoodStrong : Bad);
                }
                else
                {
                    PlacedItem sp = SideGrid.GetAt(Mathf.FloorToInt(h.StashPos.x), Mathf.FloorToInt(h.StashPos.y));
                    if (sp != null)
                        ShowStashHighlight(sp.X, sp.Y, sp.Item.Width, sp.Item.Height, HoverTint);
                }
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

        public static string BuildTooltipText(ItemData item, out int lineCount, StatSheet stats = null)
            => ItemTooltip.BuildTooltipText(item, out lineCount, stats);

        // The item lying on the ground under the mouse shows its stats too; on a phone, the item
        // nearest the player does, beside it (see LootPicker) - no holding a finger on it.
        private void UpdateGroundTooltip()
        {
            ItemData item = GroundHover;
            Mouse mouse = Mouse.current;
            Vector2 at = Vector2.zero;
            bool show = false;
            if (item != null && TouchMode.Active)
            {
                Camera cam = Camera.main;
                Vector3 screen = cam != null ? cam.WorldToScreenPoint(GroundHoverAt + Vector3.up * 0.6f) : Vector3.back;
                show = screen.z > 0f;
                at = screen;
            }
            else if (item != null && mouse != null)
            {
                show = true;
                at = mouse.position.ReadValue();
            }

            if (!show)
            {
                if (tooltipRect.gameObject.activeSelf)
                    tooltipRect.gameObject.SetActive(false);
                return;
            }
            ShowTooltip(item, at);
        }

        // Also while an item is held, so the one it would swap with can be read first.
        private void UpdateTooltip(Hover h, Vector2 mousePos)
        {
            if (h.Extra != null)
            {
                ShowTooltipText(ExtraSlotButton.Tooltip + (ExtraButtonEnabled(h.Extra) ? "" :
                    "\n<color=#" + UiKit.Hex(UiKit.DimText) + ">Not available right now.</color>"), 3, mousePos);
                return;
            }
            if (h.Potion > 0)
            {
                ShowTooltipText(PotionTooltip(h.Potion == 1, inventory.Potions(h.Potion == 1)), 8, mousePos);
                return;
            }

            ItemData item = null;

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
            else if (h.OverStash)
            {
                PlacedItem p = SideGrid.GetAt(Mathf.FloorToInt(h.StashPos.x), Mathf.FloorToInt(h.StashPos.y));
                if (p != null)
                    item = p.Item;
            }

            if (item == null)
            {
                tooltipRect.gameObject.SetActive(false);
                return;
            }

            if (vendor != null)
            {
                int lines;
                string text = BuildTooltipText(item, out lines, inventory.Stats);
                if (h.OverStash)
                {
                    int price = Vendors.BuyPrice(item);
                    string colour = inventory.Gold >= price ? "FFD34D" : "FF7060";
                    text += "\n\n<color=#" + colour + ">Price: " + price + " gold</color>";
                }
                else
                {
                    text += "\n\n<color=#FFD34D>Sells for " + Vendors.SellPrice(item) + " gold</color>";
                }
                ShowTooltipText(text, lines + 2, mousePos);
                return;
            }

            ShowTooltip(item, mousePos);
        }

        private void ShowTooltip(ItemData item, Vector2 mousePos)
        {
            int lines;
            string text = BuildTooltipText(item, out lines, inventory.Stats);
            foreach (EquipSlot slot in SlotRules.AllSlots)
                if (inventory.Equipment.Get(slot) == item && !inventory.Equipment.IsActive(slot))
                {
                    text += "\n<color=#FF5040>Requirements unmet: item stats and skills are inactive.</color>";
                    lines++;
                    break;
                }
            ShowTooltipText(text, lines, mousePos);
        }

        // The held item's own stats, visible the whole time it's on the cursor/finger - whether
        // it's over an empty spot or over another item (whose own tooltip is shown separately by
        // ShowTooltipText/UpdateTooltip), so a drag-swap can be compared at a glance.
        private void ShowHeldTooltip(Vector2 pos)
        {
            if (cursorItem == null)
            {
                heldTooltipRect.gameObject.SetActive(false);
                return;
            }

            bool touch = TouchMode.Active;
            int lines;
            heldTooltipText.fontSize = touch ? TouchMode.ReadingFontSize : 17;
            float lineHeight = touch ? TouchMode.ReadingFontSize * 1.2f : 23f;
            heldTooltipText.text = BuildTooltipText(cursorItem, out lines, inventory.Stats);
            heldTooltipRect.sizeDelta = FitTooltip(heldTooltipText, touch ? (TouchMode.Portrait ? 800f : 600f) : 270f, 24f + lines * lineHeight);
            heldTooltipRect.gameObject.SetActive(true);
            heldTooltipRect.SetAsLastSibling();

            Vector2 size = heldTooltipRect.sizeDelta * canvas.scaleFactor;

            if (touch)
            {
                // Above the finger - the hovered slot's own tooltip (ShowTooltipText) sits to its
                // left, so the two can show together without covering each other or the finger.
                float offset = 40f * canvas.scaleFactor;
                float x = Mathf.Clamp(pos.x, size.x * 0.5f, Screen.width - size.x * 0.5f);
                heldTooltipRect.pivot = new Vector2(0.5f, 0f);
                heldTooltipRect.position = new Vector2(x, Mathf.Min(pos.y + offset, Screen.height - size.y));
                ClampTooltip(heldTooltipRect);
                return;
            }

            // Below the held item icon (the hover tooltip for whatever it's over sits beside the
            // cursor instead, see ShowTooltipText), clamped so it never runs off the bottom.
            float iconHalfHeight = cursorView != null ? cursorView.sizeDelta.y * cursorView.localScale.y * 0.5f * canvas.scaleFactor : 0f;
            float gap = 18f + iconHalfHeight;
            bool flipY = pos.y - gap - size.y < 0f;
            heldTooltipRect.pivot = new Vector2(0f, flipY ? 0f : 1f);
            float px = Mathf.Min(pos.x, Screen.width - size.x);
            heldTooltipRect.position = new Vector2(px, pos.y + (flipY ? gap : -gap));
            ClampTooltip(heldTooltipRect);
        }

        public static void ClampTooltip(RectTransform rect) => ItemTooltip.ClampTooltip(rect);

        public static Vector2 FitTooltip(Text text, float width, float estimatedHeight)
            => ItemTooltip.FitTooltip(text, width, estimatedHeight);

        private void ShowTooltipText(string text, int lines, Vector2 mousePos)
        {
            bool touch = TouchMode.Active;
            tooltipText.fontSize = touch ? TouchMode.ReadingFontSize : 17;
            float lineHeight = touch ? TouchMode.ReadingFontSize * 1.2f : 23f;
            tooltipText.text = text;
            tooltipRect.sizeDelta = FitTooltip(tooltipText, touch ? (TouchMode.Portrait ? 800f : 600f) : 270f, 24f + lines * lineHeight);

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
                ClampTooltip(tooltipRect);
                return;
            }

            // Clear of the held item, which hangs centred on the cursor.
            float gap = 18f;
            if (cursorItem != null && cursorView != null)
                gap += cursorView.sizeDelta.x * cursorView.localScale.x * 0.5f * canvas.scaleFactor;

            bool flipX = mousePos.x + gap + size.x > Screen.width;
            bool flipY = mousePos.y - 18f - size.y < 0f;
            tooltipRect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
            tooltipRect.position = new Vector2(mousePos.x + (flipX ? -gap : gap), mousePos.y + (flipY ? 18f : -18f));
            ClampTooltip(tooltipRect);
        }

        // ------------------------------------------------------------------ clicking

        private void HandleClick(Hover h)
        {
            if (h.Extra != null)
            {
                if (cursorItem == null && ExtraButtonEnabled(h.Extra))
                {
                    ExtraSlotButton.Clicked?.Invoke(h.Extra.Slot);
                    PlayUISound(ItemSounds.Place(inventory.Equipment.Get(h.Extra.Slot)));
                }
                else
                    PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                return;
            }
            if (TryQuickMove(h))
            {
            }
            else if (h.Potion > 0)
            {
                if (cursorItem != null)
                    PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                else
                    inventory.ClickPotionSlot(h.Potion == 1);
            }
            else if (h.Slot != null)
                ClickSlot(h.Slot);
            else if (h.OverGrid)
                ClickGrid(h.GridPos);
            else if (h.OverStash)
                ClickStash(h.StashPos);
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
            if (stashPanel.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(stashPanel, screen, null))
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
            UiKit.ClickConsumedFrame = Time.frameCount;
            ItemData item = cursorItem;
            cursorItem = null;
            inventory.ThrowAway(item);
        }

        private void ClickSlot(SlotView s)
        {
            if (cursorItem == null)
            {
                // Take off whatever is worn.
                cursorItem = inventory.Equipment.Unequip(s.Slot);
                if (cursorItem != null)
                    PlayUISound(ItemSounds.Pickup(cursorItem));
                return;
            }

            // A two-handed weapon pushes whatever is in the off hand into the bag (if there's room).
            if (s.Slot == EquipSlot.MainHand && SlotRules.Accepts(s.Slot, cursorItem) &&
                inventory.MeetsItemRequirements(s.Slot, cursorItem) &&
                !inventory.Equipment.CanEquip(s.Slot, cursorItem))
            {
                ItemData off = inventory.Equipment.Get(EquipSlot.OffHand);
                if (off != null && !SlotRules.HandsCompatible(cursorItem, off) && inventory.Grid.TryAutoPlace(off))
                    inventory.Equipment.Unequip(EquipSlot.OffHand);
            }

            // Only the matching type of gear fits this slot.
            ItemData replaced;
            if (!inventory.Equipment.TryEquip(s.Slot, cursorItem, out replaced))
            {
                PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
                return;
            }

            PlayUISound(ItemSounds.Place(cursorItem));
            cursorItem = replaced;
        }

        private void UseConsumable(Hover h)
        {
            if (cursorItem != null || !h.OverGrid) return;
            PlacedItem p = inventory.Grid.GetAt(Mathf.FloorToInt(h.GridPos.x), Mathf.FloorToInt(h.GridPos.y));
            if (p == null || p.Item.Type != ItemType.Consumable) return;
            bool used = inventory.UseConsumable != null && inventory.UseConsumable(p.Item);
            if (used)
            {
                if (p.Item.StackCount > 1)
                {
                    p.Item.StackCount--;
                    inventory.Grid.NotifyChanged();
                }
                else inventory.Grid.Remove(p.Item);
            }
            else PlayUISound(AudioManager.Instance != null ? AudioManager.Instance.uiDenied : null);
            Refresh();
        }

        private void ClickGrid(Vector2 pos)
        {
            if (cursorItem == null)
            {
                PlacedItem p = inventory.Grid.GetAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
                if (p != null && inventory.Grid.Remove(p.Item))
                {
                    cursorItem = p.Item;
                    PlayUISound(ItemSounds.Pickup(cursorItem));
                }
                return;
            }

            Vector2Int o = FootprintOrigin(cursorItem, pos);
            ItemData replaced;
            if (inventory.Grid.TryPlaceOrSwap(cursorItem, o.x, o.y, out replaced))
            {
                PlayUISound(ItemSounds.Place(cursorItem));
                cursorItem = replaced;
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

    public sealed class StashTabDoubleClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action OnDoubleClick;
        private float lastClickAt = -1f;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            float now = Time.unscaledTime;
            if (eventData.clickCount == 2 || (lastClickAt >= 0f && now - lastClickAt <= 0.35f))
            {
                lastClickAt = -1f;
                OnDoubleClick?.Invoke();
            }
            else
                lastClickAt = now;
        }
    }
}
