using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor {

    [CodeGeneratorOrder(-100)]
    public class EntityTypeCodeGenerator : CustomCodeGenerator {

        public static (System.Type, uint)[] GetAllTypes(CustomCodeGenerator codeGenerator, out uint count) {

            var content = new System.Collections.Generic.List<(System.Type, uint)>();
            var id = 0u;
            foreach (var type in codeGenerator.entityTypes) {

                if (type.IsValueType == false) continue;
                if (type.IsVisible == false) continue;
                if (codeGenerator.IsValidTypeForAssembly(type, true) == false) continue;

                content.Add((type, id));
                ++id;

            }

            count = id;
            return content.ToArray();

        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            // The compiler owns registration statements and IDs from the ordered manifest.
            // Keep this call in the existing initialization slot; missing inputs fail compilation.
            dataList.Add("global::ME.BECS.SourceGenerated.EntityInputs.Initialize();");
            
        }

    }

}
