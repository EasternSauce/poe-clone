using System.Collections.Generic;
using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class SkillGearTests
    {
        private static List<StatModifier> Grants(ItemData item)
        {
            var grants = new List<StatModifier>();
            foreach (StatModifier m in item.Modifiers)
            {
                if (SkillGrants.IsGrant(m.Stat))
                    grants.Add(m);
            }
            return grants;
        }

        [Test]
        public void EveryStaff_CarriesAnAttackSpell_AtEveryRarity()
        {
            var rng = new System.Random(5);
            foreach (ItemRarity rarity in new[] { ItemRarity.Normal, ItemRarity.Magic, ItemRarity.Rare })
            {
                for (int k = 0; k < 100; k++)
                {
                    ItemData staff = ItemGenerator.Generate(rng, "gnarled_staff", 1 + k % 15, rarity);
                    Assert.IsTrue(SkillGrants.IsMain(Grants(staff)[0].Stat), staff.Name + " has no attack spell");
                }
            }
        }

        [Test]
        public void SkillLevels_StayBetweenOneAndTen_AndGrowWithItemLevel()
        {
            var rng = new System.Random(9);
            int lowSum = 0, highSum = 0;
            for (int k = 0; k < 400; k++)
            {
                int low = SkillGrants.RollLevel(rng, 1);
                int high = SkillGrants.RollLevel(rng, 15);
                Assert.AreEqual(1, low);
                Assert.That(high, Is.InRange(1, SkillGrants.MaxDropLevel));
                lowSum += low;
                highSum += high;
            }
            Assert.Greater(highSum, lowSum * 5);
        }

        [Test]
        public void Grants_OnlyRollWhereTheyFit()
        {
            var rng = new System.Random(21);
            for (int k = 0; k < 2000; k++)
            {
                ItemData item = ItemGenerator.Generate(rng, 12, ItemRarity.Rare);
                foreach (StatModifier m in Grants(item))
                {
                    if (m.Stat == StatType.GrantDash)
                        Assert.AreEqual(ItemType.Boots, item.Type);
                    if (m.Stat == StatType.GrantCleave)
                        Assert.IsTrue(item.Type == ItemType.Weapon && item.WeaponType != WeaponType.Bow && item.WeaponType != WeaponType.Staff,
                            "Cleave on " + item.Type + " " + item.WeaponType);
                    if (SkillGrants.IsMain(m.Stat))
                        Assert.AreEqual(WeaponType.Staff, item.WeaponType, m.Stat + " on " + item.Type);
                }
            }
        }

        [Test]
        public void StarterGear_HasAFireBoltStaff_AndDashBoots()
        {
            List<ItemData> items = ItemCatalog.CreateStarterItems();
            ItemData staff = items.Find(i => i.Type == ItemType.Weapon && i.WeaponType == WeaponType.Staff);
            ItemData boots = items.Find(i => i.Type == ItemType.Boots);
            Assert.IsNotNull(staff);
            Assert.AreEqual(StatType.GrantFireBolt, Grants(staff)[0].Stat);
            Assert.AreEqual(1f, Grants(staff)[0].Value);
            Assert.AreEqual(StatType.GrantDash, Grants(boots)[0].Stat);
        }

        [Test]
        public void Staff_TakesBothHands()
        {
            ItemData staff = ItemGenerator.Generate(new System.Random(1), "gnarled_staff", 1, ItemRarity.Normal);
            ItemData shield = ItemGenerator.Generate(new System.Random(1), "wooden_shield", 1, ItemRarity.Normal);
            Assert.IsFalse(SlotRules.HandsCompatible(staff, shield));
            Assert.IsTrue(SlotRules.HandsCompatible(staff, null));
        }

        [Test]
        public void GrantLines_ReadAsSkills()
        {
            Assert.AreEqual("Grants Level 3 Fire Bolt", StatFormatter.ItemLine(new StatModifier(StatType.GrantFireBolt, 3)));
            Assert.AreEqual("+1 to Level of all Fire Spells", StatFormatter.ItemLine(new StatModifier(StatType.FireSpellLevels, 1)));
        }
    }
}
