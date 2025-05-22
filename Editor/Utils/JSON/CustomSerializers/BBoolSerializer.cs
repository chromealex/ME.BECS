namespace ME.BECS.Editor.JSON {
    
    public class BBoolSerializer : SerializerBase<bbool> {
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (bbool)obj;
            builder.Append(this.ToString(val));
        }
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
        public virtual string ToString(bbool val) {
            if (val == true) return "true";
            return "false";
        }

        public override object FromString(System.Type fieldType, string value) {
            var val = value.ToLower();
            if (val == "1" || val == "0") return new bbool(int.Parse(val));
            return (bbool)bool.Parse(val);
        }

    }
    
}