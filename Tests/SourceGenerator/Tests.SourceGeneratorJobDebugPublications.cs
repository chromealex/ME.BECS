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
        private static string[] Rows(Assembly assembly) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Substring(item.Value.IndexOf('\t') + 1)).ToArray();
        private static object[] Documents(string[] rows, bool editor) => ((Array)Call("Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object doc) => (string)Call("Serialize", doc);
        private static string Key(string payload) => string.Join("\n", payload.Split('\n').Skip(1).Take(2));
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt).Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        internal static Dictionary<string, Type> Owners(string profile) {
            var result = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var doc in Documents(Rows(Assembly.Load("ME.BECS.Gen." + profile)), profile == "Editor")) {
                var ownerName = Field<string>(doc, "Owner");
                var owner = Assembly.Load(ownerName).GetType("ME.BECS.SourceGenerated.DebugJobs_" + profile + "_" + Hash(ownerName), true);
                foreach (var entry in Field<KeyValuePair<int, string>[]>(doc, "Entries")) result.Add(Key(Decode(entry.Value)), owner);
            }
            return result;
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void DebugPublicationsKeepEveryContractInItsOriginalSlot(string profile) {
            var aggregate = Assembly.Load("ME.BECS.Gen." + profile);
            var rows = Rows(aggregate);
            CollectionAssert.Contains(rows, "jobdebug-publication-schema\t0\tdjE=");
            var plans = rows.Where(row => row.StartsWith("job-debug\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => Decode(row[2])).ToArray();
            Assert.IsNotEmpty(plans);
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var doc in Documents(rows, profile == "Editor")) {
                var owner = Assembly.Load(Field<string>(doc, "Owner"));
                Assert.AreNotEqual(aggregate, owner);
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
            var selection = aggregate.GetType("ME.BECS.SourceGenerated.BootstrapJobDebugSelection", true);
            Assert.IsEmpty(selection.GetFields(Static));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ExpectJobDebugPlan") }, Calls(selection.GetMethod("Publish")));
            var adapter = aggregate.GetType((profile == "Editor" ? "ME.BECS.Editor" : "ME.BECS") + ".DebugJobs", true);
            Assert.IsEmpty(adapter.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InitializeJobDebug") }, Calls(adapter.GetMethod("InitializeJobsDebug")));
#endif
        }

        [Test]
        public void RuntimePublicationAssembliesHaveOneLinkerRoot() {
            var owners = Rows(Assembly.Load("ME.BECS.Gen.Runtime")).Select(row => row.Split('\t'))
                .Where(row => row.Length == 4 && row[0].EndsWith("-registration-owner", StringComparison.Ordinal))
                .Select(row => Decode(row[3])).Distinct(StringComparer.Ordinal).ToArray();
            Assert.IsNotEmpty(owners);
            foreach (var owner in owners) Assert.AreEqual(1, Assembly.Load(owner).GetCustomAttributesData()
                .Count(attribute => attribute.AttributeType.FullName == "UnityEngine.Scripting.AlwaysLinkAssemblyAttribute"), owner);
        }

        [Test]
        public void FormatSupportsRetiredOwnersButRejectsDuplicateContracts() {
            var rows = Rows(Assembly.Load("ME.BECS.Gen.Editor"));
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
            var rows = Rows(Assembly.Load("ME.BECS.Gen.Editor"));
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
