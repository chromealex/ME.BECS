using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorJobSetupPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorJobSetupFragmentFormat", true);
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
        private static string[] Unpack(string entry) => entry.Split('|').Select(Decode).ToArray();
        private static MethodInfo[] Calls(MethodInfo method) => ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
            .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt).Select(instruction => (MethodInfo)instruction.Operand).ToArray();

        internal static Dictionary<string, MethodInfo> Methods(string profile, string kind) {
            var aggregate = Assembly.Load("ME.BECS.Gen." + profile);
            Assert.IsNull(aggregate.GetType("ME.BECS.SourceGenerated." + kind), "Typed statistics must no longer be emitted in the aggregate assembly.");
            var result = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
            foreach (var doc in Documents(Rows(aggregate), profile == "Editor")) {
                var owner = Assembly.Load(Field<string>(doc, "Owner")).GetType("ME.BECS.SourceGenerated." + kind + "_" + profile, true);
                var entries = Field<KeyValuePair<int, string>[]>(doc, "Entries");
                Assert.AreEqual(entries.Length, owner.GetMethods(Static | BindingFlags.DeclaredOnly).Length);
                foreach (var entry in entries) {
                    var job = Unpack(entry.Value)[1];
                    var method = owner.GetMethod("Initialize_" + Hash(job), Static);
                    Assert.IsNotNull(method, job);
                    result.Add(job, method);
                }
            }
            return result;
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void OwnerCallbacksKeepExactStatisticsAndGlobalEntityGroups(string profile) {
            var aggregate = Assembly.Load("ME.BECS.Gen." + profile);
            var rows = Rows(aggregate);
            CollectionAssert.Contains(rows, "jobsetup-publication-schema\t0\tdjE=");
            var weights = rows.Where(row => row.StartsWith("job-weight\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).ToArray();
            var groups = rows.Where(row => row.StartsWith("entity-registration\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .ToDictionary(row => Decode(row[2]), row => int.Parse(row[1]));
            // Index once: decoding the entire profile for every job makes this
            // read-only contract check quadratic in the number of selected jobs.
            var entityPlans = rows.Where(row => row.StartsWith("job-entity-il\t", StringComparison.Ordinal) ||
                row.StartsWith("job-entity-fallback\t", StringComparison.Ordinal))
                .ToDictionary(row => Decode(row.Split('\t')[2]).Split('\n')[1], StringComparer.Ordinal);
            var debugPlans = rows.Where(row => row.StartsWith("job-debug\t", StringComparison.Ordinal))
                .GroupBy(row => Decode(row.Split('\t')[2]).Split('\n')[1])
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var kinds = new[] { "JobEntityInputCalls", "JobWeightInputs", "JobLayoutInputs" };
            var methods = kinds.Select(kind => Methods(profile, kind)).ToArray();
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var doc in Documents(rows, profile == "Editor")) {
                var owner = Assembly.Load(Field<string>(doc, "Owner"));
                Assert.AreNotEqual(aggregate, owner);
                var publisher = owner.GetType("ME.BECS.SourceGenerated.JobSetupFragment_" + profile, true);
                var publish = publisher.GetMethod("Publish", Static);
                CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InstallJobSetupFragment") }, Calls(publish));
                Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                if (profile == "Editor") Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
                var metadata = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { doc, Serialize(doc) });
                Assert.AreEqual(1, owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Count(item => item.Key == "ME.BECS.JobSetupFragment.v1" && item.Value == metadata));
                var entries = Field<KeyValuePair<int, string>[]>(doc, "Entries");
                var callbacks = (Action[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    var ordinal = entries[i].Key;
                    Assert.IsTrue(seen.Add(ordinal));
                    var fields = Unpack(entries[i].Value);
                    Assert.AreEqual(Decode(weights[ordinal][2]), fields[1]);
                    Assert.AreEqual(string.Join("\t", weights[ordinal]), fields[3]);
                    Assert.AreEqual(groups.Count, int.Parse(fields[2]));
                    var expectedEntity = entityPlans[fields[1]];
                    Assert.AreEqual(expectedEntity, fields[4]);
                    CollectionAssert.AreEqual(debugPlans[fields[1]], fields[5].Split('\n'));
                    var bindings = fields[6].Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split('\t')).ToArray();
                    foreach (var binding in bindings) Assert.AreEqual(groups[binding[1]], int.Parse(binding[0]));
                    CollectionAssert.AreEquivalent(Decode(expectedEntity.Split('\t')[2]).Split('\n').Skip(5)
                        .Select(row => string.Join("\t", row.Split('\t').Take(2))), bindings.Select(row => string.Join("\t", row.Skip(2))));
                    Assert.AreEqual(owner, callbacks[i].Method.DeclaringType.Assembly);
                    Assert.IsTrue(callbacks[i].Method.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                    CollectionAssert.AreEqual(methods.Select(map => map[fields[1]]), Calls(callbacks[i].Method));
                }
            }
            Assert.IsNotEmpty(seen);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, weights.Length), seen);
            var selection = aggregate.GetType("ME.BECS.SourceGenerated.BootstrapJobSetupSelection", true);
            Assert.IsEmpty(selection.GetFields(Static));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("ExpectJobSetupPlan") }, Calls(selection.GetMethod("Publish")));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void DispatchInterleavesEveryOriginalSlotWithItsStatistics(string profile) {
            var aggregate = Assembly.Load("ME.BECS.Gen." + profile);
            var rows = Rows(aggregate);
            var setup = rows.Where(row => row.StartsWith("job-weight\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .ToDictionary(row => Decode(row[2]), row => int.Parse(row[1]));
            var slots = rows.Where(row => row.StartsWith("job-early-init\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1])).Select(row => Decode(row[2]).Split('\n')[1]).ToArray();
            var setupCall = typeof(BootstrapRuntime).GetMethod("InvokeJobSetup");
            var initCall = typeof(BootstrapRuntime).GetMethod("InvokeJobEarlyInit");
            var il = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(aggregate.GetType("ME.BECS.SourceGenerated.JobBootstrapInputs", true).GetMethod("Initialize")).ToArray();
            var actual = new System.Collections.Generic.List<(MethodInfo, int)>();
            for (var i = 2; i < il.Length; ++i) {
                if (!(il[i].Operand is MethodInfo method) || method != setupCall && method != initCall) continue;
                Assert.AreEqual(profile == "Editor" ? 1 : 0, Constant(il[i - 1].OpCode, il[i - 1].Operand));
                actual.Add((method, Constant(il[i - 2].OpCode, il[i - 2].Operand)));
            }
            CollectionAssert.AreEqual(slots.SelectMany((job, index) => new[] { (setupCall, setup[job]), (initCall, index) }), actual);
        }

        [Test]
        public void FormatRoundTripsRetirementAndRejectsMalformedRows() {
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
            var sample = rows.First(row => row.StartsWith("jobsetup-registration-owner\t", StringComparison.Ordinal)).Split('\t');
            var entry = Unpack(Decode(sample[2]));
            foreach (var index in new[] { 3, 4, 5 }) {
                var malformed = entry.ToArray();
                malformed[index] = malformed[index].Substring(0, malformed[index].IndexOf('\t') + 1);
                Assert.IsFalse((bool)Call("ValidEntry", string.Join("|", malformed.Select(Encode))));
            }
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { string.Join("\t", sample), string.Join("\t", sample) }, true));
        }

        [Test]
        public void FullPlanPreflightAndCompiledInitializerBodiesRemainCovered() {
            var il = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(typeof(BootstrapRuntime).GetMethod("RequireInstalledPlan"));
            CollectionAssert.IsSubsetOf(new[] { "runtimeJobSetup", "editorJobSetup" }, il.Where(instruction => instruction.Operand is FieldInfo)
                .Select(instruction => ((FieldInfo)instruction.Operand).Name).ToArray());
            var tests = new Tests_SourceGeneratorContracts();
            tests.JobEntityInitializersPreserveGroupArgumentOrderWithoutExecution();
            tests.JobWeightInitializersPreserveSelectedSourceOrNumericPlanWithoutExecution();
            tests.JobLayoutInitializersUseNativeSizesFromSafetyPlansWithoutRunningJobs();
            Assert.DoesNotThrow(() => BootstrapRuntime.RequireInstalledPlan(editor: true));
        }

        private static int Constant(OpCode opcode, object operand) {
            if (opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_S) return Convert.ToInt32(operand);
            if (opcode == OpCodes.Ldc_I4_M1) return -1;
            if (opcode.Value >= OpCodes.Ldc_I4_0.Value && opcode.Value <= OpCodes.Ldc_I4_8.Value) return opcode.Value - OpCodes.Ldc_I4_0.Value;
            throw new InvalidOperationException("Expected an explicit job/profile integer.");
        }
    }
}
