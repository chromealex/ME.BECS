namespace ME.BECS.Editor.JSON {

    using ME.BECS.Views;
    
    /// <summary>
    /// Presents view serializer state through the associated view.
    /// </summary>
    public class ViewSerializer : ObjectReferenceSerializer<EntityView, View> {

        /// <summary>
        /// Protocol prefix used by <c>ViewSerializer</c>.
        /// </summary>
        public override string ProtocolPrefix => "view";

        /// <summary>
        /// Returns ID.
        /// </summary>
        public override uint GetId(View obj, ref string customData) {
            customData = obj.viewSource.providerId.ToString();
            return obj.viewSource.prefabId;
        }
        /// <summary>
        /// Restores view serializer from the supplied serialized representation.
        /// </summary>
        public override View Deserialize(uint objectId, EntityView obj, string customData) {
            return new View() { viewSource = new ViewSource() { prefabId = objectId, providerId = uint.Parse(customData) } };
        }

    }

}