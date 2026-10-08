namespace ME.BECS.CsvImporter {

    using UnityEngine;
    
    /// <summary>
    /// Defines entity config CSV importer state and operations.
    /// </summary>
    [CreateAssetMenu(menuName = "ME.BECS/Entity Config CSV Importer")]
    public class EntityConfigCsvImporter : ScriptableObject {

        /// <summary>
        /// Target directory used by <c>EntityConfigCsvImporter</c>.
        /// </summary>
        public Object targetDirectory;
        /// <summary>
        /// Csv urls used by <c>EntityConfigCsvImporter</c>.
        /// </summary>
        [TextArea]
        public string[] csvUrls;

    }

}