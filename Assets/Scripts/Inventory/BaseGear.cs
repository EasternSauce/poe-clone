using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Marks a piece of the character's starting clothing (e.g. the hood) that should disappear
    /// once something is equipped in the given slot (e.g. a helmet).
    /// </summary>
    public class BaseGear : MonoBehaviour
    {
        [SerializeField] private EquipSlot hiddenBy;
        [SerializeField] private bool visibleIfItemHasCape;

        public EquipSlot HiddenBy
        {
            get { return hiddenBy; }
            set { hiddenBy = value; }
        }

        /// <summary>
        /// If true, this piece stays visible when the item in the slot has a cape
        /// (used for the character's cloak, which body armour with a cape keeps).
        /// </summary>
        public bool VisibleIfItemHasCape
        {
            get { return visibleIfItemHasCape; }
            set { visibleIfItemHasCape = value; }
        }
    }
}
