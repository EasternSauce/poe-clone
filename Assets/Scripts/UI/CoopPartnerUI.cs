using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Network.Replication;
using PoeClone.Quests;

namespace PoeClone.UI
{
    /// <summary>Read-only partner details; never applies remote gear or passives to the local player.</summary>
    public class CoopPartnerUI : MonoBehaviour
    {
        private static CoopPartnerUI instance;
        private GameObject root;
        private Canvas canvas;
        private RectTransform panel, viewport, content, tooltip, tooltipContent;
        private Text summary, objectives, tooltipText;
        private Image xpFill;
        private ScrollRect scroll, tooltipScroll;
        private RectTransform items;
        private string inventoryKey;
        private Vector2 layoutSize;
        private GearState displayed;
        private float width;
        private bool wasTouch;
        private bool wasLinked;
        private float hideTooltipAt;

        public static bool IsOpen => instance != null && instance.root.activeSelf;

        public static void Open()
        {
            if (instance == null || GameSessionController.Instance?.Coop == null) return;
            instance.root.SetActive(true);
            instance.scroll.verticalNormalizedPosition = 1f;
            instance.Layout();
            instance.Render();
            PlayerHUD.SetHiddenBy(instance, true);
        }

        private void Close()
        {
            root.SetActive(false);
            tooltip.gameObject.SetActive(false);
            PlayerHUD.SetHiddenBy(this, false);
        }

        private void Awake()
        {
            instance = this;
            canvas = UiKit.NewCanvas("CoopPartnerCanvas", transform, 970, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = group.blocksRaycasts = true;
            root = canvas.gameObject;
            Image shade = UiKit.NewImage("Shade", canvas.transform, new Color(0f, 0f, 0f, 0.75f));
            UiKit.Stretch(shade.rectTransform, 0f);
            shade.raycastTarget = true;
            panel = UiKit.NewImage("Panel", canvas.transform, UiKit.PanelColor).rectTransform;
            UiKit.Grain(panel.GetComponent<Image>());
            UiKit.Frame(panel);
            TouchMode.AddBlocker(panel);
            UiKit.CloseButton(panel, Close);
            summary = UiKit.NewText("Partner", panel, "Co-op partner", 26, UiKit.Gold, TextAnchor.UpperLeft);
            summary.supportRichText = false;
            Image bar = UiKit.NewImage("XP", panel, new Color(.08f, .07f, .05f));
            UiKit.TopLeft(bar.rectTransform, new Vector2(24f, -122f), new Vector2(520f, 10f));
            xpFill = UiKit.NewImage("Progress", bar.transform, UiKit.Gold);
            xpFill.rectTransform.anchorMin = Vector2.zero;
            xpFill.rectTransform.anchorMax = Vector2.one;
            xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;
            Button("PassiveTree", panel, "Passive tree", new Vector2(24f, -146f), new Vector2(220f, 48f), OpenTree);
            viewport = UiKit.NewRect("Viewport", panel);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            content = UiKit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            items = UiKit.NewRect("Inventory", content);
            objectives = UiKit.NewText("Quests", content, "", 22, UiKit.TextColor, TextAnchor.UpperLeft);
            objectives.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectives.verticalOverflow = VerticalWrapMode.Overflow;
            tooltip = UiKit.NewImage("ItemDetails", panel, new Color(.06f, .05f, .04f, 1f)).rectTransform;
            var tipEvents = tooltip.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => hideTooltipAt = 0f);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (!TouchMode.Active) hideTooltipAt = Time.unscaledTime + .15f; });
            tipEvents.triggers.Add(enter); tipEvents.triggers.Add(exit);
            UiKit.Frame(tooltip);
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(.5f, 0f); tooltip.pivot = new Vector2(.5f, 0f);
            tooltip.anchoredPosition = new Vector2(0f, 20f);
            RectTransform tipViewport = UiKit.NewRect("Viewport", tooltip);
            UiKit.Stretch(tipViewport, 16f);
            tipViewport.gameObject.AddComponent<RectMask2D>();
            tipViewport.gameObject.AddComponent<Image>().color = Color.clear;
            tooltipContent = UiKit.NewRect("Content", tipViewport);
            tooltipContent.anchorMin = new Vector2(0, 1); tooltipContent.anchorMax = Vector2.one;
            tooltipContent.pivot = new Vector2(.5f, 1);
            tooltipText = UiKit.NewText("Text", tooltipContent, "", 20, UiKit.TextColor, TextAnchor.UpperLeft);
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap; tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            tooltipScroll = tipViewport.gameObject.AddComponent<ScrollRect>();
            tooltipScroll.content = tooltipContent; tooltipScroll.viewport = tipViewport; tooltipScroll.horizontal = false;
            tooltipScroll.movementType = ScrollRect.MovementType.Clamped;
            tooltipScroll.scrollSensitivity = 30f;
            tooltip.gameObject.SetActive(false);
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            PlayerHUD.SetHiddenBy(this, false);
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!root.activeSelf) return;
            if (hideTooltipAt > 0f && Time.unscaledTime >= hideTooltipAt)
            {
                hideTooltipAt = 0f;
                tooltip.gameObject.SetActive(false);
            }
            if (GameSessionController.Instance?.Coop == null) { Close(); return; }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && !UiKit.IsTypingInTextField()) { Close(); return; }
            Vector2 size = new Vector2(Screen.width, Screen.height);
            if (layoutSize != size || wasTouch != TouchMode.Active) Layout();
            if (displayed != GameSessionController.Instance.Coop.PartnerGear || wasLinked != GameSessionController.Instance.Coop.Linked) Render();
        }

        private void Layout()
        {
            layoutSize = new Vector2(Screen.width, Screen.height); wasTouch = TouchMode.Active;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = TouchMode.Active ? new Vector2(960, 960) : new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Canvas.ForceUpdateCanvases();
            width = Mathf.Min(1000f, ((RectTransform)canvas.transform).rect.width - 32f);
            panel.anchorMin = new Vector2(.5f, 0f); panel.anchorMax = new Vector2(.5f, 1f);
            panel.sizeDelta = new Vector2(width, -40f); panel.anchoredPosition = Vector2.zero;
            UiKit.TopLeft(summary.rectTransform, new Vector2(24f, -24f), new Vector2(width-104f, 90f));
            summary.fontSize = TouchMode.Active ? 32 : 26;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24f, 20f); viewport.offsetMax = new Vector2(-24f, -212f);
            inventoryKey = null; displayed = null;
            tooltip.gameObject.SetActive(false);
        }

        private void Render()
        {
            var coop = GameSessionController.Instance.Coop;
            wasLinked = coop.Linked;
            displayed = coop.PartnerGear;
            string connection = coop.Linked ? "" : " | Reconnecting (up to 10 minutes)";
            if (displayed == null)
            {
                items.gameObject.SetActive(false);
                objectives.text = "";
                xpFill.rectTransform.anchorMax = new Vector2(0f, 1f);
                summary.text = coop.PartnerName + connection + "\nWaiting for partner details...";
                return;
            }
            items.gameObject.SetActive(true);
            float progress = displayed.xpr > 0 ? Mathf.Clamp01((float)displayed.xp / displayed.xpr) : 1f;
            summary.text = coop.PartnerName + " | Level " + displayed.lv + connection +
                "\nXP " + displayed.xp + " / " + displayed.xpr + " (" + Mathf.RoundToInt(progress*100f) + "%)";
            xpFill.rectTransform.anchorMax = new Vector2(progress, 1f);
            string key = JsonUtility.ToJson(new ItemList { eq = displayed.eq, bag = displayed.bag });
            if (key != inventoryKey)
            {
                inventoryKey = key;
                tooltip.gameObject.SetActive(false);
                foreach (Transform child in items) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                DrawItems();
            }
            var text = new StringBuilder("<color=#DDBB77>QUEST OBJECTIVES</color>\n");
            foreach (var active in displayed.qactive ?? Array.Empty<QuestProgressState>())
            {
                QuestDefinition q = QuestBook.Get(active.id);
                if (q == null) continue;
                text.Append('\n').Append(q.Title).Append('\n');
                if (!string.IsNullOrEmpty(active.objective))
                    text.Append(active.objective);
                else if (active.progress >= q.Count)
                    text.Append("Ready to turn in: ").Append(QuestBook.Whereabouts(q.ReturnTo));
                else
                    text.Append(q.Objective).Append(" (").Append(active.progress).Append('/').Append(q.Count).Append(')');
                text.Append('\n');
            }
            if (displayed.qactive == null || displayed.qactive.Length == 0) text.Append("No active objectives.\n");
            text.Append("\nCompleted quests: ").Append(displayed.qdone?.Length ?? 0);
            text.Append("\n\nGold: ").Append(displayed.gold).Append(" | Health potions: ").Append(displayed.hpot)
                .Append(" | Mana potions: ").Append(displayed.mpot);
            objectives.text = text.ToString();
            objectives.fontSize = TouchMode.Active ? 28 : 22;
            float top = items.sizeDelta.y + 24f;
            UiKit.TopLeft(objectives.rectTransform, new Vector2(0, -top), new Vector2(width-48f, 0));
            float height = objectives.preferredHeight;
            objectives.rectTransform.sizeDelta = new Vector2(width-48f, height);
            content.sizeDelta = new Vector2(0f, top + height + 24f);
        }

        [Serializable] private class ItemList { public GearItem[] eq; public GearItem[] bag; }

        private void DrawItems()
        {
            float available = width-48f;
            Label("EQUIPMENT — hover or hold for details", 0f, available);
            float cardWidth = (available-12f)/2f;
            var slots = SlotRules.AllSlots;
            for (int k = 0; k < slots.Length; k++)
            {
                GearItem wire = Array.Find(displayed.eq ?? Array.Empty<GearItem>(), g => g.x == (int)slots[k]);
                ItemData item = GearCodec.ToItem(wire);
                Image card = UiKit.NewImage(slots[k].ToString(), items, new Color(.18f, .15f, .12f));
                UiKit.Inset(card);
                UiKit.TopLeft(card.rectTransform, new Vector2((k%2)*(cardWidth+12f), -48f-(k/2)*86f), new Vector2(cardWidth, 78f));
                if (item != null) AddItem(card.rectTransform, item, false);
                Text label = UiKit.NewText("Label", card.transform, slots[k] + "\n" + (item?.Name ?? "Empty"),
                    TouchMode.Active ? 24 : 19, item != null ? UiKit.RarityColor(item.Rarity) : UiKit.DimText, TextAnchor.MiddleLeft);
                label.supportRichText = false;
                UiKit.TopLeft(label.rectTransform, new Vector2(82f, -4f), new Vector2(cardWidth-88f, 70f));
                label.raycastTarget = false;
            }
            float bagTop = 48f + Mathf.Ceil(slots.Length/2f)*86f + 18f;
            Label("BACKPACK", bagTop, available);
            float cell = available/12f;
            for (int y = 0; y < 5; y++) for (int x = 0; x < 12; x++)
            {
                Image cellImage = UiKit.NewImage("Cell", items, new Color(.1f, .09f, .07f));
                UiKit.Inset(cellImage);
                UiKit.TopLeft(cellImage.rectTransform, new Vector2(x*cell, -bagTop-48f-y*cell), new Vector2(cell-2f, cell-2f));
            }
            foreach (GearItem wire in displayed.bag ?? Array.Empty<GearItem>())
            {
                ItemData item = GearCodec.ToItem(wire);
                if (item == null) continue;
                Image card = UiKit.NewImage("BagItem", items, new Color(.23f, .19f, .13f));
                UiKit.Inset(card);
                UiKit.TopLeft(card.rectTransform, new Vector2(wire.x*cell, -bagTop-48f-wire.y*cell), new Vector2(wire.w*cell-2f, wire.h*cell-2f));
                AddItem(card.rectTransform, item, true);
            }
            UiKit.TopLeft(items, Vector2.zero, new Vector2(available, bagTop+48f+5f*cell));
        }

        private void Label(string text, float y, float available)
        {
            Text label = UiKit.NewText("Heading", items, text, TouchMode.Active ? 28 : 22, UiKit.Gold, TextAnchor.MiddleLeft);
            UiKit.TopLeft(label.rectTransform, new Vector2(0, -y), new Vector2(available, 42f));
        }

        private void AddItem(RectTransform card, ItemData item, bool stretch)
        {
            Image icon = UiKit.NewImage("Icon", card, Color.white);
            icon.sprite = ItemArt.PaintedIcon(item) ?? IconFactory.Get(item);
            icon.preserveAspect = true; icon.raycastTarget = false;
            if (stretch) UiKit.Stretch(icon.rectTransform, 5f);
            else UiKit.TopLeft(icon.rectTransform, new Vector2(6f, -6f), new Vector2(66f, 66f));
            var target = card.gameObject.AddComponent<PartnerItemTooltip>();
            target.Show = () => ShowTooltip(item);
            target.Hide = () =>
            {
                if (TouchMode.Active) tooltip.gameObject.SetActive(false);
                else hideTooltipAt = Time.unscaledTime + .15f;
            };
        }

        private void ShowTooltip(ItemData item)
        {
            hideTooltipAt = 0f;
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            float tipWidth = Mathf.Min(width-32f, TouchMode.Active ? 800f : 620f);
            tooltipText.fontSize = TouchMode.Active ? 28 : 20;
            tooltipText.text = InventoryUI.BuildTooltipText(item, out _);
            UiKit.TopLeft(tooltipText.rectTransform, Vector2.zero, new Vector2(tipWidth-32f, 0));
            float height = tooltipText.preferredHeight;
            tooltipText.rectTransform.sizeDelta = new Vector2(tipWidth-32f, height);
            tooltipContent.sizeDelta = new Vector2(0, height);
            tooltip.sizeDelta = new Vector2(tipWidth, Mathf.Min(height+32f, panel.rect.height*.65f));
            tooltipScroll.verticalNormalizedPosition = 1f;
        }

        private void OpenTree()
        {
            var coop = GameSessionController.Instance?.Coop;
            if (coop == null) return;
            if (PassiveTreeUI.InspectPartner(coop.PartnerName, coop.PartnerGear)) Close();
            else summary.text = "Finish or cancel your pending passive changes before inspecting your partner's tree.";
        }

        private static void Button(string name, Transform parent, string text, Vector2 pos, Vector2 size, Action click)
        {
            Image image = UiKit.NewImage(name, parent, new Color(.24f, .19f, .13f));
            UiKit.TopLeft(image.rectTransform, pos, size);
            image.gameObject.AddComponent<Button>().onClick.AddListener(() => click());
            Text label = UiKit.NewText("Label", image.transform, text, 26, UiKit.Gold, TextAnchor.MiddleCenter);
            UiKit.Stretch(label.rectTransform, 2f); label.raycastTarget = false;
        }
    }

    public class PartnerItemTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Action Show, Hide;
        private bool pressing;
        private float started;
        private Vector2 origin;
        private PointerEventData pointer;
        public void OnPointerEnter(PointerEventData e) { if (!TouchMode.Active) Show?.Invoke(); }
        public void OnPointerExit(PointerEventData e) { pressing = false; if (!TouchMode.Active) Hide?.Invoke(); }
        public void OnPointerDown(PointerEventData e) { pressing = true; started = Time.unscaledTime; origin = e.position; pointer = e; Hide?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { pressing = false; }
        private void Update()
        {
            if (!pressing) return;
            if (pointer.dragging || Vector2.Distance(pointer.position, origin) > 16f) { pressing = false; return; }
            if (Time.unscaledTime-started >= .45f) { pressing = false; Show?.Invoke(); }
        }
    }
}
