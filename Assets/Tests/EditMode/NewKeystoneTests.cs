using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class NewKeystoneTests
    {
        private static StatSheet Sheet(params StatModifier[] mods) =>
            StatSheet.Build(new BaseStats(), new EquipmentSet(), mods);

        [Test]
        public void UnwaveringStanceStopsEvasionAndStaggerWithoutDeletingEvasionRating()
        {
            var normal = Sheet(new StatModifier(StatType.Evasion, 600));
            var stance = Sheet(new StatModifier(StatType.Evasion, 600), new StatModifier(StatType.UnwaveringStance, 1));
            Assert.AreEqual(0.5f, normal.EvadeChance, 0.001f);
            Assert.AreEqual(600f, stance.Total(StatType.Evasion));
            Assert.AreEqual(0f, stance.EvadeChance);
            Assert.AreEqual(0f, DefenceMath.StaggerChance(100f, 100f, stance.StunAvoidance));
            var iron = Sheet(new StatModifier(StatType.Evasion, 600), new StatModifier(StatType.UnwaveringStance, 1),
                new StatModifier(StatType.IronReflexes, 1));
            Assert.GreaterOrEqual(iron.Total(StatType.Armour), 600f);
            Assert.AreEqual(0f, iron.EvadeChance);
        }

        [Test]
        public void ReturningVolleyTurnsThreeUnitsAfterTheFirstEnemyNotAfterLaunchOrLaterHits()
        {
            var flight = new ReturningProjectileFlight();
            flight.Advance(30f);
            Assert.IsFalse(flight.BeginReturn());
            flight.HitEnemy();
            flight.Advance(1.5f);
            flight.HitEnemy();
            Assert.AreEqual(1.5f, flight.RemainingDistance);
            Assert.IsFalse(flight.BeginReturn());
            flight.Advance(1.49f);
            Assert.IsFalse(flight.CanTurn);
            flight.Advance(0.01f);
            Assert.IsTrue(flight.BeginReturn());
            flight.HitEnemy();
            flight.Advance(50f);
            Assert.IsTrue(flight.Returning);
            Assert.IsFalse(flight.BeginReturn(), "A return hit must never create another outbound leg");
        }

        [Test]
        public void SpreadPoisonPreservesPotencyTickPhaseAndOriginalExpiration()
        {
            var original = new PoisonStackClock(12f, 6, 100f);
            Assert.IsTrue(original.TryTick(100.5f, out float first));
            Assert.AreEqual(12f, first);
            var spread = original.Copy();
            var spreadAgain = spread.Copy();
            Assert.AreEqual(101f, spread.NextTick);
            Assert.AreEqual(103f, spreadAgain.ExpiresAt);
            Assert.IsFalse(spread.TryTick(100.9f, out _), "Spreading must not cause an immediate extra tick");
            Assert.IsTrue(spread.TryTick(101f, out float tick));
            Assert.AreEqual(12f, tick, "Spreading must not rescale poison from current equipment");
            Assert.AreEqual(101f, original.NextTick, "Copies must not mutate the original stack");
            float rest = 0;
            while (spreadAgain.TryTick(104f, out float due)) rest += due;
            Assert.AreEqual(60f, rest, "Only the five original remaining ticks should be copied");
            Assert.IsFalse(spreadAgain.CanSpread(104f));
            Assert.IsFalse(original.CanSpread(103f), "An expired stack cannot restart on another enemy");
        }

        [Test]
        public void IdenticalPoisonCopiesCanMergeWithoutMergingDifferentPotenciesOrDeadlines()
        {
            var original = new PoisonStackClock(10f, 6, 0f);
            var copy = original.Copy();
            Assert.IsTrue(original.Matches(copy));
            Assert.IsFalse(original.Matches(new PoisonStackClock(20f, 6, 0f)));
            Assert.IsFalse(original.Matches(new PoisonStackClock(10f, 8, 0f)));
            copy.TryTick(0.5f, out _);
            Assert.IsFalse(original.Matches(copy));
        }

        [Test]
        public void EffigyLimitAndBonusesAreIndependentOfMinionBonuses()
        {
            var baseline = Sheet();
            var minions = Sheet(new StatModifier(StatType.AdditionalMinions, 5), new StatModifier(StatType.MinionLife, 300),
                new StatModifier(StatType.MinionDamage, 300), new StatModifier(StatType.MinionArmour, 10000));
            Assert.AreEqual(2, EffigyRules.Limit(minions));
            Assert.AreEqual(EffigyRules.Life(baseline), EffigyRules.Life(minions));
            Assert.AreEqual(EffigyRules.DamageMultiplier(baseline), EffigyRules.DamageMultiplier(minions));
            Assert.AreEqual(EffigyRules.DamageTaken(100, true, baseline), EffigyRules.DamageTaken(100, true, minions));
            var effigies = Sheet(new StatModifier(StatType.AdditionalEffigies, 1), new StatModifier(StatType.EffigyLife, 50),
                new StatModifier(StatType.EffigyDamage, 20), new StatModifier(StatType.EffigyArmour, 200));
            Assert.AreEqual(3, EffigyRules.Limit(effigies));
            Assert.AreEqual(EffigyRules.Life(baseline) * 1.5f, EffigyRules.Life(effigies), 0.001f);
            Assert.AreEqual(0.6f, EffigyRules.DamageMultiplier(effigies), 0.001f);
            Assert.Less(EffigyRules.DamageTaken(100, true, effigies), EffigyRules.DamageTaken(100, true, baseline));
        }

        [Test]
        public void EffigyCastSpeedAddsWithSpellAndWeaponSpeedAndResistanceStillCaps()
        {
            var sheet = Sheet(new StatModifier(StatType.CastSpeed, 10), new StatModifier(StatType.EffigyCastSpeed, 20),
                new StatModifier(StatType.EffigyResistances, 100));
            Assert.AreEqual(1.45f, EffigyRules.CastRate(sheet, 15), 0.001f);
            Assert.AreEqual(1.74f, EffigyRules.CastRate(sheet, 15, 1.2f), 0.001f);
            Assert.AreEqual(25f, EffigyRules.DamageTaken(100, false, sheet), 0.001f);
        }

        [Test]
        public void ExtraEffigyNeedsTravelAndSupportingPassivesAreSpreadAcrossBranches()
        {
            var entry = PassiveTree.Get("w_archmage");
            var extra = PassiveTree.Get("k_secondwind");
            Assert.IsTrue(entry.Keystone && extra.Keystone);
            Assert.AreNotEqual(entry.Branch, extra.Branch);
            var queue = new Queue<string>();
            var distances = new Dictionary<string, int> { [entry.Id] = 0 };
            queue.Enqueue(entry.Id);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                foreach (var link in PassiveTree.Get(id).Links)
                    if (!distances.ContainsKey(link)) { distances[link] = distances[id] + 1; queue.Enqueue(link); }
            }
            Assert.GreaterOrEqual(distances[extra.Id], 6);
            var branches = new HashSet<PassiveBranch>();
            foreach (var node in PassiveTree.Nodes)
                if (node.Mods.Any(m => m.Stat >= StatType.EffigyLife && m.Stat <= StatType.EffigyRange)) branches.Add(node.Branch);
            CollectionAssert.IsSubsetOf(new[] { PassiveBranch.Might, PassiveBranch.Wisdom, PassiveBranch.Storm, PassiveBranch.Zeal }, branches);
        }
    }
}
