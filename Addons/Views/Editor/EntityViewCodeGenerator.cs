using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using ME.BECS.Views;
using ME.BECS.Editor.Jobs;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor.Aspects {

    // Transitional discovery feeder: IL analysis remains here, C# emission is compiler-owned.
    public class EntityViewCodeGenerator : CustomCodeGenerator {
        public override string SourceInitializationKind => this.GetType() == typeof(EntityViewCodeGenerator) ? "views" : null;
        private Plan collected;

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare View Safety")]
        private static void CompareSafety() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before checking view safety.");
                return;
            }
            var comparison = new SourceGeneratorViewSafety();
            var details = new StringBuilder();
            var equal = 0;
            var different = 0;
            var unavailable = 0;
            var owners = UnityEditor.TypeCache.GetTypesDerivedFrom<EntityView>().Concat(UnityEditor.TypeCache.GetTypesDerivedFrom<IViewModule>())
                .Where(type => !type.IsAbstract && !type.ContainsGenericParameters).Distinct().OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal);
            foreach (var owner in owners) {
                foreach (var phase in new[] { nameof(EntityView.ApplyState), nameof(EntityView.ApplyStateParallel) }) {
                    try {
                        var method = owner.GetMethod(phase, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (method == null) continue;
                        var legacy = JobsEarlyInitCodeGenerator.GetMethodTypesInfo(method, useAnalyzer: false);
                        var status = comparison.Compare(owner, phase, legacy, out var detail);
                        if (status == 1) ++equal; else if (status == 0) ++different; else ++unavailable;
                        details.AppendLine(owner.AssemblyQualifiedName + " :: " + phase + " — " + detail);
                    } catch (System.Exception exception) {
                        ++unavailable;
                        details.AppendLine(owner.AssemblyQualifiedName + " :: " + phase + " — " + exception);
                    }
                }
            }
            SourceGeneratorReport.Publish("ViewSafety", "View callback safety: complete equal=" + equal + ", complete differences=" + different +
                ", incomplete/unavailable=" + unavailable + ". Raw callback dependencies before view tracking filters. " +
                "Fresh legacy IL vs source summaries; callbacks and initialization NOT invoked. Production selection remains unchanged.", details.ToString());
        }

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare View Tracker Inputs")]
        private static void CompareInputs() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before checking view tracker inputs.");
                return;
            }
            var details = new StringBuilder();
            details.AppendLine("Snapshot UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            var equal = 0;
            var differences = 0;
            var unavailable = 0;
            foreach (var editor in new[] { false, true }) {
                var name = "ME.BECS.Gen." + (editor ? "Editor" : "Runtime");
                var owners = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetName().Name == name).ToArray();
                if (owners.Length != 1) {
                    ++unavailable;
                    details.AppendLine(name + ": expected one loaded assembly, found " + owners.Length);
                    continue;
                }
                try {
                    var feeder = new EntityViewCodeGenerator { editorAssembly = editor, asms = EditorUtils.GetAssembliesInfo() };
                    var expectedText = new StringBuilder();
                    feeder.AppendSourceGeneratorInputs(expectedText);
                    var expected = expectedText.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var actual = owners[0].GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(attribute => attribute.Key == "ME.BECS.ViewTrackerInputs.v1").Select(attribute => attribute.Value).ToArray();
                    var same = expected.SequenceEqual(actual, StringComparer.Ordinal);
                    if (same) ++equal; else ++differences;
                    var plan = feeder.Collect();
                    details.AppendLine(name + ": views/modules=" + plan.entries.Count + ", tracked components=" + plan.tracked.Length +
                        ", expected records=" + expected.Length + ", compiled records=" + actual.Length + ", equal=" + same +
                        ", MVID=" + owners[0].ManifestModule.ModuleVersionId);
                } catch (System.Exception exception) {
                    ++unavailable;
                    details.AppendLine(name + ": " + exception);
                }
            }
            SourceGeneratorReport.Publish("ViewTracker", "View tracker inputs: equal=" + equal + ", differences=" + differences +
                ", unavailable=" + unavailable + ". Fresh IL discovery vs compiled source input metadata; initialization NOT invoked. " +
                "This checks selection and order, not Burst/player execution.", details.ToString());
        }
        private sealed class Entry {
            public Type type;
            public bool module;
            public readonly scg::List<Type> components = new scg::List<Type>();
        }

        private sealed class Plan {
            public int capacity;
            public Type[] tracked;
            public readonly scg::List<Entry> entries = new scg::List<Entry>();
        }

        public override void AddInitialization(scg::List<string> dataList, scg::List<Type> references) {
            dataList.Add("global::ME.BECS.SourceGenerated.ViewTrackerInputs.Initialize();");
        }

        public override void AddSourceGeneratorReferences(scg::List<Type> references) {
            var plan = this.Collect();
            references.AddRange(plan.entries.Select(entry => entry.type));
            references.AddRange(plan.tracked);
        }

        public override FileContent[] AddFileContent(scg::List<Type> references) {
            this.AddSourceGeneratorReferences(references);
            // Normal regeneration overwrites the old file; never leave a stale executable body.
            return new[] { new FileContent { filename = "EntityView", content = "// View tracker registration is emitted by the source generator.\n" } };
        }

        public override void AppendSourceGeneratorInputs(StringBuilder manifest) {
            var plan = this.Collect();
            void Append(string kind, int ordinal, string payload) => manifest.Append(kind).Append('\t')
                .Append(ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))).Append('\n');
            Append("view-tracker", 0, "v2\n" + plan.capacity.ToString(CultureInfo.InvariantCulture) + "\n" +
                string.Join("\n", plan.tracked.Select(type => type.AssemblyQualifiedName)));
            var views = 0;
            var modules = 0;
            foreach (var entry in plan.entries)
                Append(entry.module ? "view-tracker-module" : "view-tracker-view", entry.module ? modules++ : views++,
                    entry.type.AssemblyQualifiedName + "\n" + string.Join("\n", entry.components.Select(type => type.AssemblyQualifiedName)));
        }

        private Plan Collect() {
            if (this.collected != null) return this.collected;
            var views = UnityEditor.TypeCache.GetTypesDerivedFrom<EntityView>().OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, StringComparer.Ordinal).ToArray();
            var modules = UnityEditor.TypeCache.GetTypesDerivedFrom<IViewModule>().OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, StringComparer.Ordinal).ToArray();
            var plan = new Plan { capacity = views.Length + modules.Length };
            var types = new scg::HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
            foreach (var selection in new[] { (module: false, values: views), (module: true, values: modules) }) {
                foreach (var viewType in selection.values) {
                    if (viewType.IsAbstract || viewType.GenericTypeArguments.Length > 0 || !this.IsValidTypeForAssembly(viewType)) continue;
                    var entry = new Entry { type = viewType, module = selection.module };
                    plan.entries.Add(entry);
                    if (typeof(IViewIgnoreTracker).IsAssignableFrom(viewType)) continue;
                    var ignored = new scg::HashSet<Type>(GetTrackingArguments(viewType, typeof(IViewTrackIgnore<>)));
                    void Add(JobsEarlyInitCodeGenerator.TypeInfo component) {
                        entry.components.Add(component.type);
                        types.Add(component);
                    }
                    foreach (var methodName in new[] { nameof(EntityView.ApplyState), nameof(EntityView.ApplyStateParallel) }) {
                        var method = viewType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (method == null) continue;
                        foreach (var component in JobsEarlyInitCodeGenerator.GetMethodTypesInfo(method, useAnalyzer: false)) {
                            if (component.op != RefOp.ReadOnly)
                                UnityEngine.Debug.LogWarning($"EntityView {viewType.FullName} writes to {component.type.FullName} in method {methodName}, be sure this view has been used in Visual mode world only");
                            if (!ignored.Contains(component.type)) Add(component);
                        }
                    }
                    foreach (var type in GetTrackingArguments(viewType, typeof(IViewTrack<>))) {
                        if (typeof(IAspect).IsAssignableFrom(type)) {
                            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                                if (typeof(IAspectData).IsAssignableFrom(field.FieldType) && field.GetCustomAttribute(typeof(QueryWithAttribute)) != null)
                                    Add(new JobsEarlyInitCodeGenerator.TypeInfo { type = field.FieldType.GenericTypeArguments[0], op = RefOp.ReadOnly });
                        } else Add(new JobsEarlyInitCodeGenerator.TypeInfo { type = type, op = RefOp.ReadOnly });
                    }
                }
            }
            // Tracker IDs must not depend on reflection/hash enumeration or process culture.
            // Access modes affect diagnostics, not the unique set of tracked component types.
            plan.tracked = OrderTrackingTypes(types.Select(type => type.type));
            return this.collected = plan;
        }

        internal static Type[] GetTrackingArguments(Type owner, Type contract) {
            if (contract != typeof(IViewTrack<>) && contract != typeof(IViewTrackIgnore<>))
                throw new ArgumentException("Expected a view tracking interface definition.", nameof(contract));
            // Open generic IsAssignableFrom does not match constructed interfaces.
            // GetInterfaces includes inherited contracts; identity also excludes unrelated
            // user interfaces with the same short name. Explicit opt-in order is canonical.
            return OrderTrackingTypes(owner.GetInterfaces().Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == contract)
                .Select(type => type.GetGenericArguments()[0]));
        }

        internal static Type[] OrderTrackingTypes(scg::IEnumerable<Type> types) => types.Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
    }
}
