using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorContracts {
        public struct SafetyCatalogJob : ME.BECS.Jobs.IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent component) {
                component.data = 1;
            }
        }

        [Test]
        public void CompleteSafetySummaryExportsTypedComponentCatalog() {
            var summaries = typeof(SafetyCatalogJob).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobSafety.v1" && attribute.Value != null &&
                    attribute.Value.StartsWith(typeof(SafetyCatalogJob).FullName + "\n", StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual("0", summaries[0][2]);
            var catalogs = summaries[0].Where(row => row.StartsWith("A\t", StringComparison.Ordinal))
                .Select(row => row.Split('\t')).ToArray();
            Assert.AreEqual(1, catalogs.Length);
            Assert.AreEqual(5, catalogs[0].Length);
            Assert.AreEqual("GetTypes", catalogs[0][3]);
            Assert.AreEqual("v1", catalogs[0][4]);
            Assert.AreEqual(typeof(SafetyCatalogJob).Assembly.FullName, catalogs[0][1]);
            var type = typeof(SafetyCatalogJob).Assembly.GetType(catalogs[0][2], true);
            var getter = type.GetMethod("GetTypes", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(getter);
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, (Type[])getter.Invoke(null, null));
        }

        private static bool Condition() => true;
        private static bool Likely(bool condition) => condition;

        public static bool Hints() {
            var likely = Unity.Burst.CompilerServices.Hint.Likely(Condition());
            var unlikely = Unity.Burst.CompilerServices.Hint.Unlikely(Condition());
            Unity.Burst.CompilerServices.Hint.Assume(Condition());
            return likely | unlikely | Likely(Condition());
        }

        [Test]
        public void BurstHintsAreLeavesButTheirArgumentsAndUserMethodsAreNot() {
            const string owner = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.";
            var summaries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(owner + "Hints\n", StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual(string.Empty, summaries[0][2]);
            var calls = summaries[0].Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[0] == "call").ToArray();
            var hints = calls.Where(row => row[3].StartsWith("M:Unity.Burst.CompilerServices.Hint.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(3, hints.Length);
            foreach (var hint in hints) Assert.That(hint, Does.Contain("!ecs-leaf"));
            var arguments = calls.Where(row => row[3] == owner + "Condition").ToArray();
            Assert.AreEqual(4, arguments.Length, "Leaf contracts must not erase argument evaluation");
            foreach (var argument in arguments) Assert.That(argument, Does.Not.Contain("!ecs-leaf"));
            var user = calls.Where(row => row[3] == owner + "Likely(System.Boolean)").ToArray();
            Assert.AreEqual(1, user.Length);
            Assert.That(user[0], Does.Not.Contain("!ecs-leaf"));
        }
    }
}
