using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorBootstrapTypePlan {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Registry => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapTypeRegistry", true);
        private static object NewRegistry() => Activator.CreateInstance(Registry, true);
        private static void Install(object registry, string owner, int count, int[] ordinals, Action[] callbacks, string identity = "selection") =>
            Registry.GetMethod("Install", Hidden).Invoke(registry, new object[] { identity, owner, count, ordinals, callbacks });
        private static void Require(object registry) => Registry.GetMethod("RequireComplete", Hidden).Invoke(registry, null);
        private static void Execute(object registry) => Registry.GetMethod("Execute", Hidden).Invoke(registry, null);
        private static T Failure<T>(TestDelegate action) where T : System.Exception =>
            (T)Assert.Throws<TargetInvocationException>(action).InnerException;

        // Read emitted typed delegate targets without running any registration,
        // Default getter, world initialization, or preservation-only AOT method.
        internal static MethodInfo[] SelectedSystems(Assembly assembly) {
            var profile = assembly.GetName().Name.EndsWith(".Editor", StringComparison.Ordinal) ? "Editor" : "Runtime";
            var owners = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row.Length == 5 && row[0] == profile.ToLowerInvariant() && row[1] == "system-registration-owner")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[4]))).Distinct().ToArray();
            var entries = new System.Collections.Generic.SortedDictionary<int, MethodInfo>();
            foreach (var owner in owners) {
                var publisher = Assembly.Load(owner).GetType("ME.BECS.SourceGenerated.SystemFragment_" + profile, true);
                var ordinals = (int[])publisher.GetField("Ordinals", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var callbacks = (Action[])publisher.GetField("Callbacks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.AreEqual(ordinals.Length, callbacks.Length);
                for (var i = 0; i < ordinals.Length; ++i) entries.Add(ordinals[i], callbacks[i].Method);
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, entries.Count).ToArray(), entries.Keys.ToArray());
            return entries.Values.ToArray();
        }

        internal static MethodInfo[] Selected(Assembly assembly) => SelectedSystems(assembly).Concat(SelectedTypes(assembly)).ToArray();

        internal static MethodInfo[] SelectedTypes(Assembly assembly) {
            var profile = assembly.GetName().Name.EndsWith(".Editor", StringComparison.Ordinal) ? "Editor" : "Runtime";
            var owners = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row.Length == 5 && row[0] == profile.ToLowerInvariant() && row[1] == "type-registration-owner")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[4]))).Distinct().ToArray();
            var entries = new System.Collections.Generic.SortedDictionary<int, MethodInfo>();
            foreach (var owner in owners) {
                var publisher = Assembly.Load(owner).GetType("ME.BECS.SourceGenerated.TypeFragment_" + profile, true);
                var ordinals = (int[])publisher.GetField("Ordinals", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var callbacks = (Action[])publisher.GetField("Callbacks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.AreEqual(ordinals.Length, callbacks.Length);
                for (var i = 0; i < ordinals.Length; ++i) entries.Add(ordinals[i], callbacks[i].Method);
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, entries.Count).ToArray(), entries.Keys.ToArray());
            return entries.Values.ToArray();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InterleavedFragmentsExecuteInGlobalOrderRegardlessOfPublicationOrder(bool reverse) {
            var registry = NewRegistry();
            var calls = new System.Collections.Generic.List<int>();
            Action[] actions = Enumerable.Range(0, 6).Select(index => (Action)(() => calls.Add(index))).ToArray();
            foreach (var owner in reverse ? new[] { 1, 0 } : new[] { 0, 1 }) {
                var ordinals = new[] { owner, owner + 2, owner + 4 };
                Install(registry, "owner" + owner, 6, ordinals, ordinals.Select(index => actions[index]).ToArray());
            }
            Assert.IsEmpty(calls, "Publication must not assign IDs.");
            Require(registry);
            Execute(registry);
            CollectionAssert.AreEqual(Enumerable.Range(0, 6).ToArray(), calls);
            calls.Clear();
            Execute(registry);
            CollectionAssert.AreEqual(Enumerable.Range(0, 6).ToArray(), calls);
        }

        [Test]
        public void MissingFragmentFailsBeforeExecutingAnyCallback() {
            var registry = NewRegistry();
            var calls = 0;
            Action callback = () => ++calls;
            Failure<InvalidOperationException>(() => Require(registry));
            Install(registry, "A", 3, new[] { 0, 2 }, new[] { callback, callback });
            Failure<InvalidOperationException>(() => Execute(registry));
            Assert.AreEqual(0, calls);
            Install(registry, "B", 3, new[] { 1 }, new[] { callback });
            Execute(registry);
            Assert.AreEqual(3, calls);
        }

        [Test]
        public void PublicationSnapshotsArraysAndIdenticalReplayDoesNotRepeatSlots() {
            var registry = NewRegistry();
            var calls = 0;
            Action callback = () => ++calls;
            var indices = new[] { 0 };
            var actions = new[] { callback };
            Install(registry, "A", 1, indices, actions);
            indices[0] = 7;
            actions[0] = () => calls += 100;
            Install(registry, "A", 1, new[] { 0 }, new[] { callback });
            Assert.AreEqual(0, calls);
            Execute(registry);
            Assert.AreEqual(1, calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingOwnersPoisonThePlanRegardlessOfArrivalOrder(bool reverse) {
            var registry = NewRegistry();
            var calls = 0;
            Action callback = () => ++calls;
            var owners = reverse ? new[] { "B", "A" } : new[] { "A", "B" };
            Install(registry, owners[0], 1, new[] { 0 }, new[] { callback });
            Failure<InvalidOperationException>(() => Install(registry, owners[1], 1, new[] { 0 }, new[] { callback }));
            Install(registry, owners[0], 1, new[] { 0 }, new[] { callback });
            Failure<InvalidOperationException>(() => Execute(registry));
            Assert.AreEqual(0, calls);
        }

        [TestCase("identity")]
        [TestCase("count")]
        [TestCase("callback")]
        [TestCase("ordinal")]
        public void MixedSelectionsOrChangedOwnerCannotSilentlyWin(string change) {
            var registry = NewRegistry();
            Action first = () => { };
            Action second = () => { };
            Install(registry, "A", 2, new[] { 0 }, new[] { first });
            Failure<InvalidOperationException>(() => Install(registry, "A", change == "count" ? 3 : 2,
                new[] { change == "ordinal" ? 1 : 0 }, new[] { change == "callback" ? second : first }, change == "identity" ? "different" : "selection"));
            Failure<InvalidOperationException>(() => Require(registry));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 2)]
        [TestCase(1, 0)]
        [TestCase(0, 0)]
        public void MalformedOrdinalsAreRejectedAtomically(int first, int second) {
            var registry = NewRegistry();
            Action callback = () => { };
            Failure<ArgumentException>(() => Install(registry, "A", 2, new[] { first, second }, new[] { callback, callback }));
            Failure<InvalidOperationException>(() => Require(registry));
            Install(registry, "A", 2, new[] { 0, 1 }, new[] { callback, callback });
            Assert.DoesNotThrow(() => Require(registry));
        }

        [Test]
        public void EmptyPlanNeedsAnExplicitPublication() {
            var registry = NewRegistry();
            Failure<InvalidOperationException>(() => Require(registry));
            Install(registry, "A", 0, Array.Empty<int>(), Array.Empty<Action>());
            Assert.DoesNotThrow(() => Execute(registry));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void PublishedPlanHasTypedCallbacksAndRuntimeOwnsTheirExecution(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var selected = Selected(assembly);
            Assert.IsNotEmpty(selected);
            Assert.IsTrue(selected.All(method => method.IsStatic && !method.ContainsGenericParameters && method.ReturnType == typeof(void) && method.GetParameters().Length == 0));
            Assert.Greater(selected.Count(method => method.DeclaringType.Assembly != assembly), 0,
                "Selected owner callbacks must be direct typed delegates, not aggregate forwarding methods.");
            var dispatch = assembly.GetType("ME.BECS.SourceGenerated.CoreTypeInputs", true).GetMethod("Initialize");
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(dispatch)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("RegisterInstalledTypes") }, calls);
        }

        [Test]
        public void LiveEditorRegistryContainsTheExactSelectedSequence() {
            BootstrapRuntime.RequireInstalledPlan(editor: true);
            var registry = typeof(BootstrapRuntime).GetField("editorTypes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var types = (BootstrapCallbackRegistry<Action>)Registry.GetField("callbacks", Hidden).GetValue(registry);
            var callbacks = new Action[types.Count];
            for (var i = 0; i < callbacks.Length; ++i) callbacks[i] = types.Get(i);
            var systems = typeof(BootstrapRuntime).GetField("editorSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var systemRegistry = (BootstrapCallbackRegistry<Action>)Registry.GetField("callbacks", Hidden).GetValue(systems);
            var systemCallbacks = new Action[systemRegistry.Count];
            for (var i = 0; i < systemCallbacks.Length; ++i) systemCallbacks[i] = systemRegistry.Get(i);
            CollectionAssert.AreEqual(Selected(Assembly.Load("ME.BECS.Gen.Editor")), systemCallbacks.Concat(callbacks).Select(action => action.Method).ToArray());
            var firstCall = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(BootstrapRuntime).GetMethod("LoadInstalled"))
                .First(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call).Operand;
            Assert.AreEqual(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan"), firstCall,
                "Completeness must be checked before any world/shared state is reset.");
        }
    }
}
