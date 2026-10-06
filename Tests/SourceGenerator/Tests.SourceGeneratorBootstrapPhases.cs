using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorBootstrapPhases {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private static Type Phases => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapPhases", true);
        private static Type Registry => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapPlanRegistry", true);
        private static Type Slots => typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapTypeRegistry", true);
        private static object Field(object value, string name) => value.GetType().GetField(name, Hidden).GetValue(value);
        private static object NewPhases(int[] map) => Activator.CreateInstance(Phases, Hidden, null,
            new object[] { Array.Empty<Action<bool>>(), Array.Empty<Action<bool>>(), Array.Empty<Action<bool>>(), map, false, false }, null);
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string[][] Rows(Assembly assembly) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t')).ToArray();

        internal static void AssertJobSequence(Assembly assembly) {
            var rows = Rows(assembly);
            var setup = rows.Where(row => row[1] == "job-weight").ToDictionary(row => Decode(row[3]), row => int.Parse(row[2]));
            var slots = rows.Where(row => row[1] == "job-early-init").OrderBy(row => int.Parse(row[2])).Select(row => Decode(row[3]).Split('\n')[1]).ToArray();
            var owner = Tests_SourceGeneratorBootstrapPublications.PhaseInputs(assembly);
            var map = (int[])owner.GetField("JobSetupOrdinals", Static).GetValue(null);
            CollectionAssert.AreEqual(slots.Select(job => setup[job]).ToArray(), map, "Repeat every setup immediately before its exact original EarlyInit slot.");
            Assert.AreEqual(setup.Count, map.Distinct().Count());
            var feeders = rows.Where(row => row[1] == "bootstrap-feeder").OrderBy(row => int.Parse(row[2])).ToArray();
            if (slots.Length != 0) Assert.IsTrue(feeders.Any(row => row[4] == "jobs"), "Selected jobs require an executable initialization phase.");
            var initializers = (Action<bool>[])owner.GetField("Initializers", Static).GetValue(null);
            Assert.AreEqual(feeders.Length, initializers.Length);
            for (var index = 0; index < feeders.Length; ++index)
                if (feeders[index][4] == "jobs") Assert.AreEqual(typeof(BootstrapRuntime).GetMethod("InitializeInstalledJobs"), initializers[index].Method);
        }

        internal static void AssertFeederSequence(Assembly assembly) {
            var rows = Rows(assembly).Where(row => row[1] == "bootstrap-feeder").OrderBy(row => int.Parse(row[2])).ToArray();
            Assert.IsNotEmpty(rows);
            var owner = Tests_SourceGeneratorBootstrapPublications.PhaseInputs(assembly);
            MethodInfo Core(string method) => typeof(BootstrapRuntime).GetMethod(method);
            MethodInfo Addon(string assemblyName, string ownerName, string method) => Assembly.Load(assemblyName).GetType(ownerName, true).GetMethod(method);
            MethodInfo Initialize(string kind) => kind switch {
                "none" => Core("NoopPhase"), "aspects" => Core("RegisterInstalledAspects"), "entities" => Core("RegisterInstalledEntities"),
                "config-counts" => Core("RegisterInstalledConfigCounts"), "jobs" => Core("InitializeInstalledJobs"),
                "views" => Addon("ME.BECS.Views", "ME.BECS.Views.BootstrapViews", "InitializeTrackers"),
                _ => throw new InvalidOperationException(kind),
            };
            MethodInfo Register(string kind) => kind switch {
                "none" => Core("NoopPhase"), "aspect-construction" => Core("RegisterInstalledAspectConstruction"),
                "config-callbacks" => Core("RegisterInstalledConfigCallbacks"), "destroy-callbacks" => Core("RegisterInstalledDestroyCallbacks"),
                "network-methods" => Addon("ME.BECS.Network", "ME.BECS.Network.BootstrapNetworkMethods", "RegisterInstalled"),
                "view-types" => Addon("ME.BECS.Views", "ME.BECS.Views.BootstrapViews", "RegisterInstalledTypes"),
                _ => throw new InvalidOperationException(kind),
            };
            CollectionAssert.AreEqual(rows.Select(row => Initialize(row[4])).ToArray(), ((Action<bool>[])owner.GetField("Initializers", Static).GetValue(null)).Select(callback => callback.Method).ToArray());
            CollectionAssert.AreEqual(rows.Select(row => Register(row[5])).ToArray(), ((Action<bool>[])owner.GetField("Registrations", Static).GetValue(null)).Select(callback => callback.Method).ToArray());
            var expectedPreflight = new System.Collections.Generic.List<MethodInfo>();
            if (rows.Any(row => row[5] == "network-methods")) expectedPreflight.Add(Addon("ME.BECS.Network", "ME.BECS.Network.BootstrapNetworkMethods", "RequireComplete"));
            if (rows.Any(row => row[4] == "views")) expectedPreflight.Add(Addon("ME.BECS.Views", "ME.BECS.Views.BootstrapViews", "RequireComplete"));
            CollectionAssert.AreEqual(expectedPreflight, ((Action<bool>[])owner.GetField("Preflight", Static).GetValue(null)).Select(callback => callback.Method).ToArray());
            CollectionAssert.AreEqual(new[] { Core("InstallPhasePlan") }, Tests_SourceGeneratorAotPublications.Calls(owner.GetMethod("Publish")));
            AssertJobSequence(assembly);
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CompiledPhasesAreOrderedDataAndContainNoGeneratedFeederBodies(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            AssertFeederSequence(assembly);
            var owner = Tests_SourceGeneratorBootstrapPublications.Owner(assembly);
            Assert.IsFalse(owner.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
            foreach (var type in new[] { Tests_SourceGeneratorBootstrapPublications.PhaseInputs(assembly),
                owner.GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true) }) {
                Assert.IsFalse(type.GetMethods(Static).Any(method => method.Name.StartsWith("InitializeFeeder_", StringComparison.Ordinal) || method.Name.StartsWith("RegisterFeeder_", StringComparison.Ordinal)));
                foreach (var method in new[] { "RegisterAdditionalTypes", "RegisterTypePlan", "RegisterGeneratedMethods", "ValidateGeneratedInputs" }) Assert.IsNull(type.GetMethod(method, Static));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CoreJobLoopRetainsRepeatedInterleavingAcrossOwnerArrivalOrders(bool reverse) {
            var log = new System.Collections.Generic.List<string>();
            var setups = Activator.CreateInstance(Slots, true);
            var slots = Activator.CreateInstance(Slots, true);
            var install = Slots.GetMethod("Install", Hidden);
            void Publish(object registry, string prefix, int count) {
                foreach (var offset in reverse ? new[] { 1, 0 } : new[] { 0, 1 }) {
                    var ordinals = Enumerable.Range(0, count).Where(index => index % 2 == offset).ToArray();
                    install.Invoke(registry, new object[] { "plan", prefix + offset, count, ordinals,
                        ordinals.Select(index => (Action)(() => log.Add(prefix + index))).ToArray() });
                }
            }
            Publish(setups, "S", 3); Publish(slots, "E", 5);
            var map = new[] { 2, 0, 2, 1, 0 };
            var phases = NewPhases(map);
            map[0] = 99;
            Assert.IsEmpty(log);
            Phases.GetMethod("InitializeJobs", Hidden).Invoke(phases, new[] { setups, slots });
            CollectionAssert.AreEqual(new[] { "S2", "E0", "S0", "E1", "S2", "E2", "S1", "E3", "S0", "E4" }, log);
        }

        [TestCase(new[] { 0, 2 }, 2, 2)]
        [TestCase(new[] { 0, 0 }, 2, 2)]
        [TestCase(new[] { 0 }, 1, 2)]
        public void InvalidJobCoverageIsRejectedBeforeExecution(int[] map, int setups, int slots) {
            var exception = Assert.Throws<TargetInvocationException>(() => Phases.GetMethod("ValidateJobs", Hidden).Invoke(NewPhases(map), new object[] { setups, slots }));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PhasePublicationCopiesArraysAndIdenticalReplayKeepsTheCorePlan(bool editor) {
            var registry = Activator.CreateInstance(Registry, true);
            var log = new System.Collections.Generic.List<string>();
            Action<bool> first = profile => log.Add("first:" + profile);
            Action<bool> second = profile => log.Add("second:" + profile);
            var register = new[] { second, first, second };
            var args = new object[] { "owner", new[] { first, second, first }, register, Array.Empty<Action<bool>>(), Array.Empty<int>(), editor, false };
            var install = Registry.GetMethod("InstallPhases", Hidden);
            install.Invoke(registry, args);
            var get = Registry.GetMethod("Get", Hidden);
            var plan = get.Invoke(registry, new object[] { editor });
            register[0] = first;
            args[2] = new[] { second, first, second };
            install.Invoke(registry, args);
            Assert.AreSame(plan, get.Invoke(registry, new object[] { editor }));
            Assert.IsEmpty(log);
            foreach (var name in new[] { "initializeTypes", "registerMethods", "validateInputs" }) Assert.AreEqual(Phases, ((Action)Field(plan, name)).Method.DeclaringType);
            ((Action)Field(plan, "registerMethods"))();
            CollectionAssert.AreEqual(new[] { "second:" + editor, "first:" + editor, "second:" + editor }, log);
            args[2] = new[] { first, second, second };
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => install.Invoke(registry, args)).InnerException);
            Assert.Throws<TargetInvocationException>(() => get.Invoke(registry, new object[] { editor }));
        }

        [TestCase("debug")]
        [TestCase("preflight")]
        [TestCase("job-map")]
        public void ChangedProfileDataCannotSilentlyReplaceAnInstalledPlan(string change) {
            var registry = Activator.CreateInstance(Registry, true);
            var calls = 0;
            Action<bool> callback = _ => ++calls;
            var args = new object[] { "owner", new[] { callback }, new[] { callback }, new[] { callback }, new[] { 0 }, true, false };
            var install = Registry.GetMethod("InstallPhases", Hidden);
            install.Invoke(registry, args);
            switch (change) {
                case "debug": args[6] = true; break;
                case "preflight": args[3] = Array.Empty<Action<bool>>(); break;
                case "job-map": args[4] = new[] { 0, 0 }; break;
            }
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => install.Invoke(registry, args)).InnerException);
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Registry.GetMethod("Get", Hidden).Invoke(registry, new object[] { true })).InnerException);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void EmptyAndInvalidMapsDoNotExecuteAnyStartupEffects() {
            Assert.DoesNotThrow(() => Phases.GetMethod("ValidateJobs", Hidden).Invoke(NewPhases(Array.Empty<int>()), new object[] { 0, 0 }));
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => NewPhases(new[] { -1 })).InnerException);
            var registry = Activator.CreateInstance(Registry, true);
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() => Registry.GetMethod("InstallPhases", Hidden).Invoke(registry,
                new object[] { null, Array.Empty<Action<bool>>(), Array.Empty<Action<bool>>(), Array.Empty<Action<bool>>(), Array.Empty<int>(), false, false })).InnerException);
            var validate = Phases.GetMethod("ValidateInputs", Hidden);
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ValidateJobSequence", Static), typeof(Action<bool>).GetMethod("Invoke") },
                Tests_SourceGeneratorAotPublications.Calls(validate), "Reject an invalid core sequence before addon checks and any shared-state reset.");
        }
    }
}
