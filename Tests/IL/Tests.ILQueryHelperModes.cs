using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using ME.BECS.Mono.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Fixtures are disassembled, never invoked: default builders aren't runnable queries.
        private static void ILBoundSchedule(QueryBuilder query) => query.Schedule<QueryModeJob, TestComponent>();
        private static void ILBoundReadonly() => ILBoundSchedule(default(QueryBuilder).AsReadonly());
        private static void ILBoundWritable() => ILBoundSchedule(default);
        private static void ILBoundBoth() { ILBoundReadonly(); ILBoundWritable(); }
        private static QueryBuilder ILBoundIdentity(QueryBuilder query) => query;
        private static QueryBuilder ILBoundFactory() => default(QueryBuilder).AsReadonly();
        private static void ILBoundReturns() {
            ILBoundIdentity(default(QueryBuilder).AsReadonly()).Schedule<QueryModeJob, TestComponent>();
            ILBoundIdentity(default).Schedule<QueryModeJob, TestComponent>();
            ILBoundFactory().Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundMakeReadonly(ref QueryBuilder query) => query.AsReadonly();
        private static void ILBoundReset(out QueryBuilder query) => query = default;
        private static void ILBoundRefOut() {
            var query = default(QueryBuilder);
            ILBoundMakeReadonly(ref query);
            query.Schedule<QueryModeJob, TestComponent>();
            ILBoundReset(out query);
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundAliasing(ref QueryBuilder first, ref QueryBuilder second) {
            first.AsReadonly();
            second = default;
            first.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundSameStorage() { var query = default(QueryBuilder); ILBoundAliasing(ref query, ref query); }
        private static void ILBoundDistinctStorage() {
            var first = default(QueryBuilder); var second = default(QueryBuilder);
            ILBoundAliasing(ref first, ref second);
        }
        private static void ILBoundConditionalAlias(bool condition) {
            var first = default(QueryBuilder); var second = default(QueryBuilder);
            ref var alias = ref (condition ? ref first : ref second);
            ILBoundMakeReadonly(ref alias);
            first.Schedule<QueryModeJob, TestComponent>();
            second.Schedule<QueryModeJob, TestComponent>();
        }
        private static ref QueryBuilder ILBoundRefIdentity(ref QueryBuilder query) => ref query;
        private static void ILBoundReferenceReturn() {
            var query = default(QueryBuilder);
            ref var alias = ref ILBoundRefIdentity(ref query);
            alias.AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundRebind(ref QueryBuilder first, ref QueryBuilder second) {
            first = ref second;
            first = default;
        }
        private static void ILBoundRebindCaller() {
            var first = default(QueryBuilder).AsReadonly(); var second = default(QueryBuilder).AsReadonly();
            ILBoundRebind(ref first, ref second);
            first.Schedule<QueryModeJob, TestComponent>();
            second.Schedule<QueryModeJob, TestComponent>();
        }
        private static bool ILBoundBoolean(bool value) => value;
        private static unsafe void ILBoundFlagSchedule(bool readOnly) =>
            JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(default, null, false, readOnly, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
        private static void ILBoundFlags() { ILBoundFlagSchedule(ILBoundBoolean(true)); ILBoundFlagSchedule(ILBoundBoolean(false)); }
        private static void ILBoundFlagReadonly() => ILBoundFlagSchedule(ILBoundBoolean(true));
        private static QueryBuilder ILBoundConditionalFactory(bool readOnly) {
            if (readOnly) return default(QueryBuilder).AsReadonly();
            return default;
        }
        private static bool ILBoundNegate(bool value) => !value;
        private static void ILBoundConditionalReturns() {
            ILBoundConditionalFactory(true).Schedule<QueryModeJob, TestComponent>();
            ILBoundConditionalFactory(false).Schedule<QueryModeJob, TestComponent>();
            ILBoundConditionalFactory(ILBoundNegate(false)).Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundGeneric<T>(QueryBuilder query) where T : struct, IJobForComponents<TestComponent> => query.Schedule<T, TestComponent>();
        private static void ILBoundGenericCaller() => ILBoundGeneric<QueryModeJob>(default(QueryBuilder).AsReadonly());
        private static void ILBoundMayThrow(ref QueryBuilder query, bool fail) {
            query = default;
            if (fail) throw new System.InvalidOperationException();
            query.AsReadonly();
        }
        private static void ILBoundNormalReturn(bool fail) {
            var query = default(QueryBuilder).AsReadonly();
            ILBoundMayThrow(ref query, fail);
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundExceptionalReturn(bool fail) {
            var query = default(QueryBuilder).AsReadonly();
            try { ILBoundMayThrow(ref query, fail); }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        private interface ILBoundUnknownMutation { void Reset(ref QueryBuilder query); }
        private static void ILBoundVirtualMutation(ILBoundUnknownMutation mutation) {
            var query = default(QueryBuilder).AsReadonly();
            mutation.Reset(ref query);
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILBoundRecursive(QueryBuilder query, bool recurse) {
            ILBoundSchedule(query);
            if (recurse) ILBoundRecursive(query, recurse);
        }
        private static void ILBoundRecursiveCaller(bool recurse) => ILBoundRecursive(default(QueryBuilder).AsReadonly(), recurse);
        private delegate void ILBoundQueryCallback(QueryBuilder query);
        private static void ILBoundEscapedHelper() {
            ILBoundReadonly();
            ILBoundQueryCallback callback = ILBoundSchedule;
            callback(default);
        }
        private static void ILBoundWriteOnlySchedule(QueryBuilder query) => query.Schedule<WriteOnlyQueryArgumentJob, TestComponent>();
        private static void ILBoundWriteOnlyBoth() {
            ILBoundWriteOnlySchedule(default(QueryBuilder).AsReadonly());
            ILBoundWriteOnlySchedule(default);
        }

        private static System.Collections.IDictionary ReadILQueryGraph(string rootName) {
            var root = ILQueryModeMethod(rootName);
            var bodies = new System.Collections.Generic.Dictionary<MethodInfo, Instruction[]>();
            var pending = new System.Collections.Generic.Queue<MethodInfo>();
            pending.Enqueue(root);
            while (pending.Count > 0) {
                var method = pending.Dequeue();
                if (bodies.ContainsKey(method)) continue;
                var instructions = method.GetInstructions().ToArray();
                bodies.Add(method, instructions);
                foreach (var call in instructions.Select(instruction => instruction.Operand).OfType<MethodInfo>())
                    if (call.DeclaringType == typeof(Tests_SourceGeneratorContracts) && call.GetMethodBody() != null)
                        pending.Enqueue(call);
            }
            return (System.Collections.IDictionary)ILQueryModeAnalyzer.GetMethod("ReadGraph", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { root, bodies });
        }

        [TestCase(nameof(ILBoundReadonly), nameof(ILBoundSchedule), "2")]
        [TestCase(nameof(ILBoundWritable), nameof(ILBoundSchedule), "1")]
        [TestCase(nameof(ILBoundBoth), nameof(ILBoundSchedule), "3")]
        [TestCase(nameof(ILBoundReturns), nameof(ILBoundReturns), "2,1,2")]
        [TestCase(nameof(ILBoundRefOut), nameof(ILBoundRefOut), "2,1")]
        [TestCase(nameof(ILBoundSameStorage), nameof(ILBoundAliasing), "1")]
        [TestCase(nameof(ILBoundDistinctStorage), nameof(ILBoundAliasing), "2")]
        [TestCase(nameof(ILBoundConditionalAlias), nameof(ILBoundConditionalAlias), "3,3")]
        [TestCase(nameof(ILBoundReferenceReturn), nameof(ILBoundReferenceReturn), "2")]
        [TestCase(nameof(ILBoundRebindCaller), nameof(ILBoundRebindCaller), "2,1")]
        [TestCase(nameof(ILBoundFlags), nameof(ILBoundFlagSchedule), "3")]
        [TestCase(nameof(ILBoundFlagReadonly), nameof(ILBoundFlagSchedule), "2")]
        [TestCase(nameof(ILBoundConditionalReturns), nameof(ILBoundConditionalReturns), "2,1,2")]
        [TestCase(nameof(ILBoundNormalReturn), nameof(ILBoundNormalReturn), "2")]
        [TestCase(nameof(ILBoundExceptionalReturn), nameof(ILBoundExceptionalReturn), "3")]
        [TestCase(nameof(ILBoundVirtualMutation), nameof(ILBoundVirtualMutation), "4")]
        [TestCase(nameof(ILBoundRecursiveCaller), nameof(ILBoundSchedule), "4")]
        [TestCase(nameof(ILBoundEscapedHelper), nameof(ILBoundSchedule), "6")]
        [TestCase(nameof(ILBoundWriteOnlyBoth), nameof(ILBoundWriteOnlySchedule), "3")]
        public void ILQueryHelpersBindValuesAliasesAndExceptionalEffects(string rootName, string methodName, string expected) {
            var graph = ReadILQueryGraph(rootName);
            var result = (System.Collections.IDictionary)graph[ILQueryModeMethod(methodName)];
            CollectionAssert.AreEqual(expected.Split(',').Select(int.Parse).ToArray(),
                result.Keys.Cast<int>().OrderBy(offset => offset).Select(offset => Convert.ToInt32(result[offset])).ToArray());
        }

        [Test]
        public void ILQueryHelperBindsClosedGenericMethod() {
            var graph = ReadILQueryGraph(nameof(ILBoundGenericCaller));
            var result = (System.Collections.IDictionary)graph[ILQueryModeMethod(nameof(ILBoundGeneric)).MakeGenericMethod(typeof(QueryModeJob))];
            CollectionAssert.AreEqual(new[] { 2 }, result.Values.Cast<object>().Select(Convert.ToInt32).ToArray());
        }

        [TestCase(nameof(ILBoundReadonly), RefOp.ReadOnly)]
        [TestCase(nameof(ILBoundWritable), RefOp.ReadWrite)]
        [TestCase(nameof(ILBoundBoth), RefOp.ReadWrite)]
        public void ILSystemDependencyFallbackConsumesAllBoundHelperModes(string rootName, RefOp expected) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var dependencies = type.GetMethod("GetComparisonDependencies", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(type), new object[] { ILQueryModeMethod(rootName) });
            var operations = (System.Collections.IEnumerable)dependencies.GetType().GetField("ops").GetValue(dependencies);
            var selected = operations.Cast<object>().Single(item => (Type)item.GetType().GetField("type").GetValue(item) == typeof(TestComponent));
            Assert.AreEqual(expected, selected.GetType().GetField("op").GetValue(selected));
        }

        [TestCase(typeof(ReadOnlyScheduleModeSystem), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(typeof(MixedScheduleModeSystem), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(typeof(GenericScheduleModeSystem<AotMarker>), typeof(AotMarker), RefOp.ReadOnly)]
        [TestCase(typeof(ReadonlyBodyWriteSystem), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(typeof(ReadonlyOtherBodyWriteSystem), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(typeof(ReadonlyOtherBodyWriteSystem), typeof(Test1Component), RefOp.ReadWrite)]
        public void ILQueryHelperBindingsReachCompiledSystemDependencies(Type system, Type component, RefOp expected) {
            var root = system.GetMethod("OnUpdate");
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var dependencies = analyzer.GetMethod("GetComparisonDependencies", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(analyzer), new object[] { root });
            var operations = (System.Collections.IEnumerable)dependencies.GetType().GetField("ops").GetValue(dependencies);
            var selected = operations.Cast<object>().Single(item => (Type)item.GetType().GetField("type").GetValue(item) == component);
            Assert.AreEqual(expected, selected.GetType().GetField("op").GetValue(selected));
        }
    }
}
