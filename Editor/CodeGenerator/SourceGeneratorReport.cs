namespace ME.BECS.Editor {

    using System;
    using System.IO;

    internal static class SourceGeneratorReport {
        internal static void Publish(string name, string totals, string details) {
            // Unity truncates long Console messages. Store the full report outside Assets;
            // writing diagnostics must not trigger asset import or another compilation.
            var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ME.BECS.SourceGenerator"));
            try {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, name + ".txt");
                File.WriteAllText(path, totals + Environment.NewLine + details);
                UnityEngine.Debug.Log("[ME.BECS] " + totals + "\nFull report: " + path);
            } catch (Exception exception) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Cannot save comparison report: " + exception.Message);
                UnityEngine.Debug.Log("[ME.BECS] " + totals);
                // Preserve all details even when the diagnostic directory is not writable.
                const int chunkSize = 8000;
                for (var offset = 0; offset < details.Length; offset += chunkSize)
                    UnityEngine.Debug.Log(details.Substring(offset, Math.Min(chunkSize, details.Length - offset)));
            }
        }
    }
}
