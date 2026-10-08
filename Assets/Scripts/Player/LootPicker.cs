using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.World;

namespace PoeClone.Player
{
    /// <summary>
    /// Picking items up off the ground, PoE style: nothing is collected by walking over it. Click
    /// (or tap) an item; in reach it goes straight into the bag, otherwise the player walks over and
    /// picks it up on arrival. Steering or attacking cancels the walk. Whatever item the pointer is on
    /// (mouse hover, or the finger while it's down) and whatever item is being walked to is outlined.
    /// A living enemy under the pointer wins over an item there (the click attacks it instead).
    /// Self-added by <see cref="PlayerController"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class LootPicker : MonoBehaviour
    {
        private const float PickupReach = 1.6f;
        // Gold is collected just by walking over it.
        private const float GoldWalkReach = 2.2f;

        private PlayerController controller;
        private PlayerStats stats;
        private PlayerInventory inventory;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;

        private LootDrop hovered;
        private LootDrop target;

        /// <summary>
        /// The item a click/tap here would pick up: one whose name or icon is under the point,
        /// unless a living enemy is under it too, which takes the click instead.
        /// </summary>
        public static LootDrop PickableAt(Vector2 screenPoint)
        {
            LootDrop drop = LootDrop.FindAtScreen(screenPoint);
            if (drop == null)
                return null;
            return EnemyUnderPointer(screenPoint) ? null : drop;
        }

        public static bool EnemyUnderPointer(Vector2 screenPoint)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return false;

            // Enemies' own capsule colliders; corpses have theirs switched off, so they don't count.
            if (!Physics.Raycast(cam.ScreenPointToRay(screenPoint), out RaycastHit hit, 300f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            return enemy != null && !enemy.IsDead;
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
        }

        private void Start()
        {
            if (inventory != null)
                inventory.ItemThrown += OnItemThrown;
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.ItemThrown -= OnItemThrown;
        }

        // An item thrown out of the bag lands on the ground just in front of the character.
        private void OnItemThrown(ItemData item)
        {
            Vector3 at = LootDrop.FreeSpotNear(transform.position + transform.forward * 1.3f, 0f);
            LootDrop.Spawn(item, LootDrop.GroundBelow(at), interactive: true, id: 0);
            ItemSounds.PlayDrop(item, at);
        }

        private void OnDisable()
        {
            SetHovered(null);
            SetTarget(null);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (stats != null && stats.IsDead)
            {
                SetHovered(null);
                SetTarget(null);
                return;
            }

            CollectPickupsUnderfoot();

            LootDrop pointed = null;
            if (!PanelOpen() && !PlayerController.IsUiFocused())
            {
                pointed = TouchMode.Active ? ReadTouch() : ReadMouse();
                // No mouse to hover with on a phone: the item nearest the player shows its tooltip.
                if (pointed == null && TouchMode.Active)
                    pointed = NearestItem();
            }
            SetHovered(pointed);

            if (target == null)
                return;

            if (InReach(target))
            {
                LootDrop drop = target;
                SetTarget(null);
                controller.CancelWalk();
                drop.TryPickUp(inventory, NoticePoint());
            }
            else if (!controller.IsWalkingToTarget)
            {
                // The walk was cancelled (steered away, attacked) or gave up.
                SetTarget(null);
            }
        }

        private void CollectPickupsUnderfoot()
        {
            for (int k = LootDrop.All.Count - 1; k >= 0; k--)
            {
                LootDrop drop = LootDrop.All[k];
                if (drop == null || !drop.IsPickup || !drop.IsInteractive)
                    continue;
                if (!drop.IsGold && (inventory == null || inventory.Potions(drop.Item.Id == ItemGenerator.HealthPotionId) >= PlayerInventory.MaxPotions))
                    continue;
                Vector3 offset = drop.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= GoldWalkReach * GoldWalkReach && drop.IsLanded)
                    drop.TryPickUp(inventory, NoticePoint());
            }
        }

        private const float NearbyTooltipRange = 2.5f;

        private LootDrop NearestItem()
        {
            LootDrop best = null;
            float bestSq = NearbyTooltipRange * NearbyTooltipRange;
            foreach (LootDrop drop in LootDrop.All)
            {
                if (drop == null || drop.IsGold || !drop.IsInteractive)
                    continue;
                Vector3 offset = drop.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < bestSq)
                {
                    bestSq = offset.sqrMagnitude;
                    best = drop;
                }
            }
            return best;
        }

        private LootDrop ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || PlayerController.IsPointerOverUi())
                return null;

            LootDrop under = PickableAt(mouse.position.ReadValue());
            if (under != null && mouse.leftButton.wasPressedThisFrame)
            {
                PlayerController.ConsumeClick();
                Request(under);
            }
            return under;
        }

        // A finger on an item outlines it; putting it down on one asks for it. Taps on the on-screen
        // buttons don't count, even if an item lies under them.
        private LootDrop ReadTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null || !screen.primaryTouch.press.isPressed)
                return null;

            Vector2 position = screen.primaryTouch.position.ReadValue();
            if (TouchMode.IsOverBlocker(position) || TouchMode.IsOverMenuBlocker(position))
                return null;

            LootDrop under = PickableAt(position);
            if (under != null && screen.primaryTouch.press.wasPressedThisFrame)
            {
                PlayerController.ConsumeClick();
                Request(under);
            }
            return under;
        }

        /// <summary>Picks the item up now if it's in reach, otherwise walks over to get it.</summary>
        public void Request(LootDrop drop)
        {
            if (drop == null || !drop.IsInteractive)
                return;

            if (InReach(drop))
            {
                SetTarget(null);
                controller.CancelWalk();
                drop.TryPickUp(inventory, NoticePoint());
                return;
            }

            SetTarget(drop);
            controller.WalkTo(drop.transform.position, PickupReach * 0.8f);
        }

        private bool InReach(LootDrop drop)
        {
            Vector3 offset = drop.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= PickupReach * PickupReach;
        }

        private Vector3 NoticePoint()
        {
            return transform.position + Vector3.up * 1.4f;
        }

        private bool PanelOpen()
        {
            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if (characterUI == null)
                characterUI = FindAnyObjectByType<CharacterPageUI>();

            return (inventoryUI != null && inventoryUI.IsOpen) || (characterUI != null && characterUI.IsOpen);
        }

        // An item is outlined while it's pointed at or being walked to; these keep that in sync.
        private void SetHovered(LootDrop drop)
        {
            // Every frame, before the early-out: a picked-up (destroyed) drop compares equal to
            // null, so the early-out alone would leave its tooltip up forever.
            InventoryUI.GroundHover = drop != null ? drop.Item : null;
            if (drop != null)
                InventoryUI.GroundHoverAt = drop.transform.position;
            if (drop == hovered)
                return;
            LootDrop previous = hovered;
            hovered = drop;
            Refresh(previous);
            Refresh(hovered);
        }

        private void SetTarget(LootDrop drop)
        {
            if (drop == target)
                return;
            LootDrop previous = target;
            target = drop;
            Refresh(previous);
            Refresh(target);
        }

        private void Refresh(LootDrop drop)
        {
            if (drop != null)
                drop.SetHighlighted(drop == hovered || drop == target);
        }
    }
}
