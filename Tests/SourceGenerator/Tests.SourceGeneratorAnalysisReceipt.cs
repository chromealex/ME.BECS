using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorAnalysisReceipt {
        private static Type Receipt => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorAnalysisReceipt", true);
        private static string Hash(char value) => new string(value, 64);
        private static string Create(char code = 'A', char assets = 'B', char runtime = 'C', char editor = 'D') =>
            (string)Receipt.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Hash(code), Hash(assets), Hash(runtime), Hash(editor) });
        private static KeyValuePair<string, string>[] Inputs(char runtime = 'C', char editor = 'D') => new[] {
            new KeyValuePair<string, string>("runtime", Hash(runtime)),
            new KeyValuePair<string, string>("editor", Hash(editor)),
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
                var assembly = Tests_SourceGeneratorInputCatalog.Owner(editor);
                CollectionAssert.AreEqual(new[] { compilerSnapshot }, assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                    .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.GraphInputSnapshot.v1")
                    .Select(attribute => attribute.Value).ToArray());
            }
        }

        [Test, Timeout(600000)]
        public void BuildMachinePreflightCanReconstructAnalysisWithoutWritingProjectInputs() {
            var directory = System.IO.Path.Combine(UnityEngine.Application.dataPath, "ME.BECS.SourceInputs");
            // Unity owns import metadata; publication owns all other files, including both root snapshots.
            var paths = System.IO.Directory.GetFiles(directory, "*", System.IO.SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) == false)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var contents = paths.Select(path => Convert.ToBase64String(System.IO.File.ReadAllBytes(path))).ToArray();
            var times = paths.Select(System.IO.File.GetLastWriteTimeUtc).ToArray();
            var method = Receipt.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputManifest", true)
                .GetMethod("TryAnalyzePublishedInputs", BindingFlags.Static | BindingFlags.NonPublic, null,
                    new[] { typeof(string), typeof(string).MakeByRefType() }, null);
            var readiness = Receipt.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputRefresh", true)
                .GetMethod("TryValidateReady", BindingFlags.Static | BindingFlags.NonPublic);
            var cacheRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ME.BECS.SourceGenerator.Tests", Guid.NewGuid().ToString("N"));
            var cachePath = System.IO.Path.Combine(cacheRoot, "IncrementalIL.v2.json");
            var analysisSession = Receipt.Assembly.GetType("ME.BECS.Editor.ILAnalysisSession", true);
            var fingerprintField = analysisSession.GetField("retainedFingerprint", BindingFlags.Static | BindingFlags.NonPublic);
            var valuesField = analysisSession.GetField("retainedValues", BindingFlags.Static | BindingFlags.NonPublic);
            var previousFingerprint = fingerprintField.GetValue(null);
            var previousValues = valuesField.GetValue(null);
            var persistentAnalysis = Receipt.Assembly.GetType("ME.BECS.Editor.ILPersistentAnalysis", true);
            var waitForCacheSave = persistentAnalysis.GetMethod("WaitForPendingSave", BindingFlags.Static | BindingFlags.NonPublic);
            try {
                Assert.IsFalse(System.IO.Directory.Exists(cacheRoot), "The cold pass must start with an empty incremental IL cache.");
                foreach (var pass in new[] { "cold", "warm" }) {
                    fingerprintField.SetValue(null, null);
                    valuesField.SetValue(null, null);
                    var args = new object[] { cachePath, null };
                    var summaries = new global::System.Collections.Generic.List<string>();
                    UnityEngine.Application.LogCallback onLog = (condition, stackTrace, type) => {
                        if (condition.StartsWith("[ME.BECS] Incremental IL summaries:", StringComparison.Ordinal)) summaries.Add(condition);
                    };
                    UnityEngine.Application.logMessageReceived += onLog;
                    try {
                        Assert.IsTrue((bool)method.Invoke(null, args), pass + " analysis: " + (string)args[1]);
                        var readyArgs = new object[] { null };
                        Assert.IsTrue((bool)readiness.Invoke(null, readyArgs), pass + " compiled input readiness: " + (string)readyArgs[0]);
                    } finally {
                        UnityEngine.Application.logMessageReceived -= onLog;
                    }
                    if (pass == "cold") {
                        waitForCacheSave.Invoke(null, null);
                        Assert.IsTrue(System.IO.File.Exists(cachePath), "The cold pass must persist summaries for the warm pass.");
                    } else {
                        var warmSummary = string.Join("\n", summaries);
                        var reused = System.Text.RegularExpressions.Regex.Match(warmSummary, @"discovery: reused=(\d+)");
                        Assert.IsTrue(reused.Success && int.Parse(reused.Groups[1].Value) > 0,
                            "The warm pass must reuse discovery summaries from the persistent cache. " + warmSummary);
                    }
                    CollectionAssert.AreEqual(paths, System.IO.Directory.GetFiles(directory, "*", System.IO.SearchOption.AllDirectories)
                        .Where(path => path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) == false)
                        .OrderBy(path => path, StringComparer.Ordinal).ToArray());
                    CollectionAssert.AreEqual(contents, paths.Select(path => Convert.ToBase64String(System.IO.File.ReadAllBytes(path))).ToArray());
                    CollectionAssert.AreEqual(times, paths.Select(System.IO.File.GetLastWriteTimeUtc).ToArray());
                }
            } finally {
                try {
                    waitForCacheSave.Invoke(null, null);
                } finally {
                    fingerprintField.SetValue(null, previousFingerprint);
                    valuesField.SetValue(null, previousValues);
                    if (System.IO.Directory.Exists(cacheRoot)) System.IO.Directory.Delete(cacheRoot, true);
                }
            }
        }

        [Test]
        public void RuntimeDiscoveryResolvesAssemblyForModuleGlobalMethodMetadata() {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("ME.BECS.GlobalMethod." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("GlobalMethodModule");
            var global = module.DefineGlobalMethod("GlobalUsageTarget", MethodAttributes.Public | MethodAttributes.Static,
                typeof(void), Type.EmptyTypes);
            global.GetILGenerator().Emit(OpCodes.Ret);
            module.CreateGlobalFunctions();

            var method = module.GetMethod("GlobalUsageTarget");
            Assert.IsNotNull(method);
            var generator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var lookup = generator.GetNestedType("UsedObjectsLookup", BindingFlags.NonPublic);
            Assert.IsNotNull(lookup);
            var resolveAssembly = lookup.GetMethod("GetMethodAssembly", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(resolveAssembly);
            var resolvedAssembly = (Assembly)resolveAssembly.Invoke(null, new object[] { method });
            Assert.AreEqual(assembly.FullName, resolvedAssembly.FullName);

            // Mono may expose emitted globals through <Module>. DynamicMethod
            // explicitly covers module-associated metadata without a declaring Type.
            var withoutDeclaringType = new DynamicMethod("GlobalUsageWithoutDeclaringType", typeof(void), Type.EmptyTypes, module);
            withoutDeclaringType.GetILGenerator().Emit(OpCodes.Ret);
            Assert.IsNull(withoutDeclaringType.DeclaringType);
            var fallbackAssembly = (Assembly)resolveAssembly.Invoke(null, new object[] { withoutDeclaringType });
            Assert.AreEqual(assembly.FullName, fallbackAssembly.FullName);
        }
    }
}
