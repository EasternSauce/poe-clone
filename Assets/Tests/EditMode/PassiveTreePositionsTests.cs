using System;
using NUnit.Framework;
using PoeClone.Inventory;
using UnityEngine;

namespace PoeClone.Tests
{
    public class PassiveTreePositionsTests
    {
        private static PassiveTreePositions Load()
        {
            TextAsset asset = Resources.Load<TextAsset>(PassiveTreePositions.ResourceName);
            Assert.IsNotNull(asset, "The layout must be included in game builds.");
            return PassiveTreePositions.Parse(asset.text);
        }

        [Test]
        public void ResourceContainsEveryNodeAndMatchesRuntimePositions()
        {
            var layout = Load();
            layout.Validate(PassiveTree.Nodes);
            Assert.AreEqual(PassiveTree.Nodes.Count, layout.nodes.Count);
            foreach (var entry in layout.nodes)
            {
                var node = PassiveTree.Get(entry.id);
                Assert.AreEqual(entry.x, node.X, entry.id);
                Assert.AreEqual(entry.y, node.Y, entry.id);
            }
        }

        [Test]
        public void EditedCoordinatesSurviveJsonRoundTrip()
        {
            var layout = Load();
            layout.nodes[0].x = -12.34567f;
            layout.nodes[0].y = 9.87654f;
            var restored = PassiveTreePositions.Parse(JsonUtility.ToJson(layout, true));
            restored.Validate(PassiveTree.Nodes);
            Assert.AreEqual(layout.nodes[0].x, restored.nodes[0].x);
            Assert.AreEqual(layout.nodes[0].y, restored.nodes[0].y);
            Assert.AreEqual(layout.nodes[0].id, restored.nodes[0].id);
        }

        [Test]
        public void MissingDuplicateAndUnknownNodesAreRejected()
        {
            var layout = Load();
            layout.nodes.RemoveAt(0);
            Assert.Throws<InvalidOperationException>(() => layout.Validate(PassiveTree.Nodes));
            layout = Load();
            layout.nodes[0].id = layout.nodes[1].id;
            Assert.Throws<InvalidOperationException>(() => layout.Validate(PassiveTree.Nodes));
            layout = Load();
            layout.nodes[0].id = "unknown_node";
            Assert.Throws<InvalidOperationException>(() => layout.Validate(PassiveTree.Nodes));
        }

        [Test]
        public void NonFiniteCoordinatesAndUnsupportedVersionsAreRejected()
        {
            var layout = Load();
            layout.nodes[0].x = float.NaN;
            Assert.Throws<InvalidOperationException>(() => layout.Validate(PassiveTree.Nodes));
            layout.nodes[0].x = float.PositiveInfinity;
            Assert.Throws<InvalidOperationException>(() => layout.Validate(PassiveTree.Nodes));
            Assert.Throws<InvalidOperationException>(() => PassiveTreePositions.Parse("{\"version\":2,\"nodes\":[]}"));
        }
    }
}
