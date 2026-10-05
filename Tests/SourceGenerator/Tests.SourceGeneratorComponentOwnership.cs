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

        private static string[] Operations(int flags) {
            var phases = new System.Collections.Generic.List<string> { "Size", "Register", "Aot" };
            if ((flags & 8) != 0) phases.AddRange(new[] { "RegisterShared", "AotShared" });
            if ((flags & 2) != 0) phases.AddRange(new[] { "RegisterStatic", "AotStatic" });
            if ((flags & 32) != 0) phases.AddRange(new[] { "RegisterConfig", "AotConfig" });
            return phases.ToArray();
        }

        private static (Type Type, int Flags)[] Selected(Assembly assembly, string profile) {
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var flags = metadata.Where(item => item.Key == "ME.BECS.ComponentFlags.v1").Select(item => item.Value.Split('\n'))
                .ToDictionary(row => row[0], row => int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture));
            return metadata.Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row.Length >= 4 && row[0] == profile.ToLowerInvariant() && row[1] == "component-registration")
                .OrderBy(row => int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture))
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])))
                .Select(identity => (Type.GetType(identity, true), flags[identity])).ToArray();
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void SelectedSourceComponentsForwardToTheirDeclaringAssembly(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var dispatch = assembly.GetType("ME.BECS.SourceGenerated.ComponentInputs", true);
            var covered = 0;
            foreach (var component in Selected(assembly, profile)) {
                var owner = Owner(component.Type);
                // Precompiled/unsupported open definitions intentionally retain a
                // compiler-owned local body until they have an owner contract.
                if (owner == null || owner.GetField("Flags_" + Key(component.Type)) == null) continue;
                ++covered;
                Assert.IsTrue(Attribute.IsDefined(owner, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                Assert.AreEqual(component.Flags, owner.GetField("Flags_" + Key(component.Type)).GetRawConstantValue());
                foreach (var operation in Operations(component.Flags)) {
                    var method = dispatch.GetMethod(operation + "_" + Name("Hash", component.Type.AssemblyQualifiedName));
                    CollectionAssert.AreEqual(new[] { Method(component.Type, operation) }, Calls(method), component.Type + " / " + operation);
                }
                Assert.IsFalse(owner.GetMethods().Any(method => Attribute.IsDefined(method, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute))),
                    "Loading an assembly must not assign component IDs or run Default getters.");
            }
            Assert.Greater(covered, 0, "This check must exercise actual forwarded registrations.");
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void ComponentPlansKeepGlobalPhaseMajorOrder(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var selected = Selected(assembly, profile);
            Assert.IsNotEmpty(selected);
            var plan = assembly.GetType("ME.BECS.SourceGenerated.CoreTypeInputs", true);
            var dispatch = assembly.GetType("ME.BECS.SourceGenerated.ComponentInputs", true);
            foreach (var operation in new[] { "Register", "Aot" }) {
                var expected = new System.Collections.Generic.List<MethodInfo>();
                foreach (var phase in new[] { (0, ""), (8, "Shared"), (2, "Static"), (32, "Config") })
                    foreach (var component in selected.Where(item => phase.Item1 == 0 || (item.Flags & phase.Item1) != 0))
                        expected.Add(operation == "Register" && Owner(component.Type)?.GetField("Flags_" + Key(component.Type)) != null ?
                            Method(component.Type, operation + phase.Item2) :
                            dispatch.GetMethod(operation + phase.Item2 + "_" + Name("Hash", component.Type.AssemblyQualifiedName)));
                var groupCount = ((Type[])assembly.GetType("ME.BECS.SourceGenerated.GroupInputs", true).GetMethod("GetComponents").Invoke(null, null)).Length;
                var actual = operation == "Register" ? Tests_SourceGeneratorBootstrapTypePlan.SelectedTypes(assembly).Skip(groupCount).ToArray() :
                    Calls(plan.GetMethod("AotComponents")).Where(method => method.DeclaringType == dispatch).ToArray();
                CollectionAssert.AreEqual(expected, actual, operation);
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
