namespace ME.BECS.Editor {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using global::Mono.Cecil;

    // Mono can lose IsUnmanagedAttribute on reflected generic parameters after
    // constructed-method lookups. Read immutable metadata, never execute attributes.
    internal static class ILGenericParameterMetadata {
        private static readonly ConcurrentDictionary<Module, Lazy<Dictionary<(int owner, int position), string>>> modules = new();

        internal static string Attributes(Type parameter) {
            if (parameter.IsGenericParameter == false) { throw new ArgumentException("Expected generic parameter.", nameof(parameter)); }
            var module = parameter.Module;
            var values = modules.GetOrAdd(module, static key => new Lazy<Dictionary<(int, int), string>>(() => Read(key))).Value;
            // Mono does not expose a usable GenericParam row token on Type.
            var owner = (MemberInfo)parameter.DeclaringMethod ?? parameter.DeclaringType;
            if (values.TryGetValue((owner.MetadataToken, parameter.GenericParameterPosition), out var value) == false) {
                throw new NotSupportedException("Generic parameter metadata is unavailable: " + parameter);
            }
            return value;
        }

        private static Dictionary<(int, int), string> Read(Module loaded) {
            using var module = ModuleDefinition.ReadModule(loaded.FullyQualifiedName, new ReaderParameters { ReadSymbols = false });
            // Compilation can replace the file before the old domain unloads.
            // Never mix the new disk metadata with an old loaded declaration.
            if (module.Mvid != loaded.ModuleVersionId) { throw new NotSupportedException("Loaded/disk metadata mismatch: " + loaded.Name); }
            var values = new Dictionary<(int, int), string>();
            foreach (var type in module.GetTypes()) {
                Add(values, type.MetadataToken.ToInt32(), type.GenericParameters);
                foreach (var method in type.Methods) { Add(values, method.MetadataToken.ToInt32(), method.GenericParameters); }
            }
            return values;
        }

        private static void Add(Dictionary<(int, int), string> values, int owner, IEnumerable<GenericParameter> parameters) {
            foreach (var parameter in parameters) {
                var rows = parameter.CustomAttributes.Select(attribute => {
                    var constructor = attribute.Constructor;
                    // Custom-attribute blobs contain values and symbolic type
                    // names, not metadata row tokens. Preserve duplicate attributes.
                    return Name(constructor.DeclaringType) + "::" + constructor.Name + "(" +
                        string.Join(";", constructor.Parameters.Select(item => Name(item.ParameterType))) + ")|" +
                        Convert.ToBase64String(attribute.GetBlob());
                }).OrderBy(row => row, StringComparer.Ordinal);
                var text = new StringBuilder("generic-attributes-v1;");
                foreach (var row in rows) { text.Append(row.Length).Append(':').Append(row); }
                values.Add((owner, parameter.Position), text.ToString());
            }
        }

        private static string Name(TypeReference type) {
            var result = type.FullName + "@" + type.Scope;
            if (type is GenericInstanceType generic) {
                result += "<" + string.Join(";", generic.GenericArguments.Select(Name)) + ">";
            } else if (type is TypeSpecification specification) {
                result += "[" + Name(specification.ElementType) + "]";
            }
            return result;
        }
    }
}
