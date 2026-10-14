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

        [Test]
        public void SkillModel_UsesReleaseRecovery_AndCombatDamageTypes()
        {
            Assert.AreEqual(1.95f, (float)Call("PiercingInterval", 1f, 0f), .0001);
            Assert.AreEqual(1.175f, (float)Call("PiercingInterval", 2f, 25f), .0001);
            Assert.AreEqual(.72f, (float)Call("PiercingInterval", 5f, 0f), .0001);
            var book = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillBook")).First(t => t != null);
            var idType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PoeClone.Skills.SkillId")).First(t => t != null);
            foreach (var expected in new[] { new[] { "VenomSlash", "Physical" }, new[] { "BurningArrow", "Fire" }, new[] { "VenomSpout", "Poison" } })
            {
                object skill = book.GetMethod("Get").Invoke(null, new[] { Enum.Parse(idType, expected[0]) });
                Assert.AreEqual(expected[1], Call("BaseElement", skill).ToString());
            }
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
