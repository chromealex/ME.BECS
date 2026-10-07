using System;
using System.Linq;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor {
    // Pure preflight: never invoke a legacy hook or instantiate its owner to find
    // out whether it still generates code. New exporters transport data only.
    internal static class SourceGeneratorExportContract {
        internal static void ValidateType(Type type) {
            if (type == null || !typeof(CustomCodeGenerator).IsAssignableFrom(type))
                throw new InvalidOperationException("Expected a source input feeder type.");
            foreach (var name in new[] { "AddInitialization", "AddMethods", "AddPublicContent", "AddFileContent" }) {
                var signature = name == "AddInitialization" ? new[] { typeof(scg::List<string>), typeof(scg::List<Type>) } :
                    name == "AddPublicContent" ? Type.EmptyTypes : new[] { typeof(scg::List<Type>) };
                if (type.GetMethod(name, signature)?.DeclaringType != typeof(CustomCodeGenerator))
                    throw new InvalidOperationException("Legacy C# generation is no longer supported: " + type.AssemblyQualifiedName + " :: " + name +
                        ". Migrate it to AppendSourceGeneratorInputs/AddSourceGeneratorReferences and a compiler emitter. " +
                        "Use GetRetiredSourceFiles for old output filenames; do not export executable C#.");
            }
        }

        internal static void Validate(CustomCodeGenerator[] feeders) {
            foreach (var feeder in feeders) {
                if (feeder == null) throw new InvalidOperationException("Null source input feeder.");
                ValidateType(feeder.GetType());
                if (string.IsNullOrEmpty(feeder.SourceInitializationKind) || string.IsNullOrEmpty(feeder.SourceRegistrationKind) ||
                    feeder.SourceInitializationKind == "legacy" || feeder.SourceRegistrationKind == "legacy")
                    throw new InvalidOperationException("Source input feeder has no compiler-owned bootstrap contract: " + feeder.GetType().AssemblyQualifiedName);
            }
        }

        internal static string[] GetRetiredFileNames(scg::IEnumerable<CustomCodeGenerator> feeders) {
            var names = new scg::HashSet<string>(StringComparer.Ordinal);
            foreach (var feeder in feeders) {
                var files = feeder.GetRetiredSourceFiles();
                if (files == null) throw new InvalidOperationException("Null retired-file plan: " + feeder.GetType().AssemblyQualifiedName);
                foreach (var name in files) {
                    if (!IsValidFileName(name)) throw new InvalidOperationException("Invalid retired source filename: " + name);
                    names.Add(name);
                }
            }
            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        internal static bool IsValidFileName(string name) => !string.IsNullOrWhiteSpace(name) && name.Trim() == name &&
            !name.EndsWith(".", StringComparison.Ordinal) && !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            !name.Any(char.IsControl) && name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0;
    }
}
