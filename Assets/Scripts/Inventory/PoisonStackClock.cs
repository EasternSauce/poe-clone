using System;

namespace PoeClone.Inventory
{
    /// <summary>A poison's immutable potency and original ticking deadline survive propagation.</summary>
    public sealed class PoisonStackClock
    {
        public const float TickSeconds = 0.5f;
        public float DamagePerTick { get; }
        public float NextTick { get; private set; }
        public float ExpiresAt { get; }
        public bool Finished => NextTick > ExpiresAt + 0.0001f;

        public PoisonStackClock(float damagePerTick, int ticks, float now)
            : this(damagePerTick, now + TickSeconds, now + Math.Max(1, ticks) * TickSeconds) { }

        private PoisonStackClock(float damage, float next, float expiry)
        {
            DamagePerTick = damage;
            NextTick = next;
            ExpiresAt = expiry;
        }

        public bool TryTick(float now, out float damage)
        {
            damage = 0f;
            if (Finished || now + 0.0001f < NextTick) return false;
            damage = DamagePerTick;
            NextTick += TickSeconds;
            return true;
        }

        public bool CanSpread(float now) => !Finished && ExpiresAt > now + 0.0001f;
        public PoisonStackClock Copy() => new PoisonStackClock(DamagePerTick, NextTick, ExpiresAt);
        public bool Matches(PoisonStackClock other) => other != null &&
            DamagePerTick == other.DamagePerTick && NextTick == other.NextTick && ExpiresAt == other.ExpiresAt;
    }
}
