using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Network.Replication;

namespace PoeClone.Tests
{
    public class DefenceMathTests
    {
        [Test]
        public void Evasion_SixHundredGivesHalf_AndIsCapped()
        {
            Assert.AreEqual(0f, DefenceMath.EvadeChance(0f));
            Assert.AreEqual(0.5f, DefenceMath.EvadeChance(600f), 1e-4f);
            Assert.AreEqual(DefenceMath.MaxEvadeChance, DefenceMath.EvadeChance(100000f), 1e-4f);
        }

        [Test]
        public void Armour_StopsMoreOfSmallHitsThanBigOnes_WithDiminishingReturns()
        {
            float small = DefenceMath.ArmourReduction(120f, 5f);
            float big = DefenceMath.ArmourReduction(120f, 30f);
            Assert.Greater(small, big);
            Assert.AreEqual(0f, DefenceMath.ArmourReduction(0f, 10f));
            float extreme = DefenceMath.ArmourReduction(1e7f, 1f);
            Assert.Greater(extreme, 0.99f);
            Assert.Less(extreme, 1f);
            Assert.AreEqual(0.5f, DefenceMath.ArmourReduction(200f, 10f), 1e-4f);
        }

        [Test]
        public void Stagger_ScalesWithLifeLost_AndAvoidStun()
        {
            Assert.AreEqual(0f, DefenceMath.StaggerChance(5f, 100f, 0f));
            Assert.AreEqual(0.5f, DefenceMath.StaggerChance(20f, 100f, 0f), 1e-4f);
            Assert.AreEqual(0f, DefenceMath.StaggerChance(20f, 400f, 0f));
            Assert.AreEqual(1f, DefenceMath.StaggerChance(35f, 100f, 0f));
            Assert.AreEqual(0.25f, DefenceMath.StaggerChance(20f, 100f, 50f), 1e-4f);
            Assert.AreEqual(0f, DefenceMath.StaggerChance(20f, 100f, 100f));
            Assert.AreEqual(0f, DefenceMath.StaggerChance(0f, 100f, 0f));
            float mitigatedHit = 18f * (1f - DefenceMath.ArmourReduction(200f, 18f));
            Assert.Less(DefenceMath.StaggerChance(mitigatedHit, 100f, 0f),
                DefenceMath.StaggerChance(18f, 100f, 0f));
        }

        [Test]
        public void Block_And_Resistance_UseTheSheetCaps()
        {
            Assert.AreEqual(0.2f, DefenceMath.BlockChance(20f), 1e-4f);
            Assert.AreEqual(StatSheet.BlockCap / 100f, DefenceMath.BlockChance(500f), 1e-4f);
            Assert.AreEqual(50f, DefenceMath.AfterResistance(100f, 50f), 1e-3f);
            Assert.AreEqual(100f - StatSheet.ResistanceCap, DefenceMath.AfterResistance(100f, 300f), 1e-3f);
        }

        [Test]
        public void Mana_SoaksAShareOfHits_OnlyWhileThereIsMana()
        {
            Assert.AreEqual(10f * DefenceMath.ManaAbsorbShare, DefenceMath.ManaAbsorbed(10f, 50f), 1e-4f);
            Assert.AreEqual(1f, DefenceMath.ManaAbsorbed(10f, 1f), 1e-4f);
            Assert.AreEqual(0f, DefenceMath.ManaAbsorbed(10f, 0f));
        }

        [Test]
        public void ManaRegen_GrowsWithPoolAndIntelligence()
        {
            float baseRegen = DefenceMath.ManaRegenPerSecond(50f, 10f);
            Assert.Greater(DefenceMath.ManaRegenPerSecond(100f, 10f), baseRegen);
            Assert.Greater(DefenceMath.ManaRegenPerSecond(50f, 40f), baseRegen);
        }
    }

    public class ItemGeneratorTests
    {
        private static int ExtraStats(ItemData item)
        {
            ItemData plain = ItemGenerator.Generate(new System.Random(1), item.Id, 1, ItemRarity.Normal);
            return item.Modifiers.Count - plain.Modifiers.Count;
        }

        [Test]
        public void EveryBase_HasAnIcon_AndA3DLook()
        {
            foreach (string id in ItemGenerator.BaseIds)
            {
                string art = ItemGenerator.Display(id, null, ItemRarity.Normal).ArtId;
                Assert.IsNotNull(Resources.Load<Sprite>("ItemIcons/" + art), "no icon for " + id);
                Assert.IsNotNull(Resources.Load<GameObject>("Equipment/" + ItemGenerator.ModelFor(art, out _)), "no 3D look for " + id);
            }
        }

        [Test]
        public void Rarity_DecidesHowManyExtraStatsRoll()
        {
            var rng = new System.Random(7);
            for (int k = 0; k < 200; k++)
            {
                ItemData normal = ItemGenerator.Generate(rng, 5, ItemRarity.Normal);
                Assert.AreEqual(0, ExtraStats(normal), normal.Name);
                Assert.AreEqual(ItemRarity.Normal, normal.Rarity);

                ItemData magic = ItemGenerator.Generate(rng, 5, ItemRarity.Magic);
                Assert.That(ExtraStats(magic), Is.InRange(1, 2), magic.Name);

                ItemData rare = ItemGenerator.Generate(rng, 5, ItemRarity.Rare);
                Assert.That(ExtraStats(rare), Is.InRange(3, 4), rare.Name);
                Assert.AreEqual(2, rare.Name.Split(' ').Length, "rares get a two-word name: " + rare.Name);
            }
        }

        [Test]
        public void ExtraStats_NeverRepeat_AndOnlyFitTheGear()
        {
            var rng = new System.Random(11);
            for (int k = 0; k < 300; k++)
            {
                ItemData item = ItemGenerator.Generate(rng, 10, ItemRarity.Rare);
                ItemData plain = ItemGenerator.Generate(new System.Random(1), item.Id, 1, ItemRarity.Normal);

                var extra = new HashSet<StatType>();
                for (int m = plain.Modifiers.Count; m < item.Modifiers.Count; m++)
                {
                    StatModifier mod = item.Modifiers[m];
                    Assert.IsTrue(extra.Add(mod.Stat), item.Name + " rolled " + mod.Stat + " twice");
                    Assert.Greater(mod.Value, 0f);

                    if (mod.Stat == StatType.MovementSpeed)
                        Assert.AreEqual(ItemType.Boots, item.Type, "movement speed on " + item.Type);
                    if (mod.Stat == StatType.BlockChance)
                        Assert.AreEqual(ItemType.Shield, item.Type, "block on " + item.Type);
                }
            }
        }

        [Test]
        public void HigherItemLevel_RollsStrongerFlatStats()
        {
            float low = 0f, high = 0f;
            var rngLow = new System.Random(3);
            var rngHigh = new System.Random(3);
            for (int k = 0; k < 300; k++)
            {
                // A ring: life is one of its random stats (a belt's life is its base stat, which never rolls again).
                low += SumLife(ItemGenerator.Generate(rngLow, "iron_ring", 1, ItemRarity.Rare));
                high += SumLife(ItemGenerator.Generate(rngHigh, "iron_ring", 30, ItemRarity.Rare));
            }
            Assert.Greater(high, low);
        }

        private static float SumLife(ItemData item)
        {
            float sum = 0f;
            foreach (StatModifier m in item.Modifiers)
            {
                if (m.Stat == StatType.MaxLife)
                    sum += m.Value;
            }
            return sum;
        }

        [Test]
        public void Weapons_KeepTheirWeaponType()
        {
            Assert.AreEqual(WeaponType.Bow, ItemGenerator.Generate(new System.Random(1), "short_bow", 1, ItemRarity.Magic).WeaponType);
            Assert.AreEqual(WeaponType.Dagger, ItemGenerator.Display("steel_dagger", "x", ItemRarity.Rare).WeaponType);
        }

        [Test]
        public void Display_KeepsNameAndRarity_AndRejectsUnknownBases()
        {
            ItemData shown = ItemGenerator.Display("iron_ring", "Doom Loop", ItemRarity.Rare);
            Assert.AreEqual("Doom Loop", shown.Name);
            Assert.AreEqual(ItemRarity.Rare, shown.Rarity);
            Assert.AreEqual("iron_ring", shown.Id);
            Assert.IsNull(ItemGenerator.Display("no_such_item", "x", ItemRarity.Normal));
        }

        [Test]
        public void RollRarity_IsMostlyNormal_SometimesRare()
        {
            var rng = new System.Random(5);
            var counts = new Dictionary<ItemRarity, int> { { ItemRarity.Normal, 0 }, { ItemRarity.Magic, 0 }, { ItemRarity.Rare, 0 } };
            for (int k = 0; k < 10000; k++)
                counts[ItemGenerator.RollRarity(rng)]++;

            Assert.That(counts[ItemRarity.Normal], Is.InRange(4500, 5500));
            Assert.That(counts[ItemRarity.Magic], Is.InRange(3000, 4000));
            Assert.That(counts[ItemRarity.Rare], Is.InRange(1200, 1800));
        }

        [Test]
        public void Rarity_DefaultsToMagicWithStats_NormalWithout()
        {
            var mods = new[] { new StatModifier(StatType.Armour, 5) };
            Assert.AreEqual(ItemRarity.Magic, new ItemData("x", "Stat Ring", ItemType.Ring, 1, 1, Color.white, mods).Rarity);
            Assert.AreEqual(ItemRarity.Normal, new ItemData("Plain", ItemType.Ring, 1, 1, Color.white).Rarity);
        }
    }

    public class LootReplicationTests
    {
        [Test]
        public void EnemyKind_AndLoot_SurviveTheWire()
        {
            var s = new StateSnapshot
            {
                seq = 1,
                p = new EntityState(),
                hud = new PlayerHudState { lv = 1 },
                e = new[] { new EntityState { i = 3, k = 5, mhp = 26f } },
                l = new[] { new LootState { i = 9, x = 1.5f, y = 0.02f, z = -4f, b = "short_bow", n = "Grim \"Song\"", q = 2 } }
            };

            StateSnapshot back = SnapshotCodec.Deserialize(SnapshotCodec.Serialize(s));

            Assert.AreEqual(5, back.e[0].k);
            Assert.AreEqual(1, back.l.Length);
            Assert.AreEqual(9, back.l[0].i);
            Assert.AreEqual(1.5f, back.l[0].x, 1e-3f);
            Assert.AreEqual(-4f, back.l[0].z, 1e-3f);
            Assert.AreEqual("short_bow", back.l[0].b);
            Assert.AreEqual("Grim \"Song\"", back.l[0].n);
            Assert.AreEqual(2, back.l[0].q);
        }

        [Test]
        public void NoLoot_StaysOffTheWire_AndReadsBackEmpty()
        {
            var s = new StateSnapshot { seq = 1, p = new EntityState(), hud = new PlayerHudState(), e = new EntityState[0] };
            string json = SnapshotCodec.Serialize(s);

            StringAssert.DoesNotContain("\"l\":", json);
            Assert.AreEqual(0, SnapshotCodec.Deserialize(json).l.Length);
        }
    }
}
