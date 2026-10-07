using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor {

    [CodeGeneratorOrder(-100)]
    public class EntityTypeCodeGenerator : CustomCodeGenerator {
        public override string SourceInitializationKind => this.GetType() == typeof(EntityTypeCodeGenerator) ? "entities" : base.SourceInitializationKind;

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
        
    }

}
