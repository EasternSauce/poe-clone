using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// What the character is wearing. Only an item of the right type fits a slot.
    /// Raises <see cref="Changed"/> so other systems (e.g. the character's visuals) can react later.
    /// </summary>
    public sealed class EquipmentSet
    {
        private readonly Dictionary<EquipSlot, ItemData> equipped = new Dictionary<EquipSlot, ItemData>();

        /// <summary>Slot that changed, and the item now in it (null if it was emptied).</summary>
        public event Action<EquipSlot, ItemData> Changed;
        public Func<EquipSlot, ItemData, bool> MeetsRequirements { get; set; }
        public bool EnforceRequirements { get; set; }
        private HashSet<EquipSlot> activeSlots;
        public bool IsActive(EquipSlot slot) => Get(slot) != null && (activeSlots == null || activeSlots.Contains(slot));
        public ItemData GetActive(EquipSlot slot) => IsActive(slot) ? Get(slot) : null;
        internal void SetActiveSlots(HashSet<EquipSlot> slots) => activeSlots = slots;

        public ItemData Get(EquipSlot slot)
        {
            ItemData item;
            return equipped.TryGetValue(slot, out item) ? item : null;
        }

        public bool CanEquip(EquipSlot slot, ItemData item)
        {
            if (!SlotRules.Accepts(slot, item))
                return false;
            if (MeetsRequirements != null && !MeetsRequirements(slot, item))
                return false;

            // The two hands have to suit each other (a bow takes no shield, a quiver needs a bow).
            if (slot == EquipSlot.MainHand)
                return SlotRules.HandsCompatible(item, Get(EquipSlot.OffHand));
            if (slot == EquipSlot.OffHand)
                return SlotRules.HandsCompatible(Get(EquipSlot.MainHand), item);
            return true;
        }

        /// <summary>Equips the item. Whatever was in the slot is returned in <paramref name="replaced"/>.</summary>
        public bool TryEquip(EquipSlot slot, ItemData item, out ItemData replaced)
            => Equip(slot, item, out replaced, false);

        /// <summary>Restore existing saved or replicated gear before character stats arrive.</summary>
        public bool Restore(EquipSlot slot, ItemData item, out ItemData replaced)
            => Equip(slot, item, out replaced, true);

        private bool Equip(EquipSlot slot, ItemData item, out ItemData replaced, bool restoring)
        {
            replaced = null;

            if (restoring ? !SlotRules.Accepts(slot, item) ||
                (slot == EquipSlot.MainHand && !SlotRules.HandsCompatible(item, Get(EquipSlot.OffHand))) ||
                (slot == EquipSlot.OffHand && !SlotRules.HandsCompatible(Get(EquipSlot.MainHand), item)) : !CanEquip(slot, item))
                return false;

            replaced = Get(slot);
            equipped[slot] = item;
            Changed?.Invoke(slot, item);
            return true;
        }

        /// <summary>Empties the slot and returns what was in it (null if it was empty).</summary>
        public ItemData Unequip(EquipSlot slot)
        {
            ItemData old = Get(slot);
            if (old == null)
                return null;

            equipped.Remove(slot);
            Changed?.Invoke(slot, null);
            return old;
        }
    }
}
