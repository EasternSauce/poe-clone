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
        public void KeystonesAreDeadEndsThatChangeTheRules()
        {
            int keystones = 0;
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                if (!node.Keystone)
                    continue;
                keystones++;
                Assert.AreEqual(1, node.Links.Count, node.Name);
                bool special = false;
                foreach (StatModifier m in node.Mods)
                    special |= StatFormatter.IsSpecial(m.Stat);
                special |= node.PerBranchMods.Length > 0;
                Assert.IsTrue(special, node.Name);
            }
            Assert.GreaterOrEqual(keystones, 12);
        }

        [Test]
        public void EveryKeystoneRequiresThreeTravelPassivesOnItsOnlyApproach()
        {
            foreach (PassiveNode keystone in PassiveTree.Nodes)
            {
                if (!keystone.Keystone)
                    continue;

                PassiveNode current = keystone;
                string towardKeystone = null;
                for (int step = 0; step < 3; step++)
                {
                    Assert.AreEqual(step == 0 ? 1 : 2, current.Links.Count, current.Name + " has another way in");
                    string next = current.Links[0] == towardKeystone ? current.Links[1] : current.Links[0];
                    PassiveNode travel = PassiveTree.Get(next);
                    Assert.IsTrue(travel.Id.StartsWith("travel_"), keystone.Name + " is missing travel point " + (step + 1));
                    towardKeystone = current.Id;
                    current = travel;
                }
            }
        }

        [Test]
        public void EveryPassiveCanBeReachedFromTheOrigin()
        {
            var reached = new HashSet<string> { PassiveTree.OriginId };
            var open = new Stack<string>();
            open.Push(PassiveTree.OriginId);
            while (open.Count > 0)
            {
                foreach (string link in PassiveTree.Get(open.Pop()).Links)
                {
                    if (reached.Add(link))
                        open.Push(link);
                }
            }
            Assert.AreEqual(PassiveTree.Nodes.Count, reached.Count);
            Assert.Greater(PassiveTree.Nodes.Count, 80);
        }

        [Test]
        public void InALoopAPassiveInTheMiddleCanBeGivenBack()
        {
            // The inner ring: m1 - f1 - g1, with m1 and g1 both on the origin.
            var allocation = new PassiveAllocation();
            allocation.Take("m1", 10);
            allocation.Take("f1", 10);
            allocation.Take("g1", 10);
            Assert.IsTrue(allocation.CanRefund("m1"), "f1 still reaches the origin through g1");
        }

        [Test]
        public void DevotionGrowsWithEachPassiveOfItsSector()
        {
            var allocation = new PassiveAllocation();
            foreach (string id in new[] { "w1", "w2", "w3", "w_m1", "w_m2", "w_archmage" })
                Assert.IsTrue(allocation.Take(id, 20), id);
            // Archmage: 15% spell damage, plus 1% per Wisdom passive (six taken), plus Spellcraft
            // 6, Potency 8, Sorcery 6.
            StatSheet sheet = StatSheet.Build(new BaseStats(), new EquipmentSet(), allocation.Modifiers());
            Assert.AreEqual(15f + 6f + 6f + 8f + 6f, sheet.Total(StatType.SpellDamage), 0.001f);
            allocation.Take("z1", 20); // another sector's passive doesn't count
            sheet = StatSheet.Build(new BaseStats(), new EquipmentSet(), allocation.Modifiers());
            Assert.AreEqual(41f, sheet.Total(StatType.SpellDamage), 0.001f);
        }

        [Test]
        public void IncreasedLifeScalesTheWholePool()
        {
            var mods = new List<StatModifier> { new StatModifier(StatType.MaxLife, 20f), new StatModifier(StatType.IncreasedLife, 10f) };
            StatSheet sheet = StatSheet.Build(new BaseStats().Set(StatType.MaxLife, 100f), new EquipmentSet(), mods);
            Assert.AreEqual(132f, sheet.Total(StatType.MaxLife), 0.001f);
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
