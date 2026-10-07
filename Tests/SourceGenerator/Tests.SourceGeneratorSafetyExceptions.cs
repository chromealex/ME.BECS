using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only. Do not execute: some bodies deliberately throw,
        // allocate managed objects or invoke unknown callbacks.
        public partial struct SafetyTryFinallyJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                try { this.ent.Set(new TestComponent()); }
                finally { this.ent.Set(new Test1Component()); }
            }
        }

        public partial struct SafetyCatchFilterJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                try { this.ent.Set(new TestComponent()); }
                catch (System.Exception) when (this.ent.Has<Test1Component>()) { this.ent.Set(new Test2Component()); }
                finally { this.ent.Set(new Test3Component()); }
            }
        }

        public partial struct SafetyReturnFinallyJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                try { return; }
                finally { this.ent.Set(new Test2Component()); }
            }
        }

        public partial struct SafetyNestedRethrowJob : Unity.Jobs.IJob {
            public Ent ent;
            public static System.Exception error;
            public void Execute() {
                try {
                    try { this.ent.Set(new TestComponent()); throw error; }
                    catch (System.Exception) when (this.ent.Has<Test1Component>()) { this.ent.Set(new Test2Component()); throw; }
                    finally { this.ent.Has<Test2Component>(); }
                } catch (System.Exception) { this.ent.Set(new Test3Component()); }
                finally { this.ent.Set(new Test1Component()); }
            }
        }

        public partial struct SafetyLoopFinallyJob : Unity.Jobs.IJob {
            public Ent ent;
            public int count;
            public bool skip;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try { if (this.skip) continue; this.ent.Set(new TestComponent()); }
                    finally { this.ent.Set(new Test1Component()); }
                }
            }
        }

        public static bool SafetyExceptionInitializer(Ent ent) {
            try { return ent.Set(new TestComponent()); }
            finally { ent.Has<Test3Component>(); }
        }

        public class SafetyExceptionConstructor {
            public bool initialized = SafetyExceptionInitializer(default);
            public SafetyExceptionConstructor(Ent ent) {
                try { ent.Set(new Test1Component()); }
                finally { ent.Set(new Test2Component()); }
            }
        }

        public class SafetyExceptionImplicitConstructor {
            public bool initialized = SafetyExceptionInitializer(default);
        }

        public partial struct SafetyConstructorEffectsJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { _ = new SafetyExceptionConstructor(this.ent); }
        }

        public partial struct SafetyImplicitConstructorEffectsJob : Unity.Jobs.IJob {
            public void Execute() { _ = new SafetyExceptionImplicitConstructor(); }
        }

        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            public partial struct ExceptionSafetyJob : Unity.Jobs.IJob {
                public Ent ent;
                public void Execute() {
                    try { this.ent.Set(default(T)); }
                    finally { this.ent.Has<TestComponent>(); }
                }
            }
        }

        public partial struct SafetyUnknownCatchJob : Unity.Jobs.IJob {
            public Ent ent;
            public static Action callback;
            public void Execute() {
                try { this.ent.Set(new TestComponent()); }
                catch (System.Exception) { callback(); }
            }
        }

        private static string SafetyExceptionDependency(Type component, int mode) =>
            "D\t" + component.Assembly.FullName + "\tT:" + component.FullName.Replace('+', '.') + "\t" + mode + "\t0";

        [TestCase(typeof(SafetyTryFinallyJob), "2,2,-,-")]
        [TestCase(typeof(SafetyCatchFilterJob), "2,0,2,2")]
        [TestCase(typeof(SafetyReturnFinallyJob), "-,-,2,-")]
        [TestCase(typeof(SafetyNestedRethrowJob), "2,2,2,2")]
        [TestCase(typeof(SafetyLoopFinallyJob), "2,2,-,-")]
        [TestCase(typeof(SafetyConstructorEffectsJob), "2,2,2,0")]
        [TestCase(typeof(SafetyImplicitConstructorEffectsJob), "2,-,-,0")]
        public void ExceptionSafetyIncludesEveryRegionWithoutReadingIL(Type job, string modes) {
            var components = new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) };
            var expected = modes.Split(',').Select((mode, index) => mode == "-" ? null : SafetyExceptionDependency(components[index], int.Parse(mode)))
                .Where(row => row != null).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            var reader = CreateSafetyReader(() => Assert.Fail("A complete exceptional effect union must not need IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void ClosedGenericExceptionSafetySubstitutesAllRegionEffects() {
            var reader = CreateSafetyReader(() => Assert.Fail("Closed generic exceptional effects must not need IL."));
            var job = typeof(GenericAotSystem<AotMarker>.ExceptionSafetyJob);
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
            var expected = new[] { SafetyExceptionDependency(typeof(AotMarker), 2), SafetyExceptionDependency(typeof(TestComponent), 0) }
                .OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void UnknownCallbackInCatchStillRequiresFallback() {
            var calls = 0;
            var reader = CreateSafetyReader(() => ++calls);
            Assert.AreEqual("Incomplete", ReadJobSafety(reader, typeof(SafetyUnknownCatchJob), out var rows, out var source));
            Assert.IsNull(source);
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke", StringComparison.Ordinal)));
            Assert.IsFalse(rows.Any(row => row.StartsWith("G\tExceptionControlFlow", StringComparison.Ordinal)));
            SelectJobSafety(reader, typeof(SafetyUnknownCatchJob));
            Assert.AreEqual(1, calls);
        }

        [TestCase(typeof(SafetyTryFinallyJob), true)]
        [TestCase(typeof(SafetyCatchFilterJob), true)]
        [TestCase(typeof(SafetyLoopFinallyJob), true)]
        public void ExceptionCountsAndWeightsRequireTheirOwnContracts(Type job, bool exceptionCounts) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var attributes = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var methodId = "M:" + job.FullName.Replace('+', '.') + ".Execute";
            var raw = attributes.Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                attribute.Value.StartsWith(methodId + "\n", StringComparison.Ordinal)).Value.Split('\n');
            CollectionAssert.Contains(raw[1].Split(','), "effect-union-schema=1");
            CollectionAssert.Contains(raw[2].Split(','), "ExceptionControlFlow");
            Assert.AreEqual(exceptionCounts, raw[1].Split(',').Any(flag =>
                flag == "exception-count-schema=1" || flag == "filter-count-schema=1"));
            Assert.AreEqual(job == typeof(SafetyCatchFilterJob), raw[1].Split(',').Contains("filter-count-schema=1"));
            foreach (var key in new[] { "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var summary = attributes.Single(attribute => attribute.Key == key &&
                    attribute.Value.StartsWith(job.FullName + "\n", StringComparison.Ordinal)).Value.Split('\n');
                // Weight sums reachable call-site costs; entity reservations also
                // need a proof of which exception regions can repeat.
                var unresolved = key == "ME.BECS.JobEntityCounts.v1" && !exceptionCounts;
                Assert.AreEqual(unresolved, summary.Any(row => row.StartsWith("G\tExceptionControlFlow", StringComparison.Ordinal)), key);
            }
        }
    }
}
