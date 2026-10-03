using System.Collections.Generic;
using NUnit.Framework;
using PoeClone.Network.Replication;

namespace PoeClone.Tests
{
    public class SnapshotCodecTests
    {
        [Test]
        public void SkillCasts_SurviveTheRoundTrip()
        {
            StateSnapshot s = Sample();
            s.sc = new[]
            {
                new SkillCastState { n = 7, s = 6, lv = 3, x = 1.5f, y = 1f, z = -2.25f, dx = 0.6f, dz = 0.8f, sz = 2.2f, c = 2 },
                new SkillCastState { n = 8, s = 5, pts = new[] { 1f, 2f, 3f, 4.5f, 5f, 6f } }
            };

            StateSnapshot back = SnapshotCodec.Deserialize(SnapshotCodec.Serialize(s));

            Assert.AreEqual(2, back.sc.Length);
            Assert.AreEqual(7, back.sc[0].n);
            Assert.AreEqual(6, back.sc[0].s);
            Assert.AreEqual(2, back.sc[0].c);
            Assert.AreEqual(2.2f, back.sc[0].sz, 0.01f);
            Assert.AreEqual(0.8f, back.sc[0].dz, 0.001f);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 4.5f, 5f, 6f }, back.sc[1].pts);
        }

        [Test]
        public void NoSkillCasts_ReadsAsAnEmptyList()
        {
            StateSnapshot back = SnapshotCodec.Deserialize(SnapshotCodec.Serialize(Sample()));
            Assert.IsNotNull(back.sc);
            Assert.AreEqual(0, back.sc.Length);
        }

        private static StateSnapshot Sample()
        {
            return new StateSnapshot
            {
                seq = 42,
                t = 123.4567,
                area = 1,
                fade = 1,
                p = new EntityState { x = 1.234f, y = 0.5f, z = -7.891f, r = 271.26f, hp = 80f, mhp = 110f, atk = 3, ap = 2, stg = 1 },
                hud = new PlayerHudState { lv = 3, xp = 40, str = 12, dex = 11, itl = 10, hp = 80f, mhp = 110f, mp = 20f, mmp = 55f },
                eq = new[] { "rusty_sword", "", "iron_helmet" },
                e = new[]
                {
                    new EntityState { i = 7, x = 10f, z = 4f, r = 90f, hp = 12.5f, mhp = 30f, ch = 1, atk = 5, ap = 4 },
                    new EntityState { i = 8, x = -3f, y = -0.9f, z = 2f, d = 1, mhp = 30f }
                }
            };
        }

        [Test]
        public void RoundTrip_KeepsEveryField_ToTransmittedPrecision()
        {
            StateSnapshot back = SnapshotCodec.Deserialize(SnapshotCodec.Serialize(Sample()));

            Assert.IsNotNull(back);
            Assert.AreEqual(42, back.seq);
            Assert.AreEqual(123.457, back.t, 1e-6);
            Assert.AreEqual(1, back.area);
            Assert.AreEqual(1, back.fade);
            Assert.AreEqual(1.23f, back.p.x, 1e-4f);
            Assert.AreEqual(-7.89f, back.p.z, 1e-4f);
            Assert.AreEqual(271.3f, back.p.r, 1e-4f);
            Assert.AreEqual(3, back.p.atk);
            Assert.AreEqual(2, back.p.ap);
            Assert.AreEqual(1, back.p.stg);
            Assert.AreEqual(3, back.hud.lv);
            Assert.AreEqual(55f, back.hud.mmp, 1e-4f);
            CollectionAssert.AreEqual(new[] { "rusty_sword", "", "iron_helmet" }, back.eq);
            Assert.AreEqual(2, back.e.Length);
            Assert.AreEqual(7, back.e[0].i);
            Assert.AreEqual(12.5f, back.e[0].hp, 1e-4f);
            Assert.AreEqual(1, back.e[0].ch);
            Assert.AreEqual(4, back.e[0].ap);
            Assert.AreEqual(1, back.e[1].d);
            Assert.AreEqual(-0.9f, back.e[1].y, 1e-4f);
        }

        [Test]
        public void Serialize_StartsWithType_SoTheClientFastPathRecognisesIt()
        {
            StringAssert.StartsWith("{\"type\":\"state\"", SnapshotCodec.Serialize(Sample()));
        }

        [Test]
        public void Serialize_OmitsZeroFields_AndIsCompact()
        {
            string json = SnapshotCodec.Serialize(new StateSnapshot { seq = 1, t = 1, p = new EntityState() });

            StringAssert.DoesNotContain("\"atk\"", json);
            StringAssert.DoesNotContain("\"x\"", json);
            Assert.Less(json.Length, 80);
        }

        [Test]
        public void Serialize_UsesInvariantDecimalPoint()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
                StringAssert.Contains("\"x\":1.23", SnapshotCodec.Serialize(Sample()));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void Serialize_EscapesStrings()
        {
            var s = Sample();
            s.eq = new[] { "we\"ird\\id" };
            StateSnapshot back = SnapshotCodec.Deserialize(SnapshotCodec.Serialize(s));
            Assert.AreEqual("we\"ird\\id", back.eq[0]);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"type\":\"chat\",\"text\":\"hi\"}")]
        [TestCase("{\"type\":\"state\",\"t\":1}")]
        public void Deserialize_RejectsAnythingThatIsntAUsableSnapshot(string json)
        {
            Assert.IsNull(SnapshotCodec.Deserialize(json));
        }

        [Test]
        public void Deserialize_DefaultsMissingEnemyListToEmpty()
        {
            StateSnapshot s = SnapshotCodec.Deserialize("{\"type\":\"state\",\"t\":1,\"p\":{\"i\":0}}");
            Assert.IsNotNull(s);
            Assert.IsNotNull(s.e);
            Assert.AreEqual(0, s.e.Length);
        }
    }

    public class SnapshotTimelineTests
    {
        private const float Speed = 5f; // the test entity walks along +x at 5 units/s

        private static StateSnapshot At(double t, float? x = null, int dead = 0)
        {
            float pos = x ?? (float)(t * Speed);
            return new StateSnapshot
            {
                t = t,
                p = new EntityState { x = pos, d = dead },
                e = new[] { new EntityState { i = 1, x = pos, d = dead } }
            };
        }

        private static float SampleX(SnapshotTimeline timeline)
        {
            Assert.IsTrue(timeline.TryGetFrame(out StateSnapshot from, out StateSnapshot to, out float alpha));
            var result = new EntityState();
            SnapshotTimeline.Interpolate(from.p, to.p, alpha, result);
            return result.x;
        }

        // Feeds a steady 10Hz stream (sender time == local time + 50ms latency) while advancing
        // the playback clock at 60fps, and calls onFrame after every frame.
        private static void Stream(SnapshotTimeline timeline, double seconds, System.Action<double> onFrame = null)
        {
            double nextSend = 0;
            for (double now = 0; now < seconds; now += 1.0 / 60.0)
            {
                while (nextSend + 0.05 <= now)
                {
                    timeline.Add(At(nextSend), now);
                    nextSend += 0.1;
                }
                timeline.Advance(now);
                onFrame?.Invoke(now);
            }
        }

        [Test]
        public void FirstSnapshot_IsHeldUntilTheClockReachesIt()
        {
            var timeline = new SnapshotTimeline();
            timeline.Add(At(10.0), 0);

            Assert.AreEqual(10.0 - timeline.InterpolationDelay, timeline.RenderTime, 1e-6);
            Assert.AreEqual(50f, SampleX(timeline), 1e-4f);
        }

        [Test]
        public void SteadyStream_InterpolatesSmoothly_AlongTheTrueMotion()
        {
            var timeline = new SnapshotTimeline();
            float lastX = float.NegativeInfinity;
            float maxStep = 0f;

            Stream(timeline, 5.0, now =>
            {
                if (now < 0.5) return; // let the buffer fill
                float x = SampleX(timeline);
                // On a straight constant-speed path, interpolation must reproduce the real position at RenderTime...
                Assert.AreEqual(timeline.RenderTime * Speed, x, 0.02, $"at {now:0.000}");
                // ...and never jump backwards or skip ahead between frames.
                Assert.GreaterOrEqual(x, lastX - 1e-4f);
                if (!float.IsNegativeInfinity(lastX)) maxStep = System.Math.Max(maxStep, x - lastX);
                lastX = x;
            });

            // 5 u/s at 60fps is ~0.083 per frame; allow the clock's +-20% rate correction.
            Assert.Less(maxStep, 0.11f);
            Assert.AreEqual(0.0, timeline.Starvation, 1e-9);
        }

        [Test]
        public void SteadyStream_SettlesNearTheTargetDelay()
        {
            var timeline = new SnapshotTimeline();
            Stream(timeline, 5.0);

            double behindNewest = timeline.Newest.t - timeline.RenderTime;
            Assert.That(behindNewest, Is.InRange(0.0, timeline.InterpolationDelay + 0.1));
            Assert.That(timeline.InterpolationDelay, Is.InRange(SnapshotTimeline.MinDelay, 0.2f));
        }

        [Test]
        public void DuplicateAndOutOfOrderSnapshots_AreIgnored()
        {
            var timeline = new SnapshotTimeline();
            Assert.IsTrue(timeline.Add(At(1.0), 0));
            Assert.IsTrue(timeline.Add(At(1.1), 0.1));
            Assert.IsFalse(timeline.Add(At(1.1), 0.12));
            Assert.IsFalse(timeline.Add(At(1.05), 0.13));
            Assert.AreEqual(2, timeline.Count);
        }

        [Test]
        public void WhenDataStops_MotionIsExtrapolatedBriefly_ThenHeld()
        {
            var timeline = new SnapshotTimeline();
            Stream(timeline, 2.0);
            double newestT = timeline.Newest.t;

            for (double now = 2.0; now < 4.0; now += 1.0 / 60.0)
                timeline.Advance(now);

            Assert.Greater(timeline.Starvation, SnapshotTimeline.MaxExtrapolation);
            float x = SampleX(timeline);
            Assert.AreEqual((newestT + SnapshotTimeline.MaxExtrapolation) * Speed, x, 0.01f);
        }

        [Test]
        public void DeadEntities_AreNotExtrapolated()
        {
            var a = new EntityState { x = 0f, d = 1 };
            var b = new EntityState { x = 1f, d = 1 };
            var result = new EntityState();
            SnapshotTimeline.Interpolate(a, b, 1.5f, result);
            Assert.AreEqual(1f, result.x, 1e-6f);
        }

        [Test]
        public void Teleports_SnapInsteadOfSliding()
        {
            var a = new EntityState { x = 0f };
            var b = new EntityState { x = 50f };
            var result = new EntityState();

            SnapshotTimeline.Interpolate(a, b, 0.5f, result);
            Assert.AreEqual(0f, result.x);

            SnapshotTimeline.Interpolate(a, b, 1f, result);
            Assert.AreEqual(50f, result.x);
        }

        [Test]
        public void Yaw_TakesTheShortWayAround()
        {
            var a = new EntityState { r = 350f };
            var b = new EntityState { r = 10f };
            var result = new EntityState();
            SnapshotTimeline.Interpolate(a, b, 0.5f, result);
            Assert.AreEqual(0f, UnityEngine.Mathf.DeltaAngle(0f, result.r), 1e-3f);
        }

        [Test]
        public void DiscreteState_ComesFromTheEarlierSnapshot_UntilTheLaterOneIsReached()
        {
            var a = new EntityState { atk = 1, d = 0 };
            var b = new EntityState { atk = 2, d = 1 };
            var result = new EntityState();

            SnapshotTimeline.Interpolate(a, b, 0.9f, result);
            Assert.AreEqual(1, result.atk);
            Assert.AreEqual(0, result.d);

            SnapshotTimeline.Interpolate(a, b, 1f, result);
            Assert.AreEqual(2, result.atk);
        }

        [Test]
        public void CollectDue_ReturnsEachSnapshotOnce_InOrder_WhenPlaybackReachesIt()
        {
            var timeline = new SnapshotTimeline();
            var due = new List<StateSnapshot>();
            var seen = new List<double>();

            Stream(timeline, 3.0, now =>
            {
                due.Clear();
                timeline.CollectDue(due);
                foreach (var s in due)
                {
                    Assert.LessOrEqual(s.t, timeline.RenderTime + 1e-9);
                    seen.Add(s.t);
                }
            });

            Assert.Greater(seen.Count, 20);
            for (int k = 1; k < seen.Count; k++)
                Assert.AreEqual(0.1, seen[k] - seen[k - 1], 1e-6, "no snapshot skipped or repeated");
        }

        [Test]
        public void Jitter_GrowsTheBuffer()
        {
            var calm = new SnapshotTimeline();
            Stream(calm, 3.0);

            var jittery = new SnapshotTimeline();
            var rng = new System.Random(1);
            double now = 0;
            for (int k = 0; k < 60; k++)
            {
                double t = k * 0.1;
                now = System.Math.Max(now, t + 0.05 + rng.NextDouble() * 0.15);
                jittery.Add(At(t), now);
                jittery.Advance(now);
            }

            Assert.Greater(jittery.Jitter, calm.Jitter);
            Assert.Greater(jittery.InterpolationDelay, calm.InterpolationDelay + 0.05f);
            Assert.LessOrEqual(jittery.InterpolationDelay, SnapshotTimeline.MaxDelay);
        }

        [Test]
        public void LongSilence_ResyncsInsteadOfFastForwarding()
        {
            var timeline = new SnapshotTimeline();
            Stream(timeline, 1.0);

            // The player's tab was in the background for 30 seconds.
            timeline.Add(At(31.0), 31.05);
            Assert.AreEqual(31.0 - timeline.InterpolationDelay, timeline.RenderTime, 1e-6);
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var timeline = new SnapshotTimeline();
            Stream(timeline, 1.0);
            timeline.Clear();

            Assert.IsFalse(timeline.HasData);
            Assert.IsFalse(timeline.TryGetFrame(out _, out _, out _));
            Assert.IsTrue(timeline.Add(At(0.0), 5.0), "older sender times are accepted again after a reset (new player session)");
        }

        [Test]
        public void History_IsBounded()
        {
            var timeline = new SnapshotTimeline();
            Stream(timeline, 30.0);
            Assert.LessOrEqual(timeline.Count, 20);
        }
    }
}
