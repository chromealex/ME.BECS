using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures. Capturing a receiver address must not copy it; a
        // by-value argument must snapshot it before later argument side effects.
        public static int SyncCaptureReplace(ref JobHandle handle, ref SystemContext context) {
            handle = context.Query().Schedule<QueryModeJob, TestComponent>();
            return 0;
        }
        public static void SyncCaptureCompleteValue(JobHandle handle, int unused) => handle.Complete();
        public static void SyncCaptureCompleteRef(ref JobHandle handle, int unused) => handle.Complete();
        public static void SyncCaptureValueArgument(ref SystemContext context, bool condition) {
            var handle = context.dependsOn;
            SyncCaptureCompleteValue(handle, condition ? SyncCaptureReplace(ref handle, ref context) : SyncCaptureReplace(ref handle, ref context));
        }
        public static void SyncCaptureRefArgument(ref SystemContext context, bool condition) {
            var handle = context.dependsOn;
            SyncCaptureCompleteRef(ref handle, condition ? SyncCaptureReplace(ref handle, ref context) : SyncCaptureReplace(ref handle, ref context));
        }
        public static SystemContext SyncCaptureContextCopy(SystemContext context) => context;
        public static SystemContext SyncCaptureSwitchValue(bool condition, SystemContext context) =>
            condition switch { true => context, _ => context };
        public static void SyncCaptureReadonlyReceiver(in SystemContext context, bool condition) =>
            context.SetDependency(condition ? context.dependsOn : default);
        public static void SyncCaptureRefChoice(bool condition, ref JobHandle first, ref JobHandle second) =>
            (condition ? ref first : ref second).Complete();
        public static int SyncCaptureSchedule(ref SystemContext context) {
            context.SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
            return 0;
        }
        public static void SyncCaptureScalarStore(ref SystemContext context, ref int value, bool condition) =>
            value = condition ? SyncCaptureSchedule(ref context) : SyncCaptureSchedule(ref context);

        public partial struct SyncCaptureConditionalSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                (this.condition ? context.dependsOn : SyncFlowReturn(ref context)).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureConditionalDefaultSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                (this.condition ? context.dependsOn : default(JobHandle)).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureNestedSystem : IUpdate {
            public Ent ent;
            public bool first, second;
            public void OnUpdate(ref SystemContext context) {
                (this.first ? (this.second ? context.dependsOn : context.dependsOn) : context.dependsOn).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureValueArgumentSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                SyncCaptureValueArgument(ref context, this.condition);
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureRefArgumentSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                SyncCaptureRefArgument(ref context, this.condition);
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureReceiverSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.SetDependency(this.condition ? context.Query().Schedule<QueryModeJob, TestComponent>() :
                    context.Query().Schedule<QueryModeJob, TestComponent>());
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureTemporaryReceiverSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                SyncCaptureContextCopy(context).SetDependency(this.condition ? context.Query().Schedule<QueryModeJob, TestComponent>() :
                    context.Query().Schedule<QueryModeJob, TestComponent>());
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureConditionalReceiverSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                (this.condition ? context : context).SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureBorrowedStoreSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncCaptureScalarStore(ref context, ref this.ent.Get<TestComponent>().data, this.condition);
            }
        }
        public partial struct SyncCaptureSwitchReceiverSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                (this.condition switch { true => context, _ => context }).SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncCaptureFieldStoreSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                this.ent.Get<TestComponent>().data = this.condition ? SyncCaptureSchedule(ref context) : SyncCaptureSchedule(ref context);
            }
        }
        public partial struct SyncCaptureReadonlySystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                SyncCaptureReadonlyReceiver(in context, this.condition);
                this.ent.Set(new TestComponent());
            }
        }

        [TestCase(typeof(SyncCaptureConditionalSystem), "proven")]
        [TestCase(typeof(SyncCaptureConditionalDefaultSystem), "unproven")]
        [TestCase(typeof(SyncCaptureNestedSystem), "proven")]
        [TestCase(typeof(SyncCaptureValueArgumentSystem), "unproven")]
        [TestCase(typeof(SyncCaptureRefArgumentSystem), "proven")]
        [TestCase(typeof(SyncCaptureReceiverSystem), "proven")]
        [TestCase(typeof(SyncCaptureTemporaryReceiverSystem), "unproven")]
        [TestCase(typeof(SyncCaptureConditionalReceiverSystem), "unproven")]
        [TestCase(typeof(SyncCaptureSwitchReceiverSystem), "unproven")]
        [TestCase(typeof(SyncCaptureBorrowedStoreSystem), "unproven")]
        [TestCase(typeof(SyncCaptureFieldStoreSystem), "unproven")]
        public void SynchronizationCapturesPreserveValueAndAddressSemantics(Type system, string status) {
            SourceSynchronizationProvesActualHandleCoverage(system, status);
        }

        [TestCase(typeof(SyncCaptureReadonlySystem))]
        public void UnknownCaptureAddressesCannotCertifySynchronization(Type system) {
            IncompleteSynchronizationNeverClaimsProof(system, "FlowCaptureStorage");
        }

        [TestCase(nameof(SyncCaptureValueArgument), ".SyncCaptureCompleteValue(", false)]
        [TestCase(nameof(SyncCaptureRefArgument), ".SyncCaptureCompleteRef(", true)]
        public void CapturedCallArgumentKeepsOrCopiesItsOriginalStorage(string method, string target, bool aliases) {
            var rows = SynchronizationRows(method);
            var contracts = SynchronizationContracts(rows);
            var replacements = rows.Where(row => row[0] == "C" &&
                SynchronizationCallName(row, contracts).Contains(".SyncCaptureReplace(")).ToArray();
            Assert.AreEqual(2, replacements.Length);
            var replacedSlot = replacements[0][5].Split(':')[2];
            Assert.AreEqual(replacedSlot, replacements[1][5].Split(':')[2]);
            var call = rows.Single(row => row[0] == "C" && SynchronizationCallName(row, contracts).Contains(target));
            var argument = call[5].Split(':');
            Assert.AreEqual(aliases ? "ref" : "value", argument[1]);
            Assert.AreEqual(aliases, replacedSlot == argument[2], "A ref operand retains the original cell; a value operand is a snapshot.");
            if (!aliases) {
                // Trace the snapshot back to storage: it must have been taken before
                // either conditional replacement, not lazily loaded at the final call.
                var slot = argument[2];
                var firstReplacement = Array.IndexOf(rows, replacements[0]);
                var index = Array.IndexOf(rows, call);
                while (slot != replacedSlot) {
                    index = Array.FindLastIndex(rows, index - 1, row => row[0] == "=" && row[1] == slot);
                    Assert.GreaterOrEqual(index, 0);
                    slot = rows[index][2];
                }
                Assert.Less(index, firstReplacement);
            }
        }

        [Test]
        public void SwitchFailurePathHasAnExactFrameworkLeafContract() {
            var rows = SynchronizationRows(nameof(SyncCaptureSwitchValue));
            CollectionAssert.IsEmpty(rows.Where(row => row[0] == "G").ToArray());
            var calls = SynchronizationContracts(rows).Values.Where(contract => contract[3] ==
                "M:System.Runtime.CompilerServices.SwitchExpressionException.#ctor").ToArray();
            Assert.AreEqual(1, calls.Length);
            CollectionAssert.Contains(calls[0], "!ecs-leaf");
        }

        [Test]
        public void CapturedBorrowedScalarStoreIsRecordedAfterConditionalScheduling() {
            var rows = SynchronizationRows(nameof(SyncCaptureScalarStore));
            var contracts = SynchronizationContracts(rows);
            var schedule = Array.FindLastIndex(rows, row => row[0] == "C" &&
                SynchronizationCallName(row, contracts).Contains(".SyncCaptureSchedule("));
            Assert.GreaterOrEqual(schedule, 0);
            Assert.Greater(Array.FindLastIndex(rows, row => row[0] == "A"), schedule,
                "The captured lvalue is still borrowed storage when the RHS has scheduled new work.");
        }
    }
}
