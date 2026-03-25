using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class EntityTypeCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            var id = 0;
            var content = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IEntityType)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                if (this.cache.TryGetValue<System.Collections.Generic.List<string>>(aspect, out var cacheData) == true) {
                    content.AddRange(cacheData);
                    continue;
                }

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect, true) == false) continue;
                
                var contentItem = new System.Collections.Generic.List<string>();
                var type = aspect;
                var strType = EditorUtils.GetTypeName(type);
                
                contentItem.Add($"EntityTypes<{strType}>.id = {id};");
                ++id;
                
                this.cache.Add(aspect, contentItem);
                content.AddRange(contentItem);

            }

            {
                if (this.cache.TryGetValue<System.Collections.Generic.List<string>>(typeof(EntityTypes), out var cacheData) == true) {
                    content.AddRange(cacheData);
                } else {
                    var data = $"EntityTypes.groupsCount = {id}u;";
                    this.cache.Add(typeof(EntityTypes), data);
                    content.Add(data);
                }
            }

            this.cache.Push();
            
            dataList.AddRange(content);
            
        }

    }

}