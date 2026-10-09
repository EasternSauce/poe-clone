using System;
using System.Collections.Generic;
using System.Linq;
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
                int high = SkillGrants.RollLevel(rng, 37);
                Assert.AreEqual(1, low);
                Assert.That(high, Is.InRange(1, SkillGrants.MaxDropLevel));
                lowSum += low;
                highSum += high;
            }
            Assert.Greater(highSum, lowSum * 2);
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
                    if (Array.IndexOf(SkillGrants.StaffMain, m.Stat) >= 0)
                        Assert.AreEqual(WeaponType.Staff, item.WeaponType, m.Stat + " on " + item.Type);
                    if (Array.IndexOf(SkillGrants.GrimoireMain, m.Stat) >= 0)
                        Assert.AreEqual(ItemType.Grimoire, item.Type, m.Stat + " on " + item.Type);
                    if (SkillGrants.IsSummon(m.Stat) && item.Type == ItemType.Weapon)
                        Assert.AreEqual(WeaponType.Sceptre, item.WeaponType, m.Stat + " on " + item.WeaponType);
                    if (SkillGrants.IsBowSkill(m.Stat))
                        Assert.IsTrue(item.Type == ItemType.Quiver || item.WeaponType == WeaponType.Bow, m.Stat + " on " + item.Type + " " + item.WeaponType);
                }
            }
        }

        [Test]
        public void EveryGrimoire_CarriesDeathMark()
        {
            var rng = new System.Random(5);
            int grimoires = 0;
            for (int k = 0; k < 3000; k++)
            {
                ItemData item = ItemGenerator.Generate(rng, 8, ItemRarity.Magic);
                if (item.Type != ItemType.Grimoire)
                    continue;
                grimoires++;
                Assert.AreEqual(StatType.GrantDeathMark, Grants(item)[0].Stat, item.Name);
            }
            Assert.Greater(grimoires, 10);
        }

        [Test]
        public void StarterGear_HasASceptre_WithRaiseSkeletons()
        {
            ItemData sceptre = ItemCatalog.CreateStarterItems().Find(i => i.Type == ItemType.Weapon && i.WeaponType == WeaponType.Sceptre);
            Assert.IsNotNull(sceptre);
            Assert.AreEqual(StatType.GrantRaiseSkeletons, Grants(sceptre)[0].Stat);
            Assert.IsFalse(SlotRules.IsTwoHanded(sceptre));
        }

        [Test]
        public void SummonLevels_ComeOnlyFromSummonStats()
        {
            Assert.AreEqual(StatType.RaiseSkeletonsLevels, SkillGrants.SummonLevelStat(StatType.GrantRaiseSkeletons));
            Assert.AreEqual(StatType.BoneGolemLevels, SkillGrants.SummonLevelStat(StatType.GrantBoneGolem));
            Assert.IsNull(SkillGrants.SummonLevelStat(StatType.GrantFireBolt));
            Assert.IsFalse(SkillGrants.IsSummon(StatType.GrantDeathMark));
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
        public void BowSkillLevels_RespectAreaTierGates()
        {
            var rng = new System.Random(3);
            int lowSum = 0, highSum = 0, lowTens = 0, highTens = 0;
            var seen = new HashSet<int>();
            for (int k = 0; k < 4000; k++)
            {
                int low = SkillGrants.RollBowLevel(rng, 1);
                int high = SkillGrants.RollBowLevel(rng, 37);
                Assert.That(low, Is.InRange(1, SkillGrants.MaxDropLevel));
                Assert.That(high, Is.InRange(1, SkillGrants.MaxDropLevel));
                seen.Add(low);
                lowSum += low;
                highSum += high;
                if (low == 10) lowTens++;
                if (high == 10) highTens++;
            }
            Assert.AreEqual(1, seen.Count, "level-one areas only grant rank-one skills");
            Assert.AreEqual(0, lowTens);
            Assert.Greater(highSum, lowSum * 1.6);
            Assert.Greater(highTens, lowTens * 2);
        }

        [Test]
        public void StarterBow_CarriesSplitShot_AndBowSkillsAreNotTheAttack()
        {
            ItemData bow = ItemCatalog.CreateStarterItems().Find(i => i.Type == ItemType.Weapon && i.WeaponType == WeaponType.Bow);
            Assert.IsNotNull(bow);
            Assert.AreEqual(StatType.GrantSplitShot, Grants(bow)[0].Stat);
            foreach (StatType grant in SkillGrants.BowSkills)
                Assert.IsFalse(SkillGrants.IsMain(grant), grant + " must not be an attack spell");
        }

        [Test]
        public void GrantLines_ReadAsSkills()
        {
            Assert.AreEqual("Grants Level 3 Fire Bolt", StatFormatter.ItemLine(new StatModifier(StatType.GrantFireBolt, 3)));
            Assert.AreEqual("+1 to Level of all Fire Spells", StatFormatter.ItemLine(new StatModifier(StatType.FireSpellLevels, 1)));
        }

        [TestCase("gnarled_staff", true)]
        [TestCase("bone_sceptre", true)]
        [TestCase("grimoire", true)]
        [TestCase("sapphire_ring", true)]
        [TestCase("jade_amulet", true)]
        [TestCase("silk_gloves", true)]
        [TestCase("arcane_gloves", true)]
        [TestCase("sage_circlet", true)]
        [TestCase("silk_robe", true)]
        [TestCase("rusty_sword", false)]
        [TestCase("short_bow", false)]
        [TestCase("leather_quiver", false)]
        [TestCase("wooden_shield", false)]
        [TestCase("silk_slippers", false)]
        [TestCase("rope_belt", false)]
        public void CastSpeed_RollsOnSuitableGear(string baseId, bool speedAllowed)
        {
            var rng = new System.Random(492);
            int speedRolls = 0;
            for (int k = 0; k < 3000; k++)
            {
                ItemData item = ItemGenerator.Generate(rng, baseId, 12, ItemRarity.Rare);
                Assert.LessOrEqual(item.Modifiers.Count(m => m.Stat == StatType.CastSpeed), 1,
                    "cast speed must only appear once per item");
                foreach (StatModifier mod in item.Modifiers)
                {
                    if (mod.Stat == StatType.CastSpeed)
                    {
                        speedRolls++;
                        Assert.That(mod.Value, Is.InRange(3f, 30f));
                        Assert.IsTrue(ItemGenerator.Legalize(item).Modifiers.Any(m =>
                            m.Stat == mod.Stat && m.Value == mod.Value), "save repair must preserve valid cast-speed rolls");
                    }
                }
            }
            Assert.AreEqual(speedAllowed, speedRolls > 0, baseId + " cast speed eligibility");
        }

        [TestCase("gnarled_staff", 6f, 18f)]
        [TestCase("bone_sceptre", 4f, 12f)]
        [TestCase("grimoire", 4f, 10f)]
        [TestCase("sapphire_ring", 4f, 10f)]
        [TestCase("jade_amulet", 4f, 10f)]
        [TestCase("silk_gloves", 4f, 10f)]
        [TestCase("sage_circlet", 3f, 8f)]
        [TestCase("silk_robe", 3f, 8f)]
        public void CastSpeed_UsesUnlockedItemLevelTiers(string baseId, float min, float max)
        {
            var rng = new System.Random(493);
            float lowSum = 0f, highSum = 0f;
            int lowCount = 0, highCount = 0;
            float topScale = ItemGenerator.RollTierScale(ItemGenerator.MaxRollTier(ItemGenerator.MaxItemLevel));
            for (int k = 0; k < 3000; k++)
            {
                foreach (int level in new[] { 1, ItemGenerator.MaxItemLevel })
                {
                    ItemData item = ItemGenerator.Generate(rng, baseId, level, ItemRarity.Rare);
                    foreach (StatModifier mod in item.Modifiers.Where(m => m.Stat == StatType.CastSpeed))
                    {
                        float minScale = 1f;
                        float maxScale = level == 1 ? 1f : topScale;
                        Assert.That(mod.Value, Is.InRange((float)Math.Round(min * minScale), (float)Math.Round(max * maxScale)));
                        Assert.IsTrue(ItemGenerator.Legalize(item).Modifiers.Any(m => m.Stat == mod.Stat && m.Value == mod.Value));
                        if (level == 1) { lowSum += mod.Value; lowCount++; }
                        else { highSum += mod.Value; highCount++; }
                    }
                }
            }
            Assert.Greater(lowCount, 0);
            Assert.Greater(highCount, 0);
            Assert.Greater(highSum / highCount, lowSum / lowCount * 1.25f,
                "high-level areas should naturally produce better cast speed rolls");
        }

        [Test]
        public void CastSpeed_AddsAllSources()
        {
            var sheet = StatSheet.Build(new BaseStats().Set(StatType.CastSpeed, 10), null, new[]
            {
                new StatModifier(StatType.CastSpeed, 40),
                new StatModifier(StatType.CastSpeed, 20),
                new StatModifier(StatType.CastSpeed, 20)
            });
            Assert.AreEqual(90f, sheet.Total(StatType.CastSpeed), 0.0001f);
            Assert.AreEqual(1.9f, sheet.CastRateMultiplier, 0.0001f);
            Assert.AreEqual(1f, StatSheet.Build(new BaseStats(), null).CastRateMultiplier);
            Assert.AreEqual("24% increased Cast Speed", StatFormatter.ItemLine(new StatModifier(StatType.CastSpeed, 24)));
            Assert.AreEqual("24%", StatFormatter.Value(StatType.CastSpeed, 24));
        }

        [Test]
        public void StrongCastSpeedUniques_AreInOrdinaryDropPool()
        {
            var names = new HashSet<string>
            {
                "The Unfinished Sentence", "Spellweaver's Hands", "Pendant of the Fleeting Thought"
            };
            foreach (string name in names)
            {
                ItemData item = UniqueItems.Current(name);
                Assert.IsNotNull(item, name);
                Assert.AreEqual(1, item.Modifiers.Count(m => m.Stat == StatType.CastSpeed), name);
                Assert.GreaterOrEqual(item.Modifiers.Single(m => m.Stat == StatType.CastSpeed).Value, 12f, name);
            }
            var rng = new System.Random(31);
            for (int k = 0; k < 20000 && names.Count > 0; k++)
                names.Remove(UniqueItems.Random(rng).Name);
            Assert.IsEmpty(names, "new cast-speed uniques must drop outside the Shepherd reward pool");
        }
    }
}
