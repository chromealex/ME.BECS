using ME.BECS.Extensions.GraphProcessor;
using UnityEngine;

namespace ME.BECS.Editor.Systems {
    
    public class SystemsGraphPostProcessor : UnityEditor.AssetPostprocessor {
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            foreach (string currentPath in importedAssets) {
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseGraph>(currentPath);
                if (graph != null) {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
                    var maxId = 1000;
                    foreach (var guid in guids) {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var importer = UnityEditor.AssetImporter.GetAtPath(path);
                        if (int.TryParse(importer.userData, out var id) == true) {
                            maxId = Mathf.Max(maxId, id);
                        }
                    }

                    {
                        var importer = UnityEditor.AssetImporter.GetAtPath(currentPath);
                        if (int.TryParse(importer.userData, out var id) == false) {
                            importer.userData = (++maxId).ToString();
                            importer.SaveAndReimport();
                        }

                        if (id != graph.id && graph.builtInGraph == false) {
                            graph.id = id;
                            UnityEditor.EditorUtility.SetDirty(graph);
                        }
                    }
                }
            }
        }
        
    }
    
}