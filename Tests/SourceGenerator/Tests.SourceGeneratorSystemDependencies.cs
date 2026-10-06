using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct ReadonlyBodyWriteJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent argument) => ent.Set(new TestComponent());
        }
        public partial struct ReadonlyOtherBodyWriteJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent argument) => ent.Set(new Test1Component());
        }
        public partial struct WriteOnlyQueryArgumentJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, [WO] ref TestComponent argument) { }
        }
        public partial struct AspectPresenceOnlyJob : IJobForAspects<QueryFilterAspect> {
            public void Execute(in JobInfo info, in Ent ent, ref QueryFilterAspect aspect) { }
        }
        public partial struct MixedPresenceOnlyJob : IJobFor1Aspects1Components<QueryFilterAspect, Test1Component> {
            public void Execute(in JobInfo info, in Ent ent, ref QueryFilterAspect aspect, [WO] ref Test1Component component) { }
        }

        // Compile-time metadata fixtures, not executable default-builder queries.
        public partial struct ReadonlyBodyWriteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(QueryBuilder).AsReadonly()
                .Schedule<ReadonlyBodyWriteJob, TestComponent>();
        }
        public partial struct ReadonlyOtherBodyWriteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(QueryBuilder).AsReadonly()
                .Schedule<ReadonlyOtherBodyWriteJob, TestComponent>();
        }
        public partial struct WriteOnlyQueryArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(QueryBuilder).Schedule<WriteOnlyQueryArgumentJob, TestComponent>();
        }
        public partial struct AspectPresenceOnlySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => QueryAspectScheduleExtensions.Schedule<AspectPresenceOnlyJob, QueryFilterAspect>(default(QueryBuilder));
        }
        public partial struct ReadonlyWriteOnlyQueryArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(QueryBuilder).AsReadonly().Schedule<WriteOnlyQueryArgumentJob, TestComponent>();
        }
        public partial struct MixedPresenceOnlySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) =>
                QueryAspectsComponentsScheduleExtensions1_1.Schedule<MixedPresenceOnlyJob, QueryFilterAspect, Test1Component>(default(QueryBuilder));
        }
        public partial struct FullDependencyUnionSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                this.ent.Set(new Test1Component());
                default(QueryBuilder).With<Test2Component>().Without<Test3Component>().AsReadonly().Schedule<QueryModeJob, TestComponent>();
            }
        }

        private static string[] SystemDependencyRows(Type system) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return system.Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == "ME.BECS.SystemDependencies.v1" && attribute.Value != null)
            .Select(attribute => attribute.Value.Split('\n'))
            .Single(rows => rows[0] == (system.IsGenericType ? system.AssemblyQualifiedName : system.FullName));
        }

        private static string SystemComponent(int mode, Type component) => "C\t" + mode + "\t" + component.AssemblyQualifiedName;

        [TestCase(typeof(HiddenRuntimeUsageSystem), typeof(HiddenUsageComponent))]
        [TestCase(typeof(HiddenArgumentRuntimeUsageSystem), typeof(GenericUsageComponent<HiddenUsageComponent>))]
        public void EditorSystemDependencyTablesRetainInaccessibleComponentTypes(Type system, Type component) {
            var bootstrap = SystemDependencyPublisher;
            var entries = ((System.Collections.IEnumerable)bootstrap.GetMethod("GetSystemComponentsDependencies")
                .Invoke(null, new object[] { system })).Cast<object>().ToArray();
            Assert.AreEqual(1, entries.Length, "Inaccessible component dependencies must not be dropped from the IL snapshot.");
            Assert.AreEqual(component, entries[0].GetType().GetField("type").GetValue(entries[0]));
            Assert.AreEqual((byte)RefOp.ReadWrite, entries[0].GetType().GetField("op").GetValue(entries[0]));
        }

        private static bool ParseSystemDependencyRows(Assembly publisher, string[] rows, out object operations, out string reason) {
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemDependencies", true);
            var parse = reader.GetMethod("TryParse", BindingFlags.NonPublic | BindingFlags.Static);
            var args = new object[] { publisher, rows, null, null, null };
            var valid = (bool)parse.Invoke(null, args);
            operations = args[3];
            reason = args[4] as string;
            if (valid) Assert.IsNotNull(args[2], "A complete plan must bind its exact lifecycle method.");
            return valid;
        }

        [TestCase(typeof(ReadOnlyScheduleModeSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(MixedScheduleModeSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(ReadonlyBodyWriteSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(WriteOnlyQueryArgumentSystem), typeof(TestComponent), 2)]
        [TestCase(typeof(ReadonlyWriteOnlyQueryArgumentSystem), typeof(TestComponent), 0)]
        [TestCase(typeof(AspectPresenceOnlySystem), typeof(TestComponent), 0)]
        [TestCase(typeof(GenericScheduleModeSystem<AotMarker>), typeof(AotMarker), 0)]
        [TestCase(typeof(ExplicitDirectAccessSummarySystem), typeof(TestComponent), 2)]
        [TestCase(typeof(HiddenRuntimeUsageSystem), typeof(HiddenUsageComponent), 2)]
        public void CompilerSystemDependenciesHaveExactTypedUnion(Type system, Type component, int mode) {
            var rows = SystemDependencyRows(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { SystemComponent(mode, component) }, rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ParseSystemDependencyRows(system.Assembly, rows, out _, out var reason), reason);
        }

        [Test]
        public void MixedJobContractsIncludeAspectPresenceAndComponentPresence() {
            var rows = SystemDependencyRows(typeof(MixedPresenceOnlySystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEquivalent(new[] { SystemComponent(0, typeof(TestComponent)), SystemComponent(2, typeof(Test1Component)) },
                rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ParseSystemDependencyRows(typeof(MixedPresenceOnlySystem).Assembly, rows, out _, out var reason), reason);
        }

        [Test]
        public void PresenceUnionDoesNotRewriteTheJobsWriteOnlyContract() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var job = typeof(WriteOnlyQueryArgumentJob);
            var rows = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobSafety.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n')).Single(entry => entry[0] == job.FullName);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var dependency = rows.Single(row => row.StartsWith("D\t", StringComparison.Ordinal)).Split('\t');
            Assert.AreEqual("T:ME.BECS.Tests.TestComponent", dependency[2]);
            Assert.AreEqual("1", dependency[3], "WO stays WO in the job. Only the system union adds the presence read.");
            Assert.AreEqual("1", dependency[4], "The access belongs to an Execute argument.");
        }

        [Test]
        public void ReadonlyArgumentsDoNotNarrowIndependentJobBodyAccesses() {
            var rows = SystemDependencyRows(typeof(ReadonlyOtherBodyWriteSystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEquivalent(new[] { SystemComponent(0, typeof(TestComponent)), SystemComponent(2, typeof(Test1Component)) },
                rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ParseSystemDependencyRows(typeof(ReadonlyOtherBodyWriteSystem).Assembly, rows, out _, out var reason), reason);
        }

        [Test]
        public void SystemUnionIncludesDirectCallsFiltersAndScheduledJobs() {
            var rows = SystemDependencyRows(typeof(FullDependencyUnionSystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEquivalent(new[] {
                SystemComponent(0, typeof(TestComponent)), SystemComponent(2, typeof(Test1Component)),
                SystemComponent(0, typeof(Test2Component)), SystemComponent(0, typeof(Test3Component)),
            }, rows.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ParseSystemDependencyRows(typeof(FullDependencyUnionSystem).Assembly, rows, out _, out var reason), reason);
        }

        [Test]
        public void SystemPointerPlanRetainsTheExactClosedDependency() {
            var rows = SystemDependencyRows(typeof(SystemPointerSummarySystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "Y\t" + typeof(GenericAotSystem<AotMarker>).AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("Y\t", StringComparison.Ordinal)).ToArray());
            Assert.IsTrue(ParseSystemDependencyRows(typeof(SystemPointerSummarySystem).Assembly, rows, out _, out var reason), reason);
        }

        [Test]
        public void IncompleteDependenciesNeverProduceATypedPlan() {
            var rows = SystemDependencyRows(typeof(UnknownScheduleModeSystem));
            Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\t", StringComparison.Ordinal)));
            Assert.IsFalse(rows.Any(row => row.StartsWith("P\t", StringComparison.Ordinal)));
            Assert.IsFalse(ParseSystemDependencyRows(typeof(UnknownScheduleModeSystem).Assembly, rows, out var operations, out _));
            Assert.IsNull(operations);
        }

        [Test]
        public void SystemDependencyParserRejectsTamperedCompleteRows() {
            var publisher = typeof(ReadOnlyScheduleModeSystem).Assembly;
            var rows = SystemDependencyRows(typeof(ReadOnlyScheduleModeSystem));
            Assert.IsTrue(ParseSystemDependencyRows(publisher, rows, out _, out var reason), reason);
            var component = rows.Single(row => row.StartsWith("C\t", StringComparison.Ordinal));
            var binding = SystemDependencyRows(typeof(MixedScheduleModeSystem)).Single(row => row.StartsWith("R\t", StringComparison.Ordinal));
            var mutations = new[] {
                rows.Concat(new[] { component }).ToArray(),
                rows.Where(row => row != component).ToArray(),
                rows.Select(row => row == component ? SystemComponent(2, typeof(TestComponent)) : row).ToArray(),
                rows.Select(row => row == component ? SystemComponent(0, typeof(Test1Component)) : row).ToArray(),
                rows.Where(row => !row.StartsWith("P\t", StringComparison.Ordinal)).ToArray(),
                rows.Select(row => row.StartsWith("R\t", StringComparison.Ordinal) ? binding : row).ToArray(),
                rows.Concat(new[] { "G\tunreported gap" }).ToArray(),
            };
            foreach (var mutation in mutations) {
                Assert.IsFalse(ParseSystemDependencyRows(publisher, mutation, out var operations, out _), string.Join("\n", mutation));
                Assert.IsNull(operations, "Rejected plans must not leak partial operations.");
            }
        }

        [Test]
        public void SystemDependencyReaderReturnsIndependentSets() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var readerType = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemDependencies", true);
            var reader = Activator.CreateInstance(readerType, true);
            var read = readerType.GetMethod("TryRead", BindingFlags.NonPublic | BindingFlags.Instance);
            var root = typeof(ReadOnlyScheduleModeSystem).GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var args = new object[] { root, null, null };
            Assert.IsTrue((bool)read.Invoke(reader, args), args[2] as string);
            var first = args[1];
            first.GetType().GetMethod("Clear").Invoke(first, null);
            Assert.IsTrue((bool)read.Invoke(reader, args), args[2] as string);
            Assert.AreNotSame(first, args[1]);
            Assert.AreEqual(1, args[1].GetType().GetProperty("Count").GetValue(args[1]));
        }

        // Keep the test asmdef independent of the Editor asmdef. Only the typed
        // metadata getters and selector run; no lifecycle, job or world is executed.
        private static Func<MethodInfo, T> CreateDependencyFallback<T>(Action visited, object result) => root => {
            visited();
            return (T)result;
        };

        private static object DependencySelector(Type system, Action fallbackVisited, out object fallback, bool export = false) {
            if (!export) SourceAnalysisTests.Require();
            var readerType = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemDependencies", true);
            var select = readerType.GetMethod(export ? "SelectForExport" : "Select", BindingFlags.NonPublic | BindingFlags.Instance);
            var resultType = select.ReturnType;
            fallback = Activator.CreateInstance(resultType);
            var opsField = resultType.GetField("ops");
            var ops = Activator.CreateInstance(opsField.FieldType);
            var operationType = opsField.FieldType.GetGenericArguments()[0];
            var operation = Activator.CreateInstance(operationType);
            operationType.GetField("type").SetValue(operation, typeof(Test3Component));
            operationType.GetField("op").SetValue(operation, RefOp.ReadWrite);
            opsField.FieldType.GetMethod("Add").Invoke(ops, new[] { operation });
            opsField.SetValue(fallback, ops);
            var errorsField = resultType.GetField("errors");
            var errors = (System.Collections.IList)Activator.CreateInstance(errorsField.FieldType);
            var errorType = errorsField.FieldType.GetGenericArguments()[0];
            var error = Activator.CreateInstance(errorType);
            errorType.GetField("message").SetValue(error, "legacy diagnostic sentinel");
            errors.Add(error);
            errorsField.SetValue(fallback, errors);
            var factory = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(CreateDependencyFallback), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(resultType);
            var callback = factory.Invoke(null, new[] { (object)fallbackVisited, fallback });
            var root = system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var arguments = export ? new[] { (object)root, callback, null, null } : new[] { (object)root, callback };
            var reader = Activator.CreateInstance(readerType, true);
            var selected = select.Invoke(reader, arguments);
            if (export) {
                Assert.AreEqual(false, arguments[2], "Source operations must not replace the IL snapshot.");
                Assert.AreEqual(false, arguments[3], "Source warnings must not replace the IL snapshot.");
                Assert.IsNull(readerType.GetField("assemblies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader),
                    "Export must not inventory source catalog publishers.");
                foreach (var field in new[] { "metadata", "selected" })
                    Assert.IsEmpty((System.Collections.IDictionary)readerType.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader),
                        "Export must not read or select a diagnostic source catalog.");
            }
            return selected;
        }

        private static string[] SelectedDependencyRows(object selection) =>
            ((System.Collections.IEnumerable)selection.GetType().GetField("ops").GetValue(selection)).Cast<object>()
            .Select(op => SystemComponent((int)(RefOp)op.GetType().GetField("op").GetValue(op), (Type)op.GetType().GetField("type").GetValue(op)))
            .OrderBy(row => row, StringComparer.Ordinal).ToArray();

        private static System.Collections.IList SelectedDependencyErrors(object selection) =>
            (System.Collections.IList)selection.GetType().GetField("errors").GetValue(selection);

        [TestCase(typeof(WriteOnlyQueryArgumentSystem), 0)]
        [TestCase(typeof(AspectPresenceOnlySystem), 0)]
        [TestCase(typeof(SyncProofCopiesSystem), 0)]
        [TestCase(typeof(SyncProofDefaultSystem), 1)]
        [TestCase(typeof(SyncProofBranchSystem), 1)]
        [TestCase(typeof(SyncProofExplicitSystem), 0)]
        [TestCase(typeof(SyncProofGenericSystem<AotMarker>), 0)]
        public void CompleteSourceSystemSelectionNeverInvokesLegacy(Type system, int errorCount) {
            var selected = DependencySelector(system, () => Assert.Fail("Complete source contracts must not read IL."), out _);
            CollectionAssert.AreEqual(SystemDependencyRows(system).Where(row => row.StartsWith("C\t", StringComparison.Ordinal))
                .OrderBy(row => row, StringComparer.Ordinal).ToArray(), SelectedDependencyRows(selected));
            var errors = SelectedDependencyErrors(selected);
            Assert.AreEqual(errorCount, errors.Count);
            if (errorCount != 0) {
                Assert.AreEqual(0, Convert.ToInt32(errors[0].GetType().GetField("code").GetValue(errors[0])));
                StringAssert.Contains("while work is still pending", errors[0].GetType().GetMethod("GetDisplayMessage").Invoke(errors[0], null) as string);
            }
        }

        [Test]
        public void IncompleteSynchronizationRetainsTheCompleteSourceDependencyUnion() {
            var system = typeof(SyncCaptureReadonlySystem);
            Assert.AreEqual("0", SystemDependencyRows(system)[2]);
            CollectionAssert.Contains(SynchronizationSummary(system), "S\tincomplete");
            var calls = 0;
            var selected = DependencySelector(system, () => ++calls, out var fallback);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(new[] { SystemComponent(2, typeof(TestComponent)) }, SelectedDependencyRows(selected));
            CollectionAssert.AreEqual(SelectedDependencyErrors(fallback), SelectedDependencyErrors(selected));
            Assert.AreNotSame(SelectedDependencyErrors(fallback), SelectedDependencyErrors(selected));
        }

        // Unknown query schedule modes still prevent a complete dependency union,
        // even when the lifecycle's handle-completion proof is available.
        [TestCase(typeof(UnknownScheduleModeSystem), "proven", 0)]
        public void IncompleteDependencyUnionRetainsTheCompleteSourceSynchronizationPlan(Type system, string synchronization, int errorCount) {
            Assert.AreNotEqual("0", SystemDependencyRows(system)[2]);
            CollectionAssert.Contains(SynchronizationSummary(system), "S\t" + synchronization);
            var calls = 0;
            var selected = DependencySelector(system, () => ++calls, out var fallback);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(SelectedDependencyRows(fallback), SelectedDependencyRows(selected));
            var errors = SelectedDependencyErrors(selected);
            Assert.AreEqual(errorCount, errors.Count);
            if (errorCount != 0) {
                Assert.AreEqual(0, Convert.ToInt32(errors[0].GetType().GetField("code").GetValue(errors[0])));
                StringAssert.Contains("while work is still pending", errors[0].GetType().GetMethod("GetDisplayMessage").Invoke(errors[0], null) as string);
            }
        }

        [Test]
        public void TwoIncompleteSourceContractsEvaluateLegacyOnlyOnce() {
            var system = typeof(SyncProofUnknownSystem);
            Assert.AreNotEqual("0", SystemDependencyRows(system)[2]);
            CollectionAssert.Contains(SynchronizationSummary(system), "S\tincomplete");
            var calls = 0;
            var selected = DependencySelector(system, () => ++calls, out var fallback);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(SelectedDependencyRows(fallback), SelectedDependencyRows(selected));
            CollectionAssert.AreEqual(SelectedDependencyErrors(fallback), SelectedDependencyErrors(selected));
        }

        [Test]
        public void SourceSynchronizationReaderRejectsUnboundAndContradictoryPlans() {
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemDependencies", true);
            var parse = reader.GetMethod("TryParseSynchronization", BindingFlags.NonPublic | BindingFlags.Static);
            var system = typeof(SyncProofDefaultSystem);
            var rows = SynchronizationSummary(system);
            var args = new object[] { system.Assembly, rows, null, null, null };
            Assert.IsTrue((bool)parse.Invoke(null, args), args[4] as string);
            var binding = SynchronizationSummary(typeof(SyncProofCopiesSystem)).Single(row => row.StartsWith("R\t", StringComparison.Ordinal));
            var mutations = new[] {
                rows.Select(row => row == "S\tunproven" ? "S\tproven" : row).ToArray(),
                rows.Select(row => row.StartsWith("R\t", StringComparison.Ordinal) ? binding : row).ToArray(),
                rows.Where(row => !row.StartsWith("R\t", StringComparison.Ordinal)).ToArray(),
                rows.Concat(new[] { "S\tunproven" }).ToArray(),
                rows.Where(row => !row.StartsWith("E\t", StringComparison.Ordinal)).ToArray(),
                SynchronizationSummary(typeof(SyncProofUnknownSystem)),
            };
            foreach (var mutation in mutations) {
                args[1] = mutation;
                Assert.IsFalse((bool)parse.Invoke(null, args), string.Join("\n", mutation));
                Assert.IsNull(args[2]);
                Assert.IsNull(args[3], "Rejected synchronization must not suppress legacy diagnostics.");
            }
        }

        [Test]
        public void SourceSynchronizationReaderReturnsIndependentErrorsAndSerializedMessages() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var readerType = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSystemDependencies", true);
            var reader = Activator.CreateInstance(readerType, true);
            var read = readerType.GetMethod("TryReadSynchronization", BindingFlags.NonPublic | BindingFlags.Instance);
            var root = typeof(SyncProofDefaultSystem).GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var args = new object[] { root, null, null };
            Assert.IsTrue((bool)read.Invoke(reader, args), args[2] as string);
            var first = (System.Collections.IList)args[1];
            Assert.AreEqual(1, first.Count);
            var original = first[0];
            var type = original.GetType();
            var serialized = Activator.CreateInstance(type);
            var message = type.GetMethod("GetDisplayMessage").Invoke(original, null);
            type.GetField("code").SetValue(serialized, type.GetField("code").GetValue(original));
            type.GetField("message").SetValue(serialized, message);
            Assert.AreEqual(message, type.GetMethod("GetDisplayMessage").Invoke(serialized, null),
                "Compiler bootstrap errors store text, not a live caller MethodInfo.");
            first.Clear();
            Assert.IsTrue((bool)read.Invoke(reader, args), args[2] as string);
            Assert.AreNotSame(first, args[1]);
            Assert.AreEqual(1, ((System.Collections.IList)args[1]).Count);
        }
    }
}
