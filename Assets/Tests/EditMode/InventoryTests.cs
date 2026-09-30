using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class InventoryGridTests
    {
        private InventoryGrid grid;

        private static ItemData Item(int w, int h, string name = "Test")
        {
            return new ItemData(name, ItemType.Helmet, w, h, Color.white);
        }

        [SetUp]
        public void SetUp()
        {
            grid = new InventoryGrid(12, 5);
        }

        [Test]
        public void TryPlace_EmptyGrid_Succeeds()
        {
            ItemData item = Item(2, 2);
            Assert.IsTrue(grid.TryPlace(item, 3, 1));
            Assert.AreSame(item, grid.GetAt(3, 1).Item);
        }

        [Test]
        public void TryPlace_PastRightEdge_Fails()
        {
            Assert.IsFalse(grid.TryPlace(Item(2, 1), 11, 0));
        }

        [Test]
        public void TryPlace_PastBottomEdge_Fails()
        {
            Assert.IsFalse(grid.TryPlace(Item(1, 3), 0, 3));
        }

        [Test]
        public void TryPlace_NegativeCoordinates_Fails()
        {
            Assert.IsFalse(grid.TryPlace(Item(1, 1), -1, 0));
            Assert.IsFalse(grid.TryPlace(Item(1, 1), 0, -1));
        }

        [Test]
        public void TryPlace_ExactlyFillingTheCorner_Succeeds()
        {
            Assert.IsTrue(grid.TryPlace(Item(2, 2), 10, 3));
        }

        [Test]
        public void TryPlace_PartialOverlap_Fails()
        {
            Assert.IsTrue(grid.TryPlace(Item(2, 2), 0, 0));
            Assert.IsFalse(grid.TryPlace(Item(2, 2), 1, 1));
        }

        [Test]
        public void TryPlace_TouchingButNotOverlapping_Succeeds()
        {
            Assert.IsTrue(grid.TryPlace(Item(2, 2), 0, 0));
            Assert.IsTrue(grid.TryPlace(Item(2, 2), 2, 0));
            Assert.IsTrue(grid.TryPlace(Item(2, 2), 0, 2));
        }

        [Test]
        public void TryPlace_SameItemTwice_Fails()
        {
            ItemData item = Item(1, 1);
            Assert.IsTrue(grid.TryPlace(item, 0, 0));
            Assert.IsFalse(grid.TryPlace(item, 5, 0));
        }

        [Test]
        public void TryPlace_Null_Fails()
        {
            Assert.IsFalse(grid.TryPlace(null, 0, 0));
        }

        [Test]
        public void GetAt_ReturnsTheItemForEveryCellItCovers()
        {
            ItemData item = Item(2, 3);
            grid.TryPlace(item, 4, 1);

            for (int x = 4; x < 6; x++)
            {
                for (int y = 1; y < 4; y++)
                    Assert.AreSame(item, grid.GetAt(x, y).Item, "cell " + x + "," + y);
            }

            Assert.IsNull(grid.GetAt(3, 1));
            Assert.IsNull(grid.GetAt(6, 1));
            Assert.IsNull(grid.GetAt(4, 0));
            Assert.IsNull(grid.GetAt(4, 4));
        }

        [Test]
        public void Remove_FreesTheCells()
        {
            ItemData item = Item(2, 2);
            grid.TryPlace(item, 0, 0);

            Assert.IsTrue(grid.Remove(item));
            Assert.IsNull(grid.GetAt(0, 0));
            Assert.IsTrue(grid.TryPlace(Item(2, 2), 0, 0));
        }

        [Test]
        public void Remove_ItemNotInGrid_ReturnsFalse()
        {
            Assert.IsFalse(grid.Remove(Item(1, 1)));
        }

        [Test]
        public void TryAutoPlace_UsesFirstFreeSpotLeftToRightTopToBottom()
        {
            grid.TryPlace(Item(2, 2), 0, 0);

            ItemData next = Item(1, 1);
            Assert.IsTrue(grid.TryAutoPlace(next));

            PlacedItem placed = grid.GetAt(2, 0);
            Assert.IsNotNull(placed);
            Assert.AreSame(next, placed.Item);
        }

        [Test]
        public void TryAutoPlace_SkipsGapsThatAreTooSmall()
        {
            InventoryGrid small = new InventoryGrid(3, 2);
            small.TryPlace(Item(1, 1), 1, 0);

            ItemData wide = Item(2, 1);
            Assert.IsTrue(small.TryAutoPlace(wide));
            Assert.AreSame(wide, small.GetAt(0, 1).Item);
        }

        [Test]
        public void TryAutoPlace_FullGrid_Fails()
        {
            InventoryGrid tiny = new InventoryGrid(2, 2);
            Assert.IsTrue(tiny.TryAutoPlace(Item(2, 2)));
            Assert.IsFalse(tiny.TryAutoPlace(Item(1, 1)));
        }

        [Test]
        public void TryAutoPlace_ItemLargerThanGrid_Fails()
        {
            Assert.IsFalse(new InventoryGrid(2, 2).TryAutoPlace(Item(3, 1)));
        }

        [Test]
        public void TryPlaceOrSwap_EmptySpot_PlacesWithoutReplacing()
        {
            ItemData item = Item(2, 2);
            ItemData replaced;

            Assert.IsTrue(grid.TryPlaceOrSwap(item, 2, 2, out replaced));
            Assert.IsNull(replaced);
            Assert.AreSame(item, grid.GetAt(2, 2).Item);
        }

        [Test]
        public void TryPlaceOrSwap_OneItemInTheWay_SwapsItOut()
        {
            ItemData old = Item(1, 1, "Old");
            ItemData incoming = Item(2, 2, "Incoming");
            grid.TryPlace(old, 3, 3);

            ItemData replaced;
            Assert.IsTrue(grid.TryPlaceOrSwap(incoming, 2, 2, out replaced));

            Assert.AreSame(old, replaced);
            Assert.IsFalse(grid.Contains(old));
            Assert.AreSame(incoming, grid.GetAt(3, 3).Item);
        }

        [Test]
        public void TryPlaceOrSwap_TwoItemsInTheWay_FailsAndChangesNothing()
        {
            ItemData a = Item(1, 1, "A");
            ItemData b = Item(1, 1, "B");
            grid.TryPlace(a, 0, 0);
            grid.TryPlace(b, 1, 0);

            ItemData incoming = Item(2, 1, "Incoming");
            ItemData replaced;

            Assert.IsFalse(grid.TryPlaceOrSwap(incoming, 0, 0, out replaced));
            Assert.IsNull(replaced);
            Assert.IsTrue(grid.Contains(a));
            Assert.IsTrue(grid.Contains(b));
            Assert.IsFalse(grid.Contains(incoming));
        }

        [Test]
        public void TryPlaceOrSwap_OutOfBounds_Fails()
        {
            ItemData replaced;
            Assert.IsFalse(grid.TryPlaceOrSwap(Item(2, 2), 11, 4, out replaced));
        }

        [Test]
        public void Changed_FiresOnPlaceAndRemove_ButNotOnFailedPlace()
        {
            int count = 0;
            grid.Changed += () => count++;

            ItemData item = Item(1, 1);
            grid.TryPlace(item, 0, 0);
            Assert.AreEqual(1, count);

            grid.TryPlace(Item(1, 1), 0, 0); // overlaps, must fail silently
            Assert.AreEqual(1, count);

            grid.Remove(item);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Constructor_RejectsNonPositiveSizes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryGrid(0, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryGrid(5, 0));
        }

        [Test]
        public void ItemData_RejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemData("X", ItemType.Ring, 0, 1, Color.white));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemData("X", ItemType.Ring, 1, 0, Color.white));
            Assert.Throws<ArgumentException>(() => new ItemData("", ItemType.Ring, 1, 1, Color.white));
        }
    }

    public class EquipmentSetTests
    {
        private EquipmentSet equipment;

        private static ItemData Of(ItemType type, string name = "Test")
        {
            return new ItemData(name, type, 1, 1, Color.white);
        }

        [SetUp]
        public void SetUp()
        {
            equipment = new EquipmentSet();
        }

        [Test]
        public void EverySlot_AcceptsOnlyItsOwnItemType()
        {
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
                {
                    bool expected = type == SlotRules.AcceptedType(slot);
                    Assert.AreEqual(expected, equipment.CanEquip(slot, Of(type)), slot + " with " + type);
                }
            }
        }

        [Test]
        public void ThereAreTwoRingSlotsAndOneAmuletSlot()
        {
            int rings = 0;
            int amulets = 0;

            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                if (SlotRules.AcceptedType(slot) == ItemType.Ring)
                    rings++;
                if (SlotRules.AcceptedType(slot) == ItemType.Amulet)
                    amulets++;
            }

            Assert.AreEqual(2, rings);
            Assert.AreEqual(1, amulets);
        }

        [Test]
        public void AllSlots_ListsEveryEnumValueExactlyOnce()
        {
            Array all = Enum.GetValues(typeof(EquipSlot));
            Assert.AreEqual(all.Length, SlotRules.AllSlots.Length);
            Assert.AreEqual(all.Length, new HashSet<EquipSlot>(SlotRules.AllSlots).Count);
        }

        [Test]
        public void TwoDifferentRings_CanBeWornAtTheSameTime()
        {
            ItemData a = Of(ItemType.Ring, "A");
            ItemData b = Of(ItemType.Ring, "B");
            ItemData replaced;

            Assert.IsTrue(equipment.TryEquip(EquipSlot.Ring1, a, out replaced));
            Assert.IsTrue(equipment.TryEquip(EquipSlot.Ring2, b, out replaced));
            Assert.AreSame(a, equipment.Get(EquipSlot.Ring1));
            Assert.AreSame(b, equipment.Get(EquipSlot.Ring2));
        }

        [Test]
        public void WrongTypeIsRejected_AndSlotStaysUnchanged()
        {
            ItemData helmet = Of(ItemType.Helmet);
            ItemData ring = Of(ItemType.Ring);
            ItemData replaced;

            Assert.IsFalse(equipment.TryEquip(EquipSlot.Ring1, helmet, out replaced));
            Assert.IsNull(equipment.Get(EquipSlot.Ring1));

            equipment.TryEquip(EquipSlot.Ring1, ring, out replaced);
            Assert.IsFalse(equipment.TryEquip(EquipSlot.Ring1, helmet, out replaced));
            Assert.AreSame(ring, equipment.Get(EquipSlot.Ring1));
            Assert.IsNull(replaced);
        }

        [Test]
        public void RingCannotGoInTheAmuletSlot_AndAmuletCannotGoInARingSlot()
        {
            ItemData replaced;
            Assert.IsFalse(equipment.TryEquip(EquipSlot.Amulet, Of(ItemType.Ring), out replaced));
            Assert.IsFalse(equipment.TryEquip(EquipSlot.Ring1, Of(ItemType.Amulet), out replaced));
            Assert.IsFalse(equipment.TryEquip(EquipSlot.Ring2, Of(ItemType.Amulet), out replaced));
        }

        [Test]
        public void WeaponOnlyGoesInMainHand_AndShieldOnlyInOffHand()
        {
            ItemData replaced;
            Assert.IsTrue(equipment.CanEquip(EquipSlot.MainHand, Of(ItemType.Weapon)));
            Assert.IsFalse(equipment.CanEquip(EquipSlot.OffHand, Of(ItemType.Weapon)));
            Assert.IsTrue(equipment.CanEquip(EquipSlot.OffHand, Of(ItemType.Shield)));
            Assert.IsFalse(equipment.CanEquip(EquipSlot.MainHand, Of(ItemType.Shield)));
            Assert.IsFalse(equipment.TryEquip(EquipSlot.MainHand, null, out replaced));
        }

        [Test]
        public void EquippingIntoAnOccupiedSlot_ReturnsTheOldItem()
        {
            ItemData first = Of(ItemType.Helmet, "First");
            ItemData second = Of(ItemType.Helmet, "Second");
            ItemData replaced;

            equipment.TryEquip(EquipSlot.Helmet, first, out replaced);
            Assert.IsNull(replaced);

            Assert.IsTrue(equipment.TryEquip(EquipSlot.Helmet, second, out replaced));
            Assert.AreSame(first, replaced);
            Assert.AreSame(second, equipment.Get(EquipSlot.Helmet));
        }

        [Test]
        public void Unequip_ReturnsItemAndEmptiesSlot()
        {
            ItemData boots = Of(ItemType.Boots);
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Boots, boots, out replaced);

            Assert.AreSame(boots, equipment.Unequip(EquipSlot.Boots));
            Assert.IsNull(equipment.Get(EquipSlot.Boots));
        }

        [Test]
        public void Unequip_EmptySlot_ReturnsNullWithoutRaisingEvent()
        {
            int events = 0;
            equipment.Changed += (slot, item) => events++;

            Assert.IsNull(equipment.Unequip(EquipSlot.Belt));
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Changed_ReportsSlotAndNewItem()
        {
            List<KeyValuePair<EquipSlot, ItemData>> log = new List<KeyValuePair<EquipSlot, ItemData>>();
            equipment.Changed += (slot, item) => log.Add(new KeyValuePair<EquipSlot, ItemData>(slot, item));

            ItemData amulet = Of(ItemType.Amulet);
            ItemData replaced;
            equipment.TryEquip(EquipSlot.Amulet, amulet, out replaced);
            equipment.Unequip(EquipSlot.Amulet);
            equipment.TryEquip(EquipSlot.Amulet, Of(ItemType.Ring), out replaced); // rejected: no event

            Assert.AreEqual(2, log.Count);
            Assert.AreEqual(EquipSlot.Amulet, log[0].Key);
            Assert.AreSame(amulet, log[0].Value);
            Assert.AreEqual(EquipSlot.Amulet, log[1].Key);
            Assert.IsNull(log[1].Value);
        }
    }

    public class IconAndCatalogTests
    {
        [Test]
        public void EveryItemType_HasAnIconThatIsNeitherEmptyNorSolid()
        {
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                Sprite sprite = IconFactory.Get(type);
                Assert.IsNotNull(sprite, type.ToString());

                float coverage = IconFactory.Coverage(sprite);
                Assert.Greater(coverage, 0.04f, type + " icon looks empty");
                Assert.Less(coverage, 0.60f, type + " icon looks like a solid block");
            }
        }

        [Test]
        public void Icons_AreCached()
        {
            Assert.AreSame(IconFactory.Get(ItemType.Ring), IconFactory.Get(ItemType.Ring));
        }

        [Test]
        public void EachItemType_HasADistinctIcon()
        {
            List<ItemType> types = new List<ItemType>((ItemType[])Enum.GetValues(typeof(ItemType)));
            for (int i = 0; i < types.Count; i++)
            {
                for (int j = i + 1; j < types.Count; j++)
                {
                    Color32[] a = IconFactory.Get(types[i]).texture.GetPixels32();
                    Color32[] b = IconFactory.Get(types[j]).texture.GetPixels32();

                    int differing = 0;
                    for (int k = 0; k < a.Length; k++)
                    {
                        if (a[k].a != b[k].a)
                            differing++;
                    }

                    Assert.Greater(differing, a.Length / 50, types[i] + " and " + types[j] + " icons are nearly identical");
                }
            }
        }

        [Test]
        public void StarterItems_AllFitInTheDefaultBag()
        {
            InventoryGrid grid = new InventoryGrid(12, 5);
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
                Assert.IsTrue(grid.TryAutoPlace(item), "no room for " + item.Name);
        }

        [Test]
        public void StarterItems_CoverEveryEquipmentSlot_AndIncludeASpareRing()
        {
            List<ItemData> items = ItemCatalog.CreateStarterItems();

            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                bool found = items.Exists(i => SlotRules.Accepts(slot, i));
                Assert.IsTrue(found, "no starter item fits " + slot);
            }

            int rings = items.FindAll(i => i.Type == ItemType.Ring).Count;
            Assert.GreaterOrEqual(rings, 3);
        }
    }
}
