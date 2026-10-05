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
