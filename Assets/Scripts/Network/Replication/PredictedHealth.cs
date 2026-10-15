using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Network.Replication
{
    /// <summary>Authoritative life minus only the hits the host has not processed yet.</summary>
    public sealed class PredictedHealth
    {
        private struct Hit
        {
            public int Sequence;
            public float Damage;
        }

        private readonly List<Hit> pending = new List<Hit>();
        private float authoritative;
        private float maximum;
        private int acknowledged;
        private int snapshotSequence = -1;

        public float Health { get; private set; }

        public float Predict(int sequence, float damage)
        {
            if (sequence <= acknowledged || damage <= 0f) return Health;
            foreach (Hit hit in pending)
                if (hit.Sequence == sequence) return Health;
            pending.Add(new Hit { Sequence = sequence, Damage = damage });
            return Recalculate();
        }

        public float ApplySnapshot(float health, float max, int processedHit, int snapshot)
        {
            // A late snapshot must not resurrect acknowledged predictions or replace newer life.
            if (snapshot < snapshotSequence) return Health;
            snapshotSequence = snapshot;
            maximum = Mathf.Max(0f, max);
            authoritative = Mathf.Clamp(health, 0f, maximum);
            acknowledged = System.Math.Max(acknowledged, processedHit);
            pending.RemoveAll(hit => hit.Sequence <= acknowledged);
            return Recalculate();
        }

        private float Recalculate()
        {
            float health = authoritative;
            foreach (Hit hit in pending) health -= hit.Damage;
            Health = Mathf.Clamp(health, 0f, maximum);
            return Health;
        }
    }
}
