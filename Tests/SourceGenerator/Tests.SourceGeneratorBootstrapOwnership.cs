using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorBootstrapOwnership {
        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void BootstrapUsesManifestWithoutExportedMarkers(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var schema = assembly.BecsInputMetadata()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Single(row => row[0] == profile.ToLowerInvariant() && row[1] == "bootstrap-schema");
            Assert.AreEqual("0", schema[2]);
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schema[3])));
            var owner = Tests_SourceGeneratorBootstrapPublications.Owner(assembly);
            var publication = owner.GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true);
            var phases = Tests_SourceGeneratorBootstrapPublications.PhaseInputs(assembly);
            var debug = Tests_SourceGeneratorJobDebugPublications.Owners(profile).Values.Distinct().ToArray();
            Assert.IsNotEmpty(debug);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (var type in new[] { publication, phases }.Concat(debug)) {
                Assert.IsFalse(type.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                foreach (var marker in new[] { "SourceBootstrapPlanV1", "SourceRegistrationPlanV1", "SourceSystemDependenciesV1", "SourceDebugPlanV1",
                    "RegisterAdditionalTypes", "RegisterGeneratedMethods" }) Assert.IsEmpty(type.GetMember(marker, all));
            }
            foreach (var type in new[] { publication, phases }) Assert.IsTrue(Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            var publish = publication.GetMethod("Publish", all);
            Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
            if (profile == "Editor") Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
            else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded,
                publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
            Assert.IsFalse(CalledMethods(publish).Any(method => method.Name == "LoadInstalled" || method.Name == "InitializeInstalledTypes"),
                "Publication must not execute initialization before all fragments are installed.");
            Assert.IsTrue(debug.All(type => type.IsPublic), "Debug owners remain available without collection-check defines.");
        }

        private static Type ExportContract => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorExportContract", true);

        private static MethodInfo[] CalledMethods(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void GeneratedEntryPointsDelegateLifecycleToRuntime(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var runtimeLoad = typeof(BootstrapRuntime).GetMethod("LoadRuntime", BindingFlags.NonPublic | BindingFlags.Static);
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("LoadInstalled") }, CalledMethods(runtimeLoad));
            Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad,
                runtimeLoad.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
            var core = typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapPhases", true);
            var plan = core.GetMethod("RegisterTypes", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(plan);
            CollectionAssert.AreEqual(new[] {
                typeof(BootstrapRuntime).GetMethod("RegisterInstalledTypes"),
                typeof(Action<bool>).GetMethod("Invoke"),
            }, CalledMethods(plan), "Global registration and feeder order must not change when the lifecycle owner moves.");
            var targets = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(runtimeLoad)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Ldftn)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            Assert.IsEmpty(targets, "The runtime loader executes installed data, not generated executable plans.");
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InitializeTypes") },
                CalledMethods(core.GetMethod("InitializeTypes", BindingFlags.NonPublic | BindingFlags.Instance)));
            Tests_SourceGeneratorBootstrapPhases.AssertFeederSequence(assembly);
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
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(false);
            var graphs = assembly.BecsInputMetadata()
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
        public void ExportContractNoLongerProvidesLegacySourceStubs() {
            Assert.IsNull(ExportContract.GetField("RetirementComment", BindingFlags.Static | BindingFlags.NonPublic),
                "Input export must not restore the retired aggregate C# stub mechanism.");
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
