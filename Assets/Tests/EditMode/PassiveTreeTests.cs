using System.Collections.Generic;
using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class PassiveTreeTests
    {
        [Test]
        public void EveryLinkGoesBothWays()
        {
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                foreach (string other in node.Links)
                    Assert.Contains(node.Id, PassiveTree.Get(other).Links, node.Id + " -> " + other);
            }
        }

        [Test]
        public void OnePointPerLevelAfterTheFirst()
        {
            var allocation = new PassiveAllocation();
            Assert.AreEqual(0, allocation.Unspent(1));
            Assert.AreEqual(4, allocation.Unspent(5));
        }

        [Test]
        public void OnlyPassivesNextToTakenOnesCanBeTaken()
        {
            var allocation = new PassiveAllocation();
            Assert.IsFalse(allocation.CanTake("m2", 10), "not next to anything taken");
            Assert.IsTrue(allocation.Take("m1", 10));
            Assert.IsTrue(allocation.Take("m2", 10));
            Assert.AreEqual(2, allocation.Spent);
        }

        [Test]
        public void CannotSpendMorePointsThanTheLevelGives()
        {
            var allocation = new PassiveAllocation();
            Assert.IsTrue(allocation.Take("m1", 2));
            Assert.IsFalse(allocation.Take("g1", 2));
        }

        [Test]
        public void OnlyTheEndOfAPathCanBeGivenBack()
        {
            var allocation = new PassiveAllocation();
            allocation.Take("m1", 10);
            allocation.Take("m2", 10);
            Assert.IsFalse(allocation.CanRefund("m1"), "m2 would be cut off");
            Assert.IsTrue(allocation.Refund("m2"));
            Assert.IsTrue(allocation.Refund("m1"));
            Assert.IsFalse(allocation.CanRefund(PassiveTree.OriginId));
        }

        [Test]
        public void PassiveStatsCountLikeGear()
        {
            var allocation = new PassiveAllocation();
            allocation.Take("m1", 10);   // +12 life
            allocation.Take("m2", 10);   // +6 strength -> +3 life

            StatSheet sheet = StatSheet.Build(new BaseStats().Set(StatType.MaxLife, 100f), new EquipmentSet(), allocation.Modifiers());
            Assert.AreEqual(100f + 12f + 3f, sheet.Total(StatType.MaxLife), 0.001f);
            Assert.AreEqual(6f, sheet.Total(StatType.Strength), 0.001f);
        }

        [Test]
        public void EveryNotableLeadsToAKeystoneWithASpecialStat()
        {
            int keystones = 0;
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                if (!node.Keystone)
                    continue;
                keystones++;
                Assert.AreEqual(1, node.Links.Count, node.Name);
                Assert.IsTrue(PassiveTree.Get(node.Links[0]).Notable, node.Name);
                bool special = false;
                foreach (StatModifier m in node.Mods)
                    special |= StatFormatter.IsSpecial(m.Stat);
                Assert.IsTrue(special, node.Name);
            }
            Assert.AreEqual(6, keystones);
        }

        [Test]
        public void ManaAbsorbAddsToTheBaseShare()
        {
            Assert.AreEqual(10f * DefenceMath.ManaAbsorbShare, DefenceMath.ManaAbsorbed(10f, 50f), 1e-4f);
            Assert.AreEqual(10f * (DefenceMath.ManaAbsorbShare + 0.3f), DefenceMath.ManaAbsorbed(10f, 50f, 30f), 1e-4f);
        }

        [Test]
        public void ResetGivesEverythingBack()
        {
            var allocation = new PassiveAllocation();
            allocation.Take("w1", 10);
            allocation.Take("w2", 10);
            allocation.ResetAll();
            Assert.AreEqual(0, allocation.Spent);
            Assert.AreEqual(new List<StatModifier>().Count, allocation.Modifiers().Count);
        }
    }
}
