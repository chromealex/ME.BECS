using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorJobDebugPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorJobDebugFragmentFormat", true);
        private static object Call(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Instance).GetValue(value);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string Hash(string value) => (string)Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
            .GetMethod("Hash", Static).Invoke(null, new object[] { value });
        private static string[] Rows(bool editor) => Tests_SourceGeneratorInputCatalog.Rows(editor);
        private static object[] Documents(string[] rows, bool editor) => ((Array)Call("Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object doc) => (string)Call("Serialize", doc);
        private static string Key(string payload) => string.Join("\n", payload.Split('\n').Skip(1).Take(2));
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt).Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        internal static Dictionary<string, Type> Owners(string profile) {
            var result = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var doc in Documents(Rows(profile == "Editor"), profile == "Editor")) {
                var ownerName = Field<string>(doc, "Owner");
                var owner = Assembly.Load(ownerName).GetType("ME.BECS.SourceGenerated.DebugJobs_" + profile + "_" + Hash(ownerName), true);
                foreach (var entry in Field<KeyValuePair<int, string>[]>(doc, "Entries")) result.Add(Key(Decode(entry.Value)), owner);
            }
            return result;
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void DebugPublicationsKeepEveryContractInItsOriginalSlot(string profile) {
            var rows = Rows(profile == "Editor");
            CollectionAssert.Contains(rows, "jobdebug-publication-schema\t0\tdjE=");
            var plans = rows.Where(row => row.StartsWith("job-debug\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => Decode(row[2])).ToArray();
            Assert.IsNotEmpty(plans);
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var doc in Documents(rows, profile == "Editor")) {
                var owner = Assembly.Load(Field<string>(doc, "Owner"));
                Assert.IsFalse(owner.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                Assert.IsFalse(owner.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
                var publisher = owner.GetType("ME.BECS.SourceGenerated.JobDebugFragment_" + profile, true);
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallJobDebugFragment") }, Calls(publish));
                Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                if (profile == "Editor") Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
                var metadata = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { doc, Serialize(doc) });
                Assert.AreEqual(1, owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Count(item => item.Key == "ME.BECS.JobDebugFragment.v1" && item.Value == metadata));
                var entries = Field<KeyValuePair<int, string>[]>(doc, "Entries");
                var safety = owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Where(item => item.Key == "ME.BECS.PublishedDebugJobSafety." + profile + ".v1")
                    .Select(item => item.Value.Split('\n')).ToArray();
                Assert.IsTrue(safety.All(plan => plan.Length >= 4 && plan[0] == "v1"));
                CollectionAssert.AreEquivalent(entries.Select(entry => Key(Decode(entry.Value))),
                    safety.Select(plan => plan[1] + "\n" + plan[2]), "Exactly one safety summary per published contract and profile.");
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    var ordinal = entries[i].Key;
                    Assert.IsTrue(seen.Add(ordinal));
                    var payload = Decode(entries[i].Value);
                    Assert.AreEqual(plans[ordinal], payload);
                    var method = callbacks[i].Method;
                    Assert.AreEqual(owner, method.DeclaringType.Assembly);
                    Assert.AreEqual("ME.BECS.SourceGenerated.DebugJobs_" + profile + "_" + Hash(owner.GetName().Name), method.DeclaringType.FullName);
                    Assert.AreEqual("Initialize_" + Hash(Key(payload)), method.Name);
                    Assert.IsTrue(method.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                    Assert.IsTrue(method.DeclaringType.IsDefined(typeof(Unity.Burst.BurstCompileAttribute), false));
                    var calls = Calls(method);
                    var job = Type.GetType(payload.Split('\n')[1], true);
                    Assert.AreEqual(1, calls.Count(call => call.Name == "SetFunction" && call.DeclaringType == typeof(CompiledJobs<>).MakeGenericType(job)));
                    Assert.AreEqual(1, calls.Count(call => call.Name == "CompileFunctionPointer" && call.DeclaringType == typeof(Unity.Burst.BurstCompiler)));
                    var function = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).Where(instruction => instruction.OpCode == OpCodes.Ldftn)
                        .Select(instruction => (MethodInfo)instruction.Operand).Single(target => target.ReturnType.IsPointer);
                    Assert.IsTrue(function.IsStatic);
                    Assert.IsTrue(function.IsDefined(typeof(Unity.Burst.BurstCompileAttribute), false));
#else
                    Assert.IsEmpty(Calls(method));
#endif
                }
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, plans.Length), seen);
            var selection = Tests_SourceGeneratorBootstrapPublications.Owner(Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor"))
                .GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true);
            Assert.IsEmpty(selection.GetFields(Static));
            Assert.AreEqual(1, Calls(selection.GetMethod("Publish", Static)).Count(method => method == typeof(BootstrapRuntime).GetMethod("ExpectJobDebugPlan")));
        }

        [Test]
        public void RuntimeExecutablePublicationAssembliesHaveOneLinkerRoot() {
            var owners = Rows(false).Select(row => row.Split('\t'))
                // Input catalogs contain Editor validation metadata only, not a
                // runtime callback. They intentionally do not root an assembly.
                .Where(row => row.Length == 4 && row[0] != "inputcatalog-registration-owner" &&
                    row[0].EndsWith("-registration-owner", StringComparison.Ordinal))
                .Select(row => Decode(row[3])).Distinct(StringComparer.Ordinal).ToArray();
            Assert.IsNotEmpty(owners);
            foreach (var owner in owners) Assert.AreEqual(1, Assembly.Load(owner).GetCustomAttributesData()
                .Count(attribute => attribute.AttributeType.FullName == "UnityEngine.Scripting.AlwaysLinkAssemblyAttribute"), owner);
        }

        [Test]
        public void FormatSupportsRetiredOwnersButRejectsDuplicateContracts() {
            var rows = Rows(true);
            var docs = Documents(rows, true);
            CollectionAssert.AreEqual(docs.Select(Serialize), Documents(rows.Reverse().ToArray(), true).Select(Serialize));
            foreach (var doc in docs) {
                var args = new object[] { Serialize(doc), null };
                Assert.IsTrue((bool)Call("TryParse", args));
                Assert.AreEqual(Serialize(doc), Serialize(args[1]));
                doc.GetType().GetField("Entries", Instance).SetValue(doc, Array.Empty<KeyValuePair<int, string>>());
                Assert.IsTrue((bool)Call("TryParse", Serialize(doc), null));
            }
            var row = rows.First(value => value.StartsWith("jobdebug-registration-owner\t", StringComparison.Ordinal)).Split('\t');
            row[1] = "0";
            var first = string.Join("\t", row);
            row[1] = "1";
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { first, string.Join("\t", row) }, true));
            var payload = Decode(Decode(row[2])).Split('\n');
            payload[2] = "AnotherContract, Assembly";
            row[2] = Encode(Encode(string.Join("\n", payload)));
            Assert.DoesNotThrow(() => Documents(new[] { first, string.Join("\t", row) }, true), "A distinct contract of the same job must keep a distinct registration slot.");
            Assert.IsFalse((bool)Call("ValidEntry", Encode("v2\nOnlyJob")));
        }

        [Test]
        public void PreflightPrecedesResetAndLayoutsPreserveSafetyFields() {
            var il = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan"));
            CollectionAssert.IsSubsetOf(new[] { "runtimeJobDebug", "editorJobDebug" }, il.Where(instruction => instruction.Operand is FieldInfo)
                .Select(instruction => ((FieldInfo)instruction.Operand).Name).ToArray());
            new Tests_SourceGeneratorContracts().DebugWrapperLayoutsMatchTransportPlansWithoutBurstExecution();
            new Tests_SourceGeneratorContracts().JobBootstrapCallsFollowEveryOrderedSlotWithoutExecution();
        }

        [Test]
        public void BootstrapRegistersTheLastSelectedContractForEachJob() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            var rows = Rows(true);
            var owners = Owners("Editor");
            var plans = rows.Where(row => row.StartsWith("job-debug\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => Decode(row[2])).GroupBy(payload => payload.Split('\n')[1]).Select(group => group.Last()).ToArray();
            AllTests.Start();
            try {
                foreach (var payload in plans) {
                    var job = Type.GetType(payload.Split('\n')[1], true);
                    var get = typeof(CompiledJobs<>).MakeGenericType(job).GetMethod("GetJobType");
                    var owner = owners[Key(payload)];
                    var name = "JobDebugData_" + Hash(Key(payload));
                    Assert.AreEqual(owner.GetNestedType(name), get.Invoke(null, new object[] { false }), payload);
                    Assert.AreEqual(owner.GetNestedType(name + "Unsafe"), get.Invoke(null, new object[] { true }), payload);
                }
            } finally { AllTests.Dispose(); }
#else
            Assert.Ignore("Debug registration requires both collection-check defines.");
#endif
        }
    }
}
