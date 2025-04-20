using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class AspectsCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
            var content = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAspect)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect) == false) continue;
                
                var type = aspect;
                var strType = EditorUtils.GetTypeName(type);
                var types = new System.Collections.Generic.List<string>();
                var fieldsCount = 0;
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var field in fields) {
                    var fieldType = field.FieldType;
                    if (typeof(IAspectData).IsAssignableFrom(fieldType) == true &&
                        field.GetCustomAttribute(typeof(QueryWithAttribute)) != null) {
                        ++fieldsCount;
                        var gType = fieldType.GenericTypeArguments[0];
                        if (gType.IsVisible == false) continue;
                        types.Add(EditorUtils.GetTypeName(gType));
                        references.Add(gType);
                    }
                }

                var str = $"AspectTypeInfo<{strType}>.Validate();";
                content.Add(str);
                if (fieldsCount > 0 && fieldsCount == types.Count) {
                    references.Add(type);
                    str = $"AspectTypeInfo.with.Get(AspectTypeInfo<{strType}>.typeId).Resize({types.Count});";
                    content.Add(str);
                    for (int i = 0; i < types.Count; ++i) {
                        str = $"AspectTypeInfo.with.Get(AspectTypeInfo<{strType}>.typeId).Get({i}) = StaticTypes<{types[i]}>.typeId;";
                        content.Add(str);
                    }
                }

            }
            
            dataList.AddRange(content);
            
        }

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            var content = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAspect)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect) == false) continue;

                var type = aspect;
                var strType = EditorUtils.GetTypeName(type);
                var types = new System.Collections.Generic.List<string>();
                var fieldsCount = 0;
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).OrderBy(x => x.FieldType.FullName).ToArray();
                foreach (var field in fields) {
                    var fieldType = field.FieldType;
                    if (typeof(IAspectData).IsAssignableFrom(fieldType) == true) {
                        ++fieldsCount;
                        var gType = fieldType.GenericTypeArguments[0];
                        if (gType.IsVisible == false) continue;
                        types.Add($"aspect.{field.Name} = new {EditorUtils.GetDataTypeName(fieldType)}<{EditorUtils.GetTypeName(gType)}>(in world);");
                    }
                }

                if (fieldsCount > 0 && fieldsCount == types.Count) {
                    var str = $@"{{
ref var aspect = ref world.InitializeAspect<{strType}>();
{string.Join("\n", types)}
}}";
                    content.Add(str);
                }
                
            }
            
            var def = new CodeGenerator.MethodDefinition() {
                methodName = "AspectsConstruct",
                type = "World",
                registerMethodName = "RegisterCallback",
                definition = "ref World world",
                content = string.Join("\n", content),
            };
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>() { def };

        }

    }

}