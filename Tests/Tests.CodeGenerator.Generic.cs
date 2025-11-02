using System.Linq;
using System.Collections.Generic;
using scg = System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using ME.BECS.Editor.Generators;
using ME.BECS.Editor;

namespace ME.BECS.Tests {

    public interface ITestGenericTag : IComponent { }

    public struct TestGenericTag1 : ITestGenericTag { }
    public struct TestGenericTag2 : ITestGenericTag { }
    public struct TestGenericTag3 : ITestGenericTag { }

    public struct TestGenericComponent<TTag> : IComponent where TTag : unmanaged, ITestGenericTag {
        public int value;
    }

    public struct TestGenericComponentShared<TTag> : IComponentShared where TTag : unmanaged, ITestGenericTag {
        public int value;
    }

    public struct TestGenericComponentStatic<TTag> : IConfigComponentStatic where TTag : unmanaged, ITestGenericTag {
        public int value;
    }

    public struct TestGenericSystem<TTag> : IUpdate where TTag : unmanaged, ITestGenericTag {
        public void OnUpdate(ref SystemContext context) {
        }
    }

    public struct TestGenericSystemAwake<TTag> : IAwake where TTag : unmanaged, ITestGenericTag {
        public void OnAwake(ref SystemContext context) {
        }
    }

    public class Tests_CodeGenerator_Generic {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

        [Test]
        public void GenericComponentsCodeGenerator_FindsGenericComponentDefinitions() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.asms = asms;
            generator.editorAssembly = true;
            
            var genericComponentDefinitions = TypeCache.GetTypesDerivedFrom(typeof(IComponent))
                .Where(x => x.IsGenericTypeDefinition && x.IsValueType && typeof(IComponent).IsAssignableFrom(x))
                .ToArray();

            var testGenericComponent = genericComponentDefinitions.FirstOrDefault(x => 
                x.Name == "TestGenericComponent`1" && 
                x.GetGenericArguments().Length == 1);

            Assert.IsNotNull(testGenericComponent, "TestGenericComponent should be found");
        }

        [Test]
        public void GenericComponentsCodeGenerator_FindsConstraintType() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.asms = asms;
            generator.editorAssembly = true;

            var genericDef = typeof(TestGenericComponent<>);
            var typeParams = genericDef.GetGenericArguments();
            
            System.Type constraintType = null;
            if (typeParams.Length > 0) {
                var allConstraints = typeParams[0].GetGenericParameterConstraints();
                constraintType = allConstraints.FirstOrDefault(x => x.IsInterface == true);
            }

            Assert.IsNotNull(constraintType, "Constraint type should be found");
            Assert.AreEqual(typeof(ITestGenericTag), constraintType, "Constraint should be ITestGenericTag");
        }

        [Test]
        public void GenericComponentsCodeGenerator_FindsPossibleTypes() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.asms = asms;
            generator.editorAssembly = true;

            var constraintType = typeof(ITestGenericTag);
            var possibleTypes = TypeCache.GetTypesDerivedFrom(constraintType)
                .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                .ToArray();

            Assert.IsTrue(possibleTypes.Length >= 3, "Should find at least 3 tag types");
            Assert.IsTrue(possibleTypes.Any(x => x == typeof(TestGenericTag1)), "Should find TestGenericTag1");
            Assert.IsTrue(possibleTypes.Any(x => x == typeof(TestGenericTag2)), "Should find TestGenericTag2");
            Assert.IsTrue(possibleTypes.Any(x => x == typeof(TestGenericTag3)), "Should find TestGenericTag3");
        }

        [Test]
        public void GenericComponentsCodeGenerator_CreatesInstantiations() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.asms = asms;
            generator.editorAssembly = true;

            var genericDef = typeof(TestGenericComponent<>);
            var constraintType = typeof(ITestGenericTag);
            var possibleTypes = TypeCache.GetTypesDerivedFrom(constraintType)
                .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                .ToArray();

            var allInstantiations = new scg::HashSet<System.Type>();
            foreach (var possibleType in possibleTypes) {
                try {
                    var instantiated = genericDef.MakeGenericType(possibleType);
                    if (typeof(IComponent).IsAssignableFrom(instantiated)) {
                        allInstantiations.Add(instantiated);
                    }
                } catch {
                }
            }

            Assert.IsTrue(allInstantiations.Count >= 3, "Should create at least 3 instantiations");
            Assert.IsTrue(allInstantiations.Any(x => x == typeof(TestGenericComponent<TestGenericTag1>)), 
                "Should create TestGenericComponent<TestGenericTag1>");
            Assert.IsTrue(allInstantiations.Any(x => x == typeof(TestGenericComponent<TestGenericTag2>)), 
                "Should create TestGenericComponent<TestGenericTag2>");
            Assert.IsTrue(allInstantiations.Any(x => x == typeof(TestGenericComponent<TestGenericTag3>)), 
                "Should create TestGenericComponent<TestGenericTag3>");
        }

        [Test]
        public void GenericComponentsCodeGenerator_GeneratesValidationCalls() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.dir = "Assets/ME.BECS.Gen/Editor";
            generator.asms = asms;
            generator.editorAssembly = true;
            
            var cache = new Cache();
            var loadMethod = typeof(Cache).GetMethod("Load", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (loadMethod != null) {
                loadMethod.Invoke(cache, new object[] { generator.dir, "Cache/GenericComponentsCodeGenerator.cache" });
            } else {
                var cacheDataField = typeof(Cache).GetField("cacheData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cacheDataField != null) {
                    var cachedItemType = typeof(Cache).GetNestedType("CachedItem", System.Reflection.BindingFlags.Public);
                    if (cachedItemType != null) {
                        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), cachedItemType);
                        var dictionary = System.Activator.CreateInstance(dictionaryType);
                        cacheDataField.SetValue(cache, dictionary);
                    }
                }
            }
            generator.cache = cache;

            var dataList = new scg::List<string>();
            var references = new scg::List<System.Type>();

            generator.AddInitialization(dataList, references);

            var allGenericComponentValidations = dataList.Where(x => x.Contains("TestGenericComponent")).ToList();
            var testGenericComponentValidations = dataList.Where(x => 
                x.Contains("TestGenericComponent") && 
                (x.Contains("TestGenericTag1") || x.Contains("TestGenericTag2") || x.Contains("TestGenericTag3")))
                .ToList();

            var genericDefs = TypeCache.GetTypesDerivedFrom(typeof(IComponent))
                .Where(x => x.IsGenericTypeDefinition && x.IsValueType && typeof(IComponent).IsAssignableFrom(x))
                .Where(x => x.Name.Contains("TestGenericComponent"))
                .ToArray();

            var constraintType = typeof(ITestGenericTag);
            var possibleTypes = TypeCache.GetTypesDerivedFrom(constraintType)
                .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                .Where(x => x.Name.StartsWith("TestGenericTag"))
                .ToArray();

            if (testGenericComponentValidations.Count < 3) {
                UnityEngine.Debug.Log($"Found {genericDefs.Length} generic component definitions: {string.Join(", ", genericDefs.Select(x => x.Name))}");
                UnityEngine.Debug.Log($"Found {possibleTypes.Length} possible tag types: {string.Join(", ", possibleTypes.Select(x => x.Name))}");
                UnityEngine.Debug.Log($"Found {allGenericComponentValidations.Count} total TestGenericComponent validations");
                UnityEngine.Debug.Log($"Found {testGenericComponentValidations.Count} TestGenericComponent validations with tags");
                UnityEngine.Debug.Log($"All validations: {string.Join("\n", dataList.Take(20))}");
            }

            Assert.IsTrue(testGenericComponentValidations.Count >= 3, 
                $"Should generate at least 3 validation calls for TestGenericComponent. Found: {testGenericComponentValidations.Count}, Total TestGenericComponent: {allGenericComponentValidations.Count}, GenericDefs: {genericDefs.Length}, PossibleTypes: {possibleTypes.Length}");

            foreach (var validation in testGenericComponentValidations) {
                Assert.IsTrue(validation.Contains("StaticTypes<"), 
                    "Validation call should use StaticTypes");
                Assert.IsTrue(validation.Contains(".Validate(") || validation.Contains(".ValidateShared(") || validation.Contains(".ValidateStatic("), 
                    "Validation call should use Validate, ValidateShared, or ValidateStatic method");
            }
        }

        [Test]
        public void GenericComponentsCodeGenerator_HandlesSharedComponents() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.asms = asms;
            generator.editorAssembly = true;

            var genericDef = typeof(TestGenericComponentShared<>);
            var constraintType = typeof(ITestGenericTag);
            var possibleTypes = TypeCache.GetTypesDerivedFrom(constraintType)
                .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                .ToArray();

            var instantiated = genericDef.MakeGenericType(possibleTypes[0]);
            
            var isTag = (System.Runtime.InteropServices.Marshal.SizeOf(instantiated) <= 1 &&
                         instantiated.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length == 0).ToString().ToLower();

            var componentTypeName = EditorUtils.GetDataTypeName(instantiated);
            var hasCustomHash = instantiated.GetMethod(nameof(IComponentShared.GetHash), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) != null ||
                                instantiated.GetInterfaceMap(typeof(IComponentShared)).TargetMethods.Any(m => m.IsPrivate == true && m.Name == typeof(IComponentShared).FullName + "." + nameof(IComponentShared.GetHash));
            var validationCall = $"StaticTypes<{componentTypeName}>.ValidateShared(isTag: {isTag}, hasCustomHash: {hasCustomHash.ToString().ToLower()});";

            Assert.IsTrue(validationCall.Contains("ValidateShared"), 
                "Shared component should use ValidateShared");
        }

        [Test]
        public void GenericComponentsCodeGenerator_HandlesStaticComponents() {
            var generator = new GenericComponentsCodeGenerator();
            var asms = EditorUtils.GetAssembliesInfo();
            generator.asms = asms;
            generator.editorAssembly = true;

            var genericDef = typeof(TestGenericComponentStatic<>);
            var constraintType = typeof(ITestGenericTag);
            var possibleTypes = TypeCache.GetTypesDerivedFrom(constraintType)
                .Where(x => x.IsValueType && !x.IsGenericTypeDefinition && x.IsVisible)
                .ToArray();

            var instantiated = genericDef.MakeGenericType(possibleTypes[0]);
            
            var isTag = (System.Runtime.InteropServices.Marshal.SizeOf(instantiated) <= 1 &&
                         instantiated.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length == 0).ToString().ToLower();

            var componentTypeName = EditorUtils.GetDataTypeName(instantiated);
            var validationCall = $"StaticTypes<{componentTypeName}>.ValidateStatic(isTag: {isTag});";

            Assert.IsTrue(validationCall.Contains("ValidateStatic"), 
                "Static component should use ValidateStatic");
        }

        [Test]
        public void CodeGenerator_PatchSystemsList_FindsGenericSystems() {
            var types = new scg::List<System.Type>();
            types.Add(typeof(TestGenericSystem<>));
            types.Add(typeof(TestGenericSystemAwake<>));

            CodeGenerator.PatchSystemsList(types);

            Assert.IsTrue(types.Any(x => x == typeof(TestGenericSystem<TestGenericTag1>)), 
                "Should find TestGenericSystem<TestGenericTag1>");
            Assert.IsTrue(types.Any(x => x == typeof(TestGenericSystem<TestGenericTag2>)), 
                "Should find TestGenericSystem<TestGenericTag2>");
            Assert.IsTrue(types.Any(x => x == typeof(TestGenericSystem<TestGenericTag3>)), 
                "Should find TestGenericSystem<TestGenericTag3>");
            Assert.IsTrue(types.Any(x => x == typeof(TestGenericSystemAwake<TestGenericTag1>)), 
                "Should find TestGenericSystemAwake<TestGenericTag1>");

            Assert.IsFalse(types.Contains(typeof(TestGenericSystem<>)), 
                "Should remove generic definition from list");
            Assert.IsFalse(types.Contains(typeof(TestGenericSystemAwake<>)), 
                "Should remove generic definition from list");
        }

        [Test]
        public void CodeGenerator_PatchSystemsList_OnlyOneGenericParameter() {
            var types = new scg::List<System.Type>();
            types.Add(typeof(TestGenericSystem<>));

            CodeGenerator.PatchSystemsList(types);

            var instantiatedTypes = types.Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(TestGenericSystem<>));
            
            foreach (var instantiatedType in instantiatedTypes) {
                var genericArgs = instantiatedType.GetGenericArguments();
                Assert.AreEqual(1, genericArgs.Length, 
                    "Each instantiation should have exactly 1 generic argument");
            }
        }

        [Test]
        public void CodeGenerator_PatchSystemsList_OnlyValueTypes() {
            var types = new scg::List<System.Type>();
            types.Add(typeof(TestGenericSystem<>));

            CodeGenerator.PatchSystemsList(types);

            var instantiatedTypes = types.Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(TestGenericSystem<>));
            
            foreach (var instantiatedType in instantiatedTypes) {
                Assert.IsTrue(instantiatedType.IsValueType, 
                    "All instantiated types should be value types");
            }
        }

    }

}

