using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor {

    [CodeGeneratorOrder(-100)]
    public class EntityTypeCodeGenerator : CustomCodeGenerator {

        public static (System.Type, uint)[] GetAllTypes(out uint maxId) {

            var content = new System.Collections.Generic.List<(System.Type, uint)>();
            var id = 0u;
            var aspects = EditorUtils.GetTypesDerivedFrom(typeof(IEntityType));
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                content.Add((aspect, id));
                ++id;

            }

            maxId = id;
            return content.ToArray();

        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            var id = 0u;
            var content = new System.Collections.Generic.List<string>();
            var types = this.entityTypes;//EditorUtils.GetTypesDerivedFrom(typeof(IEntityType));
            
            {
                var data = $"EntityTypes.Init();";
                content.Add(data);
            }
            
            foreach (var type in types) {

                if (type.IsValueType == false) continue;
                if (type.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(type, true) == false) continue;
                
                var contentItem = new System.Collections.Generic.List<string>();
                var strType = EditorUtils.GetTypeName(type);

                contentItem.Add($"EntityTypes.Register<{strType}>({id});");
                ++id;
                
                content.AddRange(contentItem);

            }

            {
                var data = $"EntityTypes.groupsCount = {id}u;";
                content.Add(data);
            }

            dataList.AddRange(content);
            
        }

    }

}