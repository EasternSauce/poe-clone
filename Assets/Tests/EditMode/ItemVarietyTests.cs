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
        public void StrengthBases_RollStrengthMoreOften()
        {
            var rng = new System.Random(5);
            int plate = 0, robe = 0;
            for (int k = 0; k < 400; k++)
            {
                plate += Count(ItemGenerator.Generate(rng, "chain_hauberk", 8, ItemRarity.Rare), StatType.Strength);
                robe += Count(ItemGenerator.Generate(rng, "silk_robe", 8, ItemRarity.Rare), StatType.Strength);
            }
            Assert.Greater(plate, robe * 2);
        }

        [Test]
        public void AStatTheBaseHas_NeverRollsAgain()
        {
            var rng = new System.Random(9);
            for (int k = 0; k < 300; k++)
            {
                Assert.AreEqual(1, Count(ItemGenerator.Generate(rng, "iron_helmet", 12, ItemRarity.Rare), StatType.Armour));
                ItemData any = ItemGenerator.Generate(rng, 12, ItemRarity.Rare);
                var seen = new HashSet<StatType>();
                foreach (StatModifier m in any.Modifiers)
                    Assert.IsTrue(seen.Add(m.Stat), any.Name + " has two " + m.Stat + " lines");
            }
        }

        [Test]
        public void FreshItems_AreAlreadyLegal()
        {
            var rng = new System.Random(21);
            for (int k = 0; k < 2000; k++)
            {
                ItemData item = ItemGenerator.Generate(rng, 1 + k % ItemGenerator.MaxItemLevel, ItemRarity.Rare);
                Assert.AreSame(item, ItemGenerator.Legalize(item), item.Name + " (" + item.Id + ") was changed");
            }
            foreach (ItemData starter in ItemCatalog.CreateStarterItems())
                Assert.AreSame(starter, ItemGenerator.Legalize(starter), starter.Name);
        }

        [Test]
        public void OutdatedItems_AreFixedOnLoad()
        {
            // An old plate helm: Armour above today's range, a second Armour line, Spell Damage
            // (never on helmets) and a skill at level 14.
            var old = new ItemData("iron_helmet", "Old Helm", ItemType.Helmet, 2, 2, UnityEngine.Color.white, new[]
            {
                new StatModifier(StatType.Armour, 500f),
                new StatModifier(StatType.Armour, 30f),
                new StatModifier(StatType.SpellDamage, 40f),
                new StatModifier(StatType.GrantFrostNova, 14f),
            }, rarity: ItemRarity.Rare);

            ItemData loaded = ItemRecord.From(old).ToItem();

            Assert.AreEqual(1, Count(loaded, StatType.Armour));
            Assert.LessOrEqual(loaded.Modifiers[0].Value, 22f);
            Assert.AreEqual(0, Count(loaded, StatType.SpellDamage));
            Assert.AreEqual(1, Count(loaded, StatType.GrantFrostNova));
            foreach (StatModifier m in loaded.Modifiers)
            {
                if (m.Stat == StatType.GrantFrostNova)
                    Assert.AreEqual(10f, m.Value);
            }
        }

        private static int Count(ItemData item, StatType stat)
        {
            int n = 0;
            foreach (StatModifier m in item.Modifiers)
            {
                if (m.Stat == stat)
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
