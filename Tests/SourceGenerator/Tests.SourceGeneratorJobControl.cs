using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures. Never execute a lifecycle, job, callback or handle.
        public static JobHandle ControlReceiver(in Ent ent) { ent.Set(new TestComponent()); return default; }
        public static void ControlApiOperations(in Ent ent, ref JobHandle first, ref JobHandle second, ref JobHandle third, NativeArray<JobHandle> handles) {
            ControlReceiver(in ent).Complete();
            JobHandle.CompleteAll(ref first, ref second);
            JobHandle.CompleteAll(ref first, ref second, ref third);
            JobHandle.CompleteAll(handles);
            _ = first.IsCompleted;
            JobHandle.ScheduleBatchedJobs();
        }
        public static void ControlGeneric<T>(ref SystemContext context, in Ent ent) where T : unmanaged, IComponent {
            context.dependsOn.Complete();
            ent.Get<T>();
        }
        public partial struct ControlGenericSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) => ControlGeneric<TestComponent>(ref context, in this.ent);
        }
        public partial struct ControlFirstJob : IJob {
            public Ent ent;
            public void Execute() => this.ent.Set(new Test1Component());
        }
        public partial struct ControlSecondJob : IJob {
            public Ent ent;
            public void Execute() => this.ent.Set(new Test2Component());
        }
        public partial struct ControlCompleteAllSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var input = context.dependsOn;
                var first = IJobExtensions.Schedule(default(ControlFirstJob), default);
                var second = IJobExtensions.Schedule(default(ControlSecondJob), default);
                JobHandle.CompleteAll(ref input, ref first, ref second);
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct ControlIncompleteAllSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var input = context.dependsOn;
                var first = IJobExtensions.Schedule(default(ControlFirstJob), default);
                _ = IJobExtensions.Schedule(default(ControlSecondJob), default);
                JobHandle.CompleteAll(ref input, ref first);
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct ControlAliasedAllSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var input = context.dependsOn;
                JobHandle.CompleteAll(ref input, ref input);
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct ControlPollSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                if (context.dependsOn.IsCompleted) this.ent.Set(new TestComponent());
            }
        }
        public partial struct ControlFlushSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.ScheduleBatchedJobs();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct ControlArraySystem : IUpdate {
            public Ent ent;
            public NativeArray<JobHandle> handles;
            public void OnUpdate(ref SystemContext context) {
                JobHandle.CompleteAll(this.handles);
                this.ent.Set(new TestComponent());
            }
        }
        public static Func<JobHandle> UnknownControlReceiver;
        public partial struct ControlUnknownReceiverSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => UnknownControlReceiver().Complete();
        }
        public struct UserControlHandle {
            public void Complete() => UnknownControlReceiver();
        }
        public static void UserControlApi() => default(UserControlHandle).Complete();
        public partial struct ControlUnknownUserSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => UserControlApi();
        }
        public partial struct ControlEscapedMethodSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                Action callback = context.dependsOn.Complete;
                callback();
            }
        }
        public partial struct ControlInsideJob : IJob {
            public Ent ent;
            public void Execute() {
                default(JobHandle).Complete();
                this.ent.Set(new TestComponent());
            }
        }

        private static string[] ControlSummary(Type type, string key) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return type.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key &&
                attribute.Value.StartsWith((type.IsGenericType ? type.AssemblyQualifiedName : type.FullName) + "\n", StringComparison.Ordinal)).Value.Split('\n');
        }

        [Test]
        public void JobControlIsNotAnEcsLeafAndRetainsReceiverEffects() {
            var operations = ExternalValueOperations(nameof(ControlApiOperations));
            var controls = operations.Where(row => row[3].StartsWith("M:Unity.Jobs.JobHandle.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(6, controls.Length);
            CollectionAssert.AreEquivalent(new[] { "!job-control=complete", "!job-control=complete-all", "!job-control=complete-all",
                "!job-control=complete-array", "!job-control=poll", "!job-control=flush" },
                controls.Select(row => row.Single(token => token.StartsWith("!job-control=", StringComparison.Ordinal))).ToArray());
            foreach (var row in controls) Assert.IsFalse(row.Contains("!ecs-leaf"));
            var receiver = operations.Single(row => row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.ControlReceiver(", StringComparison.Ordinal));
            Assert.IsFalse(receiver.Any(token => token.StartsWith("!job-control=", StringComparison.Ordinal) || token == "!ecs-leaf"));
            foreach (var row in ExternalValueOperations(nameof(UserControlApi)))
                Assert.IsFalse(row.Any(token => token.StartsWith("!job-control=", StringComparison.Ordinal)));
        }

        [TestCase(typeof(ControlGenericSystem), "proven", 0)]
        [TestCase(typeof(ControlCompleteAllSystem), "proven", 2)]
        [TestCase(typeof(ControlIncompleteAllSystem), "unproven", 2)]
        [TestCase(typeof(ControlAliasedAllSystem), "proven", 0)]
        [TestCase(typeof(ControlPollSystem), "unproven", 0)]
        [TestCase(typeof(ControlFlushSystem), "unproven", 0)]
        [TestCase(typeof(ControlArraySystem), "incomplete", 0)]
        public void JobControlSeparatesSubmissionCoverageFromCompletionProof(Type system, string synchronization, int jobs) {
            var scheduled = ControlSummary(system, "ME.BECS.SystemScheduledJobs.v1");
            var direct = ControlSummary(system, "ME.BECS.SystemDirectAccess.v1");
            var modes = ControlSummary(system, "ME.BECS.SystemScheduleModes.v1");
            Assert.AreEqual("0", scheduled[2], string.Join("\n", scheduled));
            Assert.AreEqual(jobs, scheduled.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
            Assert.AreEqual("0", direct[2], string.Join("\n", direct));
            CollectionAssert.AreEqual(new[] { SafetyExceptionDependency(typeof(TestComponent), 2) },
                direct.Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).ToArray());
            Assert.AreEqual("0", modes[2], string.Join("\n", modes));
            var dependencies = SystemDependencyRows(system);
            Assert.AreEqual("0", dependencies[2], string.Join("\n", dependencies));
            var expected = (jobs == 0 ? new[] { typeof(TestComponent) } :
                new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component) })
                .Select(type => SystemComponent(2, type)).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, dependencies.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).OrderBy(row => row, StringComparer.Ordinal).ToArray());
            var sync = SynchronizationSummary(system);
            CollectionAssert.Contains(sync, "S\t" + synchronization, string.Join("\n", sync));
            Assert.AreEqual(synchronization != "incomplete", sync[2] == "0");
            var fallbackCalls = 0;
            var selected = DependencySelector(system, () => ++fallbackCalls, out _);
            Assert.AreEqual(synchronization == "incomplete" ? 1 : 0, fallbackCalls);
            CollectionAssert.AreEqual(expected, SelectedDependencyRows(selected));
        }

        [TestCase(typeof(ControlUnknownReceiverSystem))]
        [TestCase(typeof(ControlUnknownUserSystem))]
        [TestCase(typeof(ControlEscapedMethodSystem))]
        public void JobControlCannotHideCallbacksLookalikesOrEscapedDelegates(Type system) {
            foreach (var key in new[] { "ME.BECS.SystemScheduledJobs.v1", "ME.BECS.SystemDirectAccess.v1", "ME.BECS.SystemDependencies.v1", "ME.BECS.SystemSynchronization.v3" }) {
                var rows = ControlSummary(system, key);
                Assert.AreNotEqual("0", rows[2], key + "\n" + string.Join("\n", rows));
            }
        }

        [Test]
        public void JobControlScopeDoesNotCertifyJobSafetyEntityCountsOrWeights() {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(ControlInsideJob), key);
                Assert.AreNotEqual("0", rows[2], key);
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)), key);
            }
        }
    }
}
