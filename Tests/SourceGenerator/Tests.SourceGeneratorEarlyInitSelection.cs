using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static object EarlyInitSourceReader() => Activator.CreateInstance(
            Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorJobEarlyInit", true), true);

        [Test]
        public void CombinedJobInputsPersistAcrossSessionsWithDependencyReplay() {
            const BindingFlags hidden = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var assembly = Assembly.Load("ME.BECS.Editor");
            var session = assembly.GetType("ME.BECS.Editor.ILAnalysisSession", true);
            var persistent = assembly.GetType("ME.BECS.Editor.ILPersistentAnalysis", true);
            var captureType = assembly.GetType("ME.BECS.Editor.ILDependencyCapture", true);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "becs-job-inputs-" + Guid.NewGuid().ToString("N") + ".json");
            var environment = assembly.GetType("ME.BECS.Editor.ILAnalysisEnvironment", true)
                .GetMethod("Capture", hidden).Invoke(null, new object[] { path });
            var get = assembly.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true).GetMethod("GetJobInputs", hidden);
            var wait = persistent.GetMethod("WaitForPendingSave", hidden);
            MethodBase[] dependencies = null;
            try {
                foreach (var warm in new[] { false, true }) {
                    using (var memo = (IDisposable)Activator.CreateInstance(session, true))
                    using (var cache = (IDisposable)Activator.CreateInstance(persistent, instance, null, new[] { (object)false, environment }, null))
                    using (var capture = (IDisposable)Activator.CreateInstance(captureType, true)) {
                        get.Invoke(null, new object[] { typeof(NativeBoolSizeJob) });
                        var statistics = (System.Collections.Generic.Dictionary<string, (int hits, int misses)>)persistent
                            .GetField("statistics", instance).GetValue(cache);
                        Assert.AreEqual(warm ? (1, 0) : (0, 1), statistics["job-inputs"]);
                        var observed = (MethodBase[])captureType.GetProperty("Methods", instance).GetValue(capture);
                        if (warm == true) {
                            CollectionAssert.AreEquivalent(dependencies, observed, "A persisted hit must replay the nested analyzers' dependencies.");
                            Assert.AreEqual(1, statistics.Count, "A warm combined hit must not reload the individual analyzer records.");
                        } else {
                            Assert.IsNotEmpty(observed);
                            dependencies = observed;
                        }
                    }
                    wait.Invoke(null, null);
                    Assert.IsTrue(System.IO.File.Exists(path));
                }
            } finally {
                wait.Invoke(null, null);
                if (System.IO.File.Exists(path) == true) { System.IO.File.Delete(path); }
            }
        }

        [TestCase(typeof(DebugNoArgumentsJob))]
        [TestCase(typeof(NativeBoolSizeJob))]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob))]
        public void CombinedJobInputsMatchIndependentAnalyzers(Type job) {
            const BindingFlags hidden = BindingFlags.Static | BindingFlags.NonPublic;
            var assembly = Assembly.Load("ME.BECS.Editor");
            var sessionType = assembly.GetType("ME.BECS.Editor.ILAnalysisSession", true);
            var generator = assembly.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            object safety, weight;
            bool covered;
            var entityArgs = new object[] { job, null, null };
            using ((IDisposable)Activator.CreateInstance(sessionType, true)) {
                safety = generator.GetMethod("GetJobTypesInfo").Invoke(null, new object[] { job, null });
                covered = (bool)assembly.GetType("ME.BECS.Editor.Jobs.ILJobEntityCounts", true)
                    .GetMethod("TryGetExportPayload", hidden).Invoke(null, entityArgs);
                weight = assembly.GetType("ME.BECS.Editor.Jobs.ILJobWeights", true)
                    .GetMethod("Analyze", hidden).Invoke(null, new object[] { job, null });
            }
            using ((IDisposable)Activator.CreateInstance(sessionType, true)) {
                var get = generator.GetMethod("GetJobInputs", hidden);
                var actual = get.Invoke(null, new object[] { job });
                object Field(string name) => actual.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actual);
                CollectionAssert.AreEquivalent((System.Collections.IEnumerable)safety, (System.Collections.IEnumerable)Field("safety"));
                Assert.AreEqual(covered, Field("entityCovered"));
                Assert.AreEqual(entityArgs[1], Field("entityPayload"));
                Assert.AreEqual(weight, Field("weight"));
                Assert.AreSame(actual, get.Invoke(null, new object[] { job }), "All export consumers must share one job snapshot.");
            }
        }

        [Test]
        public void EarlyInitSelectionMemoSurvivesPublicationHandoffAndKeepsDuplicates() {
            var assembly = Assembly.Load("ME.BECS.Editor");
            var sessionType = assembly.GetType("ME.BECS.Editor.ILAnalysisSession", true);
            var feederType = assembly.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var select = feederType.GetMethod("SelectEarlyInitJobs", BindingFlags.Instance | BindingFlags.NonPublic);
            var reads = 0;
            Func<Func<object>, object> metadata = read => { ++reads; return read(); };
            var worker = (IDisposable)Activator.CreateInstance(sessionType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { System.Threading.CancellationToken.None, null, metadata }, null);
            System.Collections.Generic.List<Type> Select(System.Collections.Generic.List<Type> jobs, object feeder = null) {
                feeder ??= Activator.CreateInstance(feederType);
                feederType.GetField("jobTypes").SetValue(feeder, jobs);
                return (System.Collections.Generic.List<Type>)select.Invoke(feeder, new object[] { typeof(ME.BECS.Jobs.IJobForComponents) });
            }
            object snapshot;
            var job = typeof(DebugNoArgumentsJob);
            try {
                var first = Select(new System.Collections.Generic.List<Type> { job, job });
                CollectionAssert.AreEqual(new[] { job, job }, first);
                first.Clear();
                CollectionAssert.AreEqual(new[] { job, job }, Select(new System.Collections.Generic.List<Type> { job, job }));
                Assert.AreEqual(1, reads, "A different exporter and list must reuse the same selection.");
                snapshot = sessionType.GetMethod("Detach", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(worker, null);
            } finally { worker.Dispose(); }

            using var publication = (IDisposable)Activator.CreateInstance(sessionType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { "early-init-selection-test", false, snapshot }, null);
            var values = (System.Collections.IDictionary)sessionType.GetField("values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(publication);
            var count = values.Count;
            CollectionAssert.AreEqual(new[] { job, job }, Select(new System.Collections.Generic.List<Type> { job, job }));
            Assert.AreEqual(count, values.Count, "Publication must find the transferred selection entry.");
            var mutable = new System.Collections.Generic.List<Type> { job, job };
            var feederInstance = Activator.CreateInstance(feederType);
            Select(mutable, feederInstance);
            mutable.RemoveAt(0);
            CollectionAssert.AreEqual(new[] { job }, Select(mutable, feederInstance), "In-place list changes must change the cache key.");
            Assert.Greater(values.Count, count);
        }

        private static bool ReadEarlyInitMethods(object reader, Type job,
            out System.Collections.Generic.KeyValuePair<int, MethodInfo>[] methods, out string reason) {
            var args = new object[] { job, null, null };
            var result = (bool)reader.GetType().GetMethod("TryGetMethods", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, args);
            methods = args[1] as System.Collections.Generic.KeyValuePair<int, MethodInfo>[];
            reason = args[2] as string;
            return result;
        }

        [TestCase(typeof(NativeBoolSizeJob), 2)]
        [TestCase(typeof(DebugNoArgumentsJob), 0)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob), 2)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.ReadOnlySafetyJob), 2)]
        public void SourceEarlyInitSelectsClosedMethodsWithoutCallSpelling(Type job, int phase) {
            Assert.IsTrue(ReadEarlyInitMethods(EarlyInitSourceReader(), job, out var methods, out var reason), reason);
            Assert.AreEqual(1, methods.Length);
            Assert.AreEqual(phase, methods[0].Key);
            var method = methods[0].Value;
            Assert.IsTrue(method.IsPublic && method.IsStatic);
            Assert.IsFalse(method.ContainsGenericParameters);
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.IsEmpty(method.GetParameters());
            Assert.AreEqual(job.Assembly, method.DeclaringType.Assembly);
            CollectionAssert.AreEqual(job.IsGenericType ? job.GetGenericArguments() : Type.EmptyTypes, method.GetGenericArguments());
            // Never execute this method: it initializes job shared statics.
        }

        [Test]
        public void SourceEarlyInitReturnsDefensiveSnapshots() {
            var reader = EarlyInitSourceReader();
            Assert.IsTrue(ReadEarlyInitMethods(reader, typeof(NativeBoolSizeJob), out var first, out var firstReason), firstReason);
            var expected = first[0];
            first[0] = default;
            Assert.IsTrue(ReadEarlyInitMethods(reader, typeof(NativeBoolSizeJob), out var second, out var secondReason), secondReason);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(expected, second[0]);
        }

        [TestCase(typeof(GenericAotSystem<>.UnannotatedSafetyJob))]
        [TestCase(typeof(DeferredAccessSummaryJob))]
        public void SourceEarlyInitMissingSelectionIsNotEmptySuccess(Type job) {
            Assert.IsFalse(ReadEarlyInitMethods(EarlyInitSourceReader(), job, out var methods, out var reason));
            Assert.IsNull(methods);
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void EarlyInitExpansionPreservesAlreadyClosedGenericJobs() {
            var job = typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob);
            var jobs = new System.Collections.Generic.List<Type> { job, job };
            PatchEarlyInitJobs(jobs);
            CollectionAssert.AreEqual(new[] { job, job }, jobs);
        }

        [Test]
        public void EarlyInitExpansionDoesNotUseOpenGenericComponents() {
            var jobs = new System.Collections.Generic.List<Type> { typeof(Tests_ILJobEntityCounts.GenericContainer<>.Job) };
            PatchEarlyInitJobs(jobs);
            Assert.IsNotEmpty(jobs);
            Assert.IsFalse(jobs.Any(job => job.ContainsGenericParameters), "Open components cannot produce concrete EarlyInit registrations.");
            CollectionAssert.Contains(jobs, typeof(Tests_ILJobEntityCounts.GenericContainer<TestComponent>.Job));
        }

        private static void PatchEarlyInitJobs(System.Collections.Generic.List<Type> jobs) =>
            Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.CodeGenerator", true)
                .GetMethod("PatchSystemsList", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { jobs });

        [Test]
        public void SourceEarlyInitKeepsDiagnosticOrderAndStatOnlySlots() {
            var assembly = Assembly.Load("ME.BECS.Editor");
            var type = assembly.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var generator = Activator.CreateInstance(type);
            var jobs = new System.Collections.Generic.List<Type> {
                typeof(NativeBoolSizeJob), typeof(DebugNoArgumentsJob), typeof(NativeBoolSizeJob),
                typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob),
            };
            type.GetField("jobTypes").SetValue(generator, jobs);
            type.GetField("editorAssembly").SetValue(generator, true);
            var assemblies = assembly.GetType("ME.BECS.Editor.EditorUtils", true)
                .GetMethod("GetAssembliesInfo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            type.GetField("asms").SetValue(generator, assemblies);
            var select = type.GetMethod("SelectSourceEarlyInit", BindingFlags.Instance | BindingFlags.NonPublic);
            var actual = (System.Collections.Generic.List<(Type job, MethodInfo method)>)select.Invoke(generator, null);
            var compare = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "CompareEarlyInit" && method.GetParameters().Length == 4);
            var arguments = new object[] { jobs, true, 0, null };
            var report = (string)compare.Invoke(null, arguments);
            Assert.AreEqual(0, arguments[2], report);
            var expected = (System.Collections.Generic.List<(Type job, MethodInfo method)>)arguments[3];
            CollectionAssert.AreEqual(expected, actual, report);
            Assert.AreEqual(2, actual.Count(entry => entry.job == typeof(NativeBoolSizeJob) && entry.method == null));
            Assert.AreEqual(2, actual.Count(entry => entry.job == typeof(NativeBoolSizeJob) && entry.method != null));
        }
    }
}
