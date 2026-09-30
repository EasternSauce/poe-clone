using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class StatSheetTests
    {
        private EquipmentSet equipment;
        private BaseStats baseStats;

        private static ItemData Item(ItemType type, params StatModifier[] mods)
        {
            return new ItemData(type + "_" + Guid.NewGuid(), "Test " + type, type, 1, 1, Color.white, mods);
        }

        private static StatModifier Mod(StatType stat, float value)
        {
            return new StatModifier(stat, value);
        }

        private StatSheet Sheet()
        {
            return StatSheet.Build(baseStats, equipment);
        }

        [SetUp]
        public void SetUp()
        {
            equipment = new EquipmentSet();
            baseStats = new BaseStats()
                .Set(StatType.Strength, 10)
                .Set(StatType.Dexterity, 10)
                .Set(StatType.Intelligence, 10)
                .Set(StatType.MaxLife, 100)
                .Set(StatType.MaxMana, 50);
        }

        [Test]
        public void NoGear_TotalsEqualBase_AndAttributesGrantNothingOnTheirOwn()
        {
            StatSheet sheet = Sheet();

            Assert.AreEqual(100f, sheet.Total(StatType.MaxLife));
            Assert.AreEqual(50f, sheet.Total(StatType.MaxMana));
            Assert.AreEqual(10f, sheet.Total(StatType.Strength));
            Assert.AreEqual(0f, sheet.Total(StatType.Evasion), "base dexterity must not grant evasion by itself");
            Assert.AreEqual(0f, sheet.FromGear(StatType.MaxLife));
        }

        [Test]
        public void EquippingAnItem_AddsItsModifiers()
        {
            ItemData helmet = Item(ItemType.Helmet, Mod(StatType.Armour, 32), Mod(StatType.MaxLife, 12));
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Helmet, helmet, out replaced);

            StatSheet sheet = Sheet();
            Assert.AreEqual(32f, sheet.Total(StatType.Armour));
            Assert.AreEqual(112f, sheet.Total(StatType.MaxLife));
            Assert.AreEqual(12f, sheet.Gear(StatType.MaxLife));
        }

        [Test]
        public void UnequippingRemovesTheBonus()
        {
            ItemData boots = Item(ItemType.Boots, Mod(StatType.MovementSpeed, 10));
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Boots, boots, out replaced);
            Assert.AreEqual(10f, Sheet().Total(StatType.MovementSpeed));

            equipment.Unequip(EquipSlot.Boots);
            Assert.AreEqual(0f, Sheet().Total(StatType.MovementSpeed));
        }

        [Test]
        public void SwappingItems_ReplacesTheOldBonusWithTheNewOne()
        {
            ItemData a = Item(ItemType.Helmet, Mod(StatType.Armour, 30));
            ItemData b = Item(ItemType.Helmet, Mod(StatType.Armour, 10));
            ItemData replaced;

            equipment.TryEquip(EquipSlot.Helmet, a, out replaced);
            equipment.TryEquip(EquipSlot.Helmet, b, out replaced);

            Assert.AreEqual(10f, Sheet().Total(StatType.Armour));
        }

        [Test]
        public void TwoRings_BothCount()
        {
            ItemData ruby = Item(ItemType.Ring, Mod(StatType.FireResistance, 20));
            ItemData another = Item(ItemType.Ring, Mod(StatType.FireResistance, 15));
            ItemData replaced;

            equipment.TryEquip(EquipSlot.Ring1, ruby, out replaced);
            equipment.TryEquip(EquipSlot.Ring2, another, out replaced);

            Assert.AreEqual(35f, Sheet().Total(StatType.FireResistance));
        }

        [Test]
        public void StrengthFromGear_GrantsLife_IntelligenceGrantsMana_DexterityGrantsEvasion()
        {
            ItemData a = Item(ItemType.Helmet, Mod(StatType.Strength, 8), Mod(StatType.Intelligence, 6), Mod(StatType.Dexterity, 9));
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Helmet, a, out replaced);

            StatSheet sheet = Sheet();
            Assert.AreEqual(4f, sheet.Derived(StatType.MaxLife));
            Assert.AreEqual(104f, sheet.Total(StatType.MaxLife));
            Assert.AreEqual(3f, sheet.Derived(StatType.MaxMana));
            Assert.AreEqual(53f, sheet.Total(StatType.MaxMana));
            Assert.AreEqual(9f, sheet.Total(StatType.Evasion));
            Assert.AreEqual(18f, sheet.Total(StatType.Strength));
        }

        [Test]
        public void Resistances_AreCappedAt75_ButGearBonusKeepsTheRawAmount()
        {
            ItemData a = Item(ItemType.Ring, Mod(StatType.ColdResistance, 100));
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Ring1, a, out replaced);

            StatSheet sheet = Sheet();
            Assert.AreEqual(75f, sheet.Total(StatType.ColdResistance));
            Assert.AreEqual(100f, sheet.FromGear(StatType.ColdResistance));
        }

        [Test]
        public void Block_IsCappedAt75()
        {
            ItemData shield = Item(ItemType.Shield, Mod(StatType.BlockChance, 90));
            ItemData replaced;
            equipment.TryEquip(EquipSlot.OffHand, shield, out replaced);

            Assert.AreEqual(75f, Sheet().Total(StatType.BlockChance));
        }

        [Test]
        public void LevelAndExperience_ArePassedThrough()
        {
            baseStats.Level = 7;
            baseStats.Experience = 250;
            baseStats.ExperienceRequired = 700;

            StatSheet sheet = Sheet();
            Assert.AreEqual(7, sheet.Level);
            Assert.AreEqual(250, sheet.Experience);
            Assert.AreEqual(700, sheet.ExperienceRequired);
        }

        [Test]
        public void ChangingBaseStats_Rebuilds_WithGearStillApplied()
        {
            ItemData belt = Item(ItemType.Belt, Mod(StatType.MaxLife, 15));
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Belt, belt, out replaced);

            baseStats.Set(StatType.MaxLife, 120); // e.g. after levelling up
            Assert.AreEqual(135f, Sheet().Total(StatType.MaxLife));
        }

        [Test]
        public void Build_WithNullBase_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StatSheet.Build(null, equipment));
        }
    }

    public class StatFormatterTests
    {
        [Test]
        public void ItemLines_UsePathOfExileWording()
        {
            Assert.AreEqual("+30 to Armour", StatFormatter.ItemLine(new StatModifier(StatType.Armour, 30)));
            Assert.AreEqual("+12 to Maximum Life", StatFormatter.ItemLine(new StatModifier(StatType.MaxLife, 12)));
            Assert.AreEqual("+15 to Maximum Mana", StatFormatter.ItemLine(new StatModifier(StatType.MaxMana, 15)));
            Assert.AreEqual("+8 to Strength", StatFormatter.ItemLine(new StatModifier(StatType.Strength, 8)));
            Assert.AreEqual("+20% to Fire Resistance", StatFormatter.ItemLine(new StatModifier(StatType.FireResistance, 20)));
            Assert.AreEqual("+12% Chance to Block", StatFormatter.ItemLine(new StatModifier(StatType.BlockChance, 12)));
            Assert.AreEqual("Adds 6 Physical Damage", StatFormatter.ItemLine(new StatModifier(StatType.PhysicalDamage, 6)));
            Assert.AreEqual("10% increased Movement Speed", StatFormatter.ItemLine(new StatModifier(StatType.MovementSpeed, 10)));
            Assert.AreEqual("4% increased Attack Speed", StatFormatter.ItemLine(new StatModifier(StatType.AttackSpeed, 4)));
        }

        [Test]
        public void NegativeValues_ReadCorrectly()
        {
            Assert.AreEqual("-10 to Strength", StatFormatter.ItemLine(new StatModifier(StatType.Strength, -10)));
            Assert.AreEqual("5% reduced Movement Speed", StatFormatter.ItemLine(new StatModifier(StatType.MovementSpeed, -5)));
        }

        [Test]
        public void EveryStatHasALabelAndAnItemLine()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                Assert.IsFalse(string.IsNullOrEmpty(StatFormatter.Label(stat)), stat + " label");
                Assert.IsFalse(string.IsNullOrEmpty(StatFormatter.ItemLine(new StatModifier(stat, 5))), stat + " item line");
            }
        }

        [Test]
        public void Values_AreFormattedWithPercentWhereNeeded()
        {
            Assert.AreEqual("42", StatFormatter.Value(StatType.Armour, 42f));
            Assert.AreEqual("20%", StatFormatter.Value(StatType.FireResistance, 20f));
            Assert.AreEqual("10%", StatFormatter.Value(StatType.MovementSpeed, 10f));
            Assert.AreEqual("2.5", StatFormatter.Number(2.5f));
        }
    }

    // Every item must be complete: real stats, a painted icon, and a 3D look that attaches to the right sockets.
    public class ItemContentTests
    {
        private static readonly HashSet<string> ValidSockets = new HashSet<string>
        {
            "Socket_Head", "Socket_Chest", "Socket_Waist", "Socket_Neck",
            "Socket_HandL", "Socket_HandR", "Socket_FootL", "Socket_FootR",
            "Socket_Ring", "Socket_MainHand", "Socket_OffHand"
        };

        private static string[] ExpectedSockets(ItemType type)
        {
            switch (type)
            {
                case ItemType.Helmet: return new[] { "Socket_Head" };
                case ItemType.BodyArmour: return new[] { "Socket_Chest" };
                case ItemType.Gloves: return new[] { "Socket_HandL", "Socket_HandR" };
                case ItemType.Boots: return new[] { "Socket_FootL", "Socket_FootR" };
                case ItemType.Belt: return new[] { "Socket_Waist" };
                case ItemType.Amulet: return new[] { "Socket_Neck" };
                case ItemType.Ring: return new[] { "Socket_Ring" };
                case ItemType.Weapon: return new[] { "Socket_MainHand" };
                case ItemType.Shield: return new[] { "Socket_OffHand" };
                default: return new string[0];
            }
        }

        [Test]
        public void EveryStarterItem_HasStats()
        {
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
                Assert.Greater(item.Modifiers.Count, 0, item.Name + " has no stats");
        }

        [Test]
        public void ItemIds_AreUnique()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
                Assert.IsTrue(ids.Add(item.Id), "duplicate id " + item.Id);
        }

        [Test]
        public void EveryStarterItem_HasAPaintedIcon_SizedForItsCells()
        {
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
            {
                Sprite icon = Resources.Load<Sprite>("ItemIcons/" + item.Id);
                Assert.IsNotNull(icon, item.Name + " has no icon at Resources/ItemIcons/" + item.Id);

                // Icons are drawn at 96 px per cell, so they match the item's footprint exactly.
                Assert.AreEqual(item.Width * 96, icon.texture.width, item.Name + " icon width");
                Assert.AreEqual(item.Height * 96, icon.texture.height, item.Name + " icon height");
            }
        }

        [Test]
        public void EveryStarterItem_HasA3DLook_ThatAttachesToTheRightSockets()
        {
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
            {
                GameObject prefab = Resources.Load<GameObject>("Equipment/" + item.Id);
                Assert.IsNotNull(prefab, item.Name + " has no prefab at Resources/Equipment/" + item.Id);

                HashSet<string> found = new HashSet<string>();
                foreach (Transform child in prefab.transform)
                {
                    Assert.IsTrue(ValidSockets.Contains(child.name), item.Name + " has a child that is not a socket: " + child.name);
                    found.Add(child.name);
                    Assert.Greater(child.childCount, 0, item.Name + "/" + child.name + " is empty");
                }

                foreach (string expected in ExpectedSockets(item.Type))
                    Assert.IsTrue(found.Contains(expected), item.Name + " is missing " + expected);
            }
        }

        [Test]
        public void EveryEquipmentPrefab_HasNoMissingMeshesOrMaterials()
        {
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
            {
                GameObject prefab = Resources.Load<GameObject>("Equipment/" + item.Id);
                Assert.IsNotNull(prefab, item.Id);

                foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                    Assert.IsNotNull(mf.sharedMesh, item.Id + "/" + mf.name + " has no mesh");

                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                    Assert.IsNotNull(r.sharedMaterial, item.Id + "/" + r.name + " has no material");
            }
        }

        [Test]
        public void ItemsOfTheSameType_ButDifferentIds_LookDifferent()
        {
            // The two helmets must not share a prefab, or equipping the other one would show no change.
            List<ItemData> items = ItemCatalog.CreateStarterItems();
            GameObject a = Resources.Load<GameObject>("Equipment/" + items.Find(i => i.Id == "iron_helmet").Id);
            GameObject b = Resources.Load<GameObject>("Equipment/" + items.Find(i => i.Id == "bronze_helmet").Id);
            Assert.AreNotSame(a, b);
        }
    }
}
