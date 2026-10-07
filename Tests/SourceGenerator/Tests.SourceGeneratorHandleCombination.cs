using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only; tests never schedule jobs or evaluate native handles.
        public static JobHandle ExternalHandleArgument(in Ent ent) { ent.Set(new TestComponent()); return default; }
        public static NativeArray<JobHandle> ExternalHandleArray(in Ent ent) { ent.Set(new Test1Component()); return default; }
        public static NativeSlice<JobHandle> ExternalHandleSlice(in Ent ent) { ent.Set(new Test2Component()); return default; }
        public static unsafe JobHandle* ExternalHandlePointer(in Ent ent) { ent.Set(new Test3Component()); return null; }
        public static int ExternalHandleCount(in Ent ent) { ent.Set(new Test1Component()); return 0; }

        public static unsafe void ExternalHandleCombinations(in Ent ent) {
            _ = JobHandle.CombineDependencies(ExternalHandleArgument(in ent), ExternalHandleArgument(in ent));
            _ = JobHandle.CombineDependencies(ExternalHandleArgument(in ent), ExternalHandleArgument(in ent), ExternalHandleArgument(in ent));
            _ = JobHandle.CombineDependencies(ExternalHandleArray(in ent));
            _ = JobHandle.CombineDependencies(ExternalHandleSlice(in ent));
            _ = JobHandleUnsafeUtility.CombineDependencies(ExternalHandlePointer(in ent), ExternalHandleCount(in ent));
        }
        public partial struct ExternalHandleEffectsJob : IJob {
            public Ent ent;
            public void Execute() => ExternalHandleCombinations(in this.ent);
        }

        public static void OtherExternalHandleMembers(JobHandle handle) {
            handle.Complete();
            _ = handle.IsCompleted;
            JobHandle.ScheduleBatchedJobs();
        }

        public static Action UnknownHandleCombinationCallback;
        public static class UserHandleCombination {
            public static JobHandle CombineDependencies(JobHandle first, JobHandle second) {
                UnknownHandleCombinationCallback();
                return first;
            }
        }
        public static void UserHandleCombinationCall() => UserHandleCombination.CombineDependencies(default, default);

        public partial struct CombinedTerminalFirstJob : IJob { public void Execute() => UnknownHandleCombinationCallback(); }
        public partial struct CombinedTerminalSecondJob : IJob { public void Execute() => UnknownHandleCombinationCallback(); }
        public partial struct CombinedSchedulingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                ExternalHandleCombinations(default);
                context.SetDependency(JobHandle.CombineDependencies(context.dependsOn,
                    IJobExtensions.Schedule(default(CombinedTerminalFirstJob), default),
                    IJobExtensions.Schedule(default(CombinedTerminalSecondJob), default)));
            }
        }
        public partial struct UnknownCombinedSchedulingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => context.SetDependency(UserHandleCombination.CombineDependencies(
                IJobExtensions.Schedule(default(CombinedTerminalFirstJob), default),
                IJobExtensions.Schedule(default(CombinedTerminalSecondJob), default)));
        }

        public partial struct CombineTwoSyncSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.CombineDependencies(context.dependsOn, default).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct CombineThreeSyncSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.CombineDependencies(context.dependsOn,
                    IJobExtensions.Schedule(default(CombinedTerminalFirstJob), default),
                    IJobExtensions.Schedule(default(CombinedTerminalSecondJob), default)).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct CombineWithoutCompleteSyncSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                _ = JobHandle.CombineDependencies(context.dependsOn, default);
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct CombineMissingInputSyncSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.CombineDependencies(
                    IJobExtensions.Schedule(default(CombinedTerminalFirstJob), default),
                    IJobExtensions.Schedule(default(CombinedTerminalSecondJob), default)).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct CombineArraySyncSystem : IUpdate {
            public Ent ent;
            public NativeArray<JobHandle> handles;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.CombineDependencies(this.handles).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct CombineSliceSyncSystem : IUpdate {
            public Ent ent;
            public NativeSlice<JobHandle> handles;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.CombineDependencies(this.handles).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public unsafe partial struct CombinePointerSyncSystem : IUpdate {
            public Ent ent;
            public JobHandle* handles;
            public int count;
            public void OnUpdate(ref SystemContext context) {
                JobHandleUnsafeUtility.CombineDependencies(this.handles, this.count).Complete();
                this.ent.Set(new TestComponent());
            }
        }

        [Test]
        public void HandleCombinationContractsRetainEveryArgumentCall() {
            var operations = ExternalValueOperations(nameof(ExternalHandleCombinations));
            var combinations = operations.Where(row => row[3].StartsWith("M:Unity.Jobs.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(5, combinations.Length);
            foreach (var call in combinations) CollectionAssert.Contains(call, "!ecs-leaf", call[3]);
            var arguments = operations.Where(row => row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.ExternalHandle", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(9, arguments.Length);
            foreach (var call in arguments) Assert.IsFalse(call.Contains("!ecs-leaf"), call[3]);
        }

        [Test]
        public void HandleCombinationSafetyIncludesArgumentEffectsWithoutIL() {
            var reader = CreateSafetyReader(() => Assert.Fail("Audited combinations and their arguments must use source."));
            var job = typeof(ExternalHandleEffectsJob);
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
            var expected = new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) }
                .Select(type => SafetyExceptionDependency(type, 2)).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [TestCase(nameof(OtherExternalHandleMembers))]
        [TestCase(nameof(UserHandleCombinationCall))]
        public void HandleCombinationContractsDoNotWhitelistCompletionOrLookalikes(string method) {
            var operations = ExternalValueOperations(method);
            Assert.IsTrue(operations.Length > 0);
            foreach (var call in operations) Assert.IsFalse(call.Contains("!ecs-leaf"), call[3]);
        }

        [TestCase(typeof(CombinedSchedulingSystem), true)]
        [TestCase(typeof(UnknownCombinedSchedulingSystem), false)]
        public void HandleCombinationDiscoveryRetainsScheduledArgumentsAndUnknownCallbacks(Type system, bool complete) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemScheduledJobs.v1").Select(attribute => attribute.Value.Split('\n'))
                .Single(row => row[0] == system.FullName);
            Assert.AreEqual(complete, rows[2] == "0", string.Join("\n", rows));
            Assert.AreEqual(2, rows.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            if (!complete) Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke:", StringComparison.Ordinal)));
        }

        [TestCase(typeof(CombineTwoSyncSystem), "proven")]
        [TestCase(typeof(CombineThreeSyncSystem), "proven")]
        [TestCase(typeof(CombineWithoutCompleteSyncSystem), "unproven")]
        [TestCase(typeof(CombineMissingInputSyncSystem), "unproven")]
        [TestCase(typeof(CombineArraySyncSystem), "incomplete")]
        [TestCase(typeof(CombineSliceSyncSystem), "incomplete")]
        [TestCase(typeof(CombinePointerSyncSystem), "incomplete")]
        public void HandleCombinationRequiresCompleteAndKnownInputCoverage(Type system, string status) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + status, string.Join("\n", rows));
            Assert.AreEqual(status != "incomplete", rows[2] == "0", string.Join("\n", rows));
        }
    }
}
