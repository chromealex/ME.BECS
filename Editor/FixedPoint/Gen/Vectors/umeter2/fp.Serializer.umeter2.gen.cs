namespace ME.BECS.Editor {
    
    using ME.BECS.Editor.JSON;

    public class Fpumeter2Serializer : SerializerBase<umeter2> {

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (umeter2)obj;
            builder.Append(val.ToString());
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
        
        public override object FromString(System.Type fieldType, string value) => umeter2.Parse(value);
        
    }

}