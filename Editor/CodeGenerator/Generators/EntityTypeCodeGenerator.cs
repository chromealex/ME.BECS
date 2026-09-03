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

            var content = new System.Collections.Generic.List<string>();
            var types = GetAllTypes(this, out var count);
            
            {
                var data = $"EntityTypes.Init();";
                content.Add(data);
            }
            
            foreach (var item in types) {
                var contentItem = new System.Collections.Generic.List<string>();
                var strType = EditorUtils.GetTypeName(item.Item1);

                contentItem.Add($"EntityTypes.Register<{strType}>({item.Item2});");
                
                content.AddRange(contentItem);

            }

            {
                var data = $"EntityTypes.groupsCount = {count}u;";
                content.Add(data);
            }

            dataList.AddRange(content);
            
        }

    }

}
