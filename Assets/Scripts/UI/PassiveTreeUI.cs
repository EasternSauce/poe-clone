using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Player;

namespace PoeClone.UI
{
    /// <summary>
    /// The passive tree panel (P, or the TREE button on touch): every passive as a circle, linked
    /// to its neighbours. Click/tap one that can be taken to take it; select a taken one at the end
    /// of a path to give it back. Hovering (or tapping) shows what a passive gives at the bottom.
    /// Installed by GameSessionController.
    /// </summary>
    public class PassiveTreeUI : MonoBehaviour
    {
        private const float Width = 1000f;
        private const float Height = 780f;
        private const float SpreadX = 400f;
        private const float SpreadY = 300f;
        private static readonly Vector2 TreeCentre = new Vector2(0f, 40f);

        private static PassiveTreeUI instance;

        private class NodeView
        {
            public PassiveNode Node;
            public Image Ring;
            public Image Body;
        }

        private class LinkView
        {
            public string A, B;
            public Image Line;
        }

        private readonly List<NodeView> views = new List<NodeView>();
        private readonly List<LinkView> links = new List<LinkView>();

        private GameObject panelRoot;
        private Text pointsText;
        private Text infoText;
        private Image refundButton;
        private PlayerPassives passives;
        private PassiveNode selected;
        private bool dirty = true;

        public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

        public static void SetOpen(bool open)
        {
            if (instance == null)
                return;
            // Both panels sit in the middle of the screen: one at a time.
            if (open)
                SkillBarUI.SetOpen(false);
            instance.panelRoot.SetActive(open);
            instance.dirty = true;
        }

        private void Awake()
        {
            instance = this;
            Build();
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (passives != null)
                passives.Changed -= MarkDirty;
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

            if (passives == null || !passives.enabled || spectator)
            {
                panelRoot.SetActive(false);
                return;
            }

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

        // ------------------------------------------------------------------ state

        private static readonly Color Locked = new Color(0.16f, 0.15f, 0.14f, 1f);

        private static Color BranchColor(PassiveBranch branch)
        {
            switch (branch)
            {
                case PassiveBranch.Might: return new Color(0.85f, 0.30f, 0.25f);
                case PassiveBranch.Grace: return new Color(0.35f, 0.80f, 0.40f);
                case PassiveBranch.Wisdom: return new Color(0.40f, 0.55f, 1.00f);
                default: return UiKit.Gold;
            }
        }

        private void Refresh()
        {
            PassiveAllocation allocation = passives.Allocation;
            int unspent = passives.Unspent;
            pointsText.text = unspent > 0
                ? "<color=#FFD040>" + unspent + " passive point" + (unspent > 1 ? "s" : "") + " to spend</color>"
                : "<color=#" + UiKit.Hex(UiKit.DimText) + ">No points to spend - one more each level</color>";

            foreach (NodeView v in views)
            {
                bool taken = allocation.Has(v.Node.Id);
                bool available = allocation.CanTake(v.Node.Id, passives.Level);
                Color c = BranchColor(v.Node.Branch);
                v.Body.color = taken ? c : available ? Color.Lerp(Locked, c, 0.35f) : Locked;
                v.Ring.color = taken ? UiKit.Gold : available ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(0.35f, 0.32f, 0.28f, 1f);
            }

            foreach (LinkView l in links)
            {
                bool both = allocation.Has(l.A) && allocation.Has(l.B);
                l.Line.color = both ? UiKit.Gold : new Color(0.30f, 0.27f, 0.23f, 1f);
            }

            ShowInfo(selected);
        }

        private void ShowInfo(PassiveNode node)
        {
            if (node == null)
            {
                infoText.text = "<color=#" + UiKit.Hex(UiKit.DimText) + ">Point at a passive to see what it gives. " +
                                (TouchMode.Active ? "Tap one next to a taken passive to take it." : "Click one next to a taken passive to take it; right-click a taken one at the end of a path to give it back.") +
                                "</color>";
                refundButton.gameObject.SetActive(false);
                return;
            }

            var sb = new StringBuilder();
            sb.Append("<b><color=#").Append(UiKit.Hex(BranchColor(node.Branch))).Append(">").Append(node.Name)
                .Append(node.Notable ? "  (notable)" : "").Append("</color></b>   ");
            if (node.Mods.Length == 0)
                sb.Append("Where every path starts.");
            for (int k = 0; k < node.Mods.Length; k++)
            {
                if (k > 0)
                    sb.Append(",  ");
                sb.Append(StatFormatter.ItemLine(node.Mods[k]));
            }

            PassiveAllocation allocation = passives.Allocation;
            bool taken = allocation.Has(node.Id);
            sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">");
            if (node.Id == PassiveTree.OriginId)
                sb.Append("Always yours.");
            else if (taken)
                sb.Append(allocation.CanRefund(node.Id) ? "Taken." : "Taken. Give back the passives after it first.");
            else if (allocation.CanTake(node.Id, passives.Level))
                sb.Append(TouchMode.Active ? "Tap again to take it." : "Click to take it.");
            else
                sb.Append(passives.Unspent <= 0 ? "No points left." : "Take a passive next to it first.");
            sb.Append("</color>");

            infoText.text = sb.ToString();
            refundButton.gameObject.SetActive(taken && allocation.CanRefund(node.Id));
        }

        private void OnHover(PassiveNode node)
        {
            if (!TouchMode.Active)
                ShowInfo(node);
        }

        private void OnHoverEnd()
        {
            if (!TouchMode.Active)
                ShowInfo(selected);
        }

        private void OnClick(PassiveNode node, PointerEventData.InputButton button)
        {
            if (passives == null)
                return;

            if (button == PointerEventData.InputButton.Right)
            {
                passives.Refund(node.Id);
                selected = node;
                dirty = true;
                return;
            }

            // Touch: the first tap only selects (shows what it gives); a second tap takes it.
            bool take = !TouchMode.Active || selected == node;
            selected = node;
            if (take && passives.Take(node.Id))
            {
                if (Audio.AudioManager.Instance != null)
                    Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.uiItemPlace, 0.4f);
            }
            dirty = true;
        }

        private void RefundSelected()
        {
            if (selected != null && passives != null)
                passives.Refund(selected.Id);
            dirty = true;
        }

        // ------------------------------------------------------------------ building

        private Vector2 NodePosition(PassiveNode node)
        {
            return TreeCentre + new Vector2(node.X * SpreadX, node.Y * SpreadY);
        }

        private void Build()
        {
            Canvas canvas = UiKit.NewCanvas("PassiveCanvas", transform, 65, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            Image panel = UiKit.NewImage("PassivePanel", canvas.transform, UiKit.PanelColor);
            panel.raycastTarget = true;
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(Width, Height);
            UiKit.AddOutline(panel, UiKit.BorderColor, 3f);
            panelRoot = panel.gameObject;
            TouchMode.AddBlocker(pr);

            Text title = UiKit.NewText("Title", pr, "PASSIVES", 26, UiKit.Gold, TextAnchor.UpperCenter);
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, -14f), new Vector2(Width, 34f));

            pointsText = UiKit.NewText("Points", pr, "", 18, UiKit.TextColor, TextAnchor.UpperCenter);
            UiKit.TopLeft(pointsText.rectTransform, new Vector2(0f, -46f), new Vector2(Width, 24f));

            Image close = UiKit.NewImage("Close", pr, new Color(0.25f, 0.1f, 0.08f, 1f));
            close.raycastTarget = true;
            UiKit.TopLeft(close.rectTransform, new Vector2(Width - 50f, -12f), new Vector2(38f, 38f));
            Text x = UiKit.NewText("X", close.rectTransform, "X", 20, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(x.rectTransform, 0f);
            close.gameObject.AddComponent<TouchPointerRelay>().Up += _ => SetOpen(false);

            Image reset = NewButton(pr, "Reset", "Reset all", new Vector2(16f, -12f), new Vector2(120f, 38f));
            reset.gameObject.AddComponent<TouchPointerRelay>().Up += _ =>
            {
                if (passives != null)
                    passives.ResetAll();
                selected = null;
                dirty = true;
            };

            // Links first, so the circles draw over them.
            var linked = new HashSet<string>();
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                foreach (string other in node.Links)
                {
                    string key = string.CompareOrdinal(node.Id, other) < 0 ? node.Id + "|" + other : other + "|" + node.Id;
                    if (!linked.Add(key))
                        continue;

                    Vector2 a = NodePosition(node);
                    Vector2 b = NodePosition(PassiveTree.Get(other));
                    Image line = UiKit.NewImage("Link", pr, Color.gray);
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
                float size = node.Notable ? 62f : node.Id == PassiveTree.OriginId ? 56f : 44f;
                Image ring = UiKit.NewImage("Node_" + node.Id, pr, Color.white);
                ring.sprite = UiKit.Disc;
                ring.raycastTarget = true;
                RectTransform rr = ring.rectTransform;
                rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
                rr.anchoredPosition = NodePosition(node);
                rr.sizeDelta = new Vector2(size, size);

                Image body = UiKit.NewImage("Body", rr, Color.white);
                body.sprite = UiKit.Disc;
                UiKit.Stretch(body.rectTransform, node.Notable ? 6f : 4f);

                var handler = ring.gameObject.AddComponent<NodeHandler>();
                PassiveNode captured = node;
                handler.Enter = () => OnHover(captured);
                handler.Exit = OnHoverEnd;
                handler.Click = button => OnClick(captured, button);

                views.Add(new NodeView { Node = node, Ring = ring, Body = body });
            }

            // What the pointed-at passive gives, along the bottom.
            Image info = UiKit.NewImage("Info", pr, new Color(0f, 0f, 0f, 0.35f));
            UiKit.TopLeft(info.rectTransform, new Vector2(16f, -(Height - 96f)), new Vector2(Width - 32f, 80f));
            infoText = UiKit.NewText("Text", info.rectTransform, "", 18, UiKit.TextColor, TextAnchor.MiddleLeft);
            infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.TopLeft(infoText.rectTransform, new Vector2(12f, 0f), new Vector2(Width - 32f - 180f, 80f));

            refundButton = NewButton(info.rectTransform, "Refund", "Give back", new Vector2(Width - 32f - 150f, -20f), new Vector2(140f, 40f));
            refundButton.gameObject.AddComponent<TouchPointerRelay>().Up += _ => RefundSelected();
        }

        private static Image NewButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            Image button = UiKit.NewImage(name, parent, new Color(0.18f, 0.14f, 0.10f, 1f));
            button.raycastTarget = true;
            UiKit.TopLeft(button.rectTransform, pos, size);
            UiKit.AddOutline(button, UiKit.BorderColor, 1.5f);
            Text text = UiKit.NewText("Label", button.rectTransform, label, 17, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, 0f);
            return button;
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
