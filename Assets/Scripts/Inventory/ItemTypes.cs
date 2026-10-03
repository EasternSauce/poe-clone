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
        Shield,
        Quiver,  // off hand, worn with a bow
        Potion,  // lies on the ground; picked up into the potion slots, never the bag
        Gold     // lies on the ground; picked up into the purse
    }

    /// <summary>
    /// How special an item is, PoE style: Normal (its base stats only), Magic (one or two extra
    /// stats, blue) or Rare (three or more, yellow, with a made-up name).
    /// </summary>
    public enum ItemRarity
    {
        Normal,
        Magic,
        Rare,
        Unique   // hand-made, fixed stats (UniqueItems)
    }

    /// <summary>
    /// The style of weapon an item is, for a Weapon item. Decides which attack animation and
    /// base attack pace the player's combat script uses when it is wielded.
    /// Unarmed is not stored on any item; combat code falls back to it when no weapon is equipped.
    /// </summary>
    public enum WeaponType
    {
        // Values are stable (saved/replicated by number); add new types at the end.
        Unarmed,
        Sword,
        Axe,
        Mace,
        Dagger,
        Bow,
        Staff    // two-handed; its spell (see SkillGrants.StaffMain) is its attack
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

        /// <summary>For a Weapon item, which attack animation/pace it uses. Meaningless otherwise.</summary>
        public WeaponType WeaponType { get; }

        /// <summary>Normal / Magic / Rare: the colour of its name and border.</summary>
        public ItemRarity Rarity { get; }

        private string artId;

        /// <summary>
        /// Which icon and 3D look the item uses (Resources/ItemIcons and Resources/Equipment): its
        /// own id, or for a higher tier of a base, the base it's drawn like (tinted by ArtTint).
        /// </summary>
        public string ArtId
        {
            get { return artId ?? Id; }
            set { artId = value; }
        }

        /// <summary>Multiplied over the shared art, so tiers of the same look tell apart.</summary>
        public Color ArtTint { get; set; } = Color.white;

        public ItemData(string name, ItemType type, int width, int height, Color tint)
            : this(null, name, type, width, height, tint, null)
        {
        }

        /// <param name="rarity">Defaults to Magic for an item with stats and Normal without (the starter items).</param>
        public ItemData(string id, string name, ItemType type, int width, int height, Color tint, IEnumerable<StatModifier> modifiers, bool hasCape = false, WeaponType weaponType = WeaponType.Sword, ItemRarity? rarity = null)
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
            WeaponType = weaponType;
            Rarity = rarity ?? (Modifiers.Count > 0 ? ItemRarity.Magic : ItemRarity.Normal);
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

        /// <summary>
        /// Every type the slot takes. The off hand holds a shield, or a quiver for a bow;
        /// <see cref="AcceptedType"/> is its main one (the slot's hint icon).
        /// </summary>
        public static ItemType[] AcceptedTypes(EquipSlot slot)
        {
            if (slot == EquipSlot.OffHand)
                return new[] { ItemType.Shield, ItemType.Quiver };
            return new[] { AcceptedType(slot) };
        }

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
            return item != null && Array.IndexOf(AcceptedTypes(slot), item.Type) >= 0;
        }

        /// <summary>
        /// Whether a main-hand and an off-hand item can be worn together. A bow takes both hands:
        /// no shield with it, only a quiver; and a quiver is only for a bow (or empty hands). A staff
        /// takes both hands and leaves nothing for the off hand. Either may be null (nothing in that hand).
        /// </summary>
        public static bool HandsCompatible(ItemData mainHand, ItemData offHand)
        {
            bool bow = mainHand != null && mainHand.Type == ItemType.Weapon && mainHand.WeaponType == WeaponType.Bow;
            bool staff = mainHand != null && mainHand.Type == ItemType.Weapon && mainHand.WeaponType == WeaponType.Staff;

            if (staff)
                return offHand == null;
            if (bow)
                return offHand == null || offHand.Type == ItemType.Quiver;
            if (offHand != null && offHand.Type == ItemType.Quiver)
                return mainHand == null;
            return true;
        }
    }
}
