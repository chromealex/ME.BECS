using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only. These fixtures must never be scheduled or invoked.
        public struct CreationOnDispose : IDisposable {
            public void Dispose() { Ent.New(); }
        }

        public partial struct FinallyOnceCreationJob : Unity.Jobs.IJob {
            public void Execute() { try { } finally { Ent.New(); } }
        }
        public partial struct FinallyReturnCreationJob : Unity.Jobs.IJob {
            public void Execute() { try { return; } finally { Ent.New(); } }
        }
        public partial struct FinallyThrowCreationJob : Unity.Jobs.IJob {
            public void Execute() { try { throw null; } finally { Ent.New(); } }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyRepeatingCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try { } finally { Ent.New(); }
                }
            }
        }
        public partial struct FinallySurroundingLoopCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                try { while (this.count > 0) --this.count; }
                finally { Ent.New(); }
            }
        }
        public partial struct FinallyReturnFromLoopCreationJob : Unity.Jobs.IJob {
            public int count;
            public bool stop;
            public void Execute() {
                try {
                    for (var i = 0; i < this.count; ++i) if (this.stop) return;
                } finally { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyGotoCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                again:
                try { if (this.count-- > 0) goto again; }
                finally { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyContinueBreakCreationJob : Unity.Jobs.IJob {
            public int count;
            public bool skip, stop;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try {
                        if (this.skip) continue;
                        if (this.stop) break;
                        Ent.New();
                    } finally { Ent.New(); }
                }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyNestedRepeatingCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try { } finally {
                        try { Ent.New(); } finally { Ent.New(); }
                    }
                }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyOuterAndInnerCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                try {
                    for (var i = 0; i < this.count; ++i) {
                        try { } finally { Ent.New(); }
                    }
                } finally { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyInternalLoopCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                try { } finally {
                    for (var i = 0; i < this.count; ++i) Ent.New();
                    Ent.New();
                }
            }
        }
        public static void CreateInFinally() { try { } finally { Ent.New(); } }
        public static Ent CreateInFinallyInitializer() { try { return default; } finally { Ent.New(); } }
        public class FinallyCountConstructor {
            public Ent value = CreateInFinallyInitializer();
            public FinallyCountConstructor() : this(0) { }
            public FinallyCountConstructor(int unused) { try { } finally { Ent.New(); } }
        }
        public class FinallyCountImplicitConstructor {
            public Ent value = CreateInFinallyInitializer();
        }
        public partial struct FinallyConstructorCreationJob : Unity.Jobs.IJob {
            public void Execute() { _ = new FinallyCountConstructor(); }
        }
        public partial struct FinallyImplicitConstructorCreationJob : Unity.Jobs.IJob {
            public void Execute() { _ = new FinallyCountImplicitConstructor(); }
        }
        public partial struct FinallyRepeatedHelperCreationJob : Unity.Jobs.IJob {
            public void Execute() { CreateInFinally(); CreateInFinally(); }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FinallyMixedHelperCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                CreateInFinally();
                for (var i = 0; i < this.count; ++i) CreateInFinally();
            }
        }
        public partial struct UsingCreationJob : Unity.Jobs.IJob {
            public void Execute() { using (default(CreationOnDispose)) { } }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct UsingRepeatingCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) { using var value = default(CreationOnDispose); }
            }
        }
        public partial struct UsingSurroundingLoopCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                using var value = default(CreationOnDispose);
                while (this.count > 0) --this.count;
            }
        }
        public partial struct FinallyCatchCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try { throw null; }
                catch (System.Exception) { Ent.New(); }
                finally { Ent.New(); }
            }
        }
        public partial struct FinallyUnknownCreationJob : Unity.Jobs.IJob {
            public static Action callback;
            public void Execute() { try { } finally { callback(); } }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            [EntitiesJobMaxCount(8)]
            public partial struct FinallyCountsJob : Unity.Jobs.IJob {
                public int count;
                public void Execute() {
                    CreateInFinally();
                    for (var i = 0; i < this.count; ++i) {
                        try { } finally { CreateInFinally(); }
                    }
                }
            }
        }

        private static string[] FinallyJobSummary(Type job, string kind) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var owner = job.IsGenericType ? job.GetGenericArguments()[0].Assembly : job.Assembly;
            var identity = job.IsGenericType ? job.AssemblyQualifiedName : job.FullName;
            return owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "ME.BECS." + kind + ".v1" &&
                    attribute.Value.StartsWith(identity + "\n", StringComparison.Ordinal)).Value.Split('\n');
        }

        [TestCase(typeof(FinallyOnceCreationJob), "1", "0")]
        [TestCase(typeof(FinallyReturnCreationJob), "1", "0")]
        [TestCase(typeof(FinallyThrowCreationJob), "1", "0")]
        [TestCase(typeof(FinallyRepeatingCreationJob), "0", "1")]
        [TestCase(typeof(FinallySurroundingLoopCreationJob), "1", "0")]
        [TestCase(typeof(FinallyReturnFromLoopCreationJob), "1", "0")]
        [TestCase(typeof(FinallyGotoCreationJob), "0", "1")]
        [TestCase(typeof(FinallyContinueBreakCreationJob), "0", "2")]
        [TestCase(typeof(FinallyNestedRepeatingCreationJob), "0", "2")]
        [TestCase(typeof(FinallyOuterAndInnerCreationJob), "1", "1")]
        [TestCase(typeof(FinallyInternalLoopCreationJob), "1", "1")]
        [TestCase(typeof(FinallyRepeatedHelperCreationJob), "2", "0")]
        [TestCase(typeof(FinallyMixedHelperCreationJob), "1", "1")]
        [TestCase(typeof(FinallyConstructorCreationJob), "2", "0")]
        [TestCase(typeof(FinallyImplicitConstructorCreationJob), "1", "0")]
        [TestCase(typeof(UsingCreationJob), "1", "0")]
        [TestCase(typeof(UsingRepeatingCreationJob), "0", "1")]
        [TestCase(typeof(UsingSurroundingLoopCreationJob), "1", "0")]
        [TestCase(typeof(FinallyCatchCreationJob), "2", "0")]
        [TestCase(typeof(GenericAotSystem<AotMarker>.FinallyCountsJob), "1", "1")]
        public void FinallyEntityCountsDistinguishRepeatedAndOneShotRegions(Type job, string inline, string loop) {
            var rows = FinallyJobSummary(job, "JobEntityCounts");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var counts = rows.Select(row => row.Split('\t')).Single(row => row[0] == "C");
            Assert.AreEqual(inline, counts[3]);
            Assert.AreEqual(loop, counts[4]);
            Assert.IsTrue(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [Test]
        public void CatchAndFinallyContributeBothStaticWeights() {
            var weight = FinallyJobSummary(typeof(FinallyCatchCreationJob), "JobWeights");
            Assert.IsFalse(weight.Any(row => row.StartsWith("G\tExceptionControlFlow", StringComparison.Ordinal)));
            CollectionAssert.Contains(weight, "W\tME.BECS.Ent.NewEnt_INTERNAL\t20");
        }

        [Test]
        public void UnknownFinallyCallbackIsNotAnEmptyEffect() {
            foreach (var kind in new[] { "JobEntityCounts", "JobWeights" }) {
                var rows = FinallyJobSummary(typeof(FinallyUnknownCreationJob), kind);
                Assert.AreNotEqual("0", rows[2]);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke", StringComparison.Ordinal)));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }
    }
}
