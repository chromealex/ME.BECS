using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [Test]
        public void CompilerGraphJobInjectionsMatchFieldsAndRepeatedOwnerOrder() {
            var attributes = Tests_SourceGeneratorGraphPublications.PublishedMetadata();
            var inputs = Tests_SourceGeneratorInputCatalog.Rows(false).Select(row => ("runtime\t" + row).Split('\t')).ToArray();
            string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
            string Hash(string value) {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }
            string[] Split(string value, char separator) => value.Length == 0 ? Array.Empty<string>() : value.Split(separator);
            Assert.AreEqual("v1", Decode(inputs.Single(row => row[1] == "graph-injection-schema")[3]));
            Assert.IsFalse(inputs.Any(row => row[1] == "graph-job" || row[1] == "job-delta-registration" || row[1] == "graph-apply"),
                "Editor must transport job identities only, never fields, callback kinds or apply actions.");
            var plans = attributes.Where(attribute => attribute.Key == "ME.BECS.PublishedGraphJobInjectionPlan.v1").Select(attribute => attribute.Value.Split('\n')).ToArray();
            var actions = attributes.Where(attribute => attribute.Key == "ME.BECS.PublishedGraphInjectionActions.v1").Select(attribute => attribute.Value.Split('\n')).ToArray();
            var effective = attributes.Where(attribute => attribute.Key == "ME.BECS.PublishedGraphJobSelection.v1").Select(attribute => attribute.Value.Split('\n')).ToArray();
            var count = 0;
            foreach (var graph in inputs.Where(row => row[1] == "graph-registration")) {
                var id = graph[4];
                var slots = inputs.Where(row => row[1] == "graph-system" && row[4] == id)
                    .OrderBy(row => int.Parse(row[5], CultureInfo.InvariantCulture)).Select(row => Type.GetType(Decode(row[3]), true)).ToArray();
                var raw = inputs.Where(row => row[1] == "graph-job-selection" && row[4] == id).ToArray();
                var selections = effective.Where(row => row[1] == id).ToArray();
                CollectionAssert.AreEqual(slots.Select(type => type.AssemblyQualifiedName).Distinct().ToArray(), raw.Select(row => Decode(row[3])).ToArray());
                CollectionAssert.AreEqual(raw.Select(row => Decode(row[3])).ToArray(), selections.Select(row => row[2]).ToArray());
                foreach (var row in raw) {
                    var payload = Decode(row[5]).Split('\n');
                    Assert.AreEqual("v2", payload[0]);
                    Assert.AreEqual("il", payload[1], "Regenerate graph inputs with fresh IL job discovery.");
                    var owner = Type.GetType(Decode(row[3]), true);
                    var jobs = new System.Collections.Generic.HashSet<Type>();
                    Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobs", true)
                        .GetMethod("Collect").Invoke(null, new object[] { owner, jobs, null });
                    var expectedJobs = jobs.OrderBy(job => job.FullName, StringComparer.Ordinal)
                        .ThenBy(job => job.Assembly.FullName, StringComparer.Ordinal).Select(job => job.AssemblyQualifiedName).ToArray();
                    CollectionAssert.AreEqual(expectedJobs, payload.Skip(2).Where(value => value.Length != 0).ToArray());
                    CollectionAssert.AreEqual(expectedJobs, selections.Single(selection => selection[2] == owner.AssemblyQualifiedName)
                        .Skip(4).Where(value => value.Length != 0).ToArray(), "Compiler must retain the IL snapshot, not replace it with source metadata.");
                }
                foreach (var selection in selections) {
                    Assert.AreEqual("v1", selection[0]);
                    Assert.AreEqual("il", selection[3]);
                    var jobs = selection.Skip(4).Where(value => value.Length != 0).Select(value => Type.GetType(value, true)).ToArray();
                    CollectionAssert.AreEqual(jobs.OrderBy(job => job.FullName, StringComparer.Ordinal)
                        .ThenBy(job => job.Assembly.FullName, StringComparer.Ordinal).ToArray(), jobs);
                }
                var graphJobs = selections.SelectMany(row => row.Skip(4)).Where(value => value.Length != 0).Distinct().ToArray();
                count += graphJobs.Length;
                foreach (var identity in graphJobs) {
                    var plan = plans.Single(row => row.Length == 5 && row[1] == id && row[2] == identity);
                    var job = Type.GetType(identity, true);
                    var fields = job.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(field => typeof(IInject).IsAssignableFrom(field.FieldType) || Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute))).ToArray();
                    if (!job.IsVisible) {
                        CollectionAssert.IsEmpty(fields, "Private helper jobs may be recorded, but cannot require an emitted injection patch: " + identity);
                        Assert.AreEqual("none", plan[3], identity);
                    }
                    var expected = fields.Select(field => {
                        Assert.IsFalse(field.IsInitOnly, job.FullName + "." + field.Name);
                        if (Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute))) return field.Name + ":d";
                        Assert.AreEqual(typeof(InjectSystem<>), field.FieldType.GetGenericTypeDefinition());
                        var slot = Array.IndexOf(slots, field.FieldType.GenericTypeArguments[0]);
                        Assert.GreaterOrEqual(slot, 0, job.FullName + "." + field.Name);
                        return field.Name + ":" + slot.ToString(CultureInfo.InvariantCulture);
                    }).ToArray();
                    Assert.AreEqual("v1", plan[0]);
                    Assert.AreEqual(fields.Length == 0 ? "none" : fields.Any(field => typeof(IInject).IsAssignableFrom(field.FieldType)) ? "graph" : "delta", plan[3]);
                    CollectionAssert.AreEqual(expected, Split(plan[4], ','), job.FullName);
                }
                var expectedActions = selections.SelectMany(row => new[] { "s:" + Hash(row[2]) }.Concat(
                    row.Skip(4).Where(value => value.Length != 0).Select(identity => (identity, plan: plans.Single(plan => plan[1] == id && plan[2] == identity)))
                        .Where(job => job.plan[3] != "none").Select(job => (job.plan[3] == "graph" ? "j:" : "d:") + Hash(job.identity)))).ToArray();
                var actualActions = actions.Single(row => row.Length == 3 && row[1] == id);
                Assert.AreEqual("v1", actualActions[0]);
                CollectionAssert.AreEqual(expectedActions, Split(actualActions[2], ','));
            }
            Assert.AreEqual(count, plans.Length);
            Assert.AreEqual(inputs.Count(row => row[1] == "graph-registration"), actions.Length);
            // Metadata and reflection only. No world, registration or patch callback is invoked.
        }
    }
}
