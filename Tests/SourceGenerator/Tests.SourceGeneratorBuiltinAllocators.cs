using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures. Never invoke allocation, freeing or scheduling.
        public static Allocator BuiltinAllocationAlias => Allocator.Persistent;
        public static Allocator BuiltinAllocationGetter { get { return BuiltinAllocationAlias; } }
        public static AllocatorManager.AllocatorHandle BuiltinAllocationHandle => BuiltinAllocationGetter;
        public static Allocator BuiltinAllocationFactory(int ignored) => Allocator.Temp;
        public static AllocatorManager.AllocatorHandle BuiltinAllocationHandleFactory() => AllocatorManager.TempJob;
        public static int BuiltinAllocationSize() { default(Ent).Set(new TestComponent()); Ent.New(); return 4; }
        public static Allocator BuiltinAllocationEffects => BuiltinAllocationFactory(BuiltinAllocationSize());
        public static Allocator mutableAllocation;
        public static readonly Allocator readonlyAllocation = Allocator.Persistent;
        public static Allocator BuiltinAllocationUnknown => mutableAllocation;
        public static Allocator BuiltinAllocationReadonly => readonlyAllocation;
        public static Allocator BuiltinAllocationConditional(bool choose) => choose ? Allocator.Temp : (Allocator)64;
        public static Allocator BuiltinAllocationRecursive => BuiltinAllocationCycle();
        public static Allocator BuiltinAllocationCycle() => BuiltinAllocationRecursive;
        public static unsafe void BuiltinAllocationOperations() {
            _ = AllocatorManager.Allocate(Allocator.Temp, 4, 4);
            _ = AllocatorManager.Allocate<int>(BuiltinAllocationHandle);
            AllocatorManager.Free(BuiltinAllocationGetter, null);
            AllocatorManager.Free(BuiltinAllocationHandleFactory(), null, 4, 4);
            AllocatorManager.Free<int>(BuiltinAllocationHandle, null);
            _ = UnsafeUtility.Malloc(4L, 4, BuiltinAllocationGetter);
            UnsafeUtility.Free(null, BuiltinAllocationAlias);
        }
        public static unsafe void BuiltinAllocationUnproven(AllocatorManager.AllocatorHandle handle, Allocator allocator) {
            _ = AllocatorManager.Allocate(handle, 4, 4);
            _ = AllocatorManager.Allocate(allocator, 4, 4);
            AllocatorManager.Free(handle, null);
            _ = AllocatorManager.Allocate((Allocator)64, 4, 4);
            _ = AllocatorManager.Allocate(BuiltinAllocationUnknown, 4, 4);
            _ = AllocatorManager.Allocate(BuiltinAllocationReadonly, 4, 4);
            _ = UnsafeUtility.Malloc(4L, 4, allocator);
            UnsafeUtility.Free(null, allocator);
        }
        public partial struct BuiltinAllocationJob : IJob {
            public void Execute() => BuiltinAllocationOperations();
        }
        public partial struct BuiltinAllocationEffectsJob : IJob {
            public unsafe void Execute() => _ = AllocatorManager.Allocate(BuiltinAllocationEffects, 4, 4);
        }
        public partial struct BuiltinAllocationConstructorJob : IJob {
            public void Execute() => _ = new NativeList<int>(1, BuiltinAllocationEffects);
        }
        public static unsafe void BuiltinAllocationFree<T>(T* pointer) where T : unmanaged => AllocatorManager.Free(BuiltinAllocationHandle, pointer);
        public partial struct BuiltinAllocationTypedFreeJob : IJob {
            public unsafe void Execute() => BuiltinAllocationFree<Test1Component>(null);
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct BuiltinAllocationJob : IJob {
                public unsafe void Execute() {
                    _ = AllocatorManager.Allocate<T>(BuiltinAllocationHandle);
                    BuiltinAllocationFree<T>(null);
                }
            }
        }
        public partial struct BuiltinAllocationUnknownJob : IJob {
            public void Execute() => BuiltinAllocationUnproven(default, default);
        }
        public partial struct BuiltinAllocationConditionalJob : IJob {
            public bool choose;
            public unsafe void Execute() => _ = AllocatorManager.Allocate(BuiltinAllocationConditional(this.choose), 4, 4);
        }
        public partial struct BuiltinAllocationRecursiveJob : IJob {
            public unsafe void Execute() => _ = AllocatorManager.Allocate(BuiltinAllocationRecursive, 4, 4);
        }
        public partial struct BuiltinAllocationStoredHandleJob : IJob {
            public unsafe void Execute() {
                var handle = BuiltinAllocationHandle;
                _ = AllocatorManager.Allocate(handle, 4, 4);
            }
        }
        public partial struct BuiltinAllocationMutatedContainerJob : IJob {
            public void Execute() {
                var list = new UnsafeList<int>(1, BuiltinAllocationHandle);
                list.Allocator = (Allocator)64;
                list.Add(1);
            }
        }
        public static int BuiltinAllocationScheduleArgument() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 0; }
        public static Allocator BuiltinAllocationScheduling => BuiltinAllocationFactory(BuiltinAllocationScheduleArgument());
        public partial struct BuiltinAllocationBeforeFreeSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) { UnsafeUtility.Free(null, BuiltinAllocationAlias); context.dependsOn.Complete(); }
        }
        public partial struct BuiltinAllocationAfterFreeSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); AllocatorManager.Free(BuiltinAllocationAlias, null); }
        }
        public partial struct BuiltinAllocationLateFreeSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) { context.dependsOn.Complete(); UnsafeUtility.Free(null, BuiltinAllocationScheduling); }
        }
        public partial struct BuiltinAllocationPendingSystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = AllocatorManager.Allocate(BuiltinAllocationScheduling, 4, 4);
                _ = default(Ent).Read<TestComponent>();
            }
        }
        public partial struct BuiltinAllocationOnlySystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) => _ = AllocatorManager.Allocate<TestComponent>(BuiltinAllocationHandle);
        }

        [Test]
        public void BuiltinAllocatorProofIsCallSiteSpecificAndDoesNotEraseFactories() {
            var rows = ExternalValueOperations(nameof(BuiltinAllocationOperations));
            var allocations = rows.Where(row => row[3].Contains("AllocatorManager.Allocate") || row[3].Contains("AllocatorManager.Free") ||
                row[3].Contains("UnsafeUtility.Malloc") || row[3].Contains("UnsafeUtility.Free")).ToArray();
            Assert.AreEqual(7, allocations.Length);
            foreach (var row in allocations) CollectionAssert.Contains(row, "!native-allocation=builtin-v1");
            Assert.AreEqual(4, allocations.Count(row => row.Contains("!native-memory-access")));
            Assert.IsFalse(rows.Where(row => row[3].Contains("Tests_SourceGeneratorContracts.BuiltinAllocation")).Any(row => row.Contains("!ecs-leaf")));
            Assert.IsFalse(ExternalValueOperations(nameof(BuiltinAllocationUnproven)).Any(row => row.Contains("!native-allocation=builtin-v1")));
        }

        [Test]
        public void BuiltinAllocatorReturnContractsAreExportedWithoutExecutingGetters() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2")
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            foreach (var name in new[] { "get_BuiltinAllocationAlias", "get_BuiltinAllocationGetter", "get_BuiltinAllocationHandle",
                         "BuiltinAllocationFactory", "BuiltinAllocationHandleFactory", "get_BuiltinAllocationEffects" }) {
                var row = rows.Single(row => row[0] == "M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name ||
                    row[0].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name + "(", StringComparison.Ordinal));
                CollectionAssert.Contains(row[1].Split(','), "allocator-result=builtin-v1", name);
            }
            foreach (var name in new[] { "get_BuiltinAllocationUnknown", "get_BuiltinAllocationReadonly", "BuiltinAllocationConditional",
                         "get_BuiltinAllocationRecursive", "BuiltinAllocationCycle" }) {
                var row = rows.Single(row => row[0] == "M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name ||
                    row[0].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name + "(", StringComparison.Ordinal));
                Assert.IsFalse(row[1].Split(',').Contains("allocator-result=builtin-v1"), name);
            }
        }

        [TestCase(typeof(BuiltinAllocationJob), 0)]
        [TestCase(typeof(BuiltinAllocationEffectsJob), 1)]
        [TestCase(typeof(BuiltinAllocationConstructorJob), 1)]
        [TestCase(typeof(BuiltinAllocationTypedFreeJob), 2)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.BuiltinAllocationJob), 3)]
        public void BuiltinAllocatorEffectsUseCompleteSourceWithoutIL(Type job, int scenario) {
            var reader = CreateSafetyReader(() => Assert.Fail("Proven built-in allocator must not invoke legacy analysis."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            var expected = scenario == 0 ? Array.Empty<string>() : new[] { SafetyExceptionDependency(
                scenario == 1 ? typeof(TestComponent) : scenario == 2 ? typeof(Test1Component) : typeof(AotMarker), 2) };
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
            var counts = ControlSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.AreEqual(scenario == 1, counts.Any(row => row.StartsWith("C\t", StringComparison.Ordinal) && row.EndsWith("\t1\t0", StringComparison.Ordinal)));
            var weights = ControlSummary(job, "ME.BECS.JobWeights.v1");
            Assert.AreEqual("0", weights[2], string.Join("\n", weights));
            Assert.AreEqual(scenario == 1 ? "12" : "0", weights[3]);
        }

        [TestCase(typeof(BuiltinAllocationUnknownJob))]
        [TestCase(typeof(BuiltinAllocationConditionalJob))]
        [TestCase(typeof(BuiltinAllocationRecursiveJob))]
        [TestCase(typeof(BuiltinAllocationStoredHandleJob))]
        [TestCase(typeof(BuiltinAllocationMutatedContainerJob))]
        public void BuiltinAllocatorProofDoesNotEscapeIntoMutableStorage(Type job) {
            foreach (var kind in new[] { "JobSafety", "JobEntityCounts", "JobWeights" }) {
                var rows = ControlSummary(job, "ME.BECS." + kind + ".v1");
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\t", StringComparison.Ordinal)));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }

        [TestCase(typeof(BuiltinAllocationBeforeFreeSystem), "unproven")]
        [TestCase(typeof(BuiltinAllocationAfterFreeSystem), "proven")]
        [TestCase(typeof(BuiltinAllocationLateFreeSystem), "unproven")]
        [TestCase(typeof(BuiltinAllocationPendingSystem), "unproven")]
        [TestCase(typeof(BuiltinAllocationOnlySystem), "proven")]
        public void BuiltinAllocatorSynchronizationRetainsArgumentsAndFreeOrdering(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
        }
    }
}
