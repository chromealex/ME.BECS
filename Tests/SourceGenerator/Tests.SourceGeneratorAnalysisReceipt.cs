using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorAnalysisReceipt {
        private static Type Receipt => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorAnalysisReceipt", true);
        private static string Hash(char value) => new string(value, 64);
        private static string Create(char code = 'A', char assets = 'B', char runtime = 'C', char editor = 'D') =>
            (string)Receipt.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Hash(code), Hash(assets), Hash(runtime), Hash(editor) });
        private static KeyValuePair<string, string>[] Inputs(char runtime = 'C', char editor = 'D') => new[] {
            new KeyValuePair<string, string>("ME.BECS.Gen.Runtime", Hash(runtime)),
            new KeyValuePair<string, string>("ME.BECS.Gen.Editor", Hash(editor)),
        };
        private static KeyValuePair<string, string[]>[] Compiled(char runtime = 'C', char editor = 'D') =>
            Inputs(runtime, editor).Select(pair => new KeyValuePair<string, string[]>(pair.Key, new[] { pair.Value })).ToArray();

        private static bool Validate(string text, char code, KeyValuePair<string, string>[] inputs,
            KeyValuePair<string, string[]>[] compiled, out string snapshot, out string reason) {
            var args = new object[] { text, Hash(code), inputs, compiled, null, null };
            var result = (bool)Receipt.GetMethod("ValidateCompiled", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            snapshot = (string)args[4]; reason = (string)args[5];
            return result;
        }

        [Test]
        public void NonSemanticCodeChangeNeedsNewAnalysisButNotNewCompiledInput() {
            var old = Create();
            Assert.IsTrue(Validate(old, 'A', Inputs(), Compiled(), out var before, out var reason), reason);
            Assert.IsFalse(Validate(old, 'E', Inputs(), Compiled(), out _, out _), "A previous analysis cannot authorize new code.");
            var current = Create(code: 'E');
            Assert.AreNotEqual(old, current, "The project-local receipt must advance after successful analysis.");
            Assert.IsTrue(Validate(current, 'E', Inputs(), Compiled(), out var after, out reason), reason);
            Assert.AreEqual(before, after, "Unchanged semantic inputs retain valid compiled evidence after code re-analysis.");
        }

        [Test]
        public void ChangedSemanticInputNeedsActualCompilationAfterAnalysis() {
            var text = Create(code: 'E', runtime: 'F');
            Assert.IsFalse(Validate(text, 'E', Inputs(runtime: 'F'), Compiled(), out _, out var reason));
            StringAssert.Contains("Wait for successful Unity compilation", reason);
            Assert.IsTrue(Validate(text, 'E', Inputs(runtime: 'F'), Compiled(runtime: 'F'), out _, out reason), reason);
        }

        [Test]
        public void ChangedOrPartiallyPublishedInputsCannotReusePreviousAnalysis() {
            Assert.IsFalse(Validate(Create(), 'A', Inputs(runtime: 'F'), Compiled(runtime: 'F'), out _, out _));
            Assert.IsFalse(Validate(Create(), 'A', Inputs(editor: 'F'), Compiled(editor: 'F'), out _, out _));
            foreach (var inputs in new[] { Array.Empty<KeyValuePair<string, string>>(), Inputs().Take(1).ToArray(), Inputs().Concat(Inputs()).ToArray() })
                Assert.IsFalse(Validate(Create(), 'A', inputs, Compiled(), out _, out _));
        }

        [Test]
        public void CompletedAnalysisDoesNotRequireCompiledConsumersUntilTheReadinessGate() {
            var args = new object[] { Create(), Hash('A'), Inputs(), null, null };
            Assert.IsTrue((bool)Receipt.GetMethod("ValidateAnalysis", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args));
            Assert.AreEqual(Hash('B'), args[3]);
            Assert.IsFalse(Validate(Create(), 'A', Inputs(), Array.Empty<KeyValuePair<string, string[]>>(), out _, out _));
        }

        [Test]
        public void MissingStaleOrAmbiguousCompiledConsumersAreRejected() {
            var valid = Compiled();
            foreach (var compiled in new[] {
                Array.Empty<KeyValuePair<string, string[]>>(), valid.Take(1).ToArray(), valid.Concat(valid).ToArray(), Compiled(editor: 'E'),
                new[] { valid[0], new KeyValuePair<string, string[]>(valid[1].Key, null) },
                new[] { valid[0], new KeyValuePair<string, string[]>(valid[1].Key, Array.Empty<string>()) },
                new[] { valid[0], new KeyValuePair<string, string[]>(valid[1].Key, new[] { Hash('D'), Hash('D') }) },
            }) Assert.IsFalse(Validate(Create(), 'A', Inputs(), compiled, out _, out _));
            Assert.IsTrue(Validate(Create(), 'A', Inputs().Reverse().ToArray(), valid.Reverse().ToArray(), out _, out _));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("incomplete")]
        public void MissingInterruptedOrMalformedReceiptsRequireAnalysis(string text) {
            Assert.IsFalse(Validate(text, 'A', Inputs(), Compiled(), out _, out var reason));
            StringAssert.Contains("no completed source input analysis", reason);
        }

        [Test]
        public void ReceiptChecksumRejectsEditedFingerprintOrInputHashes() {
            var text = Create();
            foreach (var field in new[] { 'A', 'B', 'C', 'D' })
                Assert.IsFalse(Validate(text.Replace(Hash(field), Hash('F')), 'A', Inputs(), Compiled(), out _, out _));
            Assert.IsFalse(Validate(text.TrimEnd('\n'), 'A', Inputs(), Compiled(), out _, out _));
            Assert.IsFalse(Validate(text + "extra\n", 'A', Inputs(), Compiled(), out _, out _));
        }

        [Test]
        public void ReceiptLivesInProjectLibraryAndCurrentInputsPassTheRealGate() {
            var path = (string)Receipt.GetProperty("PathName", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.AreEqual(System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "../Library/ME.BECS.SourceGenerator/AnalysisReceipt.v1.txt")), path);
            var snapshot = Receipt.Assembly.GetType("ME.BECS.Editor.SourceGeneratorGraphSnapshot", true);
            var current = (string)snapshot.GetMethod("GetCurrent").Invoke(null, null);
            var args = new object[] { current, null };
            Assert.IsTrue((bool)snapshot.GetMethod("IsCompiledCurrent").Invoke(null, args), (string)args[1]);
            var compilerSnapshot = (string)snapshot.GetMethod("GetCompilerSnapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            foreach (var editor in new[] { false, true }) {
                var assembly = Assembly.Load("ME.BECS.Gen." + (editor ? "Editor" : "Runtime"));
                CollectionAssert.AreEqual(new[] { compilerSnapshot }, assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                    .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.GraphInputSnapshot.v1")
                    .Select(attribute => attribute.Value).ToArray());
            }
        }

        [Test]
        public void BuildMachinePreflightCanReconstructAnalysisWithoutWritingProjectInputs() {
            var directory = System.IO.Path.Combine(UnityEngine.Application.dataPath, "ME.BECS.SourceInputs");
            var paths = System.IO.Directory.GetFiles(directory, "*.additionalfile", System.IO.SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var contents = paths.Select(System.IO.File.ReadAllText).ToArray();
            var times = paths.Select(System.IO.File.GetLastWriteTimeUtc).ToArray();
            var method = Receipt.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputManifest", true)
                .GetMethod("TryAnalyzePublishedInputs", BindingFlags.Static | BindingFlags.NonPublic);
            var args = new object[] { null };
            Assert.IsTrue((bool)method.Invoke(null, args), (string)args[0]);
            CollectionAssert.AreEqual(paths, System.IO.Directory.GetFiles(directory, "*.additionalfile", System.IO.SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray());
            CollectionAssert.AreEqual(contents, paths.Select(System.IO.File.ReadAllText).ToArray());
            CollectionAssert.AreEqual(times, paths.Select(System.IO.File.GetLastWriteTimeUtc).ToArray());
        }
    }
}
