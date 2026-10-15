using NUnit.Framework;
using PoeClone.Inventory;
using UnityEngine;

namespace PoeClone.Tests
{
    public class ItemRequirementsTests
    {
        [Test]
        public void RestoreBypassesRequirementsButStillRejectsIncompatibleHands()
        {
            var equipment = new EquipmentSet { MeetsRequirements = (slot, item) => false };
            ItemData bow = ItemGenerator.Generate(new System.Random(7), "short_bow", 1, ItemRarity.Normal);
            var shield = new ItemData("test_shield", "Shield", ItemType.Shield, 2, 2, Color.white, null);
            var quiver = new ItemData("test_quiver", "Quiver", ItemType.Quiver, 2, 2, Color.white, null);

            Assert.IsFalse(equipment.TryEquip(EquipSlot.MainHand, bow, out _));
            Assert.IsTrue(equipment.Restore(EquipSlot.MainHand, bow, out _));
            Assert.IsFalse(equipment.Restore(EquipSlot.OffHand, shield, out _));
            Assert.IsTrue(equipment.Restore(EquipSlot.OffHand, quiver, out _));
            Assert.IsFalse(equipment.Restore(EquipSlot.Helmet, bow, out _));

            equipment.Unequip(EquipSlot.OffHand);
            equipment.Unequip(EquipSlot.MainHand);
            Assert.IsTrue(equipment.Restore(EquipSlot.OffHand, shield, out _));
            Assert.IsFalse(equipment.Restore(EquipSlot.MainHand, bow, out _));
        }

        private static ItemData Armour() => new ItemData("hunter_coat", "Hunter Coat", ItemType.BodyArmour,
            2, 3, Color.white, new[] { new StatModifier(StatType.Dexterity, 8), new StatModifier(StatType.Evasion, 70) });

        [Test]
        public void OwnAttributesCanQualifyAnItem()
        {
            var equipment = new EquipmentSet { EnforceRequirements = true };
            equipment.Restore(EquipSlot.BodyArmour, Armour(), out _);
            var stats = new BaseStats { Level = 7 };
            stats.Set(StatType.Dexterity, 24);
            StatSheet sheet = StatSheet.Build(stats, equipment);
            Assert.IsTrue(equipment.IsActive(EquipSlot.BodyArmour));
            Assert.AreEqual(32, sheet.Total(StatType.Dexterity));
        }

        [Test]
        public void RemovingAttributeRingDisablesArmourAndRestoringItReactivates()
        {
            var equipment = new EquipmentSet { EnforceRequirements = true };
            ItemData armour = Armour();
            var ring = new ItemData("iron_ring", "Dex Ring", ItemType.Ring, 1, 1, Color.white,
                new[] { new StatModifier(StatType.Dexterity, 30) });
            equipment.Restore(EquipSlot.BodyArmour, armour, out _);
            equipment.Restore(EquipSlot.Ring1, ring, out _);
            var stats = new BaseStats { Level = 7 };
            stats.Set(StatType.Strength, 10).Set(StatType.Dexterity, 10);
            Assert.AreEqual(70, StatSheet.Build(stats, equipment).Gear(StatType.Evasion));
            equipment.Unequip(EquipSlot.Ring1);
            StatSheet sheet = StatSheet.Build(stats, equipment);
            Assert.AreSame(armour, equipment.Get(EquipSlot.BodyArmour));
            Assert.IsFalse(equipment.IsActive(EquipSlot.BodyArmour));
            Assert.AreEqual(0, sheet.Gear(StatType.Evasion));
            Assert.AreEqual(10, sheet.Total(StatType.Dexterity));
            equipment.Restore(EquipSlot.Ring1, ring, out _);
            Assert.AreEqual(70, StatSheet.Build(stats, equipment).Gear(StatType.Evasion));
            Assert.IsTrue(equipment.IsActive(EquipSlot.BodyArmour));
        }

        [Test]
        public void LosingSupportDisablesDependentGearUntilStable()
        {
            var equipment = new EquipmentSet { EnforceRequirements = true };
            ItemData armour = Armour();
            var helmet = new ItemData("tracker_hood", "Tracker Hood", ItemType.Helmet, 2, 2, Color.white,
                new[] { new StatModifier(StatType.Strength, 30) });
            var gloves = new ItemData("plated_gauntlets", "Plated Gauntlets", ItemType.Gloves, 2, 2, Color.white,
                new[] { new StatModifier(StatType.Armour, 28) });
            equipment.Restore(EquipSlot.BodyArmour, armour, out _);
            equipment.Restore(EquipSlot.Helmet, helmet, out _);
            equipment.Restore(EquipSlot.Gloves, gloves, out _);
            var stats = new BaseStats { Level = 7 };
            stats.Set(StatType.Strength, 10).Set(StatType.Dexterity, 10);
            StatSheet sheet = StatSheet.Build(stats, equipment);
            Assert.IsFalse(equipment.IsActive(EquipSlot.Helmet));
            Assert.IsFalse(equipment.IsActive(EquipSlot.Gloves));
            Assert.AreEqual(0, sheet.Gear(StatType.Armour));
        }

        [Test]
        public void SavedGearBelowRequiredLevelStaysWornWithoutStats()
        {
            var equipment = new EquipmentSet { EnforceRequirements = true };
            ItemData armour = ItemRecord.From(Armour()).ToItem();
            equipment.Restore(EquipSlot.BodyArmour, armour, out _);
            var stats = new BaseStats { Level = 1 };
            stats.Set(StatType.Dexterity, 100);
            StatSheet sheet = StatSheet.Build(stats, equipment);
            Assert.AreSame(armour, equipment.Get(EquipSlot.BodyArmour));
            Assert.IsFalse(equipment.IsActive(EquipSlot.BodyArmour));
            Assert.AreEqual(0, sheet.Gear(StatType.Evasion));
        }
    }
}
