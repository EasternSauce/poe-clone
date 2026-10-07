using System;
using System.Collections.Generic;
using NUnit.Framework;
using PoeClone.Inventory;
using UnityEngine;

namespace PoeClone.Tests
{
    public class PassiveTreeSpacingTests
    {
        private static PassiveNode Node(string id) { return new PassiveNode { Id = id }; }
        private static void Link(PassiveNode a, PassiveNode b) { a.Links.Add(b.Id); b.Links.Add(a.Id); }
        private static PassiveTreePositions Layout(params PassiveNode[] graph)
        {
            var layout = new PassiveTreePositions();
            foreach (var node in graph) layout.nodes.Add(new PassiveTreePositions.Entry { id = node.Id, x = node.X, y = node.Y });
            return layout;
        }
        private static float Distance(PassiveTreePositions layout, string a, string b)
        {
            var p = layout.nodes.Find(n => n.id == a);
            var q = layout.nodes.Find(n => n.id == b);
            return Vector2.Distance(new Vector2(p.x, p.y), new Vector2(q.x, q.y));
        }

        [TestCase(0.7f)]
        [TestCase(0.88f)]
        [TestCase(10f)]
        public void LongConnectionIsPulledAlongItsHeadingAndShortConnectionIsUnchanged(float length)
        {
            var graph = new[] { Node("origin"), Node("a") };
            Link(graph[0], graph[1]);
            graph[0].X = 7;
            graph[0].Y = -3;
            graph[1].X = 7 + length * 0.6f;
            graph[1].Y = -3 + length * 0.8f;
            var layout = Layout(graph);
            PassiveTreeSpacing.Apply(layout, graph);
            float expected = Mathf.Min(length, 4 * PassiveTreeSpacing.BasicDiameter);
            Assert.AreEqual(7, layout.nodes[0].x);
            Assert.AreEqual(-3, layout.nodes[0].y);
            Assert.That(layout.nodes[1].x, Is.EqualTo(7 + expected * 0.6f).Within(1e-6f));
            Assert.That(layout.nodes[1].y, Is.EqualTo(-3 + expected * 0.8f).Within(1e-6f));
        }

        [Test]
        public void PullUsesMovedParentAndPushResolvesOverlapWithEarlierLocks()
        {
            var graph = new[] { Node("origin"), Node("a"), Node("b") };
            Link(graph[0], graph[1]);
            Link(graph[1], graph[2]);
            graph[1].X = 10;
            graph[2].X = -10;
            var layout = Layout(graph);
            PassiveTreeSpacing.Apply(layout, graph);
            Assert.That(layout.nodes[1].x, Is.EqualTo(0.88f).Within(1e-6f));
            // Pulling b toward the moved a puts it at the origin. Push must then clear both locks.
            Assert.GreaterOrEqual(Distance(layout, "origin", "b"), 0.44f - 1e-6f);
            Assert.GreaterOrEqual(Distance(layout, "a", "b"), 0.44f - 1e-6f);
            Assert.Less(Distance(layout, "a", "b"), 2f);
        }

        [Test]
        public void CoincidentCyclePreservesSpacingAndOriginAcrossPasses()
        {
            var graph = new[] { Node("origin"), Node("a"), Node("b"), Node("c") };
            for (int i = 0; i < graph.Length; i++) Link(graph[i], graph[(i + 1) % graph.Length]);
            graph[0].X = 7;
            graph[0].Y = -3;
            foreach (var node in graph) { node.X = 7; node.Y = -3; }
            var layout = Layout(graph);
            Assert.AreEqual(3, PassiveTreeSpacing.Apply(layout, graph));
            Assert.AreEqual(7, layout.nodes[0].x);
            Assert.AreEqual(-3, layout.nodes[0].y);
            for (int i = 0; i < graph.Length; i++)
                for (int j = i + 1; j < graph.Length; j++)
                    Assert.GreaterOrEqual(Distance(layout, graph[i].Id, graph[j].Id), 3 * PassiveTreeSpacing.BasicDiameter - 1e-6f);
            PassiveTreeSpacing.Apply(layout, graph);
            Assert.AreEqual(7, layout.nodes[0].x);
            Assert.AreEqual(-3, layout.nodes[0].y);
            for (int i = 0; i < graph.Length; i++)
                for (int j = i + 1; j < graph.Length; j++)
                    Assert.GreaterOrEqual(Distance(layout, graph[i].Id, graph[j].Id), 3 * PassiveTreeSpacing.BasicDiameter - 1e-6f);
        }

        [Test]
        public void DeadEndExceptionIncludesAttachmentButNotOtherTails()
        {
            var graph = new[] { Node("origin"), Node("branch"), Node("a"), Node("b"), Node("unrelated") };
            Link(graph[0], graph[1]);
            Link(graph[1], graph[2]);
            Link(graph[2], graph[3]);
            Link(graph[1], graph[4]);
            graph[0].X = -0.8f;
            graph[2].X = 0.1f;
            graph[3].X = 0.4f;
            graph[4].Y = 0.1f;
            var layout = Layout(graph);
            PassiveTreeSpacing.Apply(layout, graph);
            Assert.That(Distance(layout, "branch", "a"), Is.InRange(0.43999f, 0.4401f));
            Assert.GreaterOrEqual(Distance(layout, "branch", "unrelated"), 0.44f - 1e-6f);
            Assert.GreaterOrEqual(Distance(layout, "a", "unrelated"), 0.66f - 1e-6f);
            Assert.GreaterOrEqual(Distance(layout, "b", "unrelated"), 0.66f - 1e-6f);
        }

        [Test]
        public void FiveNodeTailQualifiesButSixNodeTailDoesNot()
        {
            foreach (int length in new[] { 5, 6 })
            {
                var graph = new List<PassiveNode> { Node("origin") };
                for (int i = 1; i <= length; i++) { graph.Add(Node("tail" + i)); Link(graph[i - 1], graph[i]); }
                var layout = Layout(graph.ToArray());
                PassiveTreeSpacing.Apply(layout, graph);
                float expected = PassiveTreeSpacing.BasicDiameter * (length == 5 ? 2 : 3);
                Assert.That(Distance(layout, "origin", "tail1"), Is.InRange(expected - 1e-6f, expected + 0.0001f));
            }
        }

        [Test]
        public void AlternatePushAvoidsCrossingAnAlreadyLockedConnection()
        {
            var graph = new[] { Node("origin"), Node("a"), Node("b"), Node("target") };
            Link(graph[0], graph[1]);
            Link(graph[0], graph[2]);
            Link(graph[1], graph[3]);
            Link(graph[2], graph[3]);
            graph[1].X = 2;
            graph[2].X = 1;
            graph[2].Y = 0.3f;
            graph[3].X = 1;
            graph[3].Y = 0.2f;
            var layout = Layout(graph);
            PassiveTreeSpacing.Apply(layout, graph);
            // A downward push from b would make b-target cross the locked origin-a edge.
            Assert.Greater(layout.nodes.Find(n => n.id == "target").y, 0.13f);
            for (int i = 0; i < graph.Length; i++)
                for (int j = i + 1; j < graph.Length; j++)
                    Assert.GreaterOrEqual(Distance(layout, graph[i].Id, graph[j].Id), 0.66f - 1e-6f);
        }

        [Test]
        public void ScratchArrangementIgnoresOldPositionsAndPreservesMinimumSpacing()
        {
            var graph = new[] { Node("origin"), Node("a"), Node("b"), Node("c") };
            for (int i = 0; i < graph.Length; i++) Link(graph[i], graph[(i + 1) % graph.Length]);
            var first = Layout(graph);
            var second = Layout(graph);
            for (int i = 0; i < second.nodes.Count; i++)
            {
                second.nodes[i].x = 100 + i * 13;
                second.nodes[i].y = -300 + i * 21;
                graph[i].X = -200;
                graph[i].Y = 400;
            }
            PassiveTreeSpacing.ArrangeFromScratch(first, graph);
            PassiveTreeSpacing.ArrangeFromScratch(second, graph);
            Assert.AreEqual(JsonUtility.ToJson(first), JsonUtility.ToJson(second));
            Assert.AreEqual(0, first.nodes[0].x);
            Assert.AreEqual(0, first.nodes[0].y);
            for (int i = 0; i < graph.Length; i++)
            {
                for (int j = i + 1; j < graph.Length; j++)
                    Assert.GreaterOrEqual(Distance(first, graph[i].Id, graph[j].Id), 0.66f - 1e-6f);
            }
            Assert.AreEqual(0, PassiveTreeSpacing.ArrangeFromScratch(first, graph));
        }

        [Test]
        public void ScratchArrangementUsesCompactSpacingForShortDeadEnds()
        {
            var graph = new[] { Node("origin"), Node("a"), Node("b") };
            Link(graph[0], graph[1]);
            Link(graph[1], graph[2]);
            var layout = Layout(graph);
            PassiveTreeSpacing.ArrangeFromScratch(layout, graph);
            Assert.That(Distance(layout, "origin", "a"), Is.InRange(0.44f - 1e-6f, 0.4401f));
            Assert.GreaterOrEqual(Distance(layout, "a", "b"), 0.44f - 1e-6f);
        }

        [Test]
        public void UnreachableScratchArrangementLeavesDraftUnchanged()
        {
            var graph = new[] { Node("origin"), Node("unreachable") };
            var layout = Layout(graph);
            string before = JsonUtility.ToJson(layout);
            Assert.Throws<InvalidOperationException>(() => PassiveTreeSpacing.ArrangeFromScratch(layout, graph));
            Assert.AreEqual(before, JsonUtility.ToJson(layout));
        }

        [Test]
        public void AuthoredTreeCanBeArrangedFromScratchWithValidSpacing()
        {
            var asset = Resources.Load<TextAsset>(PassiveTreePositions.ResourceName);
            var layout = PassiveTreePositions.Parse(asset.text);
            PassiveTreeSpacing.ArrangeFromScratch(layout, PassiveTree.Nodes);
            layout.Validate(PassiveTree.Nodes);
            // Space tree now also pulls long BFS connections before resolving spacing.
            PassiveTreeSpacing.Apply(layout, PassiveTree.Nodes);
            layout.Validate(PassiveTree.Nodes);
        }

        [Test]
        public void UnreachableNodesAreRejectedBeforeChangingLayout()
        {
            var graph = new[] { Node("origin"), Node("unreachable") };
            var layout = Layout(graph);
            string before = JsonUtility.ToJson(layout);
            Assert.Throws<InvalidOperationException>(() => PassiveTreeSpacing.Apply(layout, graph));
            Assert.AreEqual(before, JsonUtility.ToJson(layout));
        }

        [Test]
        public void AuthoredTreeRemainsValidAndDeterministicAfterSpacing()
        {
            var asset = Resources.Load<TextAsset>(PassiveTreePositions.ResourceName);
            var layout = PassiveTreePositions.Parse(asset.text);
            var origin = layout.nodes.Find(n => n.id == "origin");
            Vector2 before = new Vector2(origin.x, origin.y);
            var repeat = PassiveTreePositions.Parse(asset.text);
            PassiveTreeSpacing.Apply(layout, PassiveTree.Nodes);
            PassiveTreeSpacing.Apply(repeat, PassiveTree.Nodes);
            layout.Validate(PassiveTree.Nodes);
            Assert.AreEqual(before, new Vector2(origin.x, origin.y));
            Assert.AreEqual(JsonUtility.ToJson(layout), JsonUtility.ToJson(repeat));
            for (int i = 0; i < layout.nodes.Count; i++)
                for (int j = i + 1; j < layout.nodes.Count; j++)
                    Assert.GreaterOrEqual(Distance(layout, layout.nodes[i].id, layout.nodes[j].id),
                        2 * PassiveTreeSpacing.BasicDiameter - 1e-6f);
        }
    }
}
