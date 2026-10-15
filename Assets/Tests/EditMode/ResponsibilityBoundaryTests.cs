using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Network.Replication;

namespace PoeClone.Tests
{
    /// <summary>Small state/contract checks; these never start gameplay or a network session.</summary>
    public class ResponsibilityBoundaryTests
    {
        // Runtime helpers live in Assembly-CSharp, which an asmdef test assembly cannot reference.
        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method).Invoke(target, args);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static object Binding(object bindings, int slot) =>
            bindings.GetType().GetProperty("Item").GetValue(bindings, new object[] { slot });

        [TestCase(true)]
        [TestCase(false)]
        public void MovingBindingsPreservesCooldownAndReplicaDuplicates(bool moveExisting)
        {
            object bindings = Activator.CreateInstance(RuntimeType("PoeClone.Skills.SkillBindings"), new object[] { 4 });
            object dash = Enum.Parse(RuntimeType("PoeClone.Skills.SkillId"), "Dash");
            var item = new ItemData("Test", ItemType.Ring, 1, 1, Color.white);
            Call(bindings, "SetSource", 0, dash, item, EquipSlot.Ring1, 5);
            Call(bindings, "StartCooldown", 0, 6f, 10f);

            Call(bindings, "Assign", 3, dash, item, EquipSlot.Ring1, 5, moveExisting);

            Assert.AreEqual(16f, Field(Binding(bindings, 3), "ReadyAt"));
            Assert.AreEqual(6f, Field(Binding(bindings, 3), "Cooldown"));
            Assert.AreEqual(moveExisting ? null : dash, Field(Binding(bindings, 0), "Skill"));
            Assert.AreEqual(moveExisting ? 0f : 16f, Field(Binding(bindings, 0), "ReadyAt"));
            Call(bindings, "Clear", 3);
            Assert.IsNull(Field(Binding(bindings, 3), "Item"));
            Assert.AreEqual(0f, Field(Binding(bindings, 3), "Cooldown"));
        }

        [Test]
        public void CooldownsOnlyPropagateToBindingsOfTheSameItemAndGrant()
        {
            object bindings = Activator.CreateInstance(RuntimeType("PoeClone.Skills.SkillBindings"), new object[] { 4 });
            object dash = Enum.Parse(RuntimeType("PoeClone.Skills.SkillId"), "Dash");
            var first = new ItemData("First", ItemType.Ring, 1, 1, Color.white);
            var second = new ItemData("Second", ItemType.Ring, 1, 1, Color.white);
            Call(bindings, "SetSource", 0, dash, first, EquipSlot.Ring1, 5);
            Call(bindings, "SetSource", 1, dash, first, EquipSlot.Ring1, 6);
            Call(bindings, "SetSource", 2, dash, first, EquipSlot.Ring1, 5);
            Call(bindings, "SetSource", 3, dash, second, EquipSlot.Ring2, 5);

            Call(bindings, "StartCooldown", 0, 6f, 10f);

            Assert.AreEqual(16f, Field(Binding(bindings, 0), "ReadyAt"));
            Assert.AreEqual(0f, Field(Binding(bindings, 1), "ReadyAt"));
            Assert.AreEqual(16f, Field(Binding(bindings, 2), "ReadyAt"));
            Assert.AreEqual(0f, Field(Binding(bindings, 3), "ReadyAt"));
        }

        [Test]
        public void MenuReplicaWaitsForItsSessionAndKeepsEarlyGearAcrossReset()
        {
            var go = new GameObject("MenuReplicaBoundaryTest");
            try
            {
                PlayerInventory inventory = go.AddComponent<PlayerInventory>();
                typeof(PlayerInventory).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(inventory, null);
                int applications = 0;
                Action<GearItem[]> ground = items => applications++;
                object menus = Activator.CreateInstance(RuntimeType("PoeClone.Network.SpectatorMenuReplica"),
                    new object[] { null, inventory, null, ground });
                var gear = new GearState
                {
                    pid = 7, gold = 12, lv = 5, st = 1, stn = new[] { "First", "Second" },
                    bag = new[] { new GearItem { i = "short_bow", n = "Short Bow", t = (int)ItemType.Weapon,
                        wp = (int)WeaponType.Bow, w = 2, h = 3, tn = "FFFFFFFF", x = 1, y = 1 } }
                };
                Call(menus, "Receive", JsonUtility.ToJson(gear));
                Call(menus, "Reset"); // First motion snapshot may arrive after the gear message.
                Call(menus, "ApplyPending", 8);
                Assert.IsEmpty(inventory.Grid.Items);
                Assert.AreEqual(0, applications);

                Call(menus, "ApplyPending", 7);
                Assert.AreEqual(1, inventory.Grid.Items.Count);
                Assert.AreEqual(12, inventory.Gold);
                Assert.AreEqual("Second", inventory.StashTabName(1));
                Assert.AreEqual(true, Call(menus, "HasGearFor", 7));
                Call(menus, "ApplyPending", 7);
                Assert.AreEqual(1, applications, "apply each message once");

                Call(menus, "Reset");
                Assert.IsEmpty(inventory.Grid.Items);
                Assert.AreEqual(false, Call(menus, "HasGearFor", 7));
                Call(menus, "ApplyPending", 8);
                Assert.IsEmpty(inventory.Grid.Items, "another session must not inherit the previous bag");
            }
            finally
            {
                SpectatorMirror.ClearRemote();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
