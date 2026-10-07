using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static int ilSafetyInitializerExecutions;
        private sealed class ILSafetyStatic<T> where T : unmanaged, IComponent {
            public static int value;
            static ILSafetyStatic() {
                ++ilSafetyInitializerExecutions;
                default(Ent).Get<T>();
            }
            public static void Touch() { }
        }
        private sealed class ILSafetyConstructed<T> where T : unmanaged, IComponent {
            static ILSafetyConstructed() { ++ilSafetyInitializerExecutions; default(Ent).Read<Test1Component>(); }
            public ILSafetyConstructed() { ++ilSafetyInitializerExecutions; default(Ent).Get<T>(); }
            public ILSafetyConstructed(int unused) => default(Ent).Get<Test3Component>();
        }
        private struct ILSafetyDefaultValue { }
        private static T ILSafetyNew<T>() where T : new() => new T();
        private static class ILSafetyInitializers {
            public static void StaticCall() => ILSafetyStatic<TestComponent>.Touch();
            public static void IgnoredSafetyContract() => default(Ent).Enable<TestComponent>();
            public static int FieldRead() => ILSafetyStatic<TestComponent>.value;
            public static ref int FieldAddress() => ref ILSafetyStatic<TestComponent>.value;
            public static void FieldWrite() => ILSafetyStatic<TestComponent>.value = 1;
            public static void ClosedTypes() { ILSafetyStatic<TestComponent>.Touch(); ILSafetyStatic<Test2Component>.Touch(); }
            public static void Constructor() => _ = new ILSafetyConstructed<TestComponent>();
            public static void GenericConstructor() => _ = ILSafetyNew<ILSafetyConstructed<TestComponent>>();
            public static void GenericDefaultValue() => _ = ILSafetyNew<ILSafetyDefaultValue>();
            public static Type TypeIdentity() => typeof(ILSafetyStatic<TestComponent>);
            public static void SharedStaticContext() => _ = Unity.Burst.SharedStatic<int>.GetOrCreate<ILSafetyStatic<TestComponent>>();
            public static TestComponent SharedComponentReference() => Unity.Burst.SharedStatic<TestComponent>.GetOrCreate<ILSafetyDefaultValue>().Data;
            public static unsafe void SharedComponentPointer() => _ = Unity.Burst.SharedStatic<TestComponent>.GetOrCreate<ILSafetyDefaultValue>().UnsafeDataPointer;
            public static void ScheduleReadonly() => default(QueryBuilder).AsReadonly().Schedule<ILSafetyInitializedJob, TestComponent>();
        }
        private partial struct ILSafetyInitializedJob : IJobForComponents<TestComponent> {
            static ILSafetyInitializedJob() { ++ilSafetyInitializerExecutions; default(Ent).Get<TestComponent>(); }
            public void Execute(in JobInfo info, in Ent ent, [RO] ref TestComponent component) { }
        }

        private static System.Collections.Generic.Dictionary<Type, RefOp> ILSafetyInitializationAccesses(string method, bool traverseHierarchy = true) {
            var root = typeof(ILSafetyInitializers).GetMethod(method);
            var generator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = (System.Collections.IEnumerable)generator.GetMethod("GetMethodTypesInfo")
                .Invoke(null, new object[] { root, traverseHierarchy, false, false, null });
            var values = result.Cast<object>().Select(item => (type: (Type)item.GetType().GetField("type").GetValue(item),
                mode: (RefOp)item.GetType().GetField("op").GetValue(item)));
            return values.GroupBy(item => item.type).ToDictionary(group => group.Key,
                group => group.Select(item => item.mode).Distinct().Count() == 1 ? group.First().mode : RefOp.ReadWrite);
        }

        [TestCase(nameof(ILSafetyInitializers.StaticCall))]
        [TestCase(nameof(ILSafetyInitializers.FieldRead))]
        [TestCase(nameof(ILSafetyInitializers.FieldAddress))]
        [TestCase(nameof(ILSafetyInitializers.FieldWrite))]
        [TestCase(nameof(ILSafetyInitializers.IgnoredSafetyContract))]
        [TestCase(nameof(ILSafetyInitializers.SharedComponentReference))]
        public void ILSafetyRetainsStaticInitializationEffectsWithoutExecutingThem(string method) {
            var before = ilSafetyInitializerExecutions;
            var operations = ILSafetyInitializationAccesses(method);
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, operations.Keys);
            Assert.AreEqual(RefOp.ReadWrite, operations[typeof(TestComponent)]);
            Assert.AreEqual(before, ilSafetyInitializerExecutions);
        }

        [TestCase(nameof(ILSafetyInitializers.Constructor))]
        [TestCase(nameof(ILSafetyInitializers.GenericConstructor))]
        public void ILSafetyBindsOnlyTheConstructedClosedTypeAndConstructor(string method) {
            var before = ilSafetyInitializerExecutions;
            var operations = ILSafetyInitializationAccesses(method);
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, operations.Keys);
            Assert.AreEqual(RefOp.ReadWrite, operations[typeof(TestComponent)]);
            Assert.AreEqual(RefOp.ReadOnly, operations[typeof(Test1Component)]);
            Assert.AreEqual(before, ilSafetyInitializerExecutions);
        }

        [Test]
        public void ILSafetyKeepsClosedInitializerInstancesDistinct() {
            var operations = ILSafetyInitializationAccesses(nameof(ILSafetyInitializers.ClosedTypes));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test2Component) }, operations.Keys);
            Assert.IsTrue(operations.Values.All(mode => mode == RefOp.ReadWrite));
        }

        [TestCase(nameof(ILSafetyInitializers.TypeIdentity))]
        [TestCase(nameof(ILSafetyInitializers.SharedStaticContext))]
        [TestCase(nameof(ILSafetyInitializers.GenericDefaultValue))]
        [TestCase(nameof(ILSafetyInitializers.SharedComponentPointer))]
        public void ILSafetyDoesNotInventInitializersFromTypeArguments(string method) =>
            CollectionAssert.IsEmpty(ILSafetyInitializationAccesses(method));

        [Test]
        public void ILSafetyShallowInspectionDoesNotTraverseInitializers() =>
            CollectionAssert.IsEmpty(ILSafetyInitializationAccesses(nameof(ILSafetyInitializers.StaticCall), false));

        [Test]
        public void ILSafetyShallowInspectionKeepsSharedComponentReference() =>
            Assert.AreEqual(RefOp.ReadWrite, ILSafetyInitializationAccesses(nameof(ILSafetyInitializers.SharedComponentReference), false)[typeof(TestComponent)]);

        [Test]
        public void ILSystemReadonlyScheduleRetainsIndependentJobInitializerWrites() {
            var operations = ILSystemPresenceAccesses(typeof(ILSafetyInitializers).GetMethod(nameof(ILSafetyInitializers.ScheduleReadonly)));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, operations.Keys);
            Assert.AreEqual(RefOp.ReadWrite, operations[typeof(TestComponent)]);
        }
    }
}
