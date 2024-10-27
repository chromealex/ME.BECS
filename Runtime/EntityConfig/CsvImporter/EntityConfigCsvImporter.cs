namespace ME.BECS.CsvImporter {

    using UnityEngine;
    
    [CreateAssetMenu(menuName = "ME.BECS/Entity Config CSV Importer")]
    public class EntityConfigCsvImporter : ScriptableObject {

        public Object targetDirectory;
        [TextArea]
        public string[] csvUrls;

    }

}