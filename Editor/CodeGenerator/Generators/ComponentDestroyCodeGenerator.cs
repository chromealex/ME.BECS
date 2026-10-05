
using System.Linq;

namespace ME.BECS.Editor {

    public class ComponentDestroyCodeGenerator : CustomCodeGenerator {
        public override string SourceRegistrationKind => this.GetType() == typeof(ComponentDestroyCodeGenerator) ? "destroy-callbacks" : base.SourceRegistrationKind;

        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<System.Type> references) =>
            references.AddRange(GetSelectedComponents(this.editorAssembly, this.asms));

        internal static System.Type[] GetSelectedComponents(bool editor, System.Collections.Generic.List<AssemblyInfo> assemblies) =>
            UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentDestroy>()
                .Where(type => type.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, type, assemblies))
                .OrderBy(type => type.FullName, System.StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, System.StringComparer.Ordinal).ToArray();

    }

}
