namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Run-local, metadata-only presence contracts. No builder/aspect initializer
    // or generated catalog getter is executed while collecting dependencies.
    internal sealed class ILQueryPresence {
        private static readonly Type[] Markers = {
            typeof(IJobForComponentsBase), typeof(IJobForAspectsBase), typeof(IJobForAspectsComponentsBase),
            typeof(IJobParallelForComponentsBase), typeof(IJobParallelForAspectsBase), typeof(IJobParallelForAspectsComponentsBase),
        };
        private readonly Dictionary<MethodInfo, Type[]> filters = new Dictionary<MethodInfo, Type[]>();
        private readonly Dictionary<MethodInfo, Type[]> schedules = new Dictionary<MethodInfo, Type[]>();
        private readonly Dictionary<Type, Type[]> aspects = new Dictionary<Type, Type[]>();
        // Static queries are absent when ENABLE_BECS_FLAT_QUERIES is enabled.
        private readonly Type staticBuilder = typeof(QueryBuilder).Assembly.GetType("ME.BECS.QueryBuilderStatic", false);
        private readonly Type staticQuery = typeof(QueryBuilder).Assembly.GetType("ME.BECS.Query", false);

        internal bool TryFilter(MethodInfo method, out Type[] components) {
            if (this.filters.TryGetValue(method, out components)) return components != null;
            components = null;
            if (this.IsFilter(method)) {
                if (method.ContainsGenericParameters) throw new InvalidOperationException("Open query presence filter: " + method);
                var types = method.GetGenericArguments();
                components = method.Name == "WithAspect" ? this.Aspect(types[0]) :
                    Canonical(types.Where(type => method.Name != "WithAny" || type != typeof(TNull)).Select(Component));
            }
            this.filters.Add(method, components);
            return components != null;
        }

        private bool IsFilter(MethodInfo method) {
            if (!method.IsGenericMethod) return false;
            var owner = method.DeclaringType;
            var compose = owner == typeof(ArchetypeQueries.QueryCompose);
            var dynamic = owner == typeof(QueryBuilder);
            var @static = owner != null && owner == this.staticBuilder;
            var begin = owner != null && owner == this.staticQuery && method.IsStatic && this.staticBuilder != null && method.ReturnType == this.staticBuilder;
            if (!compose && !dynamic && !@static && !begin) return false;
            var parameters = method.GetParameters();
            if (begin) {
                if (!(parameters.Length == 1 && parameters[0].ParameterType == typeof(SystemContext).MakeByRefType()) &&
                    !(parameters.Length == 2 && parameters[0].ParameterType == typeof(World).MakeByRefType() && parameters[1].ParameterType == typeof(Unity.Jobs.JobHandle))) return false;
            } else if (method.IsStatic || parameters.Length != 0 || method.ReturnType != (compose ? typeof(void) : owner)) return false;
            var count = method.GetGenericArguments().Length;
            switch (method.Name) {
                case "With": case "Without": case "WithAspect": return count == 1;
                case "WithAll": return !compose && count >= 2 && count <= (dynamic ? 4 : 2);
                case "WithAny": return count >= 2 && count <= (dynamic ? 4 : 2);
                default: return false;
            }
        }

        internal Type[] Scheduled(MethodInfo method) {
            if (this.schedules.TryGetValue(method, out var components)) return components;
            var result = new List<Type>();
            if (SourceGeneratorScheduledJobsValidation.IsSchedulingMethod(method) && method.DeclaringType.Assembly == typeof(Ent).Assembly) {
                // Bind the actual scheduler's work interface, not every interface
                // implemented by the job. A dual Unity/BECS job's ordinary Unity
                // Schedule does not add the BECS query's implicit filters.
                var contract = ILJobScheduleContract.GetWorkInterface(method);
                if (Markers.Any(marker => marker.IsAssignableFrom(contract))) {
                    foreach (var argument in contract.GetGenericArguments()) {
                        if (typeof(IAspect).IsAssignableFrom(argument)) result.AddRange(this.Aspect(argument));
                        else result.Add(Component(argument));
                    }
                }
            }
            this.schedules.Add(method, components = Canonical(result));
            return components;
        }

        private Type[] Aspect(Type aspect) {
            if (this.aspects.TryGetValue(aspect, out var components)) return components;
            if (!aspect.IsValueType || aspect.ContainsGenericParameters || !typeof(IAspect).IsAssignableFrom(aspect))
                throw new InvalidOperationException("Invalid query presence aspect: " + aspect);
            var result = new List<Type>();
            foreach (var field in aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (!field.IsDefined(typeof(QueryWithAttribute), false)) continue;
                var data = field.FieldType;
                if (!typeof(IAspectData).IsAssignableFrom(data) || !data.IsGenericType || data.ContainsGenericParameters)
                    throw new InvalidOperationException("Invalid QueryWith field in query presence analysis: " + aspect + "." + field.Name);
                result.Add(Component(data.GetGenericArguments()[0]));
            }
            this.aspects.Add(aspect, components = Canonical(result));
            return components;
        }

        private static Type Component(Type type) {
            if (!type.IsValueType || type.ContainsGenericParameters || !typeof(IComponentBase).IsAssignableFrom(type))
                throw new InvalidOperationException("Invalid query presence component: " + type);
            return type;
        }
        private static Type[] Canonical(IEnumerable<Type> types) => types.Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
    }
}
