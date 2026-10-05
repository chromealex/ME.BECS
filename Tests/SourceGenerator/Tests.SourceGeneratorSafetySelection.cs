using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct SafetyExplicitSelectionJob : IJobForComponents<TestComponent> {
            void IJobForComponents<TestComponent>.Execute(in JobInfo info, in Ent ent, ref TestComponent component) => component.data = 1;
            // Unrelated overload: must not be selected instead of the interface slot.
            public void Execute(int unused) => default(Ent).Set(new Test1Component());
        }

        [TestCase(typeof(SafetyExplicitSelectionJob), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(typeof(WriteOnlyQueryArgumentJob), typeof(TestComponent), RefOp.WriteOnly)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.ReadOnlySafetyJob), typeof(AotMarker), RefOp.ReadOnly)]
        public void ILSafetyUsesExactExecuteContract(Type job, Type component, RefOp mode) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = analyzer.GetMethod("GetJobTypesInfo").Invoke(null, new object[] { job, null });
            var records = SafetySelectionRecords(result);
            CollectionAssert.AreEqual(new[] { "D\t" + component.Assembly.FullName + "\tT:" + component.FullName.Replace('+', '.') +
                "\t" + (int)mode + "\t1" }, records);
        }

        [TestCase(typeof(SafetyConstructorEffectsJob), "2,2,2,0")]
        [TestCase(typeof(SafetyImplicitConstructorEffectsJob), "2,-,-,0")]
        [TestCase(typeof(SafetyCatchFilterJob), "2,0,2,2")]
        public void ILSafetyIncludesConstructorsAndExceptionRegions(Type job, string modes) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = analyzer.GetMethod("GetJobTypesInfo").Invoke(null, new object[] { job, null });
            var components = new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) };
            var expected = modes.Split(',').Select((mode, index) => mode == "-" ? null :
                "D\t" + components[index].Assembly.FullName + "\tT:" + components[index].FullName.Replace('+', '.') + "\t" + mode + "\t0")
                .Where(row => row != null).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(result));
        }

        // Metadata fixtures only; these bodies are never executed by the tests.
        public partial struct SafetyIncompleteSelectionJob : Unity.Jobs.IJob {
            public static Action callback;
            public void Execute() => callback();
        }
        public struct SafetyMissingSelectionJob { }

        private static Func<Type, T> CreateSafetyFallback<T>(Action visited) => job => {
            visited();
            return Activator.CreateInstance<T>();
        };

        private static object CreateSafetyReader(Action legacyVisited) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorJobSafety", true);
            var resultType = type.GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance).ReturnType;
            var factory = typeof(Tests_SourceGeneratorContracts).GetMethod(nameof(CreateSafetyFallback), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(resultType);
            var callback = factory.Invoke(null, new object[] { legacyVisited });
            return type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single(ctor => ctor.GetParameters().Length == 1)
                .Invoke(new[] { callback });
        }

        private static object SelectJobSafety(object reader, Type job) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return reader.GetType().GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, new object[] { job });
        }

        private static string ReadJobSafety(object reader, Type job, out string[] rows, out object dependencies) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var args = new object[] { job, null, null, null };
            var status = reader.GetType().GetMethod("ReadSource", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, args);
            rows = args[1] as string[];
            dependencies = args[2];
            return status.ToString();
        }

        private static string[] SafetySelectionRecords(object dependencies) =>
            ((System.Collections.IEnumerable)dependencies).Cast<object>().Select(item => {
                var type = (Type)item.GetType().GetField("type").GetValue(item);
                var mode = (RefOp)item.GetType().GetField("op").GetValue(item);
                var argument = (bool)item.GetType().GetField("isArg").GetValue(item);
                return "D\t" + type.Assembly.FullName + "\tT:" + type.FullName.Replace('+', '.') + "\t" + (int)mode + "\t" + (argument ? "1" : "0");
            }).OrderBy(row => row, StringComparer.Ordinal).ToArray();

        [TestCase(typeof(SafetyCatalogJob))]
        [TestCase(typeof(NativeBoolSizeJob))]
        [TestCase(typeof(WriteOnlyQueryArgumentJob))]
        [TestCase(typeof(SafetyExplicitSelectionJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.ReadOnlySafetyJob))]
        public void CompleteJobSafetyUsesTypedSourceWithoutReadingIL(Type job) {
            var reader = CreateSafetyReader(() => Assert.Fail("Complete job safety must not invoke the legacy IL analyzer."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out var source));
            var expected = rows.Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(source));
            var selected = SelectJobSafety(reader, job);
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(selected));
            selected.GetType().GetMethod("Clear").Invoke(selected, null);
            rows[2] = "corrupted caller copy";
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)), "Caller mutation must not affect the run-local cache.");
        }

        [TestCase(typeof(SafetyMissingSelectionJob), "Missing")]
        [TestCase(typeof(SafetyIncompleteSelectionJob), "Incomplete")]
        public void MissingOrIncompleteJobSafetyFallsBackOncePerRun(Type job, string status) {
            var calls = 0;
            var reader = CreateSafetyReader(() => ++calls);
            Assert.AreEqual(status, ReadJobSafety(reader, job, out _, out var dependencies));
            Assert.IsNull(dependencies, "Incomplete metadata must never expose a partial source dependency set.");
            var first = SelectJobSafety(reader, job);
            var second = SelectJobSafety(reader, job);
            Assert.AreEqual(1, calls);
            Assert.AreNotSame(first, second);
            CollectionAssert.IsEmpty(SafetySelectionRecords(first));
            CollectionAssert.IsEmpty(SafetySelectionRecords(second));
        }

        // Inject corrupt snapshots into a fresh reader, never into assembly attributes
        // or global state. Exercise the real selection gate, not just the row parser.
        private static void ReplaceSafetySnapshot(object reader, Type job, string[] rows) {
            if (job.IsGenericType) {
                var closed = reader.GetType().GetField("closed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader);
                var entries = (System.Collections.IDictionary)closed.GetType().GetField("rows", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(closed);
                entries["ME.BECS.JobSafety.v1\n" + job.AssemblyQualifiedName] = rows;
            } else {
                var catalogs = (System.Collections.IDictionary)reader.GetType().GetField("catalogs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader);
                ((System.Collections.IDictionary)catalogs[job.Assembly])[job.FullName] = rows;
            }
        }

        [TestCase(typeof(SafetyCatalogJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob))]
        public void CorruptAndConflictingSafetyCannotSilentlyUseLegacy(Type job) {
            var baseline = CreateSafetyReader(() => Assert.Fail("Unexpected IL call."));
            Assert.AreEqual("Complete", ReadJobSafety(baseline, job, out var rows, out _));
            var dependency = rows.Single(row => row.StartsWith("D\t", StringComparison.Ordinal));
            var mutations = new[] {
                (string[])null, // Duplicate/conflicting publishers are stored as null.
                new[] { rows[0] },
                rows.Select((row, index) => index == 2 ? "00" : row).ToArray(),
                rows.Concat(new[] { "G\tunreported coverage gap" }).ToArray(),
                rows.Where(row => !row.StartsWith("A\t", StringComparison.Ordinal)).ToArray(),
                rows.Select(row => row == dependency ? row.Replace("\t2\t", "\t99\t") : row).ToArray(),
                rows.Concat(new[] { dependency }).ToArray(),
            };
            foreach (var mutation in mutations) {
                var reader = CreateSafetyReader(() => Assert.Fail("Corruption must stop export, not fall back to IL."));
                Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
                ReplaceSafetySnapshot(reader, job, mutation);
                Assert.AreEqual("Invalid", ReadJobSafety(reader, job, out _, out var dependencies));
                Assert.IsNull(dependencies);
                var exception = Assert.Throws<TargetInvocationException>(() => SelectJobSafety(reader, job));
                Assert.IsInstanceOf<InvalidOperationException>(exception.GetBaseException());
                StringAssert.Contains("Invalid source safety catalog", exception.GetBaseException().Message);
            }
        }

        [Test]
        public void JobSafetyComparisonRemainsSeparateFromSourceSelection() {
            var job = typeof(SafetyCatalogJob);
            var reader = CreateSafetyReader(() => Assert.Fail("Source selection must not use the comparison baseline."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out var source));
            var emptyLegacy = Activator.CreateInstance(source.GetType());
            var compare = reader.GetType().GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.AreEqual(0, compare.Invoke(null, new[] { (object)job, rows, emptyLegacy, null }), "An independent empty baseline must still report a difference.");
            CollectionAssert.AreEqual(SafetySelectionRecords(source), SafetySelectionRecords(SelectJobSafety(reader, job)));
        }
    }
}
