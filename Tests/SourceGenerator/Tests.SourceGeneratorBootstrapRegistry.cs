using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorBootstrapRegistry {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Registry => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapPlanRegistry", true);
        private static object NewRegistry() => Activator.CreateInstance(Registry, true);
        private static void Install(object registry, string owner, Action types, Action methods, bool editor) =>
            Registry.GetMethod("Install", Hidden).Invoke(registry, new object[] { owner, types, methods, editor });
        private static object Get(object registry, bool editor) => Registry.GetMethod("Get", Hidden).Invoke(registry, new object[] { editor });
        private static object Field(object plan, string field) => plan.GetType().GetField(field, Hidden).GetValue(plan);
        private static T Failure<T>(TestDelegate action) where T : System.Exception =>
            (T)Assert.Throws<TargetInvocationException>(action).InnerException;

        [TestCase(false)]
        [TestCase(true)]
        public void PublishingIsSideEffectFreeAndProfileOrderIndependent(bool editorFirst) {
            var registry = NewRegistry();
            var calls = 0;
            Action runtimeTypes = () => calls += 1;
            Action runtimeMethods = () => calls += 2;
            Action editorTypes = () => calls += 4;
            Action editorMethods = () => calls += 8;
            foreach (var editor in new[] { editorFirst, !editorFirst })
                Install(registry, editor ? "editor owner" : "runtime owner", editor ? editorTypes : runtimeTypes,
                    editor ? editorMethods : runtimeMethods, editor);
            Assert.AreEqual(0, calls);
            foreach (var editor in new[] { false, true }) {
                var plan = Get(registry, editor);
                Assert.AreEqual(editor ? "editor owner" : "runtime owner", Field(plan, "owner"));
                Assert.AreSame(editor ? editorTypes : runtimeTypes, Field(plan, "initializeTypes"));
                Assert.AreSame(editor ? editorMethods : runtimeMethods, Field(plan, "registerMethods"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepublishingTheSamePlanDoesNotReplaceItOrExecuteIt(bool editor) {
            var registry = NewRegistry();
            var calls = 0;
            Action types = () => ++calls;
            Action methods = () => ++calls;
            Install(registry, "owner", types, methods, editor);
            var first = Get(registry, editor);
            Install(registry, "owner", types, methods, editor);
            Assert.AreSame(first, Get(registry, editor));
            Assert.AreEqual(0, calls);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ConflictsDisableTheProfileRegardlessOfPublisherOrder(bool editor, bool reverse) {
            var registry = NewRegistry();
            Action first = () => { };
            Action second = () => { };
            var owners = reverse ? new[] { "B", "A" } : new[] { "A", "B" };
            Install(registry, "other profile", first, second, !editor);
            Install(registry, owners[0], first, second, editor);
            StringAssert.Contains("Conflicting", Failure<InvalidOperationException>(() => Install(registry, owners[1], first, second, editor)).Message);
            Failure<InvalidOperationException>(() => Get(registry, editor));
            // Replaying a previously valid publisher cannot silently clear ambiguity.
            Install(registry, owners[0], first, second, editor);
            Failure<InvalidOperationException>(() => Get(registry, editor));
            Assert.IsNotNull(Get(registry, !editor));
        }

        [Test]
        public void SameOwnerWithDifferentCallbacksIsNotAnIdempotentPublication() {
            var registry = NewRegistry();
            Action first = () => { };
            Action second = () => { };
            Install(registry, "owner", first, first, true);
            Failure<InvalidOperationException>(() => Install(registry, "owner", first, second, true));
            Failure<InvalidOperationException>(() => Get(registry, true));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrInvalidPlansNeverInvokeCallbacks(bool editor) {
            var registry = NewRegistry();
            var calls = 0;
            Action callback = () => ++calls;
            StringAssert.Contains("unavailable", Failure<InvalidOperationException>(() => Get(registry, editor)).Message);
            Failure<ArgumentNullException>(() => Install(registry, "owner", null, callback, editor));
            Failure<ArgumentNullException>(() => Install(registry, "owner", callback, null, editor));
            Failure<ArgumentException>(() => Install(registry, "", callback, callback, editor));
            Failure<InvalidOperationException>(() => Get(registry, editor));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void RuntimeOwnsTheAutomaticLoadPhase() {
            var method = typeof(BootstrapRuntime).GetMethod("LoadRuntime", BindingFlags.Static | BindingFlags.NonPublic);
            var attribute = method.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad, attribute.loadType);
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("LoadInstalled") }, calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OptionalAddonPreflightIsPublishedWithoutExecutingAndConflictsAreRejected(bool editor) {
            var registry = NewRegistry();
            var calls = 0;
            Action registration = () => ++calls;
            Action validate = () => throw new InvalidOperationException("incomplete addon");
            var install = Registry.GetMethod("InstallWithPreflight", Hidden);
            var args = new object[] { "owner", registration, registration, validate, editor };
            install.Invoke(registry, args);
            install.Invoke(registry, args);
            Assert.AreSame(validate, Field(Get(registry, editor), "validateInputs"));
            Assert.Throws<InvalidOperationException>(() => ((Action)Field(Get(registry, editor), "validateInputs"))());
            Assert.AreEqual(0, calls);
            args[3] = registration;
            Failure<InvalidOperationException>(() => install.Invoke(registry, args));
            Failure<InvalidOperationException>(() => Get(registry, editor));
            Assert.IsFalse(typeof(BootstrapRuntime).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "ME.BECS.Network"));
            var checks = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan"));
            Assert.IsTrue(checks.Any(instruction => instruction.Operand is FieldInfo field && field.Name == "validateInputs"));
            Assert.IsTrue(checks.Any(instruction => instruction.Operand is MethodInfo method && method.DeclaringType == typeof(Action) && method.Name == "Invoke"));
        }

        [Test]
        public void EditorPlanWasPublishedWithoutRunningWorldInitialization() {
            Assert.DoesNotThrow(() => BootstrapRuntime.RequireInstalledPlan(editor: true));
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(AllTests).GetMethod("Start"))
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                .Select(instruction => ((MethodInfo)instruction.Operand).DeclaringType.FullName + "." + ((MethodInfo)instruction.Operand).Name).ToArray();
            CollectionAssert.AreEqual(new[] { "ME.BECS.BootstrapRuntime.RequireInstalledPlan", "ME.BECS.ObjectReferenceRegistry.ClearRuntimeObjects",
                "ME.BECS.BootstrapRuntime.LoadInstalled" }, calls);
        }
    }
}
