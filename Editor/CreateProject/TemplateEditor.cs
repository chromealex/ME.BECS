namespace ME.BECS.Editor {
    
    using CreateProject;

    public static class TemplateEditor {

        #if ME_BECS_EDITOR_INTERNAL
        [UnityEditor.MenuItem("ME.BECS/Internal/Export Template")]
        public static void ExportMeta() {

            var files = new System.Collections.Generic.List<TemplateInnerJson.File>();
            var configs = new System.Collections.Generic.List<TemplateInnerJson.File>();
            var views = new System.Collections.Generic.List<TemplateInnerJson.File>();
            foreach (var obj in ObjectReferenceRegistry.data.objects) {
                var src = obj.data.source ?? obj.data.sourceReference.editorAsset;
                var path = UnityEditor.AssetDatabase.GetAssetPath(src);
                if (path.StartsWith("Assets/__template") == false) continue;
                path = path.Replace("Assets/__template/data/", string.Empty);
                if (obj.data.Is<EntityConfig>() == true) {
                    configs.Add(new TemplateInnerJson.File() {
                        search = $"sourceId: {obj.data.sourceId}",
                        target = "sourceId: {0}",
                        file = path,
                    });
                } else if (obj.data.Is<ME.BECS.Views.EntityView>() == true) {
                    views.Add(new TemplateInnerJson.File() {
                        search = $"prefabId: {obj.data.sourceId}",
                        target = "prefabId: {0}",
                        file = path,
                    });
                } else {
                    files.Add(new TemplateInnerJson.File() {
                        search = $"id: {obj.data.sourceId}",
                        target = "id: {0}",
                        file = path,
                    });
                }
            }

            var json = new TemplateInnerJson() {
                files = files.ToArray(),
                views = views.ToArray(),
                configs = configs.ToArray(),
            };
            
            System.IO.File.WriteAllText("Assets/__template/template.json", UnityEngine.JsonUtility.ToJson(json, true));

        }
        #endif

    }

}