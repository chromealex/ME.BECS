using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorJobInitPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorJobInitFragmentFormat", true);
        private static object Call(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Instance).GetValue(value);
        private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static object[] Documents(string[] rows, bool editor) => ((Array)Call("Documents", rows, editor)).Cast<object>().ToArray();
        private static string Serialize(object doc) => (string)Call("Serialize", doc);
        private static string Row(int ordinal, string payload, string owner) => "jobinit-registration-owner\t" + ordinal + "\t" +
            Encode((string)Call("EntryValue", payload)) + "\t" + Encode(owner);
        private static string[] Rows(Assembly assembly, string profile) => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1" && item.Value.StartsWith(profile.ToLowerInvariant() + "\t", StringComparison.Ordinal))
            .Select(item => item.Value.Substring(profile.Length + 1)).ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt).Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        [TestCase(false)]
        [TestCase(true)]
        public void FormatKeepsRepeatedAndStatOnlySlotsAndSupportsRetiredOwners(bool editor) {
            const string payload = "v1\nJob, Assembly\nWrapper, Assembly\nInitialize";
            const string empty = "v1\nJob, Assembly\n\n";
            var rows = new[] { Row(0, payload, "Z"), Row(1, empty, "A"), Row(2, payload, "Z") };
            var docs = Documents(rows, editor);
            CollectionAssert.AreEqual(docs.Select(Serialize), Documents(rows.Reverse().ToArray(), editor).Select(Serialize));
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<KeyValuePair<int, string>[]>(docs[1], "Entries").Select(entry => entry.Key));
            foreach (var doc in docs) {
                var args = new object[] { Serialize(doc), null };
                Assert.IsTrue((bool)Call("TryParse", args));
                Assert.AreEqual(Serialize(doc), Serialize(args[1]));
                foreach (var entry in Field<KeyValuePair<int, string>[]>(doc, "Entries"))
                    Assert.AreEqual(entry.Key == 1 ? empty : payload, Call("Payload", entry.Value));
                doc.GetType().GetField("Entries", Instance).SetValue(doc, Array.Empty<KeyValuePair<int, string>>());
                args = new object[] { Serialize(doc), null };
                Assert.IsTrue((bool)Call("TryParse", args));
            }
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(0, payload, "Other") }, editor));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { Row(0, payload, "ME.BECS.Gen.Runtime") }, editor));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void OwnerCallbacksExactlyMatchSelectedClosedMethodsWithoutInvokingThem(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var rows = Rows(assembly, profile);
            CollectionAssert.Contains(rows, "jobinit-publication-schema\t0\tdjE=");
            var slots = rows.Where(row => row.StartsWith("job-early-init\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => Decode(row[2])).ToArray();
            Assert.IsNotEmpty(slots);
            Assert.IsTrue(slots.Any(slot => slot.Split('\n')[2].Length == 0), "Stat-only slots must not disappear.");
            Assert.IsTrue(slots.Any(slot => slot.Split('\n').Length > 4), "Closed generic wrappers must be covered.");
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var doc in Documents(rows, profile == "Editor")) {
                var owner = Assembly.Load(Field<string>(doc, "Owner"));
                Assert.AreNotEqual(assembly, owner);
                var publisher = owner.GetType("ME.BECS.SourceGenerated.JobInitFragment_" + profile, true);
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallJobInitFragment") }, Calls(publish));
                Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                if (profile == "Editor") Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
                var metadata = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { doc, Serialize(doc) });
                Assert.AreEqual(1, owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Count(item => item.Key == "ME.BECS.JobInitFragment.v1" && item.Value == metadata));
                var entries = Field<KeyValuePair<int, string>[]>(doc, "Entries");
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    var ordinal = entries[i].Key;
                    Assert.IsTrue(seen.Add(ordinal));
                    Assert.AreEqual(slots[ordinal], Call("Payload", entries[i].Value));
                    Assert.AreEqual(owner, callbacks[i].Method.DeclaringType.Assembly);
                    Assert.IsTrue(callbacks[i].Method.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                    var slot = slots[ordinal].Split('\n');
                    if (slot[2].Length == 0) { Assert.IsEmpty(Calls(callbacks[i].Method)); continue; }
                    var target = Type.GetType(slot[2], true).GetMethod(slot[3], Static);
                    if (slot.Length > 4) target = target.MakeGenericMethod(slot.Skip(4).Select(identity => Type.GetType(identity, true)).ToArray());
                    Assert.IsFalse(target.ContainsGenericParameters);
                    CollectionAssert.AreEqual(new[] { target }, Calls(callbacks[i].Method));
                }
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, slots.Length), seen);
            var selection = assembly.GetType("ME.BECS.SourceGenerated.BootstrapJobInitSelection", true);
            Assert.IsEmpty(selection.GetFields(Static));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ExpectJobInitPlan") }, Calls(selection.GetMethod("Publish")));
            var initialize = assembly.GetType("ME.BECS.SourceGenerated.JobBootstrapInputs", true).GetMethod("Initialize");
            var il = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(initialize).ToArray();
            var ordinals = new System.Collections.Generic.List<int>();
            for (var i = 2; i < il.Length; ++i) {
                if (!(il[i].Operand is MethodInfo method) || method != typeof(BootstrapRuntime).GetMethod("InvokeJobEarlyInit")) continue;
                Assert.AreEqual(profile == "Editor" ? 1 : 0, Constant(il[i - 1].OpCode, il[i - 1].Operand));
                ordinals.Add(Constant(il[i - 2].OpCode, il[i - 2].Operand));
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, slots.Length), ordinals, "Every slot must dispatch at its own exact position.");
            Assert.IsFalse(Calls(initialize).Any(method => method.DeclaringType.FullName.StartsWith("ME.BECS.SourceGenerated.JobEarlyInit_", StringComparison.Ordinal)));
        }

        private static int Constant(OpCode opcode, object operand) {
            if (opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_S) return Convert.ToInt32(operand);
            if (opcode == OpCodes.Ldc_I4_M1) return -1;
            if (opcode.Value >= OpCodes.Ldc_I4_0.Value && opcode.Value <= OpCodes.Ldc_I4_8.Value) return opcode.Value - OpCodes.Ldc_I4_0.Value;
            throw new InvalidOperationException("Expected an explicit slot/profile integer.");
        }

        [Test]
        public void FullPlanIsRequiredBeforeResetAndExistingInterleavingIsUnchanged() {
            var il = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan"));
            CollectionAssert.IsSubsetOf(new[] { "runtimeJobInit", "editorJobInit" }, il.Where(instruction => instruction.Operand is FieldInfo)
                .Select(instruction => ((FieldInfo)instruction.Operand).Name).ToArray());
            new Tests_SourceGeneratorContracts().JobBootstrapCallsFollowEveryOrderedSlotWithoutExecution();
            Assert.DoesNotThrow(() => BootstrapRuntime.RequireInstalledPlan(editor: true));
        }
    }
}
