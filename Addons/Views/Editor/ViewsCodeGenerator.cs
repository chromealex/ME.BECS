using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ME.BECS.Views.Editor {
    using ME.BECS.Editor;

    // Discover the Unity-selected types only. Virtual-slot binding, callback flags
    // and executable registration are emitted by the compiler.
    /// <summary>
    /// Exports views registration data for generated code.
    /// </summary>
    public class ViewsCodeGenerator : CustomCodeGenerator {
        private Type[] selected;

        /// <summary>
        /// Source registration kind used by <c>ViewsCodeGenerator</c>.
        /// </summary>
        public override string SourceRegistrationKind => this.GetType() == typeof(ViewsCodeGenerator) ? "view-types" : base.SourceRegistrationKind;

        private Type[] Collect() => this.selected ??= UnityEditor.TypeCache.GetTypesDerivedFrom<EntityView>()
            .Where(type => !type.IsAbstract && !type.ContainsGenericParameters && this.IsValidTypeForAssembly(type))
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<Type> references) {
            references.Add(typeof(UnsafeViewsModule));
            references.AddRange(this.Collect());
        }

        /// <summary>
        /// Adds this feature's registration inputs to the source-generator export.
        /// </summary>
        public override void AppendSourceGeneratorInputs(StringBuilder manifest) {
            if (this.SourceRegistrationKind != "view-types") return;
            var types = this.Collect();
            void Append(string kind, int ordinal, string payload) => manifest.Append(kind).Append('\t')
                .Append(ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))).Append('\n');
            Append("view-type-schema", 0, "v1\n" + types.Length.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < types.Length; ++index)
                Append("view-type", index, types[index].AssemblyQualifiedName);
        }
    }
}
