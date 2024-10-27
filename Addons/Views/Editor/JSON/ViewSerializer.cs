namespace ME.BECS.Editor.JSON {

    using ME.BECS.Views;
    
    public class ViewSerializer : ObjectReferenceSerializer<EntityView, View> {

        public override string ProtocolPrefix => "view";

        public override uint GetId(View obj, ref string customData) {
            customData = obj.viewSource.providerId.ToString();
            return obj.viewSource.prefabId;
        }
        public override View Deserialize(uint objectId, EntityView obj, string customData) {
            return new View() { viewSource = new ViewSource() { prefabId = objectId, providerId = uint.Parse(customData) } };
        }

    }

}