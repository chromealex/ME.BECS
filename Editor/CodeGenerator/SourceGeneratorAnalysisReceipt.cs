namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ME.BECS.CodeGeneration;

    // Analysis freshness is project-local cache state, not compiler input. The
    // receipt only becomes usable together with matching compiled input hashes.
    internal static class SourceGeneratorAnalysisReceipt {
        private const string Schema = "ME.BECS.AnalysisReceipt.v1";
        internal const string ContentMetadataKey = "ME.BECS.InputContentHash.v1";
        private static readonly string[] Consumers = { "ME.BECS.Gen.Runtime", "ME.BECS.Gen.Editor" };
        private static string PathName => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
            "../Library/ME.BECS.SourceGenerator/AnalysisReceipt.v1.txt"));

        internal static void Invalidate() {
            // An interrupted/partial export must not resurrect a previous receipt
            // after an Editor restart, even if its old assembly metadata matches.
            if (File.Exists(PathName)) File.Delete(PathName);
        }

        internal static void Commit(string fingerprint, string compilerSnapshot, string runtimeContent, string editorContent) {
            // Record the data actually produced by analysis, not whatever another
            // process may have written to the input files in the meantime.
            var content = Create(fingerprint, compilerSnapshot,
                SourceGeneratorNames.Hash(runtimeContent), SourceGeneratorNames.Hash(editorContent));
            var path = PathName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        internal static string Create(string fingerprint, string compilerSnapshot, string runtimeHash, string editorHash) {
            var values = new[] { fingerprint, compilerSnapshot, runtimeHash, editorHash };
            if (values.Any(value => !IsHash(value))) throw new ArgumentException("Invalid source input analysis receipt hash.");
            var payload = Schema + "\n" + string.Join("\n", values) + "\n";
            return payload + SourceGeneratorNames.Hash(payload) + "\n";
        }

        private static bool IsHash(string value) => value != null && value.Length == 64 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');

        private static bool TryParse(string text, out string[] fields) {
            fields = (text ?? "").Split('\n');
            return fields.Length == 7 && fields[0] == Schema && fields[6] == "" &&
                fields.Skip(1).Take(5).All(IsHash) &&
                fields[5] == SourceGeneratorNames.Hash(string.Join("\n", fields.Take(5)) + "\n");
        }

        private static KeyValuePair<string, string>[] CurrentInputHashes() => new[] { false, true }
            .Select(editor => new KeyValuePair<string, string>(Consumers[editor ? 1 : 0],
                SourceGeneratorNames.Hash(File.ReadAllText(SourceGeneratorInputTransport.InputPath(editor))))).ToArray();

        private static string Read() => File.Exists(PathName) ? File.ReadAllText(PathName) : "";

        internal static bool IsCurrent(string fingerprint) {
            try { return ValidateAnalysis(Read(), fingerprint, CurrentInputHashes(), out _, out _); }
            catch (Exception) { return false; }
        }

        internal static bool TryValidateCompiled(string fingerprint, Assembly[] assemblies, out string compilerSnapshot, out string reason) {
            var compiled = assemblies.Select(assembly => new KeyValuePair<string, string[]>(assembly.GetName().Name,
                assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Where(attribute => attribute.Key == ContentMetadataKey).Select(attribute => attribute.Value).ToArray())).ToArray();
            return ValidateCompiled(Read(), fingerprint, CurrentInputHashes(), compiled, out compilerSnapshot, out reason);
        }

        internal static bool ValidateAnalysis(string text, string fingerprint, KeyValuePair<string, string>[] inputs,
            out string compilerSnapshot, out string reason) {
            compilerSnapshot = null;
            reason = "Current code/assets have no completed source input analysis. Regenerate inputs in the Editor.";
            if (!TryParse(text, out var fields) || string.IsNullOrEmpty(fingerprint) || fields[1] != fingerprint) return false;
            for (var index = 0; index < Consumers.Length; ++index) {
                var matches = inputs.Where(pair => pair.Key == Consumers[index]).ToArray();
                if (matches.Length != 1 || matches[0].Value != fields[index + 3]) {
                    reason = Consumers[index] + " input data changed after analysis. Regenerate inputs in the Editor.";
                    return false;
                }
            }
            compilerSnapshot = fields[2];
            reason = "";
            return true;
        }

        internal static bool ValidateCompiled(string text, string fingerprint, KeyValuePair<string, string>[] inputs,
            KeyValuePair<string, string[]>[] compiled, out string compilerSnapshot, out string reason) {
            if (!ValidateAnalysis(text, fingerprint, inputs, out compilerSnapshot, out reason)) return false;
            foreach (var consumer in Consumers) {
                var expected = inputs.Single(pair => pair.Key == consumer).Value;
                var matches = compiled.Where(pair => pair.Key == consumer).ToArray();
                if (matches.Length != 1 || matches[0].Value == null || matches[0].Value.Length != 1 || matches[0].Value[0] != expected) {
                    reason = consumer + " has not compiled the analyzed input data. Wait for successful Unity compilation.";
                    return false;
                }
            }
            return true;
        }
    }
}
