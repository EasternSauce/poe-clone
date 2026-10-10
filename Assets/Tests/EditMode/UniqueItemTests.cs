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
                    new StatModifier(StatType.MaxLife, 30, 3),
                    new StatModifier(StatType.LifeLeech, 99),
                    new StatModifier(StatType.AdditionalArrows, 4)
                }, rarity: ItemRarity.Unique);
            ItemData current = ItemGenerator.Legalize(old);
            Assert.AreEqual(7f, Value(current, StatType.PhysicalDamage));
            Assert.AreEqual(30f, Value(current, StatType.MaxLife));
            Assert.AreEqual(10f, Value(current, StatType.Strength));
            Assert.AreEqual(2.5f, Value(current, StatType.LifePercentOnKill));
            Assert.AreEqual(0f, ValueOrZero(current, StatType.AttackSpeed));
            Assert.AreEqual(0f, ValueOrZero(current, StatType.LifeLeech));
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
            Assert.That(Value(belt, StatType.MaxLife), Is.InRange(50f, 70f));
            Assert.That(Value(belt, StatType.HealthPotionRecovery), Is.InRange(20f, 30f));
            Assert.AreEqual(1f, Value(belt, StatType.PercentLifeRegen));
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
        }

        [Test]
        public void EveryUniqueHasArchetypeStatsAndOneOrTwoSignatures()
        {
            for (int k = 0; k < UniqueItems.Count; k++)
                Assert.That(UniqueItems.Create(k).Modifiers.Count, Is.InRange(4, 7), UniqueItems.Create(k).Name);
        }

        [Test]
        public void UniquesNeverRequireLessThanTheirBaseAndSpanEveryAreaLevel()
        {
            var levels = new System.Collections.Generic.List<int>();
            for (int k = 0; k < UniqueItems.Count; k++)
            {
                ItemData item = UniqueItems.Create(k);
                int baseLevel = ItemGenerator.RequirementsOf(ItemGenerator.Display(item.Id, null, ItemRarity.Normal)).Level;
                int uniqueLevel = ItemGenerator.RequirementsOf(item).Level;
                Assert.GreaterOrEqual(uniqueLevel, baseLevel, item.Name);
                Assert.AreEqual(uniqueLevel, UniqueItems.RequiredLevelFor(item.Name), item.Name);
                if (UniqueItems.CanDrop(k, 100)) levels.Add(uniqueLevel);
            }
            foreach (int area in new[] { 1, 9, 17, 25, 30 })
                Assert.IsTrue(levels.Exists(l => l >= area && l <= area + 4), "no unique starts dropping around level " + area);
        }

        [Test]
        public void UniquesOnlyDropWhereTheAreaLevelReachesThem()
        {
            var rng = new System.Random(5);
            foreach (int area in new[] { 1, 9, 17, 25, 34 })
                for (int k = 0; k < 400; k++)
                {
                    ItemData item = UniqueItems.Random(rng, area);
                    Assert.LessOrEqual(UniqueItems.RequiredLevelFor(item.Name), area, item.Name + " at area level " + area);
                }
        }

        [Test]
        public void StrongerUniquesDropLessOften()
        {
            var rng = new System.Random(9);
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            for (int k = 0; k < 40000; k++)
            {
                string name = UniqueItems.Random(rng, 34).Name;
                counts[name] = counts.TryGetValue(name, out int n) ? n + 1 : 1;
            }
            counts.TryGetValue("The Glass Diadem", out int diadem);
            Assert.Greater(diadem, 0);
            Assert.Less(diadem, counts["Bonehew"] / 5);
        }

        [Test]
        public void UniqueEffectsDescribeThemselves()
        {
            Assert.AreEqual("You take 10% less Damage", StatFormatter.ItemLine(new StatModifier(StatType.DamageTaken, -10)));
            Assert.AreEqual("You take 15% more Damage", StatFormatter.ItemLine(new StatModifier(StatType.DamageTaken, 15)));
            Assert.AreEqual("40% less Maximum Life", StatFormatter.ItemLine(new StatModifier(StatType.MoreLife, -40)));
            Assert.AreEqual("Attacks deal Fire damage instead of Physical", StatFormatter.ItemLine(new StatModifier(StatType.PhysicalToFire, 1)));
            Assert.IsTrue(StatFormatter.IsSpecial(StatType.SpellEcho));
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
            Assert.That(Value(staff, StatType.GrantChainLightning), Is.InRange(7f, 8f));
            Assert.AreEqual(2f, Value(staff, StatType.AdditionalChains));

            ItemData ledger = UniqueItems.Current("Ledger of the Fallen");
            Assert.IsNotNull(ledger);
            Assert.That(Value(ledger, StatType.GrantDeathMark), Is.InRange(5f, 6f));
            Assert.That(Value(ledger, StatType.MarkEffect), Is.InRange(25f, 35f));

            var gear = new EquipmentSet();
            Assert.IsTrue(gear.TryEquip(EquipSlot.MainHand, staff, out _));
            StatSheet sheet = StatSheet.Build(new BaseStats(), gear);
            Assert.AreEqual(2f, sheet.Total(StatType.AdditionalChains));
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
