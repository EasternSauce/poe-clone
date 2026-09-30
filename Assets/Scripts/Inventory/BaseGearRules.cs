namespace PoeClone.Inventory
{
    /// <summary>When a piece of the character's starting clothing should be hidden.</summary>
    public static class BaseGearRules
    {
        /// <summary>
        /// A starting piece is hidden once something is worn in the slot that replaces it,
        /// unless the piece is allowed to stay (the cloak) and the worn item has a cape.
        /// </summary>
        public static bool IsHidden(EquipSlot hiddenBy, bool visibleIfItemHasCape, EquipmentSet equipment)
        {
            if (equipment == null)
                return false;

            ItemData worn = equipment.Get(hiddenBy);
            if (worn == null)
                return false;

            return !(visibleIfItemHasCape && worn.HasCape);
        }
    }
}
