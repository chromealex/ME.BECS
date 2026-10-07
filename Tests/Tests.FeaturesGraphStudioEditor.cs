namespace ME.BECS.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using ME.BECS.Extensions.GraphProcessor;
    using ME.BECS.FeaturesGraph;
    using ME.BECS.FeaturesGraph.Nodes;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class Tests_FeaturesGraphStudioEditor {
        private SystemsGraph graph;
        private static Type EditorType(string name) => Assembly.Load("ME.BECS.Features.Editor")
            .GetType("ME.BECS.Editor.FeaturesGraph." + name, true);
        private static MethodInfo Command(string name) => EditorType("FeaturesGraphStudioCommands")
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() {
            this.graph = ScriptableObject.CreateInstance<SystemsGraph>();
            this.graph.InitializeValidation();
        }

        [TearDown]
        public void TearDown() {
            Undo.ClearUndo(this.graph);
            UnityEngine.Object.DestroyImmediate(this.graph);
        }

        private SystemNode System() => (SystemNode)this.graph.AddNode(BaseNode.CreateFromType<SystemNode>(new Vector2(37, 91)));
        private void Link(BaseNode source, BaseNode target) => this.graph.Connect(target.inputPorts[0], source.outputPorts[0]);
        private bool Linked(BaseNode source, BaseNode target) => this.graph.edges.Any(edge => edge.outputNode == source && edge.inputNode == target);
        private BaseNode Insert(BaseNode anchor, bool parallel, out string error) {
            var args = new object[] { this.graph, typeof(SystemNode), anchor, parallel, null };
            var node = (BaseNode)Command("Insert").Invoke(null, args);
            error = (string)args[4];
            return node;
        }

        [Test]
        public void InsertAfterRewiresEverySuccessorAndPreservesOtherBranches() {
            var start = this.graph.GetStartNode(0); var end = this.graph.GetEndNode();
            var a = this.System(); var b = this.System(); var c = this.System();
            this.Link(start, a); this.Link(a, b); this.Link(a, c); this.Link(b, end); this.Link(c, end);
            var position = a.position;
            var inserted = this.Insert(a, false, out var error);
            Assert.IsNull(error); Assert.IsNotNull(inserted);
            Assert.IsTrue(this.Linked(a, inserted));
            Assert.IsTrue(this.Linked(inserted, b)); Assert.IsTrue(this.Linked(inserted, c));
            Assert.IsFalse(this.Linked(a, b)); Assert.IsFalse(this.Linked(a, c));
            Assert.IsTrue(this.Linked(b, end)); Assert.IsTrue(this.Linked(c, end));
            Assert.AreEqual(position, a.position, "Editor insertion must preserve the legacy layout.");
        }

        [Test]
        public void InsertOnConnectionPreservesTheOtherSuccessor() {
            var a = this.System(); var b = this.System(); var c = this.System();
            this.Link(a, b); this.Link(a, c);
            var edge = this.graph.edges.Single(value => value.inputNode == b);
            var args = new object[] { this.graph, typeof(SystemNode), edge, null };
            var inserted = (BaseNode)Command("InsertOnEdge").Invoke(null, args);
            Assert.IsNull(args[3]); Assert.IsNotNull(inserted);
            Assert.IsTrue(this.Linked(a, inserted)); Assert.IsTrue(this.Linked(inserted, b));
            Assert.IsFalse(this.Linked(a, b)); Assert.IsTrue(this.Linked(a, c));
            Assert.IsFalse(this.Linked(inserted, c));
        }

        [Test]
        public void ParallelNodeSharesAllPredecessorsAndAllJoinTargets() {
            var start = this.graph.GetStartNode(0); var end = this.graph.GetEndNode();
            var p = this.System(); var q = this.System(); var a = this.System(); var tail = this.System();
            this.Link(start, p); this.Link(start, q); this.Link(p, a); this.Link(q, a); this.Link(a, tail); this.Link(tail, end);
            var sibling = this.Insert(a, true, out var error);
            Assert.IsNull(error); Assert.IsNotNull(sibling);
            foreach (var parent in new BaseNode[] { p, q }) {
                Assert.IsTrue(this.Linked(parent, a)); Assert.IsTrue(this.Linked(parent, sibling));
            }
            Assert.IsTrue(this.Linked(a, tail)); Assert.IsTrue(this.Linked(sibling, tail));
            Assert.IsFalse(this.Linked(a, sibling)); Assert.IsFalse(this.Linked(sibling, a));
        }

        [Test]
        public void ParallelInsertionNeverOverwritesSingleCapacityPorts() {
            var start = this.graph.GetStartNode(0); var end = this.graph.GetEndNode(); var a = this.System();
            this.Link(start, a); this.Link(a, end);
            start.outputPorts[0].portData.acceptMultipleEdges = false;
            var count = this.graph.nodes.Count; var edges = this.graph.edges.ToArray();
            Assert.IsNull(this.Insert(a, true, out var error)); Assert.IsNotNull(error);
            Assert.AreEqual(count, this.graph.nodes.Count);
            CollectionAssert.AreEqual(edges, this.graph.edges);
        }

        [Test]
        public void ReconnectingInputReplacesOnlyTheChosenDependency() {
            var a = this.System(); var b = this.System(); var c = this.System();
            this.Link(a, b); this.Link(a, c);
            var old = this.graph.edges.Single(edge => edge.inputNode == b);
            var args = new object[] { this.graph, b.inputPorts[0], c.outputPorts[0], null, new[] { old } };
            Assert.IsTrue((bool)Command("Connect").Invoke(null, args));
            Assert.IsNull(args[3]); Assert.IsFalse(this.Linked(a, b));
            Assert.IsTrue(this.Linked(c, b)); Assert.IsTrue(this.Linked(a, c));
        }

        [Test]
        public void ReconnectingOutputRemovesOldLinksAndPreservesUnrelatedBranches() {
            var a = this.System(); var b = this.System(); var c = this.System(); var d = this.System();
            this.Link(a, b); this.Link(a, c); this.Link(c, d);
            var old = this.graph.edges.Where(edge => edge.outputNode == a).ToArray();
            var args = new object[] { this.graph, d.inputPorts[0], a.outputPorts[0], null, old };
            Assert.IsTrue((bool)Command("Connect").Invoke(null, args));
            Assert.IsFalse(this.Linked(a, b)); Assert.IsFalse(this.Linked(a, c));
            Assert.IsTrue(this.Linked(a, d)); Assert.IsTrue(this.Linked(c, d));
        }

        [Test]
        public void InvalidReconnectionKeepsOldLinks() {
            var a = this.System(); var b = this.System(); var c = this.System();
            this.Link(a, b); this.Link(b, c);
            var old = this.graph.edges.Where(edge => edge.inputNode == b).ToArray();
            var before = this.graph.edges.ToArray();
            var args = new object[] { this.graph, b.inputPorts[0], c.outputPorts[0], null, old };
            Assert.IsFalse((bool)Command("Connect").Invoke(null, args));
            CollectionAssert.AreEqual(before, this.graph.edges);
        }

        [Test]
        public void DeleteReconnectsEveryNeighbourWithoutDuplicatingExistingLinks() {
            var p = this.System(); var q = this.System(); var node = this.System();
            var a = this.System(); var b = this.System();
            this.Link(p, node); this.Link(q, node); this.Link(node, a); this.Link(node, b); this.Link(p, a);
            var args = new object[] { this.graph, node, null };
            Assert.IsTrue((bool)Command("Delete").Invoke(null, args)); Assert.IsNull(args[2]);
            Assert.IsFalse(this.graph.nodes.Contains(node));
            foreach (var parent in new[] { p, q }) foreach (var child in new[] { a, b })
                Assert.AreEqual(1, this.graph.edges.Count(edge => edge.outputNode == parent && edge.inputNode == child));
        }

        [Test]
        public void DeleteWithIncompatibleCapacityLeavesGraphUnchanged() {
            var p = this.System(); var node = this.System(); var a = this.System(); var b = this.System();
            this.Link(p, node); this.Link(node, a); this.Link(node, b);
            p.outputPorts[0].portData.acceptMultipleEdges = false;
            var before = this.graph.edges.ToArray();
            var args = new object[] { this.graph, node, null };
            Assert.IsFalse((bool)Command("Delete").Invoke(null, args)); Assert.IsNotNull(args[2]);
            Assert.IsTrue(this.graph.nodes.Contains(node)); CollectionAssert.AreEqual(before, this.graph.edges);
        }

        [Test]
        public void ConnectingParallelBranchPreservesTheExistingBranchWhenTheyJoinLater() {
            var source = this.System(); var fire = this.System(); var owner = this.System();
            var tail = this.System(); var join = this.System();
            this.Link(source, fire); this.Link(fire, tail); this.Link(tail, join); this.Link(owner, join);
            var old = this.graph.edges.Where(edge => edge.outputNode == source).ToArray();
            var args = new object[] { this.graph, owner.inputPorts[0], source.outputPorts[0], null, old };
            Assert.IsTrue((bool)Command("Connect").Invoke(null, args)); Assert.IsNull(args[3]);
            Assert.IsTrue(this.Linked(source, fire)); Assert.IsTrue(this.Linked(source, owner));
            Assert.IsTrue(this.Linked(fire, tail)); Assert.IsTrue(this.Linked(tail, join)); Assert.IsTrue(this.Linked(owner, join));
        }

        [Test]
        public void SequentialReconnectionStillRemovesTheOldLinkEvenWithALaterJoin() {
            var source = this.System(); var a = this.System(); var b = this.System(); var join = this.System();
            this.Link(source, a); this.Link(a, b); this.Link(b, join);
            var old = this.graph.edges.Where(edge => edge.outputNode == source).ToArray();
            var args = new object[] { this.graph, b.inputPorts[0], source.outputPorts[0], null, old };
            Assert.IsTrue((bool)Command("Connect").Invoke(null, args));
            Assert.IsFalse(this.Linked(source, a)); Assert.IsTrue(this.Linked(source, b));
        }

        [Test]
        public void ConnectingSequenceRemovesTheBypassAndKeepsIndependentBranches() {
            var source = this.System(); var owner = this.System(); var fire = this.System();
            var parallel = this.System(); var tail = this.System();
            this.Link(source, owner); this.Link(source, fire); this.Link(source, parallel);
            this.Link(fire, tail); this.Link(parallel, tail);
            var args = new object[] { this.graph, fire.inputPorts[0], owner.outputPorts[0], null, null };
            Assert.IsTrue((bool)Command("Connect").Invoke(null, args));
            Assert.IsFalse(this.Linked(source, fire));
            Assert.IsTrue(this.Linked(source, owner)); Assert.IsTrue(this.Linked(owner, fire));
            Assert.IsTrue(this.Linked(source, parallel)); Assert.IsTrue(this.Linked(parallel, tail));
            Assert.IsTrue(this.Linked(fire, tail));
        }

        [Test]
        public void ConnectingLongerSequenceRemovesTheTransitiveBypass() {
            var source = this.System(); var a = this.System(); var b = this.System(); var target = this.System();
            this.Link(source, a); this.Link(a, b); this.Link(source, target);
            var args = new object[] { this.graph, target.inputPorts[0], b.outputPorts[0], null, null };
            Assert.IsTrue((bool)Command("Connect").Invoke(null, args));
            Assert.IsFalse(this.Linked(source, target)); Assert.AreEqual(3, this.graph.edges.Count);
        }

        [Test]
        public void ManualCycleIsRejectedWithoutChangingTheAsset() {
            var a = this.System(); var b = this.System(); this.Link(a, b);
            var args = new object[] { this.graph, a.inputPorts[0], b.outputPorts[0], null, null };
            Assert.IsFalse((bool)Command("Connect").Invoke(null, args));
            Assert.IsNotNull(args[3]); Assert.AreEqual(1, this.graph.edges.Count);
        }

        [Test]
        public void InsertUsesOneUndoStepForNodeAndConnections() {
            var start = this.graph.GetStartNode(0); var end = this.graph.GetEndNode(); var a = this.System();
            this.Link(start, a); this.Link(a, end);
            Undo.IncrementCurrentGroup();
            Assert.IsNotNull(this.Insert(a, false, out _));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); this.graph.Deserialize();
            Assert.AreEqual(3, this.graph.nodes.Count); Assert.AreEqual(2, this.graph.edges.Count);
            Assert.IsTrue(this.graph.edges.Any(edge => edge.outputNode is SystemNode && edge.inputNode is ExitNode));
        }

        [Test]
        public void PanningCannotLeaveContentBoundsOrScrollSmallGraphs() {
            var clamp = EditorType("FeaturesGraphStudioLayout").GetMethod("ClampScroll", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(new Vector2(0, 400), clamp.Invoke(null, new object[] { new Vector2(-10, 900), new Vector2(1200, 900), new Vector2(800, 500) }));
            Assert.AreEqual(Vector2.zero, clamp.Invoke(null, new object[] { new Vector2(100, 100), new Vector2(300, 200), new Vector2(800, 500) }));
            Assert.AreEqual(Vector2.zero, clamp.Invoke(null, new object[] { new Vector2(float.NaN, float.PositiveInfinity), new Vector2(1200, 900), new Vector2(800, 500) }));
        }

        [Test]
        public void PhaseMaskSupportsAllNoneAndIndependentMethods() {
            var metadata = EditorType("FeaturesGraphStudioMetadata");
            var bit = metadata.GetMethod("PhaseBit", BindingFlags.Static | BindingFlags.NonPublic);
            var includes = metadata.GetMethod("Includes", BindingFlags.Static | BindingFlags.NonPublic);
            var awake = (int)bit.Invoke(null, new object[] { Method.Awake });
            var update = (int)bit.Invoke(null, new object[] { Method.Update });
            Assert.AreNotEqual(awake, update);
            Assert.IsTrue((bool)includes.Invoke(null, new object[] { awake | update, Method.Awake }));
            Assert.IsTrue((bool)includes.Invoke(null, new object[] { awake | update, Method.Update }));
            Assert.IsFalse((bool)includes.Invoke(null, new object[] { awake | update, Method.Start }));
            Assert.IsFalse((bool)includes.Invoke(null, new object[] { 0, Method.Update }));
        }

        [Test]
        public void LayeringAlignsBranchesAndPlacesJoinAfterLongestDependency() {
            var edges = new List<(int, int)> { (0, 1), (0, 2), (1, 3), (2, 3), (0, 3), (0, 1) };
            var args = new object[] { 4, edges, 0 };
            var ranks = (int[])EditorType("FeaturesGraphStudioLayout").GetMethod("GetLayers", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 2 }, ranks); Assert.AreEqual(0, args[2]);
        }

        [Test]
        public void CyclicAndDisconnectedNodesRemainVisible() {
            var args = new object[] { 5, new List<(int, int)> { (0, 1), (1, 2), (2, 1), (2, 3) }, 0 };
            var ranks = (int[])EditorType("FeaturesGraphStudioLayout").GetMethod("GetLayers", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            Assert.AreEqual(5, ranks.Length); Assert.AreEqual(3, args[2]); Assert.AreEqual(0, ranks[4]);
        }

        [Test]
        public void VisualRelayRequiresCompleteConnectionsAndNeverChangesTheAsset() {
            var a = this.System(); var b = this.System(); var c = this.System(); var d = this.System();
            this.Link(a, c); this.Link(a, d); this.Link(b, c); this.Link(b, d);
            var edges = this.graph.edges.ToArray(); var nodes = this.graph.nodes.ToArray();
            var windowType = EditorType("FeaturesGraphStudioWindow");
            var editor = (EditorWindow)ScriptableObject.CreateInstance(windowType);
            try {
                windowType.GetMethod("CreateGUI").Invoke(editor, null);
                windowType.GetMethod("OpenRoot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { this.graph });
                var canvas = editor.rootVisualElement.Q(className: "studio-canvas");
                var relaysField = canvas.GetType().GetField("relayEdges", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.AreEqual(4, ((System.Collections.IDictionary)relaysField.GetValue(canvas)).Count);
                CollectionAssert.AreEqual(nodes, this.graph.nodes); CollectionAssert.AreEqual(edges, this.graph.edges);
                this.graph.Disconnect(edges.Single(edge => edge.outputNode == b && edge.inputNode == d));
                canvas.GetType().GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, null);
                Assert.AreEqual(0, ((System.Collections.IDictionary)relaysField.GetValue(canvas)).Count);
            } finally { UnityEngine.Object.DestroyImmediate(editor); }
        }

        [Test]
        public void DetachedChainAlignsBeforeItsDestinationAndKeepsConnectedRanks() {
            // Connected path 0->1->2->3->4; detached 5->6->4.
            var ranks = new[] { 0, 1, 2, 3, 4, 0, 1 };
            var edges = new[] { (0, 1), (1, 2), (2, 3), (3, 4), (5, 6), (6, 4) };
            EditorType("FeaturesGraphStudioLayout").GetMethod("AlignDisconnectedToTargets", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { ranks, edges, new[] { 0 } });
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 2, 3 }, ranks);
        }

        [Test]
        public void CanvasPreservesStoredPositionsAndOmitsEmptyFieldSummaries() {
            this.System();
            var positions = this.graph.nodes.Select(node => node.position).ToArray();
            var windowType = EditorType("FeaturesGraphStudioWindow");
            var editor = (EditorWindow)ScriptableObject.CreateInstance(windowType);
            try {
                windowType.GetMethod("CreateGUI").Invoke(editor, null);
                windowType.GetMethod("OpenRoot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { this.graph });
                var canvas = editor.rootVisualElement.Q(className: "studio-canvas");
                Assert.IsNotNull(canvas);
                Assert.AreEqual(typeof(VisualElement), canvas.GetType().BaseType);
                Assert.AreEqual(this.graph.nodes.Count, canvas.Query(className: "studio-card").ToList().Count);
                Assert.IsEmpty(canvas.Query(className: "studio-field-summary").ToList());
                CollectionAssert.AreEqual(positions, this.graph.nodes.Select(node => node.position));
            } finally { UnityEngine.Object.DestroyImmediate(editor); }
        }
    }
}
