using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures: neither these lifecycle bodies nor their jobs are executed.
        public partial struct SyncProofCopiesSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) => SyncFlowCopies(ref context, in this.ent);
        }
        public partial struct SyncProofBranchSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) => SyncFlowBranch(ref context, this.condition, in this.ent);
        }
        public partial struct SyncProofStaleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) => SyncFlowReschedule(ref context, in this.ent);
        }
        public partial struct SyncProofDefaultSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                default(JobHandle).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofNewHandleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofCombinedSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var first = context.Query().Schedule<QueryModeJob, TestComponent>();
                var second = context.Query().Schedule<QueryModeJob, TestComponent>();
                JobHandle.CombineDependencies(first, second).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofOnlyOneBranchSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var first = context.Query().Schedule<QueryModeJob, TestComponent>();
                context.Query().Schedule<QueryModeJob, TestComponent>();
                first.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofGenericHandleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                SyncFlowGenericHandle(ref context);
                this.ent.Set(new TestComponent());
            }
        }
        public static void SyncFlowAssign(out JobHandle result, JobHandle value) => result = value;
        public partial struct SyncProofOutHandleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                SyncFlowAssign(out var copy, context.dependsOn);
                copy.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public static void SyncFlowReplace(ref SystemContext context, JobHandle value) => context.SetDependency(value);
        public partial struct SyncProofRefContextSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var work = context.Query().Schedule<QueryModeJob, TestComponent>();
                SyncFlowReplace(ref context, work);
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public static void SyncFlowReplaceCopy(SystemContext context, JobHandle value) => context.SetDependency(value);
        public partial struct SyncProofContextCopySystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var work = context.Query().Schedule<QueryModeJob, TestComponent>();
                SyncFlowReplaceCopy(context, work);
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofLateCompleteSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                this.ent.Set(new TestComponent());
                context.dependsOn.Complete();
            }
        }
        public partial struct SyncProofWaitQuerySystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.Query().WaitForAllJobs();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofGenericSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) => SyncFlowGeneric<T>(ref context, in this.ent);
        }
        public partial struct SyncProofExplicitSystem : IUpdate {
            public Ent ent;
            void IUpdate.OnUpdate(ref SystemContext context) {
                SyncFlowReturn(ref context).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncProofFinallySystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) => SyncFlowFinally(ref context, in this.ent);
        }
        public static Action syncProofUnknownCallback;
        public partial struct SyncProofUnknownSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => syncProofUnknownCallback();
        }
        public partial struct SyncProofLoopSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                while (this.repeat) {
                    context.dependsOn.Complete();
                    this.ent.Set(new TestComponent());
                }
            }
        }

        private static string[] SynchronizationSummary(Type system) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return system.Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == "ME.BECS.SystemSynchronization.v3" && attribute.Value != null)
            .Select(attribute => attribute.Value.Split('\n'))
            .Single(rows => rows[0] == (system.IsGenericType ? system.AssemblyQualifiedName : system.FullName));
        }

        private static bool ValidateSynchronizationRows(string[] rows) {
            var method = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("ValidateSynchronizationSummary", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)method.Invoke(null, new object[] { rows, null });
        }

        [TestCase(typeof(SyncProofCopiesSystem), "proven")]
        [TestCase(typeof(SyncProofBranchSystem), "unproven")]
        [TestCase(typeof(SyncProofStaleSystem), "unproven")]
        [TestCase(typeof(SyncProofDefaultSystem), "unproven")]
        [TestCase(typeof(SyncProofNewHandleSystem), "proven")]
        [TestCase(typeof(SyncProofCombinedSystem), "proven")]
        [TestCase(typeof(SyncProofOnlyOneBranchSystem), "unproven")]
        [TestCase(typeof(SyncProofGenericHandleSystem), "proven")]
        [TestCase(typeof(SyncProofOutHandleSystem), "proven")]
        [TestCase(typeof(SyncProofRefContextSystem), "proven")]
        [TestCase(typeof(SyncProofContextCopySystem), "unproven")]
        [TestCase(typeof(SyncProofLateCompleteSystem), "unproven")]
        [TestCase(typeof(SyncProofWaitQuerySystem), "proven")]
        [TestCase(typeof(SyncProofGenericSystem<AotMarker>), "proven")]
        [TestCase(typeof(SyncProofExplicitSystem), "proven")]
        [TestCase(typeof(SyncProofLoopSystem), "proven")]
        [TestCase(typeof(SyncProofFinallySystem), "unproven")]
        public void SourceSynchronizationProvesActualHandleCoverage(Type system, string status) {
            var rows = SynchronizationSummary(system);
            var diagnostic = string.Join("\n", rows);
            Assert.AreEqual("0", rows[2], diagnostic);
            CollectionAssert.Contains(rows, "S\t" + status, diagnostic);
            Assert.GreaterOrEqual(int.Parse(rows.Single(row => row.StartsWith("A\t", StringComparison.Ordinal)).Substring(2)), 1, diagnostic);
            var unproven = int.Parse(rows.Single(row => row.StartsWith("U\t", StringComparison.Ordinal)).Substring(2));
            if (status == "proven") Assert.AreEqual(0, unproven, diagnostic);
            else Assert.Greater(unproven, 0, diagnostic);
            Assert.IsTrue(ValidateSynchronizationRows(rows), diagnostic);
        }

        [TestCase(typeof(SyncProofUnknownSystem), "DelegateInvoke")]
        public void IncompleteSynchronizationNeverClaimsProof(Type system, string reason) {
            var rows = SynchronizationSummary(system);
            Assert.AreNotEqual("0", rows[2]);
            CollectionAssert.Contains(rows, "S\tincomplete");
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\t" + reason, StringComparison.Ordinal)), string.Join("\n", rows));
            Assert.IsTrue(ValidateSynchronizationRows(rows));
        }

        [Test]
        public void SynchronizationReportRejectsContradictoryOrDuplicateProofRecords() {
            var rows = SynchronizationSummary(typeof(SyncProofDefaultSystem));
            Assert.IsTrue(ValidateSynchronizationRows(rows));
            Assert.IsFalse(ValidateSynchronizationRows(rows.Select(row => row == "S\tunproven" ? "S\tproven" : row).ToArray()));
            Assert.IsFalse(ValidateSynchronizationRows(rows.Select(row => row == "U\t1" ? "U\t0" : row).ToArray()));
            Assert.IsFalse(ValidateSynchronizationRows(rows.Concat(new[] { "S\tunproven" }).ToArray()));
            Assert.IsFalse(ValidateSynchronizationRows(rows.Where(row => !row.StartsWith("E\t", StringComparison.Ordinal)).ToArray()));
            Assert.IsFalse(ValidateSynchronizationRows(rows.Where(row => !row.StartsWith("R\t", StringComparison.Ordinal)).ToArray()));
            Assert.IsFalse(ValidateSynchronizationRows(rows.Concat(new[] { "G\tunreported gap" }).ToArray()));
        }
    }
}
