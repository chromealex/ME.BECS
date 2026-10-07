using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata/IL only: none of these default builders or job bodies are run.
        private static void ILQueryRefAlias() {
            var query = default(QueryBuilder);
            ref var alias = ref query;
            alias.AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
            query = default;
            alias.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryRefParameter(ref QueryBuilder query) {
            query.AsReadonly();
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryRefReassignment(ref QueryBuilder query, ref QueryBuilder other) {
            query.AsReadonly();
            ref var before = ref query;
            query = ref other;
            query.AsReadonly();
            other = default;
            query.Schedule<QueryModeJob, TestComponent>();
            before.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryPossiblyAliasedParameters(ref QueryBuilder first, ref QueryBuilder second) {
            first.AsReadonly();
            second = default;
            first.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryBranchAlias(bool condition) {
            var first = default(QueryBuilder); var second = default(QueryBuilder);
            ref var alias = ref (condition ? ref first : ref second);
            alias.AsReadonly();
            first.Schedule<QueryModeJob, TestComponent>();
            second.Schedule<QueryModeJob, TestComponent>();
        }
        private static unsafe void ILQueryExplicitBooleanModes(bool readOnly) {
            JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(default, null, false, false, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
            JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(default, null, false, true, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
            JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(default, null, false, readOnly, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
        }
        private static void ILQueryCatchAfterMutation() {
            var query = default(QueryBuilder).AsReadonly();
            try { ResetQueryMode(ref query); }
            catch (System.Exception) { query.Schedule<QueryModeJob, TestComponent>(); }
        }
        private static void ILQueryMayThrow() { }
        private static void ILQueryFinallyReset() {
            var query = default(QueryBuilder).AsReadonly();
            try { ILQueryMayThrow(); }
            finally { query = default; }
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryIndependentComponentModes() {
            var read = default(QueryBuilder).AsReadonly();
            var write = default(QueryBuilder);
            write.Schedule<QueryModeJob, TestComponent>();
            read.Schedule<NativeBoolSizeJob, CompilerNativeBool>();
        }
        private static void ILQueryResetBeforeSchedule() {
            var query = default(QueryBuilder).AsReadonly();
            query = default;
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryUnknownMutationIsLocal() {
            var first = default(QueryBuilder).AsReadonly();
            var second = default(QueryBuilder).AsReadonly();
            ResetQueryMode(ref first);
            first.Schedule<QueryModeJob, TestComponent>();
            second.Schedule<QueryModeJob, TestComponent>();
        }
        private static void ILQueryByValueHelper(QueryBuilder query) { query = default; }
        private static void ILQueryByValueMutationKeepsOriginal() {
            var query = default(QueryBuilder).AsReadonly();
            ILQueryByValueHelper(query);
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static QueryBuilder AsReadonly(QueryBuilder query) => query;
        private static void ILUserNamedAsReadonlyIsNotAContract() =>
            AsReadonly(default).Schedule<QueryModeJob, TestComponent>();
        private static unsafe void ILQueryUnknownMemoryWrite(void* address) {
            var query = default(QueryBuilder).AsReadonly();
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemClear(address, 1);
            query.Schedule<QueryModeJob, TestComponent>();
        }
        private static unsafe void ILQueryCapturedFlag() {
            // Analyze the lowered local function separately; its closure field is
            // unknown without a caller binding, not its unrelated bool parameter.
            bool readOnly = true;
            void Local(bool unrelated) => JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(
                default, null, false, readOnly, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
            Local(false);
        }

        private static Type ILQueryModeAnalyzer => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILQueryScheduleModes", true);
        private static MethodInfo ILQueryModeMethod(string name) => typeof(Tests_SourceGeneratorContracts).GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        private static int[] ReadILQueryModes(MethodInfo method) {
            var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
            var result = (System.Collections.IDictionary)ILQueryModeAnalyzer.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { method, instructions });
            return result.Keys.Cast<int>().OrderBy(offset => offset).Select(offset => Convert.ToInt32(result[offset])).ToArray();
        }

        // Masks: 1=normal, 2=readonly, 4=unresolved; unions retain every possibility.
        [TestCase(nameof(IndependentQueryModes), "2,1,1,2")]
        [TestCase(nameof(QueryCopiesAndReset), "2,1")]
        [TestCase(nameof(TemporaryQueryReceiverIsNotTheOriginal), "1,2,1")]
        [TestCase(nameof(QueryModeBranch), "3")]
        [TestCase(nameof(QueryModeBothBranches), "2")]
        [TestCase(nameof(QueryModeConditionalValue), "3")]
        [TestCase(nameof(QueryModeLoop), "3,3")]
        [TestCase(nameof(QueryModeInParameter), "4,2")]
        [TestCase(nameof(QueryModeParameterBranch), "6")]
        [TestCase(nameof(QueryModeRefMutation), "4")]
        [TestCase(nameof(QueryModeOmittedMutation), "2")]
        [TestCase(nameof(QueryModeUnknownFactory), "4")]
        [TestCase(nameof(QueryModeFactory), "1,2")]
        [TestCase(nameof(ILQueryRefAlias), "2,1")]
        [TestCase(nameof(ILQueryRefParameter), "2")]
        [TestCase(nameof(ILQueryRefReassignment), "1,3")]
        [TestCase(nameof(ILQueryPossiblyAliasedParameters), "3")]
        [TestCase(nameof(ILQueryBranchAlias), "3,3")]
        [TestCase(nameof(ILQueryExplicitBooleanModes), "1,2,4")]
        [TestCase(nameof(ILQueryIndependentComponentModes), "1,2")]
        [TestCase(nameof(ILQueryResetBeforeSchedule), "1")]
        [TestCase(nameof(ILQueryUnknownMutationIsLocal), "4,2")]
        [TestCase(nameof(ILQueryByValueMutationKeepsOriginal), "2")]
        [TestCase(nameof(ILUserNamedAsReadonlyIsNotAContract), "4")]
        [TestCase(nameof(ILQueryUnknownMemoryWrite), "6")]
        public void ILScheduleModesFollowQueryStorage(string name, string expected) {
            CollectionAssert.AreEqual(expected.Split(',').Select(int.Parse).ToArray(), ReadILQueryModes(ILQueryModeMethod(name)));
        }

        [Test]
        public void ILScheduleModesKeepClosedGenericJobCalls() {
            var method = ILQueryModeMethod(nameof(QueryModeGenericHelper)).MakeGenericMethod(typeof(QueryModeJob));
            CollectionAssert.AreEqual(new[] { 4 }, ReadILQueryModes(method), "An unbound caller query must not become a proven normal/readonly value.");
        }

        [Test]
        public void ILCapturedReadonlyIsNotAnUnrelatedParameter() {
            var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(ILQueryModeMethod(nameof(ILQueryCapturedFlag)));
            var nested = instructions.Select(instruction => instruction.Operand).OfType<MethodInfo>()
                .Single(method => method.Name.Contains("g__Local"));
            CollectionAssert.AreEqual(new[] { 4 }, ReadILQueryModes(nested));
        }

        [TestCase(nameof(ILQueryCatchAfterMutation))]
        [TestCase(nameof(ILQueryFinallyReset))]
        public void ILExceptionalModeFlowCannotLosePossibleWrites(string name) {
            var modes = ReadILQueryModes(ILQueryModeMethod(name));
            Assert.AreEqual(1, modes.Length);
            Assert.IsTrue((modes[0] & 5) != 0, "A mutation/reset on an exception path must not leave a readonly-only schedule.");
        }

        [TestCase(1, RefOp.WriteOnly, true, RefOp.WriteOnly)]
        [TestCase(2, RefOp.WriteOnly, true, RefOp.ReadOnly)]
        [TestCase(3, RefOp.WriteOnly, true, RefOp.ReadWrite)]
        [TestCase(4, RefOp.WriteOnly, true, RefOp.ReadWrite)]
        [TestCase(2, RefOp.ReadWrite, false, RefOp.ReadWrite)]
        [TestCase(3, RefOp.ReadOnly, true, RefOp.ReadOnly)]
        public void ILScheduleModeProjectionRetainsReadAndWriteUnion(int mask, RefOp access, bool argument, RefOp expected) {
            var mode = Enum.ToObject(ILQueryModeAnalyzer.GetNestedType("Mode", BindingFlags.NonPublic), mask);
            Assert.AreEqual(expected, ILQueryModeAnalyzer.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, access, argument }));
        }

        [TestCase(nameof(ILQueryIndependentComponentModes), true)]
        [TestCase(nameof(ILQueryResetBeforeSchedule), false)]
        public void LegacyDependencyFallbackDoesNotLeakReadonlyBetweenQueries(string name, bool hasReadonlyJob) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var dependencies = type.GetMethod("GetComparisonDependencies", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(type), new object[] { ILQueryModeMethod(name) });
            var operations = (System.Collections.IEnumerable)dependencies.GetType().GetField("ops").GetValue(dependencies);
            var modes = operations.Cast<object>().ToDictionary(item => (Type)item.GetType().GetField("type").GetValue(item),
                item => (RefOp)item.GetType().GetField("op").GetValue(item));
            Assert.AreEqual(RefOp.ReadWrite, modes[typeof(TestComponent)]);
            if (hasReadonlyJob) Assert.AreEqual(RefOp.ReadOnly, modes[typeof(CompilerNativeBool)]);
            else Assert.IsFalse(modes.ContainsKey(typeof(CompilerNativeBool)));
        }

        private static void ILCompleteWithoutComponentAccess(ref SystemContext context) => context.dependsOn.Complete();

        [Test]
        public void LegacyFallbackCannotRecommendRemovingCompleteBasedOnComponentsAlone() {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var dependencies = type.GetMethod("GetComparisonDependencies", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(type), new object[] { ILQueryModeMethod(nameof(ILCompleteWithoutComponentAccess)) });
            var errors = (System.Collections.IEnumerable)dependencies.GetType().GetField("errors").GetValue(dependencies);
            Assert.IsFalse(errors.Cast<object>().Any(error => error.GetType().GetField("code").GetValue(error).ToString() == "MethodNotRequired"));
        }
    }
}
