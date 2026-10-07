namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    // No lifecycle code is invoked. Missing/invalid catalogs retain the reflection path during migration.
    public static class SourceGeneratorSystemLifecycle {
        private static readonly Dictionary<Assembly, Dictionary<string, (int present, int burst, int discarded)>> catalogs = new();

        public static bool TryGet(Type system, string phase, out bool present, out bool burst, out bool discarded) {
            present = burst = discarded = false;
            var bit = phase == "OnAwake" ? 1 : phase == "OnStart" ? 2 : phase == "OnUpdate" ? 4 :
                phase == "OnDestroy" ? 8 : phase == "OnDrawGizmos" ? 16 : 0;
            if (bit == 0 || system == null || system.Assembly.IsDynamic) return false;
            if (system.IsGenericType) system = system.GetGenericTypeDefinition();
            lock (catalogs) {
                if (!catalogs.TryGetValue(system.Assembly, out var catalog)) {
                    catalog = Read(system.Assembly);
                    catalogs.Add(system.Assembly, catalog);
                }
                if (catalog == null || !catalog.TryGetValue(system.FullName, out var flags)) return false;
                present = (flags.present & bit) != 0;
                burst = (flags.burst & bit) != 0;
                discarded = (flags.discarded & bit) != 0;
                return true;
            }
        }

        private static Dictionary<string, (int present, int burst, int discarded)> Read(Assembly assembly) {
            var result = new Dictionary<string, (int present, int burst, int discarded)>(StringComparer.Ordinal);
            try {
                // Do not use GetCustomAttributesData: some Unity assembly implementations do not support it.
                foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>()) {
                    if (attribute.Key != "ME.BECS.SystemLifecycle.v1") continue;
                    var fields = attribute.Value?.Split('\t');
                    if (fields == null || fields.Length != 4 || string.IsNullOrEmpty(fields[0]) || result.ContainsKey(fields[0]) ||
                        !Mask(fields[1], out var present) || !Mask(fields[2], out var burst) || !Mask(fields[3], out var discarded) ||
                        ((burst | discarded) & ~present) != 0) return null;
                    result.Add(fields[0], (present, burst, discarded));
                }
            } catch (NotSupportedException) { return null;
            } catch (NotImplementedException) { return null; }
            return result;
        }

        private static bool Mask(string value, out int mask) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out mask) &&
            mask >= 0 && mask <= 31 && mask.ToString(CultureInfo.InvariantCulture) == value;

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare System Lifecycle")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) { UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation."); return; }
            var report = new System.Text.StringBuilder();
            var systems = new System.Collections.Generic.List<Type>(UnityEditor.TypeCache.GetTypesDerivedFrom<ISystem>());
            var closed = new System.Collections.Generic.List<Type>(systems);
            CodeGenerator.PatchSystemsList(closed);
            systems.AddRange(closed);
            systems.Sort((left, right) => StringComparer.Ordinal.Compare(left.AssemblyQualifiedName, right.AssemblyQualifiedName));
            var visited = new HashSet<Type>();
            var compared = 0; var differences = 0; var unavailable = 0; var errors = 0;
            foreach (var system in systems) {
                if (!system.IsValueType || !visited.Add(system)) continue;
                foreach (var phase in new[] { "OnAwake", "OnStart", "OnUpdate", "OnDestroy", "OnDrawGizmos" }) {
                    try {
                        if (!TryGet(system, phase, out var present, out var burst, out var discarded)) {
                            ++unavailable; report.AppendLine("Unavailable: " + system + "." + phase); continue;
                        }
                        // Independent interface-map oracle; do not call the source-first Burst helper here.
                        var method = SourceGeneratorScheduledJobsValidation.GetLifecycleMethod(system, phase);
                        var expectedBurst = method != null && Attribute.IsDefined(method, typeof(Unity.Burst.BurstCompileAttribute));
                        var expectedDiscarded = method != null && Attribute.IsDefined(method, typeof(WithoutBurstAttribute));
                        ++compared;
                        if (present == (method != null) && burst == expectedBurst && discarded == expectedDiscarded) continue;
                        ++differences;
                        report.AppendLine("Difference: " + system + "." + phase + " source=" + present + "/" + burst + "/" + discarded +
                            " reflection=" + (method != null) + "/" + expectedBurst + "/" + expectedDiscarded);
                    } catch (Exception exception) { ++errors; report.AppendLine("Error: " + system + "." + phase + ": " + exception.Message); }
                }
            }
            SourceGeneratorReport.Publish("SystemLifecycle", "Lifecycle phases: compared=" + compared + ", differences=" + differences +
                ", unavailable=" + unavailable + ", errors=" + errors + ". Metadata only; no lifecycle invocation or Burst execution.", report.ToString());
        }
    }
}
