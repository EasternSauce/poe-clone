using System;
using System.Collections;
using System.Collections.Generic;
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

        [TestCase(1f, 1f, .1f, 4f)]
        [TestCase(2f, 1f, .05f, 4f)]
        [TestCase(1f, 2f, .1f, 2f)]
        public void SpellCastDurationAndCooldownScaleIndependently(float castRate, float recoveryRate, float cast, float cooldown)
        {
            object skill = Activator.CreateInstance(RuntimeType("PoeClone.Skills.SkillDefinition"));
            skill.GetType().GetField("Spell").SetValue(skill, true);
            skill.GetType().GetField("CastTime").SetValue(skill, .1f);
            skill.GetType().GetField("Cooldown").SetValue(skill, 4f);
            Assert.AreEqual(cast, (float)Call(skill, "CastDuration", castRate), .0001f);
            Assert.AreEqual(cooldown, (float)Call(skill, "RecoveryDuration", recoveryRate), .0001f);
            Assert.AreEqual(cast + cooldown, (float)Call(skill, "UseInterval", castRate, recoveryRate), .0001f,
                "The full cast must finish before the separate cooldown starts");
        }

        [Test]
        public void EverySpellHasAnExplicitPositiveCastTimeAndSpamSpellsHaveNoCooldown()
        {
            var spam = new HashSet<string> { "FireBolt", "IceShard", "ChainLightning", "VenomSpout" };
            foreach (object skill in (IEnumerable)RuntimeType("PoeClone.Skills.SkillBook").GetField("All").GetValue(null))
            {
                if (!(bool)Field(skill, "Spell")) continue;
                string id = Field(skill, "Id").ToString();
                Assert.Greater((float)Field(skill, "CastTime"), 0f, id);
                if (spam.Contains(id)) Assert.AreEqual(0f, Field(skill, "Cooldown"), id);
                if (id == "RaiseSkeletons" || id == "SkeletonMages") Assert.AreEqual(4f, Field(skill, "Cooldown"), id);
            }
        }

        [Test]
        public void CastSpeedAndCooldownRecoveryAreSeparateStats()
        {
            var sheet = StatSheet.Build(new BaseStats().Set(StatType.CastSpeed, 100), null);
            Assert.AreEqual(2f, sheet.CastRateMultiplier);
            Assert.AreEqual(1f, sheet.CooldownRecoveryMultiplier);
            sheet = StatSheet.Build(new BaseStats().Set(StatType.CooldownRecovery, 100), null);
            Assert.AreEqual(1f, sheet.CastRateMultiplier);
            Assert.AreEqual(2f, sheet.CooldownRecoveryMultiplier);
            var legacy = new StatModifier(StatType.LegacyCooldownRecovery, 25);
            Assert.AreEqual(StatType.CooldownRecovery, legacy.Stat);
        }

        [TestCase(StatType.GrantRaiseSkeletons)]
        [TestCase(StatType.GrantSkeletonMages)]
        public void EquippedSkeletonSpellUsesCastSpeedOnlyForItsGesture(StatType grant)
        {
            var go = new GameObject("Equipped skeleton timing check");
            go.SetActive(false);
            try
            {
                var inventory = go.AddComponent<PlayerInventory>();
                Type type = RuntimeType("PoeClone.Skills.PlayerSkills");
                Component skills = go.AddComponent(type);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var equipment = new EquipmentSet();
                typeof(PlayerInventory).GetProperty("Equipment").SetValue(inventory, equipment);
                type.GetField("inventory", flags).SetValue(skills, inventory);
                var weapon = new ItemData("timing-check", "Test Sceptre", ItemType.Weapon, 1, 1, Color.white,
                    new[] { new StatModifier(grant, 1f) }, weaponType: WeaponType.Sceptre);
                Assert.IsTrue(equipment.TryEquip(EquipSlot.MainHand, weapon, out _));
                object id = type.GetProperty("MainSkill").GetValue(skills);
                var basis = new BaseStats().Set(StatType.CastSpeed, 100);
                typeof(PlayerInventory).GetProperty("Stats").SetValue(inventory, StatSheet.Build(basis, null));
                Assert.AreEqual(.0975f, (float)Call(skills, "CastTime", id), .0001f);
                Assert.AreEqual(4f, (float)Call(skills, "Cooldown", id), .0001f);
                basis.Set(StatType.CooldownRecovery, 100);
                typeof(PlayerInventory).GetProperty("Stats").SetValue(inventory, StatSheet.Build(basis, null));
                Assert.AreEqual(.0975f, (float)Call(skills, "CastTime", id), .0001f);
                Assert.AreEqual(2f, (float)Call(skills, "Cooldown", id), .0001f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(WeaponType.Staff)]
        [TestCase(WeaponType.Sceptre)]
        [TestCase(WeaponType.Unarmed)]
        public void CastingGestureUsesSpecifiedDurationInsteadOfGenericAttackSpeed(WeaponType weapon)
        {
            var go = new GameObject("Cast duration check");
            go.SetActive(false);
            try
            {
                Component animator = go.AddComponent(RuntimeType("PoeClone.Visuals.CharacterAttackAnimator"));
                Call(animator, "PlayCast", weapon, .1f);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                object profile = animator.GetType().GetField("activeProfile", flags).GetValue(animator);
                float duration = (float)Field(profile, "Duration");
                float rate = (float)animator.GetType().GetField("speedMultiplier", flags).GetValue(animator);
                Assert.AreEqual(.1f, duration / rate, .0001f);
                Assert.IsTrue((bool)animator.GetType().GetField("castingGesture", flags).GetValue(animator));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static IEnumerator Schedule(IEnumerator routine) => (IEnumerator)
            RuntimeType("PoeClone.World.WorldBuildScheduler").GetMethod("Run")
                .Invoke(null, new object[] { routine, double.MaxValue });

        private static IEnumerator ChildRoutine(List<string> log, object wait, bool fail)
        {
            try
            {
                log.Add("child started");
                yield return wait;
                if (fail) throw new InvalidOperationException("construction failed");
                log.Add("child finished");
            }
            finally { log.Add("child disposed"); }
        }

        private static IEnumerator ParentRoutine(List<string> log, object wait, bool fail)
        {
            try
            {
                yield return ChildRoutine(log, wait, fail);
                log.Add("parent finished");
            }
            finally { log.Add("parent disposed"); }
        }

        [Test]
        public void WorldConstructionPreservesWaitsAndDisposesNestedRoutines()
        {
            var log = new List<string>();
            var wait = new WaitForSecondsRealtime(1f);
            IEnumerator scheduled = Schedule(ParentRoutine(log, wait, false));
            Assert.IsTrue(scheduled.MoveNext());
            Assert.AreSame(wait, scheduled.Current);
            CollectionAssert.AreEqual(new[] { "child started" }, log);
            Assert.IsFalse(scheduled.MoveNext());
            CollectionAssert.AreEqual(new[] { "child started", "child finished", "child disposed", "parent finished", "parent disposed" }, log);
        }

        [Test]
        public void CancellingWorldConstructionDisposesBothActiveRoutines()
        {
            var log = new List<string>();
            IEnumerator scheduled = Schedule(ParentRoutine(log, new WaitForSecondsRealtime(1f), false));
            Assert.IsTrue(scheduled.MoveNext());
            ((IDisposable)scheduled).Dispose();
            CollectionAssert.AreEqual(new[] { "child started", "child disposed", "parent disposed" }, log);
        }

        [Test]
        public void FailedWorldConstructionPropagatesTheErrorAndDisposesBothRoutines()
        {
            var log = new List<string>();
            IEnumerator scheduled = Schedule(ParentRoutine(log, null, true));
            Assert.Throws<InvalidOperationException>(() => scheduled.MoveNext());
            CollectionAssert.AreEqual(new[] { "child started", "child disposed", "parent disposed" }, log);
        }

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
        public void EachCooldownRestoresOneCharge()
        {
            object bindings = Activator.CreateInstance(RuntimeType("PoeClone.Skills.SkillBindings"), new object[] { 1 });
            object dash = Enum.Parse(RuntimeType("PoeClone.Skills.SkillId"), "Dash");
            var item = new ItemData("Boots", ItemType.Boots, 1, 1, Color.white);
            Call(bindings, "SetSource", 0, dash, item, EquipSlot.Boots, 1);
            Call(bindings, "StartCooldown", 0, 4f, 10f);
            Call(bindings, "StartCooldown", 0, 4f, 10f);

            Assert.AreEqual(18f, Field(Binding(bindings, 0), "ReadyAt"));
            Assert.AreEqual(1, Call(bindings, "ChargesAt", 0, 3, 10f));
            Assert.AreEqual(2, Call(bindings, "ChargesAt", 0, 3, 15f));
            Assert.AreEqual(3, Call(bindings, "ChargesAt", 0, 3, 18f));
            Assert.AreEqual(3f, Call(bindings, "RechargeLeftAt", 0, 15f));
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
