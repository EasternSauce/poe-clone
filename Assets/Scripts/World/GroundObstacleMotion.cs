using UnityEngine;

namespace PoeClone.World
{
    /// <summary>Checks scenery at ground level, even when a character's visual arc is airborne.</summary>
    internal static class GroundObstacleMotion
    {
        public static Vector3 Clamp(CharacterController body, Vector3 from, Vector3 target)
        {
            return Sweep(body, from, target, out _);
        }

        public static Vector3 Slide(CharacterController body, Vector3 from, Vector3 target)
        {
            Vector3 stopped = Sweep(body, from, target, out Vector3 normal);
            if (normal.sqrMagnitude < 0.001f) return stopped;
            Vector3 remaining = target - stopped;
            remaining.y = 0f;
            Vector3 slide = Vector3.ProjectOnPlane(remaining, normal);
            return Sweep(body, stopped, stopped + slide, out _);
        }

        private static Vector3 Sweep(CharacterController body, Vector3 from, Vector3 target, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (body == null) return target;
            Vector3 path = target - from;
            path.y = 0f;
            float distance = path.magnitude;
            if (distance < 0.0001f) return target;

            Vector3 scale = body.transform.lossyScale;
            float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(radius * 2f, body.height * Mathf.Abs(scale.y));
            Vector3 center = from + body.transform.TransformVector(body.center);
            float feet = center.y - height * 0.5f;
            // A flat-bottomed footprint prevents capsule edges from riding up low furniture.
            Vector3 half = new Vector3(radius, (height - 0.1f) * 0.5f, radius);
            center.y += 0.05f;
            float allowed = distance;
            foreach (RaycastHit hit in Physics.BoxCastAll(center, half, path / distance,
                Quaternion.identity, distance + 0.04f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Collider obstacle = hit.collider;
                if (obstacle == null || obstacle.transform.IsChildOf(body.transform) ||
                    obstacle is CharacterController || obstacle.GetComponentInParent<Enemies.EnemyHealth>() != null ||
                    obstacle.GetComponentInParent<Player.PlayerStats>() != null ||
                    obstacle.bounds.max.y <= feet + 0.15f)
                    continue;
                Vector3 side = hit.normal;
                side.y = 0f;
                if (side.sqrMagnitude < 0.001f) continue;
                float stop = Mathf.Max(0f, hit.distance - 0.04f);
                if (stop >= allowed) continue;
                allowed = stop;
                normal = side.normalized;
            }
            Vector3 result = from + path / distance * allowed;
            result.y = target.y;
            return result;
        }
    }
}
