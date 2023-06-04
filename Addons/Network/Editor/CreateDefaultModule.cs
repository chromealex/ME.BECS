namespace ME.BECS.Network.Editor {
    
    using ME.BECS.Editor;

    public class CreateDefaultModule : ME.BECS.Features.Editor.CreateProjectDefaultModule {

        public override string CreateModule(string projectPath, string projectName) {
            
            var viewsModuleContent = EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/DefaultNetworkModule-Template.txt").text;
            var assetName = $"{projectName}-NetworkModule";
            viewsModuleContent = viewsModuleContent.Replace("{{NAME}}", assetName);
            var assetPath = $"{projectPath}/{assetName}.asset";
            System.IO.File.WriteAllText(assetPath, viewsModuleContent);
            UnityEditor.AssetDatabase.ImportAsset(assetPath);

            var obj = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(assetPath);
            UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long localId);

            return guid;
            
        }


    }

}