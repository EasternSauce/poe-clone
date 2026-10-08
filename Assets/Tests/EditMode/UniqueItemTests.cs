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
        public void EveryUniqueRollsDifferentStatsAndKeepsThemThroughRepeatedJsonLoads()
        {
            var rng = new System.Random(731);
            for (int k = 0; k < UniqueItems.Count; k++)
            {
                ItemData first = UniqueItems.Create(k, rng);
                first.ItemLevel = 37;
                var changedStats = new System.Collections.Generic.HashSet<StatType>();
                for (int roll = 0; roll < 32; roll++)
                {
                    ItemData next = UniqueItems.Create(k, rng);
                    for (int m = 0; m < first.Modifiers.Count; m++)
                        if (first.Modifiers[m].Value != next.Modifiers[m].Value)
                            changedStats.Add(first.Modifiers[m].Stat);
                }
                Assert.GreaterOrEqual(changedStats.Count, 2, first.Name);
                ItemData loaded = first;
                for (int load = 0; load < 3; load++)
                {
                    loaded = UnityEngine.JsonUtility.FromJson<ItemRecord>(
                        UnityEngine.JsonUtility.ToJson(ItemRecord.From(loaded))).ToItem();
                    Assert.AreEqual(37, loaded.ItemLevel, first.Name);
                    for (int m = 0; m < first.Modifiers.Count; m++)
                    {
                        Assert.AreEqual(first.Modifiers[m].Stat, loaded.Modifiers[m].Stat, first.Name);
                        Assert.AreEqual(first.Modifiers[m].Value, loaded.Modifiers[m].Value, first.Name);
                        Assert.AreEqual(0, loaded.Modifiers[m].Tier, first.Name);
                    }
                }
            }
        }

        [Test]
        public void UniqueMigrationClampsOldValuesWithoutRerolling()
        {
            var old = new ItemData("hand_axe", "Bonehew", ItemType.Weapon, 1, 3,
                UnityEngine.Color.white, new[]
                {
                    new StatModifier(StatType.PhysicalDamage, 999, 1),
                    new StatModifier(StatType.AttackSpeed, 1, 2),
                    new StatModifier(StatType.MaxLife, 21, 3),
                    new StatModifier(StatType.LifeLeech, 99),
                    new StatModifier(StatType.AdditionalArrows, 4)
                }, rarity: ItemRarity.Unique);
            ItemData current = ItemGenerator.Legalize(old);
            Assert.AreEqual(19f, Value(current, StatType.PhysicalDamage));
            Assert.AreEqual(8f, Value(current, StatType.AttackSpeed));
            Assert.AreEqual(21f, Value(current, StatType.MaxLife));
            Assert.AreEqual(3f, Value(current, StatType.LifeLeech));
            Assert.AreEqual(10f, Value(current, StatType.ArmourPenetration));
            Assert.AreEqual(0f, ValueOrZero(current, StatType.AdditionalArrows));
        }

        [Test]
        public void UniqueTooltipsOmitAllModifierTierLabels()
        {
            var item = new ItemData("hand_axe", "Bonehew", ItemType.Weapon, 1, 3,
                UnityEngine.Color.white, new[] { new StatModifier(StatType.MaxLife, 20, 1) },
                rarity: ItemRarity.Unique);
            string tooltip = InventoryUI.BuildTooltipText(item, out _);
            StringAssert.DoesNotContain("[T1]", tooltip);
            StringAssert.DoesNotContain("[Unique]", tooltip);
            StringAssert.DoesNotContain("[Tier unknown]", tooltip);
            StringAssert.Contains("+20", tooltip);
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
            Assert.That(Value(belt, StatType.MaxLife), Is.InRange(18f, 26f));
            Assert.That(Value(belt, StatType.LifeRegen), Is.InRange(2f, 4f));
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
