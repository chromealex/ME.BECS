using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ME.BECS.Tests {

    public class Tests_ObjectReferenceValidation {
        [Serializable]
        public struct Metadata : IObjectItemData {
            public string sourceName;
            public bool IsValid(UnityEngine.Object obj) => obj != null && obj.name.StartsWith("ObjectReferenceValidation-", StringComparison.Ordinal);
            public void Validate(UnityEngine.Object obj) { this.sourceName = obj.name; }
        }

        private ObjectReferenceRegistryData previous, registry;
        private Texture2D source;
        private ObjectReferenceRegistryItem first, second;
        private MethodInfo validate;

        [SetUp]
        public void SetUp() {
            this.validate = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ObjectReferenceValidate", true)
                .GetMethod("Validate", new[] { typeof(int), typeof(int) });
            this.previous = ObjectReferenceRegistry.data;
            this.registry = ScriptableObject.CreateInstance<ObjectReferenceRegistryData>();
            this.source = new Texture2D(1, 1) { name = "ObjectReferenceValidation-source" };
            this.first = ScriptableObject.CreateInstance<ObjectReferenceRegistryItem>();
            this.second = ScriptableObject.CreateInstance<ObjectReferenceRegistryItem>();
            this.first.data = new ItemInfo { source = this.source, sourceId = 10u, customData = new Metadata() };
            this.second.data = new ItemInfo { source = this.source, sourceId = 20u, customData = new Metadata() };
            this.registry.objects = new[] { this.first, this.second };
            ObjectReferenceRegistry.data = this.registry;
            this.Run(0, 2);
            this.ClearDirty();
        }

        [TearDown]
        public void TearDown() {
            ObjectReferenceRegistry.data = this.previous;
            UnityEngine.Object.DestroyImmediate(this.first);
            UnityEngine.Object.DestroyImmediate(this.second);
            UnityEngine.Object.DestroyImmediate(this.source);
            UnityEngine.Object.DestroyImmediate(this.registry);
        }

        private void Run(int offset, int count) => this.validate.Invoke(null, new object[] { offset, count });

        private void ClearDirty() {
            EditorUtility.ClearDirty(this.first);
            EditorUtility.ClearDirty(this.second);
            EditorUtility.ClearDirty(this.registry);
        }

        [Test]
        public void RepeatedValidationPreservesReferencesAndLeavesRegistryClean() {
            // Reflection avoids adding an Addressables dependency to the test asmdef.
            var reference = typeof(ItemInfo).GetField("sourceReference");
            var before = reference.GetValue(this.first.data);
            var members = this.registry.objects;
            this.Run(0, 2);
            Assert.AreSame(before, reference.GetValue(this.first.data));
            Assert.AreSame(members, this.registry.objects);
            Assert.IsFalse(EditorUtility.IsDirty(this.first));
            Assert.IsFalse(EditorUtility.IsDirty(this.second));
            Assert.IsFalse(EditorUtility.IsDirty(this.registry));
            Assert.AreEqual(10u, this.first.data.sourceId);
            Assert.AreEqual(20u, this.second.data.sourceId);
        }

        [Test]
        public void ChangedEntryDoesNotDirtyFollowingEntries() {
            this.first.data.sourceType = null;
            this.Run(0, 2);
            Assert.IsTrue(EditorUtility.IsDirty(this.first));
            Assert.IsFalse(EditorUtility.IsDirty(this.second));
            Assert.IsFalse(EditorUtility.IsDirty(this.registry));
        }

        [Test]
        public void InPlaceMetadataChangesArePersisted() {
            this.source.name += "-changed";
            this.Run(0, 1);
            Assert.IsTrue(EditorUtility.IsDirty(this.first));
            Assert.IsFalse(EditorUtility.IsDirty(this.second));
            Assert.AreEqual(this.source.name, ((Metadata)this.first.data.customData).sourceName);
            this.ClearDirty();
            this.Run(0, 1);
            Assert.IsFalse(EditorUtility.IsDirty(this.first));
        }

        [Test]
        public void GameObjectFlagChangesArePersisted() {
            this.first.data.isGameObject = true;
            this.Run(0, 1);
            Assert.IsFalse(this.first.data.isGameObject);
            Assert.IsTrue(EditorUtility.IsDirty(this.first));
            Assert.IsFalse(EditorUtility.IsDirty(this.second));
        }

        [Test]
        public void RangeIsClampedWithoutOverflowAndNullEntriesAreSkipped() {
            this.registry.objects = new[] { null, this.first, this.second };
            this.first.data.sourceType = null;
            this.Run(0, int.MaxValue);
            Assert.IsTrue(EditorUtility.IsDirty(this.first));
            Assert.IsFalse(EditorUtility.IsDirty(this.second));
            Assert.DoesNotThrow(() => this.Run(int.MaxValue, int.MaxValue));
        }
    }
}
