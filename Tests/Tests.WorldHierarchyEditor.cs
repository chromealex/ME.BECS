using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ME.BECS.Transforms;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.BECS.Tests {
    public unsafe class Tests_WorldHierarchyEditor {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private EditorWindow window;
        private UnityEngine.Object[] previousSelection;
        [UnityEngine.TestTools.UnitySetUp]
        public IEnumerator SetUp() { this.previousSelection = Selection.objects; AllTests.Start(); yield return null; }
        [UnityEngine.TestTools.UnityTearDown]
        public IEnumerator TearDown() {
            if (this.window != null) UnityEngine.Object.DestroyImmediate(this.window);
            Selection.objects = this.previousSelection;
            AllTests.Dispose(); yield return null;
        }
        private object Field(string name) => this.window.GetType().GetField(name, Private).GetValue(this.window);
        private void Call(string name, params object[] args) => this.window.GetType().GetMethod(name, Private).Invoke(this.window, args);
        private void Open(World world) {
            var type = Assembly.Load("ME.BECS.Features.Editor").GetType("ME.BECS.Editor.WorldHierarchyEditorWindow", true);
            this.window = (EditorWindow)ScriptableObject.CreateInstance(type);
            this.Call("SelectWorld", world);
            this.Call("DrawEntities");
        }
        [Test]
        public void EmptyWindowStillContainsWorldTabButton() {
            this.Open(default);
            var button = this.window.rootVisualElement.Q<Button>(className: "add-world-tab");
            Assert.IsNotNull(button);
            // The plus is drawn geometrically (two line elements), not as a text glyph.
            Assert.IsTrue(button.ClassListContains("becs-add-button"));
            Assert.AreEqual(2, button.Query(className: "becs-add-button-line").ToList().Count);
            Assert.IsNotNull(button.Q(className: "becs-add-button-line-vertical"));
            Assert.IsTrue(button.enabledSelf);
            Assert.IsTrue(this.window.rootVisualElement.ClassListContains("world-dashboard"));
            Assert.IsNotNull(this.window.rootVisualElement.Q(className: "dashboard-tabs"));
            Assert.IsNotNull(this.window.rootVisualElement.Q<Label>(className: "empty-label"));
        }
        [Test]
        public void SnapshotTracksReparentingWithoutDuplicatingEntities() {
            using var world = World.Create();
            var a = Ent.New(); var b = Ent.New(); var child = Ent.New();
            child.Set(new ParentComponent { value = a });
            this.Open(world);
            var parents = (Dictionary<Ent, Ent>)this.Field("parents");
            Assert.AreEqual(a, parents[child]);
            this.Call("DrawEntities"); this.Call("DrawEntities");
            Assert.AreEqual(3, ((System.Collections.Generic.HashSet<Ent>)this.Field("current")).Count);
            var tree = (TreeView)this.Field("treeView");
            tree.ExpandItem((int)a.id);
            this.Call("DrawEntities");
            Assert.IsTrue(tree.IsExpanded((int)a.id));
            Ent.New();
            this.Call("DrawEntities");
            Assert.IsTrue(tree.IsExpanded((int)a.id));
            child.Set(new ParentComponent { value = b });
            this.Call("DrawEntities");
            Assert.AreEqual(b, parents[child]);
            child.Set(new ParentComponent { value = child });
            this.Call("DrawEntities"); Assert.IsTrue(parents[child].IsEmpty());
        }
        [Test]
        public void DeleteDoesNotMutateLiveLogicWorld() {
            using var world = World.Create(); world.state.ptr->Mode = WorldMode.Logic;
            var ent = Ent.New(); this.Open(world);
            var selected = (System.Collections.Generic.HashSet<Ent>)this.Field("selected");
            selected.Add(ent);
            this.Call("DeleteSelected"); Assert.IsTrue(ent.IsAlive());
        }
    }
}
