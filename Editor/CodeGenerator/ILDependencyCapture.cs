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
        private HashSet<Type> types;
        internal Type[] Types => this.types?.ToArray() ?? Array.Empty<Type>();
        // Open parameters/constructed types have no resolvable AQN. Persist their
        // declaring definition and arguments instead; never silently lose them.
        internal static string[] PersistentTypes(IEnumerable<Type> types) {
            var visited = new HashSet<Type>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            void Add(Type type) {
                if (type == null || visited.Add(type) == false) { return; }
                if (type.HasElementType == true) {
                    Add(type.GetElementType());
                } else if (type.IsGenericParameter == true) {
                    Add(type.DeclaringType);
                    foreach (var constraint in type.GetGenericParameterConstraints()) { Add(constraint); }
                } else {
                    if (type.IsGenericType == true && type.IsGenericTypeDefinition == false) {
                        Add(type.GetGenericTypeDefinition());
                        foreach (var argument in type.GetGenericArguments()) { Add(argument); }
                    }
                    if (type.AssemblyQualifiedName != null && (type.ContainsGenericParameters == false || type.IsGenericTypeDefinition == true)) {
                        names.Add(type.AssemblyQualifiedName);
                    } else if (type.ContainsGenericParameters == false) {
                        throw new NotSupportedException("Type dependency has no persistent identity: " + type);
                    }
                }
            }
            foreach (var type in types) { Add(type); }
            return Sorted(names);
        }
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
            if ((current.methods ??= new HashSet<MethodBase>()).Add(method) == true) {
                ObserveDeclaration(method);
            }
        }

        internal static void ObserveType(Type type) {
            if (current == null || type == null) { return; }
            (current.types ??= new HashSet<Type>()).Add(type);
        }

        // An operand names a declaration, not necessarily an executed body.
        // Keep method-body traversal owned by the individual IL analyzers.
        internal static void ObserveDeclaration(MemberInfo member) {
            if (current == null || member == null) { return; }
            ObserveType(member.DeclaringType);
            if (member is Type type) {
                ObserveType(type);
            } else if (member is FieldInfo field) {
                ObserveType(field.FieldType);
            } else if (member is MethodBase method) {
                void Parameter(ParameterInfo parameter) {
                    ObserveType(parameter.ParameterType);
                    foreach (var modifier in parameter.GetRequiredCustomModifiers()) { ObserveType(modifier); }
                    foreach (var modifier in parameter.GetOptionalCustomModifiers()) { ObserveType(modifier); }
                }
                foreach (var parameter in method.GetParameters()) { Parameter(parameter); }
                if (method is MethodInfo info) {
                    Parameter(info.ReturnParameter);
                    foreach (var argument in info.GetGenericArguments()) { ObserveType(argument); }
                }
            }
        }
        internal static void Observe(IEnumerable<MethodBase> methods) {
            if (current == null || methods == null) return;
            foreach (var method in methods) Observe(method);
        }

        // A completed capture already contains the signature dependencies of
        // these methods. Replay both sets without reflecting every signature
        // again at each enclosing memo hit.
        internal static void Replay(MethodBase[] methods, string[] queries, string[] assemblies, Type[] types) {
            if (current == null) { return; }
            if (methods.Length != 0) {
                if (current.methods == null) { current.methods = new HashSet<MethodBase>(methods); }
                else { current.methods.UnionWith(methods); }
            }
            ObserveRecorded(queries, assemblies, types);
        }

        // Records a TypeCache.GetTypesDerivedFrom(constraint) result. The summary is
        // valid only while the same query returns the same set of types, and it can
        // see the declarations of every returned type.
        internal static void ObserveQuery(Type constraint, IEnumerable<Type> results) {
            if (current == null || constraint == null) return;
            var types = results?.Where(type => type != null).ToArray() ?? Array.Empty<Type>();
            (current.queries ??= new HashSet<string>(StringComparer.Ordinal)).Add(QueryKey(constraint) + "\t" + QueryResult(types));
            ObserveAssembly(constraint.Assembly);
            ObserveType(constraint);
            foreach (var type in types) {
                ObserveAssembly(type.Assembly);
                ObserveType(type);
            }
        }

        internal static void ObserveAssembly(Assembly assembly) {
            if (current == null || assembly == null || assembly.IsDynamic) return;
            (current.assemblies ??= new HashSet<string>(StringComparer.Ordinal)).Add(assembly.GetName().Name);
        }

        // Replays recorded queries/assemblies of a memo or persisted hit.
        internal static void ObserveRecorded(IEnumerable<string> queries, IEnumerable<string> assemblies, IEnumerable<Type> types = null) {
            if (current == null) return;
            if (types != null) { foreach (var type in types) { ObserveType(type); } }
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
            if (this.types != null && this.types.Count != 0) {
                if (current.types == null) { current.types = new HashSet<Type>(this.types); }
                else { current.types.UnionWith(this.types); }
            }
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
