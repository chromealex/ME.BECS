using System.Linq;

namespace ME.BECS.Editor.Systems {

    using scg = System.Collections.Generic;
    using ME.BECS.FeaturesGraph;
    
    // Retirement names and compatibility query only. Graph bodies are compiler-owned.
    /// <summary>
    /// Exports systems registration data for generated code.
    /// </summary>
    public class SystemsCodeGenerator : CustomCodeGenerator {

        /// <summary>
        /// Returns retired source files.
        /// </summary>
        public override scg::IEnumerable<string> GetRetiredSourceFiles() {
            if (this.editorAssembly) yield break;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph").OrderBy(value => value, System.StringComparer.Ordinal)) {
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (graph == null || graph.isInnerGraph) continue;
                var baseName = "Graph" + EditorUtils.GetCodeName(graph.name);
                foreach (var phase in new[] { "Initialize", "Awake", "Start", "Update", "Destroy", "DrawGizmos" })
                    yield return baseName + "." + phase;
            }
        }

        /// <summary>
        /// Returns systems count.
        /// </summary>
        public static int GetSystemsCount(SystemsGraph graph) {
            return SourceGeneratorInputManifest.GetSystemsCount(graph);
        }

    }

}
