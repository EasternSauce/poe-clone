using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class ItemTierTests
    {
        [Test]
        public void HigherTiersNeverDropBelowTheirLevel()
        {
            var rng = new System.Random(11);
            for (int k = 0; k < 500; k++)
            {
                ItemData item = ItemGenerator.Generate(rng, 1, ItemRarity.Normal);
                Assert.AreEqual(1, ItemGenerator.MinLevelOf(item.Id), item.Id + " dropped at item level 1");
            }
        }

        [Test]
        public void HigherTiersDropDeeperIn()
        {
            var rng = new System.Random(12);
            bool sawTier = false;
            for (int k = 0; k < 500 && !sawTier; k++)
                sawTier = ItemGenerator.MinLevelOf(ItemGenerator.Generate(rng, 10, ItemRarity.Normal).Id) > 1;
            Assert.IsTrue(sawTier);
        }

        [Test]
        public void TiersBorrowTheirBaseArt_AndTintIt()
        {
            ItemData tier = ItemGenerator.Display("champion_blade", null, ItemRarity.Normal);
            ItemData original = ItemGenerator.Display("rusty_sword", null, ItemRarity.Normal);
            Assert.AreEqual("rusty_sword", tier.ArtId);
            Assert.AreEqual(original.Type, tier.Type);
            Assert.AreEqual(original.WeaponType, tier.WeaponType);
            Assert.AreNotEqual(UnityEngine.Color.white, tier.ArtTint);
            Assert.AreEqual("rusty_sword", original.ArtId);
        }

        [Test]
        public void SavedTierItemsGetTheirArtBack()
        {
            ItemData tier = ItemGenerator.Generate(new System.Random(3), "full_plate", 9, ItemRarity.Rare);
            ItemData loaded = ItemRecord.From(tier).ToItem();
            Assert.AreEqual(tier.ArtId, loaded.ArtId);
            Assert.AreEqual(tier.ArtTint, loaded.ArtTint);
        }
    }
}
