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
    }
}
