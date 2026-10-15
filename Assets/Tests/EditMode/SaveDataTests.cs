using NUnit.Framework;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class SaveDataTests
    {
        [Test]
        public void DuplicateEquipmentSlotsRecoverTheExtraItemInTheBag()
        {
            var go = new GameObject("DuplicateGearSaveTest");
            try
            {
                PlayerInventory inventory = NewInventory(go);
                var data = new SaveData();
                ItemData first = ItemGenerator.Generate(new System.Random(1), "iron_ring", 5, ItemRarity.Rare);
                ItemData second = ItemGenerator.Generate(new System.Random(2), "iron_ring", 5, ItemRarity.Rare);
                data.equipped.Add(new EquippedRecord { slot = (int)EquipSlot.Ring1, item = ItemRecord.From(first) });
                data.equipped.Add(new EquippedRecord { slot = (int)EquipSlot.Ring1, item = ItemRecord.From(second) });

                Assert.IsEmpty(data.RestoreInventory(inventory));
                Assert.AreEqual(first.Name, inventory.Equipment.Get(EquipSlot.Ring1).Name);
                Assert.AreEqual(1, inventory.Grid.Items.Count);
                Assert.AreEqual(second.Name, inventory.Grid.Items[0].Item.Name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ExtraSavedStashTabsReturnTheirItemsForRecovery()
        {
            var go = new GameObject("ExtraStashSaveTest");
            try
            {
                PlayerInventory inventory = NewInventory(go);
                var data = new SaveData();
                for (int i = 0; i < PlayerInventory.StashTabCount; i++)
                    data.stashTabs.Add(new StashTabRecord());
                data.stashTabs[PlayerInventory.StashTabCount - 1].items.Add(null);
                data.stashTabs[PlayerInventory.StashTabCount - 1].items.Add(new PlacedRecord
                    { item = ItemRecord.From(ItemData.RegretItem()) });

                var overflow = data.RestoreStash(inventory);
                Assert.AreEqual(1, overflow.Count);
                Assert.AreEqual(ItemData.RegretId, overflow[0].Id);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FullBagReturnsUnplaceableSavedItemsInsteadOfLosingThem()
        {
            var go = new GameObject("OverflowSaveTest");
            try
            {
                PlayerInventory inventory = NewInventory(go);
                for (int y = 0; y < inventory.Grid.Height; y++)
                    for (int x = 0; x < inventory.Grid.Width; x++)
                        Assert.IsTrue(inventory.Grid.TryPlace(new ItemData("Filler", ItemType.Ring, 1, 1, Color.white), x, y));
                var data = new SaveData();
                data.bag.Add(new PlacedRecord { item = ItemRecord.From(ItemData.RegretItem()) });

                var overflow = data.RestoreInventory(inventory);
                Assert.AreEqual(1, overflow.Count);
                Assert.AreEqual(ItemData.RegretId, overflow[0].Id);
                Assert.AreEqual(inventory.Grid.Width * inventory.Grid.Height, inventory.Grid.Items.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(-10, 1)]
        [TestCase(37, 37)]
        [TestCase(200, 100)]
        public void SavedConsumableStacksKeepTheirCountsWithinTheStackLimit(int saved, int expected)
        {
            ItemRecord record = ItemRecord.From(ItemData.RegretItem());
            record.count = saved;
            ItemData copy = JsonUtility.FromJson<ItemRecord>(JsonUtility.ToJson(record)).ToItem();
            Assert.AreEqual(expected, copy.StackCount);
        }

        [Test]
        public void EmptySavedEntriesDoNotPreventRestoringValidItems()
        {
            var go = new GameObject("SparseSaveTest");
            try
            {
                PlayerInventory inventory = NewInventory(go);
                var data = new SaveData();
                ItemData bow = ItemGenerator.Generate(new System.Random(7), "short_bow", 5, ItemRarity.Rare);
                ItemRecord record = ItemRecord.From(bow);
                record.mods.Add(null);
                data.bag.Add(null);
                data.bag.Add(new PlacedRecord());
                data.bag.Add(new PlacedRecord { item = record, x = -1, y = -1 });
                data.equipped.Add(null);
                data.equipped.Add(new EquippedRecord());
                data.stash.Add(null);
                data.stash.Add(new PlacedRecord());
                data.stash.Add(new PlacedRecord { item = ItemRecord.From(bow), x = 7, y = 4 });

                Assert.IsEmpty(data.RestoreInventory(inventory));
                Assert.AreEqual(1, inventory.Grid.Items.Count);
                Assert.AreEqual(bow.Name, inventory.Grid.Items[0].Item.Name);
                Assert.AreEqual(bow.Name, inventory.Stash.GetAt(7, 4).Item.Name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ItemsSurviveAJsonRoundTrip()
        {
            ItemData original = ItemGenerator.Generate(new System.Random(7), "short_bow", 5, ItemRarity.Rare);
            var data = new SaveData();
            data.bag.Add(new PlacedRecord { item = ItemRecord.From(original), x = 2, y = 1 });

            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            ItemData copy = loaded.bag[0].item.ToItem();

            Assert.AreEqual(original.Id, copy.Id);
            Assert.AreEqual(original.Name, copy.Name);
            Assert.AreEqual(original.Type, copy.Type);
            Assert.AreEqual(original.WeaponType, copy.WeaponType);
            Assert.AreEqual(original.Rarity, copy.Rarity);
            Assert.AreEqual(original.Width, copy.Width);
            Assert.AreEqual(original.Modifiers.Count, copy.Modifiers.Count);
            for (int k = 0; k < original.Modifiers.Count; k++)
            {
                Assert.AreEqual(original.Modifiers[k].Stat, copy.Modifiers[k].Stat);
                Assert.AreEqual(original.Modifiers[k].Value, copy.Modifiers[k].Value, 0.001f);
            }
        }

        [Test]
        public void UniquesKeepTheirRarityAndLore()
        {
            ItemData unique = UniqueItems.Create(0);
            ItemData copy = JsonUtility.FromJson<ItemRecord>(JsonUtility.ToJson(ItemRecord.From(unique))).ToItem();
            Assert.AreEqual(ItemRarity.Unique, copy.Rarity);
            Assert.IsNotNull(UniqueItems.FlavourFor(copy));
        }

        // Edit-mode tests don't run Awake, which builds the grids.
        private static PlayerInventory NewInventory(GameObject go)
        {
            PlayerInventory inventory = go.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(inventory, null);
            return inventory;
        }

        [Test]
        public void TheStashSurvivesSavingAndLoading()
        {
            var go = new GameObject("StashTest");
            try
            {
                PlayerInventory inventory = NewInventory(go);
                ItemData bow = ItemGenerator.Generate(new System.Random(3), "short_bow", 5, ItemRarity.Magic);
                Assert.IsTrue(inventory.Stash.TryPlace(bow, 7, 4));

                var data = new SaveData();
                data.CaptureInventory(inventory);
                SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

                var go2 = new GameObject("StashTest2");
                try
                {
                    PlayerInventory fresh = NewInventory(go2);
                    Assert.AreEqual(0, loaded.RestoreInventory(fresh).Count);
                    Assert.AreEqual(0, fresh.Grid.Items.Count);
                    PlacedItem placed = fresh.Stash.GetAt(7, 4);
                    Assert.IsNotNull(placed);
                    Assert.AreEqual(bow.Name, placed.Item.Name);
                }
                finally
                {
                    Object.DestroyImmediate(go2);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryStashTabAndItsCustomNameSurviveTheRoundTrip()
        {
            var source = new GameObject("AllStashTabsSaveTest");
            var target = new GameObject("AllStashTabsRestoreTest");
            try
            {
                PlayerInventory inventory = NewInventory(source);
                for (int tab = 0; tab < PlayerInventory.StashTabCount; tab++)
                {
                    ItemData stack = ItemData.RegretItem();
                    stack.StackCount = tab + 1;
                    Assert.IsTrue(inventory.StashTabs[tab].TryPlace(stack, tab, tab));
                    inventory.RenameStashTab(tab, "Saved " + tab);
                }
                inventory.SetStashTab(3);
                var data = new SaveData();
                data.CaptureInventory(inventory);
                SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
                PlayerInventory restored = NewInventory(target);
                Assert.IsEmpty(loaded.RestoreInventory(restored));
                for (int tab = 0; tab < PlayerInventory.StashTabCount; tab++)
                {
                    Assert.AreEqual(tab + 1, restored.StashTabs[tab].GetAt(tab, tab).Item.StackCount);
                    Assert.AreEqual("Saved " + tab, restored.StashTabCustomName(tab));
                }
                Assert.IsEmpty(restored.Grid.Items);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ListsAndNumbersSurviveTheRoundTrip()
        {
            var data = new SaveData { level = 6, experience = 120, gold = 345, healthPotions = 4, manaPotions = 2 };
            data.passives.Add("m1");
            data.skillSlots.AddRange(new[] { 0, -1, 3, 1 });
            data.questsDone.Add("woods");
            data.questsActive.Add(new QuestRecord { id = "graves", progress = 7 });
            data.visited.AddRange(new[] { 0, 1, 2 });

            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            Assert.AreEqual(6, loaded.level);
            Assert.AreEqual(345, loaded.gold);
            Assert.AreEqual("m1", loaded.passives[0]);
            Assert.AreEqual(-1, loaded.skillSlots[1]);
            Assert.AreEqual(7, loaded.questsActive[0].progress);
            Assert.AreEqual(3, loaded.visited.Count);
        }

        [Test]
        public void OldSavesNeedOnePassiveResetAndNewSavesDoNot()
        {
            SaveData old = JsonUtility.FromJson<SaveData>(
                "{\"version\":1,\"level\":12,\"passives\":[\"m1\"],\"respecCharges\":2}");
            Assert.AreEqual(0, old.passiveAllocationVersion);
            Assert.AreEqual(2, old.respecCharges);

            old.passives.Clear();
            old.passiveAllocationVersion = SaveData.CurrentPassiveAllocationVersion;
            SaveData migrated = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(old));
            Assert.AreEqual(SaveData.CurrentPassiveAllocationVersion, migrated.passiveAllocationVersion);
            Assert.AreEqual(0, migrated.passives.Count);
            Assert.AreEqual(2, migrated.respecCharges);
            Assert.AreEqual(12, migrated.level);
        }
    }
}
