using System;

namespace PoeClone.Inventory
{
    /// <summary>Returning Volley counts distance from the first impact, never from launch or later hits.</summary>
    public sealed class ReturningProjectileFlight
    {
        public const float DistanceAfterHit = 3f;
        public const float DamageMultiplier = 0.6f;
        public bool HasHit { get; private set; }
        public bool Returning { get; private set; }
        public float RemainingDistance { get; private set; }
        public bool CanTurn => HasHit && !Returning && RemainingDistance <= 0.0001f;

        public void HitEnemy()
        {
            if (HasHit) return;
            HasHit = true;
            RemainingDistance = DistanceAfterHit;
        }

        public void Advance(float distance)
        {
            if (HasHit && !Returning) RemainingDistance = Math.Max(0f, RemainingDistance - Math.Max(0f, distance));
        }

        public bool BeginReturn()
        {
            if (!CanTurn) return false;
            Returning = true;
            return true;
        }
    }
}
