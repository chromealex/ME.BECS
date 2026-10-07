using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [Test]
        public void CompilerGraphSystemInjectionsMatchFieldsAndFirstRegisteredTargets() {
            var attributes = Tests_SourceGeneratorGraphPublications.PublishedMetadata();
            var inputs = Tests_SourceGeneratorInputCatalog.Rows(false).Select(row => ("runtime\t" + row).Split('\t')).ToArray();
            string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var slots = inputs.Where(row => row[1] == "graph-system").Select(row => (
                graph: int.Parse(row[4], CultureInfo.InvariantCulture),
                slot: int.Parse(row[5], CultureInfo.InvariantCulture),
                type: Type.GetType(Decode(row[3]), true))).ToArray();
            var schema = inputs.Single(row => row[1] == "graph-injection-schema");
            Assert.AreEqual("v1", Decode(schema[3]), "Regenerate Runtime inputs: compiler must select graph system injection fields.");
            Assert.IsFalse(inputs.Any(row => row[1] == "graph-system-injection" || row[1] == "graph-system-injection-auto"));
            var selections = slots.Select(slot => (slot.graph, owner: slot.type.AssemblyQualifiedName)).Distinct().ToArray();
            var plans = attributes.Where(attribute => attribute.Key == "ME.BECS.PublishedGraphSystemInjectionPlan.v1")
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(selections.Length, plans.Length);
            foreach (var selection in selections) {
                var plan = plans.Single(row => row.Length == 5 && row[1] == selection.graph.ToString(CultureInfo.InvariantCulture) && row[2] == selection.owner);
                Assert.AreEqual("v1", plan[0]);
                var owner = Type.GetType(selection.owner, true);
                var graphSlots = slots.Where(slot => slot.graph == selection.graph).OrderBy(slot => slot.slot).ToArray();
                Assert.AreEqual(graphSlots.First(slot => slot.type == owner).slot.ToString(CultureInfo.InvariantCulture), plan[3]);
                var fields = owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => typeof(IInject).IsAssignableFrom(field.FieldType)).ToArray();
                var expected = fields.Select(field => {
                    Assert.IsFalse(field.IsInitOnly, field.Name);
                    Assert.AreEqual(typeof(InjectSystem<>), field.FieldType.GetGenericTypeDefinition(), field.Name);
                    var target = field.FieldType.GenericTypeArguments[0];
                    var slot = graphSlots.First(item => item.type == target).slot;
                    return field.Name + ":" + slot.ToString(CultureInfo.InvariantCulture);
                }).ToArray();
                CollectionAssert.AreEqual(expected, plan[4].Length == 0 ? Array.Empty<string>() : plan[4].Split(','), owner.FullName);
            }
            foreach (var graph in slots.Select(slot => slot.graph).Distinct()) {
                var owner = Tests_SourceGeneratorGraphPublications.Owner(graph.ToString(CultureInfo.InvariantCulture));
                Assert.IsFalse(owner.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                Assert.IsNull(owner.GetType("ME.BECS.SourceGenerated.SystemInjectionInputs", false),
                    "Unused graph-independent injection helpers must not be emitted.");
            }
            // Metadata and field shape only: no graph, patch or registration method is invoked.
        }
    }
}
