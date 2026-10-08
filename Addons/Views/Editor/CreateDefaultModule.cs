namespace ME.BECS.Views.Editor {
    
    using ME.BECS.Editor;

    /// <summary>
    /// Provides lifecycle integration for the create default feature.
    /// </summary>
    public class CreateDefaultModule : CreateProjectDefaultModule {

        /// <summary>
        /// Selected execution or presentation mode.
        /// </summary>
        public override ModeSupport mode => ModeSupport.SinglePlayer | ModeSupport.Multiplayer;

        /// <summary>
        /// Creates module.
        /// </summary>
        public override string CreateModule(string projectPath, string projectName) {
            
            var viewsModuleContent = EditorUtils.LoadResource<UnityEngine.TextAsset>("ME.BECS.Resources/Templates/DefaultViewsModule-Template.txt").text;
            var assetName = $"{projectName}-ViewsModule";
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