using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ME.BECS.Editor.Generators {

    public class GenericComponentsCodeGenerator : CustomCodeGenerator {

        private static Type[] _cachedGenericComponentDefinitions;
        private static readonly System.Collections.Generic.Dictionary<Type, Type[]> _cachedPossibleTypes = new System.Collections.Generic.Dictionary<Type, Type[]>();

        private struct GenericDefInfo {
            public Type constraintType;
            public Type[] possibleTypes;
        }

        private struct ValidationInfo {
            public string validationCall;
            public string defaultValueCall;
        }

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<Type> references) {
            _cachedGenericComponentDefinitions ??= UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(IComponent))
                .Where(x => x.IsGenericTypeDefinition && x.IsValueType && typeof(IComponent).IsAssignableFrom(x))
                .ToArray();
            var genericComponentDefinitions = _cachedGenericComponentDefinitions;

            var allInstantiations = new System.Collections.Generic.HashSet<Type>();
            var referencesSet = new System.Collections.Generic.HashSet<Type>(references);

            cache.SetKey("GenericDefInfo");
            foreach (var genericDef in genericComponentDefinitions) {
                
                if (IsValidTypeForAssembly(genericDef, true) == false) continue;
                if (genericDef.GetGenericArguments().Length != 1) continue;

                GenericDefInfo defInfo;
                if (cache.TryGetValue<GenericDefInfo>(genericDef, out var cachedDefInfo) == false) {
                    var typeParams = genericDef.GetGenericArguments();
                    Type constraintType = null;
                    Type[] allConstraints = null;
                    if (typeParams.Length > 0) {
                        allConstraints = typeParams[0].GetGenericParameterConstraints();
                        constraintType = allConstraints.FirstOrDefault(x => x.IsInterface == true);
                        if (constraintType == null && allConstraints.Length > 0) {
                            constraintType = allConstraints.FirstOrDefault(x => x.IsValueType);
                        }
                    }

                    Type[] possibleTypes = null;
                    if (constraintType != null) {
                        if (constraintType.IsInterface) {
                            if (!_cachedPossibleTypes.TryGetValue(constraintType, out possibleTypes)) {
                                possibleTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(constraintType)
                                    .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                                    .ToArray();
                                _cachedPossibleTypes[constraintType] = possibleTypes;
                            }
                        } else if (constraintType.IsValueType) {
                            possibleTypes = new[] { constraintType };
                        }
                    }

                    defInfo = new GenericDefInfo {
                        constraintType = constraintType,
                        possibleTypes = possibleTypes ?? Type.EmptyTypes
                    };
                    cache.Add(genericDef, defInfo);
                } else {
                    defInfo = cachedDefInfo;
                }

                if (defInfo.constraintType != null && defInfo.possibleTypes.Length > 0) {
                    foreach (var possibleType in defInfo.possibleTypes) {
                        if (IsValidTypeForAssembly(possibleType) == false) continue;
                        try {
                            var instantiated = genericDef.MakeGenericType(possibleType);
                            if (allInstantiations.Add(instantiated)) {
                                if (referencesSet.Add(instantiated)) {
                                    references.Add(instantiated);
                                }
                            }
                        } catch {
                        }
                    }
                }
            }

            var validatedTypes = new System.Collections.Generic.HashSet<Type>();
            cache.SetKey("ValidationInfo");
            foreach (var componentType in allInstantiations.OrderBy(x => x.FullName)) {
                if (!validatedTypes.Add(componentType)) continue;

                ValidationInfo validationInfo;
                var componentTypeName = EditorUtils.GetDataTypeName(componentType);
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var isTagType = System.Runtime.InteropServices.Marshal.SizeOf(componentType) <= 1 &&
                                CodeGenerator.GetCachedFields(componentType, flags).Length == 0;
                var isTag = isTagType.ToString().ToLower();

                var cacheKeyType = componentType.IsGenericType && !componentType.IsGenericTypeDefinition 
                    ? componentType.GetGenericTypeDefinition() 
                    : componentType;

                if (cache.TryGetValue<ValidationInfo>(cacheKeyType, out var cachedValidation) == false) {
                    string validationCall;
                    if (typeof(IConfigComponentStatic).IsAssignableFrom(componentType)) {
                        validationCall = $"StaticTypes<{componentTypeName}>.ValidateStatic(isTag: {isTag});";
                    } else if (typeof(IComponentShared).IsAssignableFrom(componentType)) {
                        var hasCustomHash = CodeGenerator.GetCachedMethod(componentType, nameof(IComponentShared.GetHash), flags) != null ||
                                            CodeGenerator.GetCachedInterfaceMap(componentType, typeof(IComponentShared)).TargetMethods.Any(m => m.IsPrivate == true && m.Name == typeof(IComponentShared).FullName + "." + nameof(IComponentShared.GetHash));
                        validationCall = $"StaticTypes<{componentTypeName}>.ValidateShared(isTag: {isTag}, hasCustomHash: {hasCustomHash.ToString().ToLower()});";
                    } else {
                        validationCall = $"StaticTypes<{componentTypeName}>.Validate(isTag: {isTag});";
                    }

                    string defaultValueCall = null;
                    if (isTagType == false) {
                        var defaultProperty = cacheKeyType.GetProperty("Default", BindingFlags.Static | BindingFlags.Public);
                        if (defaultProperty != null) {
                            defaultValueCall = $"StaticTypes<{componentTypeName}>.SetDefaultValue({componentTypeName}.Default);";
                        }
                    }

                    validationInfo = new ValidationInfo {
                        validationCall = validationCall,
                        defaultValueCall = defaultValueCall
                    };
                    cache.Add(cacheKeyType, validationInfo);
                } else {
                    validationInfo = cachedValidation;
                    if (validationInfo.defaultValueCall == null && isTagType == false) {
                        if (cacheKeyType.GetProperty("Default", BindingFlags.Static | BindingFlags.Public) != null) {
                            validationInfo.defaultValueCall = $"StaticTypes<{componentTypeName}>.SetDefaultValue({componentTypeName}.Default);";
                            cache.Add(cacheKeyType, validationInfo);
                        }
                    }
                }
                
                dataList.Add(validationInfo.validationCall);
                if (validationInfo.defaultValueCall != null) {
                    dataList.Add(validationInfo.defaultValueCall);
                }
            }
            
        }

    }

}

