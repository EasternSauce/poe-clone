using NUnit.Framework;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class SaveDataTests
    {
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
