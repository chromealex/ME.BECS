namespace ME.BECS.Editor.JSON {

    public class ConfigSerializer : ObjectReferenceSerializer<EntityConfig, Config> {

        public override string ProtocolPrefix => "config";
        public override uint GetId(Config obj, ref string customData) => obj.sourceId;
        public override Config Deserialize(uint objectId, EntityConfig obj, string customData) {
            return new Config() { sourceId = objectId };
        }

    }

}