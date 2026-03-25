using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor {

    [CodeGeneratorOrder(-100)]
    public class EntityTypeCodeGenerator : CustomCodeGenerator {

        public static (System.Type, uint)[] GetAllTypes() {
            
            var content = new System.Collections.Generic.List<(System.Type, uint)>();
            var id = 0u;
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IEntityType)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                content.Add((aspect, id));
                ++id;

            }

            return content.ToArray();

        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            var id = 0u;
            var content = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IEntityType)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect, true) == false) continue;
                
                var contentItem = new System.Collections.Generic.List<string>();
                var type = aspect;
                var strType = EditorUtils.GetTypeName(type);

                contentItem.Add($"EntityTypes<{strType}>.id = {id};");
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