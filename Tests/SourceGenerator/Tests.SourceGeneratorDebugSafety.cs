using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static string[][] CompiledDebugSafetyPlans(string profile) =>
            Tests_SourceGeneratorJobDebugPublications.Owners(profile).Values.Select(type => type.Assembly).Distinct()
                .SelectMany(assembly => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>())
                .Where(attribute => attribute.Key == "ME.BECS.PublishedDebugJobSafety." + profile + ".v1")
                .Select(attribute => attribute.Value.Split('\n')).ToArray();

        [TestCase(typeof(SafetyCatalogJob))]
        [TestCase(typeof(NativeBoolSizeJob))]
        [TestCase(typeof(DebugNoArgumentsJob))]
        [TestCase(typeof(WriteOnlyQueryArgumentJob))]
        [TestCase(typeof(SafetyExplicitSelectionJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.ReadOnlySafetyJob))]
        public void DebugSafetySelectionUsesExportedILSnapshot(Type job) {
            var plans = CompiledDebugSafetyPlans("Editor").Where(plan => plan[1] == job.AssemblyQualifiedName).ToArray();
            Assert.IsNotEmpty(plans, "Regenerate inputs with IL debug safety selection.");
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var dependencies = analyzer.GetMethod("GetJobTypesInfo").Invoke(null, new object[] { job, null });
            var expected = ((System.Collections.IEnumerable)dependencies).Cast<object>().Select(item => {
                var type = (Type)item.GetType().GetField("type").GetValue(item);
                var mode = (RefOp)item.GetType().GetField("op").GetValue(item);
                return (type, row: "S\t" + mode + "\t" + type.AssemblyQualifiedName);
            }).OrderBy(item => item.type.FullName, StringComparer.Ordinal)
                .ThenBy(item => item.type.Assembly.FullName, StringComparer.Ordinal).Select(item => item.row).ToArray();
            foreach (var plan in plans) {
                Assert.AreEqual("v1", plan[0]);
                Assert.AreEqual("il", plan[3]);
                CollectionAssert.AreEqual(expected, plan.Skip(4).ToArray());
            }
            var exported = Tests_SourceGeneratorInputCatalog.Rows(true).Select(row => row.Split('\t'))
                .Where(row => row.Length == 3 && row[0] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\n'))
                .Where(plan => plan[1] == job.AssemblyQualifiedName).ToArray();
            Assert.AreEqual(plans.Length, exported.Length);
            foreach (var plan in exported) {
                Assert.AreEqual("v2", plan[0]);
                CollectionAssert.AreEqual(new[] { "S\til" }.Concat(expected).ToArray(),
                    plan.Skip(5).Where(row => row.StartsWith("S\t", StringComparison.Ordinal)).ToArray(),
                    "Roslyn must use the validated IL snapshot, not replace it with a source summary.");
            }
        }

        [TestCase(typeof(SafetyCatalogJob), false)]
        [TestCase(typeof(SafetyMissingSelectionJob), false)]
        [TestCase(typeof(SafetyIncompleteSelectionJob), false)]
        [TestCase(typeof(FinallyAmbiguousQueryJob), false)]
        public void DebugSafetyExportRetainsSelectionOriginAndIndependentCache(Type job, bool expectedSource) {
            var calls = 0;
            var reader = CreateSafetyReader(() => ++calls);
            var method = reader.GetType().GetMethod("SelectForExport", BindingFlags.NonPublic | BindingFlags.Instance);
            var args = new object[] { job, null };
            var first = method.Invoke(reader, args);
            Assert.AreEqual(expectedSource, args[1]);
            var expected = SafetySelectionRecords(first);
            first.GetType().GetMethod("Clear").Invoke(first, null);
            var second = method.Invoke(reader, args);
            Assert.AreEqual(expectedSource, args[1]);
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(second));
            Assert.AreNotSame(first, second);
            Assert.AreEqual(1, calls);
        }

        [TestCase(typeof(SafetyCatalogJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob))]
        public void InvalidSourceSafetyCannotBlockILExport(Type job) {
            var calls = 0;
            var reader = CreateSafetyReader(() => ++calls);
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
            ReplaceSafetySnapshot(reader, job, new[] { "damaged source catalog" });
            Assert.AreEqual("Invalid", ReadJobSafety(reader, job, out _, out _), "Comparison must still report damaged metadata.");
            var args = new object[] { job, null };
            var exported = reader.GetType().GetMethod("SelectForExport", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, args);
            Assert.AreEqual(false, args[1]);
            Assert.AreEqual(1, calls);
            CollectionAssert.IsEmpty(SafetySelectionRecords(exported));
        }

        [Test]
        public void AmbiguousSourceSafetyExportsBothExecuteContractsFromIL() {
            var job = typeof(FinallyAmbiguousQueryJob);
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorJobSafety", true);
            var reader = Activator.CreateInstance(type, true);
            // Load the diagnostic cache, then reproduce a conflicting publisher.
            // Use the real IL analyzer here, not the empty test fallback.
            ReadJobSafety(reader, job, out _, out _);
            ReplaceSafetySnapshot(reader, job, null);
            Assert.AreEqual("Invalid", ReadJobSafety(reader, job, out _, out _));

            var args = new object[] { job, null };
            var exported = type.GetMethod("SelectForExport", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, args);
            Assert.AreEqual(false, args[1]);
            var expected = new[] {
                (component: typeof(TestComponent), argument: "1"),
                (component: typeof(Test1Component), argument: "0"),
            }.Select(item => "D\t" + item.component.Assembly.FullName + "\tT:" + item.component.FullName.Replace('+', '.') +
                "\t" + (int)RefOp.ReadWrite + "\t" + item.argument).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(exported),
                "Both implemented Execute slots must contribute; no source dependency subset may replace the IL union.");
        }

        [Test]
        public void ILSafetyFailureCannotFallBackToSource() {
            var reader = CreateSafetyReader(() => throw new InvalidOperationException("IL analysis failed"));
            var exception = Assert.Throws<TargetInvocationException>(() => reader.GetType()
                .GetMethod("SelectForExport", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(reader, new object[] { typeof(SafetyCatalogJob), null }));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            StringAssert.Contains("IL analysis failed", exception.InnerException.Message);
        }

        [Test]
        public void DebugComponentOnlyTNullJobDoesNotInventAnAspectArgument() {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var generator = Activator.CreateInstance(type);
            type.GetField("editorAssembly").SetValue(generator, true);
            var assemblies = type.GetField("asms");
            assemblies.SetValue(generator, Activator.CreateInstance(assemblies.FieldType));
            var plan = type.GetMethod("CreateDebugWrapperPlan", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(generator, new object[] { typeof(Tests_ILJobEntityCounts.GenericContainer<TNull>.Job),
                    typeof(IJobForComponentsBase), typeof(IComponentBase), null });
            Assert.AreEqual(true, plan.GetType().GetField("hasTypedArguments").GetValue(plan));
            CollectionAssert.AreEqual(new[] { typeof(TNull) }, (Type[])plan.GetType().GetField("components").GetValue(plan));
            CollectionAssert.IsEmpty((Type[])plan.GetType().GetField("aspects").GetValue(plan));
        }

        [TestCase(typeof(FinallyAmbiguousQueryJob), 3u)]
        [TestCase(typeof(FinallyExplicitQueryJob), 1u)]
        [TestCase(typeof(FinallyGenericExplicitScheduleSystem<AotMarker>.Job), 1u)]
        [TestCase(typeof(SafetyExplicitSelectionJob), 1u)]
        public void LegacyStatisticsUseExactExecuteContracts(Type job, uint expectedWeight) {
            // Exercise the compatibility path as well as the new analyzers. No
            // job body, bootstrap, asset lookup or runtime initializer is executed.
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var generator = Activator.CreateInstance(type);
            type.GetField("entityTypes").SetValue(generator, new System.Collections.Generic.List<Type>());
            var counts = type.GetMethod("GetJobEntInfo").Invoke(null, new[] { (object)job, generator });
            Assert.IsNull(counts.GetType().GetField("count").GetValue(counts));
            Assert.AreEqual(0, counts.GetType().GetField("brCount").GetValue(counts));
            var payload = type.GetMethod("GetEntityFallbackPayload", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(generator, new object[] { job });
            Assert.AreEqual("v1\n" + job.AssemblyQualifiedName + "\n0\n0\n0", payload,
                "A selected IL fallback must not inspect the ambiguous source catalog again.");
            var weights = type.GetMethod("GetJobWeightsInfo").Invoke(null, new object[] { job, null });
            Assert.AreEqual(expectedWeight, weights.GetType().GetField("weight").GetValue(weights),
                "Unrelated Execute overloads must not contribute; every implemented job contract must be analyzed.");
        }
    }
}
