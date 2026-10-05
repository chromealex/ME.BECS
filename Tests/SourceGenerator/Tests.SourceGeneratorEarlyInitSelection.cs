using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static object EarlyInitSourceReader() => Activator.CreateInstance(
            Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorJobEarlyInit", true), true);

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
