using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures: no simulation or job execution in these tests.
        public static int SyncAddressScheduleAndComplete(ref SystemContext context) {
            context.Query().Schedule<QueryModeJob, TestComponent>().Complete();
            return 1;
        }
        public static ref int SyncAddressRef(in Ent ent) => ref ent.Get<TestComponent>().data;
        public static ref T SyncAddressGeneric<T>(in Ent ent) where T : unmanaged, IComponent => ref ent.Get<T>();
        public static ref readonly T SyncAddressReadonly<T>(in Ent ent) where T : unmanaged, IComponent => ref ent.Get<T>();
        public static ref T SyncAddressIdentity<T>(ref T value) => ref value;
        public static T SyncAddressRead<T>(ref T value) => SyncAddressIdentity(ref value);
        public static void SyncAddressStore(ref int target, int value) => target = value;
        public static void SyncAddressCapturedStore(ref SystemContext context, in Ent ent, bool condition) =>
            SyncAddressRef(in ent) = condition ? SyncCaptureSchedule(ref context) : SyncCaptureSchedule(ref context);

        public struct SyncAddressHolder {
            public Ent ent;
        }

        public partial struct SyncAddressCompletedRhsSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                this.ent.Get<TestComponent>().data = this.condition ? SyncAddressScheduleAndComplete(ref context) : 0;
            }
        }
        public partial struct SyncAddressRefResultSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncAddressCapturedStore(ref context, in this.ent, this.condition);
            }
        }
        public partial struct SyncAddressGenericResultSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncAddressGeneric<TestComponent>(in this.ent).data = this.condition ? SyncCaptureSchedule(ref context) : 0;
            }
        }
        public partial struct SyncAddressGenericCompletedSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncAddressGeneric<TestComponent>(in this.ent).data = this.condition ? SyncAddressScheduleAndComplete(ref context) : 0;
            }
        }
        public partial struct SyncAddressPropertySystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var holder = default(SyncAddressHolder);
                holder.ent = this.ent;
                holder.ent.Get<TestComponent>().data = this.condition ? SyncCaptureSchedule(ref context) : 0;
            }
        }
        public partial struct SyncAddressArgumentSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncAddressStore(ref SyncAddressRef(in this.ent), this.condition ? SyncCaptureSchedule(ref context) : 0);
            }
        }
        public partial struct SyncAddressArgumentCompletedSystem : IUpdate {
            public Ent ent;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                SyncAddressStore(ref SyncAddressRef(in this.ent), this.condition ? SyncAddressScheduleAndComplete(ref context) : 0);
            }
        }
        public partial struct SyncAddressChoiceSystem : IUpdate {
            public Ent first, second;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                (this.condition ? ref this.first.Get<TestComponent>().data : ref this.second.Get<TestComponent>().data) = SyncCaptureSchedule(ref context);
            }
        }
        public partial struct SyncAddressChoiceCompletedSystem : IUpdate {
            public Ent first, second;
            public bool condition;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                (this.condition ? ref this.first.Get<TestComponent>().data : ref this.second.Get<TestComponent>().data) = SyncAddressScheduleAndComplete(ref context);
            }
        }
        public partial struct SyncAddressGenericHandleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var handle = context.dependsOn;
                SyncAddressRead(ref handle).Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct SyncAddressReadonlySystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var value = SyncAddressReadonly<TestComponent>(in this.ent).data;
            }
        }
        public partial struct SyncAddressReadonlyCompletedSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                var value = SyncAddressReadonly<TestComponent>(in this.ent).data;
            }
        }

        [TestCase(typeof(SyncAddressCompletedRhsSystem), "proven")]
        [TestCase(typeof(SyncAddressRefResultSystem), "unproven")]
        [TestCase(typeof(SyncAddressGenericResultSystem), "unproven")]
        [TestCase(typeof(SyncAddressGenericCompletedSystem), "proven")]
        [TestCase(typeof(SyncAddressPropertySystem), "unproven")]
        [TestCase(typeof(SyncAddressArgumentSystem), "unproven")]
        [TestCase(typeof(SyncAddressArgumentCompletedSystem), "proven")]
        [TestCase(typeof(SyncAddressChoiceSystem), "unproven")]
        [TestCase(typeof(SyncAddressChoiceCompletedSystem), "proven")]
        [TestCase(typeof(SyncAddressReadonlySystem), "unproven")]
        [TestCase(typeof(SyncAddressReadonlyCompletedSystem), "proven")]
        public void BorrowedAddressUsesRemainOrderedAfterTheirAcquisition(Type system, string status) {
            SourceSynchronizationProvesActualHandleCoverage(system, status);
        }

        [TestCase(typeof(SyncAddressGenericHandleSystem), "RefReturn")]
        public void OpenGenericRefReturnsCannotHideClosedHandleAliases(Type system, string reason) {
            IncompleteSynchronizationNeverClaimsProof(system, reason);
        }

        [TestCase(nameof(SyncAddressRef), "U", "ref")]
        [TestCase(nameof(SyncAddressGeneric), "T", "ref")]
        [TestCase(nameof(SyncAddressIdentity), "T", "ref")]
        [TestCase(nameof(SyncAddressReadonly), "T", "in")]
        public void DataRefReturnProgramsRetainTheirReturnModeAndPortableType(string method, string role, string passing) {
            var rows = SynchronizationRows(method);
            CollectionAssert.IsEmpty(rows.Where(row => row[0] == "G").ToArray());
            var result = rows.Single(row => row[0] == "M");
            Assert.AreEqual(role, result[1]);
            Assert.AreEqual(passing, result[2]);
        }

        [Test]
        public void CapturedAddressEvaluatesItsGetterOnceButRecordsTheLaterStore() {
            var rows = SynchronizationRows(nameof(SyncAddressCapturedStore));
            var contracts = SynchronizationContracts(rows);
            CollectionAssert.IsEmpty(rows.Where(row => row[0] == "G").ToArray());
            var getters = rows.Where(row => row[0] == "C" && SynchronizationCallName(row, contracts).Contains(".SyncAddressRef(")).ToArray();
            Assert.AreEqual(1, getters.Length, "The address getter must not be evaluated again when the captured address is used.");
            var schedules = rows.Where(row => row[0] == "C" && SynchronizationCallName(row, contracts).Contains(".SyncCaptureSchedule(")).ToArray();
            Assert.AreEqual(2, schedules.Length);
            Assert.Less(Array.IndexOf(rows, getters[0]), Array.IndexOf(rows, schedules[0]));
            Assert.Greater(Array.FindLastIndex(rows, row => row[0] == "A"), Array.IndexOf(rows, schedules[1]));
        }
    }
}
