using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // All fixtures are private, metadata-only, and never instantiated/run.
        private class ILConstructorBase {
            protected ILConstructorBase(Ent ent) => ent.Read<TestComponent>();
        }
        private sealed class ILConstructorChain : ILConstructorBase {
            private readonly bool field = InitializeField();
            private static bool InitializeField() { default(Ent).Read<Test1Component>(); return true; }
            public ILConstructorChain(Ent ent) : this(ent, true) { }
            private ILConstructorChain(Ent ent, bool unused) : base(ent) => ent.Get<Test2Component>();
        }
        private sealed class ILGenericConstructor<T> where T : unmanaged, IComponent {
            public ILGenericConstructor() => default(Ent).Read<T>();
        }
        private sealed class ILTypeInitializer {
            public static int value;
            static ILTypeInitializer() => default(Ent).Get<Test3Component>();
            public static void Touch() { }
        }
        private sealed class ILTypeInitializerQuery {
            static ILTypeInitializerQuery() => default(QueryBuilder).AsReadonly().Schedule<QueryModeJob, TestComponent>();
            public static void Touch() { }
        }
        private sealed class ILTypeInitializerSharedQuery {
            static ILTypeInitializerSharedQuery() => Schedule(default);
            public static void Schedule(QueryBuilder query) => query.Schedule<QueryModeJob, TestComponent>();
        }
        private struct ILQueryConstructor {
            public ILQueryConstructor(QueryBuilder query) => query.Schedule<QueryModeJob, TestComponent>();
        }
        private struct ILQueryConstructorReset {
            public ILQueryConstructorReset(ref QueryBuilder query) { query = default; query.Schedule<QueryModeJob, TestComponent>(); }
        }
        private struct ILQueryConstructorReadonly {
            public ILQueryConstructorReadonly(ref QueryBuilder query) => query.AsReadonly();
        }
        private sealed class ILQueryConstructorThrows {
            public ILQueryConstructorThrows(ref QueryBuilder query) { query = default; throw new InvalidOperationException(); }
        }
        private interface ILCallReceiver { void Use(Ent ent, ref QueryBuilder query); }
        private struct ILReadonlyReceiver : ILCallReceiver {
            void ILCallReceiver.Use(Ent ent, ref QueryBuilder query) {
                ent.Read<Test2Component>();
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        private struct ILResetReceiver : ILCallReceiver {
            void ILCallReceiver.Use(Ent ent, ref QueryBuilder query) {
                ent.Get<Test2Component>();
                query = default;
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        private interface ILGenericCallReceiver { void Use<T>(Ent ent) where T : unmanaged, IComponent; }
        private struct ILGenericCallValue : ILGenericCallReceiver {
            void ILGenericCallReceiver.Use<T>(Ent ent) => ent.Read<T>();
        }
        private sealed class ILSealedReadonlyReceiver : ILCallReceiver {
            void ILCallReceiver.Use(Ent ent, ref QueryBuilder query) {
                ent.Read<Test2Component>();
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        private class ILOpenReceiver : ILCallReceiver {
            public virtual void Use(Ent ent, ref QueryBuilder query) {
                ent.Get<Test2Component>();
                query = default;
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        private sealed class ILSealedInheritedReceiver : ILOpenReceiver { }
        private sealed class ILSealedOverrideReceiver : ILOpenReceiver {
            public override void Use(Ent ent, ref QueryBuilder query) {
                ent.Read<Test2Component>();
                query.Schedule<QueryModeJob, TestComponent>();
            }
        }
        private class ILGenericVirtualBase : ILGenericCallReceiver {
            public virtual void Use<T>(Ent ent) where T : unmanaged, IComponent => ent.Get<T>();
        }
        private class ILGenericVirtualMiddle : ILGenericVirtualBase {
            public override void Use<T>(Ent ent) => ent.Read<T>();
        }
        private sealed class ILGenericVirtualSealed : ILGenericVirtualMiddle {
            // This member hides the name, but does not replace the inherited
            // interface mapping or the base class's virtual slot.
            public new void Use<T>(Ent ent) where T : unmanaged, IComponent => ent.Get<Test3Component>();
        }
        private class ILClosedVirtualBase<TUnused> {
            public virtual void Use<T>(Ent ent) where T : unmanaged, IComponent => ent.Get<T>();
        }
        private sealed class ILClosedVirtualReceiver<TUnused> : ILClosedVirtualBase<TUnused> {
            public override void Use<T>(Ent ent) => ent.Read<T>();
        }
        private static class ILReceiverHost<TUnused, TReceiver> where TReceiver : struct, ILCallReceiver {
            public static void Use(ref QueryBuilder query) => default(TReceiver).Use(default, ref query);
        }
        private static class ILBodyCalls {
            public static void ConstructorChain() => _ = new ILConstructorChain(default);
            private static T Construct<T>() where T : new() => new T();
            public static void GenericConstruction() => _ = Construct<ILGenericConstructor<TestComponent>>();
            public static void TypeInitializerField() => _ = ILTypeInitializer.value;
            public static void TypeInitializerMethod() => ILTypeInitializer.Touch();
            public static void TypeInitializerQuery() => ILTypeInitializerQuery.Touch();
            public static void TypeInitializerSharesBoundHelper() => ILTypeInitializerSharedQuery.Schedule(default(QueryBuilder).AsReadonly());
            public static void GenericConstructionUnion() {
                _ = Construct<ILGenericConstructor<TestComponent>>();
                _ = Construct<ILGenericConstructor<Test1Component>>();
            }
            public static void ReadonlyConstructor() => _ = new ILQueryConstructor(default(QueryBuilder).AsReadonly());
            public static void WritableConstructor() => _ = new ILQueryConstructor(default);
            public static void ConstructorContexts() { ReadonlyConstructor(); WritableConstructor(); }
            public static void ConstructorCopiesDoNotMutateCaller() {
                var query = default(QueryBuilder).AsReadonly();
                _ = new ILQueryConstructor(query);
                query.Schedule<QueryModeJob, TestComponent>();
            }
            public static void ConstructorRefReset() {
                var query = default(QueryBuilder).AsReadonly();
                _ = new ILQueryConstructorReset(ref query);
                query.Schedule<QueryModeJob, TestComponent>();
            }
            public static void ConstructorRefReadonly() {
                var query = default(QueryBuilder);
                _ = new ILQueryConstructorReadonly(ref query);
                query.Schedule<QueryModeJob, TestComponent>();
            }
            public static void ConstructorException() {
                var query = default(QueryBuilder).AsReadonly();
                try { _ = new ILQueryConstructorThrows(ref query); }
                catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
            }
            private static void Invoke<TUnused, TReceiver>(ref QueryBuilder query) where TReceiver : struct, ILCallReceiver =>
                default(TReceiver).Use(default, ref query);
            public static void ConstrainedMethodReadonly() { var query = default(QueryBuilder).AsReadonly(); Invoke<int, ILReadonlyReceiver>(ref query); }
            public static void ConstrainedMethodReset() { var query = default(QueryBuilder).AsReadonly(); Invoke<int, ILResetReceiver>(ref query); }
            public static void ConstrainedOwnerReadonly() { var query = default(QueryBuilder).AsReadonly(); ILReceiverHost<int, ILReadonlyReceiver>.Use(ref query); }
            public static void ConstrainedOwnerReset() { var query = default(QueryBuilder).AsReadonly(); ILReceiverHost<int, ILResetReceiver>.Use(ref query); }
            public static void ConstrainedBoth() { ConstrainedOwnerReadonly(); ConstrainedOwnerReset(); }
            private static void InvokeGeneric<TReceiver, TComponent>() where TReceiver : struct, ILGenericCallReceiver where TComponent : unmanaged, IComponent =>
                default(TReceiver).Use<TComponent>(default);
            public static void ConstrainedGenericSlot() => InvokeGeneric<ILGenericCallValue, Test1Component>();
            private static void InvokeReference<TReceiver>(TReceiver receiver, ref QueryBuilder query) where TReceiver : ILCallReceiver =>
                receiver.Use(default, ref query);
            public static void ConstrainedSealedReadonly(ILSealedReadonlyReceiver receiver) {
                var query = default(QueryBuilder).AsReadonly(); InvokeReference(receiver, ref query);
            }
            public static void ConstrainedSealedInherited(ILSealedInheritedReceiver receiver) {
                var query = default(QueryBuilder).AsReadonly(); InvokeReference(receiver, ref query);
            }
            public static void ConstrainedSealedOverride(ILSealedOverrideReceiver receiver) {
                var query = default(QueryBuilder).AsReadonly(); InvokeReference(receiver, ref query);
            }
            public static void ConstrainedOpenReference(ILOpenReceiver receiver) {
                var query = default(QueryBuilder).AsReadonly(); InvokeReference(receiver, ref query);
                query.Schedule<QueryModeJob, TestComponent>();
            }
            private static void InvokeGenericReference<TReceiver, TComponent>(TReceiver receiver) where TReceiver : ILGenericCallReceiver where TComponent : unmanaged, IComponent =>
                receiver.Use<TComponent>(default);
            public static void ConstrainedSealedGenericSlot(ILGenericVirtualSealed receiver) => InvokeGenericReference<ILGenericVirtualSealed, Test1Component>(receiver);
        }
        private partial struct ILConstrainedSafetyJob : Unity.Jobs.IJob {
            public void Execute() => ILBodyCalls.ConstrainedGenericSlot();
        }
        private partial struct ILSealedConstrainedSafetyJob : Unity.Jobs.IJob {
            public void Execute() => ILBodyCalls.ConstrainedSealedGenericSlot(null);
        }

        private static MethodInfo ILBodyMethod(string name) => typeof(ILBodyCalls).GetMethod(name, BindingFlags.Public | BindingFlags.Static);

        [Test]
        public void ILSystemDependenciesIncludeConstructorChainsAndFieldInitializers() {
            var actual = ILSystemPresenceAccesses(ILBodyMethod(nameof(ILBodyCalls.ConstructorChain)));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component) }, actual.Keys);
            Assert.AreEqual(RefOp.ReadOnly, actual[typeof(TestComponent)]);
            Assert.AreEqual(RefOp.ReadOnly, actual[typeof(Test1Component)]);
            Assert.AreEqual(RefOp.ReadWrite, actual[typeof(Test2Component)]);
        }

        [TestCase(nameof(ILBodyCalls.GenericConstruction), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.TypeInitializerField), typeof(Test3Component), RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.TypeInitializerMethod), typeof(Test3Component), RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstrainedGenericSlot), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedGenericSlot), typeof(Test1Component), RefOp.ReadOnly)]
        public void ILSystemDependenciesIncludeClosedConstructionAndDispatch(string method, Type component, RefOp mode) {
            var actual = ILSystemPresenceAccesses(ILBodyMethod(method));
            CollectionAssert.AreEqual(new[] { component }, actual.Keys);
            Assert.AreEqual(mode, actual[component]);
        }

        [TestCase(nameof(ILBodyCalls.ReadonlyConstructor), RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.WritableConstructor), RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstructorContexts), RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstructorCopiesDoNotMutateCaller), RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.ConstructorRefReset), RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstructorRefReadonly), RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.ConstructorException), RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.TypeInitializerQuery), RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.TypeInitializerSharesBoundHelper), RefOp.ReadWrite)]
        public void ILSystemConstructorQueriesRetainCallerModesAndEffects(string method, RefOp expected) {
            var actual = ILSystemPresenceAccesses(ILBodyMethod(method));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, actual.Keys);
            Assert.AreEqual(expected, actual[typeof(TestComponent)]);
        }

        [TestCase(nameof(ILBodyCalls.ConstrainedMethodReadonly), RefOp.ReadOnly, RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.ConstrainedMethodReset), RefOp.ReadWrite, RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstrainedOwnerReadonly), RefOp.ReadOnly, RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.ConstrainedOwnerReset), RefOp.ReadWrite, RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstrainedBoth), RefOp.ReadWrite, RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedReadonly), RefOp.ReadOnly, RefOp.ReadOnly)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedInherited), RefOp.ReadWrite, RefOp.ReadWrite)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedOverride), RefOp.ReadOnly, RefOp.ReadOnly)]
        public void ILSystemConstrainedQueriesUseTheActualReceiver(string method, RefOp argument, RefOp body) {
            var actual = ILSystemPresenceAccesses(ILBodyMethod(method));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test2Component) }, actual.Keys);
            Assert.AreEqual(argument, actual[typeof(TestComponent)]);
            Assert.AreEqual(body, actual[typeof(Test2Component)]);
        }

        [Test]
        public void ILSystemConstructorsKeepDifferentClosedInstances() {
            var actual = ILSystemPresenceAccesses(ILBodyMethod(nameof(ILBodyCalls.GenericConstructionUnion)));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, actual.Keys);
            Assert.IsTrue(actual.Values.All(mode => mode == RefOp.ReadOnly));
        }

        [TestCase(typeof(ILConstrainedSafetyJob))]
        [TestCase(typeof(ILSealedConstrainedSafetyJob))]
        public void ILJobSafetyAlsoBindsTheConstrainedReceiver(Type job) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = (System.Collections.IEnumerable)analyzer.GetMethod("GetJobTypesInfo")
                .Invoke(null, new object[] { job, null });
            var rows = result.Cast<object>().ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(typeof(Test1Component), rows[0].GetType().GetField("type").GetValue(rows[0]));
            Assert.AreEqual(RefOp.ReadOnly, rows[0].GetType().GetField("op").GetValue(rows[0]));
        }

        [TestCase(typeof(ILGenericVirtualBase), typeof(ILGenericVirtualSealed), typeof(ILGenericVirtualMiddle))]
        [TestCase(typeof(ILClosedVirtualBase<int>), typeof(ILClosedVirtualReceiver<int>), typeof(ILClosedVirtualReceiver<int>))]
        public void ILConstrainedVirtualGenericBindingKeepsSlotAndTypeArguments(Type owner, Type receiver, Type targetOwner) {
            var binder = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILCallTargets", true);
            var slot = owner.GetMethod("Use").MakeGenericMethod(typeof(Test1Component));
            var target = (MethodInfo)binder.GetMethod("ResolveConstrained", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { slot, receiver });
            Assert.AreEqual(targetOwner, target.DeclaringType);
            CollectionAssert.AreEqual(new[] { typeof(Test1Component) }, target.GetGenericArguments());
            Assert.IsFalse(target.ContainsGenericParameters);
        }

        [TestCase(typeof(ILSealedInheritedReceiver), true)]
        [TestCase(typeof(ILReadonlyReceiver), true)]
        [TestCase(typeof(ILOpenReceiver), false)]
        [TestCase(typeof(ILCallReceiver), false)]
        [TestCase(typeof(object[]), false)]
        [TestCase(typeof(ILReceiverHost<,>), false)]
        public void ILConstrainedReceiverMustProveTheRuntimeType(Type receiver, bool exact) {
            var binder = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILCallTargets", true);
            Assert.AreEqual(exact, binder.GetMethod("IsExactReceiver", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { receiver }));
        }

        private static Func<string> ILUnknownVirtualAddress(object value) => value.ToString;

        [Test]
        public void ILVirtualMethodAddressDoesNotProveItsRuntimeTarget() {
            var root = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(ILUnknownVirtualAddress), BindingFlags.NonPublic | BindingFlags.Static);
            var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(root).ToArray();
            var index = Array.FindIndex(instructions, instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Ldvirtftn);
            Assert.GreaterOrEqual(index, 0, "The fixture must exercise virtual method-address binding.");
            var binder = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILCallTargets", true);
            var resolve = binder.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == "Resolve" && method.GetParameters().Length == 3);
            var args = new object[] { instructions, index, null };
            Assert.AreEqual(typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes), resolve.Invoke(null, args));
            Assert.AreEqual(false, args[2]);
        }

        [TestCase(nameof(ILBodyCalls.ConstrainedSealedReadonly), false)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedInherited), false)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedOverride), false)]
        [TestCase(nameof(ILBodyCalls.ConstrainedSealedGenericSlot), false)]
        [TestCase(nameof(ILBodyCalls.ConstrainedOpenReference), true)]
        public void ILConstrainedDispatchCertaintyUsesReceiverNotTargetDeclaringType(string name, bool unresolved) {
            var generator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var args = new object[] { ILBodyMethod(name), null };
            generator.GetMethod("GetComparisonAnalysis", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(generator), args);
            var diagnostics = (string[])args[1];
            Assert.AreEqual(unresolved, diagnostics.Any(row => row.StartsWith("UnresolvedVirtualCall:", StringComparison.Ordinal)),
                string.Join("\n", diagnostics));
            if (unresolved) {
                var actual = ILSystemPresenceAccesses(ILBodyMethod(name));
                Assert.AreEqual(RefOp.ReadWrite, actual[typeof(TestComponent)], "An unknown receiver can reset the caller's readonly query.");
            }
        }
    }
}
