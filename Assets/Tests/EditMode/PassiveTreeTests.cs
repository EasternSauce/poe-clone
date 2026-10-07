using System.Collections.Generic;
using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class PassiveTreeTests
    {
        private static float GlyphRadius(PassiveNode node)
        {
            // UI: 60 px square rotated 45 degrees, 62 px notable, 56 px origin,
            // and 44 px basic, at 200 px per tree unit. Bound diamonds by a circle.
            return node.Keystone ? 30f * (float)System.Math.Sqrt(2) / 200f :
                node.Notable ? 31f / 200f : node.Id == PassiveTree.OriginId ? 28f / 200f : 22f / 200f;
        }

        private static float Side(PassiveNode a, PassiveNode b, PassiveNode c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        [Test]
        public void PassiveGlyphsHaveClearSpaceBetweenThem()
        {
            var nodes = PassiveTree.Nodes;
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    float dx = nodes[i].X - nodes[j].X, dy = nodes[i].Y - nodes[j].Y;
                    Assert.GreaterOrEqual(System.Math.Sqrt(dx * dx + dy * dy),
                        GlyphRadius(nodes[i]) + GlyphRadius(nodes[j]) + 0.05f,
                        nodes[i].Id + " overlaps " + nodes[j].Id);
                }
        }

        [Test]
        public void ConnectionsNeverCrossOrPassThroughAnotherPassive()
        {
            var edges = new List<PassiveNode[]>();
            foreach (PassiveNode a in PassiveTree.Nodes)
                foreach (string id in a.Links)
                    if (string.CompareOrdinal(a.Id, id) < 0)
                        edges.Add(new[] { a, PassiveTree.Get(id) });

            for (int i = 0; i < edges.Count; i++)
            {
                PassiveNode a = edges[i][0], b = edges[i][1];
                foreach (PassiveNode node in PassiveTree.Nodes)
                {
                    if (node == a || node == b) continue;
                    float dx = b.X - a.X, dy = b.Y - a.Y;
                    float t = System.Math.Max(0f, System.Math.Min(1f,
                        ((node.X - a.X) * dx + (node.Y - a.Y) * dy) / (dx * dx + dy * dy)));
                    float nx = node.X - a.X - t * dx, ny = node.Y - a.Y - t * dy;
                    Assert.GreaterOrEqual(System.Math.Sqrt(nx * nx + ny * ny), GlyphRadius(node) + 0.025f,
                        a.Id + " - " + b.Id + " passes through " + node.Id);
                }
                for (int j = i + 1; j < edges.Count; j++)
                {
                    PassiveNode c = edges[j][0], d = edges[j][1];
                    if (a == c || a == d || b == c || b == d) continue;
                    Assert.IsFalse(Side(a, b, c) * Side(a, b, d) < 0f &&
                        Side(c, d, a) * Side(c, d, b) < 0f,
                        a.Id + " - " + b.Id + " crosses " + c.Id + " - " + d.Id);
                }
            }
        }

        [Test]
        public void SpacingKeepsItsAverageWithoutLongOutliers()
        {
            double total = 0, squared = 0;
            int count = 0;
            foreach (PassiveNode a in PassiveTree.Nodes)
                foreach (string id in a.Links)
                {
                    if (string.CompareOrdinal(a.Id, id) >= 0) continue;
                    PassiveNode b = PassiveTree.Get(id);
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double length = System.Math.Sqrt(dx * dx + dy * dy);
                    Assert.LessOrEqual(length, 1.8, a.Id + " - " + b.Id + " is unusually long");
                    total += length;
                    squared += length * length;
                    count++;
                }
            double mean = total / count;
            Assert.That(mean, Is.InRange(0.80, 0.93));
            Assert.Less(System.Math.Sqrt(squared / count - mean * mean), 0.32);
        }

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
        public void TravelRewardsSplitBetweenAttributesAndBranchThemes()
        {
            int attributes = 0, themed = 0;
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                if (!node.Id.StartsWith("travel_"))
                    continue;
                Assert.AreEqual(1, node.Mods.Length, node.Id);
                StatModifier reward = node.Mods[0];
                if (reward.Stat == StatType.Strength || reward.Stat == StatType.Dexterity || reward.Stat == StatType.Intelligence)
                {
                    Assert.AreEqual(3f, reward.Value, node.Id);
                    attributes++;
                }
                else
                {
                    Assert.Greater(reward.Value, 0f, node.Id);
                    themed++;
                }
            }
            Assert.AreEqual(49, attributes);
            Assert.AreEqual(50, themed);
        }

        [Test]
        public void LongApproachesKeepOneSmallRewardThroughout()
        {
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                if (!node.Id.StartsWith("travel_") || !node.Id.EndsWith("_1"))
                    continue;
                string path = node.Id.Substring(0, node.Id.Length - 1);
                PassiveNode second = PassiveTree.Get(path + "2");
                if (second == null)
                    continue;
                Assert.AreEqual(node.Mods[0].Stat, second.Mods[0].Stat, path);
                Assert.AreEqual(node.Mods[0].Value, second.Mods[0].Value, path);
                PassiveNode third = PassiveTree.Get(path + "3");
                if (third == null)
                    continue;
                Assert.AreEqual(node.Mods[0].Stat, third.Mods[0].Stat, path);
                Assert.AreEqual(node.Mods[0].Value, third.Mods[0].Value, path);
            }
            Assert.AreEqual(StatType.MinionLife, PassiveTree.Get("travel_n2_k_legion_1").Mods[0].Stat);
            Assert.AreEqual(StatType.BowDamage, PassiveTree.Get("travel_g10_g_deadeye_1").Mods[0].Stat);
            Assert.AreEqual(StatType.FireResistance, PassiveTree.Get("travel_b_fireward_w_r3_1").Mods[0].Stat);
            Assert.AreEqual(StatType.MinionResistances, PassiveTree.Get("travel_b_fireward_n1_1").Mods[0].Stat);
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
        public void SmallLifePairsHaveAtLeastSixConnectionsBetweenThem()
        {
            var starts = new HashSet<string>();
            foreach (PassiveNode node in PassiveTree.Nodes)
            {
                if (!node.Notable && node.Mods.Length == 1 &&
                    node.Mods[0].Stat == StatType.IncreasedLife && node.Mods[0].Value == 3f)
                {
                    foreach (string link in node.Links)
                    {
                        PassiveNode end = PassiveTree.Get(link);
                        if (end.Notable && end.Mods.Length == 1 &&
                            end.Mods[0].Stat == StatType.IncreasedLife && end.Mods[0].Value == 6f)
                            starts.Add(node.Id);
                    }
                }
            }
            Assert.Greater(starts.Count, 1);
            foreach (string start in starts)
            {
                var seen = new HashSet<string> { start };
                var frontier = new List<string> { start };
                for (int distance = 1; distance < 6; distance++)
                {
                    var next = new List<string>();
                    foreach (string id in frontier)
                        foreach (string link in PassiveTree.Get(id).Links)
                            if (seen.Add(link))
                            {
                                Assert.IsFalse(starts.Contains(link), start + " is too close to " + link);
                                next.Add(link);
                            }
                    frontier = next;
                }
            }
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
