
using System.Linq;

namespace ME.BECS.Editor {

    public class ComponentDestroyCodeGenerator : CustomCodeGenerator {

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            var definitions = new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentDestroy>().OrderBy(x => x.FullName).ToArray();
            foreach (var component in allComponents) {

                var contentItem = new System.Collections.Generic.List<string>();
                var type = component;
                var strType = EditorUtils.GetTypeName(type);
                                if (this.cache.TryGetValue<System.Collections.Generic.List<string>>(component, out var cacheData) == true) {
                    contentItem.AddRange(cacheData);
                } else {

                    if (component.IsValueType == false) continue;
                    if (this.IsValidTypeForAssembly(component) == false) continue;

                    contentItem.Add("{");
                    contentItem.Add("if (comp == null) {");
                    contentItem.Add($"default({strType}).Destroy(in ent);");
                    contentItem.Add("} else {");
                    contentItem.Add($"_ref(({strType}*)comp).Destroy(in ent);");
                    contentItem.Add("}");
                    contentItem.Add("}");

                    if (contentItem.Count > 0) this.cache.Add(component, contentItem);
                }

                if (contentItem.Count > 0u) {
                    var def = new CodeGenerator.MethodDefinition() {
                        methodName = $"AutoDestroyRegistry_Destroy_{EditorUtils.GetCodeName(strType)}",
                        type = strType,
                        registerMethodName = "RegisterAutoDestroyCallback",
                        definition = "in Ent ent, byte* comp",
                        content = string.Join("\n", contentItem),
                        burstCompile = true,
                        pInvoke = "AutoDestroyRegistry.DestroyDelegate",
                    };
                    definitions.Add(def);
                }

            }

            return definitions;

        }

    }

}
