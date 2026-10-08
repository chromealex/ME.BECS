using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    /// <summary>
    /// Exports entity config registration data for generated code.
    /// </summary>
    public class EntityConfigCodeGenerator : CustomCodeGenerator {
        /// <summary>
        /// Source initialization kind used by <c>EntityConfigCodeGenerator</c>.
        /// </summary>
        public override string SourceInitializationKind => this.GetType() == typeof(EntityConfigCodeGenerator) ? "config-counts" : base.SourceInitializationKind;
        /// <summary>
        /// Source registration kind used by <c>EntityConfigCodeGenerator</c>.
        /// </summary>
        public override string SourceRegistrationKind => this.GetType() == typeof(EntityConfigCodeGenerator) ? "config-callbacks" : base.SourceRegistrationKind;

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<System.Type> references) {
            references.AddRange(GetMaskComponents(this.editorAssembly, this.asms));
            references.AddRange(GetCollectionComponents(this.editorAssembly, this.asms));
        }

        internal static System.Type[] GetMaskComponents(bool editor, System.Collections.Generic.List<AssemblyInfo> assemblies) =>
            UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponent>()
                .Where(type => type.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, type, assemblies) &&
                    type.GetFields(BindingFlags.Instance | BindingFlags.Public).Length > 0)
                .OrderBy(type => type.FullName, System.StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, System.StringComparer.Ordinal).ToArray();

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
