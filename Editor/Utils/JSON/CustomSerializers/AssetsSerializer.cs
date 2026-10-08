namespace ME.BECS.Editor.JSON {

    /// <summary>
    /// Serializes and deserializes object values in editor data.
    /// </summary>
    public abstract class ObjectSerializer<T> : ObjectReferenceSerializer<T, ObjectReference<T>> where T : UnityEngine.Object {

        /// <summary>
        /// Priority used when ordering or selecting this entry.
        /// </summary>
        public override int Priority => 10;
        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
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

        /// <summary>
        /// Protocol prefix used by <c>ObjectSerializer</c>.
        /// </summary>
        public override string ProtocolPrefix => "asset";
        /// <summary>
        /// Returns ID.
        /// </summary>
        public override uint GetId(ObjectReference<T> obj, ref string customData) => obj.id;
        /// <summary>
        /// Restores object serializer from the supplied serialized representation.
        /// </summary>
        public override ObjectReference<T> Deserialize(uint objectId, T obj, string customData) {
            return new ObjectReference<T>() { id = objectId };
        }

    }

    /// <summary>
    /// Serializes and deserializes obj values in editor data.
    /// </summary>
    public class ObjSerializer : ObjectSerializer<UnityEngine.Object> {

        /// <summary>
        /// Priority used when ordering or selecting this entry.
        /// </summary>
        public override int Priority => 100;
        
        /// <summary>
        /// Returns object.
        /// </summary>
        protected override UnityEngine.Object GetObject(System.Type fieldType, string path) {
            if (fieldType.IsGenericType == true) {
                var arg = fieldType.GetGenericArguments()[0];
                if (arg != typeof(UnityEngine.Object) && typeof(UnityEngine.Object).IsAssignableFrom(arg) == true) {
                    var method = this.GetType().GetMethod("GetObjectByType", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    return (UnityEngine.Object)method.MakeGenericMethod(arg).Invoke(null, new [] { path });
                }
            }
            return EditorUtils.GetAssetByPathPart<UnityEngine.Object>(path);
        }

        private static UnityEngine.Object GetObjectByType<T>(string path) where T : UnityEngine.Object {
            return EditorUtils.GetAssetByPathPart<T>(path);
        }
        
        /// <summary>
        /// Converts the supplied value into the requested representation.
        /// </summary>
        protected override object Convert(System.Type fieldType, ObjectReference<UnityEngine.Object> obj) {
            if (fieldType.IsGenericType == true) {
                var arg = fieldType.GetGenericArguments()[0];
                if (arg != typeof(UnityEngine.Object) && typeof(UnityEngine.Object).IsAssignableFrom(arg) == true) {
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