using System;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct SyncLoopChainSystem : IUpdate {
            public Ent ent;
            public int count;
            public void OnUpdate(ref SystemContext context) {
                for (var index = 0; index < this.count; ++index)
                    context.SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncLoopStaleSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                var old = context.dependsOn;
                while (this.repeat) context.SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
                old.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncLoopIndependentSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                var latest = context.dependsOn;
                while (this.repeat) latest = context.Query().Schedule<QueryModeJob, TestComponent>();
                latest.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncLoopCombinedSystem : IUpdate {
            public Ent ent;
            public int count;
            public void OnUpdate(ref SystemContext context) {
                var combined = context.dependsOn;
                for (var index = 0; index < this.count; index += 1) {
                    var work = context.Query().Schedule<QueryModeJob, TestComponent>();
                    combined = JobHandle.CombineDependencies(combined, work);
                }
                combined.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncLoopCompleteEachSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                while (this.repeat) context.Query().Schedule<QueryModeJob, TestComponent>().Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncLoopMaySkipCompleteSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                while (this.repeat) context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncLoopDoWhileSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                do { context.dependsOn.Complete(); } while (this.repeat);
                this.ent.Set(new TestComponent());
            }
        }
        public static void SyncLoopAdvance(ref SystemContext context) =>
            context.SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
        public partial struct SyncLoopHelperChainSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                while (this.repeat) SyncLoopAdvance(ref context);
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public static JobHandle SyncLoopReturnNew(ref SystemContext context) =>
            context.Query().Schedule<QueryModeJob, TestComponent>();
        public partial struct SyncLoopReturnedIndependentSystem : IUpdate {
            public Ent ent;
            public bool repeat;
            public void OnUpdate(ref SystemContext context) {
                var latest = context.dependsOn;
                while (this.repeat) latest = SyncLoopReturnNew(ref context);
                latest.Complete();
                this.ent.Set(new TestComponent());
            }
        }

        public static void SyncBorrowedWrite(ref SystemContext context, ref TestComponent component) {
            context.Query().Schedule<QueryModeJob, TestComponent>();
            component = default;
        }
        public partial struct SyncBorrowedWriteSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncBorrowedWrite(ref context, ref this.ent.Get<TestComponent>());
            }
        }
        public static int SyncScheduleValue(ref SystemContext context) {
            context.Query().Schedule<QueryModeJob, TestComponent>();
            return 1;
        }
        public static void SyncBorrowedScalarWrite(ref SystemContext context, ref int value) => value = SyncScheduleValue(ref context);
        public partial struct SyncBorrowedScalarSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncBorrowedScalarWrite(ref context, ref this.ent.Get<TestComponent>().data);
            }
        }
        public partial struct SyncLvalueBeforeScheduleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                this.ent.Get<TestComponent>().data = SyncScheduleValue(ref context);
            }
        }

        [TestCase(typeof(SyncLoopChainSystem), "proven")]
        [TestCase(typeof(SyncLoopStaleSystem), "unproven")]
        [TestCase(typeof(SyncLoopIndependentSystem), "unproven")]
        [TestCase(typeof(SyncLoopCombinedSystem), "proven")]
        [TestCase(typeof(SyncLoopCompleteEachSystem), "proven")]
        [TestCase(typeof(SyncLoopMaySkipCompleteSystem), "unproven")]
        [TestCase(typeof(SyncLoopDoWhileSystem), "proven")]
        [TestCase(typeof(SyncLoopHelperChainSystem), "proven")]
        [TestCase(typeof(SyncLoopReturnedIndependentSystem), "unproven")]
        [TestCase(typeof(SyncBorrowedWriteSystem), "unproven")]
        [TestCase(typeof(SyncBorrowedScalarSystem), "unproven")]
        [TestCase(typeof(SyncLvalueBeforeScheduleSystem), "unproven")]
        public void SynchronizationLoopsAndBorrowedWritesKeepOutstandingWork(Type system, string status) {
            var rows = SynchronizationSummary(system);
            var diagnostic = string.Join("\n", rows);
            Assert.AreEqual("0", rows[2], diagnostic);
            CollectionAssert.Contains(rows, "S\t" + status, diagnostic);
            Assert.IsTrue(ValidateSynchronizationRows(rows), diagnostic);
        }
    }
}
