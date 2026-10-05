using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace ME.BECS.Tests {
    // Test-assembly-only entry point. Never builds a Player, exports inputs, or
    // runs on reload; use the normal Test Runner API and its asynchronous lifecycle.
    internal static class SourceGeneratorMigrationTests {
        private const string Menu = "ME.BECS/Source Generator/Run Migration Smoke Tests (EditMode)";
        private static TestRunnerApi runner;
        private static readonly Results callbacks = new Results();

        [MenuItem(Menu, true)]
        private static bool CanRun() => runner == null && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Menu)]
        private static void Run() {
            if (!CanRun()) return;
            var fixtures = new[] {
                typeof(Tests_SourceGeneratorRegistrationOwners), typeof(Tests_SourceGeneratorSystemPublications),
                typeof(Tests_SourceGeneratorTypePublications),
                typeof(Tests_SourceGeneratorEntityPublications),
                typeof(Tests_SourceGeneratorJobInitPublications),
                typeof(Tests_SourceGeneratorJobSetupPublications),
                typeof(Tests_SourceGeneratorJobDebugPublications),
                typeof(Tests_SourceGeneratorGraphPublications),
                typeof(Tests_SourceGeneratorPublicationBridges),
                typeof(Tests_SourceGeneratorAspectPublications),
                typeof(Tests_SourceGeneratorDestroyPublications), typeof(Tests_Components_Destroy),
                typeof(Tests_SourceGeneratorConfigPublications), typeof(Tests_EntityConfig),
                typeof(Tests_SourceGeneratorGraphRefresh), typeof(Tests_SourceGeneratorAnalysisReceipt),
                typeof(Tests_ILIncrementalAnalysis),
                typeof(Tests_SourceGeneratorBootstrapRegistry), typeof(Tests_SourceGeneratorBootstrapTypePlan),
                typeof(Tests_SourceGeneratorBootstrapOwnership), typeof(Tests_SourceGeneratorComponentOwnership),
                typeof(Tests_SourceGeneratorSystemOwnership), typeof(Tests_SourceGeneratorInputTransport), typeof(Tests_JobEntityLimits),
            };
            // Addons stay optional: no core Tests -> Network/Views reference.
            foreach (var name in new[] { "ME.BECS.Network.Tests.Tests_SourceGeneratorNetworkMethods",
                         "ME.BECS.Views.Tests.Tests_Views_SourceRegistration", "ME.BECS.Views.Tests.Tests_Views_Publications" }) {
                var fixture = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                    .Select(assembly => assembly.GetType(name, false)).SingleOrDefault(type => type != null);
                if (fixture != null) fixtures = fixtures.Concat(new[] { fixture }).ToArray();
            }
            runner = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(callbacks);
            try {
                runner.Execute(new ExecutionSettings(new Filter {
                    testMode = TestMode.EditMode,
                    assemblyNames = fixtures.Select(type => type.Assembly.GetName().Name).Distinct().ToArray(),
                    groupNames = fixtures.Select(type => "^" + Regex.Escape(type.FullName) + @"(?:\.|$)").ToArray(),
                }));
            } catch { Cleanup(); throw; }
        }

        private static void Cleanup() {
            if (runner == null) return;
            runner.UnregisterCallbacks(callbacks);
            UnityEngine.Object.DestroyImmediate(runner);
            runner = null;
        }

        private sealed class Results : ICallbacks {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result) {
                try {
                    var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ME.BECS.SourceGenerator/MigrationSmoke.Tests.xml"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    TestRunnerApi.SaveResultToFile(result, path);
                    UnityEngine.Debug.Log("[ME.BECS] Migration smoke tests: passed=" + result.PassCount + ", failed=" + result.FailCount +
                        ", skipped=" + result.SkipCount + ". Results: " + path);
                } finally { EditorApplication.delayCall += Cleanup; }
            }
        }
    }
}
