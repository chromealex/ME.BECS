using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Generators {

    public class GenericComponentsCodeGenerator : CustomCodeGenerator {

        private struct GenericDefInfo {
            public System.Type constraintType;
            public System.Type[] possibleTypes;
        }

        private struct ValidationInfo {
            public string validationCall;
        }

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
            var genericComponentDefinitions = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IComponent))
                .Where(x => x.IsGenericTypeDefinition && x.IsValueType && typeof(IComponent).IsAssignableFrom(x))
                .ToArray();

            var allInstantiations = new System.Collections.Generic.HashSet<System.Type>();

            this.cache.SetKey("GenericDefInfo");
            foreach (var genericDef in genericComponentDefinitions) {
                
                if (this.IsValidTypeForAssembly(genericDef, true) == false) continue;
                if (genericDef.GetGenericArguments().Length != 1) continue;

                GenericDefInfo defInfo;
                if (this.cache.TryGetValue<GenericDefInfo>(genericDef, out var cachedDefInfo) == false) {
                    var typeParams = genericDef.GetGenericArguments();
                    System.Type constraintType = null;
                    System.Type[] allConstraints = null;
                    if (typeParams.Length > 0) {
                        allConstraints = typeParams[0].GetGenericParameterConstraints();
                        constraintType = allConstraints.FirstOrDefault(x => x.IsInterface == true);
                        if (constraintType == null && allConstraints.Length > 0) {
                            constraintType = allConstraints.FirstOrDefault(x => x.IsValueType);
                        }
                    }

                    System.Type[] possibleTypes = null;
                    if (constraintType != null) {
                        if (constraintType.IsInterface) {
                            possibleTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(constraintType)
                                .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                                .ToArray();
                        } else if (constraintType.IsValueType) {
                            possibleTypes = new[] { constraintType };
                        }
                    }

                    defInfo = new GenericDefInfo {
                        constraintType = constraintType,
                        possibleTypes = possibleTypes ?? new System.Type[0]
                    };
                    this.cache.Add(genericDef, defInfo);
                } else {
                    defInfo = cachedDefInfo;
                }

                if (defInfo.constraintType != null && defInfo.possibleTypes.Length > 0) {
                    foreach (var possibleType in defInfo.possibleTypes) {
                        if (this.IsValidTypeForAssembly(possibleType) == false) continue;
                        try {
                            var instantiated = genericDef.MakeGenericType(possibleType);
                            if (typeof(IComponent).IsAssignableFrom(instantiated)) {
                                allInstantiations.Add(instantiated);
                                if (references.Contains(instantiated) == false) {
                                    references.Add(instantiated);
                                }
                            }
                        } catch {
                        }
                    }
                }
            }

            var validatedTypes = new System.Collections.Generic.HashSet<System.Type>();
            this.cache.SetKey("ValidationInfo");
            foreach (var componentType in allInstantiations.OrderBy(x => x.FullName)) {
                if (!validatedTypes.Add(componentType)) continue;

                ValidationInfo validationInfo;
                if (this.cache.TryGetValue<ValidationInfo>(componentType, out var cachedValidation) == false) {
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

                    validationInfo = new ValidationInfo {
                        validationCall = validationCall
                    };
                    this.cache.Add(componentType, validationInfo);
                } else {
                    validationInfo = cachedValidation;
                }
                
                dataList.Add(validationInfo.validationCall);
            }
            
        }

    }

}

