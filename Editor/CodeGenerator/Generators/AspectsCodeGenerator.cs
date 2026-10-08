using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    /// <summary>
    /// Exports aspects registration data for generated code.
    /// </summary>
    public class AspectsCodeGenerator : CustomCodeGenerator {

        /// <summary>
        /// Source initialization kind used by <c>AspectsCodeGenerator</c>.
        /// </summary>
        public override string SourceInitializationKind => this.GetType() == typeof(AspectsCodeGenerator) ? "aspects" : base.SourceInitializationKind;
        /// <summary>
        /// Source registration kind used by <c>AspectsCodeGenerator</c>.
        /// </summary>
        public override string SourceRegistrationKind => this.GetType() == typeof(AspectsCodeGenerator) ? "aspect-construction" : base.SourceRegistrationKind;

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<System.Type> references) {

            var aspects = this.aspects;
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect, true) == false) continue;

                references.Add(aspect);
                // Collect assembly dependencies only. Constructor availability is checked
                // by the compiler against the newly compiled catalog, not the loaded DLL.
                var fields = aspect.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var field in fields) {
                    var fieldType = field.FieldType;
                    if (typeof(IAspectData).IsAssignableFrom(fieldType) == true) {
                        references.Add(fieldType);
                        if (fieldType.IsGenericType) references.AddRange(fieldType.GenericTypeArguments);
                    }
                }
                
                
            }
            
        }

    }

}
