using Unity.Jobs;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public struct ApplyEntityConfigData {

        public Ent ent;
        public uint typeId;

    }
    
    public readonly unsafe struct UnsafeEntityConfig : IIsCreated {

        private readonly struct SharedData {

            private struct Func {

                public System.IntPtr pointer;
                public System.Runtime.InteropServices.GCHandle handle;

                public bool IsValid() => this.pointer != System.IntPtr.Zero;

                public void Call(in UnsafeEntityConfig config, byte* comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(in config, comp, in ent);
                }

                public void Dispose() {
                    
                    // TODO: Free
                    //this.handle.Free();
                    this = default;

                }

            }

            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* hashes;
            [NativeDisableUnsafePtrRestriction]
            private readonly Func* functionPointers;

            [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private delegate void MethodCallerDelegate(in UnsafeEntityConfig config, void* component, in Ent ent);
            
            internal static class MethodCaller<T> where T : unmanaged, IComponent {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(in UnsafeEntityConfig config, void* component, in Ent ent) {

                    WorldStaticCallbacks.RaiseConfigComponentCallback<T>(in config, component, in ent);

                }

            }

            [INLINE(256)]
            public SharedData(IConfigComponentShared[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                
                this.offsets = _makeArray<uint>(cnt, false);
                this.typeIds = _makeArray<uint>(cnt, false);
                this.hashes = _makeArray<uint>(cnt, false);
                this.functionPointers = _makeArray<Func>(cnt);
                this.count = cnt;

                var offset = 0u;
                var size = 0u;
                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    StaticTypesLoadedManaged.loadedSharedTypesCustomHash.TryGetValue(typeId, out var hasCustomHash);
                    E.IS_VALID_TYPE_ID(typeId);
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    size += elemSize;
                    this.offsets[i] = offset;
                    this.typeIds[i] = typeId;
                    this.hashes[i] = hasCustomHash == true ? comp.GetHash() : Components.COMPONENT_SHARED_DEFAULT_HASH;
                    offset += elemSize;
                }
                this.data = (byte*)_make(size, 4, Constants.ALLOCATOR_PERSISTENT);

                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    _memcpy((void*)ptr, this.data + this.offsets[i], elemSize);
                    if (StaticTypes.collectionsCount.Get(this.typeIds[i]) > 0u) {
                        var caller = typeof(MethodCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = System.Delegate.CreateDelegate(typeof(MethodCallerDelegate), null, method);
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(del);
                        this.functionPointers[i] = new Func() {
                            pointer = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(del),
                            handle = handle,
                        };
                    }
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in UnsafeEntityConfig config, in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0u; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    var typeId = this.typeIds[i];
                    var groupId = StaticTypes.groups.Get(typeId);
                    var dataSize = StaticTypes.sizes.Get(typeId);
                    var sharedTypeId = StaticTypes.sharedTypeId.Get(typeId);
                    var hash = this.hashes[i];
                    var func = this.functionPointers[i];
                    if (func.IsValid() == true) func.Call(in config, data, in ent);
                    state->batches.SetShared(in ent, groupId, data, dataSize, typeId, sharedTypeId, state, hash);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                for (uint i = 0u; i < this.count; ++i) {
                    this.functionPointers[i].Dispose();
                }

                _free(this.data, Constants.ALLOCATOR_PERSISTENT);
                CutsPool._freeArray(this.hashes, this.count);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);
                CutsPool._freeArray(this.functionPointers, this.count);

            }

        }

        internal readonly struct Data {

            private struct Func {

                public System.IntPtr pointer;
                public System.Runtime.InteropServices.GCHandle handle;

                public bool IsValid() => this.pointer != System.IntPtr.Zero;

                public void Call(in UnsafeEntityConfig config, byte* comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(in config, comp, in ent);
                }

                public void Dispose() {
                    
                    // TODO: Free
                    //this.handle.Free();
                    this = default;

                }

            }
            
            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;
            [NativeDisableUnsafePtrRestriction]
            private readonly Func* functionPointers;

            [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private delegate void MethodCallerDelegate(in UnsafeEntityConfig config, void* component, in Ent ent);
            
            internal static class MethodCaller<T> where T : unmanaged, IComponent {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(in UnsafeEntityConfig config, void* component, in Ent ent) {

                    WorldStaticCallbacks.RaiseConfigComponentCallback<T>(in config, component, in ent);

                }

            }

            [INLINE(256)]
            public Data(IConfigComponent[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                this.offsets = _makeArray<uint>(cnt, false);
                this.typeIds = _makeArray<uint>(cnt, false);
                this.functionPointers = _makeArray<Func>(cnt);
                this.count = cnt;
                
                var offset = 0u;
                var size = 0u;
                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    E.IS_VALID_TYPE_ID(typeId);
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    size += elemSize;
                    this.offsets[i] = offset;
                    this.typeIds[i] = typeId;
                    offset += elemSize;
                }
                this.data = (byte*)_make(size);

                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    _memcpy((void*)ptr, this.data + this.offsets[i], elemSize);
                    if (StaticTypes.collectionsCount.Get(this.typeIds[i]) > 0u) {
                        var caller = typeof(MethodCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = System.Delegate.CreateDelegate(typeof(MethodCallerDelegate), null, method);
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(del);
                        this.functionPointers[i] = new Func() {
                            pointer = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(del),
                            handle = handle,
                        };
                    }
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in UnsafeEntityConfig config, in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0u; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    var typeId = this.typeIds[i];
                    var func = this.functionPointers[i];
                    if (func.IsValid() == true) func.Call(in config, data, in ent);
                    state->batches.Set(in ent, typeId, data, state);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                for (uint i = 0u; i < this.count; ++i) {
                    this.functionPointers[i].Dispose();
                }
                
                _free(this.data);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);
                CutsPool._freeArray(this.functionPointers, this.count);

            }

            [INLINE(256)]
            public bool TryRead<TComponent>(out TComponent data) where TComponent : unmanaged, IComponent {

                data = default;
                var typeId = StaticTypes<TComponent>.typeId;
                for (uint i = 0u; i < this.count; ++i) {
                    if (this.typeIds[i] == typeId) {
                        data = *(TComponent*)(this.data + this.offsets[i]);
                        return true;
                    }
                }

                return false;

            }

            [INLINE(256)]
            public bool Has<TComponent>() where TComponent : unmanaged, IComponent {
                
                var typeId = StaticTypes<TComponent>.typeId;
                for (uint i = 0u; i < this.count; ++i) {
                    if (this.typeIds[i] == typeId) {
                        return true;
                    }
                }

                return false;
                
            }

        }

        internal readonly struct DataInitialize {

            private struct Func {

                public System.IntPtr pointer;
                public System.Runtime.InteropServices.GCHandle handle;

                public void Call(byte* comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(comp, in ent);
                }

                public void Dispose() {
                    
                    // TODO: Free
                    //this.handle.Free();
                    this = default;

                }

            }

            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;
            [NativeDisableUnsafePtrRestriction]
            private readonly Func* functionPointers;
            
            [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private delegate void MethodCallerDelegate(void* component, in Ent ent);
            
            internal static class MethodCaller<T> where T : unmanaged, IConfigInitialize {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(void* component, in Ent ent) {

                    _ptrToStruct(component, out T tempData);
                    tempData.OnInitialize(in ent);
                    _structToPtr(ref tempData, component);

                }

            }
            
            [INLINE(256)]
            public DataInitialize(IConfigInitialize[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                this.offsets = _makeArray<uint>(cnt, false);
                this.typeIds = _makeArray<uint>(cnt, false);
                this.functionPointers = _makeArray<Func>(cnt, false);
                this.count = cnt;
                
                var offset = 0u;
                var size = 0u;
                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    E.IS_VALID_TYPE_ID(typeId);
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    size += elemSize;
                    this.offsets[i] = offset;
                    this.typeIds[i] = typeId;
                    offset += elemSize;
                }
                this.data = (byte*)_make(size);

                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    _memcpy((void*)ptr, this.data + this.offsets[i], elemSize);
                    {
                        var caller = typeof(MethodCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = System.Delegate.CreateDelegate(typeof(MethodCallerDelegate), null, method);
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(del);
                        this.functionPointers[i] = new Func() {
                            pointer = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(del),
                            handle = handle,
                        };
                    }
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in Ent ent) {

                for (uint i = 0u; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    this.functionPointers[i].Call(data, in ent);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                for (uint i = 0u; i < this.count; ++i) {
                    this.functionPointers[i].Dispose();
                }
                
                _free(this.data);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);
                CutsPool._freeArray(this.functionPointers, this.count);

            }

        }

        private readonly struct Aspect {

            [NativeDisableUnsafePtrRestriction]
            private readonly uint* sizes;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;
            
            public Aspect(ComponentsStorage<IAspect> configAspects) {

                var components = configAspects.components;
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                this.sizes = _makeArray<uint>(cnt, false);
                this.typeIds = _makeArray<uint>(cnt, false);
                this.count = cnt;
                
                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    AspectTypeInfoLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    E.IS_VALID_ASPECT_TYPE_ID(typeId);
                    var elemSize = AspectTypeInfo.sizes.Get(typeId);
                    this.sizes[i] = elemSize;
                    this.typeIds[i] = typeId;
                }

            }

            [INLINE(256)]
            public void Apply(in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0u; i < this.count; ++i) {
                    state->aspectsStorage.Initialize(state, this.typeIds[i], this.sizes[i]);
                    state->aspectsStorage.SetAspect(state, in ent, this.typeIds[i]);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                CutsPool._freeArray(this.sizes, this.count);
                CutsPool._freeArray(this.typeIds, this.count);

            }
            
        }

        private readonly struct CollectionsData {

            private struct Collection {

                public uint id;
                public uint length;
                public byte* array;

                public void Dispose() {
                    _free(this.array);
                    this = default;
                }

            }

            private readonly Collection* items;
            private readonly uint length;

            public CollectionsData(EntityConfig.CollectionsData data) {
                if (data.items == null || data.items.Count == 0) {
                    this = default;
                    return;
                }

                this.length = (uint)data.items.Count;
                this.items = _makeArray<Collection>(this.length);
                for (uint i = 0u; i < this.length; ++i) {
                    var item = data.items[(int)i];
                    var sizeOfElement = item.array.Count > 0 ? (uint)System.Runtime.InteropServices.Marshal.SizeOf(item.array[0].GetType()) : 0u;
                    if (sizeOfElement == 0u) continue;
                    var collection = new Collection {
                        id = item.id,
                        length = (uint)item.array.Count,
                        array = (byte*)_make(sizeOfElement * item.array.Count),
                    };
                    var offset = 0u;
                    foreach (var obj in item.array) {
                        var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(obj, System.Runtime.InteropServices.GCHandleType.Pinned);
                        var ptr = gcHandle.AddrOfPinnedObject();
                        _memcpy((void*)ptr, collection.array + offset, sizeOfElement);
                        gcHandle.Free();
                        offset += sizeOfElement;
                    }

                    this.items[i] = collection;
                }
            }

            public bool GetById(uint id, out byte* array, out uint length) {
                array = null;
                length = 0u;
                for (uint i = 0u; i < this.length; ++i) {
                    var item = this.items[i];
                    if (item.id == id) {
                        array = item.array;
                        length = item.length;
                        return true;
                    }
                }
                return false;
            }

            public void Dispose() {
                for (uint i = 0u; i < this.length; ++i) {
                    this.items[i].Dispose();
                }
                _free(this.items);
            }

        }

        internal readonly struct StaticData {

            [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
            private delegate void MethodCallerDelegate(in UnsafeEntityConfig config, void* component, in Ent ent);
            
            internal static class MethodCaller<T> where T : unmanaged, IConfigComponentStatic {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(in UnsafeEntityConfig config, void* component, in Ent ent) {

                    WorldStaticCallbacks.RaiseConfigComponentCallback<T>(in config, component, in ent);

                }

            }

            private readonly Ent staticDataEnt;

            public StaticData(in Ent ent, EntityConfig sourceConfig, UnsafeEntityConfig config) {
                
                this.staticDataEnt = ent;
                
                var state = ent.World.state;
                for (int i = 0; i < sourceConfig.staticData.components.Length; ++i) {
                    var comp = sourceConfig.staticData.components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    {
                        var caller = typeof(MethodCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = (MethodCallerDelegate)System.Delegate.CreateDelegate(typeof(MethodCallerDelegate), null, method);
                        del.Invoke(in config, (void*)ptr, in ent);
                    }
                    state->batches.Set(in this.staticDataEnt, typeId, (void*)ptr, this.staticDataEnt.World.state);
                    gcHandle.Free();
                }

            }
            
            [INLINE(256)]
            public bool HasStatic<T>() where T : unmanaged, IConfigComponentStatic {

                var state = this.staticDataEnt.World.state;
                return state->components.Has<T>(state, this.staticDataEnt.id, this.staticDataEnt.gen, checkEnabled: false);

            }

            [INLINE(256)]
            public T ReadStatic<T>() where T : unmanaged, IConfigComponentStatic {
            
                var state = this.staticDataEnt.World.state;
                return state->components.Read<T>(state, this.staticDataEnt.id, this.staticDataEnt.gen);

            }
            
        }
        
        private readonly uint id;
        [NativeDisableUnsafePtrRestriction]
        private readonly UnsafeEntityConfig* baseConfig;
        private readonly Data data;
        private readonly SharedData dataShared;
        private readonly DataInitialize dataInitialize;
        private readonly Aspect aspects;
        private readonly CollectionsData collectionsData;
        private readonly StaticData staticData;

        public bool IsCreated => this.IsValid();

        [INLINE(256)]
        public UnsafeEntityConfig(EntityConfig config, uint id = 0u, Ent staticDataEnt = default) {
            
            this.id = id > 0u ? id : EntityConfigRegistry.Register(config, out _);
            this.data = new Data(config.data.components);
            this.dataShared = new SharedData(config.sharedData.components);
            this.dataInitialize = new DataInitialize(config.dataInitialize.components);
            this.aspects = new Aspect(config.aspects);
            this.collectionsData = new CollectionsData(config.collectionsData);
            
            this.baseConfig = null;
            if (config.baseConfig is not null) {
                this.baseConfig = _make(new UnsafeEntityConfig(config.baseConfig, staticDataEnt: staticDataEnt));
            }

            this.staticData = default;
            this.staticData = new StaticData(staticDataEnt, config, this);
            
        }

        [INLINE(256)]
        public bool GetCollectionById(uint id, out byte* data, out uint length) {
            return this.collectionsData.GetById(id, out data, out length);
        }

        [INLINE(256)]
        public bool TryRead<T>(out T data) where T : unmanaged, IComponent {
            return this.data.TryRead(out data);
        }

        [INLINE(256)]
        public bool Has<T>() where T : unmanaged, IComponent {
            return this.data.Has<T>();
        }

        [INLINE(256)]
        public void Apply(in Ent ent) {

            if (this.IsValid() == false) {
                throw new System.Exception();
            }

            if (this.baseConfig != null) {
                this.baseConfig->Apply(in ent);
            }
            
            ent.Set(new EntityConfigComponent() {
                id = this.id,
            });

            this.aspects.Apply(in ent);
            this.data.Apply(in this, in ent);
            this.dataShared.Apply(in this, in ent);
            this.dataInitialize.Apply(in ent);
            
        }

        [BURST(CompileSynchronously = true)]
        private struct ConfigDisposeJob : Unity.Jobs.IJob {

            public UnsafeEntityConfig config;
            public void Execute() {
                this.config.Dispose();
            }

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new ConfigDisposeJob() {
                config = this,
            }.Schedule(dependsOn);
            return dependsOn;
        }
        
        [INLINE(256)]
        public void Dispose() {

            this.aspects.Dispose();
            this.data.Dispose();
            this.dataShared.Dispose();
            this.dataInitialize.Dispose();
            this.collectionsData.Dispose();
            if (this.baseConfig != null) {
                this.baseConfig->Dispose();
                _free(this.baseConfig);
            }

        }

        [INLINE(256)]
        public bool IsValid() {
            return this.id > 0u;
        }

        [INLINE(256)]
        public bool HasStatic<T>() where T : unmanaged, IConfigComponentStatic {

            return this.staticData.HasStatic<T>();

        }

        [INLINE(256)]
        public T ReadStatic<T>() where T : unmanaged, IConfigComponentStatic {

            return this.staticData.ReadStatic<T>();

        }

    }

}