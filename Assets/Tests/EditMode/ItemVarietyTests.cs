using System.Collections.Generic;
using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class ItemVarietyTests
    {
        private static readonly string[] Slots = { "helm", "body", "glove", "boot", "belt", "amu", "ring", "shield" };

        [Test]
        public void EverySlot_HasALineWithFourTiers()
        {
            foreach (string slot in Slots)
            {
                var levelsByLine = new Dictionary<string, HashSet<int>>();
                foreach (string id in ItemGenerator.BaseIds)
                {
                    string line = ItemGenerator.LineOf(id);
                    if (!line.StartsWith(slot + "_"))
                        continue;
                    if (!levelsByLine.TryGetValue(line, out HashSet<int> levels))
                        levelsByLine[line] = levels = new HashSet<int>();
                    levels.Add(ItemGenerator.MinLevelOf(id));
                }

                bool found = false;
                foreach (HashSet<int> levels in levelsByLine.Values)
                    found |= levels.Count >= 4;
                Assert.IsTrue(found, slot + " has no four-tier line");
            }
        }

        [Test]
        public void DeeperDrops_FavourTheNewestTier()
        {
            Dictionary<string, float> deep = ItemGenerator.DropChances(13);
            Assert.Greater(deep["warlord_plate"], deep["chain_hauberk"] * 5f);
            Assert.Greater(deep["titan_maul"], deep["great_mallet"] * 5f);

            Dictionary<string, float> shallow = ItemGenerator.DropChances(1);
            Assert.IsFalse(shallow.ContainsKey("warlord_plate"));
            Assert.Greater(shallow["chain_hauberk"], 0f);
        }

        [Test]
        public void CasterBases_RollCasterStatsMoreOften()
        {
            var rng = new System.Random(5);
            int robeCaster = 0, plateCaster = 0;
            for (int k = 0; k < 400; k++)
            {
                robeCaster += CasterStats(ItemGenerator.Generate(rng, "silk_robe", 8, ItemRarity.Rare));
                plateCaster += CasterStats(ItemGenerator.Generate(rng, "chain_hauberk", 8, ItemRarity.Rare));
            }
            Assert.Greater(robeCaster, plateCaster * 2);
        }

        private static int CasterStats(ItemData item)
        {
            // Only the random stats: the robe's own base stats are caster stats too.
            ItemData plain = ItemGenerator.Generate(new System.Random(1), item.Id, 8, ItemRarity.Normal);
            int n = 0;
            for (int k = plain.Modifiers.Count; k < item.Modifiers.Count; k++)
            {
                StatType s = item.Modifiers[k].Stat;
                if (s == StatType.Intelligence || s == StatType.MaxMana)
                    n++;
            }
            return n;
        }

        [Test]
        public void GreatWeapons_TakeBothHands()
        {
            ItemData maul = ItemGenerator.Generate(new System.Random(1), "great_mallet", 1, ItemRarity.Normal);
            ItemData shield = ItemGenerator.Generate(new System.Random(1), "wooden_shield", 1, ItemRarity.Normal);
            ItemData sword = ItemGenerator.Generate(new System.Random(1), "rusty_sword", 1, ItemRarity.Normal);

            Assert.AreEqual(WeaponType.Maul, maul.WeaponType);
            Assert.IsTrue(SlotRules.IsTwoHanded(maul));
            Assert.IsFalse(SlotRules.HandsCompatible(maul, shield));
            Assert.IsTrue(SlotRules.HandsCompatible(sword, shield));

            var equipment = new EquipmentSet();
            Assert.IsTrue(equipment.TryEquip(EquipSlot.MainHand, maul, out _));
            Assert.IsFalse(equipment.CanEquip(EquipSlot.OffHand, shield));
        }

        [Test]
        public void GreatWeapons_HitMuchHarderThanOneHandersOfTheirTier()
        {
            // They swing about 30% slower (CharacterAttackAnimator), so they need well over that in damage.
            Assert.Greater(PhysicalOf("bastard_sword"), PhysicalOf("rusty_sword") * 1.6f);
            Assert.Greater(PhysicalOf("titan_maul"), PhysicalOf("templar_mace") * 1.5f);
            Assert.Greater(PhysicalOf("executioner_axe"), PhysicalOf("butcher_axe") * 1.5f);
        }

        private static float PhysicalOf(string id)
        {
            ItemData item = ItemGenerator.Display(id, null, ItemRarity.Normal);
            ItemData rolled = ItemGenerator.Generate(new System.Random(1), id, 1, ItemRarity.Normal);
            foreach (StatModifier m in rolled.Modifiers)
            {
                if (m.Stat == StatType.PhysicalDamage)
                    return m.Value;
            }
            Assert.Fail(item.Name + " has no physical damage");
            return 0f;
        }
    }
}
