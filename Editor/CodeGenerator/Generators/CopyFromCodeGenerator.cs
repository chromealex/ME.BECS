using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class CopyFromCodeGenerator : CustomCodeGenerator {

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            if (this.editorAssembly == true) return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            // TODO: Disabled for now because of Burst Compiler failed in Unity Cloud
            if (this.editorAssembly == false) return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            
            var definitions = new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponent>().OrderBy(x => x.FullName).ToArray();
            foreach (var component in allComponents) {

                var contentItem = new System.Collections.Generic.List<string>();
                var type = component;
                var strType = EditorUtils.GetTypeName(type);
                if (this.cache.TryGetValue<System.Collections.Generic.List<string>>(component, out var cacheData) == true) {
                    contentItem.AddRange(cacheData);
                } else {

                    if (component.IsValueType == false) continue;
                    if (this.IsValidTypeForAssembly(component) == false) continue;

                    var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).OrderBy(x => x.FieldType.FullName).ToArray();
                    foreach (var field in fields) {
                        var fieldType = field.FieldType;
                        if (typeof(IUnmanagedList).IsAssignableFrom(fieldType) == true) {
                            var gType = fieldType.GenericTypeArguments[0];
                            if (gType.IsVisible == false) continue;
                            contentItem.Add("/*{");
                            contentItem.Add($"var source = ({strType}*)componentPtr;");
                            contentItem.Add($"ref var target = ref ent.Get<{strType}>();");
                            contentItem.Add(
                                $"target.{field.Name} = new {EditorUtils.GetDataTypeName(fieldType)}<{EditorUtils.GetTypeName(gType)}>(in ent, in source->{field.Name});");
                            var cloneMethod = gType.GetMethod("CopyFrom");
                            if (cloneMethod != null && cloneMethod.GetParameters().Length == 2) {
                                var p = cloneMethod.GetParameters();
                                if (p[0].ParameterType.IsByRef == true && p[0].ParameterType.GetElementType() == gType &&
                                    p[1].ParameterType.IsByRef == true && p[1].ParameterType.GetElementType() == typeof(Ent)) {
                                    contentItem.Add(
                                        $"for (uint i = 0u; i < target.{field.Name}.ElementsCount; ++i) target.{field.Name}[i].CopyFrom(in source->{field.Name}[i], in ent);");
                                }
                            }

                            contentItem.Add("}*/");
                        }
                    }

                    if (contentItem.Count > 0) this.cache.Add(component, contentItem);
                }

                if (contentItem.Count > 0u) {
                    var def = new CodeGenerator.MethodDefinition() {
                        methodName = $"CopyFrom{EditorUtils.GetCodeName(strType)}",
                        type = strType,
                        registerMethodName = "RegisterCopyFromComponentCallback",
                        definition = "void* componentPtr, in Ent ent",
                        content = string.Join("\n", contentItem),
                        burstCompile = true,
                        pInvoke = "ME.BECS.WorldStaticCallbacks.CopyFromComponentCallbackDelegate",
                    };
                    definitions.Add(def);
                }
                
            }
            
            return definitions;

        }

    }

}