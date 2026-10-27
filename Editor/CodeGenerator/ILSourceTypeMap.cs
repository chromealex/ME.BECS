namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Loaded compiler metadata, not MonoScript's primary type. Private/nested
    // helpers and every partial declaration participate in the same map.
    internal static class ILSourceTypeMap {
        internal const string MetadataKey = "ME.BECS.SourceTypeMap.v1";

        internal static Dictionary<string, string[]> Parse(string data) {
            var rows = data?.Split('\n');
            if (rows == null || rows.Length == 0 || rows[0] != "v1") {
                throw new FormatException("Invalid compiled source type map.");
            }
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var row in rows.Skip(1)) {
                if (row.Length == 0) { continue; }
                var fields = row.Split('\t');
                if (fields.Length != 2 || fields[0].Length == 0 || IsPortable(fields[1]) == false) {
                    throw new FormatException("Invalid compiled source type map row.");
                }
                if (result.TryGetValue(fields[0], out var files) == false) {
                    files = new HashSet<string>(StringComparer.Ordinal);
                    result.Add(fields[0], files);
                }
                files.Add(fields[1]);
            }
            return result.ToDictionary(pair => pair.Key, pair => pair.Value.OrderBy(path => path, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        }

        private static bool IsPortable(string path) =>
            (path.StartsWith("Assets/", StringComparison.Ordinal) == true || path.StartsWith("Packages/", StringComparison.Ordinal) == true ||
             path.StartsWith("Library/PackageCache/", StringComparison.Ordinal) == true) &&
            path.Contains("\\") == false && path.Split('/').Any(segment => segment == ".." || segment == "." || segment.Length == 0) == false;

        private static Dictionary<string, string[]> Read(Assembly assembly) => ILAnalysisSession.Get((typeof(ILSourceTypeMap), assembly), () => {
            var values = ILAnalysisSession.ReadMetadata(() => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == MetadataKey).ToArray());
            if (values.Length != 1) { return new Dictionary<string, string[]>(StringComparer.Ordinal); }
            return Parse(values[0].Value);
        });

        internal static string[] Files(Type type) {
            if (type.HasElementType == true) { type = type.GetElementType(); }
            if (type.IsGenericType == true) { type = type.GetGenericTypeDefinition(); }
            if (type.FullName == null) { return Array.Empty<string>(); }
            return Read(type.Assembly).TryGetValue(type.FullName, out var files) == true ? files : Array.Empty<string>();
        }
    }
}
