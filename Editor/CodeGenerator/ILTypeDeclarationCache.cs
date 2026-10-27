namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Entries are immutable after publication: workers copy dictionaries, not
    // mutable ownership of a cache shared with the Editor thread.
    internal sealed class ILTypeDeclarationCache {
        [Serializable] internal sealed class Entry {
            public string name, mvid, fingerprint;
            public int format;
        }
        // Bump when TypeDeclaration's serialized fingerprint representation changes.
        private const int Format = 2;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<Type, string> current = new Dictionary<Type, string>();

        internal void Import(IEnumerable<Entry> entries) {
            if (entries == null) { return; }
            foreach (var entry in entries) {
                if (entry == null || entry.format != Format || string.IsNullOrEmpty(entry.name) == true ||
                    Guid.TryParse(entry.mvid, out _) == false || entry.fingerprint == null || entry.fingerprint.Length != 64 ||
                    entry.fingerprint.Any(character => Uri.IsHexDigit(character) == false) == true) { continue; }
                this.entries[entry.name] = entry;
            }
        }

        internal Entry[] Export() => this.entries.Values.OrderBy(entry => entry.name, StringComparer.Ordinal).ToArray();

        internal string Get(Type type) {
            if (this.current.TryGetValue(type, out var value) == true) { return value; }
            var name = type.AssemblyQualifiedName ?? throw new NotSupportedException("Type has no persistent identity: " + type);
            var mvid = type.Module.ModuleVersionId.ToString("D");
            // These declarations include executable closure hashes. A helper in
            // another assembly can change while this type's own MVID stays equal.
            var executable = typeof(Attribute).IsAssignableFrom(type) == true || typeof(IRefOp).IsAssignableFrom(type) == true;
            if (executable == false && this.entries.TryGetValue(name, out var previous) == true && previous.mvid == mvid) {
                value = previous.fingerprint;
            } else {
                value = ILContentFingerprint.TypeDeclaration(type);
                if (value.StartsWith("opaque:", StringComparison.Ordinal) == true) {
                    throw new NotSupportedException("Type declaration metadata unavailable: " + type);
                }
                this.entries[name] = new Entry { name = name, mvid = mvid, fingerprint = value, format = Format };
            }
            this.current.Add(type, value);
            return value;
        }
    }
}
