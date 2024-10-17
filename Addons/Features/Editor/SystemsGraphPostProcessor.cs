using ME.BECS.Extensions.GraphProcessor;
using UnityEngine;

namespace ME.BECS.Editor.Systems {
    
    public class SystemsGraphPostProcessor : UnityEditor.AssetPostprocessor {
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            foreach (string currentPath in importedAssets) {
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseGraph>(currentPath);
                if (graph != null) {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
                    var builtInMaxId = 1;
                    var maxId = 1000;
                    foreach (var guid in guids) {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var curGraph = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseGraph>(path);
                        var importer = UnityEditor.AssetImporter.GetAtPath(path);
                        if (int.TryParse(importer.userData, out var id) == true) {
                            if (curGraph.builtInGraph == false) {
                                maxId = Mathf.Max(maxId, id);
                            } else {
                                builtInMaxId = Mathf.Max(builtInMaxId, id);
                            }
                        }
                    }

                    if (graph.builtInGraph == false) {
                        var importer = UnityEditor.AssetImporter.GetAtPath(currentPath);
                        if (int.TryParse(importer.userData, out var id) == false || graph.id == 0) {
                            if (graph.builtInGraph == false) {
                                id = ++maxId;
                            } else {
                                id = ++builtInMaxId;
                            }
                            importer.userData = id.ToString();
                            importer.SaveAndReimport();
                        }

                        if (id != graph.id) {
                            graph.id = id;
                            UnityEditor.EditorUtility.SetDirty(graph);
                        }
                    }
                }
            }
        }
        
    }
    
}