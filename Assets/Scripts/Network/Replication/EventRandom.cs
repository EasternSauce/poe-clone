using System;
using UnityEngine;

namespace PoeClone.Network.Replication
{
    /// <summary>
    /// A roll is a pure function of an event seed, purpose and target, never of draw order.
    /// Send the seed when replaying an event; cosmetics cannot consume its combat rolls.
    /// </summary>
    public static class EventRandom
    {
        // Keep purpose IDs stable: different decisions must never share a key.
        public const int Critical = 1, Evade = 2, Block = 3, Stagger = 4, DamageSpread = 5;
        public const int RainAngle = 6, RainRadius = 7;
        public const int StatPurposeOffset = 1024;
        private static uint salt = unchecked((uint)Guid.NewGuid().GetHashCode());
        private static uint sequence;

        public static int NextSeed()
        {
            uint seed = Mix(unchecked(salt + ++sequence));
            return unchecked((int)(seed == 0 ? 1u : seed));
        }

        public static float Value(int seed, int purpose, int target = 0)
        {
            uint bits = Mix(unchecked((uint)seed) ^ Mix(unchecked((uint)purpose)) ^
                            Mix(unchecked((uint)target + 0x9e3779b9u)));
            return (bits >> 8) * (1f / 16777216f);
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            salt = unchecked((uint)Guid.NewGuid().GetHashCode());
            sequence = 0;
        }
    }
}
