using NUnit.Framework;
using PoeClone.Inventory;
using System;
using System.Reflection;
using UnityEngine;

namespace PoeClone.Tests
{
    public class LifeLeechTests
    {
        [Test]
        public void FullLifeRejectsNewLeechAndInstantHealingClearsExistingLeech()
        {
            var go = new GameObject("Leech test");
            try
            {
                Type type = Type.GetType("PoeClone.Player.PlayerStats, Assembly-CSharp", true);
                Component stats = go.AddComponent(type);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo health = type.GetField("currentHealth", flags);
                var leech = (LifeLeech)type.GetField("lifeLeech", flags).GetValue(stats);
                health.SetValue(stats, 100f);
                type.GetMethod("Leech").Invoke(stats, new object[] { 10f });
                health.SetValue(stats, 50f);
                Assert.That(leech.Tick(1f, 100f), Is.Zero, "Full-life hits cannot bank recovery.");

                type.GetMethod("Leech").Invoke(stats, new object[] { 10f });
                Assert.That((float)health.GetValue(stats), Is.EqualTo(50f), "Leech must not heal instantly.");
                type.GetMethod("Heal").Invoke(stats, new object[] { 50f });
                Assert.That((float)health.GetValue(stats), Is.EqualTo(100f));
                health.SetValue(stats, 50f);
                Assert.That(leech.Tick(1f, 100f), Is.Zero, "A full heal discards active recovery.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OneHitRecoversOverTimeAndCapsItsAmount()
        {
            var leech = new LifeLeech();
            leech.Add(10000f, 1000f);
            Assert.That(leech.Tick(1f, 1000f), Is.EqualTo(10f).Within(0.001f));
            Assert.That(leech.Tick(10f, 1000f), Is.EqualTo(40f).Within(0.001f));
            Assert.That(leech.Tick(1f, 1000f), Is.Zero);
        }

        [Test]
        public void PackHitsShareCapAndExcessRecoveryIsNotQueued()
        {
            var leech = new LifeLeech();
            for (int i = 0; i < 20; i++) leech.Add(100f, 1000f);
            Assert.That(leech.Tick(5f, 1000f), Is.EqualTo(500f).Within(0.001f));
            Assert.That(leech.Tick(1f, 1000f), Is.Zero);
        }

        [Test]
        public void SmallHitsExpireWithoutLosingRecoveryOrExceedingCap()
        {
            var leech = new LifeLeech();
            for (int i = 0; i < 10; i++) leech.Add(1f, 1000f);
            leech.Add(100f, 1000f);
            // First .05 seconds: capped at 100/s; remaining .95 seconds: 10/s.
            Assert.That(leech.Tick(1f, 1000f), Is.EqualTo(14.5f).Within(0.001f));
        }

        [Test]
        public void RepeatedHitsStackAndClearingDiscardsAllRecovery()
        {
            var leech = new LifeLeech();
            leech.Add(100f, 1000f);
            Assert.That(leech.Tick(1f, 1000f), Is.EqualTo(10f).Within(0.001f));
            leech.Add(100f, 1000f);
            Assert.That(leech.Tick(1f, 1000f), Is.EqualTo(20f).Within(0.001f));
            leech.Clear();
            Assert.That(leech.Tick(1f, 1000f), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Timeout(2000)]
        public void SubnormalEffectExpiresWithoutStallingOtherRecovery(bool tinyEffectLast)
        {
            const float maximumLife = 831f;
            const float seconds = 0.005498082377016544f;
            var leech = new LifeLeech();
            if (!tinyEffectLast) leech.Add(7f * float.Epsilon, maximumLife);
            leech.Add(0.5338872075080872f, maximumLife);
            if (tinyEffectLast) leech.Add(7f * float.Epsilon, maximumLife);

            Assert.That(leech.Tick(seconds, maximumLife),
                Is.EqualTo(maximumLife * LifeLeech.RatePerEffect * seconds).Within(0.000001f));
            Assert.That(leech.Tick(1f, maximumLife),
                Is.EqualTo(0.5338872075080872f * LifeLeech.Effectiveness - maximumLife * LifeLeech.RatePerEffect * seconds).Within(0.000001f));
            Assert.That(leech.Tick(1f, maximumLife), Is.Zero);
        }

        [Test]
        public void FrameSizeDoesNotChangeRecoveryAcrossExpirations()
        {
            var coarse = new LifeLeech();
            var fine = new LifeLeech();
            for (int i = 1; i <= 20; i++)
            {
                coarse.Add(i * 3f, 1000f);
                fine.Add(i * 3f, 1000f);
            }
            float fineRecovery = 0f;
            for (int i = 0; i < 400; i++) fineRecovery += fine.Tick(0.01f, 1000f);
            Assert.That(coarse.Tick(4f, 1000f), Is.EqualTo(fineRecovery).Within(0.01f));
        }
    }
}
