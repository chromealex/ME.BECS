
using System.Linq;

namespace ME.BECS.Editor {

    public class ComponentDestroyCodeGenerator : CustomCodeGenerator {

        internal static System.Type[] GetSelectedComponents(bool editor, System.Collections.Generic.List<AssemblyInfo> assemblies) =>
            UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentDestroy>()
                .Where(type => type.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, type, assemblies))
                .OrderBy(type => type.FullName, System.StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, System.StringComparer.Ordinal).ToArray();

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            references.AddRange(GetSelectedComponents(this.editorAssembly, this.asms));
            // Preserve the registry phase position; Roslyn owns callbacks and ordered registration.
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition> {
                new CodeGenerator.MethodDefinition { generatedRegistration = "global::ME.BECS.SourceGenerated.DestroyInputs.Initialize();" },
            };

        }

    }

}
