using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorGraphRefresh {
        private static Type Snapshot => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorGraphSnapshot", true);

        [Test]
        public void LazyLibraryLoadingDoesNotChangeTheScriptInputInventory() {
            var identity = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorCodeIdentity", true);
            var select = identity.GetMethod("LoadedScripts", BindingFlags.Static | BindingFlags.NonPublic);
            var owner = typeof(Tests_SourceGeneratorGraphRefresh).Assembly;
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { owner.GetName().Name, "ME.BECS.Gen.Editor" };
            var before = new[] { owner };
            var after = new[] { typeof(object).Assembly, owner, Assembly.Load("ME.BECS.Gen.Editor") };
            CollectionAssert.AreEqual(before, (Assembly[])select.Invoke(null, new object[] { before, names }));
            CollectionAssert.AreEqual(before, (Assembly[])select.Invoke(null, new object[] { after, names }),
                "Loading a library or rebuilding a consumer must not add script inputs.");
        }

        [Test]
        public void LibraryInputsIncludeUnloadedReferencesButNotScriptsOrGeneratedConsumers() {
            var identity = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorCodeIdentity", true);
            var select = identity.GetMethod("LibraryPaths", BindingFlags.Static | BindingFlags.NonPublic);
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { "Gameplay" };
            var paths = new[] { "Lib/System.Buffers.dll", "Library/Gameplay.dll", "Lib/Core.dll",
                "Library/ME.BECS.Gen.Runtime.dll", "Library/ME.BECS.SourceInputs.Bridge.Editor_example.dll", "Lib/System.Buffers.dll" };
            foreach (var input in new[] { paths, paths.Reverse().ToArray() })
                CollectionAssert.AreEqual(new[] { "Lib/Core.dll", "Lib/System.Buffers.dll" },
                    (string[])select.Invoke(null, new object[] { input, names }));
        }

        [Test]
        public void ConsumerRecompileBoundaryIncludesTransitiveDependantsNotTheirDependencies() {
            var type = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorCodeIdentity", true);
            var method = type.GetMethod("FindConsumerDependants", BindingFlags.Static | BindingFlags.NonPublic);
            var graph = new[] {
                new KeyValuePair<string, string[]>("EditorExtension", new[] { "Assembly-CSharp-Editor", "Framework" }),
                new KeyValuePair<string, string[]>("Assembly-CSharp-Editor", new[] { "Assembly-CSharp" }),
                new KeyValuePair<string, string[]>("Assembly-CSharp", new[] { "ME.BECS.Gen.Runtime", "Gameplay" }),
                new KeyValuePair<string, string[]>("ME.BECS.Gen.Runtime", new[] { "Gameplay", "Framework" }),
                new KeyValuePair<string, string[]>("ME.BECS.SourceInputs.Bridge.Editor_example", new[] { "Gameplay", "Framework" }),
                new KeyValuePair<string, string[]>("Gameplay", new[] { "Framework" }),
                new KeyValuePair<string, string[]>("Framework", Array.Empty<string>()),
                new KeyValuePair<string, string[]>("UnrelatedCycleA", new[] { "UnrelatedCycleB" }),
                new KeyValuePair<string, string[]>("UnrelatedCycleB", new[] { "UnrelatedCycleA" }),
            };
            var expected = new[] { "Assembly-CSharp", "Assembly-CSharp-Editor", "EditorExtension" };
            foreach (var input in new[] { graph, graph.Reverse().ToArray(), graph.Where(item => !item.Key.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)).ToArray() })
                CollectionAssert.AreEquivalent(expected, (System.Collections.IEnumerable)method.Invoke(null, new object[] { input }));
        }

        [Test]
        public void AutomaticExportDoesNotRetryUnchangedFailedOrPendingInputs() {
            var method = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputRefresh", true)
                .GetMethod("ShouldExportAutomatically", BindingFlags.Static | BindingFlags.NonPublic);
            bool Retry(string fingerprint, string exported, string attempted, bool failed, bool recovery) =>
                (bool)method.Invoke(null, new object[] { fingerprint, exported, attempted, failed, recovery });
            Assert.IsTrue(Retry("new", "old", "old", false, false));
            Assert.IsFalse(Retry("new", "new", "", false, false));
            Assert.IsFalse(Retry("new", "old", "new", true, false));
            Assert.IsFalse(Retry("new", "new", "new", false, true));
            Assert.IsTrue(Retry("changed", "old", "new", true, true));
            Assert.IsTrue(Retry("new", "old", "", true, true), "Explicit retry clears the attempt stamp.");
        }

        [Test]
        public void StartupReadinessUsesCompiledEvidenceWithoutNeedingAnUpdateOrExport() {
            var method = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputRefresh", true)
                .GetMethod("CheckReady", BindingFlags.Static | BindingFlags.NonPublic);
            var reads = 0;
            var current = true;
            Func<(bool, string)> read = () => { ++reads; return (current, current ? "" : "stale compiled snapshot"); };
            bool Ready(bool running, bool failed, out string reason) {
                var args = new object[] { running, failed, read, null };
                var ready = (bool)method.Invoke(null, args);
                reason = (string)args[3];
                return ready;
            }
            // No Editor update tick/export callback is required for a batch build
            // whose startup freshness comparison has merely been queued.
            Assert.IsTrue(Ready(false, false, out _));
            Assert.AreEqual(1, reads);
            foreach (var state in new[] { (true, false), (false, true), (true, true) }) {
                Assert.IsFalse(Ready(state.Item1, state.Item2, out var reason));
                Assert.IsNotEmpty(reason);
            }
            Assert.AreEqual(1, reads, "A failed/active export cannot be accepted even if the old compiled snapshot still matches.");
            current = false;
            Assert.IsFalse(Ready(false, false, out var stale));
            Assert.AreEqual("stale compiled snapshot", stale);
            Assert.AreEqual(2, reads);
        }

        [Test]
        public void InputRefreshNoLongerPublishesDummyCSharpStamps() {
            var type = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputManifest", true);
            Assert.IsNull(type.GetMethod("CompilationStamp", BindingFlags.Static | BindingFlags.NonPublic));
            var transport = Snapshot.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputTransport", true);
            var fileName = transport.GetMethod("FileName", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var editor in new[] { false, true }) {
                var name = (string)fileName.Invoke(null, new object[] { editor });
                StringAssert.EndsWith(".ME.BECS.SourceGenerator.additionalfile", name);
                Assert.AreEqual((editor ? "EditorInputs" : "RuntimeInputs") + ".ME.BECS.SourceGenerator.additionalfile", name);
            }
        }

        [Test]
        public void LegacyGraphEmitterIsRetiredWithoutRemovingPlanInspection() {
            var assembly = Assembly.Load("ME.BECS.Features.Editor");
            var feeder = assembly.GetType("ME.BECS.Editor.Systems.SystemsCodeGenerator", true);
            const BindingFlags all = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in new[] { "AddGraph", "GetSystemGraph" }) Assert.IsNull(feeder.GetMethod(name, all));
            Assert.IsNull(feeder.GetNestedType("CollectedDeps", all));
            Assert.IsNotNull(feeder.GetMethod("GetRetiredSourceFiles", all));
            var inspector = assembly.GetType("ME.BECS.Editor.Systems.SourceGeneratorGraphLifecycleValidation", true);
            Assert.IsNotNull(inspector.GetMethod("Inspect", all));
            Assert.IsNull(inspector.GetMethod("Compare", all));
        }

        [Test]
        public void InputRefreshAndTargetChangesAreOwnedByCoreEditor() {
            var assembly = Snapshot.Assembly;
            var refresh = assembly.GetType("ME.BECS.Editor.SourceGeneratorInputRefresh", true);
            Assert.IsTrue(refresh.IsPublic, "Graph UI and other input producers share the core refresh service.");
            Assert.IsNotNull(refresh.GetMethod("Request", BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(refresh.GetMethod("TryExport", BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(refresh.GetMethod("TryRebuild", BindingFlags.Static | BindingFlags.Public));
            var target = assembly.GetType("ME.BECS.Editor.SourceGeneratorInputTargetChange", true);
            Assert.IsTrue(target.GetInterfaces().Any(type => type.FullName == "UnityEditor.Build.IActiveBuildTargetChanged"));
            foreach (var name in new[] { "SourceGeneratorInputPostprocessor", "SourceGeneratorInputSaveProcessor" })
                Assert.IsNotNull(assembly.GetType("ME.BECS.Editor." + name, true));
        }

        [Test]
        public void AssetSnapshotTracksConfigTypesAndThemesInCanonicalOrder() {
            var combine = Snapshot.GetMethod("CombineAssets", BindingFlags.Static | BindingFlags.NonPublic);
            string Read(string[] types, string themes = "theme-A") => (string)combine.Invoke(null, new object[] { "graph-A", types, themes });
            var baseline = Read(new[] { "C\tA", "A\tB" });
            Assert.AreEqual(baseline, Read(new[] { "A\tB", "C\tA", "C\tA" }));
            Assert.AreNotEqual(baseline, Read(new[] { "C\tA" }));
            Assert.AreNotEqual(baseline, Read(new[] { "C\tA", "A\tC" }));
            Assert.AreNotEqual(baseline, Read(new[] { "C\tA", "C\tB" }));
            Assert.AreNotEqual(baseline, Read(new[] { "C\tA", "A\tB" }, "theme-B"));
        }

        [TestCase("Assets/Configs/Unit.asset", true)]
        [TestCase("Assets/Graphs/Root.asset", true)]
        [TestCase("Assets/Styles/Theme.uss", true)]
        [TestCase("Packages/example/Configs/Unit.asset", true)]
        [TestCase("Assets/MovedFolder", true)]
        [TestCase("Assets/ME.BECS.Gen/Editor/Input.asset", false)]
        [TestCase("Assets\\ME.BECS.Gen\\Input.asset", false)]
        [TestCase("Assets/ME.BECS.Gen", false)]
        [TestCase("Assets/Texture.png", false)]
        [TestCase("Assets/Script.cs", false)]
        [TestCase("Assets/ME.BECS.SourceInputs/EditorInputs.ME.BECS.SourceGenerator.additionalfile", true)]
        [TestCase("Assets/ME.BECS.SourceInputs/TypeFragments/TypeFragment.ME.BECS.SourceGenerator.additionalfile", true)]
        [TestCase("Assets/Unrelated.OtherGenerator.additionalfile", false)]
        [TestCase(null, false)]
        public void AssetRefreshIncludesConfigsAndThemesButExcludesGeneratedAssets(string path, bool expected) {
            Assert.AreEqual(expected, Snapshot.GetMethod("CanAffectAssetInputs").Invoke(null, new object[] { path }));
        }

        [Test]
        public void GraphSnapshotTracksScriptsAndTargetWithoutDependingOnCultureOrOrder() {
            var method = Snapshot.GetMethod("Combine", BindingFlags.Static | BindingFlags.NonPublic);
            string Read(string graphs, string target, string[] scripts) => (string)method.Invoke(null, new object[] { graphs, target, scripts });
            var previous = CultureInfo.CurrentCulture;
            try {
                var baseline = Read("graph-A", "target-A", new[] { "z\tmvid-A", "I\tmvid-B", "İ\tmvid-C" });
                foreach (var name in new[] { "en-US", "ru-RU", "tr-TR" }) {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    Assert.AreEqual(baseline, Read("graph-A", "target-A", new[] { "İ\tmvid-C", "z\tmvid-A", "I\tmvid-B", "I\tmvid-B" }));
                }
                Assert.AreNotEqual(baseline, Read("graph-B", "target-A", new[] { "z\tmvid-A", "I\tmvid-B", "İ\tmvid-C" }));
                Assert.AreNotEqual(baseline, Read("graph-A", "target-B", new[] { "z\tmvid-A", "I\tmvid-B", "İ\tmvid-C" }));
                Assert.AreNotEqual(baseline, Read("graph-A", "target-A", new[] { "z\tmvid-NEW", "I\tmvid-B", "İ\tmvid-C" }));
            } finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void SuccessfulExportAloneCannotProveCompiledGraphInputsCurrent() {
            var method = Snapshot.GetMethod("ValidateCompiled", BindingFlags.Static | BindingFlags.NonPublic);
            var fingerprint = new string('A', 64);
            var runtime = new KeyValuePair<string, string[]>("ME.BECS.Gen.Runtime", new[] { fingerprint });
            var editor = new KeyValuePair<string, string[]>("ME.BECS.Gen.Editor", new[] { fingerprint });
            bool Valid(KeyValuePair<string, string[]>[] snapshots, out string reason) {
                var args = new object[] { fingerprint, snapshots, null };
                var result = (bool)method.Invoke(null, args);
                reason = (string)args[2];
                return result;
            }
            Assert.IsTrue(Valid(new[] { editor, runtime }, out var detail), detail);
            foreach (var incomplete in new[] {
                Array.Empty<KeyValuePair<string, string[]>>(), new[] { runtime }, new[] { editor },
                new[] { runtime, runtime, editor },
                new[] { runtime, new KeyValuePair<string, string[]>(editor.Key, Array.Empty<string>()) },
                new[] { runtime, new KeyValuePair<string, string[]>(editor.Key, new[] { fingerprint, fingerprint }) },
                new[] { runtime, new KeyValuePair<string, string[]>(editor.Key, new[] { new string('B', 64) }) },
                new[] { new KeyValuePair<string, string[]>(runtime.Key, null), editor },
            }) {
                Assert.IsFalse(Valid(incomplete, out detail));
                StringAssert.Contains("Regenerate inputs", detail);
            }
        }

        [TestCase("Runtime")]
        [TestCase("Editor")]
        public void CompiledGraphSnapshotMatchesItsManifestRecord(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            Assert.IsFalse(metadata.Any(item => item.Key == "ME.BECS.InputRecovery.v1"),
                "Recovery is incomplete compilation, never a usable bootstrap alongside a freshness snapshot.");
            var value = metadata.Single(item => item.Key == "ME.BECS.GraphInputSnapshot.v1").Value;
            var record = metadata.Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Single(row => row.Length == 4 && row[0] == profile.ToLowerInvariant() && row[1] == "graph-input-snapshot");
            Assert.AreEqual("0", record[2]);
            Assert.AreEqual(value, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(record[3])));
            Assert.AreEqual(64, value.Length);
            Assert.IsTrue(value.All(character => character >= '0' && character <= '9' || character >= 'A' && character <= 'F'));
        }
    }
}
