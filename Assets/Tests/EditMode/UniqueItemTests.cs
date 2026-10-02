using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class UniqueItemTests
    {
        [Test]
        public void EveryUniqueIsBuiltOnARealBaseWithStats()
        {
            for (int k = 0; k < UniqueItems.Count; k++)
            {
                ItemData item = UniqueItems.Create(k);
                Assert.AreEqual(ItemRarity.Unique, item.Rarity, item.Name);
                Assert.GreaterOrEqual(item.Modifiers.Count, 3, item.Name);
                Assert.IsNotNull(UniqueItems.FlavourFor(item), item.Name);
            }
        }

        [Test]
        public void UniquesCanBeEquippedInTheirSlot()
        {
            for (int k = 0; k < UniqueItems.Count; k++)
            {
                ItemData item = UniqueItems.Create(k);
                var gear = new EquipmentSet();
                bool equipped = false;
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    if (gear.TryEquip(slot, item, out _))
                    {
                        equipped = true;
                        break;
                    }
                }
                Assert.IsTrue(equipped, item.Name);
            }
        }

        [Test]
        public void EveryUniqueHasASpecialStat()
        {
            for (int k = 0; k < UniqueItems.Count; k++)
            {
                ItemData item = UniqueItems.Create(k);
                bool special = false;
                foreach (StatModifier m in item.Modifiers)
                    special |= StatFormatter.IsSpecial(m.Stat);
                Assert.IsTrue(special, item.Name);
            }
        }

        [Test]
        public void OtherItemsHaveNoFlavour()
        {
            ItemData plain = ItemGenerator.Generate(new System.Random(3), "rusty_sword", 1, ItemRarity.Rare);
            Assert.IsNull(UniqueItems.FlavourFor(plain));
        }
    }
}
