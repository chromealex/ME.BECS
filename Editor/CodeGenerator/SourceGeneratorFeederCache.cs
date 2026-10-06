namespace ME.BECS.Editor {
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using scg = System.Collections.Generic;
    using Names = ME.BECS.CodeGeneration.SourceGeneratorNames;

    // Cache data records, not generated C# or reflection objects. The assembly
    // fingerprint covers transitive helpers as well as the job's owning assembly.
    // A miss/corrupt cache always falls back to analysis, never an empty plan.
    internal static class SourceGeneratorFeederCache {
        [Serializable]
        private sealed class Record {
            public string key;
            public string code;
            public string text;
            public string[] references;
            public string checksum;
        }

        internal static string SelectionKey(CustomCodeGenerator feeder) {
            var key = new StringBuilder("ME.BECS.CompiledFeeder.v1\n").Append(feeder.GetType().AssemblyQualifiedName)
                .Append('\n').Append(feeder.editorAssembly ? "editor\n" : "runtime\n");
            void Types(string kind, scg::IEnumerable<Type> types) {
                key.Append(kind).Append('\n');
                if (types == null) { key.Append("<null>\n"); return; }
                // Preserve selection order: it may determine registration IDs.
                foreach (var type in types) key.Append(type.AssemblyQualifiedName).Append('\n');
            }
            Types("systems", feeder.systems);
            Types("jobs", feeder.jobTypes);
            Types("entities", feeder.entityTypes);
            Types("aspects", feeder.aspects);
            if (feeder.asms != null) foreach (var assembly in feeder.asms) {
                if (SourceGeneratorPublicationBridges.IsBridge(assembly.name)) continue;
                key.Append("assembly\t").Append(assembly.name).Append('\t').Append(assembly.isEditor).Append('\n');
                foreach (var platform in assembly.includePlatforms ?? Array.Empty<string>()) key.Append("P\t").Append(platform).Append('\n');
                foreach (var reference in assembly.references ?? Array.Empty<string>()) key.Append("R\t").Append(reference).Append('\n');
            }
            return Names.Hash(key.ToString());
        }

        internal static void Append(CustomCodeGenerator feeder, StringBuilder manifest) {
            feeder.preparedInputReferences = null;
            var code = ILAnalysisSession.CodeFingerprint;
            if (!feeder.CacheCompiledInputs || string.IsNullOrEmpty(code)) {
                feeder.AppendSourceGeneratorInputs(manifest);
                return;
            }
            var key = SelectionKey(feeder);
            // One slot per feeder/profile; a new selection replaces the previous
            // one rather than leaving an unbounded history of code/graph revisions.
            var slot = Names.Hash(feeder.GetType().AssemblyQualifiedName + "\n" + feeder.editorAssembly);
            var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/ME.BECS.SourceGenerator/CompiledInputs", slot + ".json"));
            if (!ILAnalysisSession.Rebuild && TryRead(path, key, code, out var cached, out var references)) {
                // New publications resolve references per owner. Only reuse a
                // legacy cached list when one exists; explicit callers can compute it lazily.
                feeder.preparedInputReferences = references.Length != 0 ? references : null;
                manifest.Append(cached);
                CodeGeneratorTimings.Subject("Reused compiled inputs: " + feeder.GetType().Name);
                return;
            }

            var text = new StringBuilder();
            feeder.AppendSourceGeneratorInputs(text);
            var record = new Record {
                key = key, code = code, text = text.ToString(),
                references = Array.Empty<string>(),
            };
            record.checksum = Checksum(record);
            manifest.Append(record.text);
            TryWrite(path, record);
        }

        private static string Checksum(Record record) => Names.Hash(record.key + "\n" + record.code + "\n" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(record.text)) + "\n" + string.Join("\n", record.references));

        private static bool TryRead(string path, string key, string code, out string text, out Type[] references) {
            text = null;
            references = null;
            try {
                if (!File.Exists(path)) return false;
                var record = UnityEngine.JsonUtility.FromJson<Record>(File.ReadAllText(path));
                if (record == null || record.key != key || record.code != code || record.text == null || record.references == null ||
                    record.checksum != Checksum(record) || record.references.Any(string.IsNullOrEmpty)) return false;
                var resolved = record.references.Select(name => Type.GetType(name, false)).ToArray();
                if (resolved.Any(type => type == null)) return false;
                text = record.text;
                references = resolved;
                return true;
            } catch (Exception exception) when (!(exception is OperationCanceledException)) {
                return false; // A disposable Library cache must never block export.
            }
        }

        private static void TryWrite(string path, Record record) {
            var temporary = path + ".tmp";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, UnityEngine.JsonUtility.ToJson(record), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            } catch (IOException) { }
              catch (UnauthorizedAccessException) { }
              catch (NotSupportedException) { }
            finally {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
