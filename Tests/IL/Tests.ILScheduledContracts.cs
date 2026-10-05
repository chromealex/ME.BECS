using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using ME.BECS.Mono.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata/IL fixtures only. Never run default queries or these job bodies.
        // Private to keep deliberately multi-contract jobs out of runtime EarlyInit:
        // its one-wrapper-per-job-family contract is a separate limitation.
        private partial struct ILDualScheduleJob : Unity.Jobs.IJob, IJobForComponents<TestComponent> {
            public Ent target;
            void Unity.Jobs.IJob.Execute() => this.target.Read<Test1Component>();
            public void Execute(in JobInfo info, in Ent ent, [WO] ref TestComponent value) { }
            public void Execute(int unrelated) => this.target.Get<Test4Component>();
        }
        private partial struct ILTwoComponentsScheduleJob : IJobForComponents<TestComponent>, IJobForComponents<Test1Component> {
            public Ent target;
            void IJobForComponents<TestComponent>.Execute(in JobInfo info, in Ent ent, [WO] ref TestComponent value) => this.target.Read<Test2Component>();
            void IJobForComponents<Test1Component>.Execute(in JobInfo info, in Ent ent, [RO] ref Test1Component value) => this.target.Get<Test3Component>();
        }
        private partial struct ILSelectedGeneric<T> where T : unmanaged, IComponentBase {
            public partial struct Job : IJobForComponents<T>, Unity.Jobs.IJob {
                public Ent target;
                public void Execute(in JobInfo info, in Ent ent, [WO] ref T value) { }
                void Unity.Jobs.IJob.Execute() => this.target.Get<Test4Component>();
            }
        }
        private partial struct ILSelectedSingleJob : IJobSingle, Unity.Jobs.IJob {
            public Ent target;
            void IJobSingle.Execute() => this.target.Read<Test1Component>();
            void Unity.Jobs.IJob.Execute() => this.target.Get<Test2Component>();
        }
        private partial struct ILSelectedUnityJob : Unity.Jobs.IJob, Unity.Jobs.IJobFor, Unity.Jobs.IJobParallelFor, Unity.Jobs.IJobParallelForBatch, Unity.Jobs.IJobParallelForDefer {
            public Ent target;
            void Unity.Jobs.IJob.Execute() => this.target.Read<TestComponent>();
            void Unity.Jobs.IJobFor.Execute(int index) => this.target.Read<Test1Component>();
            void Unity.Jobs.IJobParallelFor.Execute(int index) => this.target.Read<Test2Component>();
            void Unity.Jobs.IJobParallelForBatch.Execute(int start, int count) => this.target.Read<Test3Component>();
            void Unity.Jobs.IJobParallelForDefer.Execute(int index) => this.target.Read<Test4Component>();
        }
        private partial struct ILSelectedCommandJob : IJobCommandBuffer, IJobParallelForCommandBuffer, IJobParallelForCommandBufferBatch {
            public Ent target;
            public void Execute(in CommandBufferJob buffer) => this.target.Read<TestComponent>();
            void IJobParallelForCommandBuffer.Execute(in CommandBufferJobParallel buffer) => this.target.Read<Test1Component>();
            void IJobParallelForCommandBufferBatch.Execute(in CommandBufferJobBatch buffer) => this.target.Read<Test2Component>();
        }

        private static void ILSelectedDualUnity() => Unity.Jobs.IJobExtensions.Schedule(default(ILDualScheduleJob));
        private static void ILSelectedDualQuery() => default(QueryBuilder).Schedule<ILDualScheduleJob, TestComponent>();
        private static void ILSelectedDualReadonly() => default(QueryBuilder).AsReadonly().Schedule<ILDualScheduleJob, TestComponent>();
        private static void ILSelectedFirst() => default(QueryBuilder).Schedule<ILTwoComponentsScheduleJob, TestComponent>();
        private static void ILSelectedSecond() => default(QueryBuilder).Schedule<ILTwoComponentsScheduleJob, Test1Component>();
        private static void ILSelectedBoth() { ILSelectedFirst(); ILSelectedSecond(); }
        private static void ILSelectedBothReverse() { ILSelectedSecond(); ILSelectedFirst(); }
        private static void ILSelectedGenericQuery() => default(QueryBuilder).AsReadonly().Schedule<ILSelectedGeneric<AotMarker>.Job, AotMarker>();
        private static void ILSelectedGenericUnity() => Unity.Jobs.IJobExtensions.Schedule(default(ILSelectedGeneric<AotMarker>.Job));
        private static void ILSelectedUnitySingle() => Unity.Jobs.IJobExtensions.Schedule(default(ILSelectedUnityJob));
        private static void ILSelectedUnityFor() => Unity.Jobs.IJobForExtensions.Schedule(default(ILSelectedUnityJob), 1, default);
        private static void ILSelectedUnityParallel() => Unity.Jobs.IJobParallelForExtensions.Schedule(default(ILSelectedUnityJob), 1, 1, default);
        private static void ILSelectedUnityBatch() => Unity.Jobs.IJobParallelForBatchExtensions.ScheduleBatch(default(ILSelectedUnityJob), 1, 1, default);
        private static void ILSelectedUnityDefer() => Unity.Jobs.IJobParallelForDeferExtensions.Schedule(default(ILSelectedUnityJob), default(Unity.Collections.NativeList<int>), 1, default);
        private static void ILSelectedSingle() => default(ILSelectedSingleJob).ScheduleSingle();
        private static void ILSelectedSingleRef() { var job = default(ILSelectedSingleJob); job.ScheduleSingleByRef(); }
        private static void ILSelectedSingleInject() => default(ILSelectedSingleJob).ScheduleSingleWithInject(0);
        private static void ILSelectedSingleInjectRef() { var job = default(ILSelectedSingleJob); job.ScheduleSingleWithInjectByRef(0); }
        private static void ILSelectedCommand() => default(QueryBuilder).Schedule<ILSelectedCommandJob>();
        #pragma warning disable CS0618 // Audited compatibility APIs remain valid scheduler entry points.
        private static void ILSelectedCommandParallel() => default(QueryBuilder).ScheduleParallelFor<ILSelectedCommandJob>();
        private static void ILSelectedCommandBatch() => default(QueryBuilder).ScheduleParallelForBatch<ILSelectedCommandJob>();
        #pragma warning restore CS0618
        private static class ILScheduleNamedHelper {
            public static Unity.Jobs.JobHandle Schedule<T>() where T : struct, Unity.Jobs.IJob => default;
            public static Unity.Jobs.JobHandle ScheduleSingle<T>() where T : struct, IJobSingle => default;
        }
        private static void ILSelectedNamesAreNotSchedulers() {
            ILScheduleNamedHelper.Schedule<ILDualScheduleJob>();
            ILScheduleNamedHelper.ScheduleSingle<ILSelectedSingleJob>();
        }

        private static Type ILScheduledContractReader => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILJobScheduleContract", true);
        private static MethodInfo ILScheduledCall(string name) => ILQueryModeMethod(name).GetInstructions()
            .Select(instruction => instruction.Operand).OfType<MethodInfo>().Single(method => method.Name.StartsWith("Schedule", StringComparison.Ordinal));
        private static Type ILScheduledContract(MethodInfo method) => (Type)ILScheduledContractReader
            .GetMethod("GetWorkInterface", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { method });

        [TestCase(nameof(ILSelectedDualUnity), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedDualQuery), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(nameof(ILSelectedDualReadonly), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedGenericQuery), typeof(AotMarker), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedGenericUnity), typeof(Test4Component), RefOp.ReadWrite)]
        [TestCase(nameof(ILSelectedUnitySingle), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedUnityFor), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedUnityParallel), typeof(Test2Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedUnityBatch), typeof(Test3Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedUnityDefer), typeof(Test4Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedSingle), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedSingleRef), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedSingleInject), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedSingleInjectRef), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedCommand), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedCommandParallel), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedCommandBatch), typeof(Test2Component), RefOp.ReadOnly)]
        public void ILScheduledDependenciesUseOnlyTheSelectedExecute(string method, Type component, RefOp access) {
            var actual = ILSystemPresenceAccesses(ILQueryModeMethod(method));
            CollectionAssert.AreEqual(new[] { component }, actual.Keys);
            Assert.AreEqual(access, actual[component]);
        }

        [TestCase(nameof(ILSelectedFirst), typeof(TestComponent), typeof(Test2Component), RefOp.ReadWrite, RefOp.ReadOnly)]
        [TestCase(nameof(ILSelectedSecond), typeof(Test1Component), typeof(Test3Component), RefOp.ReadOnly, RefOp.ReadWrite)]
        public void ILScheduledClosedInterfacesKeepTheirOwnArgumentsAndBody(string method, Type argument, Type body, RefOp argumentMode, RefOp bodyMode) {
            var actual = ILSystemPresenceAccesses(ILQueryModeMethod(method));
            CollectionAssert.AreEquivalent(new[] { argument, body }, actual.Keys);
            Assert.AreEqual(argumentMode, actual[argument]);
            Assert.AreEqual(bodyMode, actual[body]);
        }

        [TestCase(nameof(ILSelectedBoth))]
        [TestCase(nameof(ILSelectedBothReverse))]
        public void ILScheduledContractCacheCannotLeakBetweenContracts(string method) {
            var actual = ILSystemPresenceAccesses(ILQueryModeMethod(method));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) }, actual.Keys);
            Assert.AreEqual(RefOp.ReadWrite, actual[typeof(TestComponent)]);
            Assert.AreEqual(RefOp.ReadOnly, actual[typeof(Test1Component)]);
            Assert.AreEqual(RefOp.ReadOnly, actual[typeof(Test2Component)]);
            Assert.AreEqual(RefOp.ReadWrite, actual[typeof(Test3Component)]);
        }

        [Test]
        public void ILScheduledContractKeepsForwardedParameterAnnotations() {
            var call = ILScheduledCall(nameof(ILSelectedDualQuery));
            var contract = ILScheduledContract(call);
            Assert.AreEqual(typeof(IJobForComponents<TestComponent>), contract);
            var root = (MethodInfo)ILScheduledContractReader.GetMethod("GetExecuteMethod", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { typeof(ILDualScheduleJob), contract });
            Assert.IsTrue(root.GetParameters().Last().IsDefined(typeof(WOAttribute), false),
                "An imported in/modreq adapter must not erase the user's WO annotation.");
        }

        [Test]
        public void ILScheduledContractDoesNotNarrowGlobalJobRegistration() {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = analyzer.GetMethod("GetJobTypesInfo").Invoke(null, new object[] { typeof(ILDualScheduleJob), null });
            var components = ((System.Collections.IEnumerable)result).Cast<object>()
                .Select(item => (Type)item.GetType().GetField("type").GetValue(item)).Distinct().ToArray();
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, components,
                "Global safety still includes every supported contract, but never an unrelated Execute overload.");
        }

        [Test]
        public void ILScheduledContractRejectsOpenAndUserNamedMethods() {
            var closed = ILScheduledCall(nameof(ILSelectedDualQuery));
            var error = Assert.Throws<TargetInvocationException>(() => ILScheduledContract(closed.GetGenericMethodDefinition()));
            Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
            foreach (var method in typeof(ILScheduleNamedHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                var job = method.Name == "Schedule" ? typeof(ILDualScheduleJob) : typeof(ILSelectedSingleJob);
                error = Assert.Throws<TargetInvocationException>(() => ILScheduledContract(method.MakeGenericMethod(job)));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
            }
            CollectionAssert.IsEmpty(ILSystemPresenceAccesses(ILQueryModeMethod(nameof(ILSelectedNamesAreNotSchedulers))));
        }
    }
}
