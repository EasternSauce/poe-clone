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

        [Test]
        public void BloodrootTradesRawRecoveryForPotionRecovery()
        {
            ItemData belt = UniqueItems.Current("Bloodroot Cord");
            Assert.IsNotNull(belt);
            Assert.AreEqual(22f, Value(belt, StatType.MaxLife));
            Assert.AreEqual(3f, Value(belt, StatType.LifeRegen));
            Assert.AreEqual(25f, Value(belt, StatType.HealthPotionRecovery));
            Assert.AreEqual(0f, ValueOrZero(belt, StatType.OnslaughtOnHealthPotion));
            Assert.AreEqual("25% increased Life recovered by Health Potions",
                StatFormatter.ItemLine(new StatModifier(StatType.HealthPotionRecovery, 25)));
        }

        [Test]
        public void NewOrdinaryUniquesUseExistingTreeEffects()
        {
            Assert.AreEqual(StatType.Shatter, Signature("Frostglass Bough"));
            Assert.AreEqual(StatType.Stormblade, Signature("Thundergrip"));
            Assert.AreEqual(StatType.GlacialStep, Signature("Winter's Passage"));
            Assert.AreEqual(StatType.SecondWind, Signature("Mercy's Echo"));
            Assert.AreEqual(StatType.DeathsHerald, Signature("Ledger of the Fallen"));
            ItemData draught = UniqueItems.Current("The Last Draught");
            Assert.AreEqual(1f, Value(draught, StatType.OnslaughtOnHealthPotion));
            Assert.AreEqual(0f, ValueOrZero(draught, StatType.HealthPotionRecovery));
        }

        [Test]
        public void PassiveTreeHasNoSingleAbilityBonuses()
        {
            var singleAbilityStats = new[]
            {
                StatType.AdditionalChains, StatType.MarkEffect, StatType.GlacialStep,
                StatType.SecondWind, StatType.DeathsHerald, StatType.RaiseSkeletonsLevels,
                StatType.SkeletonMagesLevels, StatType.SpiritWolvesLevels, StatType.BoneGolemLevels
            };
            foreach (PassiveNode node in PassiveTree.Nodes)
                foreach (StatModifier modifier in node.Mods)
                {
                    Assert.IsFalse(SkillGrants.IsGrant(modifier.Stat), node.Id);
                    Assert.IsFalse(System.Array.IndexOf(singleAbilityStats, modifier.Stat) >= 0,
                        node.Id + " only benefits one ability: " + modifier.Stat);
                }
        }

        [Test]
        public void SingleAbilityBonusesAreAvailableOnUniquesWithTheirSkill()
        {
            ItemData staff = UniqueItems.Current("Conductor's Reach");
            Assert.IsNotNull(staff);
            Assert.AreEqual(6f, Value(staff, StatType.GrantChainLightning));
            Assert.AreEqual(1f, Value(staff, StatType.AdditionalChains));

            ItemData amulet = UniqueItems.Current("Death's Grip");
            Assert.IsNotNull(amulet);
            Assert.AreEqual(6f, Value(amulet, StatType.GrantDeathMark));
            Assert.AreEqual(20f, Value(amulet, StatType.MarkEffect));

            var gear = new EquipmentSet();
            Assert.IsTrue(gear.TryEquip(EquipSlot.MainHand, staff, out _));
            Assert.IsTrue(gear.TryEquip(EquipSlot.Amulet, amulet, out _));
            StatSheet sheet = StatSheet.Build(new BaseStats(), gear);
            Assert.AreEqual(1f, sheet.Total(StatType.AdditionalChains));
            Assert.AreEqual(20f, sheet.Total(StatType.MarkEffect));
        }

        private static StatType Signature(string name)
        {
            ItemData item = UniqueItems.Current(name);
            Assert.IsNotNull(item, name);
            foreach (StatModifier modifier in item.Modifiers)
                if (modifier.Stat == StatType.Shatter || modifier.Stat == StatType.Stormblade ||
                    modifier.Stat == StatType.GlacialStep || modifier.Stat == StatType.SecondWind ||
                    modifier.Stat == StatType.DeathsHerald)
                    return modifier.Stat;
            Assert.Fail(name + " has no tree effect");
            return default(StatType);
        }

        private static float Value(ItemData item, StatType stat)
        {
            foreach (StatModifier modifier in item.Modifiers)
                if (modifier.Stat == stat)
                    return modifier.Value;
            Assert.Fail(item.Name + " lacks " + stat);
            return 0f;
        }

        private static float ValueOrZero(ItemData item, StatType stat)
        {
            foreach (StatModifier modifier in item.Modifiers)
                if (modifier.Stat == stat)
                    return modifier.Value;
            return 0f;
        }
    }
}
