using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>One stat line of a saved item.</summary>
    [Serializable]
    public class ModRecord
    {
        public int stat;
        public float value;
    }

    /// <summary>An item as saved (JsonUtility-friendly: plain public fields).</summary>
    [Serializable]
    public class ItemRecord
    {
        public string id;
        public string name;
        public int type;
        public int w;
        public int h;
        public int weapon;
        public int rarity;
        public bool cape;
        public float r, g, b, a;
        public List<ModRecord> mods = new List<ModRecord>();

        public static ItemRecord From(ItemData item)
        {
            var record = new ItemRecord
            {
                id = item.Id, name = item.Name, type = (int)item.Type, w = item.Width, h = item.Height,
                weapon = (int)item.WeaponType, rarity = (int)item.Rarity, cape = item.HasCape,
                r = item.Tint.r, g = item.Tint.g, b = item.Tint.b, a = item.Tint.a
            };
            foreach (StatModifier m in item.Modifiers)
                record.mods.Add(new ModRecord { stat = (int)m.Stat, value = m.Value });
            return record;
        }

        public ItemData ToItem()
        {
            var modifiers = new List<StatModifier>();
            if (mods != null)
            {
                foreach (ModRecord m in mods)
                    modifiers.Add(new StatModifier((StatType)m.stat, m.value));
            }
            return new ItemData(id, name, (ItemType)type, Math.Max(1, w), Math.Max(1, h), new Color(r, g, b, a), modifiers,
                hasCape: cape, weaponType: (WeaponType)weapon, rarity: (ItemRarity)rarity);
        }
    }

    [Serializable]
    public class PlacedRecord
    {
        public ItemRecord item;
        public int x, y;
    }

    [Serializable]
    public class EquippedRecord
    {
        public int slot;
        public ItemRecord item;
    }

    [Serializable]
    public class QuestRecord
    {
        public string id;
        public int progress;
    }

    /// <summary>
    /// Everything that carries over between visits: the character's level, purse, potions, bag
    /// and gear, passives, skill slots and quests. Saved as JSON (see Player.SaveSystem).
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int level = 1;
        public int experience;
        public int gold;
        public int healthPotions;
        public int manaPotions;
        public List<PlacedRecord> bag = new List<PlacedRecord>();
        public List<EquippedRecord> equipped = new List<EquippedRecord>();
        public List<string> passives = new List<string>();
        public List<int> skillSlots = new List<int>();     // SkillId per slot, -1 for empty
        public List<string> questsDone = new List<string>();
        public List<QuestRecord> questsActive = new List<QuestRecord>();
        public List<int> visited = new List<int>();

        /// <summary>Copies the bag and worn gear into this save.</summary>
        public void CaptureInventory(PlayerInventory inventory)
        {
            bag.Clear();
            foreach (PlacedItem placed in inventory.Grid.Items)
                bag.Add(new PlacedRecord { item = ItemRecord.From(placed.Item), x = placed.X, y = placed.Y });

            equipped.Clear();
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.Get(slot);
                if (item != null)
                    equipped.Add(new EquippedRecord { slot = (int)slot, item = ItemRecord.From(item) });
            }
            gold = inventory.Gold;
        }

        /// <summary>
        /// Puts the saved bag and gear on an empty inventory. An item that no longer fits where it
        /// was goes anywhere else in the bag; if even that fails it is returned (to be dropped).
        /// </summary>
        public List<ItemData> RestoreInventory(PlayerInventory inventory)
        {
            var leftOver = new List<ItemData>();
            if (equipped != null)
            {
                foreach (EquippedRecord e in equipped)
                {
                    ItemData item = e.item != null ? e.item.ToItem() : null;
                    if (item != null && !inventory.Equipment.TryEquip((EquipSlot)e.slot, item, out _))
                        leftOver.Add(item);
                }
            }

            if (bag != null)
            {
                foreach (PlacedRecord p in bag)
                {
                    ItemData item = p.item != null ? p.item.ToItem() : null;
                    if (item == null)
                        continue;
                    if (!inventory.Grid.TryPlace(item, p.x, p.y) && !inventory.Grid.TryAutoPlace(item))
                        leftOver.Add(item);
                }
            }

            var result = new List<ItemData>();
            foreach (ItemData item in leftOver)
            {
                if (!inventory.Grid.TryAutoPlace(item))
                    result.Add(item);
            }

            if (gold > inventory.Gold)
                inventory.AddGold(gold - inventory.Gold);
            return result;
        }
    }
}
