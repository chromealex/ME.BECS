
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ME.BECS.Network.Editor {

    using ME.BECS.Editor;
    
    // Unity discovery only. Signatures and executable registration belong to Roslyn.
    public class NetworkCodeGenerator : CustomCodeGenerator {
        private MethodInfo[] selected;

        // The concrete built-in feeder is always discovered. Derived extensions
        // reuse its global plan; legacy C# overrides are rejected before export.
        public override string SourceRegistrationKind => this.GetType() == typeof(NetworkCodeGenerator) ? "network-methods" : base.SourceRegistrationKind;

        private MethodInfo[] Collect() => this.selected ??= UnityEditor.TypeCache.GetMethodsWithAttribute<NetworkMethodAttribute>()
            .Where(method => method.IsStatic && method.DeclaringType.IsVisible && !method.Name.Contains("$") &&
                this.IsValidTypeForAssembly(method.DeclaringType))
            // MethodsStorage assigns sequential network IDs. Culture and TypeCache tie
            // order must never change the wire mapping. Roslyn validates this same order.
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ThenBy(method => method.DeclaringType.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();

        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<Type> references) {
            references.Add(typeof(UnsafeNetworkModule));
            references.AddRange(this.Collect().Select(method => method.DeclaringType).Distinct());
        }

        public override void AppendSourceGeneratorInputs(StringBuilder manifest) {
            if (this.SourceRegistrationKind != "network-methods") return;
            var methods = this.Collect();
            void Append(string kind, int ordinal, string payload) => manifest.Append(kind).Append('\t')
                .Append(ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))).Append('\n');
            Append("network-method-schema", 0, "v1\n" + methods.Length.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < methods.Length; ++index)
                Append("network-method", index, methods[index].DeclaringType.AssemblyQualifiedName + "\n" + methods[index].Name);
        }
    }

}
