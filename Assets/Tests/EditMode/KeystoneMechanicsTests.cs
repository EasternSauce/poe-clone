using NUnit.Framework;
using PoeClone.Inventory;

namespace PoeClone.Tests
{
    public class KeystoneMechanicsTests
    {
        [Test]
        public void LaterHitsDoNotExtendOlderDebt()
        {
            var debt = new DamageDebt();
            debt.Add(40f);
            Assert.AreEqual(20f, debt.Tick(2f), 0.001f);
            debt.Add(40f);
            Assert.AreEqual(40f, debt.Tick(2f), 0.001f);
            Assert.AreEqual(20f, debt.Total, 0.001f);
            Assert.AreEqual(20f, debt.Tick(2f), 0.001f);
            Assert.AreEqual(0f, debt.Total);
        }

        [Test]
        public void RecoveryPaysOldestDebtBeforeRestoringLife()
        {
            var debt = new DamageDebt();
            debt.Add(40f);
            debt.Tick(2f);
            debt.Add(40f);
            Assert.AreEqual(0f, debt.Recover(20f));
            Assert.AreEqual(20f, debt.Tick(2f), 0.001f, "Old debt was paid; only the newer hit should tick");
            Assert.AreEqual(10f, debt.Recover(30f), 0.001f, "Recovery beyond remaining debt restores life");
            Assert.AreEqual(0f, debt.Tick(4f));
        }

        [Test]
        public void DebtDoesNotDependOnFrameSizeAndPausesWithGameTime()
        {
            var fine = new DamageDebt();
            var coarse = new DamageDebt();
            fine.Add(100f);
            coarse.Add(100f);
            Assert.AreEqual(0f, fine.Tick(0f));
            float total = 0f;
            for (int i = 0; i < 8; i++) total += fine.Tick(0.5f);
            Assert.AreEqual(coarse.Tick(8f), total, 0.001f);
            Assert.AreEqual(0f, fine.Total);
            fine.Add(20f);
            fine.Clear();
            Assert.AreEqual(0f, fine.Total);
            Assert.AreEqual(0f, fine.Tick(4f));
        }

        [Test]
        public void EquilibriumRewardsAlternationAndPenalizesRepeatingAnElement()
        {
            var state = new ElementalEquilibriumState();
            Assert.AreEqual(0f, state.Penetration(EquilibriumElement.Fire, 0f));
            state.Record(EquilibriumElement.Fire, 0f);
            Assert.AreEqual(-25f, state.Penetration(EquilibriumElement.Fire, 1f));
            Assert.AreEqual(25f, state.Penetration(EquilibriumElement.Cold, 1f));
            state.Record(EquilibriumElement.Cold, 1f);
            Assert.AreEqual(-25f, state.Penetration(EquilibriumElement.Cold, 2f));
            Assert.AreEqual(25f, state.Penetration(EquilibriumElement.Fire, 2f));
            Assert.AreEqual(25f, state.Penetration(EquilibriumElement.Lightning, 2f));
        }

        [Test]
        public void NonElementalHitsNeitherBenefitNorRefreshEquilibrium()
        {
            var state = new ElementalEquilibriumState();
            state.Record(EquilibriumElement.Fire, 0f);
            state.Record(EquilibriumElement.None, 3f);
            Assert.AreEqual(0f, state.Penetration(EquilibriumElement.None, 3.5f));
            Assert.AreEqual(-25f, state.Penetration(EquilibriumElement.Fire, 3.5f));
            Assert.AreEqual(0f, state.Penetration(EquilibriumElement.Cold, 4f));
        }
    }
}
