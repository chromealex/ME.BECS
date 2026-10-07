using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures: no constructor, job or lifecycle callback is executed.
        public static int ConstructorWrite() { default(Ent).Set(new TestComponent()); return 0; }
        public static int ConstructorComplete(ref SystemContext context) { context.dependsOn.Complete(); return 0; }
        public static int ConstructorSchedule() { _ = IJobExtensions.Schedule(default(ControlFirstJob), default); return 0; }
        public static int ConstructorScheduleComplete() {
            var handle = IJobExtensions.Schedule(default(ControlFirstJob), default);
            handle.Complete();
            return 0;
        }
        public static bool constructorBranch;

        public class CompletingConstructorBase {
            public CompletingConstructorBase(ref SystemContext context) {
                context.dependsOn.Complete();
                ConstructorWrite();
            }
        }
        public sealed class FieldBeforeBaseConstructor : CompletingConstructorBase {
            public int value = ConstructorWrite();
            public FieldBeforeBaseConstructor(ref SystemContext context) : base(ref context) { }
        }
        public sealed class DelegatingConstructor : CompletingConstructorBase {
            public int value = ConstructorWrite();
            public DelegatingConstructor(ref SystemContext context, bool marker) : this(ref context, ConstructorComplete(ref context)) { }
            public DelegatingConstructor(ref SystemContext context, int marker) : base(ref context) { }
        }
        public sealed class PendingInitializerConstructor {
            public int value = ConstructorSchedule();
            public PendingInitializerConstructor() { ConstructorWrite(); }
        }
        public sealed class CompletedInitializerConstructor {
            public int value = ConstructorScheduleComplete();
            public int Value { get; } = ConstructorWrite();
            public CompletedInitializerConstructor() { ConstructorWrite(); }
        }
        public sealed class ConditionalInitializerConstructor {
            public int first = constructorBranch ? ConstructorScheduleComplete() : ConstructorScheduleComplete();
            public int Second { get; } = constructorBranch ? ConstructorWrite() : ConstructorWrite();
        }
        public sealed class PendingBranchInitializerConstructor {
            public int first = constructorBranch ? ConstructorSchedule() : ConstructorScheduleComplete();
            public int Second { get; } = constructorBranch ? ConstructorWrite() : ConstructorWrite();
        }
        public class SchedulingConstructorBase {
            public SchedulingConstructorBase() { ConstructorSchedule(); }
        }
        public sealed class ImplicitSchedulingBaseConstructor : SchedulingConstructorBase {
            public int value = ConstructorWrite();
        }
        public sealed class GenericStorageConstructor<T> {
            public T field = default;
            public T Value { get; } = default;
            public GenericStorageConstructor() { _ = this.field; _ = this.Value; ConstructorWrite(); }
        }
        public sealed class ExceptionConstructor {
            public int value = ConstructorWrite();
            public ExceptionConstructor() { try { ConstructorWrite(); } finally { ConstructorSchedule(); } }
        }
        public static Action ConstructorEventInitializer() { ConstructorSchedule(); return null; }
        public sealed class EventInitializerConstructor {
            public event Action Changed = ConstructorEventInitializer();
            public EventInitializerConstructor() { ConstructorWrite(); }
        }
        public sealed class ImplicitEventInitializerConstructor {
            public event Action Changed = ConstructorEventInitializer();
        }
        public sealed class StaticInitializerConstructor {
            static StaticInitializerConstructor() { ConstructorSchedule(); }
            public StaticInitializerConstructor() { ConstructorWrite(); }
        }
        public sealed class ThisChainCreationConstructor {
            public Ent value = Ent.New();
            public ThisChainCreationConstructor() : this(0) { }
            public ThisChainCreationConstructor(int marker) { }
        }

        public partial struct FieldBeforeBaseSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => _ = new FieldBeforeBaseConstructor(ref context);
        }
        public partial struct FieldAfterCallerCompletionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new FieldBeforeBaseConstructor(ref context);
            }
        }
        public partial struct ThisArgumentCompletionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => _ = new DelegatingConstructor(ref context, true);
        }
        public partial struct BaseArgumentCompletionSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => _ = new BaseArgumentConstructor(ConstructorComplete(ref context));
        }
        public class ScalarConstructorBase { public ScalarConstructorBase(int marker) { } }
        public sealed class BaseArgumentConstructor : ScalarConstructorBase {
            public int value = ConstructorWrite();
            public BaseArgumentConstructor(int marker) : base(ConstructorScheduleComplete()) { }
        }
        public partial struct BaseArgumentAfterFieldSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => _ = new BaseCompletesArgumentConstructor(ref context);
        }
        public sealed class BaseCompletesArgumentConstructor : ScalarConstructorBase {
            public int value = ConstructorWrite();
            public BaseCompletesArgumentConstructor(ref SystemContext context) : base(ConstructorComplete(ref context)) { }
        }
        public partial struct PendingConstructorInitializerSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new PendingInitializerConstructor();
            }
        }
        public partial struct CompletedConstructorInitializerSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new CompletedInitializerConstructor();
            }
        }
        public partial struct ConditionalConstructorInitializerSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new ConditionalInitializerConstructor();
            }
        }
        public partial struct PendingBranchConstructorInitializerSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new PendingBranchInitializerConstructor();
            }
        }
        public partial struct ImplicitBaseSchedulingSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new ImplicitSchedulingBaseConstructor();
                ConstructorWrite();
            }
        }
        public partial struct GenericIntConstructorSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new GenericStorageConstructor<int>();
            }
        }
        public partial struct GenericHandleConstructorSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new GenericStorageConstructor<JobHandle>();
            }
        }
        public partial struct ExceptionConstructorSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new ExceptionConstructor();
            }
        }
        public partial struct EventInitializerConstructorSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new EventInitializerConstructor();
            }
        }
        public partial struct ImplicitEventInitializerConstructorSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new ImplicitEventInitializerConstructor();
                ConstructorWrite();
            }
        }
        public partial struct StaticInitializerConstructorSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = new StaticInitializerConstructor();
            }
        }
        public partial struct ThisChainCreationJob : IJob {
            public void Execute() => _ = new ThisChainCreationConstructor();
        }
        public partial struct StaticInitializerConstructorJob : IJob {
            public void Execute() => _ = new StaticInitializerConstructor();
        }

        [TestCase(typeof(FieldBeforeBaseSystem), "unproven")]
        [TestCase(typeof(FieldAfterCallerCompletionSystem), "proven")]
        [TestCase(typeof(ThisArgumentCompletionSystem), "proven")]
        [TestCase(typeof(BaseArgumentCompletionSystem), "proven")]
        [TestCase(typeof(BaseArgumentAfterFieldSystem), "unproven")]
        [TestCase(typeof(PendingConstructorInitializerSystem), "unproven")]
        [TestCase(typeof(CompletedConstructorInitializerSystem), "proven")]
        [TestCase(typeof(ConditionalConstructorInitializerSystem), "proven")]
        [TestCase(typeof(PendingBranchConstructorInitializerSystem), "unproven")]
        [TestCase(typeof(ImplicitBaseSchedulingSystem), "unproven")]
        [TestCase(typeof(GenericIntConstructorSystem), "proven")]
        [TestCase(typeof(EventInitializerConstructorSystem), "unproven")]
        [TestCase(typeof(ImplicitEventInitializerConstructorSystem), "unproven")]
        public void ConstructorSynchronizationPreservesEvaluationOrderWithoutLegacy(Type system, string expected) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + expected, string.Join("\n", rows));
            Assert.IsNotNull(DependencySelector(system, () => Assert.Fail("Complete constructor flow must not use IL."), out _));
        }

        [TestCase(typeof(GenericHandleConstructorSystem), "FieldStorage")]
        [TestCase(typeof(StaticInitializerConstructorSystem), "ConstructorTypeInitializer")]
        public void ConstructorSynchronizationKeepsUnknownStorageAndExceptionalFlowIncomplete(Type system, string gap) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\tincomplete");
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\t" + gap + ":", StringComparison.Ordinal)), string.Join("\n", rows));
        }

        [Test]
        public void ThisConstructorChainRunsInstanceInitializersOnlyOnce() {
            var rows = ControlSummary(typeof(ThisChainCreationJob), "ME.BECS.JobEntityCounts.v1");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var count = rows.Select(row => row.Split('\t')).Single(row => row[0] == "C");
            Assert.AreEqual("1", count[3]);
            Assert.AreEqual("0", count[4]);
        }

        [Test]
        public void ConstructorTypeInitializationCannotCertifyEffectCatalogs() {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = ControlSummary(typeof(StaticInitializerConstructorJob), key);
                Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tConstructorTypeInitializer ", StringComparison.Ordinal)), string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)), string.Join("\n", rows));
            }
        }
    }
}
