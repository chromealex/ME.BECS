using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorBootstrapOwnership {
        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void BootstrapUsesManifestWithoutExportedMarkers(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var ns = profile == "Editor" ? "ME.BECS.Editor" : "ME.BECS";
            var schema = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Single(row => row[0] == profile.ToLowerInvariant() && row[1] == "bootstrap-schema");
            Assert.AreEqual("0", schema[2]);
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schema[3])));
            var initializer = assembly.GetType(ns + ".StaticTypesInitializer", true);
            var registry = assembly.GetType(ns + ".StaticMethods", true);
            var debug = assembly.GetType(ns + ".DebugJobs", true);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (var pair in new[] {
                (initializer, "SourceBootstrapPlanV1"), (registry, "SourceRegistrationPlanV1"),
                (registry, "SourceSystemDependenciesV1"), (debug, "SourceDebugPlanV1"),
            }) Assert.IsEmpty(pair.Item1.GetMember(pair.Item2, all), "Regenerate inputs and retirement stubs; markers must no longer own bootstrap.");
            Assert.IsTrue(Attribute.IsDefined(initializer.GetMethod("Load"), typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            Assert.IsTrue(Attribute.IsDefined(initializer.GetMethod("RegisterAdditionalTypes", all), typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            Assert.IsTrue(Attribute.IsDefined(registry.GetMethod("RegisterGeneratedMethods", all), typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            var attributes = registry.GetCustomAttributesData();
            Assert.AreEqual(1, attributes.Count(item => item.AttributeType.FullName == "UnityEngine.Scripting.PreserveAttribute"));
            Assert.AreEqual(1, attributes.Count(item => item.AttributeType.FullName == "Unity.Burst.BurstCompileAttribute"));
            var order = attributes.Single(item => item.AttributeType.FullName == "UnityEngine.DefaultExecutionOrder");
            Assert.AreEqual(-100000, order.ConstructorArguments.Single().Value);
            Assert.IsFalse(registry.GetMethod("Load").GetCustomAttributesData()
                .Any(item => item.AttributeType.FullName == "UnityEngine.RuntimeInitializeOnLoadMethodAttribute"));
            var publish = registry.GetMethod("PublishBootstrapPlan", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(publish);
            if (profile == "Runtime") {
                var startup = publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>();
                Assert.IsNotNull(startup);
                Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, startup.loadType);
            } else Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
            Assert.IsTrue(debug.IsPublic, "The DebugJobs owner must remain available even without collection-check defines.");
        }

        private static Type ExportContract => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorExportContract", true);

        private static MethodInfo[] CalledMethods(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void GeneratedEntryPointsDelegateLifecycleToRuntime(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var ns = profile == "Editor" ? "ME.BECS.Editor" : "ME.BECS";
            var initializer = assembly.GetType(ns + ".StaticTypesInitializer", true);
            var registry = assembly.GetType(ns + ".StaticMethods", true);
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InitializeTypes") }, CalledMethods(initializer.GetMethod("Load")));
            var publish = registry.GetMethod("PublishBootstrapPlan", BindingFlags.NonPublic | BindingFlags.Static);
            CollectionAssert.AreEqual(new[] { publish, typeof(BootstrapRuntime).GetMethod("LoadInstalled") }, CalledMethods(registry.GetMethod("Load")));
            CollectionAssert.AreEqual(new[] {
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapSystemSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapTypeInputs", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapEntitySelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapAspectSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapDestroySelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapConfigSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapNetworkSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapViewsSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapJobInitSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapJobSetupSelection", true).GetMethod("Publish"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapJobDebugSelection", true).GetMethod("Publish"),
                }.Concat(profile == "Runtime" ? new[] { assembly.GetType("ME.BECS.SourceGenerated.BootstrapGraphSelection", true).GetMethod("Publish") } : Array.Empty<MethodInfo>())
                .Concat(new[] { typeof(BootstrapRuntime).GetMethod("InstallPlanWithPreflight") }).ToArray(), CalledMethods(publish),
                "Publishing must not execute registrations or reset any runtime state.");
            var plan = initializer.GetMethod("RegisterTypePlan", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(plan);
            Assert.IsTrue(Attribute.IsDefined(plan, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            CollectionAssert.AreEqual(new[] {
                assembly.GetType("ME.BECS.SourceGenerated.CoreTypeInputs", true).GetMethod("Initialize"),
                initializer.GetMethod("RegisterAdditionalTypes", BindingFlags.NonPublic | BindingFlags.Static),
            }, CalledMethods(plan), "Global registration and feeder order must not change when the lifecycle owner moves.");
            var targets = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(publish)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Ldftn)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(new[] { initializer.GetMethod("Load"),
                registry.GetMethod("RegisterGeneratedMethods", BindingFlags.NonPublic | BindingFlags.Static),
                registry.GetMethod("ValidateGeneratedInputs", BindingFlags.NonPublic | BindingFlags.Static) }, targets);
            CollectionAssert.AreEqual(new[] {
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapNetworkSelection", true).GetMethod("Validate"),
                assembly.GetType("ME.BECS.SourceGenerated.BootstrapViewsSelection", true).GetMethod("Validate"),
            }, CalledMethods(registry.GetMethod("ValidateGeneratedInputs", BindingFlags.NonPublic | BindingFlags.Static)));
        }

        [Test]
        public void BootstrapRuntimePreservesInitializationPhaseOrder() {
            Assert.AreEqual(typeof(World).Assembly, typeof(BootstrapRuntime).Assembly);
            // Inspect effects without executing bootstrap or resetting any live world.
            var calls = CalledMethods(typeof(BootstrapRuntime).GetMethod("Load"))
                .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(Logger.Core))
                .Select(method => method.DeclaringType.Name + "." + method.Name).ToArray();
            CollectionAssert.AreEqual(new[] {
                "LeakDetector.ClearAllocated", "StaticTypes.Dispose", "Worlds.Initialize", "SystemsStatic.Initialize",
                "Action.Invoke", "GlobalEvents.Initialize", "CustomModules.InvokeResetPass", "Action.Invoke",
                "EntityConfigRegistry.Initialize", "EntityConfigsRegistry.Initialize", "CustomModules.InvokeFirstPass", "CustomModules.InvokeSecondPass",
            }, calls);
            var typeCalls = CalledMethods(typeof(BootstrapRuntime).GetMethod("InitializeTypes"))
                .Where(method => !method.IsSpecialName)
                .Select(method => method.DeclaringType.Name + "." + method.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "JobUtils.Initialize", "Action.Invoke", "LocksCache.Initialize", "LocksCache.Initialize" }, typeCalls);
        }

        [Test]
        public void BootstrapRejectsMissingPlanBeforeInvokingCallbacks() {
            var calls = 0;
            Action callback = () => ++calls;
            Assert.Throws<ArgumentNullException>(() => BootstrapRuntime.InitializeTypes(null));
            Assert.Throws<ArgumentNullException>(() => BootstrapRuntime.Load(null, callback, true));
            Assert.Throws<ArgumentNullException>(() => BootstrapRuntime.Load(callback, null, false));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void InputExporterDoesNotRetainLegacyNoOpEventHandlers() {
            var assembly = ExportContract.Assembly;
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.CodeGeneratorImporter"));
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.CodeGenerator", true)
                .GetMethod("OnLogAdded", BindingFlags.Static | BindingFlags.NonPublic));
        }

        [Test]
        public void JobBackendsHaveNoEditorFileWriter() {
            var assembly = ExportContract.Assembly;
            const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var exporter = assembly.GetType("ME.BECS.Editor.CodeGenerator", true);
            Assert.IsNull(exporter.GetMethod("GenerateComponentsParallelFor", all));
            Assert.IsNull(exporter.GetNestedType("VariantInfo", BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.MainMenu", true).GetMethod("CodeGenInternalGenerateJobs", all));
        }

        [Test]
        public void LegacyScriptIndexIsReadOnlyAndHasNoImportHook() {
            var assembly = ExportContract.Assembly;
            var importer = assembly.GetType("ME.BECS.Editor.ScriptsImporter", true);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            Assert.AreEqual(typeof(object), importer.BaseType, "The old script index must no longer be an AssetPostprocessor.");
            foreach (var name in new[] { "OnPostprocessAllAssets", "SaveData", "AddCache", "LoadCache", "GetClassNames" })
                Assert.IsNull(importer.GetMethod(name, all), name);
            Assert.IsNotNull(importer.GetMethod("FindScript", all), "Keep the deferred CopyFrom investigation independent of this retirement.");
            var menu = assembly.GetType("ME.BECS.Editor.CodeGeneratorMenu", true);
            Assert.IsNull(menu.GetMethod("RunImportCache", all));
            Assert.IsNull(menu.GetMethod("RunAndClean", all));
            Assert.IsNotNull(menu.GetMethod("Run", all), "Keep an explicit retry until graph refresh is verified in Unity.");
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.CodeGenerator", true).GetMethod("CleanCache", all));
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.CodeGenerator", true).GetMethod("Destroy", all));
        }

        [Test]
        public void GraphInitializersRetainOneBurstAttributeWithoutLegacyPhaseFiles() {
            var assembly = Assembly.Load("ME.BECS.Gen.Runtime");
            var graphs = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row.Length == 6 && row[0] == "runtime" && row[1] == "graph-registration");
            foreach (var row in graphs) {
                var prefix = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3]));
                var owner = Tests_SourceGeneratorGraphPublications.Owner(row[4]).GetType(prefix + "Initialize", true);
                Assert.AreEqual(1, owner.GetCustomAttributesData().Count(attribute =>
                    attribute.AttributeType.FullName == "Unity.Burst.BurstCompileAttribute"), prefix);
            }
        }

        [Test]
        public void RetirementContentContainsNoExecutableCode() {
            var source = (string)ExportContract.GetField("RetirementComment", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            Assert.IsTrue(source.Trim().StartsWith("//", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("class "));
            Assert.IsFalse(source.Contains("namespace "));
            Assert.IsFalse(source.Contains("SourceBootstrapPlanV1"));
        }

        [TestCase("ThemesCodeGenerator")]
        [TestCase("ComponentDestroyCodeGenerator")]
        [TestCase("EntityTypeCodeGenerator")]
        [TestCase("Aspects.AspectsCodeGenerator")]
        [TestCase("Aspects.EntityConfigCodeGenerator")]
        [TestCase("Aspects.CopyFromCodeGenerator")]
        [TestCase("Jobs.JobsEarlyInitCodeGenerator")]
        [TestCase("Systems.SystemDependenciesCodeGenerator")]
        public void BuiltInFeedersDoNotOverrideLegacyCodeHooks(string name) {
            var type = ExportContract.Assembly.GetType("ME.BECS.Editor." + name, true);
            ExportContract.GetMethod("ValidateType", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { type });
        }

        [Test]
        public void DebugWrapperEmissionExistsOnlyInSourceGenerator() {
            var type = ExportContract.Assembly.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Assert.IsNull(type.GetMethod("GenerateLegacyDebugFiles", methods));
            Assert.IsNull(type.GetMethod("AddJobs", methods));
            Assert.IsNull(type.GetNestedType("Item", BindingFlags.Public | BindingFlags.NonPublic));
            var feeder = Activator.CreateInstance(type);
            CollectionAssert.AreEquivalent(new[] { "Debug.Cache", "Debug.Func", "Debug.Struct", "Debug.UnsafeStruct" },
                (System.Collections.IEnumerable)type.GetMethod("GetRetiredSourceFiles").Invoke(feeder, null),
                "Keep retirement names until old projects have upgraded; removing the writer must not restore old emitted files.");
        }

        [TestCase("Debug.Cache", true)]
        [TestCase("GraphLevel.Initialize", true)]
        [TestCase("../Other", false)]
        [TestCase("a/b", false)]
        [TestCase("a\\b", false)]
        [TestCase("Bad.cs", false)]
        [TestCase("Bad.CS", false)]
        [TestCase("Bad.", false)]
        [TestCase("Bad\nName", false)]
        [TestCase(" ", false)]
        [TestCase(null, false)]
        public void RetirementFileNamesCannotEscapeOutputDirectory(string name, bool expected) {
            Assert.AreEqual(expected, ExportContract.GetMethod("IsValidFileName", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { name }));
        }
    }
}
