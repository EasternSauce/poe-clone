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
        public int count;
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
                id = item.Id, name = item.Name, count = item.StackCount, type = (int)item.Type, w = item.Width, h = item.Height,
                weapon = (int)item.WeaponType, rarity = (int)item.Rarity, cape = item.HasCape,
                r = item.Tint.r, g = item.Tint.g, b = item.Tint.b, a = item.Tint.a
            };
            foreach (StatModifier m in item.Modifiers)
                record.mods.Add(new ModRecord { stat = (int)m.Stat, value = m.Value });
            return record;
        }

        public ItemData ToItem()
        {
            if (id == ItemData.ReawakeningId)
            {
                ItemData heart = ItemData.ReawakeningItem();
                heart.StackCount = Math.Max(1, Math.Min(heart.MaxStack, count));
                return heart;
            }
            var modifiers = new List<StatModifier>();
            if (mods != null)
            {
                foreach (ModRecord m in mods)
                    modifiers.Add(new StatModifier((StatType)m.stat, m.value));
            }
            var item = new ItemData(id, name, (ItemType)type, Math.Max(1, w), Math.Max(1, h), new Color(r, g, b, a), modifiers,
                hasCape: cape, weaponType: (WeaponType)weapon, rarity: (ItemRarity)rarity);
            ItemGenerator.ApplyArt(item);
            // Made under older rules? Stats no longer allowed are fixed or removed.
            return ItemGenerator.Legalize(item);
        }
    }

    /// <summary>One stash tab's items.</summary>
    [Serializable]
    public class StashTabRecord
    {
        public List<PlacedRecord> items = new List<PlacedRecord>();
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
        public List<PlacedRecord> stash = new List<PlacedRecord>();           // the first stash tab
        public List<StashTabRecord> stashTabs = new List<StashTabRecord>(); // the other tabs, in order
        public List<string> stashTabNames = new List<string>();             // every tab's own name, "" = none
        public List<EquippedRecord> equipped = new List<EquippedRecord>();
        public List<string> passives = new List<string>();
        public int respecCharges = 1;
        public List<int> skillSlots = new List<int>();     // SkillId per slot, -1 for empty
        public int barLayoutVersion;                         // 0: old mouse-first order; 1: current bar order
        public List<string> questsDone = new List<string>();
        public List<QuestRecord> questsActive = new List<QuestRecord>();
        public List<string> questProps = new List<string>(); // quest props used (Quests.QuestProp ids)
        public int questBook;                                 // Quests.QuestBook.Version the quests were saved under (0: before the storylines)
        public List<int> visited = new List<int>();
        public List<string> mapSeen = new List<string>();  // minimap fog of war, per area (MinimapTerrain)
        public bool gearSkills;                            // saved since skills come from gear (older characters get a staff once)
        public bool waystonesReset;                        // saved after the one-time waystone reset (older saves lose their visited areas once)

        private static void RestoreTab(InventoryGrid tab, List<PlacedRecord> records, List<ItemData> leftOver)
        {
            foreach (PlacedRecord p in records)
            {
                ItemData item = p != null && p.item != null ? p.item.ToItem() : null;
                if (item != null && !tab.TryPlace(item, p.x, p.y) && !tab.TryAutoPlace(item))
                    leftOver.Add(item);
            }
        }

        /// <summary>Copies the bag and worn gear into this save.</summary>
        public void CaptureInventory(PlayerInventory inventory)
        {
            bag.Clear();
            foreach (PlacedItem placed in inventory.Grid.Items)
                bag.Add(new PlacedRecord { item = ItemRecord.From(placed.Item), x = placed.X, y = placed.Y });

            CaptureStash(inventory);

            equipped.Clear();
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.Get(slot);
                if (item != null)
                    equipped.Add(new EquippedRecord { slot = (int)slot, item = ItemRecord.From(item) });
            }
            gold = inventory.Gold;
        }

        public void CaptureStash(PlayerInventory inventory)
        {
            if (stash == null)
                stash = new List<PlacedRecord>();
            stash.Clear();
            foreach (PlacedItem placed in inventory.StashTabs[0].Items)
                stash.Add(new PlacedRecord { item = ItemRecord.From(placed.Item), x = placed.X, y = placed.Y });
            stashTabs = new List<StashTabRecord>();
            for (int k = 1; k < inventory.StashTabs.Length; k++)
            {
                var tab = new StashTabRecord();
                foreach (PlacedItem placed in inventory.StashTabs[k].Items)
                    tab.items.Add(new PlacedRecord { item = ItemRecord.From(placed.Item), x = placed.X, y = placed.Y });
                stashTabs.Add(tab);
            }
            stashTabNames = new List<string>();
            for (int k = 0; k < inventory.StashTabs.Length; k++)
                stashTabNames.Add(inventory.StashTabCustomName(k));
        }

        /// <summary>
        /// Puts the saved bag and gear on an empty inventory. An item that no longer fits where it
        /// was goes anywhere else in the bag; if even that fails it is returned (to be dropped).
        /// </summary>
        public List<ItemData> RestoreInventory(PlayerInventory inventory, bool restoreStash = true)
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

            if (restoreStash)
                leftOver.AddRange(RestoreStash(inventory));

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

        public List<ItemData> RestoreStash(PlayerInventory inventory)
        {
            var leftOver = new List<ItemData>();
            if (stash != null)
                RestoreTab(inventory.StashTabs[0], stash, leftOver);
            if (stashTabs != null)
                for (int k = 0; k < stashTabs.Count && k + 1 < inventory.StashTabs.Length; k++)
                    if (stashTabs[k] != null && stashTabs[k].items != null)
                        RestoreTab(inventory.StashTabs[k + 1], stashTabs[k].items, leftOver);
            if (stashTabNames != null)
            {
                for (int k = 0; k < stashTabNames.Count && k < inventory.StashTabs.Length; k++)
                    inventory.RenameStashTab(k, stashTabNames[k]);
            }
            return leftOver;
        }
    }
}
