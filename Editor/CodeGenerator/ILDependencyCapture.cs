namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // A memo hit must propagate the same dependency set as the original analysis.
    // Otherwise an outer persisted result could silently forget a shared helper.
    // Besides method bodies an analysis depends on the declarations it can see
    // (assemblies) and on global type inventory queries (TypeCache). Both are
    // propagated exactly like methods so persisted summaries can be scoped.
    internal sealed class ILDependencyCapture : IDisposable {
        [ThreadStatic] private static ILDependencyCapture current;
        private readonly ILDependencyCapture previous;
        private HashSet<MethodBase> methods;
        private HashSet<string> queries;
        private HashSet<string> assemblies;
        internal MethodBase[] Methods {
            get {
                if (this.methods == null || this.methods.Count == 0) return Array.Empty<MethodBase>();
                var result = new MethodBase[this.methods.Count];
                this.methods.CopyTo(result);
                return result;
            }
        }
        // "constraint assembly-qualified name \t result hash"; sorted for persistence.
        internal string[] Queries => Sorted(this.queries);
        // Simple assembly names whose declarations were read beyond the observed bodies.
        internal string[] Assemblies => Sorted(this.assemblies);

        private static string[] Sorted(HashSet<string> values) {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            var result = values.ToArray();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        internal ILDependencyCapture() { this.previous = current; current = this; }
        internal static void Observe(MethodBase method) {
            if (current == null || method == null) return;
            (current.methods ??= new HashSet<MethodBase>()).Add(method);
        }
        internal static void Observe(IEnumerable<MethodBase> methods) {
            if (current == null || methods == null) return;
            foreach (var method in methods) Observe(method);
        }

        // Records a TypeCache.GetTypesDerivedFrom(constraint) result. The summary is
        // valid only while the same query returns the same set of types, and it can
        // see the declarations of every returned type.
        internal static void ObserveQuery(Type constraint, IEnumerable<Type> results) {
            if (current == null || constraint == null) return;
            var types = results?.Where(type => type != null).ToArray() ?? Array.Empty<Type>();
            (current.queries ??= new HashSet<string>(StringComparer.Ordinal)).Add(QueryKey(constraint) + "\t" + QueryResult(types));
            ObserveAssembly(constraint.Assembly);
            foreach (var type in types) ObserveAssembly(type.Assembly);
        }

        internal static void ObserveAssembly(Assembly assembly) {
            if (current == null || assembly == null || assembly.IsDynamic) return;
            (current.assemblies ??= new HashSet<string>(StringComparer.Ordinal)).Add(assembly.GetName().Name);
        }

        // Replays recorded queries/assemblies of a memo or persisted hit.
        internal static void ObserveRecorded(IEnumerable<string> queries, IEnumerable<string> assemblies) {
            if (current == null) return;
            if (queries != null) foreach (var query in queries) (current.queries ??= new HashSet<string>(StringComparer.Ordinal)).Add(query);
            if (assemblies != null) foreach (var name in assemblies) (current.assemblies ??= new HashSet<string>(StringComparer.Ordinal)).Add(name);
        }

        // Open constraints have no persistent identity; summaries using them stay uncached.
        internal const string OpenQuery = "<open>";
        internal static string QueryKey(Type constraint) => constraint.AssemblyQualifiedName ?? OpenQuery + constraint;

        internal static string QueryResult(IEnumerable<Type> types) => ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(string.Join("\n",
            types.Select(type => type.AssemblyQualifiedName ?? type.ToString()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)));

        // Drop observations that cannot be persisted before they reach the parent.
        internal void Discard(Predicate<MethodBase> predicate) => this.methods?.RemoveWhere(predicate);

        public void Dispose() {
            current = this.previous;
            if (current == null) return;
            // Propagate all dependencies without an interface enumerator and a
            // thread-static lookup for every method. Do not share mutable sets.
            if (this.methods != null && this.methods.Count != 0) {
                if (current.methods == null) current.methods = new HashSet<MethodBase>(this.methods);
                else current.methods.UnionWith(this.methods);
            }
            if (this.queries != null && this.queries.Count != 0) {
                if (current.queries == null) current.queries = new HashSet<string>(this.queries, StringComparer.Ordinal);
                else current.queries.UnionWith(this.queries);
            }
            if (this.assemblies != null && this.assemblies.Count != 0) {
                if (current.assemblies == null) current.assemblies = new HashSet<string>(this.assemblies, StringComparer.Ordinal);
                else current.assemblies.UnionWith(this.assemblies);
            }
        }
    }
}
