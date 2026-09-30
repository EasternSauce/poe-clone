using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Starter items. Each item's id matches its icon (Resources/ItemIcons/{id}.png)
    /// and its 3D look on the character (Resources/Equipment/{id}.prefab).
    /// </summary>
    public static class ItemCatalog
    {
        private static ItemData Make(string id, string name, ItemType type, int w, int h, Color tint, params StatModifier[] mods)
        {
            return new ItemData(id, name, type, w, h, tint, mods);
        }

        private static ItemData MakeWeapon(string id, string name, int w, int h, Color tint, WeaponType weaponType, params StatModifier[] mods)
        {
            return new ItemData(id, name, ItemType.Weapon, w, h, tint, mods, hasCape: false, weaponType: weaponType);
        }

        private static StatModifier Mod(StatType stat, float value)
        {
            return new StatModifier(stat, value);
        }

        public static List<ItemData> CreateStarterItems()
        {
            return new List<ItemData>
            {
                Make("iron_helmet", "Iron Helmet", ItemType.Helmet, 2, 2, new Color(0.62f, 0.66f, 0.72f),
                    Mod(StatType.Armour, 32), Mod(StatType.MaxLife, 12)),

                Make("bronze_helmet", "Bronze Helmet", ItemType.Helmet, 2, 2, new Color(0.80f, 0.55f, 0.25f),
                    Mod(StatType.Armour, 24), Mod(StatType.Strength, 8)),

                Make("studded_vest", "Studded Vest", ItemType.BodyArmour, 2, 3, new Color(0.55f, 0.40f, 0.26f),
                    Mod(StatType.Armour, 62), Mod(StatType.MaxLife, 25), Mod(StatType.Evasion, 20)),

                Make("leather_gloves", "Leather Gloves", ItemType.Gloves, 2, 2, new Color(0.50f, 0.36f, 0.24f),
                    Mod(StatType.Evasion, 18), Mod(StatType.Dexterity, 8), Mod(StatType.AttackSpeed, 4)),

                Make("leather_boots", "Leather Boots", ItemType.Boots, 2, 2, new Color(0.45f, 0.32f, 0.22f),
                    Mod(StatType.Evasion, 20), Mod(StatType.MovementSpeed, 10)),

                Make("rope_belt", "Rope Belt", ItemType.Belt, 2, 1, new Color(0.72f, 0.62f, 0.40f),
                    Mod(StatType.MaxLife, 15), Mod(StatType.Strength, 6)),

                Make("jade_amulet", "Jade Amulet", ItemType.Amulet, 1, 1, new Color(0.30f, 0.80f, 0.55f),
                    Mod(StatType.Dexterity, 14), Mod(StatType.Evasion, 20), Mod(StatType.LightningResistance, 12)),

                Make("iron_ring", "Iron Ring", ItemType.Ring, 1, 1, new Color(0.70f, 0.72f, 0.76f),
                    Mod(StatType.PhysicalDamage, 2), Mod(StatType.Strength, 5)),

                Make("ruby_ring", "Ruby Ring", ItemType.Ring, 1, 1, new Color(0.90f, 0.25f, 0.30f),
                    Mod(StatType.FireResistance, 20), Mod(StatType.MaxLife, 10)),

                Make("sapphire_ring", "Sapphire Ring", ItemType.Ring, 1, 1, new Color(0.30f, 0.50f, 0.95f),
                    Mod(StatType.ColdResistance, 20), Mod(StatType.MaxMana, 15)),

                MakeWeapon("rusty_sword", "Rusty Sword", 1, 3, new Color(0.65f, 0.62f, 0.58f), WeaponType.Sword,
                    Mod(StatType.PhysicalDamage, 6)),

                Make("wooden_shield", "Wooden Shield", ItemType.Shield, 2, 2, new Color(0.60f, 0.42f, 0.25f),
                    Mod(StatType.Armour, 18), Mod(StatType.BlockChance, 12))
            };
        }
    }
}
