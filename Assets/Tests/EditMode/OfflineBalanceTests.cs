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
    /// These tests never enter Play mode or construct a player or enemy.</summary>
    public class OfflineBalanceTests
    {
        private static Type EditorType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("PoeClone.EditorTools." + name)).First(t => t != null);
        private static object Call(string method, params object[] args) => EditorType("OfflineBalance").GetMethod(method).Invoke(null, args);

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

        [TestCase(false), TestCase(true)]
        public void Pulverize_PenetrationRequiresAConnectedKeystone_WithoutExceedingTheCap(bool sideRoute)
        {
            const StatType grant = StatType.GrantPulverize;
            var progress = new WeaponSkillProgress { level = 10 };
            float Penetration() => progress.nodes.Select(id => WeaponSkillTrees.Find(grant, id))
                .SelectMany(n => n.Bonuses).Where(b => b.Effect == WeaponSkillEffect.ArmourPenetration).Sum(b => b.Value);
            foreach (string id in new[] { "v2_1_0", "v2_1_1", "v2_1_2" }) Assert.IsTrue(progress.Allocate(grant, id));
            Assert.Less(Penetration(), 100f, "The notable must leave room for the keystones to matter");
            foreach (string id in sideRoute ? new[] { "v2_1_6" } : new[] { "v2_1_3", "v2_1_4" })
                Assert.IsTrue(progress.Allocate(grant, id));
            Assert.Less(Penetration(), 100f, "Travel alone must not cap penetration");
            string terminal = sideRoute ? "v2_1_7" : "v2_1_5";
            Assert.IsTrue(WeaponSkillTrees.Find(grant, terminal).Keystone);
            Assert.IsTrue(progress.Allocate(grant, terminal));
            Assert.AreEqual(100f, Penetration(), .001f, "Either complete route should reach, not waste, the penetration cap");
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
        public void BossMitigation_AppliesEnrageBeforeArmour_AndCapsPhysicalPenetration()
        {
            float Hit(float penetration, float damageTaken, bool physical) =>
                (float)Call("BossMitigatedHit", 100f, 250f, 20f, penetration, damageTaken, physical);
            Assert.AreEqual(80f * (1 - DefenceMath.ArmourReduction(250f, 80f)), Hit(0, .8f, true), .0001);
            Assert.AreNotEqual(Hit(0, 1, true) * .8f, Hit(0, .8f, true), "Enrage must change armour's hit-size denominator");
            Assert.AreEqual(80f, Hit(100, .8f, true), .0001);
            Assert.AreEqual(Hit(100, .8f, true), Hit(150, .8f, true), .0001);
            Assert.AreEqual(64f, Hit(0, .8f, false), .0001);
            Assert.AreEqual(80f, Hit(20, .8f, false), .0001);
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
        public void BossTargets_ExportThreeEquallyWeightedLiveBosses_AtEveryLevel_WithBothStates()
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
                Assert.Greater((float)Field(target, "enragedHit"), (float)Field(target, "calmHit"));
                Assert.AreEqual((float)Field(target, "calmAttacksPerSecond") * 1.25f, (float)Field(target, "enragedAttacksPerSecond"), .0001);
                Assert.AreEqual(.8f, (float)Field(target, "enragedDamageTaken"), .0001);
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
