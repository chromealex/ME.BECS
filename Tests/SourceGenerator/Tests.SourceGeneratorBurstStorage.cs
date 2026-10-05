using System;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures: never allocate shared storage or run these jobs.
        public sealed class BurstStorageContext {
            public static readonly Ent created = Ent.New();
            public BurstStorageContext() { default(Ent).Set(new Test3Component()); }
            public override int GetHashCode() { default(Ent).Set(new Test3Component()); return 1; }
            public override string ToString() { default(Ent).Set(new Test3Component()); return "context"; }
        }
        public static void BurstStorageOperations() {
            _ = SharedStatic<int>.GetOrCreate<BurstStorageContext>();
            _ = SharedStatic<int>.GetOrCreate<BurstStorageContext, TestComponent>(16);
            _ = SharedStatic<int>.GetOrCreatePartiallyUnsafeWithHashCode<BurstStorageContext>(16, 1);
            _ = SharedStatic<int>.GetOrCreatePartiallyUnsafeWithSubHashCode<BurstStorageContext>(16, 2);
            _ = SharedStatic<int>.GetOrCreateUnsafe(16, 1, 2);
            _ = BurstRuntime.GetHashCode32<BurstStorageContext>();
            _ = BurstRuntime.GetHashCode64<BurstStorageContext>();
            _ = SharedStatic<int>.GetOrCreate(typeof(BurstStorageContext));
            _ = SharedStatic<int>.GetOrCreate(subContextType: typeof(TestComponent), contextType: typeof(BurstStorageContext), alignment: 16);
            _ = BurstRuntime.GetHashCode32(typeof(BurstStorageContext));
            _ = BurstRuntime.GetHashCode64((Type)(object)typeof(BurstStorageContext));
        }
        public static void BurstStorageMixedTypeArguments(Type unknown) {
            _ = SharedStatic<int>.GetOrCreate(typeof(BurstStorageContext));
            _ = SharedStatic<int>.GetOrCreate(unknown);
            _ = SharedStatic<int>.GetOrCreate(typeof(BurstStorageContext), unknown);
            _ = SharedStatic<int>.GetOrCreate(unknown, typeof(BurstStorageContext));
            _ = BurstRuntime.GetHashCode32(typeof(BurstStorageContext));
            _ = BurstRuntime.GetHashCode32(unknown);
            _ = BurstRuntime.GetHashCode64(typeof(BurstStorageContext));
            _ = BurstRuntime.GetHashCode64(unknown);
        }
        public static uint BurstStorageAlignment(in Ent ent) { ent.Set(new TestComponent()); Ent.New(); return 16; }
        public static void BurstStorageArguments(in Ent ent) {
            _ = SharedStatic<int>.GetOrCreate<BurstStorageContext>(BurstStorageAlignment(in ent));
            _ = SharedStatic<int>.GetOrCreate(contextType: typeof(BurstStorageContext), alignment: BurstStorageAlignment(in ent));
        }
        public static T BurstStorageRead<T>() where T : unmanaged => SharedStatic<T>.GetOrCreate<BurstStorageContext, T>().Data;
        public static unsafe void BurstStoragePointerOnly() => _ = SharedStatic<Test2Component>.GetOrCreate<BurstStorageContext>().UnsafeDataPointer;
        public partial struct BurstStorageJob : IJob {
            public void Execute() { BurstStorageOperations(); BurstStoragePointerOnly(); }
        }
        public partial struct BurstStorageArgumentsJob : IJob {
            public Ent ent;
            public void Execute() => BurstStorageArguments(in this.ent);
        }
        public partial struct BurstStorageComponentJob : IJob {
            public void Execute() => _ = BurstStorageRead<Test1Component>();
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct BurstStorageJob : IJob {
                public void Execute() => _ = BurstStorageRead<T>();
            }
        }
        public partial struct BurstStorageUnknownTypeJob : IJob {
            public static Type contextType;
            public void Execute() => BurstStorageMixedTypeArguments(contextType);
        }
        public struct UserBurstStorage<T> where T : struct {
            public static UserBurstStorage<T> GetOrCreate<TContext>(uint alignment = 0) {
                default(Ent).Set(new Test2Component()); Ent.New(); return default;
            }
        }
        public partial struct BurstStorageLookalikeJob : IJob {
            public void Execute() => _ = UserBurstStorage<int>.GetOrCreate<BurstStorageContext>();
        }
        public static Type BurstStorageUserType() { default(Ent).Set(new Test3Component()); return typeof(BurstStorageContext); }
        public static void BurstStorageTypeFactory() => _ = BurstRuntime.GetHashCode64(BurstStorageUserType());

        public static uint BurstStorageScheduleArgument() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 16; }
        public partial struct BurstStorageOnlySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => BurstStorageOperations();
        }
        public partial struct BurstStorageBeforeCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { _ = BurstStorageRead<TestComponent>(); context.dependsOn.Complete(); }
        }
        public partial struct BurstStorageAfterCompleteSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); _ = BurstStorageRead<TestComponent>(); }
        }
        public partial struct BurstStorageLateArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = SharedStatic<TestComponent>.GetOrCreate<BurstStorageContext>(BurstStorageScheduleArgument()).Data;
            }
        }
        public partial struct BurstStorageReflectedLateArgumentSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = SharedStatic<TestComponent>.GetOrCreate(alignment: BurstStorageScheduleArgument(), contextType: typeof(BurstStorageContext)).Data;
            }
        }
        public partial struct BurstStorageUnknownHandleSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                SharedStatic<JobHandle>.GetOrCreate<BurstStorageContext>().Data.Complete();
                default(Ent).Set(new TestComponent());
            }
        }
        public partial struct BurstStorageUnknownTypeSystem : IUpdate {
            public static Type contextType;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = BurstRuntime.GetHashCode64(contextType);
            }
        }

        [Test]
        public void BurstStorageContractsAreExactAndTypeofIsCallSiteSpecific() {
            var operations = ExternalValueOperations(nameof(BurstStorageOperations));
            Assert.AreEqual(11, operations.Length);
            Assert.IsTrue(operations.All(row => row.Contains("!ecs-leaf")));
            Assert.AreEqual(5, operations.Count(row => row.Contains("!burst-storage=1")));
            Assert.AreEqual(2, operations.Count(row => row.Contains("!burst-type-hash=1")));
            Assert.AreEqual(4, operations.Count(row => row.Contains("!burst-typeof=1")));
            var mixed = ExternalValueOperations(nameof(BurstStorageMixedTypeArguments));
            Assert.AreEqual(8, mixed.Length);
            Assert.AreEqual(3, mixed.Count(row => row.Contains("!ecs-leaf")));
            Assert.IsFalse(ExternalValueOperations(nameof(BurstStorageTypeFactory)).Any(row => row.Contains("!ecs-leaf")),
                "A user factory returning Type has not proved the runtime receiver of its virtual metadata getters.");
            var args = ExternalValueOperations(nameof(BurstStorageArguments));
            Assert.AreEqual(4, args.Length);
            Assert.AreEqual(2, args.Count(row => row.Contains("!ecs-leaf")));
        }

        [TestCase(typeof(BurstStorageJob), null, 0, 0)]
        // Weights count unique reachable helper bodies, unlike entity multiplicity.
        [TestCase(typeof(BurstStorageArgumentsJob), typeof(TestComponent), 2, 12)]
        [TestCase(typeof(BurstStorageComponentJob), typeof(Test1Component), 0, 0)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.BurstStorageJob), typeof(AotMarker), 0, 0)]
        [TestCase(typeof(BurstStorageLookalikeJob), typeof(Test2Component), 1, 12)]
        public void BurstStorageUsesSourceAndRetainsOnlyActualEffects(Type job, Type component, int creations, int weight) {
            var reader = CreateSafetyReader(() => Assert.Fail("Covered Burst storage must not read legacy IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            CollectionAssert.AreEqual(component == null ? Array.Empty<string>() : new[] { SafetyExceptionDependency(component, 2) },
                SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            var countRows = counts.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(creations == 0 ? 0 : 1, countRows.Length);
            if (creations != 0) Assert.IsTrue(countRows.Single().EndsWith("\t" + creations + "\t0", StringComparison.Ordinal));
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual(weight.ToString(System.Globalization.CultureInfo.InvariantCulture), weights[3]);
        }

        [Test]
        public void BurstStorageUnknownTypeCannotPublishCompleteCatalogs() {
            foreach (var kind in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(BurstStorageUnknownTypeJob), kind);
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
            }
        }

        [TestCase(typeof(BurstStorageOnlySystem), "proven")]
        [TestCase(typeof(BurstStorageBeforeCompleteSystem), "unproven")]
        [TestCase(typeof(BurstStorageAfterCompleteSystem), "proven")]
        [TestCase(typeof(BurstStorageLateArgumentSystem), "unproven")]
        [TestCase(typeof(BurstStorageReflectedLateArgumentSystem), "unproven")]
        [TestCase(typeof(BurstStorageUnknownHandleSystem), "incomplete")]
        [TestCase(typeof(BurstStorageUnknownTypeSystem), "incomplete")]
        public void BurstStorageSynchronizationPreservesArgumentOrderAndOpaqueHandles(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            if (expected != "incomplete") Assert.AreEqual("0", rows[2], string.Join("\n", rows));
        }
    }
}
