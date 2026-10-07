using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ME.BECS.Tests {
    // Analysis only: no world/bootstrap, asset publication or callback execution.
    public partial class Tests_ILAnalysisSnapshot {
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type EditorType(string name) => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor." + name, true);
        private static Type Discovery => EditorType("Systems.SystemDependenciesCodeGenerator");

        public interface ISnapshotComponent { }
        public struct SnapshotComponent : IComponent, ISnapshotComponent { public int value; }
        public struct OtherSnapshotComponent : IComponent, ISnapshotComponent { public int value; }

        public partial struct SnapshotSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { default(Ent).Get<SnapshotComponent>(); }
        }

        [SystemGenericParallelMode]
        public partial struct GenericSnapshotSystem<T> : IUpdate where T : unmanaged, ISnapshotComponent, IComponent {
            public void OnUpdate(ref SystemContext context) { default(Ent).Get<T>(); }
        }

        private static object Input(Type[] includes = null, Type[] systems = null, Type[] components = null) =>
            Activator.CreateInstance(Discovery.GetNestedType("RuntimeDiscoveryInputs", BindingFlags.NonPublic),
                HiddenInstance, null, new object[] { includes ?? Type.EmptyTypes, Type.EmptyTypes,
                    systems ?? Type.EmptyTypes, components ?? Type.EmptyTypes, Type.EmptyTypes }, null);

        private static object Analyze(object input) => Discovery.GetMethod("AnalyzeRuntimeDiscovery", HiddenStatic)
            .Invoke(null, new[] { input, (object)false });

        private static Type[] Types(object result, string field) =>
            ((System.Collections.Generic.IEnumerable<Type>)result.GetType().GetField(field).GetValue(result)).ToArray();

        private static string[] Records(object result) => new[] { "systems", "components", "componentsGroup", "jobTypes", "entityTypes", "aspects" }
            .SelectMany(field => Types(result, field).Select((type, index) => field + ":" + index + ":" + type.AssemblyQualifiedName)).ToArray();

        [Test]
        public void DiscoverySnapshotCopiesInputsAndKeepsOnlyTypes() {
            var systems = new[] { typeof(SnapshotSystem) };
            var components = new[] { typeof(OtherSnapshotComponent) };
            var input = Input(systems: systems, components: components);
            systems[0] = null;
            components[0] = null;
            Assert.IsTrue(input.GetType().GetFields(HiddenInstance).All(field => field.FieldType == typeof(Type[])),
                "Workers must not retain live graphs, nodes, config values or Unity objects.");
            var result = Analyze(input);
            CollectionAssert.AreEqual(new[] { typeof(SnapshotSystem) }, Types(result, "systems"));
            CollectionAssert.AreEquivalent(new[] { typeof(SnapshotComponent), typeof(OtherSnapshotComponent) }, Types(result, "components"));
            CollectionAssert.AreEqual(Records(result), Records(Analyze(input)), "Each analysis owns its mutable sets.");
        }

        [UnityTest]
        public IEnumerator RuntimeDiscoveryHasTheSameOrderedResultOnAWorkerIncludingGenericSystems() {
            var input = Input(systems: new[] { typeof(SnapshotSystem), typeof(GenericSnapshotSystem<SnapshotComponent>) });
            var expected = Analyze(input);
            CollectionAssert.Contains(Types(expected, "components"), typeof(OtherSnapshotComponent),
                "A closed graph occurrence must retain the existing generic-definition expansion policy.");
            CollectionAssert.Contains(Types(expected, "systems"), typeof(GenericSnapshotSystem<>));
            var task = System.Threading.Tasks.Task.Run(() => Analyze(input));
            while (!task.IsCompleted) yield return null;
            CollectionAssert.AreEqual(Records(expected), Records(task.GetAwaiter().GetResult()));
        }

        private static object Environment(string target) {
            var type = EditorType("ILAnalysisEnvironment");
            var scripts = Array.CreateInstance(type.GetNestedType("Script", BindingFlags.NonPublic), 0);
            // No cache operations are requested in this test; construction and
            // context hashing must not consult Application/CompilationPipeline.
            return Activator.CreateInstance(type, HiddenInstance, null,
                new object[] { "unused-analysis-snapshot-cache", target, scripts, Array.Empty<Assembly>() }, null);
        }

        private static string Context(object environment) {
            var type = EditorType("ILPersistentAnalysis");
            using var cache = (IDisposable)Activator.CreateInstance(type, HiddenInstance, null, new[] { (object)true, environment }, null);
            return (string)type.GetMethod("Context", HiddenInstance).Invoke(cache, null);
        }

        [UnityTest]
        public IEnumerator PersistentContextUsesCapturedEnvironmentOnTheWorker() {
            var captured = Environment("captured-target");
            var expected = Context(captured);
            Assert.AreNotEqual(expected, Context(Environment("different-target")));
            var task = System.Threading.Tasks.Task.Run(() => Context(captured));
            while (!task.IsCompleted) yield return null;
            Assert.AreEqual(expected, task.GetAwaiter().GetResult());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PersistentMergeKeepsCoordinatorResultWhenWorkerChangesSameKey(bool childRemoves) {
            var type = EditorType("ILPersistentAnalysis");
            var environment = Environment("merge-fixture");
            IDisposable Scope() {
                var scope = (IDisposable)Activator.CreateInstance(type, HiddenInstance, null, new[] { (object)true, environment }, null);
                // Synthetic cache records only: never load/save a project cache.
                type.GetField("persist", HiddenInstance).SetValue(scope, false);
                return scope;
            }
            System.Collections.IDictionary Records(object scope) =>
                (System.Collections.IDictionary)type.GetField("records", HiddenInstance).GetValue(scope);
            void Changed(object scope) => ((System.Collections.Generic.HashSet<string>)type.GetField("changedRecords", HiddenInstance)
                .GetValue(scope)).Add("same-key");
            object Record() => Activator.CreateInstance(type.GetNestedType("Record", BindingFlags.NonPublic), true);
            using var coordinator = Scope();
            var expected = Record();
            Records(coordinator).Add("same-key", expected);
            Changed(coordinator);
            object snapshot;
            using (var worker = Scope()) {
                if (!childRemoves) Records(worker).Add("same-key", Record());
                Changed(worker);
                snapshot = type.GetMethod("CaptureSnapshot", HiddenInstance).Invoke(worker, null);
            }
            type.GetMethod("MergeWorker", HiddenInstance).Invoke(coordinator, new[] { snapshot });
            Assert.AreSame(expected, Records(coordinator)["same-key"],
                "Persistent and memo merges must both prefer the coordinator's computed result.");
        }
    }
}
