namespace ME.BECS.Editor {
    
    using ME.BECS.Editor.JSON;

    public class FpusecSerializer : SerializerBase<usec> {

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (usec)obj;
            builder.Append(val.ToString());
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
        
        public override object FromString(System.Type fieldType, string value) => usec.Parse(value);
        
    }

}