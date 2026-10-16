using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.UI
{
    /// <summary>The equipped item's skill tree. Selection previews; allocation is an explicit click.</summary>
    public sealed class WeaponSkillTreeUI : MonoBehaviour
    {
        private static WeaponSkillTreeUI instance;
        private PlayerSkills skills;
        private Canvas canvas;
        private RectTransform panel, view, graph, info;
        private const float MinZoom = 0.5f, MaxZoom = 2.5f;
        private bool pinching, suppressPinchClicks;
        private int pinchIdA, pinchIdB, pinchEndedFrame = -2;
        private float pinchDistance;
        private Vector2 pinchMiddle;
        private Text title, subtitle, detail, summary, buttonText, badgeText, footer;
        private Image allocate, badge;
        private ItemData weapon;
        private string selected;
        private readonly Dictionary<string, Image> buttons = new Dictionary<string, Image>();
        private int lastSpent = -1, lastLevel = -1;
        private Vector2 lastSize;
        public static bool IsOpen => instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;

        public static void SetOpen(bool open)
        {
            if (instance == null) return;
            if (open && (instance.skills == null || !instance.skills.enabled || instance.skills.MainWeapon == null)) return;
            if (open)
            {
                PassiveTreeUI.SetOpen(false);
                if (PassiveTreeUI.IsOpen) return;
                SkillBarUI.SetOpen(false);
                DialogueUI.Close();
                instance.skills.CancelActions();
                instance.weapon = null;
            }
            instance.panel.gameObject.SetActive(open);
            instance.pinching = instance.suppressPinchClicks = false;
            UiKit.ClickConsumedFrame = Time.frameCount;
            if (open) instance.Refresh();
        }

        private void Awake()
        {
            instance = this;
            canvas = UiKit.NewCanvas("Weapon skill tree", transform, 117, out var group);
            group.interactable = group.blocksRaycasts = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            panel = UiKit.NewImage("Tree panel", canvas.transform, UiKit.ReadablePanelColor).rectTransform;
            panel.sizeDelta = new Vector2(1160, 850);
            panel.GetComponent<Image>().raycastTarget = true;
            UiKit.AddOutline(panel.GetComponent<Image>(), UiKit.BorderColor, 2);
            title = Label(panel, "Weapon skill", 30, new Vector2(26, -18), new Vector2(1040, 42));
            subtitle = Label(panel, "", 18, new Vector2(26, -62), new Vector2(1080, 64));
            UiKit.CloseButton(panel, () => SetOpen(false));
            view = UiKit.NewImage("Tree viewport", panel, new Color(0.09f, 0.08f, 0.07f, 1f)).rectTransform;
            UiKit.TopLeft(view, new Vector2(18, -130), new Vector2(760, 690));
            view.GetComponent<Image>().raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var navigation = view.gameObject.AddComponent<ViewHandler>();
            navigation.Drag = delta => { if (!suppressPinchClicks) Pan(delta); };
            navigation.Scroll = (amount, at) => { if (amount != 0f) Zoom(amount > 0f ? 1.15f : 1f / 1.15f, at); };
            graph = UiKit.NewRect("Branches", view);
            graph.anchorMin = graph.anchorMax = graph.pivot = new Vector2(0.5f, 0.5f);
            graph.sizeDelta = new Vector2(760, 690);
            AddViewButton("Zoom in", "+", 18f, 52f, () => Zoom(1.25f, null));
            AddViewButton("Zoom out", "−", 78f, 52f, () => Zoom(1f / 1.25f, null));
            AddViewButton("Reset view", "Reset", 138f, 90f, ResetView);
            info = UiKit.NewImage("Selected node", panel, new Color(0.12f, 0.10f, 0.08f, 1)).rectTransform;
            UiKit.TopLeft(info, new Vector2(795, -132), new Vector2(340, 690));
            detail = Label(info, "Select a node to inspect it.", 20, new Vector2(18, -18), new Vector2(304, 275));
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.verticalOverflow = VerticalWrapMode.Truncate;
            allocate = UiKit.NewImage("Allocate", info, UiKit.BorderColor);
            UiKit.TopLeft(allocate.rectTransform, new Vector2(18, -302), new Vector2(304, 52));
            buttonText = Label(allocate.rectTransform, "Allocate · 1 point", 21, Vector2.zero, new Vector2(304, 52));
            buttonText.alignment = TextAnchor.MiddleCenter;
            UiKit.OnClick(allocate, Allocate);
            summary = Label(info, "", 17, new Vector2(18, -374), new Vector2(304, 290));
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            summary.verticalOverflow = VerticalWrapMode.Truncate;
            footer = Label(panel, "Drag to pan · Scroll/pinch to zoom · N notable · K keystone | T closes | Reset allocations at Mentor Ilyra", 16,
                new Vector2(26, -820), new Vector2(1090, 25));
            badge = UiKit.NewImage("Weapon tree shortcut", canvas.transform, UiKit.PanelColor);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 0);
            badge.rectTransform.anchoredPosition = new Vector2(-325, 116);
            badge.rectTransform.sizeDelta = new Vector2(194, 40);
            badgeText = UiKit.NewText("Label", badge.transform, "Weapon tree [T]", 18, UiKit.Gold, TextAnchor.MiddleCenter);
            UiKit.Stretch(badgeText.rectTransform, 2);
            UiKit.OnClick(badge, () => SetOpen(!IsOpen));
            panel.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (instance == this) instance = null; }

        private void Update()
        {
            if (skills == null || !skills.enabled) skills = FindAnyObjectByType<PlayerSkills>();
            bool usable = skills != null && skills.enabled && skills.MainWeapon != null && !skills.GetComponent<PlayerStats>().IsDead;
            badge.gameObject.SetActive(!TouchMode.Active && usable && !IsOpen && !DialogueUI.IsOpen && !PassiveTreeUI.IsOpen);
            if (!usable) { panel.gameObject.SetActive(false); return; }
            var k = Keyboard.current;
            if (!UiKit.IsTypingInTextField() && k != null)
            {
                if (k.tKey.wasPressedThisFrame) SetOpen(!IsOpen);
                else if (IsOpen && k.escapeKey.wasPressedThisFrame) SetOpen(false);
            }
            badgeText.text = "Weapon tree [T]" + (skills.MainWeapon.SkillProgress.Available > 0 ? " · " + skills.MainWeapon.SkillProgress.Available : "");
            if (!IsOpen) return;
            UpdatePinch();
            Vector2 size = ((RectTransform)canvas.transform).rect.size;
            if (size != lastSize)
            {
                lastSize = size;
                bool portrait = size.y > size.x;
                panel.sizeDelta = portrait ? new Vector2(800, 1240) : new Vector2(1160, 850);
                UiKit.TopLeft(info, portrait ? new Vector2(18, -830) : new Vector2(795, -132), portrait ? new Vector2(764, 350) : new Vector2(340, 690));
                UiKit.TopLeft(detail.rectTransform, new Vector2(18, -18), portrait ? new Vector2(350, 310) : new Vector2(304, 275));
                UiKit.TopLeft(allocate.rectTransform, portrait ? new Vector2(398, -18) : new Vector2(18, -302), portrait ? new Vector2(348, 52) : new Vector2(304, 52));
                UiKit.TopLeft(buttonText.rectTransform, Vector2.zero, portrait ? new Vector2(348, 52) : new Vector2(304, 52));
                UiKit.TopLeft(summary.rectTransform, portrait ? new Vector2(398, -90) : new Vector2(18, -374), portrait ? new Vector2(348, 245) : new Vector2(304, 290));
                UiKit.TopLeft(footer.rectTransform, portrait ? new Vector2(26, -1194) : new Vector2(26, -820), portrait ? new Vector2(748, 42) : new Vector2(1090, 25));
                UiKit.TopLeft(title.rectTransform, new Vector2(26, -18), portrait ? new Vector2(700, 42) : new Vector2(1040, 42));
                UiKit.TopLeft(subtitle.rectTransform, new Vector2(26, -62), portrait ? new Vector2(748, 64) : new Vector2(1080, 64));
                float scale = Mathf.Min(1f, (size.x - 24f) / panel.sizeDelta.x, (size.y - 32f) / panel.sizeDelta.y);
                panel.localScale = Vector3.one * Mathf.Max(0.25f, scale);
            }
            if (weapon != skills.MainWeapon || lastSpent != weapon.SkillProgress.nodes.Count || lastLevel != weapon.SkillProgress.level) Refresh();
            else UpdateHeader();
        }

        private void Refresh()
        {
            bool switched = weapon != skills.MainWeapon;
            weapon = skills.MainWeapon;
            if (weapon?.MainSkillGrant == null) { SetOpen(false); return; }
            if (switched) { selected = null; ResetView(); }
            foreach (Transform child in graph) Destroy(child.gameObject);
            buttons.Clear();
            var nodes = WeaponSkillTrees.For(weapon.MainSkillGrant.Value);
            if (nodes == null) { SetOpen(false); return; }
            Vector2 center = new Vector2(380, -345);
            foreach (var node in nodes)
            {
                var parent = node.Parent != null ? WeaponSkillTrees.Find(weapon.MainSkillGrant.Value, node.Parent) : null;
                Vector2 from = center + (parent?.Position ?? Vector2.zero);
                Vector2 to = center + node.Position;
                var line = UiKit.NewImage("Connection", graph, weapon.SkillProgress.nodes.Contains(node.Id) ? UiKit.Gold : UiKit.BorderColor);
                line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0, 1);
                line.rectTransform.anchoredPosition = (from + to) * 0.5f;
                line.rectTransform.sizeDelta = new Vector2((to - from).magnitude, 3);
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            }
            var root = UiKit.NewImage("Skill origin", graph, SkillBook.ForGrant(weapon.MainSkillGrant.Value).Color);
            root.sprite = UiKit.Disc;
            root.rectTransform.anchorMin = root.rectTransform.anchorMax = new Vector2(0, 1);
            root.rectTransform.anchoredPosition = center;
            root.rectTransform.sizeDelta = new Vector2(65, 65);
            var icon = UiKit.NewImage("Skill icon", root.transform, Color.white);
            icon.sprite = SkillIconFactory.Get(SkillBook.ForGrant(weapon.MainSkillGrant.Value).Id);
            UiKit.Stretch(icon.rectTransform, 6);
            foreach (var node in nodes)
            {
                var image = UiKit.NewImage(node.Name, graph, Color.white);
                image.sprite = node.Keystone ? UiKit.Square : UiKit.Disc;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
                image.rectTransform.anchoredPosition = center + node.Position;
                image.rectTransform.sizeDelta = Vector2.one * (node.Keystone ? 40 : node.Notable ? 42 : 34);
                var text = UiKit.NewText("Node", image.transform, node.Keystone ? "K" : node.Notable ? "N" : (node.Depth + 1).ToString(),
                    17, UiKit.TextColor, TextAnchor.MiddleCenter);
                UiKit.Stretch(text.rectTransform, 0);
                // Keystones are diamonds, as on the passive tree; the letter stays upright.
                if (node.Keystone)
                {
                    image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                }
                string id = node.Id;
                UiKit.OnClick(image, () =>
                {
                    if (suppressPinchClicks || Time.frameCount <= pinchEndedFrame + 1) return;
                    selected = id;
                    UpdateSelection();
                });
                buttons.Add(id, image);
            }
            lastSpent = weapon.SkillProgress.nodes.Count; lastLevel = weapon.SkillProgress.level;
            UpdateHeader(); UpdateSelection();
        }

        private void UpdateHeader()
        {
            var p = weapon.SkillProgress;
            title.text = SkillGrants.SkillName(weapon.MainSkillGrant.Value) + " · Weapon specialization";
            subtitle.text = weapon.Name + "  |  Level " + p.level + "/10  |  " + p.Available + " points available\n" +
                (p.level == 10 ? "Maximum level" : "Weapon experience: " + Mathf.FloorToInt(100f * p.experience / p.RequiredExperience) + "%") +
                "  ·  This weapon owns its progress. Levels grant points only.";
        }
        private void UpdateSelection()
        {
            var p = weapon.SkillProgress;
            foreach (var pair in buttons)
            {
                var n = WeaponSkillTrees.Find(weapon.MainSkillGrant.Value, pair.Key);
                bool owned = p.nodes.Contains(n.Id), reachable = n.Parent == null || p.nodes.Contains(n.Parent);
                pair.Value.color = n.Id == selected ? new Color(0.65f, 0.52f, 0.24f) : owned ? new Color(0.30f, 0.48f, 0.29f) :
                    reachable ? new Color(0.34f, 0.28f, 0.18f) : new Color(0.14f, 0.14f, 0.16f);
            }
            var node = WeaponSkillTrees.Find(weapon.MainSkillGrant.Value, selected);
            bool can = node != null && p.CanAllocate(weapon.MainSkillGrant.Value, node.Id);
            detail.text = node == null ? "Select a node to inspect its bonuses.\n\nFollow connected paths to their stronger passives. Every node costs one point.\n\nGreen nodes are allocated; gold nodes can be reached." :
                "<b>" + node.Name + "</b>\n" + (node.Keystone ? "Keystone" : node.Notable ? "Notable" : "Passive") + "\n\n" + node.Description;
            allocate.color = can ? UiKit.BorderColor : new Color(0.19f, 0.18f, 0.16f);
            buttonText.text = node == null ? "Select a node" : p.nodes.Contains(node.Id) ? "Allocated" : p.Available <= 0 ? "No points available" :
                !can ? (node.Parent != null && !p.nodes.Contains(node.Parent) ? "Allocate the preceding node" : "Choose one elemental school") : "Allocate · 1 point";
            var id = SkillBook.ForGrant(weapon.MainSkillGrant.Value).Id;
            summary.text = "<b>Current skill</b>\n" + skills.DamageSummary(id, p.level) + "\nCost: " + skills.ManaCost(id).ToString("0.#") + " " + skills.CostResource +
                "\n\n" + p.nodes.Count + "/" + p.level + " points allocated\n\nReset at Mentor Ilyra in Haven.\nCurrent reset price: " + skills.WeaponResetPrice + " gold.";
        }
        private void Allocate()
        {
            if (selected != null && skills.AllocateWeaponNode(weapon, selected)) Refresh();
        }
        private void AddViewButton(string name, string text, float x, float width, System.Action action)
        {
            var image = UiKit.NewImage(name, panel, UiKit.BorderColor);
            UiKit.TopLeft(image.rectTransform, new Vector2(x, -142f), new Vector2(width, 48f));
            var label = UiKit.NewText("Label", image.transform, text, 22, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(label.rectTransform, 0);
            UiKit.OnClick(image, action);
        }

        private void ResetView()
        {
            graph.localScale = Vector3.one;
            graph.anchoredPosition = Vector2.zero;
        }

        private void Pan(Vector2 screenDelta)
        {
            graph.anchoredPosition += screenDelta / Mathf.Max(0.01f, view.lossyScale.x);
            ClampView();
        }

        private void Zoom(float factor, Vector2? screenPoint)
        {
            float before = graph.localScale.x;
            float after = Mathf.Clamp(before * factor, MinZoom, MaxZoom);
            Vector2 pivot = Vector2.zero;
            if (screenPoint.HasValue)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screenPoint.Value, null, out pivot);
                pivot -= view.rect.center;
            }
            Vector2 treePoint = (pivot - graph.anchoredPosition) / before;
            graph.localScale = Vector3.one * after;
            graph.anchoredPosition = pivot - treePoint * after;
            ClampView();
        }

        private void ClampView()
        {
            Vector2 extent = Vector2.zero;
            var nodes = weapon?.MainSkillGrant != null ? WeaponSkillTrees.For(weapon.MainSkillGrant.Value) : null;
            if (nodes != null)
                foreach (var node in nodes)
                {
                    extent.x = Mathf.Max(extent.x, Mathf.Abs(node.Position.x) + 32f);
                    extent.y = Mathf.Max(extent.y, Mathf.Abs(node.Position.y) + 32f);
                }
            Vector2 limits = Vector2.Max(Vector2.zero, extent * graph.localScale.x - view.rect.size * 0.5f + Vector2.one * 80f);
            Vector2 at = graph.anchoredPosition;
            graph.anchoredPosition = new Vector2(Mathf.Clamp(at.x, -limits.x, limits.x), Mathf.Clamp(at.y, -limits.y, limits.y));
        }

        private void UpdatePinch()
        {
            TouchControl first = null, second = null;
            if (Touchscreen.current != null)
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
                if (!RectTransformUtility.RectangleContainsScreenPoint(view, a) ||
                    !RectTransformUtility.RectangleContainsScreenPoint(view, b)) return;
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

        private sealed class ViewHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
        {
            public System.Action<Vector2> Drag;
            public System.Action<float, Vector2> Scroll;
            public void OnBeginDrag(PointerEventData eventData) { eventData.eligibleForClick = false; }
            public void OnDrag(PointerEventData eventData) => Drag?.Invoke(eventData.delta);
            public void OnScroll(PointerEventData eventData) => Scroll?.Invoke(eventData.scrollDelta.y, eventData.position);
        }

        private static Text Label(Transform parent, string text, int size, Vector2 at, Vector2 dimensions)
        {
            var label = UiKit.NewText("Label", parent, text, size, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.TopLeft(label.rectTransform, at, dimensions);
            return label;
        }
    }
}
