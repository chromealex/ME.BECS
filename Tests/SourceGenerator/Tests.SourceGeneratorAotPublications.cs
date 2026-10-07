using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorAotPublications {
        internal static string Profile(Assembly selection) => selection.BecsInputMetadata()
            .Single(attribute => attribute.Key == "ME.BECS.TypeInputProfile.v1").Value == "editor" ? "Editor" : "Runtime";
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));

        // Decode the independent, original global selection rather than deriving
        // expected coverage from whichever AOT methods happen to be emitted.
        internal static (int Ordinal, string Phase, Type Selected, Type Publisher)[] Entries(Assembly selection, bool systems) {
            var profile = Profile(selection);
            return selection.BecsInputMetadata()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row.Length == 5 && row[0] == profile.ToLowerInvariant() && row[1] == (systems ? "system" : "type") + "-registration-owner")
                .Select(row => {
                    var value = Decode(row[3]);
                    var phase = systems ? "Register" : value.Split('|')[0];
                    var identity = systems ? value : Decode(value.Split('|')[1]);
                    var owner = Assembly.Load(Decode(row[4]));
                    return (Ordinal: int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture), Phase: phase,
                        Selected: Type.GetType(identity, true), Publisher: owner.GetType("ME.BECS.SourceGenerated." + (systems ? "System" : "Type") + "Fragment_" + profile, true));
                }).OrderBy(entry => entry.Ordinal).ToArray();
        }

        internal static MethodInfo Size(Assembly selection, Type component) {
            var entry = Entries(selection, false).Single(item => item.Phase == "Register" && item.Selected == component);
            return entry.Publisher.GetMethod("Size_" + entry.Ordinal);
        }

        internal static int Flags(Assembly selection, Type component) {
            var entry = Entries(selection, false).Single(item => item.Phase == "Register" && item.Selected == component);
            var field = entry.Publisher.GetField("Flags_" + entry.Ordinal, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(field, component.FullName);
            Assert.IsTrue(field.IsLiteral && field.FieldType == typeof(int));
            return (int)field.GetRawConstantValue();
        }

        internal static string ExpectedSystemPlan(Type system) {
            var contracts = new[] { typeof(IAwake), typeof(IStart), typeof(IUpdate), typeof(IDestroy), typeof(IDrawGizmos) };
            var present = 0;
            var factory = 0;
            for (var index = 0; index < contracts.Length; ++index) {
                if (!contracts[index].IsAssignableFrom(system)) continue;
                var method = system.GetInterfaceMap(contracts[index]).TargetMethods.Single();
                present |= 1 << index;
                if (!Attribute.IsDefined(method, typeof(WithoutBurstAttribute))) factory |= 1 << index;
            }
            var burst = Attribute.IsDefined(system, typeof(Unity.Burst.BurstCompileAttribute)) ? factory : 0;
            return system.AssemblyQualifiedName + "\n" + present.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                burst.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + factory.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string SystemPlan(Assembly selection, Type system) {
            var entry = Entries(selection, true).Single(item => item.Selected == system);
            return entry.Publisher.Assembly.BecsInputMetadata()
                .Where(attribute => attribute.Key == "ME.BECS.SystemAotPublication." + Profile(selection) + ".v1")
                .Select(attribute => attribute.Value).Single(value => value.StartsWith(system.AssemblyQualifiedName + "\n", StringComparison.Ordinal));
        }

        internal static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();

        internal static void AssertRoots(Assembly selection) {
            var profile = Profile(selection);
            foreach (var systems in new[] { true, false }) {
                var entries = Entries(selection, systems);
                Assert.IsNotEmpty(entries);
                foreach (var group in entries.GroupBy(entry => entry.Publisher)) {
                    var owner = group.Key;
                    Assert.IsFalse(owner.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                    Assert.IsTrue(Attribute.IsDefined(owner, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                    var root = owner.GetMethod("PreserveReferences");
                    Assert.IsNotNull(root, owner.AssemblyQualifiedName);
                    Assert.IsTrue(root.IsStatic && !root.ContainsGenericParameters && root.ReturnType == typeof(void) && root.GetParameters().Length == 0);
                    Assert.IsTrue(Attribute.IsDefined(root, typeof(UnityEngine.Scripting.PreserveAttribute)));
                    Assert.IsFalse(Attribute.IsDefined(root, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute)));
                    Assert.IsFalse(Attribute.IsDefined(root, typeof(UnityEditor.InitializeOnLoadMethodAttribute)));
                    var callbacks = (Action[])owner.GetField("Callbacks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    Assert.IsFalse(callbacks.Any(callback => callback.Method == root));
                    var publish = owner.GetMethod("Publish", BindingFlags.NonPublic | BindingFlags.Static);
                    CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod(systems ? "InstallSystemFragment" : "InstallTypeFragment") }, Calls(publish),
                        "Publication must only install callbacks; it must never execute AOT/lifecycle/registration.");
                    if (profile == "Runtime") Assert.AreEqual(1, owner.Assembly.GetCustomAttributes(false)
                        .Count(attribute => attribute.GetType().FullName == "UnityEngine.Scripting.AlwaysLinkAssemblyAttribute"),
                        "The linker must process a publication assembly even when no scene references it.");
                }
            }
            // Never invoke PreserveReferences: system AOT bodies deliberately
            // reference lifecycle methods and are not safe runtime initializers.
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void SelectedOwnersHaveTypedPreservationRootsWithoutAggregate(string profile) {
            var selection = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            AssertRoots(selection);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DiagnosticPlanMatchesInterfaceMapsWithoutAggregateAssemblies(bool editor) {
            var selection = Tests_SourceGeneratorInputCatalog.Owner(editor);
            var rows = Tests_SourceGeneratorInputCatalog.Rows(editor);
            var entries = Entries(selection, true);
            var expected = entries.Select(entry => ExpectedSystemPlan(entry.Selected)).ToArray();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)).ToArray();
            var validation = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorValidation", true)
                .GetMethod("ReadPublishedAotPlan", BindingFlags.Static | BindingFlags.NonPublic);
            var issues = new System.Collections.Generic.List<string>();
            foreach (var reverse in new[] { false, true }) {
                var actual = (string[])validation.Invoke(null, new object[] { reverse ? rows.Reverse().ToArray() : rows, editor,
                    reverse ? assemblies.Reverse().ToArray() : assemblies, new System.Text.StringBuilder(), issues });
                Assert.IsEmpty(issues);
                CollectionAssert.AreEqual(expected, actual, "Explicit global ordinals must survive input and assembly enumeration order.");
            }
            Assert.IsNotEmpty(entries);
            var missing = entries[0].Publisher.Assembly;
            validation.Invoke(null, new object[] { rows, editor, assemblies.Where(assembly => assembly != missing).ToArray(), new System.Text.StringBuilder(), issues });
            Assert.IsNotEmpty(issues, "A compiled input catalog cannot stand in for missing typed AOT roots.");
        }
    }
}
