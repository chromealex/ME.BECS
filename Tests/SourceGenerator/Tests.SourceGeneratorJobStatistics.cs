using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void JobStatisticsExportUsesILWithoutSourceCatalogSelection(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            Assert.IsFalse(records.Any(row => row.Length > 1 && row[1] == "job-entity-initializer"),
                "Fresh exports must not select a source entity-count initializer.");
            var ilPlans = records.Where(row => row.Length == 4 && row[1] == "job-entity-il")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            var compatibilityPlans = records.Where(row => row.Length == 4 && row[1] == "job-entity-fallback")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            var entityPlans = ilPlans.Concat(compatibilityPlans).ToArray();
            Assert.IsNotEmpty(entityPlans);
            CollectionAssert.AllItemsAreUnique(entityPlans.Select(plan => plan[1]).ToArray());
            foreach (var plan in ilPlans) {
                Assert.AreEqual("v1", plan[0]);
                Assert.IsNotNull(Type.GetType(plan[1], true));
                Assert.IsTrue(plan.Skip(5).All(row => row.Split('\t').Length == 5));
            }
            foreach (var plan in compatibilityPlans) {
                Assert.AreEqual("v1", plan[0]);
                Assert.IsNotNull(Type.GetType(plan[1], true));
                Assert.IsTrue(plan.Skip(5).All(row => row.Split('\t').Length == 3));
            }
            var weights = records.Where(row => row.Length == 6 && row[1] == "job-weight").ToArray();
            Assert.IsNotEmpty(weights);
            CollectionAssert.AreEquivalent(entityPlans.Select(plan => plan[1]).ToArray(), weights
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3]))).ToArray(),
                "Every selected job must have one IL entity reservation and one IL weight.");
            foreach (var row in weights) {
                Assert.AreEqual("il", row[4], "Fresh exports must use IL weights without source selection.");
                Assert.IsTrue(uint.TryParse(row[5], NumberStyles.None, CultureInfo.InvariantCulture, out _));
            }
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void EntityFallbackPlansPreserveExportedILSnapshot(string profile) {
            var attributes = Assembly.Load("ME.BECS.Gen." + profile).GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().ToArray();
            var plans = attributes.Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1")
                .Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length == 4 && row[1] == "job-entity-fallback")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            var selections = attributes.Where(attribute => attribute.Key == "ME.BECS.JobEntitySelection.v1")
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            foreach (var plan in plans) {
                var selected = selections.Single(row => row[1] == plan[1]);
                Assert.AreEqual("v1", plan[0]);
                Assert.AreEqual("legacy", selected[2],
                    "The selected compatibility IL result must not be replaced by a source initializer.");
                CollectionAssert.AreEqual(plan.Skip(2).ToArray(), selected.Skip(3).ToArray(),
                    "Preserve limits, allocation flag, counts and entity group order from the IL export.");
            }
        }

        [TestCase(typeof(Tests_ILJobEntityCounts.Repeated))]
        [TestCase(typeof(Tests_ILJobEntityCounts.MixedLoops))]
        [TestCase(typeof(Tests_ILJobEntityCounts.UnboundedLoop))]
        public void CoveredEntityJobsExportILWithoutASourceInitializer(Type job) {
            var selected = Assembly.Load("ME.BECS.Gen.Editor").GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.JobEntitySelection.v1")
                .Select(attribute => attribute.Value.Split('\n')).Single(plan => plan[1] == job.AssemblyQualifiedName);
            Assert.AreEqual("il", selected[2], "A complete fresh IL count must not be overridden by source metadata.");
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CompilerEntityStatisticsPreserveLimitsAndCanonicalGroupOrder(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var selected = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobEntitySelection.v1").Select(attribute => attribute.Value.Split('\n'))
                .Where(rows => rows[2] == "il").ToArray();
            Assert.IsNotEmpty(selected);
            var entityInputs = assembly.GetType("ME.BECS.SourceGenerated.EntityInputs", true);
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var plan in selected) {
                var job = Type.GetType(plan[1], true);
                var countType = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.ILJobEntityCounts", true);
                var args = new object[] { job, null, null };
                Assert.IsTrue((bool)countType.GetMethod("TryGetPayload", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args), args[2] as string);
                var il = ((string)args[1]).Split('\n');
                CollectionAssert.AreEqual(il.Skip(2).Take(3).Concat(il.Skip(5).Select(row => row.Split('\t'))
                    .Where(row => row[2] != "0").Select(row => string.Join("\t", row.Take(3)))), plan.Skip(3));
                foreach (var group in il.Skip(5).Select(row => row.Split('\t'))) {
                    var key = group[0] + "\t" + group[1];
                    Assert.IsNotNull(entityInputs.GetField("Id_" + (string)hash.Invoke(null, new object[] { key })),
                        "IL loop-only groups must remain in the selected global entity catalog.");
                }
            }
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CompilerWeightStatisticsPreserveFreshILValue(string profile) {
            var selected = Assembly.Load("ME.BECS.Gen." + profile).GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobWeightSelection.v1").Select(attribute => attribute.Value.Split('\n'))
                .ToArray();
            Assert.IsNotEmpty(selected);
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.ILJobWeights", true)
                .GetMethod("Analyze", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var plan in selected) {
                var job = Type.GetType(plan[1], true);
                Assert.AreEqual("il", plan[2]);
                Assert.IsEmpty(plan[3], "IL values must not dispatch a source initializer.");
                Assert.AreEqual(((uint)analyzer.Invoke(null, new object[] { job, null })).ToString(CultureInfo.InvariantCulture), plan[4]);
            }
        }
    }
}
