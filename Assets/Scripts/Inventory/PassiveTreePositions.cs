using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>The shared JSON format used by the game and the position editor.</summary>
    [Serializable]
    public sealed class PassiveTreePositions
    {
        public const string ResourceName = "PassiveTreePositions";
        public const string AssetPath = "Assets/Resources/PassiveTreePositions.json";
        public int version = 1;
        public List<Entry> nodes = new List<Entry>();

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public float x;
            public float y;
        }

        public static PassiveTreePositions Parse(string json)
        {
            PassiveTreePositions result = JsonUtility.FromJson<PassiveTreePositions>(json);
            if (result == null || result.version != 1 || result.nodes == null)
                throw new InvalidOperationException("Invalid passive layout or unsupported version (expected 1).");
            return result;
        }

        // Check the whole file before applying anything to avoid partial layouts.
        public void Validate(IReadOnlyList<PassiveNode> graph)
        {
            if (version != 1 || nodes == null)
                throw new InvalidOperationException("Invalid passive layout version or nodes.");
            var expected = new HashSet<string>();
            foreach (PassiveNode node in graph) expected.Add(node.Id);
            var seen = new HashSet<string>();
            foreach (Entry entry in nodes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || !expected.Contains(entry.id))
                    throw new InvalidOperationException("Layout references an unknown or empty node ID.");
                if (!seen.Add(entry.id))
                    throw new InvalidOperationException("Duplicate position for passive: " + entry.id);
                if (float.IsNaN(entry.x) || float.IsInfinity(entry.x) ||
                    float.IsNaN(entry.y) || float.IsInfinity(entry.y))
                    throw new InvalidOperationException("Non-finite position for passive: " + entry.id);
            }
            if (seen.Count != expected.Count)
                throw new InvalidOperationException("The layout must include every passive node.");
        }
    }
}
