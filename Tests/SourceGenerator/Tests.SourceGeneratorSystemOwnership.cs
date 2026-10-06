using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorSystemOwnership {
        public class NestedOwner<T> where T : unmanaged {
            public partial struct System<U> : IUpdate where U : unmanaged {
                public T first;
                public U second;
                void IUpdate.OnUpdate(ref SystemContext context) { }
            }

            #pragma warning disable CS0693 // Deliberately shadow an outer parameter.
            public partial struct Shadow<T> : IUpdate where T : unmanaged {
                public T value;
                void IUpdate.OnUpdate(ref SystemContext context) { }
            }
            #pragma warning restore CS0693
        }

        private static readonly string[] Phases = { "Awake", "Start", "Update", "Destroy", "DrawGizmos" };

        private static string Name(string method, string value) => (string)Assembly.Load("ME.BECS.Editor")
            .GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { value });

        private static Type Owner(Type system) => system.Assembly.GetType("ME.BECS.SourceGenerated.GenericSystems_" +
            Name("Encode", system.Assembly.GetName().Name), false);

        private static MethodInfo Method(Type system, string operation) {
            var definition = system.IsGenericType ? system.GetGenericTypeDefinition() : system;
            var method = Owner(system)?.GetMethod(operation + "_" + Name("Encode", definition.FullName));
            return method != null && system.IsGenericType ? method.MakeGenericMethod(system.GetGenericArguments()) : method;
        }

        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();

        private static Type[] Selected(Assembly assembly, string profile) => assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
            .Where(row => row.Length == 4 && row[0] == profile.ToLowerInvariant() && row[1] == "system-registration")
            .OrderBy(row => int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture))
            .Select(row => Type.GetType(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])), true)).ToArray();

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void SelectedSystemsUseOwnerBodiesInGlobalOrder(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var selected = Selected(assembly, profile);
            Assert.IsNotEmpty(selected);
            var callbacks = Tests_SourceGeneratorBootstrapTypePlan.SelectedSystems(assembly);
            Assert.AreEqual(selected.Length, callbacks.Length);
            var expected = selected.Select((system, index) => Method(system, "Register") ?? callbacks[index]).ToArray();
            var plan = Tests_SourceGeneratorBootstrapTypePlan.Selected(assembly);
            CollectionAssert.AreEqual(expected, plan.Take(expected.Length).ToArray(), "System IDs retain their original global order, before groups/components.");
            var forwarded = 0;
            for (var index = 0; index < selected.Length; ++index) {
                var body = Method(selected[index], "Register");
                if (body == null) {
                    CollectionAssert.AreEqual(new[] { typeof(StaticSystemTypes<>).MakeGenericType(selected[index]).GetMethod("Validate") }, Calls(callbacks[index]));
                    continue;
                }
                ++forwarded;
                Assert.AreEqual(body, callbacks[index], selected[index].ToString());
            }
            Assert.Greater(forwarded, 0);
            TestContext.WriteLine(profile + ": owner registrations=" + forwarded + ", local fallback=" + (selected.Length - forwarded));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void AotRootRetainsSelectedMasksAndCallsOwnerBodies(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var plans = Selected(assembly, profile).ToDictionary(system => system, system => Tests_SourceGeneratorAotPublications.ExpectedSystemPlan(system).Split('\n'));
            var callbacks = Tests_SourceGeneratorBootstrapTypePlan.SelectedSystems(assembly);
            var entries = Tests_SourceGeneratorAotPublications.Entries(assembly, true);
            CollectionAssert.AreEqual(Selected(assembly, profile), entries.Select(entry => entry.Selected).ToArray());
            var forwarded = 0;
            foreach (var group in entries.GroupBy(entry => entry.Publisher)) {
                var root = group.Key.GetMethod("PreserveReferences");
                Assert.IsTrue(Attribute.IsDefined(root, typeof(UnityEngine.Scripting.PreserveAttribute)));
                Assert.IsFalse(Attribute.IsDefined(root, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute)));
                var masks = group.Key.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Where(item => item.Key == "ME.BECS.SystemAotPublication." + profile + ".v1").Select(item => item.Value).ToArray();
                CollectionAssert.AreEquivalent(group.Select(entry => string.Join("\n", plans[entry.Selected])).ToArray(), masks);
                var expected = new System.Collections.Generic.List<MethodInfo>();
                foreach (var entry in group) {
                    var system = entry.Selected;
                    var row = plans[system];
                    expected.Add(callbacks[entry.Ordinal]);
                    foreach (var phaseGroup in new[] { ("Burst", 2), ("NoBurst", 1), ("", 1), ("Factory", 3) }) {
                        var mask = int.Parse(row[phaseGroup.Item2], System.Globalization.CultureInfo.InvariantCulture);
                        for (var i = 0; i < Phases.Length; ++i) {
                            if ((mask & (1 << i)) == 0) continue;
                            var method = Method(system, "Aot" + phaseGroup.Item1 + Phases[i]);
                            if (method != null) ++forwarded;
                            else method = (phaseGroup.Item1.Length == 0 ? typeof(SourceGeneratorSystemCalls) : typeof(SourceGeneratorSystemAot))
                                .GetMethod(phaseGroup.Item1 + Phases[i]).MakeGenericMethod(system);
                            expected.Add(method);
                        }
                    }
                }
                CollectionAssert.AreEqual(expected, Calls(root), "Each owner retains the exact selected phases and closed generic bodies, in original slot order.");
            }
            Assert.Greater(forwarded, 0);
        }

        [TestCase(typeof(Tests_SourceGeneratorContracts.ExplicitAotSystem))]
        [TestCase(typeof(Tests_SourceGeneratorContracts.GenericAotSystem<Tests_SourceGeneratorContracts.AotMarker>))]
        [TestCase(typeof(NestedOwner<int>.System<float>))]
        [TestCase(typeof(NestedOwner<int>.Shadow<float>))]
        public void OwnerContractsSupportExplicitAndNestedGenericSystems(Type system) {
            var owner = Owner(system);
            Assert.IsNotNull(owner);
            Assert.IsTrue(Attribute.IsDefined(owner, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            Assert.IsFalse(Attribute.IsDefined(owner, typeof(UnityEngine.Scripting.PreserveAttribute)), "Unselected owners are not new AOT roots.");
            var register = Method(system, "Register");
            Assert.IsNotNull(register);
            var validation = Calls(register).Single();
            Assert.AreEqual(typeof(StaticSystemTypes<>).MakeGenericType(system), validation.DeclaringType);
            Assert.AreEqual("Validate", validation.Name);
            foreach (var phase in Phases) {
                var contract = typeof(ISystem).Assembly.GetType("ME.BECS.I" + phase, true);
                if (!contract.IsAssignableFrom(system)) {
                    Assert.IsNull(Method(system, "Aot" + phase));
                    continue;
                }
                foreach (var kind in new[] { "", "Burst", "NoBurst", "Factory" }) {
                    var wrapper = Method(system, "Aot" + kind + phase);
                    Assert.IsNotNull(wrapper);
                    Assert.IsFalse(Attribute.IsDefined(wrapper, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute)));
                    var call = Calls(wrapper).Single();
                    Assert.AreEqual(kind.Length == 0 ? typeof(SourceGeneratorSystemCalls) : typeof(SourceGeneratorSystemAot), call.DeclaringType);
                    Assert.AreEqual(kind + phase, call.Name);
                    CollectionAssert.AreEqual(new[] { system }, call.GetGenericArguments());
                }
            }
            var bridge = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorBridge", true);
            var args = new object[] { system, null };
            Assert.IsTrue((bool)bridge.GetMethod("TryGetSystemRegistration", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args));
            StringAssert.Contains(owner.FullName + "." + register.Name, (string)args[1]);
            var catalog = system.Assembly.GetType("ME.BECS.SourceGenerated.Catalog_" + Name("Encode", system.Assembly.GetName().Name), true);
            Assert.IsFalse(catalog.GetMethods().Any(method => method.Name.StartsWith("RegisterSystem_", StringComparison.Ordinal)),
                "The discovery catalog must not retain duplicate ordinary-system registration bodies.");
        }
    }
}
