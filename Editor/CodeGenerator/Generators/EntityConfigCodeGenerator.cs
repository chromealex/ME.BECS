using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class EntityConfigCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
            {
                var str = "StaticTypes.collectionsCount.Resize(StaticTypes.counter + 1u);";
                dataList.Add(str);
            }
            var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponent>().OrderBy(x => x.FullName).ToArray();
            var allStaticComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentStatic>().OrderBy(x => x.FullName).ToArray();
            var allSharedComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentShared>().OrderBy(x => x.FullName).ToArray();
            allComponents = allComponents.Concat(allStaticComponents).Concat(allSharedComponents).ToArray();
            foreach (var component in allComponents) {

                if (component.IsValueType == false) continue;
                if (this.IsValidTypeForAssembly(component) == false) continue;

                var collectionsCount = GetCollectionsCount(component);
                if (collectionsCount == 0u) continue;
                var type = component.FullName.Replace("+", ".");
                var str = $"StaticTypes<{type}>.SetCollectionsCount({collectionsCount}u);";
                dataList.Add(str);

            }
            
        }

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            var definitions = new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            var content = new System.Collections.Generic.List<string>();
            var allComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponent>().OrderBy(x => x.FullName).ToArray();
            var allStaticComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentStatic>().OrderBy(x => x.FullName).ToArray();
            var allSharedComponents = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentShared>().OrderBy(x => x.FullName).ToArray();
            allComponents = allComponents.Concat(allStaticComponents).Concat(allSharedComponents).ToArray();
            foreach (var component in allComponents) {

                if (component.IsValueType == false) continue;
                if (this.IsValidTypeForAssembly(component) == false) continue;

                var type = component;
                var strType = EditorUtils.GetTypeName(type);
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).OrderBy(x => x.FieldType.FullName).ToArray();
                var count = 0u;
                content.Clear();
                foreach (var field in fields) {
                    var fieldType = field.FieldType;
                    if (typeof(IUnmanagedList).IsAssignableFrom(fieldType) == true) {
                        var gType = fieldType.GenericTypeArguments[0];
                        if (gType.IsVisible == false) continue;
                        content.Add("{");
                        content.Add($"var component = ({strType}*)componentPtr;");
                        content.Add($"var res = config.GetCollectionById(component->{field.Name}.GetConfigId(), out var data, out var length);");
                        content.Add("if (res == true) {");
                        content.Add($"if (component->{field.Name}.IsCreated == true) component->{field.Name}.Dispose();");
                        content.Add($"component->{field.Name} = new {EditorUtils.GetDataTypeName(fieldType)}<{EditorUtils.GetTypeName(gType)}>(in ent, data, length);");
                        content.Add("}");
                        content.Add("}");
                        ++count;
                    }
                }

                if (count > 0u) {
                    var def = new CodeGenerator.MethodDefinition() {
                        methodName = $"EntityConfigComponentApply{EditorUtils.GetCodeName(strType)}",
                        type = strType,
                        registerMethodName = "RegisterConfigComponentCallback",
                        definition = "in UnsafeEntityConfig config, void* componentPtr, in Ent ent",
                        content = string.Join("\n", content),
                        burstCompile = true,
                        pInvoke = "ME.BECS.UnsafeEntityConfig.MethodCallerDelegate",
                    };
                    definitions.Add(def);
                }

            }
            
            return definitions;

        }

        private static uint GetCollectionsCount(System.Type componentType) {
            var count = 0u;
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields) {
                if (typeof(IUnmanagedList).IsAssignableFrom(field.FieldType) == true) {
                    ++count;
                }
            }
            return count;
        }

    }

}