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

        public ItemData Get(EquipSlot slot)
        {
            ItemData item;
            return equipped.TryGetValue(slot, out item) ? item : null;
        }

        public bool CanEquip(EquipSlot slot, ItemData item)
        {
            if (!SlotRules.Accepts(slot, item))
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
        {
            replaced = null;

            if (!CanEquip(slot, item))
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
