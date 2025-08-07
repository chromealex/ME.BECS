namespace ME.BECS.Editor.JSON {

    public abstract class ObjectSerializer<T> : ObjectReferenceSerializer<T, ObjectReference<T>> where T : UnityEngine.Object {

        public override int Priority => 10;
        public override bool IsValid(System.Type type) {
            if (type.IsGenericType == true) {
                if (typeof(IObjectReferenceId).IsAssignableFrom(type) == false) return false;
                type = type.GetGenericArguments()[0];
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) == true) {
                return true;
            }
            return false;
        }

        public override string ProtocolPrefix => "asset";
        public override uint GetId(ObjectReference<T> obj, ref string customData) => obj.id;
        public override ObjectReference<T> Deserialize(uint objectId, T obj, string customData) {
            return new ObjectReference<T>() { id = objectId };
        }

    }

    public class ObjSerializer : ObjectSerializer<UnityEngine.Object> {

        public override int Priority => 100;
        
        protected override object Convert(System.Type fieldType, ObjectReference<UnityEngine.Object> obj) {
            if (fieldType.IsGenericType == true) {
                var arg = fieldType.GetGenericArguments()[0];
                if (arg != typeof(UnityEngine.Object) && typeof(UnityEngine.Object).IsAssignableFrom(arg)) {
                    var objRefTarget = typeof(ObjectReference<>).MakeGenericType(arg);
                    var target = (IObjectReferenceId)System.Activator.CreateInstance(objRefTarget);
                    target.Id = obj.id;
                    return target;
                }
            }
            return base.Convert(fieldType, obj);
        }

    }

}