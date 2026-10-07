using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Views.Tests {
    public partial class Tests_Views_SourceSafety {
        // Discovery fixtures only: callbacks are never invoked by these tests.
        public sealed class CompilerIgnoredRead : GenericModule<ComponentA>, IViewTrackIgnore<ComponentA> { }
        public sealed class CompilerExplicitTracking : GenericModule<ComponentA>, IViewTrackIgnore<ComponentA>, IViewTrack<ComponentA>, IViewTrack<ComponentB> { }

        private static (string Kind, string[] Rows)[] TrackerMetadata(string profile, string key) => Tests_Views_Publications.SelectionAssembly(profile)
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == key).Select(attribute => attribute.Value.Split('\t'))
            .Select(row => (row[0], System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\n'))).ToArray();

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void TrackerInputsTransportCanonicalILSnapshots(string profile) {
            var inputs = TrackerMetadata(profile, "ME.BECS.ViewTrackerInputs.v1");
            var global = inputs.Single(entry => entry.Kind == "view-tracker").Rows;
            Assert.AreEqual("v3", global[0]);
            Assert.AreEqual(2, global.Length, "The compiler owns the global tracked component set and its order.");
            foreach (var entry in inputs.Where(entry => entry.Kind != "view-tracker")) {
                var owner = Type.GetType(entry.Rows[0], true);
                if (entry.Rows.Length == 2 && entry.Rows[1] == "ignored") {
                    Assert.IsTrue(typeof(IViewIgnoreTracker).IsAssignableFrom(owner));
                    continue;
                }
                var rows = entry.Rows.Skip(1).Select(row => row.Split('\t')).ToArray();
                Assert.AreEqual(2, rows.Count(row => row[0] == "S"));
                foreach (var phase in rows.Where(row => row[0] == "S")) {
                    Assert.AreEqual("il", phase[2], "Regenerate tracker inputs with compiled IL selection.");
                    var components = rows.Where(row => row[0] == "C" && row[1] == phase[1]).Select(row => row[2]).ToArray();
                    CollectionAssert.AreEqual(components.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(), components);
                }
            }
        }

        [TestCase(typeof(CompilerIgnoredRead), false)]
        [TestCase(typeof(CompilerExplicitTracking), true)]
        public void CompilerTrackingAppliesExplicitOptInAfterCallbackExclusions(Type owner, bool explicitlyTracked) {
            var selected = TrackerMetadata("Editor", "ME.BECS.ViewTrackerSelection.v1")
                .Single(entry => entry.Kind == "view-tracker-module" && entry.Rows[0] == owner.AssemblyQualifiedName).Rows;
            var expected = explicitlyTracked ? new[] { typeof(ComponentA), typeof(ComponentB) } : Array.Empty<Type>();
            CollectionAssert.AreEqual(expected.Select(type => type.AssemblyQualifiedName).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                selected.Skip(1).Where(value => value.Length != 0).ToArray());
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CompilerTrackerCatalogIsTheCanonicalUnionOfSelectedRoles(string profile) {
            var selected = TrackerMetadata(profile, "ME.BECS.ViewTrackerSelection.v1");
            var inputs = TrackerMetadata(profile, "ME.BECS.ViewTrackerInputs.v1");
            CollectionAssert.AreEqual(inputs.Where(entry => entry.Kind != "view-tracker").Select(entry => (entry.Kind, entry.Rows[0])).ToArray(),
                selected.Where(entry => entry.Kind != "view-tracker").Select(entry => (entry.Kind, entry.Rows[0])).ToArray(),
                "Compilation must retain selection order and the separate roles of dual-role owners.");
            var expected = selected.Where(entry => entry.Kind != "view-tracker").SelectMany(entry => entry.Rows.Skip(1))
                .Where(value => value.Length != 0).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, selected.Single(entry => entry.Kind == "view-tracker").Rows.Skip(2).Where(value => value.Length != 0).ToArray());
            foreach (var entry in selected.Where(entry => entry.Kind != "view-tracker" && typeof(IViewIgnoreTracker).IsAssignableFrom(Type.GetType(entry.Rows[0], true))))
                Assert.IsEmpty(entry.Rows.Skip(1).Where(value => value.Length != 0));
        }
    }
}
