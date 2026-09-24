using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class EntityConfigCodeGenerator : CustomCodeGenerator {

        internal static System.Type[] GetMaskComponents(bool editor, System.Collections.Generic.List<AssemblyInfo> assemblies) =>
            UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponent>()
                .Where(type => type.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, type, assemblies) &&
                    type.GetFields(BindingFlags.Instance | BindingFlags.Public).Length > 1)
                .OrderBy(type => type.FullName, System.StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, System.StringComparer.Ordinal).ToArray();

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            references.AddRange(GetCollectionComponents(this.editorAssembly, this.asms));
            dataList.Add("global::ME.BECS.SourceGenerated.ConfigCollectionCounts.Initialize();");
        }

        internal static System.Type[] GetCollectionComponents(bool editor, System.Collections.Generic.List<AssemblyInfo> assemblies) {
            System.Collections.Generic.IEnumerable<System.Type> Ordered(System.Type contract) =>
                UnityEditor.TypeCache.GetTypesDerivedFrom(contract).OrderBy(type => type.FullName, System.StringComparer.Ordinal)
                    .ThenBy(type => type.Assembly.FullName, System.StringComparer.Ordinal);
            return Ordered(typeof(IConfigComponent)).Concat(Ordered(typeof(IConfigComponentStatic))).Concat(Ordered(typeof(IConfigComponentShared)))
                .Where(type => type.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, type, assemblies) && GetCollectionsCount(type) > 0u)
                .Distinct().ToArray();
        }

        internal static FieldInfo[] GetCollectionFields(System.Type component) =>
            component.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => typeof(IUnmanagedList).IsAssignableFrom(field.FieldType))
                .OrderBy(field => field.FieldType.FullName, System.StringComparer.Ordinal).ToArray();

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            references.AddRange(GetMaskComponents(this.editorAssembly, this.asms));
            references.AddRange(GetCollectionComponents(this.editorAssembly, this.asms));
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition> {
                new CodeGenerator.MethodDefinition { generatedRegistration = "global::ME.BECS.SourceGenerated.ConfigMaskInputs.Initialize();" },
                new CodeGenerator.MethodDefinition { generatedRegistration = "global::ME.BECS.SourceGenerated.ConfigCollectionsInputs.Initialize();" },
            };
        }

        internal static uint GetCollectionsCount(System.Type componentType) {
            var count = 0u;
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields) {
                if (typeof(IUnmanagedList).IsAssignableFrom(field.FieldType) == true) {
                    ++count;
                }
            }
            return count;
        }

    }

}
