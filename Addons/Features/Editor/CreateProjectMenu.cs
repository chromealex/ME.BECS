
namespace ME.BECS.Features.Editor {

    public abstract class CreateProjectDefaultModule {

        public abstract string CreateModule(string projectPath, string projectName);

    }
    
    public static class CreateProjectMenu {

        [UnityEditor.MenuItem("ME.BECS/Features Graph...")]
        public static void ShowFeaturesGraph() {
            
            ME.BECS.Editor.FeaturesGraph.FeaturesGraphEditorWindow.ShowWindow();
            
        }

        public class EndCreateProject : UnityEditor.ProjectWindowCallback.EndNameEditAction {

            public System.Action<string> onCreated;

            public override void Action(int instanceId, string pathName, string resourceFile) {
                this.onCreated.Invoke(pathName);
            }

        }

        [UnityEditor.MenuItem("Assets/Create/ME.BECS/Create Project")]
        public static void CreateProject() {

            var dirObject = UnityEditor.Selection.activeObject;
            string pathRoot = "Assets";
            if (dirObject != null) {
                pathRoot = UnityEditor.AssetDatabase.GetAssetPath(dirObject);
            }
            if (pathRoot != null) {
                var newProject = UnityEngine.ScriptableObject.CreateInstance<EndCreateProject>();
                newProject.onCreated = (path) => {
                    path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
                    var newName = System.IO.Path.GetFileName(path);
                    var dirGuid = UnityEditor.AssetDatabase.CreateFolder(pathRoot, newName);
                    var dirPath = UnityEditor.AssetDatabase.GUIDToAssetPath(dirGuid);
                    {
                        newName = newName.Replace(" ", string.Empty);
                        CreateAssembly(dirPath, newName);
                        var guid = CreateTemplateScript(dirPath, newName);
                        var graph = CreateDefaultFeaturesGraph(dirPath, newName);
                        var modules = new System.Collections.Generic.List<string>();
                        var customModules = UnityEditor.TypeCache.GetTypesDerivedFrom<CreateProjectDefaultModule>();
                        foreach (var moduleType in customModules) {
                            var instance = (CreateProjectDefaultModule)System.Activator.CreateInstance(moduleType);
                            modules.Add(instance.CreateModule(dirPath, newName));
                        }
                        CreateObjectOnScene(dirPath, newName, graph, guid, modules);
                    }
                };
                UnityEditor.ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, newProject, pathRoot + "/NewProject", UnityEditor.EditorGUIUtility.FindTexture("d_Folder Icon"), "", true);
            }

        }
        
        private static ME.BECS.FeaturesGraph.SystemsGraph CreateDefaultFeaturesGraph(string path, string name) {

            var graph = ME.BECS.FeaturesGraph.SystemsGraph.CreateInstance<ME.BECS.FeaturesGraph.SystemsGraph>();
            var assetPath = $"{path}/{name}-FeaturesGraph.asset";
            UnityEditor.AssetDatabase.CreateAsset(graph, assetPath);
            graph = UnityEditor.AssetDatabase.LoadAssetAtPath<ME.BECS.FeaturesGraph.SystemsGraph>(assetPath);
            return graph;

        }

        private static void CreateObjectOnScene(string path, string name, ME.BECS.FeaturesGraph.SystemsGraph graph, string guid, System.Collections.Generic.List<string> modules) {

            UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(graph, out var guidGraph, out long localId);
            var prefabContent = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/DefaultPrefab-Template.txt").text;
            prefabContent = prefabContent.Replace("{{PROJECT_NAME}}", name);
            prefabContent = prefabContent.Replace("{{GUID}}", guid);
            prefabContent = prefabContent.Replace("{{GUID_GRAPH}}", guidGraph);
            var str = "    - enabled: 1\n      obj: {fileID: 11400000, guid: {{GUID}}, type: 2}";
            var guids = new System.Collections.Generic.List<string>();
            foreach (var module in modules) {
                guids.Add(str.Replace("{{GUID}}", module));
            }
            prefabContent = prefabContent.Replace("{{GUID_MODULES}}", string.Join("\n", guids));
            
            var assetPath = $"{path}/{name}Initializer.prefab";
            System.IO.File.WriteAllText(assetPath, prefabContent);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

            UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(assetPath));

        }

        private static string CreateTemplateScript(string path, string name) {
            
            var scriptContent = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/DefaultScript-Template.txt").text;
            scriptContent = scriptContent.Replace("{{PROJECT_NAME}}", name);

            var assetPath = $"{path}/{name}Initializer.cs";
            System.IO.File.WriteAllText(assetPath, scriptContent);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

            UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(assetPath), out var guid, out long localId);
            var metaAssetPath = $"{assetPath}.meta";
            var meta = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/DefaultScriptMeta-Template.txt").text;
            meta = meta.Replace("{{GUID}}", guid);
            System.IO.File.WriteAllText(metaAssetPath, meta);

            return guid;

        }

        private static void CreateAssembly(string path, string name) {

            var asmContent = ME.BECS.Editor.EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/DefaultAssembly-Template.txt").text;
            asmContent = asmContent.Replace("{{PROJECT_NAME}}", name);

            var assetPath = $"{path}/{name}.asmdef";
            System.IO.File.WriteAllText(assetPath, asmContent);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

        }

    }

}