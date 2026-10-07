using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Inspect metadata only: never execute these callbacks or mutate a registry.
        public struct DestroyReadsOther : IComponentDestroy {
            public void Destroy(in Ent ent) { ent.Has<TestComponent>(); }
        }
        public struct DestroyExplicitWrites : IComponentDestroy {
            void IComponentDestroy.Destroy(in Ent ent) { ent.Set(new Test1Component()); }
            public void Destroy(in Ent ent) { ent.Set(new Test2Component()); }
        }
        public interface IDestroyMarker : IComponent { }
        public struct DestroyGenericWriter : IDestroyMarker, IComponentDestroy {
            public void Destroy(in Ent ent) { ent.Set(new Test3Component()); }
        }
        public partial struct DestroyGenericSystem<T> : IUpdate where T : unmanaged, IDestroyMarker {
            public void OnUpdate(ref SystemContext context) { }
            public partial struct RemoveJob : Unity.Jobs.IJob {
                public Ent ent;
                public void Execute() { this.ent.Remove<T>(); }
            }
        }
        public partial struct DestroySetJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { this.ent.Set(new DestroyReadsOther()); }
        }
        public partial struct DestroyRemoveJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { this.ent.Remove<DestroyExplicitWrites>(); }
        }
        public partial struct DestroyAllJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { this.ent.Destroy(); }
        }
        public struct DestroyCreatesEntity : IComponentDestroy {
            public void Destroy(in Ent ent) { Ent.New(); }
        }
        public partial struct DestroyRepeatedCreationJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                this.ent.Remove<DestroyCreatesEntity>();
                this.ent.Remove<DestroyCreatesEntity>();
            }
        }
        public partial struct DestroyLoopCreationJob : Unity.Jobs.IJob {
            public Ent ent;
            public int count;
            public void Execute() {
                this.ent.Remove<DestroyCreatesEntity>();
                for (var i = 0; i < this.count; ++i) this.ent.Remove<DestroyCreatesEntity>();
            }
        }
        public partial struct DestroySynchronizedSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                this.ent.Remove<DestroyExplicitWrites>();
            }
        }

        [TestCase(typeof(DestroySetJob), typeof(DestroyReadsOther), typeof(TestComponent), 0)]
        [TestCase(typeof(DestroyRemoveJob), typeof(DestroyExplicitWrites), typeof(Test1Component), 2)]
        [TestCase(typeof(DestroyGenericSystem<DestroyGenericWriter>.RemoveJob), typeof(DestroyGenericWriter), typeof(Test3Component), 2)]
        public void DefaultDestroyBodyContributesDependenciesButRegistryRemainsOpen(Type job, Type destroyed, Type accessed, int mode) {
            var reader = CreateSafetyReader(() => Assert.Fail("Metadata inspection must not read IL."));
            Assert.AreEqual("Incomplete", ReadJobSafety(reader, job, out var rows, out var dependencies));
            Assert.IsNull(dependencies);
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tUnclosedDestroyRegistry", StringComparison.Ordinal)));
            var expected = new[] { SafetyExceptionDependency(destroyed, 2), SafetyExceptionDependency(accessed, mode) }
                .OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, rows.Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).OrderBy(row => row, StringComparer.Ordinal).ToArray());
            Assert.IsFalse(rows.Any(row => row.StartsWith("A\t", StringComparison.Ordinal)), "Do not publish a partial dependency catalog as complete.");
        }

        private static string[] DestroyJobSummary(Type job, string key) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key &&
                attribute.Value.StartsWith(job.FullName + "\n", StringComparison.Ordinal)).Value.Split('\n');
        }

        [TestCase(typeof(DestroyAllJob))]
        [TestCase(typeof(DestroySetJob))]
        [TestCase(typeof(DestroyRemoveJob))]
        public void DestroyDispatchCannotDisappearBehindIgnoredEngineMethods(Type job) {
            foreach (var key in new[] { "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = DestroyJobSummary(job, key);
                Assert.AreNotEqual("0", rows[2], key);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tUnclosedDestroyRegistry", StringComparison.Ordinal)), key);
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)), key);
            }
        }

        [TestCase(typeof(DestroyRepeatedCreationJob), "2", "0")]
        [TestCase(typeof(DestroyLoopCreationJob), "1", "1")]
        public void DefaultDestroyCreationRetainsCallMultiplicityAndLoopContext(Type job, string inline, string loop) {
            var rows = DestroyJobSummary(job, "ME.BECS.JobEntityCounts.v1");
            Assert.IsTrue(rows.Select(row => row.Split('\t')).Any(row => row.Length == 5 && row[0] == "C" && row[3] == inline && row[4] == loop), string.Join("\n", rows));
            Assert.AreNotEqual("0", rows[2], "Default-target counts are provisional until registry dispatch is closed.");
            Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
        }

        [Test]
        public void CompletingInputHandleDoesNotCertifyUnknownDestroyCallbacks() {
            var rows = SynchronizationSummary(typeof(DestroySynchronizedSystem));
            CollectionAssert.Contains(rows, "S\tincomplete");
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tUnclosedDestroyRegistry", StringComparison.Ordinal)));
        }
    }
}
