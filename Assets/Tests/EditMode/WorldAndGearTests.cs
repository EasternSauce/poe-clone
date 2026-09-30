using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class BaseGearRulesTests
    {
        private EquipmentSet equipment;

        private static ItemData Armour(bool hasCape)
        {
            return new ItemData("armour_" + hasCape, "Armour", ItemType.BodyArmour, 2, 3, Color.white, null, hasCape);
        }

        private static ItemData Helmet()
        {
            return new ItemData("Helm", ItemType.Helmet, 2, 2, Color.white);
        }

        [SetUp]
        public void SetUp()
        {
            equipment = new EquipmentSet();
        }

        [Test]
        public void Cloak_IsVisible_WhenNoArmourIsWorn()
        {
            Assert.IsFalse(BaseGearRules.IsHidden(EquipSlot.BodyArmour, true, equipment));
        }

        [Test]
        public void Cloak_IsHidden_ByArmourWithoutACape()
        {
            ItemData replaced;
            equipment.TryEquip(EquipSlot.BodyArmour, Armour(false), out replaced);

            Assert.IsTrue(BaseGearRules.IsHidden(EquipSlot.BodyArmour, true, equipment));
        }

        [Test]
        public void Cloak_StaysVisible_WithArmourThatHasACape()
        {
            ItemData replaced;
            equipment.TryEquip(EquipSlot.BodyArmour, Armour(true), out replaced);

            Assert.IsFalse(BaseGearRules.IsHidden(EquipSlot.BodyArmour, true, equipment));
        }

        [Test]
        public void Cloak_ReturnsWhenTheArmourIsTakenOff()
        {
            ItemData replaced;
            equipment.TryEquip(EquipSlot.BodyArmour, Armour(false), out replaced);
            Assert.IsTrue(BaseGearRules.IsHidden(EquipSlot.BodyArmour, true, equipment));

            equipment.Unequip(EquipSlot.BodyArmour);
            Assert.IsFalse(BaseGearRules.IsHidden(EquipSlot.BodyArmour, true, equipment));
        }

        [Test]
        public void PiecesThatCannotKeepThemselves_AreHiddenEvenIfTheArmourHasACape()
        {
            // e.g. the starting pauldron: replaced by any body armour, cape or not.
            ItemData replaced;
            equipment.TryEquip(EquipSlot.BodyArmour, Armour(true), out replaced);

            Assert.IsTrue(BaseGearRules.IsHidden(EquipSlot.BodyArmour, false, equipment));
        }

        [Test]
        public void Hood_IsHiddenByAnyHelmet_ButNotByArmour()
        {
            ItemData replaced;
            equipment.TryEquip(EquipSlot.BodyArmour, Armour(false), out replaced);
            Assert.IsFalse(BaseGearRules.IsHidden(EquipSlot.Helmet, false, equipment));

            equipment.TryEquip(EquipSlot.Helmet, Helmet(), out replaced);
            Assert.IsTrue(BaseGearRules.IsHidden(EquipSlot.Helmet, false, equipment));
        }

        [Test]
        public void NoEquipmentSet_HidesNothing()
        {
            Assert.IsFalse(BaseGearRules.IsHidden(EquipSlot.BodyArmour, true, null));
        }

        [Test]
        public void Items_DoNotHaveCapesByDefault()
        {
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
                Assert.IsFalse(item.HasCape, item.Name);
        }
    }

    // The rule: nothing you can walk up onto. Small props have no collider; solid props are tall vertical walls.
    public class ClimbingTests
    {
        private const float PlayerHeight = 2f;

        private static GameObject Prefab(string name)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/" + name + ".prefab");
            Assert.IsNotNull(p, "missing prefab " + name);
            return p;
        }

        [TestCase("Bush")]
        [TestCase("Rock")]
        public void SolidProps_AreVerticalWallsTallerThanThePlayer_StartingAtTheGround(string name)
        {
            GameObject prefab = Prefab(name);
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.Greater(colliders.Length, 0, name + " has no collider");

            foreach (Collider c in colliders)
            {
                Assert.IsNotInstanceOf<MeshCollider>(c, name + " must not use a rounded mesh collider (it can be climbed)");

                float bottom;
                float height;

                CapsuleCollider capsule = c as CapsuleCollider;
                BoxCollider box = c as BoxCollider;

                if (capsule != null)
                {
                    height = capsule.height;
                    bottom = capsule.center.y - capsule.height * 0.5f;
                }
                else if (box != null)
                {
                    height = box.size.y;
                    bottom = box.center.y - box.size.y * 0.5f;
                }
                else
                {
                    Assert.Fail(name + " uses an unexpected collider type " + c.GetType().Name);
                    return;
                }

                Assert.Less(Mathf.Abs(bottom), 0.02f, name + " wall must start at the ground");
                Assert.GreaterOrEqual(height, PlayerHeight, name + " wall must be at least as tall as the player");
            }
        }

        [TestCase("BushSmall")]
        [TestCase("RockSmall")]
        public void SmallProps_HaveNoColliderAtAll_SoYouWalkThroughThem(string name)
        {
            GameObject prefab = Prefab(name);
            Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length, name + " should be walk-through");
            Assert.Greater(prefab.GetComponentsInChildren<MeshFilter>(true).Length, 0, name + " should still be visible");
        }

        [Test]
        public void SmallAndSolidVariants_LookTheSame()
        {
            Assert.AreEqual(
                Prefab("Bush").GetComponentsInChildren<MeshFilter>(true).Length,
                Prefab("BushSmall").GetComponentsInChildren<MeshFilter>(true).Length);
            Assert.AreEqual(
                Prefab("Rock").GetComponentsInChildren<MeshFilter>(true).Length,
                Prefab("RockSmall").GetComponentsInChildren<MeshFilter>(true).Length);
        }

        [Test]
        public void TreeCanopies_CannotBeReachedFromTheGround()
        {
            // Canopy colliders must sit above head height or be steeper than the slope limit,
            // otherwise the player could walk up the foliage.
            foreach (string name in new[] { "Pine", "Oak" })
            {
                GameObject prefab = Prefab(name);
                foreach (MeshCollider mc in prefab.GetComponentsInChildren<MeshCollider>(true))
                {
                    Transform t = mc.transform;
                    float baseY = t.localPosition.y;
                    Assert.GreaterOrEqual(baseY, 0.85f, name + "/" + t.name + " canopy starts too low to be safe");
                }
            }
        }
    }

    public class GearLookTests
    {
        private static GameObject Equip(string id)
        {
            GameObject p = Resources.Load<GameObject>("Equipment/" + id);
            Assert.IsNotNull(p, id);
            return p;
        }

        [TestCase("iron_helmet")]
        [TestCase("bronze_helmet")]
        public void Helmets_HaveNoUprightHornShapes(string id)
        {
            GameObject prefab = Equip(id);

            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.sharedMesh.name != "Cone")
                    continue;

                // A cone used on a helmet must be squashed flat (a fin), not a round spike (a horn).
                Vector3 s = mf.transform.localScale;
                Assert.LessOrEqual(Mathf.Min(s.x, s.z), 0.03f, id + "/" + mf.name + " is a round spike, which reads as a horn");

                // ...and it must point backward, not up.
                Vector3 direction = mf.transform.parent.rotation * mf.transform.localRotation * Vector3.up;
                Assert.Less(direction.z, -0.5f, id + "/" + mf.name + " should sweep backward");
            }
        }

        [Test]
        public void Shield_IsAngledOutward_ButStillFacesTheFront()
        {
            GameObject prefab = Equip("wooden_shield");
            Transform node = prefab.transform.Find("Socket_OffHand");
            Assert.IsNotNull(node);

            Vector3 facing = node.localRotation * Vector3.forward;
            float yaw = Mathf.Abs(Vector3.SignedAngle(Vector3.forward, facing, Vector3.up));

            Assert.GreaterOrEqual(yaw, 20f, "shield should be visibly angled");
            Assert.LessOrEqual(yaw, 60f, "shield must stay mostly front-facing so it shows in the inventory preview");
            Assert.Greater(facing.z, 0.5f, "shield face must still point forward");
            Assert.Less(facing.x, 0f, "the shield is on the left arm, so it should turn outward to the left");
        }
    }
}
