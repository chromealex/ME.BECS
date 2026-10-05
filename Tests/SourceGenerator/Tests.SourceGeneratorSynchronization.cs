using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Source metadata fixtures only. No worlds or jobs are executed by these tests.
        public static void SyncFlowCopies(ref SystemContext context, in Ent ent) {
            var dependency = context.dependsOn;
            var unrelated = default(JobHandle);
            unrelated.Complete();
            dependency.Complete();
            ent.Set(new TestComponent());
        }

        public static void SyncFlowBranch(ref SystemContext context, bool condition, in Ent ent) {
            if (condition) context.dependsOn.Complete();
            ent.Set(new TestComponent());
        }

        public static void SyncFlowReschedule(ref SystemContext context, in Ent ent) {
            var oldDependency = context.dependsOn;
            context.SetDependency(context.Query().Schedule<QueryModeJob, TestComponent>());
            oldDependency.Complete();
            ent.Set(new TestComponent());
        }

        public static void SyncFlowRefArguments(in JobHandle observed, ref JobHandle replacement, out JobHandle output) {
            output = replacement;
            observed.Complete();
        }

        public static void SyncFlowCallRefArguments(JobHandle handle) {
            var replacement = handle;
            SyncFlowRefArguments(in handle, ref replacement, out var output);
            output.Complete();
        }

        public static JobHandle SyncFlowReturn(ref SystemContext context) => context.dependsOn;
        public static T SyncFlowIdentity<T>(T handle) => handle;
        public static void SyncFlowGenericHandle(ref SystemContext context) => SyncFlowIdentity(context.dependsOn).Complete();
        public static void SyncFlowPair(JobHandle first, JobHandle second) { }
        public static void SyncFlowNamedArguments(ref SystemContext context) =>
            SyncFlowPair(second: SyncFlowReturn(ref context), first: context.dependsOn);

        public static void SyncFlowGeneric<T>(ref SystemContext context, in Ent ent) where T : unmanaged, IComponent {
            context.dependsOn.Complete();
            ent.Set(default(T));
        }

        [System.Diagnostics.Conditional("BECS_SOURCE_GENERATOR_NEVER_DEFINED_SYNC_FLOW_TEST")]
        public static void SyncFlowOmittedCall(JobHandle handle) => handle.Complete();
        public static void SyncFlowOmitted(ref SystemContext context) {
            SyncFlowOmittedCall(SyncFlowReturn(ref context));
        }

        public static void SyncFlowFinally(ref SystemContext context, in Ent ent) {
            try { context.dependsOn.Complete(); }
            finally { ent.Set(new TestComponent()); }
        }

        public static JobHandle SyncFlowConditionalValue(bool condition, JobHandle first, JobHandle second) =>
            condition ? first : second;

        private static string[][] SynchronizationRows(string name) {
            var summary = ScheduleModeMethodSummary(name);
            var flags = summary[1].Split(',');
            CollectionAssert.Contains(flags, "sync-flow-schema=3");
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(flags.Single(flag =>
                flag.StartsWith("sync-flow=", StringComparison.Ordinal)).Substring("sync-flow=".Length)));
            var rows = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split('\t')).ToArray();
            Assert.AreEqual("v3", rows[0][0]);
            return rows;
        }

        private static System.Collections.Generic.Dictionary<string, string[]> SynchronizationContracts(string[][] rows) =>
            rows.Where(row => row[0] == "S").ToDictionary(row => row[1], row =>
                Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\t'));

        private static string SynchronizationCallName(string[] call, System.Collections.Generic.Dictionary<string, string[]> contracts) =>
            contracts[call[2]][3];

        [TestCase(nameof(SyncFlowCopies))]
        [TestCase(nameof(SyncFlowBranch))]
        [TestCase(nameof(SyncFlowReschedule))]
        [TestCase(nameof(SyncFlowRefArguments))]
        [TestCase(nameof(SyncFlowCallRefArguments))]
        [TestCase(nameof(SyncFlowReturn))]
        [TestCase(nameof(SyncFlowNamedArguments))]
        [TestCase(nameof(SyncFlowGeneric))]
        [TestCase(nameof(SyncFlowIdentity))]
        [TestCase(nameof(SyncFlowGenericHandle))]
        [TestCase(nameof(SyncFlowConditionalValue))]
        [TestCase(nameof(SyncFlowFinally))]
        [TestCase(nameof(SyncCaptureValueArgument))]
        [TestCase(nameof(SyncCaptureRefArgument))]
        [TestCase(nameof(SyncCaptureScalarStore))]
        public void SourceSynchronizationFlowHasBoundSlotsAndEdges(string name) {
            var rows = SynchronizationRows(name);
            CollectionAssert.IsEmpty(rows.Where(row => row[0] == "G"), string.Join("\n", rows.Select(row => string.Join("\t", row))));
            var slots = rows.Where(row => row[0] == "V" || row[0] == "P" || row[0] == "I").Select(row => row[1]).ToArray();
            CollectionAssert.AllItemsAreUnique(slots);
            var symbols = SynchronizationContracts(rows);
            var blocks = rows.Where(row => row[0] == "B").ToArray();
            CollectionAssert.AllItemsAreUnique(blocks.Select(row => row[1]).ToArray());
            Assert.AreEqual(1, blocks.Count(row => row[2] == "Entry"));
            Assert.AreEqual(1, blocks.Count(row => row[2] == "Exit"));
            foreach (var block in blocks) {
                foreach (var edge in block.Skip(4)) {
                    var destination = edge.Split(':')[0];
                    if (destination != "-") CollectionAssert.Contains(blocks.Select(row => row[1]).ToArray(), destination);
                }
            }
            foreach (var row in rows) {
                string[] operands;
                if (row[0] == "C") {
                    Assert.IsTrue(symbols.ContainsKey(row[2]));
                    CollectionAssert.AllItemsAreUnique(row.Skip(5).Select(argument => argument.Split(':')[0]).ToArray());
                    operands = new[] { row[1], row[4] }.Concat(row.Skip(5).Select(argument => argument.Split(':')[2])).ToArray();
                } else if (row[0] == "=" || row[0] == "Z" || row[0] == "?" || row[0] == "R") operands = row.Skip(1).ToArray();
                else continue;
                foreach (var slot in operands.Where(slot => slot != "-")) CollectionAssert.Contains(slots, slot);
            }
        }

        [Test]
        public void SynchronizationCopiesDoNotConfuseDefaultAndEntryHandles() {
            var rows = SynchronizationRows(nameof(SyncFlowCopies));
            var contracts = SynchronizationContracts(rows);
            var origins = new System.Collections.Generic.Dictionary<string, string>();
            var completed = new System.Collections.Generic.List<string>();
            foreach (var row in rows) {
                if (row[0] == "Z") origins[row[1]] = "default";
                else if (row[0] == "=") origins[row[1]] = origins[row[2]];
                else if (row[0] == "C") {
                    var call = SynchronizationCallName(row, contracts);
                    if (call.Contains("SystemContext.get_dependsOn")) {
                        Assert.AreEqual("p0", row[4]);
                        origins[row[1]] = "entry";
                    } else if (call == "M:Unity.Jobs.JobHandle.Complete") completed.Add(origins[row[4]]);
                }
            }
            CollectionAssert.AreEqual(new[] { "default", "entry" }, completed);
        }

        [Test]
        public void SynchronizationBranchKeepsThePathWithoutComplete() {
            var rows = SynchronizationRows(nameof(SyncFlowBranch));
            var contracts = SynchronizationContracts(rows);
            var blockId = "";
            var completeBlock = "";
            var accessBlock = "";
            foreach (var row in rows) {
                if (row[0] == "B") blockId = row[1];
                if (row[0] != "C") continue;
                if (SynchronizationCallName(row, contracts) == "M:Unity.Jobs.JobHandle.Complete") completeBlock = blockId;
                if (contracts[row[2]].Any(token => token.StartsWith("!safety=", StringComparison.Ordinal))) accessBlock = blockId;
            }
            Assert.IsNotEmpty(completeBlock);
            Assert.IsNotEmpty(accessBlock);
            Assert.AreNotEqual(completeBlock, accessBlock);
            var branch = rows.Single(row => row[0] == "B" && row[3] != "None");
            CollectionAssert.AreEquivalent(new[] { completeBlock, accessBlock }, branch.Skip(4).Select(edge => edge.Split(':')[0]).ToArray());
        }

        [Test]
        public void SynchronizationCallBindingsPreserveRefModesAndDefensiveCopies() {
            var callee = SynchronizationRows(nameof(SyncFlowRefArguments));
            CollectionAssert.AreEqual(new[] { "in", "ref", "out" }, callee.Where(row => row[0] == "P").Select(row => row[3]).ToArray());
            var contracts = SynchronizationContracts(callee);
            var complete = callee.Single(row => row[0] == "C" && SynchronizationCallName(row, contracts) == "M:Unity.Jobs.JobHandle.Complete");
            Assert.AreEqual("value", complete[3]);
            Assert.AreNotEqual("p0", complete[4], "Complete on an in parameter must receive a defensive copy.");
            Assert.IsTrue(callee.Any(row => row[0] == "=" && row[1] == complete[4] && row[2] == "p0"));
            var caller = SynchronizationRows(nameof(SyncFlowCallRefArguments));
            contracts = SynchronizationContracts(caller);
            var call = caller.Single(row => row[0] == "C" && SynchronizationCallName(row, contracts).Contains(".SyncFlowRefArguments("));
            CollectionAssert.AreEqual(new[] { "0:in", "1:ref", "2:out" },
                call.Skip(5).Select(argument => string.Join(":", argument.Split(':').Take(2))).ToArray());
        }

        [Test]
        public void SynchronizationNamedArgumentsKeepEvaluationOrderAndParameterOrdinals() {
            var rows = SynchronizationRows(nameof(SyncFlowNamedArguments));
            var contracts = SynchronizationContracts(rows);
            var calls = rows.Where(row => row[0] == "C").ToArray();
            Assert.AreEqual(3, calls.Length);
            StringAssert.Contains(".SyncFlowReturn(", SynchronizationCallName(calls[0], contracts));
            StringAssert.Contains("SystemContext.get_dependsOn", SynchronizationCallName(calls[1], contracts));
            StringAssert.Contains(".SyncFlowPair(", SynchronizationCallName(calls[2], contracts));
            CollectionAssert.AreEqual(new[] { "0:value:" + calls[1][1], "1:value:" + calls[0][1] }, calls[2].Skip(5).ToArray());
        }

        [Test]
        public void SynchronizationGenericHelpersRetainPortableTypesAndReturnValues() {
            var rows = SynchronizationRows(nameof(SyncFlowIdentity));
            var parameter = rows.Single(row => row[0] == "P");
            var method = rows.Single(row => row[0] == "M");
            Assert.AreEqual("T", parameter[2]);
            Assert.AreEqual("T", method[1]);
            Assert.AreEqual(parameter[4], method[3], "Both portable types must refer to the same method-owned T.");
            var returned = rows.Single(row => row[0] == "R")[1];
            Assert.IsTrue(rows.Any(row => row[0] == "=" && row[1] == returned && row[2] == "p0"));
            Assert.AreEqual("T", rows.Single(row => row[0] == "V" && row[1] == returned)[2]);
            var caller = SynchronizationRows(nameof(SyncFlowGenericHandle));
            var contracts = SynchronizationContracts(caller);
            var call = caller.Single(row => row[0] == "C" && SynchronizationCallName(row, contracts).Contains(".SyncFlowIdentity``"));
            Assert.AreEqual("H", caller.Single(row => row[0] == "V" && row[1] == call[1])[2]);
            Assert.IsTrue(contracts[call[2]][5].StartsWith("n", StringComparison.Ordinal), "The closed call must bind T to a named JobHandle type.");
        }

        [Test]
        public void SynchronizationRescheduleRemainsBeforeOldHandleCompletion() {
            var rows = SynchronizationRows(nameof(SyncFlowReschedule));
            var contracts = SynchronizationContracts(rows);
            var calls = rows.Where(row => row[0] == "C").ToArray();
            var schedule = Array.FindIndex(calls, row => contracts[row[2]].Any(token => token.StartsWith("!scheduled-job=", StringComparison.Ordinal)));
            var replace = Array.FindIndex(calls, row => SynchronizationCallName(row, contracts).Contains("SystemContext.SetDependency("));
            var complete = Array.FindIndex(calls, row => SynchronizationCallName(row, contracts) == "M:Unity.Jobs.JobHandle.Complete");
            var access = Array.FindIndex(calls, row => contracts[row[2]].Any(token => token.StartsWith("!safety=", StringComparison.Ordinal)));
            Assert.GreaterOrEqual(schedule, 0);
            Assert.Greater(replace, schedule);
            Assert.Greater(complete, replace);
            Assert.Greater(access, complete);
        }

        [Test]
        public void SynchronizationOmittedCallsAlsoOmitTheirArguments() {
            var rows = SynchronizationRows(nameof(SyncFlowOmitted));
            Assert.IsFalse(rows.Any(row => row[0] == "C"));
        }

        [TestCase(nameof(SyncCaptureReadonlyReceiver), "FlowCaptureStorage")]
        [TestCase(nameof(SyncCaptureRefChoice), "FlowCaptureStorage")]
        public void SynchronizationUnsupportedFlowIsExplicit(string name, string gap) {
            Assert.IsTrue(SynchronizationRows(name).Any(row => row[0] == "G" && row[1] == gap));
        }
    }
}
