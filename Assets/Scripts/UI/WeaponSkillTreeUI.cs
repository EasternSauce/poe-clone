using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.Audio;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.UI
{
    /// <summary>
    /// The equipped weapon's skill tree (T, or WEAPON on touch), laid out and handled like the passive
    /// tree: click (or tap twice) to preview spending, then Confirm or Cancel the draft. Hovering (or
    /// tapping) shows what a node gives in a tooltip; the skill origin shows the skill's current numbers.
    /// </summary>
    public sealed class WeaponSkillTreeUI : MonoBehaviour
    {
        private const float MinZoom = 0.5f, MaxZoom = 2.5f, Margin = 20f;
        private static readonly Color Locked = new Color(0.16f, 0.15f, 0.14f, 1f);
        private static readonly Color Pending = new Color(0.3f, 0.85f, 1f);
        private static WeaponSkillTreeUI instance;

        private sealed class NodeView
        {
            public WeaponSkillNode Node;   // null for the skill origin
            public Image Ring, Body;
            public Text Label;
        }

        private sealed class LinkView
        {
            public string Child;
            public Image Line;
        }

        private readonly List<NodeView> views = new List<NodeView>();
        private readonly List<LinkView> links = new List<LinkView>();
        private PlayerSkills skills;
        private Canvas canvas;
        private RectTransform panelRect, viewRect, content, infoRect;
        private Text title, pointsText, infoText, tooltipText, badgeText;
        private Image badge, tooltip, confirmButton, cancelButton;
        private GameObject discardDialog;
        private ItemData weapon;
        private WeaponSkillProgress draft;
        private NodeView hovered, selected, shown;
        private bool closeRequested, placeTooltip, dirty = true, layoutWasTouch;
        private Vector2 tooltipOffset, layoutSize;
        private bool pinching, suppressPinchClicks;
        private int pinchIdA, pinchIdB, pinchEndedFrame = -100;
        private float pinchDistance;
        private Vector2 pinchMiddle;

        public static bool IsOpen => instance != null && instance.panelRect != null && instance.panelRect.gameObject.activeSelf;
        private bool HasPendingChanges => draft != null && weapon != null && draft.nodes.Count != weapon.SkillProgress.nodes.Count;
        private StatType Grant => weapon.MainSkillGrant.Value;

        public static void SetOpen(bool open)
        {
            if (instance == null) return;
            if (open && (instance.skills == null || !instance.skills.enabled || instance.skills.MainWeapon == null)) return;
            if (!open && instance.HasPendingChanges && IsOpen)
            {
                instance.closeRequested = true;
                instance.hovered = null;
                instance.ShowInfo(null);
                instance.ResetPinch();
                instance.discardDialog.SetActive(true);
                instance.discardDialog.transform.SetAsLastSibling();
                return;
            }
            if (open)
            {
                PassiveTreeUI.SetOpen(false);
                if (PassiveTreeUI.IsOpen) return;
                SkillBarUI.SetOpen(false);
                DialogueUI.Close();
                instance.skills.CancelActions();
            }
            instance.Close();
            if (!open) return;
            instance.panelRect.gameObject.SetActive(true);
            instance.Rebuild();
            Canvas.ForceUpdateCanvases();
            instance.LayoutControls();
            instance.ResetView();
        }

        private void Close()
        {
            panelRect.gameObject.SetActive(false);
            draft = null;
            closeRequested = false;
            discardDialog.SetActive(false);
            ResetPinch();
            hovered = selected = null;
            ShowInfo(null);
            UiKit.ClickConsumedFrame = Time.frameCount;
        }

        private void Awake()
        {
            instance = this;
            Build();
            panelRect.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (instance == this) instance = null; }

        private void Update()
        {
            if (skills == null || !skills.enabled) skills = FindAnyObjectByType<PlayerSkills>();
            bool usable = skills != null && skills.enabled && skills.MainWeapon != null && !skills.GetComponent<PlayerStats>().IsDead;
            if (!usable) { badge.gameObject.SetActive(false); if (IsOpen) Close(); return; }
            UpdateBadge(skills.MainWeapon.SkillProgress.Available);
            var k = Keyboard.current;
            if (!UiKit.IsTypingInTextField() && k != null)
            {
                if (k.tKey.wasPressedThisFrame) SetOpen(!IsOpen);
                else if (IsOpen && k.escapeKey.wasPressedThisFrame)
                {
                    if (closeRequested) KeepEditing();
                    else SetOpen(false);
                }
            }
            if (!IsOpen) return;
            if (weapon != skills.MainWeapon) { Close(); SetOpen(true); return; }
            if (draft.level != weapon.SkillProgress.level) { draft.level = weapon.SkillProgress.level; dirty = true; }
            if (!closeRequested) UpdatePinch();
            if (dirty) { dirty = false; Refresh(); }
        }

        // A pulsing button under the passive points one while the weapon has points to spend
        // (desktop; touch shows them on its WEAPON button). Clicking it opens the tree.
        private void UpdateBadge(int available)
        {
            bool show = !TouchMode.Active && available > 0 && !IsOpen && !DialogueUI.IsOpen && !PassiveTreeUI.IsOpen && !SkillBarUI.PickerOpen;
            if (badge.gameObject.activeSelf != show) badge.gameObject.SetActive(show);
            if (!show) return;
            float scale = canvas.scaleFactor;
            badge.rectTransform.anchoredPosition = new Vector2(16f / scale, -PassiveTreeUI.BadgeBottomPixels / scale);
            string text = "+" + available + " weapon point" + (available > 1 ? "s" : "") + "  (T)";
            if (badgeText.text != text) badgeText.text = text;
            badge.rectTransform.sizeDelta = new Vector2(badgeText.preferredWidth + 36f, 36f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            badgeText.color = Color.Lerp(UiKit.Gold, new Color(1f, 0.95f, 0.7f), pulse);
            badge.color = new Color(0.12f, 0.09f, 0.04f, 0.75f + 0.15f * pulse);
        }

        // ------------------------------------------------------------------ draft

        private void OnClick(NodeView view, PointerEventData.InputButton button)
        {
            if (closeRequested || (TouchMode.Active && (suppressPinchClicks || Time.frameCount <= pinchEndedFrame + 1))) return;
            // Touch: the first tap only selects (shows what it gives); a second tap takes it.
            bool take = button == PointerEventData.InputButton.Left && (!TouchMode.Active || selected == view);
            selected = view;
            dirty = true;
            if (view.Node == null) return;
            string id = view.Node.Id;
            if (button == PointerEventData.InputButton.Right || (take && draft.nodes.Contains(id))) Refund(id);
            else if (take && draft.Allocate(Grant, id) && AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(AudioManager.Instance.Sfx("ui_click"));
        }

        // Only pending end nodes come back off the draft; confirmed nodes are reset at Mentor Ilyra.
        private bool CanRefund(string id)
        {
            if (!draft.nodes.Contains(id) || weapon.SkillProgress.nodes.Contains(id)) return false;
            foreach (string other in draft.nodes)
                if (WeaponSkillTrees.Find(Grant, other)?.Parent == id) return false;
            return true;
        }

        private void Refund(string id)
        {
            if (CanRefund(id)) draft.nodes.Remove(id);
        }

        private void ConfirmChanges()
        {
            if (closeRequested || !HasPendingChanges) return;
            var added = draft.nodes.GetRange(weapon.SkillProgress.nodes.Count, draft.nodes.Count - weapon.SkillProgress.nodes.Count);
            if (!skills.AllocateWeaponNodes(weapon, added)) return;
            var audio = AudioManager.Instance;
            if (audio != null) audio.PlayUI(audio.Sfx("passive_confirm"));
            draft = weapon.SkillProgress.Copy();
            hovered = null;
            ShowInfo(null);
            dirty = true;
        }

        private void KeepEditing()
        {
            closeRequested = false;
            discardDialog.SetActive(false);
            dirty = true;
        }

        private void DiscardAndClose()
        {
            draft = null;
            Close();
        }

        // ------------------------------------------------------------------ display

        private void Refresh()
        {
            var real = weapon.SkillProgress;
            title.text = SkillGrants.SkillName(Grant).ToUpperInvariant();
            int unspent = draft.Available;
            pointsText.text = weapon.Name + "  |  Level " + real.level + "/10  |  " +
                (real.level == 10 ? "Maximum level" : "Weapon experience " + Mathf.FloorToInt(100f * real.experience / real.RequiredExperience) + "%") + "  |  " +
                (unspent > 0 ? "<color=#FFD040>" + unspent + " point" + (unspent > 1 ? "s" : "") + " to spend</color>"
                    : "<color=#" + UiKit.Hex(UiKit.DimText) + ">No points to spend - one more each weapon level</color>");
            Color skill = SkillBook.ForGrant(Grant).Color;
            foreach (NodeView v in views)
            {
                if (v.Node == null) continue;
                bool taken = draft.nodes.Contains(v.Node.Id);
                bool available = draft.CanAllocate(Grant, v.Node.Id);
                v.Body.color = taken ? (real.nodes.Contains(v.Node.Id) ? skill : Pending) : available ? Color.Lerp(Locked, skill, 0.35f) : Locked;
                v.Ring.color = taken ? UiKit.Gold : available ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(0.35f, 0.32f, 0.28f, 1f);
                if (v.Label != null) v.Label.color = taken ? UiKit.Gold : available ? UiKit.TextColor : UiKit.DimText;
            }
            foreach (LinkView l in links)
                l.Line.color = draft.nodes.Contains(l.Child) ? UiKit.Gold : new Color(0.30f, 0.27f, 0.23f, 1f);
            ShowInfo(hovered ?? (TouchMode.Active ? selected : null));
        }

        private void ShowInfo(NodeView view)
        {
            if (shown != view) placeTooltip = true;
            shown = view;
            tooltip.gameObject.SetActive(view != null);
            infoText.text = TouchMode.Active
                ? "Tap a node to inspect it; tap again to take it. Drag to look around, pinch to zoom. Reset at Mentor Ilyra in Haven."
                : "Click to take a node; click a pending end node again to undo it. Drag to look around, scroll to zoom. T closes. Reset at Mentor Ilyra in Haven.";
            if (view == null || weapon == null || draft == null) return;

            string dim = "<color=#" + UiKit.Hex(UiKit.DimText) + ">";
            string text;
            if (view.Node == null)
            {
                var id = SkillBook.ForGrant(Grant).Id;
                var real = weapon.SkillProgress;
                text = "<b><color=#" + UiKit.Hex(UiKit.Gold) + ">" + SkillGrants.SkillName(Grant) + "</color></b>\n" +
                    skills.DamageSummary(id, real.level) + "\nCost: " + skills.ManaCost(id).ToString("0.#") + " " + skills.CostResource + "\n" +
                    dim + real.nodes.Count + "/" + real.level + " points allocated. This weapon owns its progress.\n" +
                    "Reset at Mentor Ilyra in Haven for " + skills.WeaponResetPrice + " gold.</color>";
            }
            else
            {
                var node = view.Node;
                bool taken = draft.nodes.Contains(node.Id);
                string state = weapon.SkillProgress.nodes.Contains(node.Id) ? "Allocated."
                    : taken ? "Pending - confirm to apply."
                    : draft.CanAllocate(Grant, node.Id) ? (TouchMode.Active ? "Tap again to take it." : "Click to take it.")
                    : draft.Available <= 0 ? "No points left."
                    : node.Parent != null && !draft.nodes.Contains(node.Parent) ? "Take the node before it first."
                    : "Choose one elemental school.";
                text = "<b><color=#" + UiKit.Hex(UiKit.Gold) + ">" + node.Name + "</color></b>  " + dim +
                    (node.Keystone ? "Keystone" : node.Notable ? "Notable" : "Passive") + "</color>\n" +
                    node.Description + "\n" + dim + state + "</color>";
            }
            tooltipText.text = text;
            tooltipText.fontSize = TouchMode.Active ? TouchMode.ReadingFontSize : 18;
            float width = Mathf.Min(TouchMode.Active ? (TouchMode.Portrait ? 780f : 560f) : 380f, panelRect.rect.width - 16f);
            tooltip.rectTransform.sizeDelta = new Vector2(width, 100f);
            tooltipText.rectTransform.sizeDelta = new Vector2(width - 24f, 0f);
            tooltip.rectTransform.sizeDelta = new Vector2(width, tooltipText.preferredHeight + 24f);
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;
            LayoutControls();
            if (shown == null || !tooltip.gameObject.activeSelf) return;
            // Keep a fixed offset from the inspected node: clamp once on selection, then let
            // panning and zooming carry the tooltip along, as on the passive tree.
            Vector2 at = panelRect.InverseTransformPoint(shown.Ring.rectTransform.position);
            Rect bounds = panelRect.rect;
            Vector2 size = tooltip.rectTransform.sizeDelta;
            if (placeTooltip)
            {
                float x = at.x + 20f;
                if (x + size.x > bounds.xMax - 8f) x = at.x - size.x - 20f;
                Vector2 placed = new Vector2(
                    Mathf.Clamp(x, bounds.xMin + 8f, bounds.xMax - size.x - 8f),
                    Mathf.Clamp(at.y - 20f, bounds.yMin + size.y + 8f, bounds.yMax - 8f));
                tooltipOffset = placed - at;
                placeTooltip = false;
            }
            Vector2 position = at + tooltipOffset;
            if (TouchMode.Active)
            {
                position.x = Mathf.Clamp(position.x, bounds.xMin + 8f, Mathf.Max(bounds.xMin + 8f, bounds.xMax - size.x - 8f));
                position.y = Mathf.Clamp(position.y, bounds.yMin + size.y + 8f, bounds.yMax - 140f);
            }
            tooltip.rectTransform.anchoredPosition = position;
        }

        private void LayoutControls()
        {
            bool touch = TouchMode.Active;
            if (layoutSize == panelRect.rect.size && layoutWasTouch == touch) return;
            layoutSize = panelRect.rect.size;
            layoutWasTouch = touch;
            title.fontSize = touch ? 40 : 26;
            PassiveTreeUI.TopStrip(title.rectTransform, -14f, touch ? 56f : 34f);
            pointsText.fontSize = touch ? 26 : 18;
            PassiveTreeUI.TopStrip(pointsText.rectTransform, touch ? -72f : -46f, touch ? 44f : 24f);
            float infoHeight = touch ? 128f : 80f;
            viewRect.offsetMax = new Vector2(-16f, touch ? -128f : -76f);
            viewRect.offsetMin = new Vector2(16f, 16f + infoHeight + 8f);
            infoRect.offsetMax = new Vector2(-16f, 16f + infoHeight);
            Vector2 button = touch ? new Vector2(200f, 96f) : new Vector2(130f, 40f);
            PassiveTreeUI.TopRight(confirmButton.rectTransform, new Vector2(12f, -(infoHeight - button.y) * 0.5f), button);
            PassiveTreeUI.TopRight(cancelButton.rectTransform, new Vector2(24f + button.x, -(infoHeight - button.y) * 0.5f), button);
            confirmButton.GetComponentInChildren<Text>().fontSize = cancelButton.GetComponentInChildren<Text>().fontSize = touch ? 30 : 16;
            infoText.fontSize = touch ? 26 : 18;
            infoText.rectTransform.offsetMin = new Vector2(12f, 0f);
            infoText.rectTransform.offsetMax = new Vector2(-(36f + 2f * button.x), 0f);
            var window = (RectTransform)discardDialog.transform.Find("Window");
            float width = Mathf.Min(680f, panelRect.rect.width - 40f);
            window.sizeDelta = new Vector2(width, 320f);
            Text prompt = window.Find("Prompt").GetComponent<Text>();
            prompt.fontSize = touch ? 32 : 26;
            UiKit.TopLeft(prompt.rectTransform, new Vector2(24f, -24f), new Vector2(width - 48f, 150f));
            Vector2 choice = new Vector2((width - 72f) * 0.5f, 88f);
            UiKit.TopLeft((RectTransform)window.Find("No"), new Vector2(24f, -208f), choice);
            UiKit.TopLeft((RectTransform)window.Find("Yes"), new Vector2(width * 0.5f + 12f, -208f), choice);
        }

        // ------------------------------------------------------------------ moving around

        private void Pan(Vector2 screenDelta)
        {
            float scale = viewRect.lossyScale.x > 0f ? viewRect.lossyScale.x : 1f;
            content.anchoredPosition += screenDelta / scale;
            ClampContent();
        }

        private void Zoom(float factor, Vector2? screenPoint)
        {
            float before = content.localScale.x;
            float after = Mathf.Clamp(before * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(before, after)) return;
            Vector2 pivot = Vector2.zero;
            if (screenPoint.HasValue)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, screenPoint.Value, null, out pivot);
            Vector2 treePoint = (pivot - content.anchoredPosition) / before;
            content.localScale = Vector3.one * after;
            content.anchoredPosition = pivot - treePoint * after;
            ClampContent();
        }

        private Vector2 Extent()
        {
            Vector2 extent = Vector2.zero;
            foreach (NodeView v in views)
                if (v.Node != null) extent = Vector2.Max(extent, new Vector2(Mathf.Abs(v.Node.Position.x), Mathf.Abs(v.Node.Position.y)));
            return extent;
        }

        // Keeps some of the tree in the window however far it is dragged.
        private void ClampContent()
        {
            Vector2 limit = Vector2.Max(Vector2.zero, Extent() * content.localScale.x - viewRect.rect.size * 0.5f + Vector2.one * 90f);
            Vector2 p = content.anchoredPosition;
            content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -limit.x, limit.x), Mathf.Clamp(p.y, -limit.y, limit.y));
        }

        // Fits the whole tree, with room for the outer names, into the window.
        private void ResetView()
        {
            Vector2 size = viewRect.rect.size, extent = Extent() + Vector2.one * 70f;
            float fit = Mathf.Min(size.x / (2f * extent.x), size.y / (2f * extent.y));
            content.localScale = Vector3.one * Mathf.Clamp(fit, MinZoom, MaxZoom);
            content.anchoredPosition = Vector2.zero;
        }

        private void UpdatePinch()
        {
            if (!TouchMode.Active || Touchscreen.current == null) return;
            TouchControl first = null, second = null;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;
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
            Vector2 a = first.position.ReadValue(), b = second.position.ReadValue();
            if (!pinching)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(viewRect, a) ||
                    !RectTransformUtility.RectangleContainsScreenPoint(viewRect, b)) return;
                pinching = suppressPinchClicks = true;
                pinchIdA = first.touchId.ReadValue();
                pinchIdB = second.touchId.ReadValue();
                pinchMiddle = (a + b) * 0.5f;
                pinchDistance = Vector2.Distance(a, b);
                return;
            }
            if (pinchIdA != first.touchId.ReadValue() || pinchIdB != second.touchId.ReadValue())
            {
                pinching = false;
                return;
            }
            Vector2 middle = (a + b) * 0.5f;
            float distance = Vector2.Distance(a, b);
            if (pinchDistance > 0f && distance > 0f) Zoom(distance / pinchDistance, pinchMiddle);
            Pan(middle - pinchMiddle);
            pinchMiddle = middle;
            pinchDistance = distance;
        }

        private void ResetPinch()
        {
            if (pinching || suppressPinchClicks) pinchEndedFrame = Time.frameCount;
            pinching = suppressPinchClicks = false;
        }

        // Touch picks the nearest node within a finger's reach, since nodes are small at overview zoom.
        private bool TryTouchNode(Vector2 screenPoint)
        {
            if (closeRequested || suppressPinchClicks || Time.frameCount <= pinchEndedFrame + 1) return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewRect, screenPoint)) return false;
            NodeView nearest = null;
            float radius = 32f * Mathf.Min(Screen.width, Screen.height) / 390f, best = float.PositiveInfinity;
            foreach (NodeView v in views)
            {
                Vector2 at = RectTransformUtility.WorldToScreenPoint(null, v.Ring.rectTransform.position);
                if (!RectTransformUtility.RectangleContainsScreenPoint(viewRect, at)) continue;
                float distance = (at - screenPoint).sqrMagnitude;
                float reach = Mathf.Max(radius, v.Ring.rectTransform.rect.width * Mathf.Abs(v.Ring.rectTransform.lossyScale.x) * 0.72f);
                if (distance > reach * reach || distance > best) continue;
                best = distance;
                nearest = v;
            }
            if (nearest == null) return false;
            OnClick(nearest, PointerEventData.InputButton.Left);
            return true;
        }

        // ------------------------------------------------------------------ building

        private void Rebuild()
        {
            weapon = skills.MainWeapon;
            draft = weapon.SkillProgress.Copy();
            foreach (Transform child in content) Destroy(child.gameObject);
            views.Clear();
            links.Clear();
            var nodes = WeaponSkillTrees.For(Grant);
            if (nodes == null) { Close(); return; }
            foreach (var node in nodes)
            {
                Vector2 from = node.Parent != null ? WeaponSkillTrees.Find(Grant, node.Parent).Position : Vector2.zero, to = node.Position;
                Image line = UiKit.NewImage("Link", content, Color.gray);
                line.raycastTarget = false;
                RectTransform lr = line.rectTransform;
                lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = (from + to) * 0.5f;
                lr.sizeDelta = new Vector2((to - from).magnitude, 4f);
                lr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                links.Add(new LinkView { Child = node.Id, Line = line });
            }

            NodeView origin = AddNode(null, Vector2.zero, 64f);
            var definition = SkillBook.ForGrant(Grant);
            origin.Ring.color = UiKit.Gold;
            origin.Body.color = definition.Color;
            var icon = UiKit.NewImage("Skill icon", origin.Body.transform, Color.white);
            icon.sprite = SkillIconFactory.Get(definition.Id);
            icon.raycastTarget = false;
            UiKit.Stretch(icon.rectTransform, 5f);
            foreach (var node in nodes)
                AddNode(node, node.Position, node.Keystone ? 32f : node.Notable ? 40f : 28f);

            // Notables and keystones are named on the tree (after the circles, so none covers a name).
            foreach (NodeView v in views)
            {
                if (v.Node == null || !(v.Node.Notable || v.Node.Keystone)) continue;
                float size = v.Ring.rectTransform.sizeDelta.x;
                Text label = UiKit.NewText("Label_" + v.Node.Id, content, v.Node.Name, 13, UiKit.DimText, TextAnchor.UpperCenter);
                label.raycastTarget = false;
                RectTransform tr = label.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, 1f);
                tr.anchoredPosition = v.Node.Position - new Vector2(0f, size * (v.Node.Keystone ? 0.72f : 0.5f) + 2f);
                tr.sizeDelta = new Vector2(130f, 18f);
                v.Label = label;
            }
            dirty = true;
        }

        private NodeView AddNode(WeaponSkillNode node, Vector2 at, float size)
        {
            bool keystone = node != null && node.Keystone;
            Image ring = UiKit.NewImage(node != null ? "Node_" + node.Id : "Skill origin", content, Color.white);
            // Keystones are diamonds, as on the passive tree.
            ring.sprite = keystone ? UiKit.Square : UiKit.Disc;
            if (keystone) ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            ring.raycastTarget = true;
            RectTransform rr = ring.rectTransform;
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = at;
            rr.sizeDelta = new Vector2(size, size);
            Image body = UiKit.NewImage("Body", rr, Color.white);
            body.sprite = keystone ? UiKit.Square : UiKit.Disc;
            UiKit.Stretch(body.rectTransform, node != null && node.Notable ? 5f : 3f);
            var view = new NodeView { Node = node, Ring = ring, Body = body };
            var handler = ring.gameObject.AddComponent<NodeHandler>();
            handler.Enter = () => { if (!TouchMode.Active) { hovered = view; ShowInfo(view); } };
            handler.Exit = () => { if (!TouchMode.Active) { hovered = null; ShowInfo(null); } };
            handler.Click = e =>
            {
                if (TouchMode.Active) TryTouchNode(e.position);
                else OnClick(view, e.button);
            };
            views.Add(view);
            return view;
        }

        private void Build()
        {
            canvas = UiKit.NewCanvas("Weapon skill tree", transform, 117, out var group);
            group.interactable = group.blocksRaycasts = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            Image panel = UiKit.NewImage("WeaponTreePanel", canvas.transform, UiKit.ReadablePanelColor);
            UiKit.Grain(panel);
            panel.raycastTarget = true;
            panelRect = panel.rectTransform;
            UiKit.Stretch(panelRect, Margin);
            UiKit.Frame(panelRect);
            panelRect.gameObject.AddComponent<UiAppear>().offset = Vector2.zero;
            TouchMode.AddBlocker(panelRect);

            title = UiKit.Heading(UiKit.NewText("Title", panelRect, "WEAPON SKILL", 26, UiKit.Gold, TextAnchor.UpperCenter));
            PassiveTreeUI.TopStrip(title.rectTransform, -14f, 34f);
            pointsText = UiKit.NewText("Points", panelRect, "", 18, UiKit.TextColor, TextAnchor.UpperCenter);
            PassiveTreeUI.TopStrip(pointsText.rectTransform, -46f, 24f);

            Image close = UiKit.NewImage("Close", panelRect, new Color(0.25f, 0.1f, 0.08f, 1f));
            close.raycastTarget = true;
            close.gameObject.AddComponent<MobileCloseTarget>();
            PassiveTreeUI.TopRight(close.rectTransform, new Vector2(12f, -12f), new Vector2(38f, 38f));
            Text x = UiKit.NewText("X", close.rectTransform, "X", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(x.rectTransform, 0f);
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ => SetOpen(false);

            // The window the tree is seen through: drag to pan, scroll or pinch to zoom.
            Image view = UiKit.NewImage("TreeView", panelRect, new Color(0f, 0f, 0f, 0.18f));
            view.raycastTarget = true;
            viewRect = view.rectTransform;
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            view.gameObject.AddComponent<RectMask2D>();
            var nav = view.gameObject.AddComponent<ViewHandler>();
            nav.Click = at =>
            {
                if (suppressPinchClicks || Time.frameCount <= pinchEndedFrame + 1) return;
                if (TouchMode.Active && TryTouchNode(at)) return;
                selected = hovered = null;
                ShowInfo(null);
            };
            nav.Drag = delta => { if (!suppressPinchClicks) Pan(delta); };
            nav.Scroll = (amount, at) => { if (amount != 0f) Zoom(amount > 0f ? 1.15f : 1f / 1.15f, at); };
            content = (RectTransform)new GameObject("TreeContent", typeof(RectTransform)).transform;
            content.SetParent(viewRect, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = Vector2.zero;

            Image info = UiKit.NewImage("Info", panelRect, new Color(0f, 0f, 0f, 0.35f));
            infoRect = info.rectTransform;
            infoRect.anchorMin = Vector2.zero;
            infoRect.anchorMax = new Vector2(1f, 0f);
            infoRect.pivot = new Vector2(0.5f, 0f);
            infoRect.offsetMin = new Vector2(16f, 16f);
            infoText = UiKit.NewText("Text", infoRect, "", 18, UiKit.TextColor, TextAnchor.MiddleLeft);
            infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoText.rectTransform.anchorMin = Vector2.zero;
            infoText.rectTransform.anchorMax = Vector2.one;
            confirmButton = PassiveTreeUI.NewButton(infoRect, "Confirm", "Confirm", Vector2.zero, new Vector2(130f, 40f));
            confirmButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => ConfirmChanges();
            cancelButton = PassiveTreeUI.NewButton(infoRect, "Cancel", "Cancel", Vector2.zero, new Vector2(130f, 40f));
            cancelButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => SetOpen(false);

            // Outside the masked tree so details remain readable at every zoom level.
            tooltip = UiKit.NewImage("WeaponTreeTooltip", panelRect, new Color(0.06f, 0.05f, 0.04f, 0.98f));
            tooltip.raycastTarget = false;
            UiKit.Grain(tooltip);
            RectTransform tip = tooltip.rectTransform;
            UiKit.ThinFrame(tip);
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 0.5f);
            tip.pivot = new Vector2(0f, 1f);
            tooltipText = UiKit.NewText("Text", tip, "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            tooltipText.raycastTarget = false;
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            tooltipText.rectTransform.anchorMin = tooltipText.rectTransform.anchorMax = tooltipText.rectTransform.pivot = new Vector2(0f, 1f);
            tooltipText.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            tooltip.gameObject.SetActive(false);

            Image shade = UiKit.NewImage("DiscardWeaponTreeDialog", panelRect, new Color(0f, 0f, 0f, 0.8f));
            UiKit.Stretch(shade.rectTransform, 0f);
            shade.raycastTarget = true;
            discardDialog = shade.gameObject;
            TouchMode.AddBlocker(shade.rectTransform);
            Image window = UiKit.NewImage("Window", shade.rectTransform, UiKit.ReadablePanelColor);
            window.raycastTarget = true;
            window.rectTransform.anchorMin = window.rectTransform.anchorMax = window.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiKit.ThinFrame(window.rectTransform);
            Text prompt = UiKit.NewText("Prompt", window.rectTransform, "Discard your unconfirmed weapon tree changes?", 32, UiKit.TextColor, TextAnchor.MiddleCenter);
            prompt.horizontalOverflow = HorizontalWrapMode.Wrap;
            Image no = PassiveTreeUI.NewButton(window.rectTransform, "No", "No", Vector2.zero, Vector2.one);
            no.GetComponentInChildren<Text>().fontSize = 30;
            no.gameObject.AddComponent<TouchPointerRelay>().Up += _ => KeepEditing();
            Image yes = PassiveTreeUI.NewButton(window.rectTransform, "Yes", "Yes", Vector2.zero, Vector2.one);
            yes.GetComponentInChildren<Text>().fontSize = 30;
            yes.gameObject.AddComponent<TouchPointerRelay>().Up += _ => DiscardAndClose();
            discardDialog.SetActive(false);

            badge = UiKit.NewImage("WeaponPointsBadge", canvas.transform, new Color(0.12f, 0.09f, 0.04f, 0.85f));
            badge.raycastTarget = true;
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(0f, 1f);
            UiKit.Grain(badge);
            UiKit.AddOutline(badge, UiKit.Gold, 2f);
            badgeText = UiKit.NewText("Text", badge.transform, "", 17, UiKit.Gold, TextAnchor.MiddleCenter);
            badgeText.font = UiKit.TitleFont;
            UiKit.Stretch(badgeText.rectTransform, 0f);
            UiKit.OnClick(badge, () => SetOpen(true));
            badge.gameObject.SetActive(false);
        }

        private sealed class ViewHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
        {
            public System.Action<Vector2> Click;
            public System.Action<Vector2> Drag;
            public System.Action<float, Vector2> Scroll;
            public void OnPointerClick(PointerEventData eventData) { if (!eventData.dragging) Click?.Invoke(eventData.position); }
            public void OnBeginDrag(PointerEventData eventData) { }
            public void OnDrag(PointerEventData eventData) => Drag?.Invoke(eventData.delta);
            public void OnScroll(PointerEventData eventData) => Scroll?.Invoke(eventData.scrollDelta.y, eventData.position);
        }

        private sealed class NodeHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            public System.Action Enter, Exit;
            public System.Action<PointerEventData> Click;
            public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
            public void OnPointerClick(PointerEventData eventData) { if (!eventData.dragging) Click?.Invoke(eventData); }
        }
    }
}
