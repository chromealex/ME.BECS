namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;

    // Diagnostics read the compiler owner, not a hard-coded aggregate assembly.
    /// <summary>
    /// Provides view selection catalog for BECS source-generator publication.
    /// </summary>
    public static class SourceGeneratorViewSelectionCatalog {
        /// <summary>
        /// Returns assembly.
        /// </summary>
        public static Assembly GetAssembly(bool editor) {
            var profile = editor ? "editor" : "runtime";
            var root = "ME.BECS.SourceGenerated.ViewSelectionProfile_" + (editor ? "Editor" : "Runtime");
            var owners = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetType(root, false) != null &&
                assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().Any(attribute =>
                    attribute.Key == CodeGeneration.SourceGeneratorViewSelectionFragmentFormat.MetadataKey &&
                    attribute.Value.StartsWith(profile + "\t" + assembly.GetName().Name + "\t", StringComparison.Ordinal))).ToArray();
            if (owners.Length != 1) throw new InvalidOperationException("Expected one compiled " + profile + " Views dependency owner; found " + owners.Length + ". Wait for input export/compilation.");
            return owners[0];
        }
    }
}
