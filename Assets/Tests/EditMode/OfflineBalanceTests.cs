using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    /// <summary>Reflection keeps the runtime test assembly independent of Assembly-CSharp-Editor.
    /// These tests stay in Edit mode; temporary test objects are always destroyed.</summary>
    public class OfflineBalanceTests
    {
        private static Type EditorType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("PoeClone.EditorTools." + name)).First(t => t != null);
        private static object Call(string method, params object[] args) => EditorType("OfflineBalance").GetMethod(method).Invoke(null, args);

        private static float SingleTarget(string skill, ItemData weapon, StatSheet sheet, bool cull = false)
        {
            var book = Type.GetType("PoeClone.Skills.SkillBook, Assembly-CSharp", true);
            var skillId = Type.GetType("PoeClone.Skills.SkillId, Assembly-CSharp", true);
            var model = Type.GetType("PoeClone.World.SingleTargetDamage, Assembly-CSharp", true);
            object definition = book.GetMethod("Get").Invoke(null, new[] { Enum.Parse(skillId, skill) });
            return (float)model.GetMethod("Estimate").Invoke(null,
                new object[] { definition, weapon, sheet, new WeaponSkillBonuses(weapon), 0f, 0f, 0f, cull });
        }

        private static ItemData DamageWeapon(string baseId, StatType grant, string prefix, int points)
        {
            var item = ItemGenerator.Generate(new Random(1), baseId, 21, ItemRarity.Normal, grant);
            item.SkillProgress = new WeaponSkillProgress { level = 10 };
            for (int i = 0; i < points; i++) Assert.IsTrue(item.SkillProgress.Allocate(grant, prefix + i));
            return item;
        }

        private static (float hit, float dot, float uses) ComboDps(string skill, ItemData weapon, StatSheet sheet, int step = -1)
        {
            var book = Type.GetType("PoeClone.Skills.SkillBook, Assembly-CSharp", true);
            var ids = Type.GetType("PoeClone.Skills.SkillId, Assembly-CSharp", true);
            var model = Type.GetType("PoeClone.World.SingleTargetDamage, Assembly-CSharp", true);
            var definition = book.GetMethod("Get").Invoke(null, new[] { Enum.Parse(ids, skill) });
            object[] args = step < 0
                ? new object[] { definition, weapon, sheet, new WeaponSkillBonuses(weapon), 0f, 0f, 0f, false }
                : new object[] { definition, weapon, sheet, new WeaponSkillBonuses(weapon), step, 0f, 0f, 0f, false };
            var method = step < 0 ? model.GetMethod("Estimate") : model.GetMethod("EstimateStep", BindingFlags.Static | BindingFlags.NonPublic);
            method.Invoke(null, args);
            int offset = step < 0 ? 4 : 5;
            return ((float)args[offset], (float)args[offset + 1], (float)args[offset + 2]);
        }

        [TestCase("Cleave", 1.175f)]
        [TestCase("Pulverize", 1f)]
        [TestCase("Fissure", 1.25f)]
        [TestCase("Aftershock", 1.25f)]
        [TestCase("ReapingArc", 1.25f)]
        [TestCase("LungingThrust", 1.1875f)]
        [TestCase("CrossSlash", 1.1875f)]
        [TestCase("FangStrike", 1.15f)]
        [TestCase("VenomSlash", 1.15f)]
        [TestCase("Whirlwind", 1f)]
        public void SingleTarget_AssumesCompleteSuccessfulCombos(string skill, float factor)
        {
            var weapon = DamageWeapon("rusty_sword", StatType.GrantCleave, "combo_", 0);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30).Set(StatType.ArmourPenetration, 100), null);
            var baseline = ComboDps(skill, weapon, sheet, step: 0);
            var combo = ComboDps(skill, weapon, sheet);
            int length = WeaponSkillRules.ComboLength((StatType)Enum.Parse(typeof(StatType), "Grant" + skill));
            float followupSpeed = WeaponSkillRules.ComboFollowupSpeed((StatType)Enum.Parse(typeof(StatType), "Grant" + skill));
            float cadence = length > 0 ? length / (1f + (length - 1f) / followupSpeed) : 1f;
            Assert.AreEqual(baseline.hit * factor * cadence, combo.hit, .001f);
            Assert.AreEqual(baseline.uses * cadence, combo.uses, .001f, "Complete combos include one normal interval after each finisher");
            if (skill == "FangStrike") Assert.AreEqual(baseline.dot * factor * cadence, combo.dot, .001f);
            if (skill == "VenomSlash") Assert.AreEqual(baseline.dot * cadence, combo.dot, .001f, "Built-in poison gains frequency but not combo hit damage");
        }

        [Test]
        public void SingleTarget_CleaveExplosionChangesOnlyFourthHitAndCadence()
        {
            var weapon = DamageWeapon("rusty_sword", StatType.GrantCleave, "v2_0_", 5);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30).Set(StatType.ArmourPenetration, 100), null);
            var plainHit = ComboDps("Cleave", weapon, sheet, step: 0);
            Assert.IsTrue(weapon.SkillProgress.Allocate(StatType.GrantCleave, "v2_0_5"));
            float cadence = 1.5f;
            Assert.AreEqual(plainHit.hit * cadence * (1f + 1.35f + 1f + 5.5f) / 4f,
                ComboDps("Cleave", weapon, sheet).hit, .001f);
        }

        [Test]
        public void Cleave_KeystonesHaveTwoTravelNodesAfterNotablesAndCannotBeCombined()
        {
            var nodes = WeaponSkillTrees.For(StatType.GrantCleave);
            Assert.AreEqual(19, nodes.Count);
            foreach (var keystone in nodes.Where(n => n.Keystone))
            {
                var parent = WeaponSkillTrees.Find(StatType.GrantCleave, keystone.Parent);
                Assert.IsFalse(parent.Notable);
                parent = WeaponSkillTrees.Find(StatType.GrantCleave, parent.Parent);
                Assert.IsFalse(parent.Notable);
                parent = WeaponSkillTrees.Find(StatType.GrantCleave, parent.Parent);
                Assert.IsTrue(parent.Notable);
            }
            foreach (int first in new[] { 0, 1 })
            {
                var progress = new WeaponSkillProgress { level = 10 };
                for (int i = 0; i < 6; i++) Assert.IsTrue(progress.Allocate(StatType.GrantCleave, "v2_" + first + "_" + i));
                for (int i = 0; i < 4; i++) Assert.IsTrue(progress.Allocate(StatType.GrantCleave, "v2_" + (1 - first) + "_" + i));
                Assert.IsFalse(progress.Allocate(StatType.GrantCleave, "v2_" + (1 - first) + "_4"));
            }
            Assert.IsFalse(nodes.SelectMany(n => n.Bonuses).Any(b => b.Effect == WeaponSkillEffect.MoreDamage ||
                b.Effect == WeaponSkillEffect.Area || b.Effect == WeaponSkillEffect.ComboArea || b.Effect == WeaponSkillEffect.ComboKnockback));
        }

        [Test]
        public void Cleave_OldAllocationsRefundWithoutLosingWeaponLevel()
        {
            var progress = new WeaponSkillProgress { version = 2, level = 10, nodes = new List<string> { "v2_0_0", "combo_0" } };
            progress.Sanitize(StatType.GrantCleave);
            Assert.AreEqual(10, progress.level);
            Assert.AreEqual(10, progress.Available);
            Assert.AreEqual(3, progress.version);
        }

        [TestCase(0f)]
        [TestCase(100f)]
        [TestCase(200f)]
        public void Cleave_ExplosionDamageRewardsItsLongerCommitment(float attackSpeed)
        {
            var explosion = DamageWeapon("rusty_sword", StatType.GrantCleave, "v2_0_", 6);
            var agile = DamageWeapon("rusty_sword", StatType.GrantCleave, "v2_1_", 6);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30).Set(StatType.AttackSpeed, attackSpeed), null);
            float ratio = SingleTarget("Cleave", explosion, sheet) / SingleTarget("Cleave", agile, sheet);
            Assert.That(ratio, Is.InRange(1.2f, 1.85f), "Landing the slow finisher should reward its greater commitment");
            Assert.AreEqual(2f, WeaponSkillRules.ComboFollowupSpeed(StatType.GrantCleave, doubleSlash: true));
            Assert.AreEqual(2, WeaponSkillRules.ComboLength(StatType.GrantCleave, true));
        }

        [Test]
        public void SingleTarget_MitigatesEachComboHitBeforeAveraging()
        {
            var weapon = DamageWeapon("rusty_sword", StatType.GrantCleave, "combo_", 0);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            float expected = 0;
            for (int step = 1; step <= 4; step++) expected += ComboDps("Cleave", weapon, sheet, step).hit / 4f;
            Assert.AreEqual(expected, ComboDps("Cleave", weapon, sheet).hit, .001f);
            Assert.Greater(expected, ComboDps("Cleave", weapon, sheet, step: 0).hit * 1.175f * (4f / (1f + 3f / 2.25f)),
                "Heavy hits lose a smaller fraction of damage to armour");
        }

        [TestCase(-50f)]
        [TestCase(0f)]
        [TestCase(50f)]
        [TestCase(200f)]
        public void SingleTarget_FastComboIntervalStillScalesWithAttackSpeed(float attackSpeed)
        {
            var weapon = DamageWeapon("rusty_sword", StatType.GrantCleave, "combo_", 0);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30).Set(StatType.AttackSpeed, attackSpeed), null);
            float normalUses = ComboDps("Cleave", weapon, sheet, step: 0).uses;
            float comboUses = ComboDps("Cleave", weapon, sheet).uses;
            Assert.AreEqual(normalUses * (4f / (1f + 3f / 2.25f)), comboUses, .001f);
            Assert.AreEqual((1f / normalUses + 3f / (normalUses * 2.25f)) / 4f, 1f / comboUses, .001f);
        }

        [Test]
        public void SingleTarget_MageRallyIsTwentyPercentMoreAfterRepeatedSummons()
        {
            var weapon = DamageWeapon("bone_sceptre", StatType.GrantSkeletonMages, "v2_0_", 2);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.CastSpeed, 50), null);
            float before = SingleTarget("SkeletonMages", weapon, sheet);
            Assert.IsTrue(weapon.SkillProgress.Allocate(StatType.GrantSkeletonMages, "v2_0_2"));
            Assert.AreEqual(before * 1.2f, SingleTarget("SkeletonMages", weapon, sheet), .001f);
        }

        [TestCase("SpiritWolves", "bone_sceptre", StatType.GrantSpiritWolves, "v2_0_", 10f)]
        public void SingleTarget_CullingSkipsLifeOnlyForNonBossEstimates(string skill, string baseId, StatType grant, string prefix, float threshold)
        {
            var weapon = DamageWeapon(baseId, grant, prefix, 5);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            float before = SingleTarget(skill, weapon, sheet);
            Assert.IsTrue(weapon.SkillProgress.Allocate(grant, prefix + 5));
            Assert.AreEqual(before, SingleTarget(skill, weapon, sheet), .001f, "This execute does not affect bosses");
            Assert.AreEqual(before / (1 - threshold / 100), SingleTarget(skill, weapon, sheet, true), .001f);
        }

        [Test]
        public void SingleTarget_OpeningDamageDoesNotInflateSustainedDamage()
        {
            var weapon = DamageWeapon("rusty_sword", StatType.GrantCleave, "v2_0_", 0);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            float before = SingleTarget("Cleave", weapon, sheet);
            Assert.IsTrue(weapon.SkillProgress.Allocate(StatType.GrantCleave, "v2_2_2"));
            Assert.AreEqual(before, SingleTarget("Cleave", weapon, sheet), .001f);
        }

        [Test]
        public void SingleTarget_CurseOnHitAffectsPlayerHitsButNotMinionHits()
        {
            var basis = new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30);
            var plain = StatSheet.Build(basis, null);
            var cursed = StatSheet.Build(basis.Set(StatType.CurseOnHit, 100), null);
            var staff = DamageWeapon("gnarled_staff", StatType.GrantFireBolt, "v2_0_", 0);
            var wand = DamageWeapon("bone_sceptre", StatType.GrantSkeletonMages, "v2_0_", 0);
            Assert.AreEqual(SingleTarget("FireBolt", staff, plain) * 1.15f, SingleTarget("FireBolt", staff, cursed), .001f);
            Assert.AreEqual(SingleTarget("SkeletonMages", wand, plain), SingleTarget("SkeletonMages", wand, cursed), .001f);
        }

        [Test]
        public void SingleTarget_MarchingHordeSpeedsAttackingMinionsOnly()
        {
            var basis = new BaseStats { Level = 21 };
            var plain = StatSheet.Build(basis, null);
            var horde = StatSheet.Build(basis.Set(StatType.MinionHordeSpeed, 10), null);
            var wand = DamageWeapon("bone_sceptre", StatType.GrantSkeletonMages, "v2_0_", 0);
            Assert.AreEqual(SingleTarget("SkeletonMages", wand, plain), SingleTarget("SkeletonMages", wand, horde), .001f);
            Assert.Greater(SingleTarget("SpiritWolves", wand, horde), SingleTarget("SpiritWolves", wand, plain));
        }

        [Test]
        public void ArenaCatalogue_CleaveRoutesUseCurrentNodesAndCreateLoadouts()
        {
            var builds = Type.GetType("PoeClone.World.BalanceArenaBuilds, Assembly-CSharp", true);
            var data = builds.GetProperty("Data").GetValue(null);
            var entries = ((IEnumerable)data.GetType().GetField("builds").GetValue(data)).Cast<object>()
                .Where(e => (string)e.GetType().GetField("skill").GetValue(e) == "Cleave").ToArray();
            var names = new HashSet<string>();
            foreach (var entry in entries)
            {
                var type = entry.GetType();
                string name = (string)type.GetField("name").GetValue(entry);
                names.Add(name);
                int rank = (int)type.GetField("weaponRank").GetValue(entry);
                var progress = new WeaponSkillProgress { level = rank };
                foreach (string id in ((string)type.GetField("weaponNodes").GetValue(entry)).Split(','))
                    Assert.IsTrue(progress.Allocate(StatType.GrantCleave, id), name + " has stale or illegal node " + id);
                Assert.AreEqual(0, progress.Available, "Recorded routes should spend their weapon points");
            }
            CollectionAssert.AreEquivalent(WeaponSkillTrees.Routes(StatType.GrantCleave).Select(r => r.Name), names);
            foreach (var group in entries.GroupBy(e => (string)e.GetType().GetField("id").GetValue(e)))
            {
                var ordered = group.OrderBy(e => (int)e.GetType().GetField("level").GetValue(e)).ToArray();
                foreach (var entry in new[] { ordered.First(), ordered.Last() })
                {
                    // Exercise the same loadout creation and damage preview that builds the picker.
                    Assert.DoesNotThrow(() => builds.GetMethod("CreateKit").Invoke(null, new object[] { entry, new Random(1) }));
                    Assert.Greater((float)builds.GetMethod("EstimatedDps").Invoke(null, new object[] { entry, false }), 0f);
                }
            }
        }

        [Test]
        public void ArenaCatalogue_GeneratesCurrentRoutesFromObsoleteSampleNodes()
        {
            var builds = Type.GetType("PoeClone.World.BalanceArenaBuilds, Assembly-CSharp", true);
            var entryType = builds.GetNestedType("Entry");
            var catalogueType = builds.GetNestedType("Catalogue");
            var template = Activator.CreateInstance(entryType);
            foreach (var pair in new[] { ("id", "Cleave/obsolete"), ("skill", "Cleave"), ("name", "Deleted route"),
                ("family", "Melee"), ("weaponNodes", "deleted_node") }) entryType.GetField(pair.Item1).SetValue(template, pair.Item2);
            entryType.GetField("level").SetValue(template, 34);
            entryType.GetField("weaponRank").SetValue(template, 10);
            var entries = Array.CreateInstance(entryType, 1);
            entries.SetValue(template, 0);
            var samples = Activator.CreateInstance(catalogueType);
            catalogueType.GetField("builds").SetValue(samples, entries);
            var generated = builds.GetMethod("Generate").Invoke(null, new[] { samples });
            var actual = ((IEnumerable)catalogueType.GetField("builds").GetValue(generated)).Cast<object>().ToArray();
            CollectionAssert.AreEquivalent(WeaponSkillTrees.Routes(StatType.GrantCleave).Select(r => r.Name),
                actual.Select(e => (string)entryType.GetField("name").GetValue(e)));
            foreach (var entry in actual)
            {
                var progress = new WeaponSkillProgress { level = 10 };
                foreach (string id in ((string)entryType.GetField("weaponNodes").GetValue(entry)).Split(','))
                    Assert.IsTrue(progress.Allocate(StatType.GrantCleave, id));
                Assert.AreEqual(0, progress.Available);
            }
        }

        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        public void Cleave_ComboVariantsCanTakeEitherOpeningEdgeEnding(int variant, bool echo)
        {
            var progress = new WeaponSkillProgress { level = 10 };
            for (int i = 0; i < 6; i++) Assert.IsTrue(progress.Allocate(StatType.GrantCleave, "v2_" + variant + "_" + i));
            foreach (int i in echo ? new[] { 2, 6, 7, 8 } : new[] { 2, 3, 4, 5 })
                Assert.IsTrue(progress.Allocate(StatType.GrantCleave, "v2_2_" + i));
            Assert.AreEqual(0, progress.Available);
        }

        [Test]
        public void Cleave_HeadsmanExecutesBossesAndEchoHasComparableSustainedBenefit()
        {
            var headsman = DamageWeapon("rusty_sword", StatType.GrantCleave, "", 0);
            var echo = DamageWeapon("rusty_sword", StatType.GrantCleave, "", 0);
            foreach (int i in new[] { 2, 3, 4 }) Assert.IsTrue(headsman.SkillProgress.Allocate(StatType.GrantCleave, "v2_2_" + i));
            foreach (int i in new[] { 2, 6, 7 }) Assert.IsTrue(echo.SkillProgress.Allocate(StatType.GrantCleave, "v2_2_" + i));
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            float baseline = SingleTarget("Cleave", headsman, sheet);
            Assert.IsTrue(headsman.SkillProgress.Allocate(StatType.GrantCleave, "v2_2_5"));
            Assert.IsTrue(echo.SkillProgress.Allocate(StatType.GrantCleave, "v2_2_8"));
            Assert.AreEqual(20f, new WeaponSkillBonuses(headsman).CullPercent(boss: true));
            Assert.AreEqual(baseline * 1.25f, SingleTarget("Cleave", headsman, sheet), .001f);
            Assert.AreEqual(SingleTarget("Cleave", headsman, sheet), SingleTarget("Cleave", echo, sheet), .001f);
        }

        [Test]
        public void Catalogue_CoversEverySkillAndEveryDestination_WithLegalBudgets()
        {
            var catalogue = (IEnumerable)EditorType("BalanceArchetypes").GetMethod("Catalogue").Invoke(null, null);
            var skills = new HashSet<string>();
            var destinations = new HashSet<string>();
            var identities = new HashSet<string>();
            foreach (var archetype in catalogue)
            {
                var type = archetype.GetType();
                string skill = (string)type.GetField("skill").GetValue(archetype);
                string id = (string)type.GetField("id").GetValue(archetype);
                Assert.IsTrue(identities.Add(id), "Duplicate " + id);
                skills.Add(skill);
                string[] nodes = (string[])type.GetField("requiredNodes").GetValue(archetype);
                int budget = (int)type.GetField("minimumWeaponPoints").GetValue(archetype);
                Assert.AreEqual(nodes.Length, budget);
                Assert.LessOrEqual(budget, 10);
                if (nodes.Length == 0) continue;
                StatType grant = (StatType)Enum.Parse(typeof(StatType), "Grant" + skill);
                var progress = new WeaponSkillProgress { level = 10 };
                foreach (string node in nodes)
                {
                    Assert.IsTrue(progress.Allocate(grant, node), id + " illegal at " + node);
                    if (WeaponSkillTrees.Find(grant, node).Keystone) destinations.Add(skill + "/" + node);
                }
            }
            // SkillId resides in the runtime assembly; read its actual enum rather than
            // duplicating a count that silently misses newly added skills.
            var skillType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillId")).First(t => t != null);
            CollectionAssert.AreEquivalent(Enum.GetNames(skillType), skills);
            foreach (string skill in skills)
                if (Enum.TryParse("Grant" + skill, out StatType grant) && WeaponSkillTrees.For(grant) != null)
                    foreach (var node in WeaponSkillTrees.For(grant).Where(n => n.Keystone))
                        Assert.Contains(skill + "/" + node.Id, destinations.ToList());
        }

        [Test]
        public void ResourceMath_SeparatesBurstPoolFromInfiniteSustain()
        {
            Assert.AreEqual(10f, (float)Call("TimeToEmpty", 100f, 12f, 2f), .0001);
            Assert.AreEqual(-1f, (float)Call("TimeToEmpty", 100f, 2f, 2f));
            Assert.AreEqual(.5f, (float)Call("UseFraction", 80f, 2f, 12f, 20f), .0001);
            Assert.AreEqual(1f, (float)Call("UseFraction", 0f, 0f, 0f, 30f));
            Assert.AreEqual(0f, (float)Call("ChanceUptime", 0f, 10f, 3f));
            Assert.AreEqual(1f, (float)Call("ChanceUptime", 100f, 1f, 3f));
        }

        [Test]
        public void Pulverize_FourBranchesHaveTwoSmallNodesBeforeEveryNotable()
        {
            foreach (var grant in new[] { StatType.GrantPulverize, StatType.GrantCleave })
            {
                var nodes = WeaponSkillTrees.For(grant);
                Assert.AreEqual(20, nodes.Count);
                Assert.AreEqual(4, nodes.Count(n => n.Keystone));
                for (int lane = 0; lane < 4; lane++)
                {
                    var path = nodes.Where(n => n.Id.StartsWith("v2_" + lane + "_")).OrderBy(n => n.Depth).ToArray();
                    Assert.AreEqual(lane < 2 ? 6 : 4, path.Length);
                    Assert.IsNull(path[0].Parent);
                    Assert.IsFalse(path[0].Notable || path[1].Notable);
                    Assert.IsTrue(path[2].Notable);
                    Assert.IsTrue(path.Last().Keystone);
                    Assert.IsFalse(path.Any(n => n.Id.StartsWith("combo_")));
                }
            }
            foreach (int longLane in new[] { 0, 1 })
                foreach (int shortLane in new[] { 2, 3 })
                {
                    var progress = new WeaponSkillProgress { level = 10 };
                    foreach (int lane in new[] { longLane, shortLane })
                        for (int i = 0; i < (lane < 2 ? 6 : 4); i++)
                            Assert.IsTrue(progress.Allocate(StatType.GrantPulverize, "v2_" + lane + "_" + i));
                    Assert.AreEqual(0, progress.Available);
                }
            var limited = new WeaponSkillProgress { level = 10 };
            for (int i = 0; i < 6; i++) Assert.IsTrue(limited.Allocate(StatType.GrantPulverize, "v2_0_" + i));
            for (int i = 0; i < 4; i++) Assert.IsTrue(limited.Allocate(StatType.GrantPulverize, "v2_1_" + i));
            Assert.IsFalse(limited.Allocate(StatType.GrantPulverize, "v2_1_4"));
        }

        [Test]
        public void Pulverize_ReworkRefundsOldAllocationsWithoutLosingProgress()
        {
            var progress = new WeaponSkillProgress { version = 5, level = 7, experience = 123,
                nodes = new List<string> { "v2_0_0", "v2_0_1", "v2_0_2" } };
            progress.Sanitize(StatType.GrantPulverize);
            Assert.AreEqual(7, progress.version);
            Assert.AreEqual(7, progress.level);
            Assert.AreEqual(123, progress.experience);
            Assert.AreEqual(7, progress.Available);
            Assert.IsTrue(progress.Allocate(StatType.GrantPulverize, "v2_3_0"));
            progress.Sanitize(StatType.GrantPulverize);
            Assert.AreEqual(1, progress.nodes.Count, "Updated saves keep their allocations");
        }

        [Test]
        public void Pulverize_SpecializationsOwnTheirDamageAndCadence()
        {
            const StatType grant = StatType.GrantPulverize;
            Assert.AreEqual(3, WeaponSkillRules.ComboLength(grant));
            for (int step = 1; step <= 3; step++)
            {
                Assert.AreEqual(1f, WeaponSkillRules.ComboHitMultiplier(grant, step));
                Assert.AreEqual(step == 3 ? 3f : 1f, WeaponSkillRules.ComboHitMultiplier(grant, step, worldshaker: true));
            }
            Assert.AreEqual(2, WeaponSkillRules.ComboLength(grant, twinHammer: true));
            Assert.AreEqual(1.3f, WeaponSkillRules.ComboHitMultiplier(grant, 1, twinHammer: true));
            Assert.AreEqual(1.3f, WeaponSkillRules.ComboHitMultiplier(grant, 2, twinHammer: true));
            Assert.AreEqual(0.75f, WeaponSkillRules.ComboRecoveryMultiplier(grant, true));
            Assert.Greater(WeaponSkillRules.ComboSustainedSpeed(grant, twinHammer: true), WeaponSkillRules.ComboSustainedSpeed(grant));
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            foreach (int lane in new[] { 0, 1 })
            {
                var weapon = DamageWeapon("iron_mace", grant, "v2_" + lane + "_", 5);
                float before = ComboDps("Pulverize", weapon, sheet).hit;
                Assert.IsTrue(weapon.SkillProgress.Allocate(grant, "v2_" + lane + "_5"));
                Assert.Greater(ComboDps("Pulverize", weapon, sheet).hit, before);
            }
        }

        [Test]
        public void Pulverize_FinishersStunNormalEnemiesButNotBosses()
        {
            var type = Type.GetType("PoeClone.Enemies.EnemyHealth, Assembly-CSharp", true);
            var target = new UnityEngine.GameObject("Finisher stun test");
            try
            {
                var enemy = target.AddComponent(type);
                var kinds = Type.GetType("PoeClone.Enemies.EnemyKinds, Assembly-CSharp", true);
                var roster = (Array)kinds.GetField("All").GetValue(null);
                type.GetMethod("Configure").Invoke(enemy, new object[] { 0, roster.GetValue(0), 1 });
                var hit = type.GetMethods().Single(m => m.Name == "TakeDamage" && m.GetParameters().Length > 4);
                var parameters = hit.GetParameters();
                var args = parameters.Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue :
                    parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray();
                args[0] = 1f;
                args[1] = Enum.Parse(parameters[1].ParameterType, "Physical");
                args[Array.FindIndex(parameters, parameter => parameter.Name == "stunningHit")] = true;
                args[Array.FindIndex(parameters, parameter => parameter.Name == "playImpactSound")] = false;
                Assert.Greater((float)hit.Invoke(enemy, args), 0f);
                var staggerType = Type.GetType("PoeClone.Combat.Stagger, Assembly-CSharp", true);
                var stagger = target.GetComponent(staggerType);
                Assert.IsNotNull(stagger);
                Assert.IsTrue((bool)staggerType.GetProperty("IsStaggered").GetValue(stagger));
                float until = (float)staggerType.GetField("stunnedUntil", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stagger);
                Assert.Greater(until, UnityEngine.Time.time + 1.9f);
                int triggers = (int)staggerType.GetProperty("TriggerCount").GetValue(stagger);
                for (int i = 0; i < roster.Length; i++)
                {
                    object kind = roster.GetValue(i);
                    if (!(bool)kind.GetType().GetField("IsBoss").GetValue(kind)) continue;
                    type.GetMethod("Configure").Invoke(enemy, new object[] { i, kind, 1 });
                    hit.Invoke(enemy, args);
                    Assert.AreEqual(triggers, staggerType.GetProperty("TriggerCount").GetValue(stagger), "Bosses cannot be stunned by Worldshaker");
                    break;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test]
        public void Pulverize_PerfectImpactRewardsOnlyTheInnerHalfAndUpdatesDps()
        {
            Assert.AreEqual(1.3f, WeaponSkillRules.PerfectImpactMultiplier(0f, 4f));
            Assert.AreEqual(1.3f, WeaponSkillRules.PerfectImpactMultiplier(2f, 4f));
            Assert.AreEqual(1f, WeaponSkillRules.PerfectImpactMultiplier(2.001f, 4f));
            Assert.AreEqual(1f, WeaponSkillRules.PerfectImpactMultiplier(0f, 0f));
            Assert.AreEqual(1.3f, WeaponSkillRules.PerfectImpactMultiplier(2.7f, 5.4f));
            const StatType grant = StatType.GrantPulverize;
            var weapon = DamageWeapon("iron_mace", grant, "v2_3_", 3);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            float before = ComboDps("Pulverize", weapon, sheet).hit;
            Assert.IsTrue(weapon.SkillProgress.Allocate(grant, "v2_3_3"));
            Assert.AreEqual(before * 1.3f, ComboDps("Pulverize", weapon, sheet).hit, .001f);
            Assert.AreEqual("Bonecrusher", WeaponSkillTrees.Find(grant, "v2_2_3").Name);
            Assert.AreEqual("Perfect Impact", WeaponSkillTrees.Find(grant, "v2_3_3").Name);
        }

        [Test]
        public void Pulverize_BonecrusherBiasesFourShardsAndKeepsFreeScatter()
        {
            var origin = UnityEngine.Vector3.zero;
            var targets = new[] { UnityEngine.Vector3.forward * 4f, UnityEngine.Vector3.back * 3f };
            var directions = WeaponSkillRules.BonecrusherDirections(origin, targets, 72);
            var repeated = WeaponSkillRules.BonecrusherDirections(origin, targets, 72);
            CollectionAssert.AreEqual(directions, repeated);
            Assert.AreEqual(6, directions.Length);
            for (int i = 0; i < 4; i++)
            {
                float angle = Math.Min(UnityEngine.Vector3.Angle(directions[i], targets[0]), UnityEngine.Vector3.Angle(directions[i], targets[1]));
                Assert.LessOrEqual(angle, 10.01f);
            }
            int freelyScattered = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var scatter = WeaponSkillRules.BonecrusherDirections(origin, targets, seed);
                for (int i = 4; i < 6; i++)
                    if (UnityEngine.Vector3.Angle(scatter[i], targets[0]) > 15f && UnityEngine.Vector3.Angle(scatter[i], targets[1]) > 15f)
                        freelyScattered++;
            }
            Assert.Greater(freelyScattered, 20);
            foreach (var direction in WeaponSkillRules.BonecrusherDirections(origin, new UnityEngine.Vector3[0], 72))
            {
                Assert.AreEqual(1f, direction.magnitude, .0001f);
                Assert.AreEqual(0f, direction.y, .0001f);
            }
            // The on-kill specialization contributes no fictitious sustained boss DPS.
            var weapon = DamageWeapon("iron_mace", StatType.GrantPulverize, "v2_2_", 3);
            var sheet = StatSheet.Build(new BaseStats { Level = 21 }.Set(StatType.PhysicalDamage, 30), null);
            float before = ComboDps("Pulverize", weapon, sheet).hit;
            Assert.IsTrue(weapon.SkillProgress.Allocate(StatType.GrantPulverize, "v2_2_3"));
            Assert.AreEqual(before, ComboDps("Pulverize", weapon, sheet).hit, .001f);
        }

        [Test]
        public void SkillModel_UsesReleaseRecovery_AndCombatDamageTypes()
        {
            Assert.AreEqual(1.95f, (float)Call("PiercingInterval", 1f, 0f), .0001);
            Assert.AreEqual(1.175f, (float)Call("PiercingInterval", 2f, 25f), .0001);
            Assert.AreEqual(.72f, (float)Call("PiercingInterval", 5f, 0f), .0001);
            var book = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillBook")).First(t => t != null);
            var idType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillId")).First(t => t != null);
            foreach (var expected in new[] { new[] { "VenomSlash", "Physical" }, new[] { "BurningArrow", "Fire" }, new[] { "VenomSpout", "Poison" }, new[] { "RainOfArrows", "Physical" } })
            {
                object skill = book.GetMethod("Get").Invoke(null, new[] { Enum.Parse(idType, expected[0]) });
                Assert.AreEqual(expected[1], Call("BaseElement", skill).ToString());
            }
        }

        [TestCase(1, 1, 2f, 1f, 2.25f)]
        [TestCase(8, 1, 2f, 1f, 4f)]
        [TestCase(8, 3, 2f, 1f, 12f)]
        [TestCase(8, 1, 2f, 3f, 13f)]
        [TestCase(1, 2, 0f, 1f, 12f)]
        public void RainOverlapIncludesOneGuaranteedCentralArrowPerSalvo(int arrows, int salvos, float spread, float radius, float expected)
        {
            var skills = Type.GetType("PoeClone.Skills.PlayerSkills, Assembly-CSharp", true);
            float hits = (float)skills.GetMethod("ExpectedRainHits").Invoke(null, new object[] { arrows, salvos, spread, radius });
            Assert.AreEqual(expected, hits, .0001f);
        }

        [TestCase(5, false)]
        [TestCase(6, true)]
        public void CloudburstKeepsItsSixPointApproachAndExtraArrowsWithADamageTradeoff(int rank, bool canFinish)
        {
            const StatType grant = StatType.GrantRainOfArrows;
            ItemData bow = ItemGenerator.Generate(new Random(1), "short_bow", 1, ItemRarity.Normal, grant);
            bow.SkillProgress = new WeaponSkillProgress { level = rank };
            for (int i = 0; i < 5; i++) Assert.IsTrue(bow.SkillProgress.Allocate(grant, "v2_0_" + i));
            var before = new WeaponSkillBonuses(bow);
            Assert.AreEqual(3, before.ExtraProjectiles);
            Assert.AreEqual(1f, before.MoreDamageMultiplier);
            Assert.AreEqual(canFinish, bow.SkillProgress.Allocate(grant, "v2_0_5"));
            var after = new WeaponSkillBonuses(bow);
            Assert.AreEqual(canFinish ? 7 : 3, after.ExtraProjectiles);
            Assert.AreEqual(canFinish ? .65f : 1f, after.MoreDamageMultiplier, .0001f);
            Assert.AreEqual(before.Get(WeaponSkillEffect.Area), after.Get(WeaponSkillEffect.Area));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void BurningArrowDevelopingRoutesEarnDamageBeforeTheirDestinations(int lane)
        {
            const StatType grant = StatType.GrantBurningArrow;
            ItemData bow = ItemGenerator.Generate(new Random(1), "short_bow", 1, ItemRarity.Normal, grant);
            bow.SkillProgress = new WeaponSkillProgress { level = 4 };
            for (int i = 0; i < 4; i++) Assert.IsTrue(bow.SkillProgress.Allocate(grant, "v2_" + lane + "_" + i));
            var tree = new WeaponSkillBonuses(bow);
            float notable = lane == 0 ? 1.2f : 1f;
            Assert.AreEqual((float)Math.Pow(1.07, 3) * notable, tree.MoreDamageMultiplier, .0001f);
            Assert.AreEqual(0f, tree.Get(WeaponSkillEffect.Cost), "Damage redistribution must not change mana costs");
            Assert.IsFalse(bow.SkillProgress.nodes.Any(id => WeaponSkillTrees.Find(grant, id).Keystone));
        }

        [Test]
        public void BurningArrowCompletedIgnitionRouteKeepsItsPreviousDamageBudget()
        {
            const StatType grant = StatType.GrantBurningArrow;
            ItemData bow = ItemGenerator.Generate(new Random(1), "short_bow", 1, ItemRarity.Normal, grant);
            bow.SkillProgress = new WeaponSkillProgress { level = 8 };
            foreach (string id in new[] { "v2_0_0", "v2_0_1", "v2_0_2", "v2_0_3", "v2_0_4", "v2_0_5", "v2_0_6", "v2_0_7" })
                Assert.IsTrue(bow.SkillProgress.Allocate(grant, id));
            var tree = new WeaponSkillBonuses(bow);
            float previousMultiplier = 1.2f * 1.45f * 1.25f;
            Assert.That(tree.MoreDamageMultiplier / previousMultiplier, Is.InRange(.98f, 1.05f));
            Assert.AreEqual(40f, tree.Get(WeaponSkillEffect.Ignite));
            Assert.AreEqual(120f, tree.Get(WeaponSkillEffect.Duration));
        }

        [TestCase(3, 55f, 30f)]
        [TestCase(6, 90f, 60f)]
        public void VenomArrowPoisonInvestmentRequiresItsConnectedNotable(int rank, float expectedPoison, float expectedDuration)
        {
            const StatType grant = StatType.GrantVenomArrow;
            ItemData bow = ItemGenerator.Generate(new Random(1), "short_bow", 1, ItemRarity.Normal, grant);
            bow.SkillProgress = new WeaponSkillProgress { level = rank };
            Assert.IsFalse(bow.SkillProgress.Allocate(grant, "v2_0_2"));
            Assert.IsTrue(bow.SkillProgress.Allocate(grant, "v2_0_0"));
            Assert.IsTrue(bow.SkillProgress.Allocate(grant, "v2_0_1"));
            Assert.AreEqual(0f, new WeaponSkillBonuses(bow).Get(WeaponSkillEffect.Poison));
            for (int i = 2; i < rank; i++) Assert.IsTrue(bow.SkillProgress.Allocate(grant, "v2_0_" + i));
            var tree = new WeaponSkillBonuses(bow);
            Assert.AreEqual(expectedPoison, tree.Get(WeaponSkillEffect.Poison));
            Assert.AreEqual(expectedDuration, tree.Get(WeaponSkillEffect.Duration));
            Assert.AreEqual(0f, tree.Get(WeaponSkillEffect.Cost));
            Assert.AreEqual(1f, tree.MoreDamageMultiplier, "Only poison should gain damage from this change");
        }

        [Test]
        public void TopFifth_UsesCeilingAndMeanCohort_AndStableSeed()
        {
            Type engine = EditorType("OfflineBalance"), sampleType = engine.GetNestedType("Sample");
            Type listType = typeof(List<>).MakeGenericType(sampleType);
            var list = (IList)Activator.CreateInstance(listType);
            for (int i = 1; i <= 6; i++)
            {
                object sample = Activator.CreateInstance(sampleType);
                sampleType.GetField("sustainedDps").SetValue(sample, (float)i);
                list.Add(sample);
            }
            var top = (IList)Call("TopFifth", list);
            Assert.AreEqual(2, top.Count);
            Assert.AreEqual(6f, sampleType.GetField("sustainedDps").GetValue(top[0]));
            Assert.AreEqual(5f, sampleType.GetField("sustainedDps").GetValue(top[1]));
            Assert.AreEqual(Call("StableSeed", 7, "Cleave/base", 10, 2), Call("StableSeed", 7, "Cleave/base", 10, 2));
            Assert.AreNotEqual(Call("StableSeed", 7, "Cleave/base", 10, 2), Call("StableSeed", 7, "Cleave/base", 10, 3));
        }

        [Test]
        public void RecalculationCandidates_CompareWithinArchetypeAndLevel_AndPreserveSource()
        {
            Type sampleType = EditorType("OfflineBalance").GetNestedType("Sample");
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(sampleType));
            void Add(int id, string archetype, int level, float dps, string excluded = null)
            {
                object sample = Activator.CreateInstance(sampleType);
                sampleType.GetField("sample").SetValue(sample, id);
                sampleType.GetField("archetype").SetValue(sample, archetype);
                sampleType.GetField("level").SetValue(sample, level);
                sampleType.GetField("sustainedDps").SetValue(sample, dps);
                sampleType.GetField("excluded").SetValue(sample, excluded);
                list.Add(sample);
            }
            Add(1, "strong", 34, 100);
            Add(2, "strong", 34, 75);
            Add(3, "strong", 34, 74.9f);
            Add(4, "strong", 34, 9999, "Melee survival");
            Add(5, "weak", 34, 2);
            Add(6, "strong", 1, 1);
            Add(7, "strong", 34, float.NaN);
            Add(8, "strong", 34, float.PositiveInfinity);
            Add(9, "zero", 1, 0);
            var retained = ((IEnumerable)Call("RecalculationCandidates", list, .75f)).Cast<object>().ToList();
            CollectionAssert.AreEquivalent(new[] { 1, 2, 5, 6 },
                retained.Select(s => (int)sampleType.GetField("sample").GetValue(s)));
            Assert.AreEqual(9, list.Count, "Pruning must not remove original observations");
            Assert.IsNull(sampleType.GetField("excluded").GetValue(list[2]), "Low DPS is not a survival failure");
            var bestOnly = ((IEnumerable)Call("RecalculationCandidates", list, 1f)).Cast<object>().ToList();
            CollectionAssert.AreEquivalent(new[] { 1, 5, 6 },
                bestOnly.Select(s => (int)sampleType.GetField("sample").GetValue(s)));
        }

        [TestCase(0f), TestCase(-.1f), TestCase(1.01f), TestCase(float.NaN)]
        public void RecalculationCandidates_RejectInvalidCutoff(float cutoff)
        {
            Type sampleType = EditorType("OfflineBalance").GetNestedType("Sample");
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(sampleType));
            var error = Assert.Throws<TargetInvocationException>(() => Call("RecalculationCandidates", list, cutoff));
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(error.InnerException);
        }

        [Test]
        public void BossMitigation_UsesLevelReferenceAndDiminishingPhysicalPenetration()
        {
            float Hit(float penetration, bool physical) =>
                (float)Call("BossMitigatedHit", 100f, 250f, 20f, penetration, physical, 1);
            Assert.AreEqual(100f * 480f / 730f, Hit(0, true), .0001);
            Assert.AreEqual(100f * 480f / (480f + 250f * 0.375f), Hit(100, true), .0001);
            Assert.Greater(Hit(150, true), Hit(100, true));
            Assert.Less(Hit(10000, true), 100f);
            Assert.AreEqual(Hit(40, true), (float)Call("BossMitigatedHit",
                100f, 250f * DefenceMath.EnemyArmourScale(34), 20f, 40f, true, 34), .0001);
            Assert.AreEqual(80f, Hit(0, false), .0001);
            Assert.AreEqual(100f, Hit(20, false), .0001);
        }

        [Test]
        public void BossPressure_IsSharedAcrossOneArmy_AndUntargetableWolvesNeedNoDeathReplacement()
        {
            Assert.AreEqual(5f, (float)Call("MinionLifetime", 100f, 20f, 1, 0f, false), .0001);
            Assert.AreEqual(10f, (float)Call("MinionLifetime", 100f, 20f, 2, 0f, false), .0001);
            Assert.AreEqual(-1f, (float)Call("MinionLifetime", 100f, 20f, 2, 10f, false));
            Assert.AreEqual(-1f, (float)Call("MinionLifetime", 100f, 20f, 1, 0f, true));
        }

        [Test]
        public void BossTargets_ExportThreeEquallyWeightedLiveBosses_AtEveryLevel()
        {
            var targets = ((IEnumerable)Call("BossTargets")).Cast<object>().ToList();
            Assert.AreEqual(3 * 34, targets.Count);
            object Field(object target, string field) => target.GetType().GetField(field).GetValue(target);
            CollectionAssert.AreEquivalent(new[] { "Gravelord Mortis", "Ashen Warlord", "Rimeheart" }, targets.Select(t => (string)Field(t, "name")).Distinct());
            foreach (var level in targets.GroupBy(t => (int)Field(t, "level")))
            {
                Assert.AreEqual(3, level.Count());
                Assert.AreEqual(1f, level.Sum(t => (float)Field(t, "weight")), .0001);
            }
            foreach (var target in targets)
            {
                Assert.Greater((float)Field(target, "armour"), 0);
                Assert.Greater((float)Field(target, "rawHit"), 0);
                Assert.Greater((float)Field(target, "attacksPerSecond"), 0);
            }
        }

        [Test]
        public void CharacterDamageProgression_DoesNotReintroduceWeaponRankScaling_OrBuffSupportSpells()
        {
            var book = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillBook")).First(t => t != null);
            var ids = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillId")).First(t => t != null);
            foreach (string id in new[] { "FireBolt", "IceShard", "ChainLightning", "VenomSpout", "FrostNova", "WarCry", "DeathMark" })
            {
                object skill = book.GetMethod("Get").Invoke(null, new[] { Enum.Parse(ids, id) });
                Type type = skill.GetType();
                float RankDamage(int rank) => (float)type.GetMethod("DamageAt").Invoke(skill, new object[] { rank });
                float CharacterDamage(int level) => (float)type.GetMethod("DamageForCharacter").Invoke(skill, new object[] { level });
                Assert.AreEqual(RankDamage(1), RankDamage(10), id + " must not gain base damage from weapon rank");
                Assert.AreEqual(RankDamage(1), CharacterDamage(1), .0001, id);
                bool main = (bool)type.GetProperty("Main").GetValue(skill);
                if (main) Assert.Greater(CharacterDamage(34), CharacterDamage(1), id);
                else Assert.AreEqual(CharacterDamage(1), CharacterDamage(34), id + " support balance is separate");
                Assert.AreEqual(CharacterDamage(1), CharacterDamage(0), .0001, id);
            }
        }

        [Test]
        public void MinionDamageProgression_UsesOwnerCharacterLevel_AndPreservesStatInvestment()
        {
            var minions = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.Minion")).First(t => t != null);
            var kinds = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.MinionKind")).First(t => t != null);
            var early = StatSheet.Build(new BaseStats { Level = 1 }, null);
            var late = StatSheet.Build(new BaseStats { Level = 34 }, null);
            var invested = StatSheet.Build(new BaseStats { Level = 34 }.Set(StatType.MinionDamage, 100), null);
            foreach (string name in Enum.GetNames(kinds))
            {
                object kind = Enum.Parse(kinds, name);
                float Damage(int rank, StatSheet sheet) => (float)minions.GetMethod("DamageFor").Invoke(null, new object[] { kind, rank, sheet, null });
                Assert.AreEqual(Damage(1, early), Damage(10, early), .0001, name + " rank only grants points");
                Assert.Greater(Damage(1, late), Damage(1, early), name);
                Assert.AreEqual(Damage(1, late) * 2, Damage(1, invested), .001, name);
            }
        }

        [Test]
        public void ViperDamageCurveImprovesEarlyLevelsAndPreservesLevel34Damage()
        {
            Type minions = Type.GetType("PoeClone.Skills.Minion, Assembly-CSharp", true);
            object viper = Enum.Parse(minions.Assembly.GetType("PoeClone.Skills.MinionKind"), "Viper");
            float Damage(int level) => (float)minions.GetMethod("DamageFor").Invoke(null,
                new object[] { viper, 1, StatSheet.Build(new BaseStats { Level = level }, null), null });
            float Previous(int level) => 13f * (2f * 1.2f / 1.3f) * (1f + .022f * (level - 1));
            Assert.Greater(Damage(1), Previous(1) * 1.2f);
            Assert.Greater(Damage(15), Previous(15) * 1.05f);
            Assert.AreEqual(Previous(34), Damage(34), .01f);
            for (int level = 2; level <= 34; level++) Assert.Greater(Damage(level), Damage(level - 1));
        }

        [TestCase("Warrior", .035f)]
        [TestCase("Mage", .035f)]
        [TestCase("Wolf", .035f)]
        [TestCase("Golem", .045f)]
        public void OtherMinionDamageCurvesKeepTheirGrowthRates(string name, float growth)
        {
            Type minions = Type.GetType("PoeClone.Skills.Minion, Assembly-CSharp", true);
            object kind = Enum.Parse(minions.Assembly.GetType("PoeClone.Skills.MinionKind"), name);
            float Damage(int level) => (float)minions.GetMethod("DamageFor").Invoke(null,
                new object[] { kind, 1, StatSheet.Build(new BaseStats { Level = level }, null), null });
            Assert.AreEqual(Damage(1) * (1f + growth * 33f), Damage(34), .001f);
        }

        [Test]
        public void MinionRules_RespectArmyAndChampionRoutesAndDefenceSnapshots()
        {
            // Runtime types are reflected because this test assembly cannot reference Assembly-CSharp.
            Type minions = Type.GetType("PoeClone.Skills.Minion, Assembly-CSharp", true);
            Type kinds = minions.Assembly.GetType("PoeClone.Skills.MinionKind");
            Type damageTypes = minions.Assembly.GetType("PoeClone.Combat.DamageType");
            object warrior = Enum.Parse(kinds, "Warrior");
            var cap = minions.GetMethod("CapFor", new[] { kinds, typeof(WeaponSkillBonuses) });
            var item = new ItemData("test_sceptre", "Sceptre", ItemType.Weapon, 2, 3, UnityEngine.Color.white,
                new[] { new StatModifier(StatType.GrantRaiseSkeletons, 10) }, weaponType: WeaponType.Sceptre);
            item.SkillProgress = new WeaponSkillProgress { level = 10 };
            for (int i = 0; i <= 5; i++) Assert.IsTrue(item.SkillProgress.Allocate(StatType.GrantRaiseSkeletons, "v2_0_" + i));
            var army = new WeaponSkillBonuses(item);
            Assert.AreEqual(5, cap.Invoke(null, new object[] { warrior, army }));

            item.SkillProgress.nodes.Clear();
            for (int i = 0; i <= 5; i++) Assert.IsTrue(item.SkillProgress.Allocate(StatType.GrantRaiseSkeletons, "v2_1_" + i));
            var champion = new WeaponSkillBonuses(item);
            Assert.AreEqual(1, cap.Invoke(null, new object[] { warrior, champion }));
            Assert.AreEqual(5, cap.Invoke(null, new object[] { warrior, army }), "an existing army keeps its snapshot");

            var sheet = StatSheet.Build(new BaseStats().Set(StatType.MinionResistances, 70), null);
            Assert.AreEqual(75f, minions.GetMethod("ResistanceFor").Invoke(null, new object[] { sheet, champion }));
            Assert.AreEqual(25f, (float)minions.GetMethod("DamageTaken").Invoke(null,
                new[] { (object)100f, Enum.Parse(damageTypes, "Fire"), sheet, champion }), .001f);
            float Hit(WeaponSkillBonuses tree) => (float)minions.GetMethod("DamageTaken").Invoke(null,
                new[] { (object)100f, Enum.Parse(damageTypes, "Physical"), sheet, tree });
            Assert.Less(Hit(champion), Hit(army), "Bone Shields mitigates physical hits");
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator FullSurvey_ExportsEveryArchetypeAtLevelsOneThrough34()
        {
            Call("Start", 20, 20261014);
            try
            {
                string status;
                do
                {
                    yield return null;
                    status = (string)Call("Status");
                    Assert.IsFalse(status.StartsWith("failed:"), status);
                    Assert.AreNotEqual("cancelled", status);
                } while (!status.StartsWith("complete:"));
                Assert.IsTrue(File.Exists("BalanceResults/survey.json"));
                Assert.IsFalse(UnityEditor.EditorApplication.isPlaying);
                string validation = (string)Call("ValidateReportFile");
                Assert.AreEqual("valid", validation);
            }
            finally { Call("Cancel"); }
        }
    }
}
