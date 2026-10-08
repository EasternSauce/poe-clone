using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Player;

namespace PoeClone.UI
{
    /// <summary>
    /// The passive tree panel (P, or the TREE button on touch): every passive as a circle, linked
    /// to its neighbours. Click/tap to preview spending, then Confirm or Cancel the draft. Hovering (or tapping) shows what a passive gives in a tooltip.
    /// The tree is bigger than the panel: drag to move around it, scroll or pinch to zoom.
    /// Installed by GameSessionController.
    /// </summary>
    public class PassiveTreeUI : MonoBehaviour
    {
        private const float UnitPixels = 200f;   // one tree unit (PassiveNode.X/Y) at zoom 1
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 1.5f;
        private const float StartZoom = 0.4f;
        private const float ViewTop = 76f;       // the tree's window inside the panel, below the title
        private const float ViewBottom = 104f;   // and above the info strip

        // The panel fills the screen but for this margin, so the tree gets all the room there is.
        private const float Margin = 20f;
        private static PassiveTreeUI instance;

        private class NodeView
        {
            public PassiveNode Node;
            public Image Ring;
            public Image Body;
            public Text Label;
        }

        private class LinkView
        {
            public string A, B;
            public Image Line;
        }

        private readonly List<NodeView> views = new List<NodeView>();
        private readonly List<LinkView> links = new List<LinkView>();

        private GameObject panelRoot;
        private Image badge;
        private Text badgeText;
        private Text pointsText;
        private Text infoText;
        private Image tooltip;
        private Text tooltipText;
        private PassiveNode hovered;
        private Image confirmButton;
        private Image cancelButton;
        private PassiveAllocation draft;
        private bool resetPending;
        private bool closeRequested;
        private PassiveAllocation Preview => !Spectating && draft != null ? draft : passives.Allocation;
        public bool HasPendingChanges => draft != null && (resetPending ||
            !new HashSet<string>(draft.Taken).SetEquals(passives.Allocation.Taken));

        private void BeginDraft()
        {
            if (passives == null) return;
            draft = new PassiveAllocation();
            draft.ReplaceWith(passives.Allocation.Taken);
            resetPending = false;
            closeRequested = false;
        }

        public void ConfirmChanges()
        {
            if (Spectating) return;
            if (!HasPendingChanges) { SetOpen(false); return; }
            if (!passives.ConfirmDraft(draft, resetPending)) return;
            draft = null;
            resetPending = false;
            SetOpen(false);
        }

        public void CancelChanges()
        {
            if (Spectating) return;
            draft = null;
            resetPending = false;
            SetOpen(false);
        }
        private Image resetButton;
        private Image zoomInButton;
        private Image zoomOutButton;
        private Image centreButton;
        private Text resetLabel;
        private PlayerPassives passives;
        private PassiveNode selected;
        private Vector2 tooltipOffset;
        private bool placeTooltip;
        private PassiveNode shown;          // the passive the tooltip describes
        private PassiveNode mirroredHover;  // spectators: the player's pointed-at passive last shown
        private bool dirty = true;

        public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

        /// <summary>The passive whose details are showing (pointed at, or selected), for spectators; null if none or closed.</summary>
        public static string ShownId => IsOpen && instance.shown != null ? instance.shown.Id : null;

        // Spectators see the watched player's tree read-only (SpectatorMirror).
        private static bool Spectating => SpectatorMirror.Active;

        public static void SetOpen(bool open)
        {
            if (instance == null)
                return;
            if (!open && !Spectating && instance.HasPendingChanges)
            {
                instance.closeRequested = true;
                instance.hovered = null;
                instance.ShowInfo(null);
                instance.dirty = true;
                return;
            }
            if (open && !IsOpen && !Spectating) instance.BeginDraft();
            // Both panels sit in the middle of the screen: one at a time.
            if (open)
                SkillBarUI.SetOpen(false);
            instance.panelRoot.SetActive(open);
            if (!open)
            {
                instance.ResetPinch();
                instance.hovered = null;
                instance.ShowInfo(null);
            }
            instance.dirty = true;
            if (open && !instance.viewFitted)
            {
                // The window's size is only known once the canvas has laid it out.
                Canvas.ForceUpdateCanvases();
                instance.ResetView();
                instance.viewFitted = true;
            }
        }

        private RectTransform panelRect;
        private RectTransform viewRect;
        private bool viewFitted;
        private RectTransform content;
        private bool pinching;
        private bool suppressPinchClicks;
        private int pinchEndedFrame = -100;
        private int pinchIdA, pinchIdB;
        private Vector2 pinchMiddle;
        private float pinchDistance;

        private void Awake()
        {
            instance = this;
            Build();
            TouchMode.Changed += UpdateZoomButtons;
            panelRoot.SetActive(false);

        }

        private void OnDestroy()
        {
            if (passives != null)
                passives.Changed -= MarkDirty;
            TouchMode.Changed -= UpdateZoomButtons;
            if (instance == this)
                instance = null;
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void Update()
        {
            var session = GameSessionController.Instance;
            bool spectator = session != null && session.Role == SessionRole.Spectator;

            if (passives == null)
            {
                passives = FindAnyObjectByType<PlayerPassives>();
                if (passives != null)
                    passives.Changed += MarkDirty;
            }

            if (spectator && passives != null)
            {
                UpdateMirror();
                return;
            }

            if (passives == null || !passives.enabled || spectator)
            {
                panelRoot.SetActive(false);
                badge.gameObject.SetActive(false);
                return;
            }

            UpdateBadge();

            if (panelRoot.activeSelf)
                UpdatePinch();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.pKey.wasPressedThisFrame)
                    SetOpen(!panelRoot.activeSelf);
                else if (panelRoot.activeSelf && keyboard.escapeKey.wasPressedThisFrame)
                    SetOpen(false);
            }

            if (panelRoot.activeSelf && dirty)
            {
                dirty = false;
                Refresh();
            }
        }

        // A spectator's copy of the watched player's tree: open while theirs is (or while the
        // spectator opened it with P), showing what the player points at until the spectator
        // moves their own mouse.
        private void UpdateMirror()
        {
            if (badge.gameObject.activeSelf)
                badge.gameObject.SetActive(false);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.pKey.wasPressedThisFrame)
                    SpectatorMirror.Toggle(SpectatorMirror.Menu.Tree);
                else if (keyboard.escapeKey.wasPressedThisFrame)
                    SpectatorMirror.Close(SpectatorMirror.Menu.Tree);
            }

            bool open = SpectatorMirror.Shown(SpectatorMirror.Menu.Tree);
            bool remote = SpectatorMirror.ShowsRemote(SpectatorMirror.Menu.Tree);
            if (panelRoot.activeSelf != open)
            {
                panelRoot.SetActive(open);
                dirty = true;
            }
            if (!open)
            {
                ResetPinch();
                return;
            }

            UpdatePinch();

            if (remote && SpectatorMirror.FollowingPlayer)
            {
                PassiveNode pointed = SpectatorMirror.TreeHover != null ? PassiveTree.Get(SpectatorMirror.TreeHover) : null;
                if (pointed != mirroredHover)
                {
                    mirroredHover = pointed;
                    selected = pointed;
                    dirty = true;
                }
            }

            if (dirty)
            {
                dirty = false;
                Refresh();
            }
        }

        // A gently pulsing note below the level/health HUD while there are points to spend;
        // clicking it opens the tree.
        private void UpdateBadge()
        {
            int unspent = passives.Unspent;
            bool show = unspent > 0 && !panelRoot.activeSelf && !SkillBarUI.PickerOpen && !passives.GetComponent<PlayerStats>().IsDead;
            if (badge.gameObject.activeSelf != show)
                badge.gameObject.SetActive(show);
            if (!show)
                return;

            // PlayerHUD uses screen pixels (OnGUI), while this badge lives on a scaled canvas.
            float scale = badge.canvas.scaleFactor;
            float hudBottom = TouchMode.Active ? 124f * TouchMode.GuiScale
                : PlayerHUD.ControlsHidden ? 326f : 378f;
            badge.rectTransform.anchoredPosition = new Vector2(20f / scale, -(hudBottom + 10f) / scale);

            string text = "+" + unspent + " passive point" + (unspent > 1 ? "s" : "") + (TouchMode.Active ? "" : "  (P)");
            if (badgeText.text != text)
            {
                badgeText.text = text;
                badge.rectTransform.sizeDelta = new Vector2(badgeText.preferredWidth + 36f, 36f);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            badgeText.color = Color.Lerp(UiKit.Gold, new Color(1f, 0.95f, 0.7f), pulse);
            badge.color = new Color(0.12f, 0.09f, 0.04f, 0.75f + 0.15f * pulse);
        }

        // ------------------------------------------------------------------ state

        private static readonly Color Locked = new Color(0.16f, 0.15f, 0.14f, 1f);

        private static Color BranchColor(PassiveBranch branch)
        {
            switch (branch)
            {
                case PassiveBranch.Might: return new Color(0.85f, 0.30f, 0.25f);
                case PassiveBranch.Grace: return new Color(0.35f, 0.80f, 0.40f);
                case PassiveBranch.Wisdom: return new Color(0.40f, 0.55f, 1.00f);
                case PassiveBranch.Fury: return new Color(0.95f, 0.60f, 0.22f);
                case PassiveBranch.Storm: return new Color(0.30f, 0.82f, 0.90f);
                case PassiveBranch.Zeal: return new Color(0.80f, 0.42f, 0.88f);
                case PassiveBranch.Necromancy: return new Color(0.78f, 0.92f, 0.52f);
                default: return UiKit.Gold;
            }
        }

        private void Refresh()
        {
            PassiveAllocation allocation = Preview;
            int unspent = allocation.Unspent(passives.Level);
            pointsText.text = unspent > 0
                ? "<color=#FFD040>" + unspent + " passive point" + (unspent > 1 ? "s" : "") + " to spend</color>"
                : "<color=#" + UiKit.Hex(UiKit.DimText) + ">No points to spend - one more each level</color>";

            int charges = passives.RespecCharges - (resetPending ? 1 : 0);
            confirmButton.gameObject.SetActive(!Spectating);
            cancelButton.gameObject.SetActive(!Spectating);
            resetButton.gameObject.SetActive(!Spectating);
            resetLabel.text = "Reset all (" + charges + ")";
            resetButton.color = charges > 0 ? new Color(0.18f, 0.14f, 0.10f, 1f) : new Color(0.12f, 0.10f, 0.08f, 1f);
            resetLabel.color = charges > 0 ? UiKit.TextColor : UiKit.DimText;

            foreach (NodeView v in views)
            {
                bool taken = allocation.Has(v.Node.Id);
                bool available = allocation.CanTake(v.Node.Id, passives.Level);
                Color c = BranchColor(v.Node.Branch);
                v.Body.color = taken ? (!Spectating && !passives.Allocation.Has(v.Node.Id) ? new Color(0.3f, 0.85f, 1f) : c) : available ? Color.Lerp(Locked, c, 0.35f) : Locked;
                v.Ring.color = taken ? UiKit.Gold : available ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(0.35f, 0.32f, 0.28f, 1f);
                if (v.Label != null)
                    v.Label.color = taken ? UiKit.Gold : available ? UiKit.TextColor : UiKit.DimText;
            }

            foreach (LinkView l in links)
            {
                bool both = allocation.Has(l.A) && allocation.Has(l.B);
                l.Line.color = both ? UiKit.Gold : new Color(0.30f, 0.27f, 0.23f, 1f);
            }

            ShowInfo(hovered ?? (TouchMode.Active || (Spectating && SpectatorMirror.FollowingPlayer) ? selected : null));
        }

        private void LateUpdate()
        {
            if (!panelRoot.activeSelf || shown == null || !tooltip.gameObject.activeSelf)
                return;

            // Keep a fixed offset from the inspected tree position. Clamp once on
            // selection, then allow panning/zooming to carry the tooltip off-screen.
            Vector2 at = panelRect.InverseTransformPoint(content.TransformPoint(NodePosition(shown)));
            if (placeTooltip)
            {
                Rect bounds = panelRect.rect;
                Vector2 size = tooltip.rectTransform.sizeDelta;
                float x = at.x + 20f;
                if (x + size.x > bounds.xMax - 8f)
                    x = at.x - size.x - 20f;
                float y = at.y - 20f;
                Vector2 placed = new Vector2(
                    Mathf.Clamp(x, bounds.xMin + 8f, bounds.xMax - size.x - 8f),
                    Mathf.Clamp(y, bounds.yMin + size.y + 8f, bounds.yMax - 8f));
                tooltipOffset = placed - at;
                placeTooltip = false;
            }
            tooltip.rectTransform.anchoredPosition = at + tooltipOffset;
        }

        private void ShowInfo(PassiveNode node)
        {
            if (shown != node) placeTooltip = true;
            shown = node;
            tooltip.gameObject.SetActive(node != null);
            infoText.text = PlayerHUD.ControlsHidden ? "" : TouchMode.Active
                ? "Tap a passive to inspect it; tap again to take it. Drag to look around, pinch to zoom."
                : "Click to take a passive; click a pending end node again to undo it. Drag to look around, scroll to zoom. (H hides this.)";
            if (closeRequested)
                infoText.text = "You have unconfirmed changes. Confirm to spend your points, or Cancel to discard changes.";
            if (node == null) return;

            var sb = new StringBuilder();
            sb.Append("<b><color=#").Append(UiKit.Hex(BranchColor(node.Branch))).Append(">").Append(node.Name ?? "Passive")
                .Append("</color></b>\n");
            if (node.Mods.Length == 0)
                sb.Append("Where every path starts.");
            for (int k = 0; k < node.Mods.Length; k++)
            {
                if (k > 0)
                    sb.Append("\n");
                sb.Append(StatFormatter.ItemLine(node.Mods[k]));
            }

            PassiveAllocation allocation = Preview;
            bool taken = allocation.Has(node.Id);
            sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">");
            if (node.Id == PassiveTree.OriginId)
                sb.Append("Always yours.");
            else if (taken)
                sb.Append(passives.Allocation.Has(node.Id) && !resetPending ? "Allocated." : "Pending - confirm to apply.");
            else if (allocation.CanTake(node.Id, passives.Level))
                sb.Append(TouchMode.Active ? "Tap again to take it." : "Click to take it.");
            else
                sb.Append(allocation.Unspent(passives.Level) <= 0 ? "No points left." : "Take a passive next to it first.");
            sb.Append("</color>");

            tooltipText.text = sb.ToString();
            float width = Mathf.Min(360f, panelRect.rect.width - 16f);
            tooltip.rectTransform.sizeDelta = new Vector2(width, 100f);
            tooltipText.rectTransform.sizeDelta = new Vector2(width - 24f, 0f);
            tooltip.rectTransform.sizeDelta = new Vector2(width, tooltipText.preferredHeight + 24f);

        }

        private void OnHover(PassiveNode node)
        {
            if (!TouchMode.Active)
            {
                hovered = node;
                ShowInfo(node);
            }
        }

        private void OnHoverEnd()
        {
            if (!TouchMode.Active)
            {
                hovered = null;
                ShowInfo(null);
            }
        }

        private void OnClick(PassiveNode node, PointerEventData.InputButton button)
        {
            if (passives == null || (TouchMode.Active &&
                (suppressPinchClicks || Time.frameCount <= pinchEndedFrame + 1)))
                return;

            if (Spectating)
            {
                selected = node;
                dirty = true;
                return;
            }

            if (button == PointerEventData.InputButton.Right)
            {
                if (draft != null && (resetPending || !passives.Allocation.Has(node.Id))) draft.Refund(node.Id);
                selected = node;
                dirty = true;
                return;
            }

            // Touch: the first tap only selects (shows what it gives); a second tap takes it.
            bool take = !TouchMode.Active || selected == node;
            selected = node;
            if (draft == null) BeginDraft();
            closeRequested = false;
            if (take && draft.Has(node.Id) && (resetPending || !passives.Allocation.Has(node.Id))) draft.Refund(node.Id);
            else if (take && draft.Take(node.Id, passives.Level))
            {
                if (Audio.AudioManager.Instance != null)
                    Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.uiItemPlace, 0.4f);
            }
            dirty = true;
        }

        // ------------------------------------------------------------------ building

        private Vector2 NodePosition(PassiveNode node)
        {
            return new Vector2(node.X, node.Y) * UnitPixels;
        }

        // ------------------------------------------------------------------ moving around

        private void Pan(Vector2 screenDelta)
        {
            // Screen pixels to the panel's own units (the canvas and touch scaling in between).
            float scale = viewRect.lossyScale.x > 0f ? viewRect.lossyScale.x : 1f;
            content.anchoredPosition += screenDelta / scale;
            ClampContent();
        }

        private void UpdatePinch()
        {
            if (!TouchMode.Active || Touchscreen.current == null)
                return;

            TouchControl first = null, second = null;
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed)
                    continue;
                if (first == null) first = touch;
                else { second = touch; break; }
            }

            if (second == null)
            {
                pinching = false;
                if (first == null && suppressPinchClicks)
                {
                    suppressPinchClicks = false;
                    pinchEndedFrame = Time.frameCount;
                }
                return;
            }

            Vector2 a = first.position.ReadValue();
            Vector2 b = second.position.ReadValue();
            if (!pinching)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(viewRect, a) ||
                    !RectTransformUtility.RectangleContainsScreenPoint(viewRect, b))
                    return;
                pinching = true;
                suppressPinchClicks = true;
                pinchIdA = first.touchId.ReadValue();
                pinchIdB = second.touchId.ReadValue();
                pinchMiddle = (a + b) * 0.5f;
                pinchDistance = Vector2.Distance(a, b);
                return;
            }

            if (first.touchId.ReadValue() != pinchIdA || second.touchId.ReadValue() != pinchIdB)
            {
                pinching = false;
                return;
            }

            Vector2 middle = (a + b) * 0.5f;
            float distance = Vector2.Distance(a, b);
            if (pinchDistance > 0f && distance > 0f)
                Zoom(distance / pinchDistance, pinchMiddle);
            Pan(middle - pinchMiddle);
            pinchMiddle = middle;
            pinchDistance = distance;
        }

        private void ResetPinch()
        {
            if (pinching || suppressPinchClicks)
                pinchEndedFrame = Time.frameCount;
            pinching = false;
            suppressPinchClicks = false;
        }

        private void UpdateZoomButtons()
        {
            bool touch = TouchMode.Active;
            zoomInButton.gameObject.SetActive(!touch);
            zoomOutButton.gameObject.SetActive(!touch);
            TopRight(centreButton.rectTransform, new Vector2(touch ? 12f + 46f : 12f + 3f * 46f, -12f),
                new Vector2(88f, 38f));
        }

        // Zooms keeping the point under the pointer (or the middle of the window) where it is.
        private void Zoom(float factor, Vector2? screenPoint)
        {
            float before = content.localScale.x;
            float after = Mathf.Clamp(before * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(before, after))
                return;

            Vector2 pivot = Vector2.zero;
            if (screenPoint.HasValue)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, screenPoint.Value, null, out pivot);
            Vector2 treePoint = (pivot - content.anchoredPosition) / before;
            content.localScale = Vector3.one * after;
            content.anchoredPosition = pivot - treePoint * after;
            ClampContent();
        }

        // Keeps some of the tree in the window however far it is dragged.
        private void ClampContent()
        {
            float reachX = 0f, reachY = 0f;
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                reachX = Mathf.Max(reachX, Mathf.Abs(node.X));
                reachY = Mathf.Max(reachY, Mathf.Abs(node.Y));
            }
            reachX *= UnitPixels * content.localScale.x;
            reachY *= UnitPixels * content.localScale.y;
            Vector2 half = viewRect.rect.size * 0.5f;
            Vector2 p = content.anchoredPosition;
            // Leave room for the outer node and its label at the edge of the window.
            float limitX = Mathf.Max(0f, reachX - half.x + 90f);
            float limitY = Mathf.Max(0f, reachY - half.y + 90f);
            p.x = Mathf.Clamp(p.x, -limitX, limitX);
            p.y = Mathf.Clamp(p.y, -limitY, limitY);
            content.anchoredPosition = p;
        }

        // Start with readable spacing near the origin; zooming out can show the full tree.
        private void ResetView()
        {
            Vector2 size = viewRect.rect.size;
            float extent = 0f;
            foreach (PassiveNode node in PassiveTree.Nodes)
                extent = Mathf.Max(extent, Mathf.Abs(node.X), Mathf.Abs(node.Y));
            float fit = Mathf.Min(size.x, size.y) / (2f * (extent + 0.5f) * UnitPixels);
            content.localScale = Vector3.one * Mathf.Clamp(Mathf.Max(StartZoom, fit), MinZoom, MaxZoom);
            content.anchoredPosition = Vector2.zero;
        }

        private static void TopRight(RectTransform rt, Vector2 fromCorner, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-fromCorner.x, fromCorner.y);
            rt.sizeDelta = size;
        }

        private static void TopStrip(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("PassiveCanvas", transform, 800, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            badge = UiKit.NewImage("PointsBadge", canvas.transform, new Color(0.12f, 0.09f, 0.04f, 0.85f));
            badge.raycastTarget = true;
            RectTransform br = badge.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 1f);
            br.sizeDelta = new Vector2(260f, 36f);
            UiKit.AddOutline(badge, UiKit.Gold, 1.5f);
            badgeText = UiKit.NewText("Text", br, "", 19, UiKit.Gold, TextAnchor.MiddleCenter);
            badgeText.fontStyle = FontStyle.Bold;
            UiKit.Stretch(badgeText.rectTransform, 0f);
            badge.gameObject.AddComponent<TouchPointerRelay>().Up += _ => SetOpen(true);
            badge.gameObject.SetActive(false);

            Image panel = UiKit.NewImage("PassivePanel", canvas.transform, UiKit.PanelColor);
            UiKit.Grain(panel);
            panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform;
            UiKit.Stretch(pr, Margin);
            UiKit.AddOutline(panel, UiKit.BorderColor, 3f);
            panelRoot = panel.gameObject;
            panelRect = pr;
            TouchMode.AddBlocker(pr);

            Text title = UiKit.NewText("Title", pr, "PASSIVES", 26, UiKit.Gold, TextAnchor.UpperCenter);
            TopStrip(title.rectTransform, -14f, 34f);

            pointsText = UiKit.NewText("Points", pr, "", 18, UiKit.TextColor, TextAnchor.UpperCenter);
            TopStrip(pointsText.rectTransform, -46f, 24f);

            Image close = UiKit.NewImage("Close", pr, new Color(0.25f, 0.1f, 0.08f, 1f));
            close.raycastTarget = true;
            TopRight(close.rectTransform, new Vector2(12f, -12f), new Vector2(38f, 38f));
            Text x = UiKit.NewText("X", close.rectTransform, "X", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(x.rectTransform, 0f);
            // A spectator shuts only their own copy (the player's stays open for the player).
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                if (Spectating)
                    SpectatorMirror.Close(SpectatorMirror.Menu.Tree);
                else
                    SetOpen(false);
            };

            resetButton = NewButton(pr, "Reset", "Reset all", new Vector2(16f, -12f), new Vector2(150f, 38f));
            resetLabel = resetButton.GetComponentInChildren<Text>();
            resetButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                if (passives != null && !Spectating && passives.RespecCharges > 0 && Preview.Spent > 0)
                {
                    if (draft == null) BeginDraft();
                    draft.ResetAll();
                    resetPending = passives.Allocation.Spent > 0;
                    selected = null;
                    dirty = true;
                }
            };

            // The window the tree is seen through: drag to pan, scroll or pinch to zoom.
            Image view = UiKit.NewImage("TreeView", pr, new Color(0f, 0f, 0f, 0.18f));
            view.raycastTarget = true;
            RectTransform vr = view.rectTransform;
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = new Vector2(16f, ViewBottom);
            vr.offsetMax = new Vector2(-16f, -ViewTop);
            view.gameObject.AddComponent<RectMask2D>();
            viewRect = view.rectTransform;
            var nav = view.gameObject.AddComponent<ViewHandler>();
            nav.Click = () =>
            {
                if (suppressPinchClicks || Time.frameCount <= pinchEndedFrame + 1) return;
                selected = null;
                hovered = null;
                ShowInfo(null);
            };
            nav.Drag = delta => { if (!suppressPinchClicks) Pan(delta); };
            nav.Scroll = (amount, at) => Zoom(amount > 0f ? 1.15f : 1f / 1.15f, at);

            var contentGo = new GameObject("TreeContent", typeof(RectTransform));
            content = (RectTransform)contentGo.transform;
            content.SetParent(viewRect, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = Vector2.zero;
            ResetView();

            zoomInButton = NewButton(pr, "ZoomIn", "+", Vector2.zero, new Vector2(38f, 38f));
            TopRight(zoomInButton.rectTransform, new Vector2(12f + 2f * 46f, -12f), new Vector2(38f, 38f));
            zoomInButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => Zoom(1.25f, null);
            zoomOutButton = NewButton(pr, "ZoomOut", "-", Vector2.zero, new Vector2(38f, 38f));
            TopRight(zoomOutButton.rectTransform, new Vector2(12f + 46f, -12f), new Vector2(38f, 38f));
            zoomOutButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => Zoom(1f / 1.25f, null);
            centreButton = NewButton(pr, "Centre", "Centre", Vector2.zero, new Vector2(88f, 38f));
            centreButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ResetView();
            UpdateZoomButtons();

            // Every graph edge is one uninterrupted straight connection.
            var linked = new HashSet<string>();
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                foreach (string other in node.Links)
                {
                    string key = string.CompareOrdinal(node.Id, other) < 0 ? node.Id + "|" + other : other + "|" + node.Id;
                    if (!linked.Add(key)) continue;
                    Vector2 a = NodePosition(node), b = NodePosition(PassiveTree.Get(other));
                    Image line = UiKit.NewImage("Link", content, Color.gray);
                    line.raycastTarget = false;
                    RectTransform lr = line.rectTransform;
                    lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
                    lr.anchoredPosition = (a + b) * 0.5f;
                    lr.sizeDelta = new Vector2((b - a).magnitude, 6f);
                    lr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    links.Add(new LinkView { A = node.Id, B = other, Line = line });
                }
            }

            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                float size = node.Keystone ? 60f : node.Notable ? 62f : node.Id == PassiveTree.OriginId ? 56f : 44f;
                Image ring = UiKit.NewImage("Node_" + node.Id, content, Color.white);
                // Keystones are diamonds, so they read as different in kind, not just bigger.
                ring.sprite = node.Keystone ? UiKit.Square : UiKit.Disc;
                if (node.Keystone)
                    ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                ring.raycastTarget = true;
                RectTransform rr = ring.rectTransform;
                rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
                rr.anchoredPosition = NodePosition(node);
                rr.sizeDelta = new Vector2(size, size);

                Image body = UiKit.NewImage("Body", rr, Color.white);
                body.sprite = node.Keystone ? UiKit.Square : UiKit.Disc;
                UiKit.Stretch(body.rectTransform, node.Notable ? 6f : 4f);

                var handler = ring.gameObject.AddComponent<NodeHandler>();
                PassiveNode captured = node;
                handler.Enter = () => OnHover(captured);
                handler.Exit = OnHoverEnd;
                handler.Click = button => OnClick(captured, button);

                views.Add(new NodeView { Node = node, Ring = ring, Body = body });
            }

            // Notables and keystones are named on the tree, so it reads at a glance (after the
            // circles, so no circle covers a name).
            foreach (NodeView v in views)
            {
                if (!v.Node.Notable)
                    continue;
                float size = v.Ring.rectTransform.sizeDelta.x;
                Text label = UiKit.NewText("Label_" + v.Node.Id, content, v.Node.Name, 15, UiKit.DimText, TextAnchor.UpperCenter);
                label.raycastTarget = false;
                RectTransform tr = label.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, 1f);
                tr.anchoredPosition = NodePosition(v.Node) - new Vector2(0f, size * (v.Node.Keystone ? 0.72f : 0.5f) + 2f);
                tr.sizeDelta = new Vector2(150f, 20f);
                v.Label = label;
            }

            // Controls and the touch refund action stay along the bottom.
            Image info = UiKit.NewImage("Info", pr, new Color(0f, 0f, 0f, 0.35f));
            RectTransform ir = info.rectTransform;
            ir.anchorMin = new Vector2(0f, 0f);
            ir.anchorMax = new Vector2(1f, 0f);
            ir.pivot = new Vector2(0.5f, 0f);
            ir.offsetMin = new Vector2(16f, 16f);
            ir.offsetMax = new Vector2(-16f, 96f);
            infoText = UiKit.NewText("Text", info.rectTransform, "", 18, UiKit.TextColor, TextAnchor.MiddleLeft);
            infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform tr0 = infoText.rectTransform;
            tr0.anchorMin = Vector2.zero;
            tr0.anchorMax = Vector2.one;
            tr0.offsetMin = new Vector2(12f, 0f);
            tr0.offsetMax = new Vector2(-310f, 0f);

            confirmButton = NewButton(info.rectTransform, "Confirm", "Confirm", Vector2.zero, new Vector2(130f, 40f));
            TopRight(confirmButton.rectTransform, new Vector2(156f, -20f), new Vector2(130f, 40f));
            confirmButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ConfirmChanges();
            cancelButton = NewButton(info.rectTransform, "Cancel", "Cancel", Vector2.zero, new Vector2(130f, 40f));
            TopRight(cancelButton.rectTransform, new Vector2(12f, -20f), new Vector2(130f, 40f));
            cancelButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => CancelChanges();

            // Outside the masked tree so details remain readable at every zoom level.
            tooltip = UiKit.NewImage("PassiveTooltip", pr, new Color(0.06f, 0.05f, 0.04f, 0.98f));
            tooltip.raycastTarget = false;
            UiKit.AddOutline(tooltip, UiKit.BorderColor, 2f);
            RectTransform tip = tooltip.rectTransform;
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 0.5f);
            tip.pivot = new Vector2(0f, 1f);
            tooltipText = UiKit.NewText("Text", tip, "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            tooltipText.raycastTarget = false;
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            tooltipText.rectTransform.anchorMin = tooltipText.rectTransform.anchorMax = new Vector2(0f, 1f);
            tooltipText.rectTransform.pivot = new Vector2(0f, 1f);
            tooltipText.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            tooltip.gameObject.SetActive(false);
        }

        private static Image NewButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            Image button = UiKit.NewImage(name, parent, new Color(0.18f, 0.14f, 0.10f, 1f));
            UiKit.Inset(button);
            button.raycastTarget = true;
            UiKit.TopLeft(button.rectTransform, pos, size);
            UiKit.AddOutline(button, UiKit.BorderColor, 1.5f);
            Text text = UiKit.NewText("Label", button.rectTransform, label, 17, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, 0f);
            return button;
        }

        private class ViewHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
        {
            public System.Action Click;
            public void OnPointerClick(PointerEventData eventData) { if (!eventData.dragging) Click?.Invoke(); }
            public System.Action<Vector2> Drag;
            public System.Action<float, Vector2> Scroll;

            public void OnBeginDrag(PointerEventData eventData) { }
            public void OnDrag(PointerEventData eventData) => Drag?.Invoke(eventData.delta);
            public void OnScroll(PointerEventData eventData) => Scroll?.Invoke(eventData.scrollDelta.y, eventData.position);
        }

        private class NodeHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            public System.Action Enter;
            public System.Action Exit;
            public System.Action<PointerEventData.InputButton> Click;

            public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
            public void OnPointerClick(PointerEventData eventData) => Click?.Invoke(eventData.button);
        }
    }
}
