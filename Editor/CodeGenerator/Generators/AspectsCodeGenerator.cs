using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class AspectsCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
            var content = new System.Collections.Generic.List<string>();
			var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAspect)).OrderBy(x => x.FullName).ToArray();
			var componentsToValidate = new System.Collections.Generic.HashSet<System.Type>();
            
            foreach (var aspect in aspects) {
                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;
                if (this.IsValidTypeForAssembly(aspect, true) == false) continue;
                
                var fields = aspect.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var field in fields) {
                    var fieldType = field.FieldType;
                    if (typeof(IAspectData).IsAssignableFrom(fieldType) == true) {
                        var gType = fieldType.GenericTypeArguments[0];
                        if (gType.IsVisible == false) continue;
                        if (gType.IsValueType && typeof(IComponentBase).IsAssignableFrom(gType)) {
                            componentsToValidate.Add(gType);
                        }
                    }
                }
            }
            
            if (componentsToValidate.Count > 0) {
                var validations = componentsToValidate
                    .Where(t => t != null && t.IsValueType && typeof(IComponentBase).IsAssignableFrom(t))
                    .OrderBy(t => t.FullName)
                    .Select(t => {
                        var isTag = IsTagTypeLocal(t).ToString().ToLower();
                        var typeName = EditorUtils.GetDataTypeName(t);
                        return $"StaticTypes<{typeName}>.Validate(isTag: {isTag});";
                    });
                content.AddRange(validations);
            }
            
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
						componentsToValidate.Add(gType);
                        types.Add(EditorUtils.GetTypeName(gType));
                        references.Add(gType);
                        content.Add($"StaticTypes<{EditorUtils.GetTypeName(gType)}>.Validate(isTag: false);");
                    }
                }

                var str = $"AspectTypeInfo<{strType}>.Validate();";
                contentItem.Add(str);
                if (fieldsCount > 0 && fieldsCount == types.Count) {
                    references.Add(type);
                    str = $"AspectTypeInfo.with.Get(AspectTypeInfo<{strType}>.typeId).Resize({types.Count});";
                    contentItem.Add(str);
                    for (int i = 0; i < types.Count; ++i) {
                        str = $"AspectTypeInfo.with.Get(AspectTypeInfo<{strType}>.typeId).Get({i}) = StaticTypes<{types[i]}>.typeId;";
                        contentItem.Add(str);
                    }
                }
                
                this.cache.Add(aspect, contentItem);
                content.AddRange(contentItem);

            }
            
            this.cache.Push();
            
            dataList.AddRange(content);
            
        }

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            var content = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAspect)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect, true) == false) continue;

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
                        var fieldOffset = System.Runtime.InteropServices.Marshal.OffsetOf(type, field.Name);
                        var gTypeName = EditorUtils.GetDataTypeName(gType);
                        var ptrTypeName = $"ME.BECS.AspectDataPtr<{gTypeName}>";
                        types.Add($"*(({ptrTypeName}*)(addr + {fieldOffset})) = new {ptrTypeName}(in world);");
                    }
                }

                if (fieldsCount > 0) {
                    var str = $@"{{
ref var aspect = ref world.InitializeAspect<{strType}>();
var addr = (byte*)_addressPtr(ref aspect);
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

        private static bool IsTagTypeLocal(System.Type type) {
            if (type == null || type.IsGenericParameter || type.ContainsGenericParameters) return false;
            try {
                if (System.Runtime.InteropServices.Marshal.SizeOf(type) <= 1 &&
                    type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length == 0) {
                    return true;
                }
            } catch {
                return false;
            }
            return false;
        }

    }

}