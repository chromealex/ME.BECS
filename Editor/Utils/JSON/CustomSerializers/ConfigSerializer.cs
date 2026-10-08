namespace ME.BECS.Editor.JSON {

    /// <summary>
    /// Serializes and deserializes config values in editor data.
    /// </summary>
    public class ConfigSerializer : ObjectReferenceSerializer<EntityConfig, Config> {

        /// <summary>
        /// Protocol prefix used by <c>ConfigSerializer</c>.
        /// </summary>
        public override string ProtocolPrefix => "config";
        /// <summary>
        /// Returns ID.
        /// </summary>
        public override uint GetId(Config obj, ref string customData) => obj.sourceId;
        /// <summary>
        /// Restores config serializer from the supplied serialized representation.
        /// </summary>
        public override Config Deserialize(uint objectId, EntityConfig obj, string customData) {
            return new Config() { sourceId = objectId };
        }

    }

}