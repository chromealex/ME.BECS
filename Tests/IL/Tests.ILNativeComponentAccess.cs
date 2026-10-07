using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private struct ILNativeGenericComponent<T> : IComponent where T : unmanaged { public T value; }
        private struct ILNativeKey : IEquatable<ILNativeKey> {
            public bool Equals(ILNativeKey other) { default(Ent).Read<Test2Component>(); return true; }
            public override int GetHashCode() { default(Ent).Get<Test1Component>(); return 0; }
        }
        private struct ILNativeLookalike<T> where T : unmanaged {
            public void Add(in T value) => default(Ent).Get<Test3Component>();
            public static unsafe T ReadArrayElement(void* ptr, int index) { default(Ent).Get<Test3Component>(); return default; }
        }

        // IL fixtures only. Do not execute these methods or native APIs.
        private static unsafe class ILNativeFixtures {
            private static T ReadGeneric<T>(void* ptr) => UnsafeUtility.ReadArrayElement<T>(ptr, 0);
            private static void WriteGeneric<T>(void* ptr, T value) => UnsafeUtility.WriteArrayElement(ptr, 0, value);
            private static TestComponent Argument() { default(Ent).Get<Test1Component>(); return default; }
            public static TestComponent Read() => UnsafeUtility.ReadArrayElement<TestComponent>(null, 0);
            public static TestComponent ReadStride() => UnsafeUtility.ReadArrayElementWithStride<TestComponent>(null, 0, 4);
            public static void Write() => UnsafeUtility.WriteArrayElement<TestComponent>(null, 0, default);
            public static void WriteStride() => UnsafeUtility.WriteArrayElementWithStride<TestComponent>(null, 0, 4, default);
            public static void CopyFromPointer() => UnsafeUtility.CopyPtrToStructure(null, out TestComponent value);
            public static void CopyToPointer() { var value = default(TestComponent); UnsafeUtility.CopyStructureToPtr(ref value, null); }
            public static void Address() { var value = default(TestComponent); _ = UnsafeUtility.AddressOf(ref value); }
            public static void Reference() => _ = UnsafeUtility.AsRef<TestComponent>(null);
            public static void ElementReference() => _ = UnsafeUtility.ArrayElementAsRef<TestComponent>(null, 0);
            public static void Reinterpret() { var value = default(TestComponent); _ = UnsafeUtility.As<TestComponent, Test1Component>(ref value); }
            public static void ClosedHelperRead() => _ = ReadGeneric<TestComponent>(null);
            public static void ClosedHelpers() { _ = ReadGeneric<TestComponent>(null); WriteGeneric<Test1Component>(null, default); }
            public static void GenericComponentRead() => _ = ReadGeneric<ILNativeGenericComponent<int>>(null);
            public static void Layout() { _ = UnsafeUtility.SizeOf<TestComponent>(); _ = UnsafeUtility.AlignOf<TestComponent>(); }
            public static void ScalarAndRaw() { _ = ReadGeneric<int>(null); WriteGeneric<int>(null, 0); UnsafeUtility.MemCpy(null, null, 4L); }
            public static TestComponent LocalValues() { var value = default(TestComponent); value.data = 2; var copy = value; return copy; }
            public static void ListRead() => _ = default(NativeList<TestComponent>)[0];
            public static void UnsafeListRead() => _ = default(UnsafeList<TestComponent>)[0];
            public static void ListWrite() { var list = default(NativeList<TestComponent>); list[0] = default; }
            public static void UnsafeListWrite() { var list = default(UnsafeList<TestComponent>); list[0] = default; }
            public static void ListReference() => _ = default(NativeList<TestComponent>).ElementAt(0);
            public static void ListAdd() => default(NativeList<TestComponent>).Add(default);
            public static void UnsafeListAdd() => default(UnsafeList<TestComponent>).Add(default);
            public static void ListAddNoResize() => default(NativeList<TestComponent>).AddNoResize(default);
            public static void ListRange() => default(NativeList<TestComponent>).AddRange(null, 1);
            public static void ListResize() => default(NativeList<TestComponent>).ResizeUninitialized(4);
            public static void ListCapacity() { var list = default(NativeList<TestComponent>); list.Capacity = 8; }
            public static void ListRemove() => default(NativeList<TestComponent>).RemoveAt(0);
            public static void ListDispose() => default(NativeList<TestComponent>).Dispose();
            public static void ListHeader() {
                var list = default(NativeList<TestComponent>);
                _ = list.IsCreated; _ = list.Length; _ = list.Capacity; list.Clear();
            }
            public static void ScalarList() => default(NativeList<int>).Add(0);
            public static void MemoryArgument() => UnsafeUtility.WriteArrayElement(null, 0, Argument());
            public static void ListArgument() => default(NativeList<TestComponent>).Add(Argument());
            public static void MapRead() => default(NativeHashMap<ILNativeKey, TestComponent>).TryGetValue(default, out _);
            public static void MapWrite() => default(NativeHashMap<ILNativeKey, TestComponent>).TryAdd(default, default);
            public static void MemoryLookalike() => ILNativeLookalike<TestComponent>.ReadArrayElement(null, 0);
            public static void ListLookalike() => default(ILNativeLookalike<TestComponent>).Add(default);
        }

        private static System.Collections.Generic.Dictionary<Type, RefOp> ILNativeAccesses(string method, bool traverseHierarchy = true) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var root = typeof(ILNativeFixtures).GetMethod(method);
            var result = (System.Collections.IEnumerable)analyzer.GetMethod("GetMethodTypesInfo")
                .Invoke(null, new object[] { root, traverseHierarchy, false, false, null });
            return result.Cast<object>().Select(item => (type: (Type)item.GetType().GetField("type").GetValue(item),
                    mode: (RefOp)item.GetType().GetField("op").GetValue(item)))
                .GroupBy(item => item.type).ToDictionary(group => group.Key,
                    group => group.Select(item => item.mode).Distinct().Count() == 1 ? group.First().mode : RefOp.ReadWrite);
        }

        [TestCase(nameof(ILNativeFixtures.Read), RefOp.ReadOnly)]
        [TestCase(nameof(ILNativeFixtures.ReadStride), RefOp.ReadOnly)]
        [TestCase(nameof(ILNativeFixtures.Write), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.WriteStride), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.CopyFromPointer), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.CopyToPointer), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.Address), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.Reference), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ElementReference), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListRead), RefOp.ReadOnly)]
        [TestCase(nameof(ILNativeFixtures.UnsafeListRead), RefOp.ReadOnly)]
        [TestCase(nameof(ILNativeFixtures.ListWrite), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.UnsafeListWrite), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListReference), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListAdd), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.UnsafeListAdd), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListAddNoResize), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListRange), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListResize), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListCapacity), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListRemove), RefOp.ReadWrite)]
        [TestCase(nameof(ILNativeFixtures.ListDispose), RefOp.ReadWrite)]
        public void ILNativeTypedAccessRetainsExactModeEvenWithoutBodyTraversal(string method, RefOp expected) {
            foreach (var traverse in new[] { false, true }) {
                var operations = ILNativeAccesses(method, traverse);
                CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, operations.Keys, method);
                Assert.AreEqual(expected, operations[typeof(TestComponent)], method);
            }
        }

        [TestCase(nameof(ILNativeFixtures.Layout))]
        [TestCase(nameof(ILNativeFixtures.ScalarAndRaw))]
        [TestCase(nameof(ILNativeFixtures.LocalValues))]
        [TestCase(nameof(ILNativeFixtures.ListHeader))]
        [TestCase(nameof(ILNativeFixtures.ScalarList))]
        public void ILNativeLayoutScalarsAndLocalCopiesDoNotInventComponentAccess(string method) =>
            CollectionAssert.IsEmpty(ILNativeAccesses(method));

        [TestCase(nameof(ILNativeFixtures.MemoryArgument))]
        [TestCase(nameof(ILNativeFixtures.ListArgument))]
        [TestCase(nameof(ILNativeFixtures.Reinterpret))]
        public void ILNativeContractsPreserveArgumentEffectsAndBothReinterpretedTypes(string method) {
            var operations = ILNativeAccesses(method);
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, operations.Keys);
            Assert.IsTrue(operations.Values.All(mode => mode == RefOp.ReadWrite));
        }

        [Test]
        public void ILNativeClosedGenericHelpersDoNotShareTypeSubstitutions() {
            var operations = ILNativeAccesses(nameof(ILNativeFixtures.ClosedHelpers));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, operations.Keys);
            Assert.AreEqual(RefOp.ReadOnly, operations[typeof(TestComponent)]);
            Assert.AreEqual(RefOp.ReadWrite, operations[typeof(Test1Component)]);
            operations = ILNativeAccesses(nameof(ILNativeFixtures.GenericComponentRead));
            CollectionAssert.AreEqual(new[] { typeof(ILNativeGenericComponent<int>) }, operations.Keys);
            Assert.AreEqual(RefOp.ReadOnly, operations[typeof(ILNativeGenericComponent<int>)]);
            CollectionAssert.IsEmpty(ILNativeAccesses(nameof(ILNativeFixtures.ClosedHelperRead), false));
        }

        [TestCase(nameof(ILNativeFixtures.MapRead), RefOp.ReadOnly)]
        [TestCase(nameof(ILNativeFixtures.MapWrite), RefOp.ReadWrite)]
        public void ILNativeMapTypedEffectsDoNotHideHashAndEqualityCallbacks(string method, RefOp valueAccess) {
            var operations = ILNativeAccesses(method);
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component) }, operations.Keys);
            Assert.AreEqual(valueAccess, operations[typeof(TestComponent)]);
            Assert.AreEqual(RefOp.ReadWrite, operations[typeof(Test1Component)]);
            Assert.AreEqual(RefOp.ReadOnly, operations[typeof(Test2Component)]);
        }

        [TestCase(nameof(ILNativeFixtures.MemoryLookalike))]
        [TestCase(nameof(ILNativeFixtures.ListLookalike))]
        public void ILNativeNamesAloneDoNotSuppressUserImplementations(string method) {
            var operations = ILNativeAccesses(method);
            CollectionAssert.AreEqual(new[] { typeof(Test3Component) }, operations.Keys);
            Assert.AreEqual(RefOp.ReadWrite, operations[typeof(Test3Component)]);
        }
    }
}
