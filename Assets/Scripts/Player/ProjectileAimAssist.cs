using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;

namespace PoeClone.Player
{
    /// <summary>Mobile projectile correction around raw aim, with nearby enemies vetoing the snap.</summary>
    public static class ProjectileAimAssist
    {
        public const float MinimumDistance = 9f;
        public const float ConeHalfAngle = 15f;

        public static EnemyHealth Target(Vector3 origin, Vector3 direction, float range)
        {
            if (!TouchMode.Active) return null;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return null;

            EnemyHealth best = null;
            float bestAngle = float.MaxValue;
            foreach (EnemyHealth enemy in EnemyHealth.Active)
            {
                if (enemy == null || enemy.IsDead || enemy.IsAlly || !enemy.isActiveAndEnabled) continue;
                Vector3 offset = enemy.transform.position - origin;
                offset.y = 0f;
                float distanceSquared = offset.sqrMagnitude;
                // A coincident enemy counts as close, regardless of its undefined bearing.
                if (distanceSquared < 0.000001f) return null;
                float angle = Vector3.Angle(direction, offset);
                if (angle > ConeHalfAngle) continue;
                // Check every close enemy before selecting, independent of enumeration order.
                if (distanceSquared < MinimumDistance * MinimumDistance) return null;
                if (distanceSquared > range * range) continue;
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = enemy;
                }
            }
            return best;
        }

        public static Vector3 Direction(Vector3 origin, Vector3 rawDirection, float range)
        {
            EnemyHealth target = Target(origin, rawDirection, range);
            if (target == null) return rawDirection;
            Vector3 direction = target.transform.position - origin;
            direction.y = 0f;
            return direction.normalized;
        }
    }
}
