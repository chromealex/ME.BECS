using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorComponentOwnership {
        public class GenericOwner<T> where T : unmanaged {
            public struct Nested<U> : IComponent where U : unmanaged {
                public T first;
                public U second;
                public static Nested<U> Default => default;
            }
        }

        public struct GenericShared<T> : IComponentShared where T : unmanaged {
            public T value;
            uint IComponentShared.GetHash() => 17u;
        }

        public struct StaticRegistration : IConfigComponentStatic { public int value; }
        public struct ConfigRegistration : IConfigInitialize {
            public int value;
            public void OnInitialize(in Ent ent) { }
        }

        private static string Name(string method, string value) => (string)Assembly.Load("ME.BECS.Editor")
            .GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { value });

        private static Type Owner(Type component) => component.Assembly.GetType("ME.BECS.SourceGenerated.ComponentRegistrations_" +
            Name("Encode", component.Assembly.GetName().Name), false);

        private static string Key(Type component) => Name("Encode", (component.IsGenericType ? component.GetGenericTypeDefinition() : component).FullName);

        private static MethodInfo Method(Type component, string operation) {
            var method = Owner(component).GetMethod(operation + "_" + Key(component));
            return component.IsGenericType ? method.MakeGenericMethod(component.GetGenericArguments()) : method;
        }

        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        private static (Type Type, int Flags)[] Selected(Assembly assembly, string profile) {
            var metadata = assembly.BecsInputMetadata().ToArray();
            var flags = Tests_SourceGeneratorAotPublications.Entries(assembly, false).Where(entry => entry.Phase == "Register")
                .ToDictionary(entry => entry.Selected.AssemblyQualifiedName, entry => (int)entry.Publisher.GetField("Flags_" + entry.Ordinal).GetRawConstantValue());
            return metadata.Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row.Length >= 4 && row[0] == profile.ToLowerInvariant() && row[1] == "component-registration")
                .OrderBy(row => int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture))
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])))
                .Select(identity => (Type.GetType(identity, true), flags[identity])).ToArray();
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void SelectedSourceComponentsForwardToTheirDeclaringAssembly(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var entries = Tests_SourceGeneratorAotPublications.Entries(assembly, false);
            var callbacks = Tests_SourceGeneratorBootstrapTypePlan.SelectedTypes(assembly);
            var covered = 0;
            foreach (var component in Selected(assembly, profile)) {
                var owner = Owner(component.Type);
                // Precompiled/unsupported open definitions intentionally retain a
                // compiler-owned local body until they have an owner contract.
                if (owner == null || owner.GetField("Flags_" + Key(component.Type)) == null) continue;
                ++covered;
                Assert.IsTrue(Attribute.IsDefined(owner, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                Assert.AreEqual(component.Flags, owner.GetField("Flags_" + Key(component.Type)).GetRawConstantValue());
                foreach (var entry in entries.Where(item => item.Selected == component.Type && item.Phase != "Group")) {
                    Assert.AreEqual(Method(component.Type, entry.Phase), callbacks[entry.Ordinal], component.Type + " / " + entry.Phase);
                    var aot = entry.Publisher.GetMethod("Aot_" + entry.Ordinal);
                    CollectionAssert.AreEqual(new[] { Method(component.Type, "Aot" + entry.Phase.Substring("Register".Length)) }, Calls(aot));
                    if (entry.Phase == "Register") CollectionAssert.AreEqual(new[] { Method(component.Type, "Size") }, Calls(entry.Publisher.GetMethod("Size_" + entry.Ordinal)));
                }
                Assert.IsFalse(owner.GetMethods().Any(method => Attribute.IsDefined(method, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute))),
                    "Loading an assembly must not assign component IDs or run Default getters.");
            }
            Assert.Greater(covered, 0, "This check must exercise actual forwarded registrations.");
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void ComponentPlansKeepGlobalPhaseMajorOrder(string profile) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var selected = Selected(assembly, profile);
            Assert.IsNotEmpty(selected);
            var callbacks = Tests_SourceGeneratorBootstrapTypePlan.SelectedTypes(assembly);
            var entries = Tests_SourceGeneratorAotPublications.Entries(assembly, false);
            var expected = new System.Collections.Generic.List<string>();
            foreach (var phase in new[] { (0, ""), (8, "Shared"), (2, "Static"), (32, "Config") })
                foreach (var component in selected.Where(item => phase.Item1 == 0 || (item.Flags & phase.Item1) != 0))
                    expected.Add("Register" + phase.Item2 + "\n" + component.Type.AssemblyQualifiedName);
            CollectionAssert.AreEqual(expected, entries.Where(entry => entry.Phase != "Group").Select(entry => entry.Phase + "\n" + entry.Selected.AssemblyQualifiedName).ToArray());
            foreach (var group in entries.GroupBy(entry => entry.Publisher)) {
                var aot = group.Where(entry => entry.Phase != "Group").Select(entry => group.Key.GetMethod("Aot_" + entry.Ordinal)).ToArray();
                CollectionAssert.AreEqual(aot, Calls(group.Key.GetMethod("PreserveReferences")), "AOT preserves exactly the phase-major slots assigned to this owner.");
                foreach (var entry in group) {
                    if (entry.Phase == "Group") { Assert.IsNull(group.Key.GetMethod("Aot_" + entry.Ordinal)); continue; }
                    var forwarded = Owner(entry.Selected)?.GetField("Flags_" + Key(entry.Selected)) != null;
                    var register = forwarded ? Method(entry.Selected, entry.Phase) : callbacks[entry.Ordinal];
                    Assert.AreEqual(register, callbacks[entry.Ordinal]);
                    var expectedAot = forwarded ? Method(entry.Selected, "Aot" + entry.Phase.Substring("Register".Length)) :
                        (entry.Phase == "RegisterShared" ? typeof(StaticTypesShared<>) : entry.Phase == "RegisterStatic" ? typeof(StaticTypesStatic<>) :
                            entry.Phase == "RegisterConfig" ? typeof(ConfigInitializeTypes<>) : typeof(StaticTypes<>)).MakeGenericType(entry.Selected).GetMethod("AOT");
                    var actual = Calls(group.Key.GetMethod("Aot_" + entry.Ordinal)).Single();
                    if (!forwarded) actual = Calls(actual).Single();
                    Assert.AreEqual(expectedAot, actual, entry.Selected + " / " + entry.Phase);
                }
            }
        }

        [TestCase(typeof(Tests_SourceGeneratorContracts.CompilerDefault), 4, 4u)]
        [TestCase(typeof(Tests_SourceGeneratorContracts.CompilerSizedEmpty), 0, 8u)]
        [TestCase(typeof(Tests_SourceGeneratorContracts.CompilerNativeBool), 0, 1u)]
        [TestCase(typeof(GenericOwner<int>.Nested<float>), 4, 8u)]
        [TestCase(typeof(GenericShared<int>), 24, 4u)]
        [TestCase(typeof(StaticRegistration), 2, 4u)]
        [TestCase(typeof(ConfigRegistration), 32, 4u)]
        public void OwnerBodiesSupportNativeLayoutClosedGenericsAndSpecialPhases(Type component, int flags, uint size) {
            var owner = Owner(component);
            Assert.IsNotNull(owner);
            Assert.AreEqual(flags, owner.GetField("Flags_" + Key(component)).GetRawConstantValue());
            Assert.AreEqual(size, Method(component, "Size").Invoke(null, null));
            var sizeFields = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(Method(component, "Size"))
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Ldsfld)
                .Select(instruction => (FieldInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(new[] { typeof(TSize<>).MakeGenericType(component).GetField("size") }, sizeFields,
                "The component owner must use the BECS native-size API without a new Unity.Collections asmdef dependency.");
            // Inspect rather than invoke registration, Default or AOT methods.
            var register = Calls(Method(component, "Register"));
            CollectionAssert.AreEqual((flags & 4) == 0 ? new[] { "Validate" } : new[] { "Validate", "get_Default", "SetDefaultValue" },
                register.Select(method => method.Name).ToArray());
            Assert.AreEqual(component, register[0].DeclaringType.GetGenericArguments().Single());
            foreach (var phase in new[] { (8, "Shared", typeof(StaticTypesShared<>)), (2, "Static", typeof(StaticTypesStatic<>)),
                         (32, "Config", typeof(ConfigInitializeTypes<>)) }) {
                if ((flags & phase.Item1) == 0) {
                    Assert.IsNull(owner.GetMethod("Register" + phase.Item2 + "_" + Key(component)));
                    Assert.IsNull(owner.GetMethod("Aot" + phase.Item2 + "_" + Key(component)));
                    continue;
                }
                var aot = Calls(Method(component, "Aot" + phase.Item2)).Single();
                Assert.AreEqual(phase.Item3.MakeGenericType(component), aot.DeclaringType);
                Assert.AreEqual("AOT", aot.Name);
            }
        }
    }
}
