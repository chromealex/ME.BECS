namespace ME.BECS.Editor.JSON {
    
    /// <summary>
    /// Serializes and deserializes b bool values in editor data.
    /// </summary>
    public class BBoolSerializer : SerializerBase<bbool> {
        /// <summary>
        /// Writes b bool serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (bbool)obj;
            builder.Append(this.ToString(val));
        }
        /// <summary>
        /// Restores b bool serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public virtual string ToString(bbool val) {
            if (val == true) return "true";
            return "false";
        }

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var val = value.ToLower();
            if (val == "1" || val == "0") return new bbool(int.Parse(val));
            return (bbool)bool.Parse(val);
        }

    }
    
}