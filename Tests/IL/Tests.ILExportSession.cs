using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace ME.BECS.Tests {
    // Metadata-only regressions. No bootstrap/world or callback execution.
    public class Tests_ILExportSession {
        private static Type EditorType(string name) => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor." + name, true);
        private static IDisposable Session() => (IDisposable)Activator.CreateInstance(EditorType("ILAnalysisSession"), true);
        private static Array Instructions(MethodBase method) => (Array)EditorType("ILAnalysisSession")
            .GetMethod("Instructions", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { method });
        private static IDisposable Worker(CancellationToken token) => (IDisposable)Activator.CreateInstance(
            EditorType("ILAnalysisSession"), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { token, (Action<MethodBase>)null }, null);
        private static bool Boundary(string name, MethodBase method) => (bool)EditorType("ILInfrastructure")
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { method });

        private sealed class UserError : System.Exception {
            public UserError() => default(Ent).Get<Test1Component>();
        }
        private static void Body() { default(Ent).Get<TestComponent>(); }
        private static void WithFrameworkMetadata() {
            _ = typeof(Tests_ILExportSession).Assembly.FullName;
            _ = new InvalidOperationException("diagnostic");
            _ = new UserError();
            Body();
        }

        [Test]
        public void ILBodyCacheIsScopedAndNestedScopesRestoreTheOuterSnapshot() {
            var body = typeof(Tests_ILExportSession).GetMethod(nameof(Body), BindingFlags.NonPublic | BindingFlags.Static);
            Array first;
            using (Session()) {
                first = Instructions(body);
                Assert.AreSame(first, Instructions(body));
                using (Session()) Assert.AreNotSame(first, Instructions(body));
                Assert.AreSame(first, Instructions(body));
            }
            using (Session()) Assert.AreNotSame(first, Instructions(body));
        }

        [Test]
        public void ExportILCacheReusesOnlyTheSameCodeSnapshotAndSupportsForcedRebuild() {
            IDisposable Export(string fingerprint, bool rebuild = false) => (IDisposable)Activator.CreateInstance(
                EditorType("ILAnalysisSession"), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { fingerprint, rebuild }, null);
            var body = typeof(Tests_ILExportSession).GetMethod(nameof(Body), BindingFlags.NonPublic | BindingFlags.Static);
            var fingerprint = Guid.NewGuid().ToString("N");
            Array first;
            using (Export(fingerprint)) first = Instructions(body);
            using (Export(fingerprint)) Assert.AreSame(first, Instructions(body));
            using (Session()) Assert.AreNotSame(first, Instructions(body));
            using (Export(fingerprint)) Assert.AreSame(first, Instructions(body));
            using (Export(fingerprint, true)) {
                Assert.AreNotSame(first, Instructions(body));
                first = Instructions(body);
            }
            using (Export(fingerprint + "-changed-dependency")) Assert.AreNotSame(first, Instructions(body));
        }

        [UnityTest]
        public IEnumerator WorkerMemoTransfersOnceWithoutSharingTheRetainedEditorDictionary() {
            var type = EditorType("ILAnalysisSession");
            var body = typeof(Tests_ILExportSession).GetMethod(nameof(Body), BindingFlags.NonPublic | BindingFlags.Static);
            var fingerprint = Guid.NewGuid().ToString("N");
            IDisposable Export() => (IDisposable)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { fingerprint, false }, null);
            Array editorInstructions;
            using (Export()) editorInstructions = Instructions(body);
            var task = System.Threading.Tasks.Task.Run(() => {
                using var scope = Worker(CancellationToken.None);
                var instructions = Instructions(body);
                var snapshot = type.GetMethod("Detach", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scope, null);
                var failure = Assert.Throws<TargetInvocationException>(() => Instructions(body));
                Assert.IsInstanceOf<InvalidOperationException>(failure.InnerException);
                return (snapshot, instructions);
            });
            while (!task.IsCompleted) yield return null;
            var result = task.GetAwaiter().GetResult();
            Assert.AreNotSame(editorInstructions, result.instructions);
            IDisposable Adopt() => (IDisposable)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { (object)fingerprint, false, result.snapshot }, null);
            using (Adopt()) Assert.AreSame(result.instructions, Instructions(body));
            var duplicate = Assert.Throws<TargetInvocationException>(() => Adopt());
            Assert.IsInstanceOf<InvalidOperationException>(duplicate.InnerException);
            using (Export()) Assert.AreSame(editorInstructions, Instructions(body));
        }

        [Test]
        public void WorkerCancellationStopsMemoHitsAndPreventsHandoff() {
            var type = EditorType("ILAnalysisSession");
            var body = typeof(Tests_ILExportSession).GetMethod(nameof(Body), BindingFlags.NonPublic | BindingFlags.Static);
            using var cancellation = new CancellationTokenSource();
            using var scope = Worker(cancellation.Token);
            Instructions(body);
            cancellation.Cancel();
            var read = Assert.Throws<TargetInvocationException>(() => Instructions(body));
            Assert.IsInstanceOf<OperationCanceledException>(read.InnerException);
            var handoff = Assert.Throws<TargetInvocationException>(() =>
                type.GetMethod("Detach", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scope, null));
            Assert.IsInstanceOf<OperationCanceledException>(handoff.InnerException);
        }

        [Test]
        public void CompiledFeederCachePreservesSelectionOrderAndDoesNotCacheAssetFeeders() {
            var feederType = EditorType("Jobs.JobsEarlyInitCodeGenerator");
            var feeder = Activator.CreateInstance(feederType);
            var key = EditorType("SourceGeneratorFeederCache").GetMethod("SelectionKey", BindingFlags.Static | BindingFlags.NonPublic);
            string Read() => (string)key.Invoke(null, new[] { feeder });
            void Jobs(params Type[] jobs) => feederType.GetField("jobTypes").SetValue(feeder, new System.Collections.Generic.List<Type>(jobs));
            Jobs(typeof(Tests_ILJobEntityCounts.Repeated), typeof(Tests_ILJobEntityCounts.MixedLoops));
            var baseline = Read();
            Assert.AreEqual(baseline, Read());
            Jobs(typeof(Tests_ILJobEntityCounts.MixedLoops), typeof(Tests_ILJobEntityCounts.Repeated));
            Assert.AreNotEqual(baseline, Read(), "Registration order is part of the cache key.");
            Jobs(typeof(Tests_ILJobEntityCounts.Repeated), typeof(Tests_ILJobEntityCounts.MixedLoops));
            feederType.GetField("editorAssembly").SetValue(feeder, true);
            Assert.AreNotEqual(baseline, Read(), "Editor and Runtime selections are independent.");
            Assert.IsTrue((bool)feederType.GetProperty("CacheCompiledInputs").GetValue(feeder));
            foreach (var name in new[] { "ThemesCodeGenerator", "Aspects.EntityConfigCodeGenerator" }) {
                var type = EditorType(name);
                Assert.IsFalse((bool)type.GetProperty("CacheCompiledInputs").GetValue(Activator.CreateInstance(type)), name);
            }
        }

        [Test]
        public void CompiledFeederCacheReanalyzesMissingBridgeReceiptsAndReusesIndependentRows() {
            var feederType = EditorType("Jobs.JobsEarlyInitCodeGenerator");
            var feeder = Activator.CreateInstance(feederType);
            foreach (var field in new[] { "systems", "jobTypes", "entityTypes", "aspects" })
                feederType.GetField(field).SetValue(feeder, new System.Collections.Generic.List<Type>());
            var cacheType = EditorType("SourceGeneratorFeederCache");
            var key = (string)cacheType.GetMethod("SelectionKey", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { feeder });
            var names = cacheType.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true);
            string Hash(string value) => (string)names.GetMethod("Hash", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { value });
            var slot = Hash(feederType.AssemblyQualifiedName + "\nFalse");
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "../Library/ME.BECS.SourceGenerator/CompiledInputs", slot + ".json"));
            var previous = System.IO.File.Exists(path) == true ? System.IO.File.ReadAllBytes(path) : null;
            var fingerprint = Guid.NewGuid().ToString("N");
            var recordType = cacheType.GetNestedType("Record", BindingFlags.NonPublic);
            void Write(string text) {
                var record = Activator.CreateInstance(recordType, true);
                recordType.GetField("key").SetValue(record, key);
                recordType.GetField("code").SetValue(record, fingerprint);
                recordType.GetField("text").SetValue(record, text);
                recordType.GetField("references").SetValue(record, Array.Empty<string>());
                var checksum = cacheType.GetMethod("Checksum", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { record });
                recordType.GetField("checksum").SetValue(record, checksum);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(record));
            }
            string Row(string owner) => "system-registration-owner\t0\tdjE=\t" +
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(owner)) + "\n";
            IDisposable Export() => (IDisposable)Activator.CreateInstance(EditorType("ILAnalysisSession"),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { fingerprint, false }, null);
            string Append() {
                var manifest = new System.Text.StringBuilder();
                using (Export()) cacheType.GetMethod("Append", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { feeder, manifest });
                return manifest.ToString();
            }
            try {
                var missingReceipt = Row("ME.BECS.SourceInputs.Bridge.Runtime_" +
                    Guid.NewGuid().ToString("N").ToUpperInvariant() + Guid.NewGuid().ToString("N").ToUpperInvariant());
                Write(missingReceipt);
                Assert.AreNotEqual(missingReceipt, Append(), "A cache entry without its bridge receipt must be reanalyzed.");

                var independent = Row("User.Assembly");
                Write(independent);
                Assert.AreEqual(independent, Append(), "A bridge-independent cache entry should be reused verbatim.");
            } finally {
                if (previous != null) {
                    System.IO.File.WriteAllBytes(path, previous);
                } else if (System.IO.File.Exists(path) == true) {
                    System.IO.File.Delete(path);
                }
            }
        }

        [Test]
        public void ILInfrastructureDoesNotSuppressUserConstructorsOrGenericSpecializations() {
            Assert.IsTrue(Boundary("IsLeaf", typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) })));
            Assert.IsFalse(Boundary("SkipBody", typeof(UserError).GetConstructor(Type.EmptyTypes)));
            Assert.IsTrue(Boundary("IsOpaque", typeof(Type).GetProperty(nameof(Type.Assembly)).GetMethod));
            Assert.IsFalse(Boundary("IsLeaf", typeof(Type).GetProperty(nameof(Type.Assembly)).GetMethod));
            var list = typeof(System.Collections.Generic.List<TestComponent>);
            Assert.IsFalse(Boundary("SkipBody", list.GetMethod("Add")));
            var construction = typeof(Activator).GetMethods().Single(method => method.Name == "CreateInstance" && method.IsGenericMethod);
            Assert.IsFalse(Boundary("SkipBody", construction.MakeGenericMethod(typeof(UserError))));
        }

        [Test]
        public void SafetySnapshotsRetainUserEffectsAndCannotBeClearedByACaller() {
            var root = typeof(Tests_ILExportSession).GetMethod(nameof(WithFrameworkMetadata), BindingFlags.NonPublic | BindingFlags.Static);
            var read = EditorType("Jobs.JobsEarlyInitCodeGenerator").GetMethod("GetMethodTypesInfo");
            using (Session()) {
                var first = read.Invoke(null, new object[] { root, true, false, false, null });
                first.GetType().GetMethod("Clear").Invoke(first, null);
                var second = (IEnumerable)read.Invoke(null, new object[] { root, true, false, false, null });
                var types = second.Cast<object>().Select(item => (Type)item.GetType().GetField("type").GetValue(item)).ToArray();
                CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, types);
            }
        }

        [Test]
        public void SinglePassExportKeepsRepeatedAndClosedGenericEntityCounts() {
            var counts = EditorType("Jobs.ILJobEntityCounts");
            foreach (var job in new[] { typeof(Tests_ILJobEntityCounts.Repeated),
                         typeof(Tests_ILJobEntityCounts.MixedLoops), typeof(Tests_ILJobEntityCounts.GenericContainer<TestComponent>.Job) }) {
                using (Session()) {
                    var strict = new object[] { job, null, null };
                    var export = new object[] { job, null, null };
                    var strictComplete = counts.GetMethod("TryGetPayload", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, strict);
                    var exportComplete = counts.GetMethod("TryGetExportPayload", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, export);
                    Assert.AreEqual(strictComplete, exportComplete, job.FullName);
                    Assert.AreEqual(strict[1], export[1], job.FullName);
                    var known = counts.GetMethod("AnalyzeKnown", BindingFlags.NonPublic | BindingFlags.Static);
                    var first = (IDictionary)known.Invoke(null, new object[] { job, null });
                    Assert.IsNotEmpty(first, job.FullName);
                    first.Clear();
                    Assert.IsNotEmpty((IDictionary)known.Invoke(null, new object[] { job, null }), job.FullName);
                }
            }
        }
    }
}
