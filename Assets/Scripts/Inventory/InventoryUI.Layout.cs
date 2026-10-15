using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    public partial class InventoryUI
    {

        private void ApplyPanelLayout()
        {
            bool touch = TouchMode.Active;
            panel.GetComponent<Image>().color = UiKit.ReadablePanelColor;
            Transform closeParent = touch ? canvas.transform : panel;
            if (inventoryClose.parent != closeParent)
            {
                inventoryClose.SetParent(closeParent,false);
                inventoryClose.anchorMin = inventoryClose.anchorMax = inventoryClose.pivot = Vector2.one;
                inventoryClose.anchoredPosition = new Vector2(-12f,-12f);
            }
            previewPanel.gameObject.SetActive(isOpen && !touch && !stashOpen);
            LayoutStash(touch);
            float rightInset = touch ? TouchRightInset : DesktopRightInset;
            // The stash and trader share a wide window with the bag. Keep its right edge clear of
            // touch buttons, then fit the whole window inside the remaining screen area.
            Vector2 size = stashOpen
                ? new Vector2(panelWidth + stashPanel.sizeDelta.x, Mathf.Max(regularPanelHeight, stashPanel.sizeDelta.y))
                : new Vector2(panelWidth, regularPanelHeight);
            bool stacked = touch && TouchMode.Portrait && stashOpen;
            var bag = (RectTransform)panel.Find("Bag");
            if (stacked)
            {
                size = new Vector2(Mathf.Max(panelWidth, stashPanel.sizeDelta.x), regularPanelHeight + stashPanel.sizeDelta.y + 32f);
                stashPanel.anchorMin = stashPanel.anchorMax = stashPanel.pivot = new Vector2(0.5f, 1f);
                stashPanel.anchoredPosition = new Vector2(0f, -16f);
                bag.anchorMin = bag.anchorMax = bag.pivot = new Vector2(0.5f, 0f);
                bag.anchoredPosition = new Vector2(0f, 16f);
            }
            else
            {
                stashPanel.anchorMin = stashPanel.anchorMax = stashPanel.pivot = new Vector2(0f, 0.5f);
                stashPanel.anchoredPosition = Vector2.zero;
                bag.anchorMin = bag.anchorMax = bag.pivot = new Vector2(1f, 0.5f);
                bag.anchoredPosition = Vector2.zero;
            }
            panel.sizeDelta = size;
            panel.anchorMin = panel.anchorMax = panel.pivot = touch ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 0.5f);
            panel.anchoredPosition = touch ? (Screen.safeArea.center - new Vector2(Screen.width,Screen.height)*0.5f)/Mathf.Max(0.01f,canvas.scaleFactor) : new Vector2(-rightInset, 0f);
            float scale = 1f;
            if (touch)
            {
                float canvasScale = Mathf.Max(0.01f, canvas.scaleFactor);
                float availableWidth = Screen.safeArea.width / canvasScale - 32f;
                float availableHeight = Screen.safeArea.height / canvasScale - 160f;
                scale = Mathf.Min(availableWidth / size.x, availableHeight / size.y);
            }
            panel.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            if (cursorView != null)
                cursorView.localScale = panel.localScale;

            previewPanel.anchoredPosition = new Vector2(-(rightInset + panelWidth + 16f), 0f);
            layoutScreenWidth = Screen.width;
            layoutScreenHeight = Screen.height;
            layoutCanvasScale = canvas.scaleFactor;
            layoutTouchMode = touch;
        }

        private void LayoutStash(bool touch)
        {
            float grid = PlayerInventory.StashSize * StashCell;
            float header = touch ? 80f : 34f;
            float tabs = touch ? 80f : TabRowHeight;
            float search = touch ? 80f : SearchHeight;
            float note = touch ? 44f : NoteHeight;
            stashPanel.sizeDelta = new Vector2(grid + Pad * 2f, grid + Pad * 2f + header + tabs + search + note);
            sideTitle.fontSize = touch ? 40 : 24;
            sideTitle.rectTransform.sizeDelta = new Vector2(stashPanel.sizeDelta.x, header - 4f);
            sideNote.fontSize = touch ? 26 : 16;
            sideNote.rectTransform.sizeDelta = new Vector2(-20f,note);
            stashArea.anchoredPosition = new Vector2(0f,-(Pad + header + tabs));
            UiKit.TopLeft(stashTabRow,new Vector2(Pad,-(Pad+header-4f)),new Vector2(grid,tabs-4f));
            for (int k=0;k<stashTabButtons.Length;k++)
            {
                var rt=stashTabButtons[k].rectTransform;
                rt.sizeDelta=new Vector2(rt.sizeDelta.x,tabs-4f);
                stashTabLabels[k].fontSize=touch ? 28 : 16;
            }
            stashSearchRow.offsetMin=new Vector2(Pad,10f+note);
            stashSearchRow.offsetMax=new Vector2(-Pad,10f+note+search-6f);
            stashSearchRow.Find("Field/Text").GetComponent<Text>().fontSize=touch ? 28 : 17;
            var clear=(RectTransform)stashSearchRow.Find("Clear");
            clear.sizeDelta=Vector2.one*(touch ? 72f : 30f);
            clear.GetComponentInChildren<Text>().fontSize=touch ? 32 : 16;
            ((RectTransform)stashSearchRow.Find("Field")).offsetMax=new Vector2(touch ? -78f : -36f,0f);
        }

        private Vector2 StashCellPos(int col, int row)
        {
            return new Vector2(col * StashCell + Gap * 0.5f, -row * StashCell - Gap * 0.5f);
        }

        private Vector2 StashCellSize(int w, int h)
        {
            return new Vector2(w * StashCell - Gap, h * StashCell - Gap);
        }

        private void BuildStash(float panelW, float panelH)
        {
            float gridSize = PlayerInventory.StashSize * StashCell;

            // The stash is the left section of the inventory panel itself (which widens to make
            // room), so it shares the panel's background, border and X.
            Image back = UiKit.NewImage("StashPanel", panel, Color.clear);
            back.raycastTarget = true;
            stashPanel = back.rectTransform;
            stashPanel.anchorMin = new Vector2(0f, 0.5f);
            stashPanel.anchorMax = new Vector2(0f, 0.5f);
            stashPanel.pivot = new Vector2(0f, 0.5f);
            stashPanel.anchoredPosition = Vector2.zero;
            stashPanel.sizeDelta = new Vector2(gridSize + Pad * 2f, gridSize + Pad * 2f + 34f + TabRowHeight + SearchHeight + NoteHeight);
            TouchMode.AddMenuBlocker(stashPanel);

            // A divider between the stash section and the equipment/bag section.
            Image divider = UiKit.NewImage("Divider", stashPanel, UiKit.BorderColor);
            divider.rectTransform.anchorMin = new Vector2(1f, 0f);
            divider.rectTransform.anchorMax = new Vector2(1f, 1f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.anchoredPosition = Vector2.zero;
            divider.rectTransform.sizeDelta = new Vector2(2f, -Pad * 2f);

            sideTitle = UiKit.Heading(UiKit.NewText("Title", stashPanel, "STASH", 24, UiKit.Gold, TextAnchor.UpperCenter));
            UiKit.TopLeft(sideTitle.rectTransform, new Vector2(0f, -12f), new Vector2(stashPanel.sizeDelta.x, 30f));

            sideNote = UiKit.NewText("Note", stashPanel, "", 16, UiKit.DimText, TextAnchor.MiddleCenter);
            sideNote.rectTransform.anchorMin = new Vector2(0f, 0f);
            sideNote.rectTransform.anchorMax = new Vector2(1f, 0f);
            sideNote.rectTransform.pivot = new Vector2(0.5f, 0f);
            sideNote.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            sideNote.rectTransform.sizeDelta = new Vector2(-20f, NoteHeight);

            // Search, PoE style: everything in the stash that doesn't match greys out, and the tabs
            // holding a match light up.
            stashSearchRow = UiKit.SearchBox(stashPanel, "Search the stash", "Search the stash (names and modifiers)", query =>
            {
                stashQuery = query;
                gridDirty = true;
            });
            stashSearchRow.anchorMin = new Vector2(0f, 0f);
            stashSearchRow.anchorMax = new Vector2(1f, 0f);
            stashSearchRow.pivot = new Vector2(0.5f, 0f);
            stashSearchRow.offsetMin = new Vector2(Pad, 10f + NoteHeight);
            stashSearchRow.offsetMax = new Vector2(-Pad, 10f + NoteHeight + SearchHeight - 6f);

            stashArea = UiKit.NewRect("StashGrid", stashPanel);
            stashArea.anchorMin = new Vector2(0.5f, 1f);
            stashArea.anchorMax = new Vector2(0.5f, 1f);
            stashArea.pivot = new Vector2(0.5f, 1f);
            stashArea.anchoredPosition = new Vector2(0f, -(Pad + 34f + TabRowHeight));

            // Stash tabs: a row of buttons under the title (hidden for a trader's goods).
            stashTabRow = UiKit.NewRect("StashTabs", stashPanel);
            UiKit.TopLeft(stashTabRow, new Vector2(Pad, -(Pad + 30f)), new Vector2(gridSize, TabRowHeight - 4f));
            float tabWidth = (gridSize - 4f * (PlayerInventory.StashTabCount - 1)) / PlayerInventory.StashTabCount;
            stashTabButtons = new Image[PlayerInventory.StashTabCount];
            stashTabLabels = new Text[PlayerInventory.StashTabCount];
            for (int k = 0; k < PlayerInventory.StashTabCount; k++)
            {
                int tab = k;
                Image button = UiKit.NewImage("Tab" + (k + 1), stashTabRow, CellColor);
                UiKit.TopLeft(button.rectTransform, new Vector2(k * (tabWidth + 4f), 0f), new Vector2(tabWidth, TabRowHeight - 4f));
                UiKit.AddOutline(button, UiKit.BorderColor, 1.5f);
                Text label = UiKit.NewText("Label", button.rectTransform, inventory.StashTabName(k), 16, UiKit.TextColor, TextAnchor.MiddleCenter);
                UiKit.Stretch(label.rectTransform, 3f);
                button.gameObject.AddComponent<RectMask2D>();
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
                UiKit.OnClick(button, () =>
                {
                    // A spectator sees whichever tab the player has open.
                    if (SpectatorMirror.Active)
                        return;
                    inventory.SetStashTab(tab);
                    gridDirty = true;
                    RefreshStashTabs();
                });
                button.gameObject.AddComponent<StashTabDoubleClick>().OnDoubleClick = () =>
                {
                    if (!SpectatorMirror.Active) BeginRenameTab(tab);
                };
                stashTabButtons[k] = button;
                stashTabLabels[k] = label;
            }
            RefreshStashTabs();
            stashArea.sizeDelta = new Vector2(gridSize, gridSize);

            Image lines = UiKit.NewImage("GridLines", stashArea, GridLineColor);
            UiKit.Stretch(lines.rectTransform, -Gap);
            UiKit.AddOutline(lines, UiKit.BorderColor, 2f);
            for (int y = 0; y < PlayerInventory.StashSize; y++)
            {
                for (int x = 0; x < PlayerInventory.StashSize; x++)
                {
                    Image cell = UiKit.NewImage("Cell", stashArea, CellColor);
                    UiKit.Inset(cell);
                    UiKit.TopLeft(cell.rectTransform, StashCellPos(x, y), StashCellSize(1, 1));
                }
            }

            stashItems = UiKit.NewRect("Items", stashArea);
            UiKit.Stretch(stashItems, 0f);
            stashHighlight = UiKit.NewImage("Highlight", stashArea, Color.clear);
            stashHighlight.enabled = false;

            stashPanel.gameObject.SetActive(false);
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

        private void BuildUI(bool hasPreview)
        {
            canvas = UiKit.NewCanvas("InventoryCanvas", transform, 800, out canvasGroup);
            // The panels catch the pointer, so a click on them never also attacks or walks.
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup.blocksRaycasts = true;

            float gridW = inventory.Grid.Width * cellSize;
            float gridH = inventory.Grid.Height * cellSize;
            float equipW = EquipCols * cellSize;
            float equipH = EquipRows * cellSize;
            float panelW = Mathf.Max(gridW, equipW) + Pad * 2f;
            float panelH = equipH + 24f + gridH + Pad * 2f;
            panelWidth = panelW;
            regularPanelHeight = panelH;

            // Inventory panel on the right side of the screen, like PoE.
            Image panelImage = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(panelImage);
            panelImage.raycastTarget = true;
            panel = panelImage.rectTransform;
            panel.anchorMin = new Vector2(1f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-30f, 0f);
            panel.sizeDelta = new Vector2(panelW, panelH);
            UiKit.Frame(panel);
            UiAppear panelFade = panel.gameObject.AddComponent<UiAppear>();
            panelFade.offset = Vector2.zero;
            panelFade.duration = OpenFade;
            panelFade.startScale = 1f; // touch mode scales this panel itself
            TouchMode.AddMenuBlocker(panel);
            inventoryClose = UiKit.CloseButton(panel, Close);
            // Mobile keeps the exit above equipment and independent of the inventory's fit scale.
            inventoryClose.SetParent(canvas.transform, false);

            // Character preview panel, just to the left of the inventory.
            Image previewBg = UiKit.NewImage("PreviewPanel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(previewBg);
            previewBg.raycastTarget = true;
            previewPanel = previewBg.rectTransform;
            previewPanel.anchorMin = new Vector2(1f, 0.5f);
            previewPanel.anchorMax = new Vector2(1f, 0.5f);
            previewPanel.pivot = new Vector2(1f, 0.5f);
            previewPanel.anchoredPosition = new Vector2(-(30f + panelW + 16f), 0f);
            previewPanel.sizeDelta = new Vector2(panelH * 0.68f, panelH);
            UiKit.Frame(previewPanel);
            UiAppear previewFade = previewPanel.gameObject.AddComponent<UiAppear>();
            previewFade.offset = Vector2.zero;
            previewFade.duration = OpenFade;
            TouchMode.AddMenuBlocker(previewPanel);

            if (hasPreview)
            {
                GameObject rawGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
                rawGo.transform.SetParent(previewPanel, false);
                RawImage raw = rawGo.GetComponent<RawImage>();
                raw.texture = preview.Texture;
                raw.raycastTarget = false;
                UiKit.Stretch((RectTransform)rawGo.transform, 8f);
            }

            // Equipment and bag keep the panel's right edge, so the stash can widen it leftwards.
            RectTransform bagArea = UiKit.NewRect("Bag", panel);
            bagArea.anchorMin = new Vector2(1f, 0.5f);
            bagArea.anchorMax = new Vector2(1f, 0.5f);
            bagArea.pivot = new Vector2(1f, 0.5f);
            bagArea.anchoredPosition = Vector2.zero;
            bagArea.sizeDelta = new Vector2(panelW, panelH);

            // Equipment board (top).
            RectTransform equipArea = UiKit.NewRect("Equipment", bagArea);
            equipArea.anchorMin = new Vector2(0.5f, 1f);
            equipArea.anchorMax = new Vector2(0.5f, 1f);
            equipArea.pivot = new Vector2(0.5f, 1f);
            equipArea.anchoredPosition = new Vector2(0f, -Pad);
            equipArea.sizeDelta = new Vector2(equipW, equipH);

            foreach (SlotLayout l in Layout)
                BuildSlot(equipArea, l);

            // The potion slots, either side of the body armour: only potions go here, and they stack.
            BuildPotionSlot(equipArea, 0, 2, 2);
            BuildPotionSlot(equipArea, 1, 5, 2);

            // Inventory grid (bottom).
            gridArea = UiKit.NewRect("Grid", bagArea);
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
                    UiKit.Inset(cell);
                    UiKit.TopLeft(cell.rectTransform, CellPos(x, y), CellSize(1, 1));
                }
            }

            gridItems = UiKit.NewRect("Items", gridArea);
            UiKit.Stretch(gridItems, 0f);

            gridHighlight = UiKit.NewImage("Highlight", gridArea, Color.clear);
            gridHighlight.enabled = false;

            // Gold carried: in the gap between the equipment board and the bag, over its right edge.
            Image coins = UiKit.NewImage("GoldIcon", bagArea, Color.white);
            coins.sprite = Resources.Load<Sprite>("ItemIcons/gold_coins");
            coins.preserveAspect = true;
            RectTransform coinsRect = coins.rectTransform;
            coinsRect.anchorMin = coinsRect.anchorMax = coinsRect.pivot = new Vector2(1f, 0f);
            coinsRect.sizeDelta = new Vector2(24f, 24f);
            coinsRect.anchoredPosition = new Vector2(-(panelW - gridW) * 0.5f, Pad + gridH + 9f);
            goldText = UiKit.NewText("Gold", bagArea, "", 20, new Color(1f, 0.83f, 0.3f, 1f), TextAnchor.MiddleRight);
            goldText.font = UiKit.BoldFont;
            RectTransform goldRect = goldText.rectTransform;
            goldRect.anchorMin = goldRect.anchorMax = goldRect.pivot = new Vector2(1f, 0f);
            goldRect.sizeDelta = new Vector2(200f, 22f);
            goldRect.anchoredPosition = coinsRect.anchoredPosition - new Vector2(26f, 0f);
            UiKit.AddOutline(goldText, new Color(0f, 0f, 0f, 0.7f), 1f);
            RefreshGold();

            BuildStash(panelW, panelH);
            inventoryClose.SetAsLastSibling(); // above the stash section added after it

            BuildTooltip();
        }

        private void BuildPotionSlot(RectTransform parent, int index, int col, int row)
        {
            Image bg = UiKit.NewImage(index == 0 ? "HealthPotions" : "ManaPotions", parent, SlotColor);
            UiKit.Inset(bg);
            RectTransform rt = bg.rectTransform;
            UiKit.TopLeft(rt, SlotPos(col, row), SlotSize(1, 1));
            UiKit.AddOutline(bg, index == 0 ? new Color(0.55f, 0.2f, 0.2f, 1f) : new Color(0.25f, 0.3f, 0.6f, 1f), 1.5f);

            Image icon = UiKit.NewImage("Icon", rt, Color.white);
            icon.sprite = Resources.Load<Sprite>("ItemIcons/" + (index == 0 ? ItemGenerator.HealthPotionId : ItemGenerator.ManaPotionId));
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 2f);

            Text count = UiKit.NewText("Count", rt, "", 15, Color.white, TextAnchor.LowerRight);
            count.font = UiKit.BoldFont;
            UiKit.Stretch(count.rectTransform, 3f);
            UiKit.AddOutline(count, Color.black, 1f);

            Image overlay = UiKit.NewImage("Overlay", rt, Color.clear);
            UiKit.Stretch(overlay.rectTransform, 0f);

            potionSlots[index] = rt;
            potionCounts[index] = count;
            potionOverlays[index] = overlay;
        }

        private void BuildSlot(RectTransform parent, SlotLayout l)
        {
            Image bg = UiKit.NewImage("Slot_" + l.Slot, parent, SlotColor);
            UiKit.Inset(bg);
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

            Image extra = null;
            if (ExtraSlotButton != null)
            {
                extra = UiKit.NewImage("ExtraButton", rt, Color.white);
                UiKit.TopLeft(extra.rectTransform, new Vector2(rt.sizeDelta.x - 21f, -1f), new Vector2(20f, 20f));
                Text label = UiKit.NewText("Label", extra.rectTransform, ExtraSlotButton.Label, 13, UiKit.TextColor, TextAnchor.MiddleCenter);
                UiKit.Stretch(label.rectTransform, 0f);
                UiKit.StyleButton(extra, label);
            }

            slotViews.Add(new SlotView { Slot = l.Slot, Rect = rt, Overlay = overlay, Hint = hint, Extra = extra });
        }

        private void BuildTooltip()
        {
            CanvasGroup unusedGroup;
            tooltipCanvas = UiKit.NewCanvas("InventoryTooltipCanvas", transform, 820, out unusedGroup);
            tooltipCanvas.overrideSorting = true;

            Image bg = UiKit.NewImage("Tooltip", tooltipCanvas.transform, TooltipColor);
            UiKit.Grain(bg);
            tooltipRect = bg.rectTransform;
            tooltipRect.anchorMin = Vector2.zero;
            tooltipRect.anchorMax = Vector2.zero;
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.sizeDelta = new Vector2(270f, 100f);
            UiKit.ThinFrame(tooltipRect).color = new Color(0.72f, 0.68f, 0.62f, 1f);

            // Only to read: a tooltip beside an item on the ground mustn't swallow taps on the world.
            bg.raycastTarget = false;

            tooltipText = UiKit.NewText("Text", tooltipRect, "", 17, UiKit.TextColor, TextAnchor.UpperLeft);
            tooltipText.raycastTarget = false;
            UiKit.Stretch(tooltipText.rectTransform, 10f);
            SetTooltipLayer(tooltipText.transform, 822);

            tooltipRect.gameObject.SetActive(false);

            // The item on the cursor's own stats, shown the whole time it's held (not just while
            // it's hovering a slot) so a drag-swap can be compared against what's underneath without
            // covering that slot's own tooltip (see ShowHeldTooltip for where they're kept apart).
            Image heldBg = UiKit.NewImage("HeldTooltip", tooltipCanvas.transform, TooltipColor);
            UiKit.Grain(heldBg);
            heldTooltipRect = heldBg.rectTransform;
            heldTooltipRect.anchorMin = Vector2.zero;
            heldTooltipRect.anchorMax = Vector2.zero;
            heldTooltipRect.pivot = new Vector2(0f, 1f);
            heldTooltipRect.sizeDelta = new Vector2(270f, 100f);
            // The hover tooltip's frame is dimmer, so the held item's stats stand out from it.
            UiKit.ThinFrame(heldTooltipRect);

            heldTooltipText = UiKit.NewText("Text", heldTooltipRect, "", 17, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.Stretch(heldTooltipText.rectTransform, 10f);
            SetTooltipLayer(heldTooltipText.transform, 822);

            heldTooltipRect.gameObject.SetActive(false);

            mobileSelection = UiKit.NewRect("MobileSelectedItem", tooltipCanvas.transform);
            mobileSelection.anchorMin = mobileSelection.anchorMax = new Vector2(0.5f, 0.5f);
            mobileSelection.pivot = new Vector2(0.5f, 0.5f);
            SetTooltipLayer(mobileSelection, 821);
            // Explicit layers keep edges above both tooltip backgrounds and below their text.
            for (int edge = 0; edge < 4; edge++)
            {
                Image line = UiKit.NewImage("Edge" + edge, mobileSelection, new Color(0.65f, 0.54f, 0.3f, 1f));
                line.raycastTarget = false;
                RectTransform rt = line.rectTransform;
                bool horizontal = edge < 2;
                float side = edge % 2;
                rt.anchorMin = horizontal ? new Vector2(0f, side) : new Vector2(side, 0f);
                rt.anchorMax = horizontal ? new Vector2(1f, side) : new Vector2(side, 1f);
                rt.sizeDelta = new Vector2(3f, 3f);
                rt.anchoredPosition = Vector2.zero;
            }
            mobileSelection.gameObject.SetActive(false);
        }

        private static void SetTooltipLayer(Transform target, int order)
        {
            Canvas layer = target.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = order;
        }

        // ------------------------------------------------------------------ item views

        private RectTransform CreateItemView(Transform parent, ItemData item, Vector2 size, float alpha, Vector2? iconLimit = null)
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

            // Draw the rarity frame inward from the item bounds, flush with the slot edge.
            // Unity's Outline expands outward and can cover the grid and neighboring items.
            Image bg = UiKit.NewImage("Item_" + item.Name, parent, Color.clear);
            RectTransform outer = bg.rectTransform;
            outer.anchorMin = new Vector2(0f, 1f);
            outer.anchorMax = new Vector2(0f, 1f);
            outer.pivot = new Vector2(0f, 1f);
            outer.sizeDelta = size;

            border.a = alpha;
            Image frame = UiKit.NewImage("Frame", outer, border);
            UiKit.Stretch(frame.rectTransform, 0f);
            Image face = UiKit.NewImage("Face", frame.transform, plate);
            UiKit.Stretch(face.rectTransform, 2f);

            Image icon = UiKit.NewImage("Icon", outer, Color.white);
            icon.preserveAspect = true;
            RectTransform irt = icon.rectTransform;

            if (painted != null)
            {
                icon.sprite = painted;
                icon.color = new Color(item.IconTint.r, item.IconTint.g, item.IconTint.b, alpha);
                if (iconLimit.HasValue)
                {
                    irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                    irt.anchoredPosition = Vector2.zero;
                    irt.sizeDelta = Vector2.Min(size - new Vector2(6f, 6f), iconLimit.Value - new Vector2(6f, 6f));
                }
                else
                    UiKit.Stretch(irt, 3f);
            }
            else
            {
                Color bright = Color.Lerp(tint, Color.white, 0.3f);
                icon.sprite = IconFactory.Get(item);
                icon.color = new Color(bright.r, bright.g, bright.b, alpha);
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                Vector2 visibleSize = iconLimit.HasValue ? Vector2.Min(size, iconLimit.Value) : size;
                float s = Mathf.Min(visibleSize.x, visibleSize.y) * 0.78f;
                irt.sizeDelta = new Vector2(s, s);
            }

            // Gear the character can't use yet is tinted red inside its rarity frame.
            if (inventory != null && inventory.Stats != null && !item.Requirements.MetBy(inventory.Stats))
            {
                Image unmet = UiKit.NewImage("Unmet", outer, new Color(UnmetTint.r, UnmetTint.g, UnmetTint.b, UnmetTint.a * alpha));
                UiKit.Stretch(unmet.rectTransform, 2f);
                unmet.raycastTarget = false;
            }

            if (item.StackCount > 1)
            {
                Text count = UiKit.NewText("StackCount", outer, item.StackCount.ToString(), 16, Color.white, TextAnchor.LowerRight);
                UiKit.Stretch(count.rectTransform, 3f);
                count.raycastTarget = false;
            }

            return outer;
        }
    }
}
