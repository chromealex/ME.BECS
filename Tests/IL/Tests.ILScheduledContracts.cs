using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using ME.BECS.Mono.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [Test]
        public void RefAccessStackCountsCoverEveryFixedStackBehavior() {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var count = analyzer.GetMethod("FixedStackCount", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (System.Reflection.Emit.StackBehaviour behavior in Enum.GetValues(typeof(System.Reflection.Emit.StackBehaviour))) {
                var name = behavior.ToString();
                var expected = name == "Varpop" || name == "Varpush" ? -1 :
                    name == "Pop0" || name == "Push0" ? 0 : name.Split('_').Length;
                Assert.AreEqual(expected, count.Invoke(null, new object[] { behavior }), name);
            }
            Assert.AreEqual(-1, count.Invoke(null, new object[] { (System.Reflection.Emit.StackBehaviour)int.MaxValue }));
        }

        private partial struct ILUnusedComponentArgumentJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                ent.Read<Test1Component>();
            }
        }

        private partial struct ILUnusedRefHelperJob : IJobForComponents<TestComponent> {
            private static void Ignore(ref TestComponent value) { }
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                Ignore(ref value);
                ent.Read<Test1Component>();
            }
        }

        private partial struct ILReadRefComponentJob : IJobForComponents<TestComponent> {
            public int result;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => this.result = value.data;
        }

        private struct ILNestedArgumentComponent : IComponent {
            public TestComponent nested;
        }

        private partial struct ILNestedReadRefJob : IJobForComponents<ILNestedArgumentComponent> {
            public int result;
            public void Execute(in JobInfo info, in Ent ent, ref ILNestedArgumentComponent value) => this.result = value.nested.data;
        }

        [Test]
        public void ILNestedFieldAddressUsedForReadDoesNotInventWrite() {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = (System.Collections.IEnumerable)analyzer.GetMethod("GetJobTypesInfo")
                .Invoke(null, new object[] { typeof(ILNestedReadRefJob), null });
            var rows = result.Cast<object>().ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(typeof(ILNestedArgumentComponent), rows[0].GetType().GetField("type").GetValue(rows[0]));
            Assert.AreEqual(RefOp.ReadOnly, rows[0].GetType().GetField("op").GetValue(rows[0]));
        }

        private partial struct ILWriteRefComponentJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => value.data = 42;
        }

        private partial struct ILResetRefComponentJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => value = default;
        }

        private partial struct ILLocalReadRefComponentJob : IJobForComponents<TestComponent> {
            public int result;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                ref var alias = ref value;
                this.result = alias.data;
            }
        }

        private partial struct ILLocalWriteRefComponentJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                ref var alias = ref value;
                alias.data = 42;
            }
        }

        private partial struct ILResetThenReadRefComponentJob : IJobForComponents<TestComponent> {
            public int result;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                value = default;
                this.result = value.data;
            }
        }

        private partial struct ILReadRefHelperJob : IJobForComponents<TestComponent> {
            public int result;
            private static int Read(ref TestComponent value) => value.data;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => this.result = Read(ref value);
        }

        private partial struct ILWriteRefHelperJob : IJobForComponents<TestComponent> {
            private static void Write(ref TestComponent value) => value.data = 42;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => Write(ref value);
        }

        private partial struct ILReadLastRefHelperJob : IJobForComponents<TestComponent> {
            public int result;
            private static int Read(int offset, ref TestComponent value) => offset + value.data;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => this.result = Read(42, ref value);
        }

        private partial struct ILWriteLastRefHelperJob : IJobForComponents<TestComponent> {
            private static void Write(int data, ref TestComponent value) => value.data = data;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => Write(42, ref value);
        }

        private partial struct ILReadFirstRefHelperJob : IJobForComponents<TestComponent> {
            public int result;
            private static int Read(ref TestComponent value, int offset) => value.data + offset;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => this.result = Read(ref value, 42);
        }

        private partial struct ILWriteFirstRefHelperJob : IJobForComponents<TestComponent> {
            private static void Write(ref TestComponent value, int data) => value.data = data;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) => Write(ref value, 42);
        }

        private partial struct ILBranchRefComponentJob : IJobForComponents<TestComponent> {
            public bool write;
            public int result;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                if (this.write) value.data = 42;
                else this.result = value.data;
            }
        }

        private partial struct ILReadThenWriteRefComponentJob : IJobForComponents<TestComponent> {
            public int result;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent value) {
                this.result = value.data;
                value.data = 42;
            }
        }

        [TestCase(typeof(ILReadRefComponentJob), RefOp.ReadOnly)]
        [TestCase(typeof(ILWriteRefComponentJob), RefOp.WriteOnly)]
        [TestCase(typeof(ILResetRefComponentJob), RefOp.WriteOnly)]
        [TestCase(typeof(ILLocalReadRefComponentJob), RefOp.ReadOnly)]
        [TestCase(typeof(ILLocalWriteRefComponentJob), RefOp.WriteOnly)]
        [TestCase(typeof(ILResetThenReadRefComponentJob), RefOp.ReadWrite)]
        [TestCase(typeof(ILReadRefHelperJob), RefOp.ReadOnly)]
        [TestCase(typeof(ILWriteRefHelperJob), RefOp.WriteOnly)]
        [TestCase(typeof(ILReadLastRefHelperJob), RefOp.ReadOnly)]
        [TestCase(typeof(ILWriteLastRefHelperJob), RefOp.WriteOnly)]
        [TestCase(typeof(ILReadFirstRefHelperJob), RefOp.ReadOnly)]
        [TestCase(typeof(ILWriteFirstRefHelperJob), RefOp.WriteOnly)]
        [TestCase(typeof(ILBranchRefComponentJob), RefOp.ReadWrite)]
        [TestCase(typeof(ILReadThenWriteRefComponentJob), RefOp.ReadWrite)]
        public void ILRefComponentDirectReadDoesNotInventWrite(Type job, RefOp expected) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = (System.Collections.IEnumerable)analyzer.GetMethod("GetJobTypesInfo")
                .Invoke(null, new object[] { job, null });
            var rows = result.Cast<object>().ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(typeof(TestComponent), rows[0].GetType().GetField("type").GetValue(rows[0]));
            Assert.AreEqual(expected, rows[0].GetType().GetField("op").GetValue(rows[0]));
        }

        [TestCase(typeof(ILUnusedComponentArgumentJob))]
        [TestCase(typeof(ILUnusedRefHelperJob))]
        public void ILUnusedRefComponentDoesNotInventWriteDependency(Type job) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = (System.Collections.IEnumerable)analyzer.GetMethod("GetJobTypesInfo")
                .Invoke(null, new object[] { job, null });
            var rows = result.Cast<object>().ToArray();
            Assert.AreEqual(1, rows.Length, "The unused ref argument is not a component access; the body read must remain.");
            Assert.AreEqual(typeof(Test1Component), rows[0].GetType().GetField("type").GetValue(rows[0]));
            Assert.AreEqual(RefOp.ReadOnly, rows[0].GetType().GetField("op").GetValue(rows[0]));
        }

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
