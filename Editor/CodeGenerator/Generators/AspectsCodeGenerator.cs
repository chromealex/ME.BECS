using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class AspectsCodeGenerator : CustomCodeGenerator {

        // Derived custom feeders may override AddInitialization: do not bypass them.
        public override string SourceInitializationKind => this.GetType() == typeof(AspectsCodeGenerator) ? "aspects" : null;
        public override string SourceRegistrationKind => this.GetType() == typeof(AspectsCodeGenerator) ? "aspect-construction" : null;

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            // Dependencies are collected by AddSourceGeneratorReferences before emission.
            dataList.Add("global::ME.BECS.SourceGenerated.AspectInputs.Initialize();");
            
        }

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

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            this.AddSourceGeneratorReferences(references);
            var def = new CodeGenerator.MethodDefinition() {
                generatedRegistration = "global::ME.BECS.SourceGenerated.AspectInputs.RegisterConstruction();",
            };
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>() { def };

        }

    }

}
