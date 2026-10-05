using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using ME.BECS.Mono.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    // Metadata/IL fixtures only. These jobs, constructors and filters are never
    // executed by the unit tests below; no bootstrap or world is required.
    public partial class Tests_ILJobEntityCounts {
        public struct GroupA : IEntityType { }
        public struct GroupB : IEntityType { }
        private static void Create(in JobInfo info) => Ent.New<GroupA>(in info);
        private delegate void EntityCallback(in JobInfo info);
        internal partial struct DelegateRepeatedCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                var callback = new EntityCallback(Create); callback(in info); callback(in info);
            }
        }
        [EntitiesJobMaxCount(7)]
        private partial struct DelegateLoopCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                var callback = new EntityCallback(Create);
                for (var index = 0; index < data.data; ++index) callback(in info);
            }
        }
        private partial struct DelegateOverwrittenCreation : IJobForComponents<TestComponent> {
            private static void Empty(in JobInfo info) { }
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                var callback = new EntityCallback(Create); callback = new EntityCallback(Empty); callback(in info);
            }
        }
        private partial struct DelegateMulticastCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                var callback = new EntityCallback(Create); callback += Create; callback(in info);
            }
        }
        private partial struct DelegateBranchCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                var callback = data.data == 0 ? new EntityCallback(Create) : new EntityCallback(GenericCreate<GroupB>);
                callback(in info);
            }
        }

        public partial struct Repeated : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { Create(in info); Create(in info); }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct MixedLoops : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                Create(in info);
                for (var i = 0; i < data.data; ++i) Create(in info);
                Create(in info);
                for (var i = 0; i < data.data; ++i) Create(in info);
            }
        }
        public partial struct BackwardsAcyclic : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                goto later;
                earlier: Create(in info); return;
                later: goto earlier;
            }
        }
        private sealed class CreateInConstructor { internal CreateInConstructor(in JobInfo info) { Create(in info); } }
        public partial struct Constructors : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                _ = new CreateInConstructor(in info); _ = new CreateInConstructor(in info);
            }
        }
        private static void GenericCreate<T>(in JobInfo info) where T : unmanaged, IEntityType => Ent.New<T>(in info);
        public partial struct GenericGroups : IJobForComponents<TestComponent> {
            void IJobForComponents<TestComponent>.Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                GenericCreate<GroupA>(in info); GenericCreate<GroupB>(in info); GenericCreate<GroupA>(in info);
            }
            public void Execute(int unrelated) => Ent.New<GroupB>(default(JobInfo));
        }
        public partial struct GenericContainer<T> where T : unmanaged, IComponent {
            public partial struct Job : IJobForComponents<T> {
                public void Execute(in JobInfo info, in Ent ent, ref T data) { Create(in info); Create(in info); }
            }
        }
        public partial struct UnboundedLoop : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                for (var i = 0; i < data.data; ++i) Create(in info);
            }
        }
        public interface ICreate { void Make(in JobInfo info); }
        public struct Creator : ICreate { void ICreate.Make(in JobInfo info) => Create(in info); }
        private static void Constrained<T>(T creator, in JobInfo info) where T : struct, ICreate => creator.Make(in info);
        public partial struct InterfaceCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { Constrained(new Creator(), in info); Constrained(new Creator(), in info); }
        }
        private sealed class SealedCreator : ICreate { void ICreate.Make(in JobInfo info) => Create(in info); }
        private class OpenCreator : ICreate { public virtual void Make(in JobInfo info) => Create(in info); }
        private sealed class InheritedCreator : OpenCreator { }
        private static void ConstrainedAny<T>(T creator, in JobInfo info) where T : ICreate => creator.Make(in info);
        internal partial struct SealedInterfaceCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                ConstrainedAny<SealedCreator>(null, in info); ConstrainedAny<SealedCreator>(null, in info);
            }
        }
        internal partial struct SealedInheritedCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) => ConstrainedAny<InheritedCreator>(null, in info);
        }
        private partial struct OpenInterfaceCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) => ConstrainedAny<OpenCreator>(null, in info);
        }
        [EntitiesJobMaxCount(5)]
        public partial struct FinallyInLoop : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                for (var i = 0; i < data.data; ++i) { try { Create(in info); } finally { Create(in info); } }
            }
        }
        [EntitiesJobMaxCount(5)]
        public partial struct FinallyOutsideLoop : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                try { for (var i = 0; i < data.data; ++i) Create(in info); } finally { Create(in info); }
            }
        }
        [EntitiesJobMaxCount(5)]
        public partial struct FilterCreation : IJobForComponents<TestComponent> {
            private static bool Filter(in JobInfo info) { Create(in info); return true; }
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                try { if (data.data != 0) throw new System.Exception(); } catch (System.Exception) when (Filter(in info)) { }
            }
        }
        public partial struct RecursiveCreation : IJobForComponents<TestComponent> {
            private static void Recurse(in JobInfo info) { Create(in info); Recurse(in info); }
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) => Recurse(in info);
        }
        private static int NoCreationA(int n) => n > 0 ? NoCreationB(n - 1) : 0;
        private static int NoCreationB(int n) => NoCreationA(n);
        public partial struct EmptyRecursivePath : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                data.data = NoCreationA(data.data); Create(in info); Create(in info);
            }
        }
        private static void RecursiveA(int n, in JobInfo info) { Create(in info); if (n > 0) RecursiveB(n - 1, in info); }
        private static void RecursiveB(int n, in JobInfo info) { Create(in info); RecursiveA(n, in info); }
        [EntitiesJobMaxCount(9)]
        public partial struct MutualRecursiveCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { Create(in info); RecursiveA(data.data, in info); }
        }
        private static void TwoGroupsA(int n, in JobInfo info) { Create(in info); if (n > 0) TwoGroupsB(n - 1, in info); }
        private static void TwoGroupsB(int n, in JobInfo info) { GenericCreate<GroupB>(in info); TwoGroupsA(n, in info); }
        [EntitiesJobMaxCount(12)]
        public partial struct RepeatedRecursiveEntries : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                TwoGroupsA(data.data, in info); TwoGroupsB(data.data, in info); TwoGroupsA(data.data, in info);
            }
        }
        private static void GenericRecurse<T>(int n, in JobInfo info) where T : unmanaged, IEntityType {
            GenericCreate<T>(in info); if (n > 0) GenericRecurse<T>(n - 1, in info);
        }
        [EntitiesJobMaxCount(3)]
        public partial struct GenericRecursiveCreation : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) => GenericRecurse<GroupA>(data.data, in info);
        }
        public partial struct RecursiveUnknownCallback : IJobForComponents<TestComponent> {
            public static Action callback;
            private static void Recurse(int n) { if (n > 0) Recurse(n - 1); callback(); }
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { Recurse(data.data); Create(in info); }
        }
        private static class EmptyStaticInitialization {
            internal static readonly int value = NoCreationA(5);
        }
        public partial struct EmptyRecursiveInitialization : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { data.data = EmptyStaticInitialization.value; Create(in info); }
        }
        private static class BurstStorage {
            // A type token used as the storage key must not initialize that type.
            internal static readonly Unity.Burst.SharedStatic<int> value = Unity.Burst.SharedStatic<int>.GetOrCreate<StaticCreation>();
        }
        public partial struct BurstStorageInitialization : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { data.data = BurstStorage.value.Data; Create(in info); }
        }
        public partial struct UnknownCallback : IJobForComponents<TestComponent> {
            public static Action callback;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { Create(in info); callback(); }
        }
        public partial struct IncompleteRepeated : IJobForComponents<TestComponent> {
            public static Action callback;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                Create(in info); Create(in info); callback();
            }
        }
        public partial struct IncompleteGeneric<T> where T : unmanaged, IComponent {
            public partial struct Job : IJobForComponents<T> {
                public static Action callback;
                public void Execute(in JobInfo info, in Ent ent, ref T data) { Create(in info); Create(in info); callback(); }
            }
        }
        private partial struct IncompleteVirtual : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                OpenCreator creator = null;
                creator.Make(in info);
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct IncompleteMixedLoops : IJobForComponents<TestComponent> {
            public static Action callback;
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                for (var index = 0; index < data.data; ++index) Create(in info);
                Create(in info); callback();
            }
        }
        private sealed class StaticCreation {
            internal static readonly Ent value = Ent.New<GroupA>(default(JobInfo));
        }
        public partial struct StaticCreationJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { data.data = (int)StaticCreation.value.id; }
        }

        private static object[] Analyze(Type job) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.ILJobEntityCounts", true);
            var args = new object[] { job, null, null };
            var complete = (bool)type.GetMethod("TryAnalyze", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            return new[] { (object)complete, args[1], args[2] };
        }

        [TestCase(typeof(Repeated), 2u, 0u)]
        [TestCase(typeof(DelegateRepeatedCreation), 2u, 0u)]
        [TestCase(typeof(DelegateLoopCreation), 0u, 1u)]
        [TestCase(typeof(MixedLoops), 2u, 2u)]
        [TestCase(typeof(BackwardsAcyclic), 1u, 0u)]
        [TestCase(typeof(Constructors), 2u, 0u)]
        [TestCase(typeof(InterfaceCreation), 2u, 0u)]
        [TestCase(typeof(SealedInterfaceCreation), 2u, 0u)]
        [TestCase(typeof(SealedInheritedCreation), 1u, 0u)]
        [TestCase(typeof(GenericContainer<TestComponent>.Job), 2u, 0u)]
        [TestCase(typeof(FinallyInLoop), 0u, 2u)]
        [TestCase(typeof(FinallyOutsideLoop), 1u, 1u)]
        [TestCase(typeof(FilterCreation), 0u, 1u)]
        [TestCase(typeof(RecursiveCreation), 0u, 1u)]
        [TestCase(typeof(EmptyRecursivePath), 2u, 0u)]
        [TestCase(typeof(MutualRecursiveCreation), 1u, 2u)]
        [TestCase(typeof(GenericRecursiveCreation), 0u, 1u)]
        [TestCase(typeof(EmptyRecursiveInitialization), 1u, 0u)]
        [TestCase(typeof(BurstStorageInitialization), 1u, 0u)]
        public void ILCountsPreserveCallSitesAndRepetitionContexts(Type job, uint inline, uint loops) {
            for (var repeat = 0; repeat < 2; ++repeat) {
                var result = Analyze(job);
                Assert.IsTrue((bool)result[0], result[2] as string);
                var counts = (IDictionary)result[1];
                Assert.AreEqual(1, counts.Count);
                AssertCount(counts, typeof(GroupA), inline, loops);
            }
        }

        [Test]
        public void ILCountsKeepClosedGenericIdentitiesAndIgnoreUnrelatedExecute() {
            var result = Analyze(typeof(GenericGroups));
            Assert.IsTrue((bool)result[0], result[2] as string);
            var counts = (IDictionary)result[1];
            Assert.AreEqual(2, counts.Count);
            AssertCount(counts, typeof(GroupA), 2u, 0u);
            AssertCount(counts, typeof(GroupB), 1u, 0u);
        }

        [Test]
        public void RecursiveComponentsRetainBothGroupsAndEveryEntryCall() {
            var result = Analyze(typeof(RepeatedRecursiveEntries));
            Assert.IsTrue((bool)result[0], result[2] as string);
            var counts = (IDictionary)result[1];
            Assert.AreEqual(2, counts.Count);
            AssertCount(counts, typeof(GroupA), 0u, 3u);
            AssertCount(counts, typeof(GroupB), 0u, 3u);
        }

        [TestCase(typeof(UnknownCallback), "Unresolved")]
        [TestCase(typeof(RecursiveUnknownCallback), "Unresolved")]
        [TestCase(typeof(OpenInterfaceCreation), "UnresolvedVirtualCall")]
        [TestCase(typeof(DelegateMulticastCreation), "Unresolved")]
        [TestCase(typeof(StaticCreationJob), "UnprovenTypeInitialization")]
        public void IncompleteILNeverPublishesPartialCounts(Type job, string reason) {
            var result = Analyze(job);
            Assert.IsFalse((bool)result[0]);
            Assert.IsNull(result[1]);
            StringAssert.Contains(reason, (string)result[2]);
        }

        [TestCase(typeof(IncompleteRepeated), 2u, 0u)]
        [TestCase(typeof(IncompleteGeneric<TestComponent>.Job), 2u, 0u)]
        [TestCase(typeof(IncompleteVirtual), 1u, 0u)]
        [TestCase(typeof(IncompleteMixedLoops), 1u, 1u)]
        public void CompatibilityCountsKeepKnownCallSitesWithoutClaimingCoverage(Type job, uint inline, uint loops) {
            Assert.IsFalse((bool)Analyze(job)[0], "Unknown callbacks must not become covered IL.");
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.ILJobEntityCounts", true);
            var args = new object[] { job, null };
            var known = (IDictionary)analyzer.GetMethod("AnalyzeKnown", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            StringAssert.Contains("Unresolved", (string)args[1]);
            AssertCount(known, typeof(GroupA), inline, loops);
        }

        [TestCase(typeof(IncompleteRepeated), 2, 0, "0", "2")]
        [TestCase(typeof(IncompleteGeneric<TestComponent>.Job), 2, 0, "0", "2")]
        [TestCase(typeof(IncompleteMixedLoops), 1, 1, "8", "8")]
        public void CompatibilityExportKeepsGroupOrderAndDoesNotMutateInstructions(Type job, int inline, int loops, string maximum, string reservation) {
            var generatorType = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var generator = Activator.CreateInstance(generatorType);
            generatorType.GetField("editorAssembly").SetValue(generator, true);
            var assemblies = generatorType.GetField("asms");
            assemblies.SetValue(generator, Activator.CreateInstance(assemblies.FieldType));
            // Registration order, not type name or analysis traversal order, owns IDs.
            generatorType.GetField("entityTypes").SetValue(generator, new System.Collections.Generic.List<Type> { typeof(GroupB), typeof(GroupA) });
            var instructions = job.GetMethod("Execute").GetInstructions()
                .Concat(typeof(Tests_ILJobEntityCounts).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).GetInstructions()).ToArray();
            var before = instructions.Select(instruction => (instruction.loopInfo.openCount, instruction.loopInfo.closeCount)).ToArray();
            for (var repeat = 0; repeat < 2; ++repeat) {
                var counts = generatorType.GetMethod("GetJobEntInfo").Invoke(null, new[] { (object)job, generator });
                CollectionAssert.AreEqual(new[] { 0, inline }, (int[])counts.GetType().GetField("count").GetValue(counts));
                CollectionAssert.AreEqual(new[] { false, loops > 0 }, (bool[])counts.GetType().GetField("loopGroups").GetValue(counts));
                Assert.AreEqual(loops, counts.GetType().GetField("brCount").GetValue(counts));
                var payload = (string)generatorType.GetMethod("GetEntityFallbackPayload", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(generator, new object[] { job });
                CollectionAssert.AreEqual(new[] { "v1", job.AssemblyQualifiedName, maximum, loops.ToString(System.Globalization.CultureInfo.InvariantCulture), "1",
                    typeof(GroupA).Assembly.FullName + "\tT:" + typeof(GroupA).FullName.Replace('+', '.') + "\t" + reservation }, payload.Split('\n'));
            }
            CollectionAssert.AreEqual(before, instructions.Select(instruction => (instruction.loopInfo.openCount, instruction.loopInfo.closeCount)).ToArray());
        }

        [Test]
        public void ILCountsDoNotInvokeOverwrittenDelegateAddresses() {
            var result = Analyze(typeof(DelegateOverwrittenCreation));
            Assert.IsTrue((bool)result[0], result[2] as string);
            Assert.IsEmpty((IDictionary)result[1]);
        }

        [TestCase(typeof(Repeated), true)]
        [TestCase(typeof(GenericContainer<TestComponent>.Job), true)]
        [TestCase(typeof(UnboundedLoop), true)]
        [TestCase(typeof(IncompleteRepeated), false)]
        [TestCase(typeof(IncompleteGeneric<TestComponent>.Job), false)]
        [TestCase(typeof(IncompleteMixedLoops), false)]
        public void ProductionEntitySelectionUsesILWithoutRequiringSourceMetadata(Type job, bool covered) {
            var editor = Assembly.Load("ME.BECS.Editor");
            var generatorType = editor.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var generator = Activator.CreateInstance(generatorType);
            generatorType.GetField("editorAssembly").SetValue(generator, true);
            var assemblies = generatorType.GetField("asms");
            assemblies.SetValue(generator, Activator.CreateInstance(assemblies.FieldType));
            generatorType.GetField("entityTypes").SetValue(generator, new System.Collections.Generic.List<Type> { typeof(GroupB), typeof(GroupA) });
            var args = new object[] { job, null };
            var actual = (string)generatorType.GetMethod("GetEntityInputPayload", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(generator, args);
            Assert.AreEqual(covered, args[1], "Unknown dispatch remains a compatibility IL snapshot, not complete coverage.");
            string expected;
            if (covered) {
                var countType = editor.GetType("ME.BECS.Editor.Jobs.ILJobEntityCounts", true);
                var counts = new object[] { job, null, null };
                Assert.IsTrue((bool)countType.GetMethod("TryGetPayload", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, counts));
                expected = (string)counts[1];
            } else {
                expected = (string)generatorType.GetMethod("GetEntityFallbackPayload", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(generator, new object[] { job });
            }
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ILCountsReserveEveryPossibleSingleCastTargetGroup() {
            var result = Analyze(typeof(DelegateBranchCreation));
            Assert.IsTrue((bool)result[0], result[2] as string);
            var counts = (IDictionary)result[1];
            Assert.AreEqual(2, counts.Count);
            AssertCount(counts, typeof(GroupA), 1u, 0u);
            AssertCount(counts, typeof(GroupB), 1u, 0u);
        }

        [TestCase(typeof(Repeated), "0", "0", "1", "2\t2\t0")]
        [TestCase(typeof(MixedLoops), "8", "2", "1", "8\t2\t2")]
        [TestCase(typeof(UnboundedLoop), "0", "1", "0", "0\t0\t1")]
        [TestCase(typeof(RecursiveCreation), "0", "1", "0", "0\t0\t1")]
        [TestCase(typeof(MutualRecursiveCreation), "9", "2", "1", "9\t1\t2")]
        [TestCase(typeof(GenericRecursiveCreation), "3", "1", "1", "3\t0\t1")]
        public void ILPayloadKeepsReservationsAndLoopOnlyGroups(Type job, string maximum, string loops, string allocate, string count) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.ILJobEntityCounts", true);
            var args = new object[] { job, null, null };
            Assert.IsTrue((bool)type.GetMethod("TryGetPayload", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), args[2] as string);
            CollectionAssert.AreEqual(new[] { "v1", job.AssemblyQualifiedName, maximum, loops, allocate,
                typeof(GroupA).Assembly.FullName + "\tT:" + typeof(GroupA).FullName.Replace('+', '.') + "\t" + count }, ((string)args[1]).Split('\n'));
        }

        private static void AssertCount(IDictionary counts, Type group, uint inline, uint loops) {
            Assert.IsTrue(counts.Contains(group));
            var count = counts[group];
            Assert.AreEqual(inline, count.GetType().GetField("inline", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(count));
            Assert.AreEqual(loops, count.GetType().GetField("loop", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(count));
        }
    }
}
