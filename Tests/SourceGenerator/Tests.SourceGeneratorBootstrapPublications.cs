using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorBootstrapPublications {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorBootstrapFragmentFormat", true);
        private static object Call(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Hidden).GetValue(value);
        private static string Decode(string text) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(text));
        private static string Encode(string text) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
        private static string[] Rows(Assembly assembly) => assembly.BecsInputMetadata()
            .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Substring(item.Value.IndexOf('\t') + 1)).ToArray();
        internal static Assembly Owner(Assembly aggregate) {
            var row = Rows(aggregate).Single(value => value.StartsWith("bootstrap-registration-owner\t", StringComparison.Ordinal)).Split('\t');
            return Assembly.Load(Decode(row[3]));
        }
        internal static Type PhaseInputs(Assembly aggregate) => Owner(aggregate).GetType("ME.BECS.SourceGenerated.BootstrapPhaseInputs", true);
        private static object Document(string[] rows, bool editor) => ((Array)Call("Documents", rows, editor)).GetValue(0);
        private static object Profile(string[] rows, bool editor) => Call("Create", rows, editor);

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void ProjectOwnerPublishesExactTypeFreePlansWithoutTheAggregate(string profile) {
            var aggregate = Tests_SourceGeneratorInputCatalog.Owner(profile == "Editor");
            var rows = Rows(aggregate);
            var editor = profile == "Editor";
            var owner = Owner(aggregate);
            Assert.IsFalse(owner.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
            StringAssert.StartsWith("ME.BECS.SourceInputs.Bridge.", owner.GetName().Name);
            Assert.IsFalse(owner.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
            Assert.IsNull(owner.GetType("ME.BECS.SourceGenerated.JobBootstrapInputs", false), "The owner publishes phase data, not compatibility adapters.");
            var document = Document(rows, editor);
            var content = (string)Call("Serialize", document);
            var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
            var receipt = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { document, content });
            Assert.AreEqual(1, owner.BecsInputMetadata()
                .Count(item => item.Key == "ME.BECS.BootstrapFragment.v1" && item.Value == receipt));
            var root = owner.GetType("ME.BECS.SourceGenerated.BootstrapProfile_" + profile, true);
            var publish = root.GetMethod("Publish", Static);
            Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
            if (editor) Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
            else {
                Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                Assert.AreEqual(1, owner.GetCustomAttributes(false).Count(attribute => attribute.GetType().FullName == "UnityEngine.Scripting.AlwaysLinkAssemblyAttribute"));
            }
            var expectedCalls = new System.Collections.Generic.List<MethodInfo>();
            var expectedArguments = new System.Collections.Generic.List<object>();
            foreach (var selection in Field<Array>(Profile(rows, editor), "Selections")) {
                var kind = Field<string>(selection, "Kind");
                expectedCalls.Add(kind == "Network" ? Assembly.Load("ME.BECS.Network").GetType("ME.BECS.Network.BootstrapNetworkMethods", true).GetMethod("ExpectPlan") :
                    typeof(BootstrapRuntime).GetMethod("Expect" + kind + "Plan"));
                expectedArguments.Add(Field<string>(selection, "Plan"));
                expectedArguments.AddRange(Field<int[]>(selection, "Counts").Cast<object>());
                if (kind != "Graph") expectedArguments.Add(editor ? 1 : 0);
            }
            expectedCalls.Add(PhaseInputs(aggregate).GetMethod("Publish"));
            CollectionAssert.AreEqual(expectedCalls, Tests_SourceGeneratorAotPublications.Calls(publish));
            var arguments = new System.Collections.Generic.List<object>();
            foreach (var instruction in ME.BECS.Mono.Reflection.Disassembler.GetInstructions(publish)) {
                if (instruction.OpCode == OpCodes.Ldstr) arguments.Add(instruction.Operand);
                else if (instruction.OpCode == OpCodes.Ldc_I4 || instruction.OpCode == OpCodes.Ldc_I4_S) arguments.Add(Convert.ToInt32(instruction.Operand));
                else if (instruction.OpCode.Value >= OpCodes.Ldc_I4_0.Value && instruction.OpCode.Value <= OpCodes.Ldc_I4_8.Value) arguments.Add(instruction.OpCode.Value - OpCodes.Ldc_I4_0.Value);
            }
            CollectionAssert.AreEqual(expectedArguments, arguments, "Do not change plan identities/counts while moving the publisher.");
            var payload = (string)Call("EntryValue", Profile(rows, editor));
            StringAssert.DoesNotContain("Version=", string.Join("\n", payload.Split('|').Select(Decode)));
            Tests_SourceGeneratorBootstrapPhases.AssertFeederSequence(aggregate);
        }

        [Test]
        public void GraphFirstPassLivesWithRuntimeCompositionAndKeepsItsDelegate() {
            var aggregate = Tests_SourceGeneratorInputCatalog.Owner(false);
            var root = Owner(aggregate).GetType("ME.BECS.SourceGenerated.BootstrapProfile_Runtime", true);
            var hook = root.GetMethod("PublishGraphPass", Static);
            Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen, hook.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
            CollectionAssert.AreEqual(new[] { typeof(CustomModules).GetMethod("RegisterFirstPass") }, Tests_SourceGeneratorAotPublications.Calls(hook));
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("RegisterInstalledGraphs") },
                ME.BECS.Mono.Reflection.Disassembler.GetInstructions(hook).Where(instruction => instruction.OpCode == OpCodes.Ldftn).Select(instruction => instruction.Operand).ToArray());
            Assert.IsNull(root.Assembly.GetType("ME.BECS.SourceGenerated.GraphInputs", false));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompositionRoundTripsAndUnrelatedSnapshotChangesDoNotRewriteIt(bool editor) {
            var rows = Tests_SourceGeneratorInputCatalog.Rows(editor);
            var document = Document(rows, editor);
            var content = (string)Call("Serialize", document);
            var args = new object[] { content, null };
            Assert.IsTrue((bool)Call("TryParse", args));
            Assert.AreEqual(content, Call("Serialize", args[1]));
            var changed = rows.Select(row => row.StartsWith("graph-input-snapshot\t", StringComparison.Ordinal) ? "graph-input-snapshot\t0\t" + Encode("changed-code") : row).Reverse().ToArray();
            Assert.AreEqual(content, Call("Serialize", Document(changed, editor)));
            document.GetType().GetField("Entries", Hidden).SetValue(document, Array.Empty<System.Collections.Generic.KeyValuePair<int, string>>());
            Assert.IsTrue((bool)Call("TryParse", Call("Serialize", document), null));
        }

        [TestCase("hash")]
        [TestCase("slot")]
        [TestCase("phase")]
        public void MalformedCompositionCannotPublishPartialStartup(string change) {
            var profile = Profile(Tests_SourceGeneratorInputCatalog.Rows(true), true);
            var parts = ((string)Call("EntryValue", profile)).Split('|').Select(Decode).ToArray();
            if (change == "hash") parts[1] = parts[1].Replace(Field<string>(Field<Array>(profile, "Selections").GetValue(0), "Plan"), "invalid");
            if (change == "slot") parts[4] = "2147483647";
            if (change == "phase") parts[2] = "call-arbitrary-method";
            Assert.IsFalse((bool)Call("TryEntry", string.Join("|", parts.Select(Encode)), true, null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyFrameworkCompositionDoesNotRequireOptionalAddons(bool editor) {
            var kinds = new[] { "System", "Type", "Entity", "Aspect", "Destroy", "Config", "JobInit", "JobSetup", "JobDebug", "Graph" }
                .Where(kind => kind != "Graph" || !editor).ToArray();
            var rows = kinds.Select(kind => kind.ToLowerInvariant() + "-publication-schema\t0\tdjE=")
                .Concat(new[] { "bootstrap-feeder\t0\t" + Encode("core") + "\tnone\tnone" }).ToArray();
            var profile = Profile(rows, editor);
            CollectionAssert.AreEqual(kinds, Field<Array>(profile, "Selections").Cast<object>().Select(selection => Field<string>(selection, "Kind")));
            Assert.IsEmpty(Field<int[]>(profile, "Jobs"));
            Assert.IsTrue((bool)Call("TryEntry", Call("EntryValue", profile), editor, null));
        }
    }
}
