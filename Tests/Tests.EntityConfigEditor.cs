using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ME.BECS.Tests {
    public class Tests_EntityConfigEditor {
        private readonly System.Collections.Generic.List<UnityEngine.Object> objects = new();
        private UnityEditor.Editor editor;

        [Test]
        public void NestedCollectionsSurviveValidateSaveAndReload() {
            var path = "Assets/__NestedConfigTest_" + Guid.NewGuid().ToString("N") + ".asset";
            var config = Tests_EntityConfig.NestedConfig(isStatic: true);
            try {
                config.Validate();
                CollectionAssert.AreEqual(new[] { 1u, 2u, 3u, 4u }, config.collectionsData.items.ConvertAll(item => item.id));
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssetIfDirty(config);
                UnityEngine.Resources.UnloadAsset(config);
                config = AssetDatabase.LoadAssetAtPath<EntityConfig>(path);
                config.Validate();
                CollectionAssert.AreEqual(new[] { 1u, 2u, 3u, 4u }, config.collectionsData.items.ConvertAll(item => item.id));
                Assert.AreEqual(2, config.collectionsData.items[0].array.Count);
                var row = (Tests_EntityConfig.NestedArrayItem)config.collectionsData.items[0].array[0];
                Assert.AreEqual(2u, row.units.GetConfigId());
                Assert.AreEqual(3u, row.modifiers.GetConfigId());
                Assert.AreEqual(201u, ((Config)config.collectionsData.items[2].array[0]).sourceId);
            } finally {
                if (AssetDatabase.LoadAssetAtPath<EntityConfig>(path) != null) AssetDatabase.DeleteAsset(path);
                else if (config != null) UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [TearDown]
        public void TearDown() {
            if (this.editor != null) UnityEngine.Object.DestroyImmediate(this.editor);
            foreach (var item in this.objects) {
                Undo.ClearUndo(item);
                UnityEngine.Object.DestroyImmediate(item);
            }
            this.objects.Clear();
        }

        private EntityConfig Config(params IConfigComponent[] components) {
            var config = ScriptableObject.CreateInstance<EntityConfig>();
            config.data.components = components;
            this.objects.Add(config);
            return config;
        }

        private object Field(string name) => this.editor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this.editor);
        private void Open(params EntityConfig[] configs) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.EntityConfigEditor", true);
            this.editor = UnityEditor.Editor.CreateEditor(configs, type);
            this.editor.CreateInspectorGUI();
        }

        [Test]
        public void CommonEditMatchesTypeAcrossDifferentOrdersAndPreservesOtherValues() {
            var a = this.Config(new Tests_EntityConfig.TestConfigMaskComponent { data1 = 1, data2 = 10 }, new Tests_EntityConfig.TestConfig1Component { data = 11 });
            var b = this.Config(new Tests_EntityConfig.TestConfig1Component { data = 22 }, new Tests_EntityConfig.TestConfigMaskComponent { data1 = 2, data2 = 20 });
            this.Open(a, b);
            var types = (Dictionary<string, Type[]>)this.Field("commonTypes");
            var index = Array.IndexOf(types["data"], typeof(Tests_EntityConfig.TestConfigMaskComponent));
            Assert.GreaterOrEqual(index, 0);
            var owner = (SerializedObject)this.Field("multiSerializedObject");
            var path = "data.components.Array.data[" + index + "]";
            var component = owner.FindProperty(path);
            Assert.IsTrue(component.FindPropertyRelative("data1").hasMultipleDifferentValues);
            component.FindPropertyRelative("data1").intValue = 99;
            owner.ApplyModifiedProperties();
            this.editor.GetType().GetMethod("CommitCommonComponent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this.editor, new object[] { owner, path });
            Assert.AreEqual(99, ((Tests_EntityConfig.TestConfigMaskComponent)a.data.components[0]).data1);
            Assert.AreEqual(99, ((Tests_EntityConfig.TestConfigMaskComponent)b.data.components[1]).data1);
            Assert.AreEqual(10, ((Tests_EntityConfig.TestConfigMaskComponent)a.data.components[0]).data2);
            Assert.AreEqual(20, ((Tests_EntityConfig.TestConfigMaskComponent)b.data.components[1]).data2);
            Assert.AreEqual(11, ((Tests_EntityConfig.TestConfig1Component)a.data.components[1]).data);
            Assert.AreEqual(22, ((Tests_EntityConfig.TestConfig1Component)b.data.components[0]).data);
        }

        [Test]
        public void MissingAndRepeatedTypesAreExcludedFromCommonComponents() {
            var a = this.Config(new Tests_EntityConfig.TestConfig1Component(), new Tests_EntityConfig.TestConfigMaskComponent());
            var b = this.Config(new Tests_EntityConfig.TestConfig1Component(), new Tests_EntityConfig.TestConfig1Component());
            this.Open(a, b);
            var types = (Dictionary<string, Type[]>)this.Field("commonTypes");
            Assert.IsEmpty(types["data"]);
            Assert.AreEqual(2, a.data.components.Length);
            Assert.AreEqual(2, b.data.components.Length);
        }
    }
}
