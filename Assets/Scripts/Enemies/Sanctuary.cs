using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Warded ground out in the wilds (the Gravekeeper's candle ring, the Emberwatch camp): nothing
    /// spawns inside, monsters won't follow the player in or attack anyone inside, and any that
    /// wander in walk back out. Registered by World.WorldBuilder.
    /// </summary>
    public static class Sanctuary
    {
        private static readonly List<Vector4> zones = new List<Vector4>(); // x, z, radius, unused

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            zones.Clear();
        }

        public static void Add(Vector3 center, float radius)
        {
            zones.Add(new Vector4(center.x, center.z, radius, 0f));
        }

        /// <summary>Whether a point is inside warded ground (grown by the margin).</summary>
        public static bool Contains(Vector3 p, float margin = 0f)
        {
            return Find(p, margin, out _);
        }

        /// <summary>The way out of the ward a point is in (flat, normalised), or zero if it's outside every ward.</summary>
        public static Vector3 WayOut(Vector3 p)
        {
            if (!Find(p, 0f, out Vector4 zone))
                return Vector3.zero;
            Vector3 away = new Vector3(p.x - zone.x, 0f, p.z - zone.y);
            return away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
        }

        private static bool Find(Vector3 p, float margin, out Vector4 found)
        {
            foreach (Vector4 zone in zones)
            {
                float dx = p.x - zone.x;
                float dz = p.z - zone.y;
                float r = zone.z + margin;
                if (dx * dx + dz * dz < r * r)
                {
                    found = zone;
                    return true;
                }
            }
            found = default;
            return false;
        }
    }
}
