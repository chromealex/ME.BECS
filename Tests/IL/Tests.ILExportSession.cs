using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    // Metadata-only regressions. No bootstrap/world or callback execution.
    public class Tests_ILExportSession {
        private static Type EditorType(string name) => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor." + name, true);
        private static IDisposable Session() => (IDisposable)Activator.CreateInstance(EditorType("ILAnalysisSession"), true);
        private static Array Instructions(MethodBase method) => (Array)EditorType("ILAnalysisSession")
            .GetMethod("Instructions", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { method });
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
