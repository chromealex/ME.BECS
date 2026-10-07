namespace ME.BECS.Editor.FeaturesGraph {

    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Linq;
    using Unity.Burst;
    using UnityEditor;

    /// <summary>Read-only projection of the same lifecycle contracts used by the compiler.</summary>
    internal static class FeaturesGraphStudioMetadata {

        internal readonly struct Callback {
            public readonly Method phase;
            public readonly bool burst;
            public readonly string source;

            public Callback(Method phase, bool burst, string source) {
                this.phase = phase;
                this.burst = burst;
                this.source = source;
            }
        }

        internal static readonly Method[] Phases = {
            Method.Awake, Method.Start, Method.Update, Method.Destroy, Method.DrawGizmos,
        };

        internal const int AllPhases = (1 << 5) - 1;
        internal static int PhaseBit(Method phase) => 1 << System.Array.IndexOf(Phases, phase);
        internal static bool Includes(int mask, Method phase) => (mask & PhaseBit(phase)) != 0;

        internal static string GetSystemTitle(ISystem system) {
            if (system == null) return "System Node";
            var type = system.GetType();
            var name = ObjectNames.NicifyVariableName(type.Name.Split('`')[0]);
            if (!type.IsGenericType) return name;
            var parameters = type.GetGenericTypeDefinition().GetGenericArguments();
            var arguments = type.GetGenericArguments();
            var contracts = parameters.Select((parameter, index) => {
                var interfaces = parameter.GetGenericParameterConstraints().Where(constraint => constraint.IsInterface)
                    .Select(constraint => constraint.Name.Split('`')[0]).Distinct().ToArray();
                return interfaces.Length > 0 ? string.Join(" & ", interfaces) : arguments[index].Name.Split('`')[0];
            });
            return name + " <" + string.Join(", ", contracts) + ">";
        }

        internal static List<Callback> GetCallbacks(ISystem system) {
            var result = new List<Callback>();
            if (system == null) return result;
            var type = system.GetType();
            var systemBurst = type.IsDefined(typeof(BurstCompileAttribute), false);
            foreach (var phase in Phases) {
                var contract = GetContract(phase);
                if (!contract.IsAssignableFrom(type)) continue;
                var mapping = type.GetInterfaceMap(contract);
                for (var i = 0; i < mapping.InterfaceMethods.Length; ++i) {
                    if (mapping.InterfaceMethods[i].Name != "On" + phase) continue;
                    var target = mapping.TargetMethods[i];
                    var withoutBurst = target.IsDefined(typeof(WithoutBurstAttribute), false);
                    var methodBurst = target.IsDefined(typeof(BurstCompileAttribute), false);
                    // WithoutBurst wins even when BurstCompile is present on the method/type.
                    var burst = !withoutBurst && (systemBurst || methodBurst);
                    result.Add(new Callback(phase, burst, withoutBurst ? "[WithoutBurst]" :
                        methodBurst ? "[BurstCompile] on method" : systemBurst ? "[BurstCompile] on system" : "No Burst attribute"));
                    break;
                }
            }
            return result;
        }

        private static Type GetContract(Method phase) {
            switch (phase) {
                case Method.Awake: return typeof(IAwake);
                case Method.Start: return typeof(IStart);
                case Method.Update: return typeof(IUpdate);
                case Method.Destroy: return typeof(IDestroy);
                default: return typeof(IDrawGizmos);
            }
        }

        // Enumerating Unity's serialized children keeps the summary and editable inspector
        // consistent, including SerializeField/SerializeReference and custom property drawers.
        internal static List<string> GetFieldNames(SerializedProperty system) {
            var names = new List<string>();
            if (system == null) return names;
            var systemType = system.managedReferenceValue?.GetType();
            var iterator = system.Copy();
            var end = iterator.GetEndProperty();
            if (!iterator.NextVisible(true)) return names;
            do {
                if (SerializedProperty.EqualContents(iterator, end) || iterator.depth <= system.depth) break;
                // Entity handles are runtime state, not editable graph configuration.
                if (systemType?.GetField(iterator.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.FieldType == typeof(Ent)) continue;
                if (iterator.depth == system.depth + 1) names.Add(iterator.name);
            } while (iterator.NextVisible(false));
            return names;
        }
    }
}
