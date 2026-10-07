using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.BECS.Tests {
    public unsafe class Tests_EntityDrawer {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ScriptableObject holder;
        private SerializedObject source;
        private object drawer, view;
        private Type drawerType;

        [UnityEngine.TestTools.UnitySetUp]
        public IEnumerator SetUp() { AllTests.Start(); yield return null; }
        [UnityEngine.TestTools.UnityTearDown]
        public IEnumerator TearDown() {
            if (this.view != null) this.view.GetType().GetMethod("StopUpdates", Private).Invoke(this.view, null);
            this.source?.Dispose();
            if (this.holder != null) UnityEngine.Object.DestroyImmediate(this.holder);
            AllTests.Dispose(); yield return null;
        }
        private void Open(Ent entity) {
            var assembly = Assembly.Load("ME.BECS.Editor");
            this.drawerType = assembly.GetType("ME.BECS.Editor.EntityDrawer", true);
            this.holder = ScriptableObject.CreateInstance(assembly.GetType("ME.BECS.Editor.WorldEntityEditorWindow+TempObject", true));
            this.holder.GetType().GetField("entity").SetValue(this.holder, entity);
            this.source = new SerializedObject(this.holder);
            this.drawer = Activator.CreateInstance(this.drawerType);
            this.drawerType.GetMethod("CreatePropertyGUI").Invoke(this.drawer, new object[] { this.source.FindProperty("entity") });
            this.view = this.drawerType.GetField("view", Private).GetValue(this.drawer);
            var header = (Foldout)this.view.GetType().GetField("header", Private).GetValue(this.view);
            header.SetValueWithoutNotify(true);
            header.style.display = DisplayStyle.Flex;
            this.Refresh();
        }
        private void Refresh() => this.drawerType.GetMethod("OnUpdate").Invoke(this.drawer, null);
        private IDictionary Rows => (IDictionary)this.view.GetType().GetField("normalRows", Private).GetValue(this.view);

        [Test]
        public void DisposedInspectorSourceAndDestroyedTargetDoNotBreakScheduledRefresh() {
            using var world = World.Create();
            var entity = Ent.New(); this.Open(entity);
            this.source.Dispose(); this.source = null;
            Assert.DoesNotThrow(this.Refresh);
            UnityEngine.Object.DestroyImmediate(this.holder); this.holder = null;
            Assert.DoesNotThrow(this.Refresh);
            Assert.DoesNotThrow(this.Refresh);
            Assert.IsNull(this.view.GetType().GetField("source", Private).GetValue(this.view));
        }
        [Test]
        public void ComponentRowsSurviveValueChangesAndSameCountReplacement() {
            using var world = World.Create();
            var entity = Ent.New();
            entity.Set(new Tests_EntityConfig.TestConfig1Component { data = 1 });
            entity.Set(new Tests_EntityConfig.TestConfigMaskComponent { data1 = 10 });
            this.Open(entity);
            var retained = this.Rows[typeof(Tests_EntityConfig.TestConfigMaskComponent)];
            entity.Set(new Tests_EntityConfig.TestConfigMaskComponent { data1 = 20 });
            entity.Remove<Tests_EntityConfig.TestConfig1Component>();
            entity.Set(new Tests_EntityConfig.TestConfig2Component { data = 2 });
            this.Refresh();
            Assert.AreSame(retained, this.Rows[typeof(Tests_EntityConfig.TestConfigMaskComponent)]);
            Assert.IsFalse(this.Rows.Contains(typeof(Tests_EntityConfig.TestConfig1Component)));
            Assert.IsTrue(this.Rows.Contains(typeof(Tests_EntityConfig.TestConfig2Component)));
            entity.Destroy(); this.Refresh(); Assert.IsEmpty(this.Rows);
        }

        [Test]
        public void EditMergesOneFieldIntoLatestRuntimeValueAndDoesNotRecreateRemovedComponent() {
            using var world = World.Create();
            world.state.ptr->Mode = WorldMode.Visual;
            var entity = Ent.New();
            entity.Set(new Tests_EntityConfig.TestConfigMaskComponent { data1 = 1, data2 = 10 });
            this.Open(entity);
            var row = this.Rows[typeof(Tests_EntityConfig.TestConfigMaskComponent)];
            var type = row.GetType();
            ((Foldout)type.GetField("element").GetValue(row)).value = true;
            type.GetMethod("Refresh").Invoke(row, null);
            var owner = (SerializedObject)type.GetField("serialized", Private).GetValue(row);
            Assert.IsNotNull(owner);
            entity.Set(new Tests_EntityConfig.TestConfigMaskComponent { data1 = 2, data2 = 99 });
            var field = owner.FindProperty("data.Array.data[0].data1");
            field.intValue = 42; owner.ApplyModifiedPropertiesWithoutUndo();
            type.GetMethod("Commit", Private).Invoke(row, new object[] { field });
            var result = entity.Read<Tests_EntityConfig.TestConfigMaskComponent>();
            Assert.AreEqual(42, result.data1); Assert.AreEqual(99, result.data2);
            entity.Remove<Tests_EntityConfig.TestConfigMaskComponent>();
            field.intValue = 43; owner.ApplyModifiedPropertiesWithoutUndo();
            type.GetMethod("Commit", Private).Invoke(row, new object[] { field });
            Assert.IsFalse(entity.Has<Tests_EntityConfig.TestConfigMaskComponent>());
        }
        [Test]
        public void LogicWorldAllowsMutationOnlyDuringReplay() {
            using var world = World.Create();
            world.state.ptr->Mode = WorldMode.Logic;
            var entity = Ent.New();
            entity.Set(new Tests_EntityConfig.TestConfigMaskComponent { data1 = 1 });
            this.Open(entity);
            var resolver = this.drawerType.GetProperty("ReplayModeResolver");
            var previous = resolver.GetValue(null);
            var replay = false;
            try {
                resolver.SetValue(null, new Func<World, bool>(candidate => candidate.Equals(world) && replay));
                var canEdit = this.drawerType.GetMethod("CanEditComponents");
                Assert.IsFalse((bool)canEdit.Invoke(null, new object[] { entity }));
                var row = this.Rows[typeof(Tests_EntityConfig.TestConfigMaskComponent)];
                var type = row.GetType();
                ((Foldout)type.GetField("element").GetValue(row)).value = true;
                type.GetMethod("Refresh").Invoke(row, null);
                var owner = (SerializedObject)type.GetField("serialized", Private).GetValue(row);
                var field = owner.FindProperty("data.Array.data[0].data1");
                field.intValue = 42; owner.ApplyModifiedPropertiesWithoutUndo();
                type.GetMethod("Commit", Private).Invoke(row, new object[] { field });
                Assert.AreEqual(1, entity.Read<Tests_EntityConfig.TestConfigMaskComponent>().data1);
                replay = true;
                Assert.IsTrue((bool)canEdit.Invoke(null, new object[] { entity }));
                type.GetMethod("Commit", Private).Invoke(row, new object[] { field });
                Assert.AreEqual(42, entity.Read<Tests_EntityConfig.TestConfigMaskComponent>().data1);
                replay = false;
                Assert.IsFalse((bool)canEdit.Invoke(null, new object[] { entity }));
                world.state.ptr->Mode = WorldMode.Visual;
                Assert.IsTrue((bool)canEdit.Invoke(null, new object[] { entity }));
            } finally { resolver.SetValue(null, previous); }
        }
    }
}
