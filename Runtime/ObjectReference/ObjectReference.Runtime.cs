namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// References an entry in allocator-managed heap storage.
    /// </summary>
    public struct HeapReference<T> {

        /// <summary>
        /// Handle used by <c>HeapReference</c>.
        /// </summary>
        public System.Runtime.InteropServices.GCHandle handle;

        /// <summary>
        /// Initializes <c>HeapReference</c> from the supplied obj.
        /// </summary>
        [INLINE(256)]
        public HeapReference(T obj) {
            this.handle = System.Runtime.InteropServices.GCHandle.Alloc(obj);
        }

        /// <summary>
        /// Value wrapped or resolved by this instance.
        /// </summary>
        public T Value => (T)this.handle.Target;

        /// <summary>
        /// Releases the resources owned by this heap reference instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            if (this.handle.IsAllocated == true) this.handle.Free();
        }

    }

    /// <summary>
    /// References an entry in allocator-managed heap storage.
    /// </summary>
    public struct HeapReference {

        /// <summary>
        /// Handle used by <c>HeapReference</c>.
        /// </summary>
        public System.Runtime.InteropServices.GCHandle handle;

        /// <summary>
        /// Initializes <c>HeapReference</c> from the supplied obj.
        /// </summary>
        [INLINE(256)]
        public HeapReference(object obj) {
            this.handle = System.Runtime.InteropServices.GCHandle.Alloc(obj, System.Runtime.InteropServices.GCHandleType.Pinned);
        }

        /// <summary>
        /// Releases the resources owned by this heap reference instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            if (this.handle.IsAllocated == true) this.handle.Free();
        }

    }

    /// <summary>
    /// References a Unity object registered for the current runtime lifetime.
    /// </summary>
    [System.Serializable]
    public struct RuntimeObjectReference<T> where T : UnityEngine.Object {

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;

        /// <summary>
        /// Initializes <c>RuntimeObjectReference</c> from the supplied obj, world ID.
        /// </summary>
        [INLINE(256)]
        public RuntimeObjectReference(T obj, ushort worldId) {
            this.id = 0u;
            this.worldId = worldId;
            RuntimeObjectReference.GetObject(ref this.id, this.worldId, obj);
        }

        /// <summary>
        /// Value wrapped or resolved by this instance.
        /// </summary>
        public T Value => RuntimeObjectReference.GetObject<T>(ref this.id, this.worldId, null);

        /// <summary>
        /// Converts the supplied value to <c>RuntimeObjectReference&lt;T&gt;</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator RuntimeObjectReference<T>(T obj) {
            return new RuntimeObjectReference<T>(obj, Context.world.id);
        }

        /// <summary>
        /// Converts the supplied value to <c>T</c>.
        /// </summary>
        [INLINE(256)]
        public static implicit operator T(RuntimeObjectReference<T> reference) {
            return reference.Value;
        }

    }

    /// <summary>
    /// Stores object reference data for the associated runtime API.
    /// </summary>
    public class ObjectReferenceData {

        private readonly System.Collections.Generic.Dictionary<int, uint> objectInstanceIdToIdx = new System.Collections.Generic.Dictionary<int, uint>();
        private UnityEngine.Object[] objects;
        private uint nextId = 1u;

        /// <summary>
        /// Reads object.
        /// </summary>
        [INLINE(256)]
        public T ReadObject<T>(uint id) where T : UnityEngine.Object {
            var idx = id - 1u;
            if (id <= 0u || idx > this.objects.Length) return null;
            return (T)this.objects[idx];
        }

        /// <summary>
        /// Returns object.
        /// </summary>
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
    
    /// <summary>
    /// References a Unity object registered for the current runtime lifetime.
    /// </summary>
    public static class RuntimeObjectReference {

        private static ObjectReferenceData[] dataArr;

        /// <summary>
        /// Initializes runtime object reference state from the supplied context.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Reset);
            
        }
        
        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        public static void Reset() {
            
            dataArr = null;
            
        }
        
        [Unity.Burst.BurstDiscard]
        internal static void DisposeWorld(ushort worldId) {
            if (worldId == 0 || dataArr == null || worldId > dataArr.Length) return;
            dataArr[worldId - 1] = null;
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
        
        /// <summary>
        /// Reads object.
        /// </summary>
        [INLINE(256)]
        public static T ReadObject<T>(uint id, ushort worldId) where T : UnityEngine.Object {
            if (worldId == 0) return null;
            var data = GetData(worldId);
            return data.ReadObject<T>(id);
        }

        /// <summary>
        /// Returns object.
        /// </summary>
        [INLINE(256)]
        public static T GetObject<T>(ref uint id, ushort worldId, T obj) where T : UnityEngine.Object {
            if (worldId == 0) return null;
            var data = GetData(worldId);
            return data.GetObject(ref id, obj);
        }

    }

}