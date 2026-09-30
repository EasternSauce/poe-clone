using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>What kind of gear an item is. Decides which equipment slot it fits.</summary>
    public enum ItemType
    {
        Helmet,
        BodyArmour,
        Gloves,
        Boots,
        Belt,
        Amulet,
        Ring,
        Weapon,
        Shield
    }

    /// <summary>The places gear can be worn. There are two ring slots and one amulet slot.</summary>
    public enum EquipSlot
    {
        MainHand,
        OffHand,
        Helmet,
        Amulet,
        BodyArmour,
        Gloves,
        Ring1,
        Belt,
        Ring2,
        Boots
    }

    /// <summary>
    /// An item. Width/Height are the cells it occupies in the inventory grid.
    /// Id links it to its icon (Resources/ItemIcons/{Id}) and its 3D look (Resources/Equipment/{Id}).
    /// </summary>
    public sealed class ItemData
    {
        private static readonly IReadOnlyList<StatModifier> NoModifiers = new StatModifier[0];

        public string Id { get; }
        public string Name { get; }
        public ItemType Type { get; }
        public int Width { get; }
        public int Height { get; }
        public Color Tint { get; }
        public IReadOnlyList<StatModifier> Modifiers { get; }

        /// <summary>
        /// For body armour: true if this armour has a cape, so the character's cloak stays visible with it.
        /// Armour without one replaces the cloak.
        /// </summary>
        public bool HasCape { get; }

        public ItemData(string name, ItemType type, int width, int height, Color tint)
            : this(null, name, type, width, height, tint, null)
        {
        }

        public ItemData(string id, string name, ItemType type, int width, int height, Color tint, IEnumerable<StatModifier> modifiers, bool hasCape = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Item needs a name.", nameof(name));
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");

            Id = string.IsNullOrEmpty(id) ? name.ToLowerInvariant().Replace(' ', '_') : id;
            Name = name;
            Type = type;
            Width = width;
            Height = height;
            Tint = tint;
            Modifiers = modifiers == null ? NoModifiers : new List<StatModifier>(modifiers);
            HasCape = hasCape;
        }

        public override string ToString()
        {
            return Name + " (" + Type + " " + Width + "x" + Height + ")";
        }
    }

    /// <summary>Which item type each slot accepts. Only a matching item may go in a slot.</summary>
    public static class SlotRules
    {
        public static readonly EquipSlot[] AllSlots =
        {
            EquipSlot.MainHand,
            EquipSlot.OffHand,
            EquipSlot.Helmet,
            EquipSlot.Amulet,
            EquipSlot.BodyArmour,
            EquipSlot.Gloves,
            EquipSlot.Ring1,
            EquipSlot.Belt,
            EquipSlot.Ring2,
            EquipSlot.Boots
        };

        public static ItemType AcceptedType(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.MainHand: return ItemType.Weapon;
                case EquipSlot.OffHand: return ItemType.Shield;
                case EquipSlot.Helmet: return ItemType.Helmet;
                case EquipSlot.Amulet: return ItemType.Amulet;
                case EquipSlot.BodyArmour: return ItemType.BodyArmour;
                case EquipSlot.Gloves: return ItemType.Gloves;
                case EquipSlot.Ring1: return ItemType.Ring;
                case EquipSlot.Ring2: return ItemType.Ring;
                case EquipSlot.Belt: return ItemType.Belt;
                case EquipSlot.Boots: return ItemType.Boots;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown slot.");
            }
        }

        public static bool Accepts(EquipSlot slot, ItemData item)
        {
            return item != null && item.Type == AcceptedType(slot);
        }
    }
}
