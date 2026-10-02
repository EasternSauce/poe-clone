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
        private Text text;
        private Image back;
        private QuestLog log;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;
        private bool dirty = true;
        private int npcCount = -1;

        private void Awake()
        {
            Canvas canvas = UiKit.NewCanvas("QuestTrackerCanvas", transform, 40, out _);

            back = UiKit.NewImage("Tracker", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            RectTransform rt = back.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);

            text = UiKit.NewText("Text", rt, "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Stretch(text.rectTransform, 10f);
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
            if (log == null && QuestLog.Instance != null && QuestLog.Instance.enabled)
            {
                log = QuestLog.Instance;
                log.Changed += MarkDirty;
                dirty = true;
            }

            var session = GameSessionController.Instance;
            bool spectator = session != null && session.Role == SessionRole.Spectator;
            if (log == null || spectator)
            {
                back.gameObject.SetActive(false);
                return;
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
            back.gameObject.SetActive(text.text.Length > 0 && !covered);

            // Clear of the touch menu buttons down the right edge.
            back.rectTransform.anchoredPosition = new Vector2(TouchMode.Active ? -140f : -16f, -16f);
        }

        private void Rebuild()
        {
            var sb = new StringBuilder();
            foreach (QuestDefinition q in log.Taken())
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("<b>").Append(q.Title).Append("</b>\n");
                if (log.State(q) == QuestState.Complete)
                {
                    Npc giver = Npc.Find(q.Giver);
                    sb.Append("<color=#FFD040>  Return to ").Append(giver != null ? giver.DisplayName : q.Giver.ToString())
                        .Append(" in Haven</color>");
                }
                else
                {
                    sb.Append("<color=#").Append(UiKit.Hex(UiKit.DimText)).Append(">  ").Append(q.Objective)
                        .Append(NpcDialogues.ProgressText(log, q)).Append("</color>");
                }
            }

            text.text = sb.ToString();
            const float width = 440f;
            text.rectTransform.sizeDelta = Vector2.zero;
            back.rectTransform.sizeDelta = new Vector2(width, 0f);
            float height = text.preferredHeight;
            back.rectTransform.sizeDelta = new Vector2(width, height + 20f);
        }
    }
}
