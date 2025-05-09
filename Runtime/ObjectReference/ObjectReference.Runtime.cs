namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    [System.Serializable]
    public struct RuntimeObjectReference<T> where T : UnityEngine.Object {

        public uint id;
        public ushort worldId;

        [INLINE(256)]
        public RuntimeObjectReference(T obj, ushort worldId) {
            this.id = 0u;
            this.worldId = worldId;
            RuntimeObjectReference.GetObject(ref this.id, this.worldId, obj);
        }

        public T Value => RuntimeObjectReference.GetObject<T>(ref this.id, this.worldId, null);

        [INLINE(256)]
        public static implicit operator RuntimeObjectReference<T>(T obj) {
            return new RuntimeObjectReference<T>(obj, Context.world.id);
        }

        [INLINE(256)]
        public static implicit operator T(RuntimeObjectReference<T> reference) {
            return reference.Value;
        }

    }

    public class ObjectReferenceData {

        private readonly System.Collections.Generic.Dictionary<int, uint> objectInstanceIdToIdx = new System.Collections.Generic.Dictionary<int, uint>();
        private UnityEngine.Object[] objects;
        private uint nextId = 1u;

        [INLINE(256)]
        public T ReadObject<T>(uint id) where T : UnityEngine.Object {
            var idx = id - 1u;
            if (id <= 0u || idx > this.objects.Length) return null;
            return (T)this.objects[idx];
        }

        [INLINE(256)]
        public T GetObject<T>(ref uint id, T obj) where T : UnityEngine.Object {
            if (id == 0) {
                if (obj == null) return null;
                var instanceId = obj.GetInstanceID();
                /*if (instanceId <= 0) {
                    throw new System.Exception("Persistent asset is required");
                }*/
                if (this.objectInstanceIdToIdx.TryGetValue(instanceId, out var index) == true) {
                    id = index + 1u;
                    return (T)this.objects[index];
                }

                {
                    var size = this.nextId * 2u;
                    System.Array.Resize(ref this.objects, (int)size);
                    id = this.nextId++;
                    var idx = id - 1;
                    this.objectInstanceIdToIdx.Add(instanceId, idx);
                    this.objects[idx] = obj;
                    return obj;
                }
            } else {
                var idx = id - 1u;
                return (T)this.objects[idx];
            }
        }
        
    }
    
    public static class RuntimeObjectReference {

        private static ObjectReferenceData[] dataArr;

        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Reset);
            
        }
        
        public static void Reset() {
            
            dataArr = null;
            
        }
        
        private static ObjectReferenceData GetData(ushort worldId) {

            var idx = worldId - 1;
            if (dataArr == null || idx >= dataArr.Length) {
                System.Array.Resize(ref dataArr, idx + 1);
            }

            ref var data = ref dataArr[idx];
            if (data == null) {
                data = new ObjectReferenceData();
            }

            return data;

        }
        
        [INLINE(256)]
        public static T ReadObject<T>(uint id, ushort worldId) where T : UnityEngine.Object {
            if (worldId == 0) return null;
            var data = GetData(worldId);
            return data.ReadObject<T>(id);
        }

        [INLINE(256)]
        public static T GetObject<T>(ref uint id, ushort worldId, T obj) where T : UnityEngine.Object {
            if (worldId == 0) return null;
            var data = GetData(worldId);
            return data.GetObject(ref id, obj);
        }

    }

}