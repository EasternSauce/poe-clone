using System.Text;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.Quests;
using PoeClone.World;

namespace PoeClone.UI
{
    /// <summary>
    /// The quests under way, top right: each one's objective and progress, or who to go back to
    /// once it's done. Also keeps the "!" / "?" over the townspeople in step with the quest log.
    /// Installed by GameSessionController.
    /// </summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        private Text text, heading, detailsLabel;
        private RectTransform detailsButton;
        private bool showDetails;
        private const string DetailsPreference = "QuestTracker.ShowDetails";
        private Image back;
        private QuestLog log;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;
        private bool dirty = true;
        private static bool mobileOpen;
        public static void ToggleMobile() => mobileOpen = !mobileOpen;
        public static bool MobileOpen => mobileOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMobile() => mobileOpen = false;
        private int npcCount = -1;
        private bool laidOutForTouch, laidOutForPortrait;
        private const float HeadingHeight = 30f;

        private void Awake()
        {
            showDetails = PlayerPrefs.GetInt(DetailsPreference, 0) != 0;
            Canvas canvas = UiKit.NewCanvas("QuestTrackerCanvas", transform, 40, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group.interactable = true;
            group.blocksRaycasts = true;

            back = UiKit.NewImage("Tracker", canvas.transform, new Color(0.08f, 0.07f, 0.06f, 0.72f));
            UiKit.Grain(back);
            RectTransform rt = back.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            UiKit.ThinFrame(rt);

            heading = UiKit.Heading(UiKit.NewText("Heading", rt, "QUESTS", 15, UiKit.Gold, TextAnchor.MiddleLeft));
            UiKit.TopLeft(heading.rectTransform, new Vector2(12f, -6f), new Vector2(200f, 20f));

            Image toggle = UiKit.NewImage("DetailsToggle", rt, UiKit.PanelColor);
            toggle.raycastTarget = true;
            detailsButton = toggle.rectTransform;
            detailsButton.anchorMin = detailsButton.anchorMax = detailsButton.pivot = Vector2.one;
            detailsButton.anchoredPosition = new Vector2(-12f, -6f);
            detailsLabel = UiKit.NewText("Label", detailsButton, "", 14, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(detailsLabel.rectTransform, 0f);
            UiKit.StyleButton(toggle, detailsLabel, UiKit.Gold);
            UiKit.OnClick(toggle, () =>
            {
                showDetails = !showDetails;
                PlayerPrefs.SetInt(DetailsPreference, showDetails ? 1 : 0);
                PlayerPrefs.Save();
                MarkDirty();
            });
            TouchMode.AddBlocker(detailsButton);

            text = UiKit.NewText("Text", rt, "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Stretch(text.rectTransform, 12f);
            text.rectTransform.offsetMax = new Vector2(-12f, -HeadingHeight);
            back.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (log != null)
                log.Changed -= MarkDirty;
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void Update()
        {
            if (log == null && QuestLog.Instance != null)
            {
                log = QuestLog.Instance;
                log.Changed += MarkDirty;
                dirty = true;
            }

            var session = GameSessionController.Instance;
            bool spectator = session != null && session.Role == SessionRole.Spectator;
            if (log == null || (spectator && !log.HasReplicaState))
            {
                back.gameObject.SetActive(false);
                return;
            }

            // Phone and desktop lay the text out differently.
            if (TouchMode.Active != laidOutForTouch || TouchMode.Portrait != laidOutForPortrait)
            {
                laidOutForTouch = TouchMode.Active;
                laidOutForPortrait = TouchMode.Portrait;
                dirty = true;
            }

            // NPCs appear after the world is built; give them their markers once they're there.
            if (Npc.All.Count != npcCount)
            {
                npcCount = Npc.All.Count;
                dirty = true;
            }

            if (dirty)
            {
                dirty = false;
                Rebuild();
                NpcDialogues.RefreshMarkers();
            }

            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if (characterUI == null)
                characterUI = FindAnyObjectByType<CharacterPageUI>();
            bool covered = (inventoryUI != null && inventoryUI.IsOpen) || (characterUI != null && characterUI.IsOpen) ||
                           SkillBarUI.IsOpen || DialogueUI.IsOpen || PassiveTreeUI.IsOpen;
            back.gameObject.SetActive(text.text.Length > 0 && !covered && (!TouchMode.Active || mobileOpen));

            bool portrait = TouchMode.Portrait;
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = portrait ? new Vector2(0f, 1f) : Vector2.one;
            back.rectTransform.anchoredPosition = portrait ? new Vector2(24f, -300f) :
                TouchMode.Active ? new Vector2(-16f - MinimapUI.OccupiedWidth, -16f) :
                new Vector2(-16f, -16f - MinimapUI.Bottom);
        }

        private static readonly Color QuestTitle = new Color(0.96f, 0.88f, 0.66f, 1f);

        private void Rebuild()
        {
            var sb = new StringBuilder();

            // Nothing taken yet and the Elder has work: point the way.
            QuestDefinition first = log.Taken().Count == 0 ? log.CurrentFrom(NpcRole.Elder) : null;
            if (first != null && log.State(first) == QuestState.Available)
            {
                Npc elder = Npc.Find(NpcRole.Elder);
                sb.Append("<color=#FFD040>Talk to ").Append(elder != null ? elder.DisplayName : "the Elder")
                    .Append(" in Haven</color>");
                if (showDetails)
                    sb.Append("\n<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">  look for the ! over her head</color>");
            }

            foreach (QuestDefinition q in log.Taken())
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("<b><color=#").Append(UiKit.Hex(QuestTitle)).Append('>').Append(q.Title).Append("</color></b>\n");
                if (log.State(q) == QuestState.Complete)
                {
                    string line = showDetails ? NpcDialogues.ReturnLine(q) : QuestBook.MinimalReturnLine(q);
                    sb.Append("<color=#FFD040>  ").Append(char.ToUpperInvariant(line[0])).Append(line.Substring(1)).Append("</color>");
                }
                else
                {
                    sb.Append("<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">  ")
                        .Append(QuestBook.ObjectiveLine(log, q, showDetails)).Append("</color>");
                }
            }

            text.text = sb.ToString();
            float width = TouchMode.Active ? (TouchMode.Portrait ? 560f : 460f) : 440f;
            text.fontSize = TouchMode.Active ? TouchMode.ReadingFontSize : 18;
            heading.fontSize = TouchMode.Active ? (TouchMode.Portrait ? 32 : 24) : 15;
            float headingHeight = TouchMode.Active ? heading.fontSize + 26f : 38f;
            float buttonWidth = TouchMode.Active ? 220f : 124f;
            detailsLabel.text = showDetails ? "Hide details" : "Show details";
            detailsLabel.fontSize = TouchMode.Active ? 24 : 14;
            detailsButton.sizeDelta = new Vector2(buttonWidth, headingHeight - 10f);
            heading.rectTransform.sizeDelta = new Vector2(width - buttonWidth - 40f, headingHeight - 6f);
            text.rectTransform.offsetMax = new Vector2(-12f, -headingHeight);
            back.rectTransform.sizeDelta = new Vector2(width, 0f);
            float height = text.preferredHeight;
            back.rectTransform.sizeDelta = new Vector2(width, height + headingHeight + 12f);
        }
    }
}
