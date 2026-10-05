using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorGraphPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorGraphFragmentFormat", true);
        private static object Call(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Instance).GetValue(value);
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static Assembly Aggregate => Assembly.Load("ME.BECS.Gen.Runtime");
        private static AssemblyMetadataAttribute[] Metadata(Assembly assembly) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
        private static string[] Rows() => Metadata(Aggregate).Where(item => item.Key == "ME.BECS.TypeInput.v1" && item.Value.StartsWith("runtime\t", StringComparison.Ordinal))
            .Select(item => item.Value.Substring("runtime\t".Length)).ToArray();
        private static object[] Documents() => ((Array)Call("Documents", Rows(), false)).Cast<object>().ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt).Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        internal static Assembly Owner(string graphId) {
            foreach (var doc in Documents()) foreach (var entry in Field<KeyValuePair<int, string>[]>(doc, "Entries"))
                if (Decode(entry.Value).Split('\n')[0].Split('\t')[3] == graphId) return Assembly.Load(Field<string>(doc, "Owner"));
            throw new InvalidOperationException("No publication owner for graph " + graphId);
        }

        [Test]
        public void GraphCallbacksAndStorageLiveOutsideTheAggregateInOriginalSlots() {
            var rows = Rows();
            CollectionAssert.Contains(rows, "graph-publication-schema\t0\tdjE=");
            var original = (string[])Call("Entries", (object)rows);
            Assert.IsNotEmpty(original);
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var doc in Documents()) {
                var owner = Assembly.Load(Field<string>(doc, "Owner"));
                Assert.AreNotEqual(Aggregate, owner);
                var publisher = owner.GetType("ME.BECS.SourceGenerated.GraphFragment_Runtime", true);
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallGraphFragment") }, Calls(publish));
                Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                Assert.AreEqual(1, owner.GetCustomAttributesData().Count(attribute => attribute.AttributeType.FullName == "UnityEngine.Scripting.AlwaysLinkAssemblyAttribute"));
                var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
                var expected = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { doc, Call("Serialize", doc) });
                Assert.AreEqual(1, Metadata(owner).Count(item => item.Key == "ME.BECS.GraphFragment.v1" && item.Value == expected));
                var entries = Field<KeyValuePair<int, string>[]>(doc, "Entries");
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var index = 0; index < entries.Length; ++index) {
                    var entry = entries[index];
                    Assert.IsTrue(seen.Add(entry.Key));
                    Assert.AreEqual(original[entry.Key], entry.Value);
                    var graph = Decode(entry.Value).Split('\n')[0].Split('\t');
                    var prefix = Decode(graph[2]);
                    var suffix = System.Math.Abs(int.Parse(graph[3], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture) + "_SystemsCodeGenerator";
                    foreach (var phase in new[] { "Initialize", "Awake", "Start", "Update", "Destroy", "DrawGizmos" }) {
                        Assert.IsNull(Aggregate.GetType(prefix + phase));
                        Assert.IsNotNull(owner.GetType(prefix + phase, true));
                    }
                    var initialize = owner.GetType(prefix + "Initialize", true);
                    Assert.AreEqual(1, initialize.GetFields(Static).Count(field => field.Name == "graphNodes" + suffix + "Data"));
                    var callback = callbacks[index].Method;
                    Assert.AreEqual(owner, callback.DeclaringType.Assembly);
                    Assert.AreEqual("ME.BECS.SourceGenerated.GraphRegistration_" + entry.Key, callback.DeclaringType.FullName);
                    CollectionAssert.AreEqual(new[] { "RegisterMethod", "RegisterAwakeMethod", "RegisterStartMethod", "RegisterUpdateMethod",
                        "RegisterDrawGizmosMethod", "RegisterDestroyMethod", "RegisterGetSystemMethod" }, Calls(callback).Select(method => method.Name));
                    var targets = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(callback).Where(instruction => instruction.OpCode == OpCodes.Ldftn)
                        .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
                    Assert.AreEqual(7, targets.Length);
                    Assert.IsTrue(targets.All(method => method.DeclaringType.Assembly == owner && method.Name.EndsWith(suffix, StringComparison.Ordinal)));
                }
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, original.Length), seen);
            Assert.IsNull(Aggregate.GetType("ME.BECS.SourceGenerated.GenericJobDeltaInputs"));
        }

        [Test]
        public void PublishedLifecycleAndInjectionPlansExactlyMatchTheDiagnosticSnapshot() {
            var original = Metadata(Aggregate);
            foreach (var doc in Documents()) {
                var owner = Assembly.Load(Field<string>(doc, "Owner"));
                var ids = Field<KeyValuePair<int, string>[]>(doc, "Entries").Select(entry => Decode(entry.Value).Split('\n')[0].Split('\t')[3]).ToArray();
                foreach (var key in new[] { "GraphSystemInjectionPlan", "GraphJobSelection", "GraphJobInjectionPlan", "GraphInjectionActions", "GraphLifecyclePlan", "GraphSyncComparison" }) {
                    var payloads = original.Where(item => item.Key == "ME.BECS." + key + ".v1")
                        .Select(item => item.Value).Where(value => ids.Contains(value.Split('\n')[key == "GraphLifecyclePlan" || key == "GraphSyncComparison" ? 0 : 1])).ToArray();
                    CollectionAssert.AreEquivalent(payloads, Metadata(owner).Where(item => item.Key == "ME.BECS.Published" + key + ".v1").Select(item => item.Value), key);
                }
            }
        }

        [Test]
        public void OwnerInjectionCallsRetainRepeatedRegistrationOrder() {
            var metadata = Metadata(Aggregate);
            foreach (var graph in Rows().Where(row => row.StartsWith("graph-registration\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))) {
                var owner = Owner(graph[3]);
                var initialize = owner.GetType(Decode(graph[2]) + "Initialize", true);
                var actual = Calls(initialize.GetMethod("ApplyInjections", Static));
                var actions = metadata.Single(item => item.Key == "ME.BECS.GraphInjectionActions.v1" && item.Value.Split('\n')[1] == graph[3]).Value.Split('\n')[2];
                var expected = actions.Split(',').Where(value => value.Length != 0).Select(action =>
                    (action[0] == 's' ? "InjectSystem_" : action[0] == 'j' ? "RegisterJob_" : "Register_") + action.Substring(2))
                    .Where(name => !name.StartsWith("InjectSystem_", StringComparison.Ordinal) || initialize.GetMethod(name, Static) != null).ToArray();
                CollectionAssert.AreEqual(expected, actual.Select(method => method.Name));
                Assert.IsTrue(actual.All(method => method.DeclaringType.Assembly == owner));
            }
        }

        [Test]
        public void GraphPreflightAndFirstPassDispatcherDoNotOwnTypedState() {
            var selection = Aggregate.GetType("ME.BECS.SourceGenerated.BootstrapGraphSelection", true);
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ExpectGraphPlan") }, Calls(selection.GetMethod("Publish")));
            var adapter = Aggregate.GetType("ME.BECS.SourceGenerated.GraphInputs", true);
            Assert.IsEmpty(adapter.GetFields(Static));
            Assert.IsEmpty(adapter.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("RegisterInstalledGraphs") }, Calls(adapter.GetMethod("Register")));
            Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen,
                adapter.GetMethod("Initialize", Static).GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
            var fields = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan"))
                .Where(instruction => instruction.Operand is FieldInfo).Select(instruction => ((FieldInfo)instruction.Operand).Name);
            CollectionAssert.Contains(fields, "runtimeGraphs");
        }

        [Test]
        public void GraphEnvelopeRejectsMixedLayoutsAndAcceptsRetiredOwners() {
            var documents = Documents();
            Assert.IsNotEmpty(documents);
            foreach (var doc in documents) {
                var serialized = (string)Call("Serialize", doc);
                var args = new object[] { serialized, null };
                Assert.IsTrue((bool)Call("TryParse", args));
                Assert.AreEqual(serialized, Call("Serialize", args[1]));
                doc.GetType().GetField("Entries", Instance).SetValue(doc, Array.Empty<KeyValuePair<int, string>>());
                Assert.IsTrue((bool)Call("TryParse", Call("Serialize", doc), null));
            }
            var entry = ((string[])Call("Entries", (object)Rows())).First(value => Decode(value).Contains("graph-system\t"));
            var rows = Decode(entry).Split('\n');
            var index = Array.FindIndex(rows, row => row.StartsWith("graph-system\t", StringComparison.Ordinal));
            var fields = rows[index].Split('\t');
            fields[4] = "999999";
            rows[index] = string.Join("\t", fields);
            Assert.IsFalse((bool)Call("ValidEntry", Encode(string.Join("\n", rows))));
            Assert.IsFalse((bool)Call("ValidEntry", Encode(Decode(entry) + "\ngraph-registration\t0\tdGVzdA==\t99\t0")));
            Assert.Throws<TargetInvocationException>(() => Call("Documents", Rows().Where(row => !row.StartsWith("graph-registration-owner\t", StringComparison.Ordinal)).ToArray(), false));
            Assert.Throws<TargetInvocationException>(() => Call("Documents", Rows(), true));
        }
    }
}
