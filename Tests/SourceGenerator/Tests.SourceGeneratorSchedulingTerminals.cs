using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Never scheduled by tests: these bodies intentionally have unknown dispatch.
        // Scheduling discovery must stop at the Unity terminal, not inspect Execute.
        public static Action SchedulingTerminalUnknownCallback;
        public partial struct UnitySingleTerminalJob : IJob { public void Execute() => SchedulingTerminalUnknownCallback(); }
        public partial struct UnityParallelTerminalJob : IJobParallelFor { public void Execute(int index) => SchedulingTerminalUnknownCallback(); }
        public partial struct UnityForTerminalJob : IJobFor { public void Execute(int index) => SchedulingTerminalUnknownCallback(); }
        public partial struct UnityBatchTerminalJob : IJobParallelForBatch { public void Execute(int index, int count) => SchedulingTerminalUnknownCallback(); }
        public partial struct UnityDeferTerminalJob : IJobParallelForDefer { public void Execute(int index) => SchedulingTerminalUnknownCallback(); }

        public unsafe partial struct UnitySchedulingTerminalsSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var single = default(UnitySingleTerminalJob);
                _ = IJobExtensions.Schedule(single, default);
                _ = IJobExtensions.ScheduleByRef(ref single, default);
                var parallel = default(UnityParallelTerminalJob);
                _ = IJobParallelForExtensions.Schedule(parallel, 0, 1, default);
                _ = IJobParallelForExtensions.ScheduleByRef(ref parallel, 0, 1, default);
                var jobFor = default(UnityForTerminalJob);
                _ = IJobForExtensions.Schedule(jobFor, 0, default);
                _ = IJobForExtensions.ScheduleByRef(ref jobFor, 0, default);
                _ = IJobForExtensions.ScheduleParallel(jobFor, 0, 1, default);
                _ = IJobForExtensions.ScheduleParallelByRef(ref jobFor, 0, 1, default);
                var batch = default(UnityBatchTerminalJob);
#pragma warning disable CS0618
                _ = IJobParallelForBatchExtensions.Schedule(batch, 0, 1, default);
                _ = IJobParallelForBatchExtensions.ScheduleByRef(ref batch, 0, 1, default);
                _ = IJobParallelForBatchExtensions.ScheduleBatch(batch, 0, 1, default);
                _ = IJobParallelForBatchExtensions.ScheduleBatchByRef(ref batch, 0, 1, default);
#pragma warning restore CS0618
                _ = IJobParallelForBatchExtensions.ScheduleParallel(batch, 0, 1, default);
                _ = IJobParallelForBatchExtensions.ScheduleParallelByRef(ref batch, 0, 1, default);
                var defer = default(UnityDeferTerminalJob);
                var list = default(Unity.Collections.NativeList<int>);
                _ = IJobParallelForDeferExtensions.Schedule(defer, list, 1, default);
                _ = IJobParallelForDeferExtensions.ScheduleByRef(ref defer, list, 1, default);
                var count = 0;
                _ = IJobParallelForDeferExtensions.Schedule(defer, &count, 1, default);
                _ = IJobParallelForDeferExtensions.ScheduleByRef(ref defer, &count, 1, default);
            }
        }

        public static class UserSchedulingTerminalNames {
            public static JobHandle ScheduleByRef<T>(ref T job) where T : struct, IJob => default;
            public static JobHandle ScheduleParallel<T>(T job) where T : struct, IJob => default;
            public static JobHandle ScheduleBatchByRef<T>(ref T job) where T : struct, IJob => default;
        }
        public partial struct UserSchedulingTerminalNamesSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var job = default(UnitySingleTerminalJob);
                _ = UserSchedulingTerminalNames.ScheduleByRef(ref job);
                _ = UserSchedulingTerminalNames.ScheduleParallel(job);
                _ = UserSchedulingTerminalNames.ScheduleBatchByRef(ref job);
            }
        }

        public static bool SchedulingFilterChoice;
        public static void ScheduleGenericFinally<T>(ref T job) where T : struct, IJob {
            try { if (SchedulingFilterChoice) return; }
            finally { _ = IJobExtensions.ScheduleByRef(ref job, default); }
        }
        public partial struct SchedulingFinallySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var job = default(UnitySingleTerminalJob);
                ScheduleGenericFinally(ref job);
            }
        }
        public static bool SchedulingExceptionFilter() {
            _ = IJobParallelForExtensions.Schedule(default(UnityParallelTerminalJob), 0, 1, default);
            return SchedulingFilterChoice;
        }
        public partial struct SchedulingExceptionUnionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try {
                    _ = IJobExtensions.Schedule(default(UnitySingleTerminalJob), default);
                    if (SchedulingFilterChoice) throw null;
                } catch (System.Exception) when (SchedulingExceptionFilter()) {
                    _ = IJobForExtensions.Schedule(default(UnityForTerminalJob), 0, default);
                } finally {
                    _ = IJobParallelForBatchExtensions.ScheduleParallel(default(UnityBatchTerminalJob), 0, 1, default);
                }
            }
        }
        public partial struct SchedulingUnknownCatchSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                try { if (SchedulingFilterChoice) throw null; }
                catch (System.Exception) { SchedulingTerminalUnknownCallback(); }
            }
        }

        [TestCase(typeof(SchedulingFinallySystem), 1)]
        [TestCase(typeof(SchedulingExceptionUnionSystem), 4)]
        [TestCase(typeof(SchedulingUnknownCatchSystem), -1)]
        public void ScheduledJobExceptionUnionIncludesAllRegionsWithoutCertifyingUnknownCallbacks(Type system, int expected) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var summary = system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemScheduledJobs.v1").Select(attribute => attribute.Value.Split('\n'))
                .Single(row => row[0] == system.FullName);
            if (expected < 0) {
                Assert.AreNotEqual("0", summary[2]);
                Assert.IsTrue(summary.Any(row => row.StartsWith("G\tDelegateInvoke:", StringComparison.Ordinal)), string.Join("\n", summary));
            } else {
                Assert.AreEqual("0", summary[2], string.Join("\n", summary));
                Assert.AreEqual(expected, summary.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            }
        }

        [Test]
        public void UnitySchedulingTerminalsIncludeByRefParallelBatchAndDeferredLists() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var assembly = typeof(UnitySchedulingTerminalsSystem).Assembly;
            var attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var method = attributes.Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value.StartsWith(
                "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.UnitySchedulingTerminalsSystem.OnUpdate(", StringComparison.Ordinal)).Value.Split('\n');
            CollectionAssert.Contains(method[1].Split(','), "schedule-schema=4");
            var calls = method.Skip(4).Select(row => row.Split('\t')).Where(row => row.Length > 3 &&
                row[3].StartsWith("M:Unity.Jobs.IJob", StringComparison.Ordinal) && row[3].Contains(".Schedule")).ToArray();
            Assert.AreEqual(18, calls.Length);
            foreach (var call in calls) {
                Assert.AreEqual(1, call.Count(token => token.StartsWith("!scheduled-job=", StringComparison.Ordinal)), call[3]);
                CollectionAssert.Contains(call, "!deferred-job-call", call[3]);
            }
            var summaries = attributes.Where(attribute => attribute.Key == "ME.BECS.SystemScheduledJobs.v1").Select(attribute => attribute.Value.Split('\n')).ToArray();
            var summary = summaries.Single(row => row[0] == typeof(UnitySchedulingTerminalsSystem).FullName);
            Assert.AreEqual("0", summary[2], string.Join("\n", summary));
            Assert.AreEqual(5, summary.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            var user = summaries.Single(row => row[0] == typeof(UserSchedulingTerminalNamesSystem).FullName);
            Assert.AreEqual("0", user[2], string.Join("\n", user));
            Assert.IsFalse(user.Any(row => row.StartsWith("J\t", StringComparison.Ordinal)));
        }

        [TestCase(typeof(IJobExtensions), typeof(UnitySingleTerminalJob))]
        [TestCase(typeof(IJobParallelForExtensions), typeof(UnityParallelTerminalJob))]
        [TestCase(typeof(IJobForExtensions), typeof(UnityForTerminalJob))]
        [TestCase(typeof(IJobParallelForBatchExtensions), typeof(UnityBatchTerminalJob))]
        [TestCase(typeof(IJobParallelForDeferExtensions), typeof(UnityDeferTerminalJob))]
        public void SchedulingReflectionOracleRecognizesTheSameUnityTerminals(Type owner, Type job) {
            var classifier = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("IsSchedulingMethod", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var definition in owner.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method =>
                         method.IsGenericMethodDefinition && (method.Name.StartsWith("Schedule", StringComparison.Ordinal) || method.Name.StartsWith("Run", StringComparison.Ordinal)))) {
                var arguments = definition.GetGenericArguments().Select((_, index) => index == 0 ? job : typeof(int)).ToArray();
                Assert.AreEqual(definition.Name.StartsWith("Schedule", StringComparison.Ordinal),
                    classifier.Invoke(null, new object[] { definition.MakeGenericMethod(arguments) }), definition.ToString());
            }
            foreach (var definition in typeof(UserSchedulingTerminalNames).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.IsGenericMethodDefinition))
                Assert.IsFalse((bool)classifier.Invoke(null, new object[] { definition.MakeGenericMethod(typeof(UnitySingleTerminalJob)) }));
        }
    }
}
