using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class AspectsCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
            var componentTypesToValidate = new System.Collections.Generic.List<System.Type>();
            var aspectValidations = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAspect)).OrderBy(x => x.FullName).ToArray();
            foreach (var aspect in aspects) {

                var isCached = this.cache.TryGetValue<System.Collections.Generic.List<string>>(aspect, out var cacheData);
                
                if (aspect.IsValueType == false) continue;
                if (aspect.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(aspect, true) == false) continue;
                
                var type = aspect;
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).OrderBy(x => x.FieldType.FullName).ToArray();
                
                if (isCached == false) {
                    var contentItem = new System.Collections.Generic.List<string>();
                    var strType = EditorUtils.GetTypeName(type);
                    var types = new System.Collections.Generic.List<string>();
                    var fieldsCount = 0;
                    foreach (var field in fields) {
                        var fieldType = field.FieldType;
                        if (typeof(IAspectData).IsAssignableFrom(fieldType) == true) {
                            var gType = fieldType.GenericTypeArguments[0];

                            if (gType.IsVisible == false) {
                                continue;
                            }
                            if (gType.IsGenericTypeDefinition) {
                                continue;
                            }
                            if (this.IsValidTypeForAssembly(gType) == false) {
                                continue;
                            }

                            if (typeof(IComponent).IsAssignableFrom(gType)) {
                                componentTypesToValidate.Add(gType);
                            }

                            if (references.Contains(gType) == false) {
                                references.Add(gType);
                            }

                            if (field.GetCustomAttribute(typeof(QueryWithAttribute)) != null) {
                                ++fieldsCount;
                                types.Add(EditorUtils.GetDataTypeName(gType));
                            }
                        }
                    }

                    var str = $"AspectTypeInfo<{strType}>.Validate();";
                    contentItem.Add(str);
                    if (fieldsCount > 0 && fieldsCount == types.Count) {
                        references.Add(type);
                        var aspectVarName = EditorUtils.GetCodeName(strType);
                        str = $"var {aspectVarName} = AspectTypeInfo<{strType}>.typeId;";
                        contentItem.Add(str);
                        str = $"AspectTypeInfo.with.Get({aspectVarName}).Resize({types.Count});";
                        
                        contentItem.Add(str);
                        for (int i = 0; i < types.Count; ++i) {
                            str = $"AspectTypeInfo.with.Get({aspectVarName}).Get({i}) = StaticTypes<{types[i]}>.typeId;";
                            contentItem.Add(str);
                        }
                    }
                    
                    this.cache.Add(aspect, contentItem);
                    aspectValidations.AddRange(contentItem);
                } else {
                    aspectValidations.AddRange(cacheData);
                    foreach (var field in fields) {
                        var fieldType = field.FieldType;
                        if (typeof(IAspectData).IsAssignableFrom(fieldType) == true) {
                            var gType = fieldType.GenericTypeArguments[0];
                            if (gType.IsVisible == false) continue;
                            if (gType.IsGenericTypeDefinition) continue;
                            if (this.IsValidTypeForAssembly(gType) == false) continue;
                            if (typeof(IComponent).IsAssignableFrom(gType)) {
                                componentTypesToValidate.Add(gType);
                                if (references.Contains(gType) == false) {
                                    references.Add(gType);
                                }
                            }
                        }
                    }
                }

            }
            
            this.cache.Push();
            var validatedComponentTypes = new System.Collections.Generic.HashSet<System.Type>();
            foreach (var componentType in componentTypesToValidate.Distinct()) {
                if (!validatedComponentTypes.Add(componentType)) continue;

                var componentTypeName = EditorUtils.GetDataTypeName(componentType);
                var isTag = (System.Runtime.InteropServices.Marshal.SizeOf(componentType) <= 1 &&
                             componentType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length == 0).ToString().ToLower();

                string validationCall;
                if (typeof(IConfigComponentStatic).IsAssignableFrom(componentType)) {
                    validationCall = $"StaticTypes<{componentTypeName}>.ValidateStatic(isTag: {isTag});";
                } else if (typeof(IComponentShared).IsAssignableFrom(componentType)) {
                    var hasCustomHash = componentType.GetMethod(nameof(IComponentShared.GetHash), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) != null ||
                                        componentType.GetInterfaceMap(typeof(IComponentShared)).TargetMethods.Any(m => m.IsPrivate == true && m.Name == typeof(IComponentShared).FullName + "." + nameof(IComponentShared.GetHash));
                    validationCall = $"StaticTypes<{componentTypeName}>.ValidateShared(isTag: {isTag}, hasCustomHash: {hasCustomHash.ToString().ToLower()});";
                } else {
                    validationCall = $"StaticTypes<{componentTypeName}>.Validate(isTag: {isTag});";
                }
                dataList.Add(validationCall);
            }
            dataList.AddRange(aspectValidations);
            
        }

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            var content = new System.Collections.Generic.List<string>();
            var aspects = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IAspect)).OrderBy(x => x.FullName).ToArray();
            var componentsFromMethods = new System.Collections.Generic.HashSet<System.Type>();
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
                    if (typeof(IAspectData).IsAssignableFrom(fieldType) != true)
                        continue;
                    ++fieldsCount;
                    var gType = fieldType.GenericTypeArguments[0];
                    if (gType.IsVisible == false) continue;
                    if (gType.IsGenericTypeDefinition) continue;
                    if (this.IsValidTypeForAssembly(gType) == false) continue;
                    if (typeof(IComponent).IsAssignableFrom(gType)) {
                        componentsFromMethods.Add(gType);
                        if (references.Contains(gType) == false) {
                            references.Add(gType);
                        }
                    }
                    var fieldOffset = System.Runtime.InteropServices.Marshal.OffsetOf(type, field.Name);
                    var t = EditorUtils.FormatGenericTypeWithSingleArgument(fieldType, gType);
                    types.Add($"*(({t}*)(addr + {fieldOffset})) = new ME.BECS.AspectDataPtr<{EditorUtils.GetDataTypeName(gType)}>(in world);");
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

    }

}