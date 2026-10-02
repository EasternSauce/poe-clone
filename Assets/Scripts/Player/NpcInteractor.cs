using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Inventory;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Player
{
    /// <summary>
    /// Talking to townspeople: click (or tap) an NPC. In reach the conversation opens right away,
    /// otherwise the player walks over first; steering away cancels the walk. Walking off mid-
    /// conversation closes it. Clicking an area gate walks the player into it the same way.
    /// Self-added by <see cref="PlayerController"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NpcInteractor : MonoBehaviour
    {
        // A click only reaches an NPC that's already fairly close, so an NPC rendered small in the
        // background (behind a menu, or just far off) can't be walked to by a stray or accidental
        // click; a distant click falls through to movement/attack instead.
        private const float ClickReach = 10f;

        /// <summary>
        /// The NPC a click/tap here would talk to, unless a living enemy is under the point too
        /// (it gets attacked instead), or the NPC is too far from the player to click at all.
        /// </summary>
        public static Npc TalkableAt(Vector2 screenPoint)
        {
            Npc npc = Npc.AtScreen(screenPoint);
            if (npc == null || LootPicker.EnemyUnderPointer(screenPoint))
                return null;
            return activeInstance != null && !activeInstance.InReach(npc, ClickReach) ? null : npc;
        }

        private static NpcInteractor activeInstance;

        private PlayerController controller;
        private PlayerStats stats;
        private Npc target;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            activeInstance = this;
        }

        private void OnDisable()
        {
            if (activeInstance == this)
                activeInstance = null;
            target = null;
        }

        private void Update()
        {
            if (stats != null && stats.IsDead)
            {
                target = null;
                DialogueUI.Close();
                return;
            }

            if (!DialogueUI.IsOpen && !PlayerController.IsUiFocused())
            {
                Vector2? click = TouchMode.Active ? ReadTouch() : ReadMouse();
                if (click.HasValue)
                {
                    Npc npc = TalkableAt(click.Value);
                    AreaGate gate = npc == null && !LootPicker.EnemyUnderPointer(click.Value) ? AreaGate.AtScreen(click.Value) : null;
                    if (npc != null)
                    {
                        PlayerController.ConsumeClick();
                        Request(npc);
                    }
                    else if (gate != null)
                    {
                        PlayerController.ConsumeClick();
                        target = null;
                        controller.WalkTo(gate.WalkPoint, 0.15f);
                    }
                }
            }

            if (DialogueUI.IsOpen && DialogueUI.Speaker != null && !InReach(DialogueUI.Speaker, Npc.TalkReach + 2.5f))
                DialogueUI.Close();

            if (target == null)
                return;

            if (InReach(target, Npc.TalkReach))
            {
                Npc npc = target;
                target = null;
                controller.CancelWalk();
                NpcDialogues.Open(npc);
            }
            else if (!controller.IsWalkingToTarget)
            {
                target = null;
            }
        }

        // Where the click (or tap) on the world landed this frame, if there was one.
        private static Vector2? ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || PlayerController.IsPointerOverUi())
                return null;
            return mouse.position.ReadValue();
        }

        private static Vector2? ReadTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null || !screen.primaryTouch.press.wasPressedThisFrame)
                return null;

            Vector2 position = screen.primaryTouch.position.ReadValue();
            if (TouchMode.IsOverBlocker(position))
                return null;
            return position;
        }

        public void Request(Npc npc)
        {
            if (InReach(npc, Npc.TalkReach))
            {
                target = null;
                controller.CancelWalk();
                NpcDialogues.Open(npc);
                return;
            }

            target = npc;
            controller.WalkTo(npc.transform.position, Npc.TalkReach * 0.7f);
        }

        private bool InReach(Npc npc, float reach)
        {
            Vector3 offset = npc.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= reach * reach;
        }
    }
}
