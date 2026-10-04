using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Network.Replication
{
    /// <summary>
    /// Spectator-side playout buffer for <see cref="StateSnapshot"/>s. Snapshots arrive ~every
    /// 100ms with network jitter; rendering them as they land would make everything stutter. So
    /// this runs its own playback clock (<see cref="RenderTime"/>, on the sender's timeline) a
    /// little behind the newest snapshot, and the spectator always draws the world as it was at
    /// RenderTime by interpolating between the two snapshots either side of it. The delay adapts
    /// to measured jitter, and if data stops arriving the last motion is extrapolated briefly and
    /// then held, rather than freezing instantly or flying off.
    ///
    /// Pure logic with an injected clock (every method takes "localNow"), so it is unit tested
    /// without any networking or scene.
    /// </summary>
    public class SnapshotTimeline
    {
        public const float MinDelay = 0.12f;
        public const float MaxDelay = 0.6f;
        public const float MaxExtrapolation = 0.25f;

        /// <summary>Jumps further than this between two snapshots are teleports (area gate, revive): snap instead of sliding across the map.</summary>
        public const float TeleportDistance = 6f;

        // Beyond this much clock error the gap can't be smoothed out (e.g. the spectator's tab was
        // in the background) - jump straight to the right place.
        private const double ResyncThreshold = 1.0;
        private const float MaxRateAdjust = 0.2f;
        private const float RateGain = 0.8f;
        private const double HistorySeconds = 1.0;
        private const int MaxSnapshots = 64;

        private readonly List<StateSnapshot> snapshots = new List<StateSnapshot>();

        private bool clockStarted;
        private double lastAdvanceLocal;
        private double lastArrivalLocal;
        private double lastDueT = double.NegativeInfinity;
        private float rate = 1f;
        private float jitter;
        private float interval = 0.1f;

        public double RenderTime { get; private set; }
        public float InterpolationDelay { get; private set; } = MinDelay + 0.03f;
        public float Jitter => jitter;
        public int Count => snapshots.Count;
        public bool HasData => snapshots.Count > 0;
        public StateSnapshot Newest => snapshots.Count > 0 ? snapshots[snapshots.Count - 1] : null;

        /// <summary>How far RenderTime has run past the newest snapshot (0 while interpolating normally).</summary>
        public double Starvation => HasData ? Math.Max(0.0, RenderTime - Newest.t) : 0.0;

        public double SecondsSinceLastArrival(double localNow)
        {
            return HasData ? localNow - lastArrivalLocal : double.PositiveInfinity;
        }

        public void Clear()
        {
            snapshots.Clear();
            clockStarted = false;
            lastDueT = double.NegativeInfinity;
            rate = 1f;
            jitter = 0f;
            interval = 0.1f;
            InterpolationDelay = MinDelay + 0.03f;
        }

        /// <summary>Adds a snapshot that just arrived. Returns false (and ignores it) if it's a duplicate or older than what we already have.</summary>
        public bool Add(StateSnapshot s, double localNow)
        {
            if (s == null)
                return false;

            StateSnapshot newest = Newest;
            if (newest != null)
            {
                if (s.t <= newest.t)
                    return false;

                float sendGap = (float)(s.t - newest.t);
                float arrivalGap = (float)(localNow - lastArrivalLocal);

                // RFC 3550-style interarrival jitter: how much the spacing on arrival differs from
                // the spacing at send time. Long gaps (player tab hidden) aren't jitter, just silence.
                if (sendGap < 1f)
                {
                    jitter += (Mathf.Abs(arrivalGap - sendGap) - jitter) / 16f;
                    interval += (sendGap - interval) / 8f;
                }
            }

            snapshots.Add(s);
            lastArrivalLocal = localNow;

            // Enough buffer to cover one normal send interval plus a few standard jitters. Grows
            // quickly when the connection gets worse, shrinks slowly so it doesn't oscillate.
            float target = Mathf.Clamp(interval + 3f * jitter + 0.03f, MinDelay, MaxDelay);
            InterpolationDelay = target > InterpolationDelay
                ? Mathf.Lerp(InterpolationDelay, target, 0.5f)
                : Mathf.Lerp(InterpolationDelay, target, 0.05f);

            double ideal = s.t - InterpolationDelay;
            if (!clockStarted)
            {
                clockStarted = true;
                RenderTime = ideal;
                lastAdvanceLocal = localNow;
                rate = 1f;
            }
            else
            {
                double error = ideal - RenderTime;
                if (Math.Abs(error) > ResyncThreshold)
                {
                    RenderTime = ideal;
                    rate = 1f;
                }
                else
                {
                    // Nudge playback speed (at most +-20%, imperceptible on character motion)
                    // instead of jumping, so clock drift and latency changes are absorbed smoothly.
                    rate = 1f + Mathf.Clamp((float)error * RateGain, -MaxRateAdjust, MaxRateAdjust);
                }
            }

            Trim();
            return true;
        }

        /// <summary>Moves the playback clock forward by however much local time has passed.</summary>
        public void Advance(double localNow)
        {
            if (!clockStarted)
                return;

            double dt = Math.Max(0.0, localNow - lastAdvanceLocal);
            lastAdvanceLocal = localNow;
            RenderTime += dt * rate;

            // Don't run away indefinitely while starved; the next snapshot resyncs anyway.
            double cap = Newest.t + ResyncThreshold;
            if (RenderTime > cap)
                RenderTime = cap;
        }

        /// <summary>
        /// The two snapshots either side of RenderTime and how far between them we are. Alpha is
        /// 0..1 while interpolating, and goes past 1 (capped at MaxExtrapolation's worth) when
        /// RenderTime has run past the newest snapshot. Before the oldest snapshot - e.g. the
        /// very first one on joining - holds that one (From == To, alpha 0).
        /// </summary>
        public bool TryGetFrame(out StateSnapshot from, out StateSnapshot to, out float alpha)
        {
            from = to = null;
            alpha = 0f;
            if (snapshots.Count == 0)
                return false;

            int n = snapshots.Count;
            if (n == 1 || RenderTime <= snapshots[0].t)
            {
                from = to = snapshots[0];
                return true;
            }

            for (int k = n - 1; k >= 1; k--)
            {
                StateSnapshot a = snapshots[k - 1];
                if (RenderTime >= a.t)
                {
                    StateSnapshot b = snapshots[k];
                    double span = b.t - a.t;
                    double over = Math.Min(RenderTime - b.t, MaxExtrapolation);
                    double effective = RenderTime > b.t ? b.t + Math.Max(0.0, over) : RenderTime;
                    from = a;
                    to = b;
                    alpha = span > 1e-6 ? (float)((effective - a.t) / span) : 1f;
                    return true;
                }
            }

            from = to = snapshots[0];
            return true;
        }

        /// <summary>
        /// Snapshots RenderTime has reached since the last call, oldest first. Their discrete
        /// changes (swing started, died, area switched...) should be applied now, so events play
        /// back in sync with the interpolated motion instead of ahead of it.
        /// </summary>
        public void CollectDue(List<StateSnapshot> into)
        {
            for (int k = 0; k < snapshots.Count; k++)
            {
                StateSnapshot s = snapshots[k];
                if (s.t <= lastDueT)
                    continue;
                if (s.t > RenderTime)
                    break;
                into.Add(s);
                lastDueT = s.t;
            }
        }

        /// <summary>Linear position / shortest-arc yaw blend of one entity. Snaps on teleports and doesn't extrapolate the dead.</summary>
        public static void Interpolate(EntityState a, EntityState b, float alpha, EntityState result)
        {
            if (b == null || ReferenceEquals(a, b))
            {
                Copy(a, result);
                return;
            }

            float dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
            if (dx * dx + dy * dy + dz * dz > TeleportDistance * TeleportDistance)
            {
                Copy(alpha < 1f ? a : b, result);
                return;
            }

            if (alpha > 1f && (a.d != 0 || b.d != 0))
                alpha = 1f;

            Copy(alpha < 1f ? a : b, result);
            result.x = a.x + dx * alpha;
            result.y = a.y + dy * alpha;
            result.z = a.z + dz * alpha;
            result.r = a.r + Mathf.DeltaAngle(a.r, b.r) * alpha;
            result.bs = a.bs + (b.bs - a.bs) * alpha;
        }

        public static EntityState FindEnemy(StateSnapshot s, int id)
        {
            if (s?.e == null)
                return null;
            for (int k = 0; k < s.e.Length; k++)
            {
                if (s.e[k] != null && s.e[k].i == id)
                    return s.e[k];
            }
            return null;
        }

        private static void Copy(EntityState src, EntityState dst)
        {
            dst.i = src.i;
            dst.x = src.x;
            dst.y = src.y;
            dst.z = src.z;
            dst.r = src.r;
            dst.hp = src.hp;
            dst.mhp = src.mhp;
            dst.d = src.d;
            dst.atk = src.atk;
            dst.ap = src.ap;
            dst.stg = src.stg;
            dst.ch = src.ch;
            dst.bs = src.bs;
            dst.bp = src.bp;
            dst.bm = src.bm;
            dst.ba = src.ba;
            dst.bt = src.bt;
            dst.bl = src.bl;
        }

        // Keeps a second of history (plus the snapshot RenderTime is currently interpolating from)
        // so a late or reordered arrival still has its neighbours, without growing forever.
        private void Trim()
        {
            double keepAfter = RenderTime - HistorySeconds;
            while (snapshots.Count > 2 && (snapshots.Count > MaxSnapshots || (snapshots[1].t < keepAfter && snapshots[1].t <= RenderTime)))
                snapshots.RemoveAt(0);
        }
    }
}
