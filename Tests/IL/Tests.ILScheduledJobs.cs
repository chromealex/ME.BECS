using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static void CollectScheduledIL(MethodInfo root, System.Collections.Generic.HashSet<Type> result) =>
            Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("Collect", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { root, result });

        private static Type[] ScheduledIL(Type system) {
            var discovery = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true);
            var roots = (System.Collections.Generic.List<MethodInfo>)discovery.GetMethod("GetLifecycleMethods")
                .Invoke(null, new object[] { system, null });
            var jobs = new System.Collections.Generic.HashSet<Type>();
            foreach (var root in roots) CollectScheduledIL(root, jobs);
            return jobs.OrderBy(job => job.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
        }

        // Inspect IL only. None of these constructors, jobs or lifecycle methods run.
        [TestCase(typeof(BaseArgumentCompletionSystem))]
        [TestCase(typeof(PendingConstructorInitializerSystem))]
        [TestCase(typeof(CompletedConstructorInitializerSystem))]
        [TestCase(typeof(ConditionalConstructorInitializerSystem))]
        [TestCase(typeof(PendingBranchConstructorInitializerSystem))]
        [TestCase(typeof(ImplicitBaseSchedulingSystem))]
        [TestCase(typeof(ExceptionConstructorSystem))]
        [TestCase(typeof(EventInitializerConstructorSystem))]
        [TestCase(typeof(ImplicitEventInitializerConstructorSystem))]
        [TestCase(typeof(StaticInitializerConstructorSystem))]
        [TestCase(typeof(GenericConstructorSchedulingSystem))]
        [TestCase(typeof(CatchUsingSchedulesSystem))]
        [TestCase(typeof(FinallyUsingSchedulesSystem))]
        public void ILScheduledJobsRetainConstructionAndDisposalEffects(Type system) {
            CollectionAssert.AreEqual(new[] { typeof(ControlFirstJob) }, ScheduledIL(system));
        }

        [Test]
        public void ILScheduledJobsTreatUnitySchedulersAsTerminals() {
            CollectionAssert.AreEquivalent(new[] {
                typeof(UnitySingleTerminalJob), typeof(UnityParallelTerminalJob), typeof(UnityForTerminalJob),
                typeof(UnityBatchTerminalJob), typeof(UnityDeferTerminalJob),
            }, ScheduledIL(typeof(UnitySchedulingTerminalsSystem)));
            CollectionAssert.IsEmpty(ScheduledIL(typeof(UserSchedulingTerminalNamesSystem)));
        }

        private partial struct ILScheduledSingleAliasJob : ME.BECS.Jobs.IJobSingle { public void Execute() { } }
        private partial struct ILScheduledCommandAliasJob : ME.BECS.Jobs.IJobParallelForCommandBuffer, ME.BECS.Jobs.IJobParallelForCommandBufferBatch {
            public void Execute(in CommandBufferJobParallel buffer) { }
            public void Execute(in CommandBufferJobBatch buffer) { }
        }
        private static void ILScheduledSingleAliases() {
            var job = default(ILScheduledSingleAliasJob);
            ME.BECS.Jobs.IJobSingleExtensions.ScheduleSingle(job);
            ME.BECS.Jobs.IJobSingleExtensions.ScheduleSingleByRef(ref job);
            ME.BECS.Jobs.IJobSingleExtensions.ScheduleSingleWithInject(job, 0);
            ME.BECS.Jobs.IJobSingleExtensions.ScheduleSingleWithInjectByRef(ref job, 0);
        }
        #pragma warning disable CS0618
        private static void ILScheduledCommandAliases() {
            default(QueryBuilder).ScheduleParallelFor<ILScheduledCommandAliasJob>();
            default(QueryBuilder).ScheduleParallelForBatch<ILScheduledCommandAliasJob>();
        }
        #pragma warning restore CS0618

        [TestCase(nameof(ILScheduledSingleAliases), typeof(ILScheduledSingleAliasJob))]
        [TestCase(nameof(ILScheduledCommandAliases), typeof(ILScheduledCommandAliasJob))]
        public void ILScheduledJobsTreatBECSAliasesAsTerminals(string method, Type job) {
            var jobs = new System.Collections.Generic.HashSet<Type>();
            CollectScheduledIL(typeof(Tests_SourceGeneratorContracts).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static), jobs);
            CollectionAssert.AreEqual(new[] { job }, jobs);
        }

        [Test]
        public void ILScheduledJobsRetainCatchFilterAndFinallyCalls() {
            CollectionAssert.AreEquivalent(new[] {
                typeof(UnitySingleTerminalJob), typeof(UnityParallelTerminalJob),
                typeof(UnityForTerminalJob), typeof(UnityBatchTerminalJob),
            }, ScheduledIL(typeof(SchedulingExceptionUnionSystem)));
        }

        [TestCase(typeof(FormattingInterpolationSystem))]
        [TestCase(typeof(FormatInterpolationBeforeAccessSystem))]
        [TestCase(typeof(FormatInterpolationPairSystem))]
        [TestCase(typeof(FormatInterpolationReversePairSystem))]
        [TestCase(typeof(FormatReferenceSystem))]
        [TestCase(typeof(FormatNullableSystem))]
        [TestCase(typeof(FormatManyReferenceSchedulesSystem))]
        [TestCase(typeof(LocalMapFormatSystem))]
        [TestCase(typeof(NativeMapFormattingSystem))]
        public void ILScheduledJobsRetainImplicitFormattingCallbacks(Type system) {
            CollectionAssert.AreEqual(new[] { typeof(ControlFirstJob) }, ScheduledIL(system));
        }

        [TestCase(typeof(FormatDeferredSystem))]
        [TestCase(typeof(FormatDualInterpolationSystem))]
        [TestCase(typeof(FormatManyReferenceNoopsSystem))]
        public void ILScheduledJobsDoNotInventUnusedFormattingCallbacks(Type system) {
            CollectionAssert.IsEmpty(ScheduledIL(system));
        }

        [TestCase(typeof(FormattingInterpolationSystem))]
        [TestCase(typeof(GenericConstructorSchedulingSystem))]
        [TestCase(typeof(FinallyUsingSchedulesSystem))]
        public void GraphJobSelectionExportsFreshILWithoutReadingSourceCatalogs(Type system) {
            var selector = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobs", true);
            var collect = selector.GetMethod("Collect", BindingFlags.Public | BindingFlags.Static);
            var jobs = new System.Collections.Generic.HashSet<Type> { typeof(ControlSecondJob) };
            collect.Invoke(null, new object[] { system, jobs, null });
            CollectionAssert.AreEquivalent(new[] { typeof(ControlFirstJob), typeof(ControlSecondJob) }, jobs);
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(collect)
                .Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
            Assert.IsFalse(calls.Any(method => method.DeclaringType == selector &&
                (method.Name == "TryCollect" || method.Name == "Read")),
                "Production graph discovery must not select or validate diagnostic source summaries.");
        }

        private struct ILFormattingValue : IFormattable {
            public override string ToString() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "plain"; }
            string IFormattable.ToString(string format, IFormatProvider provider) { _ = IJobExtensions.Schedule(default(ControlSecondJob), default); return "formatted"; }
        }
        private class ILFormattingReference {
            public override string ToString() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return "plain"; }
        }
        private sealed class ILFormattingHiddenReference : ILFormattingReference {
            public new string ToString() { _ = IJobExtensions.Schedule(default(ControlSecondJob), default); return "hidden"; }
        }
        private static void FormatBoxedAlias() { object value = default(ILFormattingValue); var alias = value; _ = string.Format("{0}", alias); }
        private static void FormatOverwrite() { object value = default(ILFormattingValue); value = "plain"; _ = string.Format("{0}", value); }
        private static void FormatBranch(bool choice) {
            object value;
            if (choice) value = default(ILFormattingValue); else value = new ILFormattingReference();
            _ = string.Format("{0}", value);
        }
        private static void FormatLoop(bool choice) {
            object value = "plain";
            while (choice) { _ = string.Format("{0}", value); value = default(ILFormattingValue); }
        }
        private static void FormatArrayAlias() { var values = new object[] { default(ILFormattingValue) }; var alias = values; _ = string.Format("{0}", alias); }
        private static void FormatSeparateArrays() {
            var unused = new object[] { default(ILFormattingValue) };
            var actual = new object[] { "plain" };
            _ = string.Format("{0}", actual);
        }
        private static void FormatConcatObject() { object value = default(ILFormattingValue); _ = string.Concat(value); }
        private static void FormatNewReference() { object value = new ILFormattingReference(); _ = string.Format("{0}", value); }
        private static void FormatHiddenReference() { object value = new ILFormattingHiddenReference(); _ = string.Format("{0}", value); }
        private static void FormatUnknownObject(object value) => _ = string.Format("{0}", value);

        [TestCase(nameof(FormatBoxedAlias), 2)]
        [TestCase(nameof(FormatOverwrite), 0)]
        [TestCase(nameof(FormatBranch), 3)]
        [TestCase(nameof(FormatLoop), 2)]
        [TestCase(nameof(FormatArrayAlias), 2)]
        [TestCase(nameof(FormatSeparateArrays), 0)]
        [TestCase(nameof(FormatConcatObject), 1)]
        [TestCase(nameof(FormatNewReference), 1)]
        [TestCase(nameof(FormatHiddenReference), 1)]
        public void ILScheduledFormattingUsesActualValueFlowAndDispatchSlot(string name, int mask) {
            var method = typeof(Tests_SourceGeneratorContracts).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            var jobs = new System.Collections.Generic.HashSet<Type>(); CollectScheduledIL(method, jobs);
            CollectionAssert.AreEquivalent(new[] { typeof(ControlFirstJob), typeof(ControlSecondJob) }.Where((_, index) => (mask & 1 << index) != 0), jobs);
        }

        [Test]
        public void ILScheduledFormattingDoesNotCertifyUnknownObjectDispatch() {
            var method = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(FormatUnknownObject), BindingFlags.NonPublic | BindingFlags.Static);
            var il = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILFormattingCallbacks", true);
            var args = new object[] { method, il, false };
            var targets = (MethodInfo[])analyzer.GetMethod("Collect", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            Assert.IsTrue((bool)args[2], "A runtime object remains unresolved, not a proven empty callback set.");
            CollectionAssert.IsEmpty(targets);
        }

        private static void DisposeScheduledScope<T>(T scope) where T : struct, IDisposable => scope.Dispose();
        private static void DisposeScheduledReference<T>(T scope) where T : IDisposable => scope.Dispose();
        private class ScheduledReferenceScope : IDisposable {
            public virtual void Dispose() => _ = IJobExtensions.Schedule(default(ControlFirstJob), default);
        }
        private sealed class ScheduledInheritedScope : ScheduledReferenceScope { }
        private sealed class ScheduledOverrideScope : ScheduledReferenceScope {
            public override void Dispose() => _ = IJobExtensions.Schedule(default(ControlSecondJob), default);
        }
        private sealed class ScheduledExplicitScope : IDisposable {
            void IDisposable.Dispose() => _ = IJobExtensions.Schedule(default(ControlSecondJob), default);
        }

        [TestCase(typeof(ScheduledInheritedScope), typeof(ControlFirstJob))]
        [TestCase(typeof(ScheduledOverrideScope), typeof(ControlSecondJob))]
        [TestCase(typeof(ScheduledExplicitScope), typeof(ControlSecondJob))]
        public void ILScheduledJobsBindSealedConstrainedReceivers(Type receiver, Type job) {
            var root = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(DisposeScheduledReference), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(receiver);
            var jobs = new System.Collections.Generic.HashSet<Type>();
            CollectScheduledIL(root, jobs);
            CollectionAssert.AreEqual(new[] { job }, jobs);
        }

        [Test]
        public void ILScheduledJobsResolveClosedConstrainedHelpers() {
            var root = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(DisposeScheduledScope), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(FinallySchedulingScope));
            var jobs = new System.Collections.Generic.HashSet<Type>();
            CollectScheduledIL(root, jobs);
            CollectionAssert.AreEqual(new[] { typeof(ControlFirstJob) }, jobs);
        }

        private struct ScheduledExpansion<T> { }

        [TestCase(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility), "AddressOf")]
        [TestCase(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility), "AsRef")]
        [TestCase(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility), "ArrayElementAsRef")]
        [TestCase(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility), "As")]
        [TestCase(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtilityExtensions), "AddressOf")]
        [TestCase(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtilityExtensions), "AsRef")]
        public void ILScheduledJobsDoNotInspectUnityAddressIntrinsicImplementation(Type owner, string name) {
            var definition = owner.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == name && method.IsGenericMethodDefinition);
            var root = definition.MakeGenericMethod(definition.GetGenericArguments().Select(_ => typeof(int)).ToArray());
            var jobs = new System.Collections.Generic.HashSet<Type>();
            CollectScheduledIL(root, jobs);
            CollectionAssert.IsEmpty(jobs);
        }

        private static void ExpandScheduledTypes<T>() {
            _ = IJobExtensions.Schedule(default(ControlFirstJob), default);
            ExpandScheduledTypes<ScheduledExpansion<T>>();
        }

        [Test]
        public void ILScheduledJobsDoNotPublishAPartialSetAtTraversalLimit() {
            var root = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(ExpandScheduledTypes), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(int));
            var jobs = new System.Collections.Generic.HashSet<Type> { typeof(ControlSecondJob) };
            var exception = Assert.Throws<TargetInvocationException>(() => CollectScheduledIL(root, jobs));
            Assert.IsInstanceOf<InvalidOperationException>(exception.GetBaseException());
            StringAssert.Contains("traversal limit", exception.GetBaseException().Message);
            CollectionAssert.AreEqual(new[] { typeof(ControlSecondJob) }, jobs);
        }
    }
}
