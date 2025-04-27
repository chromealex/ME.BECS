using Unity.Jobs;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public readonly unsafe struct UnsafeEntityConfig : IIsCreated {

        [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public delegate void MethodCallerDelegate(in UnsafeEntityConfig config, void* component, in Ent ent);

        private readonly struct SharedData {

            private struct Func {

                public System.IntPtr pointer;
                public System.Runtime.InteropServices.GCHandle handle;

                public bool IsValid() => this.pointer != System.IntPtr.Zero;

                public void Call(in UnsafeEntityConfig config, safe_ptr<byte> comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(in config, comp.ptr, in ent);
                }

                public void Dispose() {
                    
                    // TODO: Free
                    //this.handle.Free();
                    this = default;

                }

            }

            private readonly safe_ptr<byte> data;
            private readonly safe_ptr<uint> offsets;
            private readonly safe_ptr<uint> typeIds;
            private readonly safe_ptr<uint> hashes;
            private readonly safe_ptr<Func> functionPointers;
            private readonly uint count;

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
                this.data = (safe_ptr<byte>)_make(size, 4, Constants.ALLOCATOR_PERSISTENT);

                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    var ptr = new safe_ptr((void*)gcHandle.AddrOfPinnedObject(), elemSize);
                    if (elemSize > 0u) _memcpy(ptr, this.data + this.offsets[i], elemSize);
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
                    var typeId = this.typeIds[i];
                    var data = this.data + this.offsets[i];
                    var groupId = StaticTypes.groups.Get(typeId);
                    var dataSize = StaticTypes.sizes.Get(typeId);
                    var sharedTypeId = StaticTypes.sharedTypeId.Get(typeId);
                    var hash = this.hashes[i];
                    Batches.SetShared(in ent, groupId, data.ptr, dataSize, typeId, sharedTypeId, state, hash);
                    var func = this.functionPointers[i];
                    if (func.IsValid() == true) {
                        var dataPtr = Components.GetUnknownType(state, typeId, groupId, in ent, out _, default);
                        func.Call(in config, new safe_ptr<byte>(dataPtr), in ent);
                    }
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

                public void Call(in UnsafeEntityConfig config, safe_ptr<byte> comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(in config, comp.ptr, in ent);
                }

                public void Dispose() {
                    
                    // TODO: Free
                    //this.handle.Free();
                    this = default;

                }

            }
            
            private readonly safe_ptr<byte> data;
            private readonly safe_ptr<uint> offsets;
            private readonly safe_ptr<uint> typeIds;
            private readonly safe_ptr<Func> functionPointers;
            private readonly uint count;

            internal static class MethodCaller<T> where T : unmanaged, IComponentBase {

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
                this.data = (safe_ptr<byte>)_make(size);

                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    var ptr = new safe_ptr((void*)gcHandle.AddrOfPinnedObject(), elemSize);
                    if (elemSize > 0u) _memcpy(ptr, this.data + this.offsets[i], elemSize);
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
            public void Apply(in UnsafeEntityConfig config, in Ent ent, Config.JoinOptions options) {

                var state = ent.World.state;
                for (uint i = 0u; i < this.count; ++i) {
                    var typeId = this.typeIds[i];
                    if (options == Config.JoinOptions.LeftJoin) {
                        if (Components.HasUnknownType(state, typeId, ent.id, ent.gen, false) == false) {
                            continue;
                        }
                    } else if (options == Config.JoinOptions.RightJoin) {
                        if (Components.HasUnknownType(state, typeId, ent.id, ent.gen, false) == true) {
                            continue;
                        }
                    }
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    var data = elemSize == 0u ? new safe_ptr<byte>() : (this.data + this.offsets[i]);
                    var func = this.functionPointers[i];
                    Batches.Set(in ent, typeId, data.ptr, state);
                    if (func.IsValid() == true) {
                        var groupId = StaticTypes.groups.Get(typeId);
                        var dataPtr = Components.GetUnknownType(state, typeId, groupId, in ent, out _, default);
                        func.Call(in config, new safe_ptr<byte>(dataPtr), in ent);
                    }
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
                        data = *(TComponent*)(this.data + this.offsets[i]).ptr;
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

                public void Call(in UnsafeEntityConfig config, void* comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(in config, comp, in ent);
                }

                public void Dispose() {
                    
                    // TODO: Free
                    //this.handle.Free();
                    this = default;

                }

            }

            private readonly safe_ptr<byte> data;
            private readonly safe_ptr<uint> offsets;
            private readonly safe_ptr<uint> typeIds;
            private readonly safe_ptr<Func> functionPointers;
            private readonly uint count;
            
            internal static class MethodCaller<T> where T : unmanaged, IConfigInitialize {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(in UnsafeEntityConfig config, void* component, in Ent ent) {

                    if (component == null) {
                        T tempData = default;
                        tempData.OnInitialize(in ent);
                    } else {
                        WorldStaticCallbacks.RaiseConfigComponentCallback<T>(in config, component, in ent);
                        _ptrToStruct(component, out T tempData);
                        tempData.OnInitialize(in ent);
                        _structToPtr(ref tempData, component);
                    }

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
                this.data = (safe_ptr<byte>)_make(size);

                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    var ptr = new safe_ptr((void*)gcHandle.AddrOfPinnedObject(), elemSize);
                    if (elemSize > 0u) _memcpy(ptr, this.data + this.offsets[i], elemSize);
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
            public void Apply(in UnsafeEntityConfig config, in Ent ent) {

                for (uint i = 0u; i < this.count; ++i) {
                    var typeId = this.typeIds[i];
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    var data = elemSize == 0u ? new safe_ptr<byte>() : (this.data + this.offsets[i]);
                    this.functionPointers[i].Call(in config, data.ptr, in ent);
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

            private readonly safe_ptr<uint> sizes;
            private readonly safe_ptr<uint> typeIds;
            private readonly uint count;
                        
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
                    state.ptr->aspectsStorage.Initialize(state, this.typeIds[i], this.sizes[i]);
                    AspectsStorage.SetAspect(state, in ent, this.typeIds[i]);
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
                public safe_ptr<byte> array;

                public void Dispose() {
                    _free(this.array);
                    this = default;
                }

            }

            private readonly safe_ptr<Collection> items;
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
                        array = (safe_ptr<byte>)_make(sizeOfElement * (uint)item.array.Count),
                    };
                    var offset = 0u;
                    foreach (var obj in item.array) {
                        var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(obj, System.Runtime.InteropServices.GCHandleType.Pinned);
                        var ptr = new safe_ptr((void*)gcHandle.AddrOfPinnedObject(), sizeOfElement);
                        _memcpy(ptr, collection.array + offset, sizeOfElement);
                        gcHandle.Free();
                        offset += sizeOfElement;
                    }

                    this.items[i] = collection;
                }
            }

            public bool GetById(uint id, out safe_ptr<byte> array, out uint length) {
                array = default;
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

            internal static class MethodCaller<T> where T : unmanaged, IConfigComponentStatic {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(in UnsafeEntityConfig config, void* component, in Ent ent) {

                    WorldStaticCallbacks.RaiseConfigComponentCallback<T>(in config, component, in ent);

                }

            }

            internal readonly Ent staticDataEnt;
            private readonly safe_ptr<uint> typeIds;
            private readonly uint count;

            public StaticData(in Ent ent, EntityConfig sourceConfig, UnsafeEntityConfig config) {
                
                this.staticDataEnt = ent;

                this.count = (uint)sourceConfig.staticData.components.Length;
                this.typeIds = _makeArray<uint>(this.count, false);
                
                for (int i = 0; i < sourceConfig.staticData.components.Length; ++i) {
                    var comp = sourceConfig.staticData.components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    this.typeIds[i] = typeId;
                    var groupId = StaticTypes.groups.Get(typeId);
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    Batches.Set(in this.staticDataEnt, typeId, (void*)ptr, this.staticDataEnt.World.state);
                    var newPtr = Components.GetUnknownType(this.staticDataEnt.World.state, typeId, groupId, in this.staticDataEnt, out _, default);
                    {
                        var caller = typeof(MethodCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = (MethodCallerDelegate)System.Delegate.CreateDelegate(typeof(MethodCallerDelegate), null, method);
                        del.Invoke(in config, newPtr, in this.staticDataEnt);
                    }
                    gcHandle.Free();
                }

            }
            
            [INLINE(256)]
            public void Apply(in UnsafeEntityConfig config, in Ent ent) {
                
                var state = ent.World.state;
                for (uint i = 0u; i < this.count; ++i) {
                    var typeId = this.typeIds[i];
                    Batches.Set_INTERNAL(typeId, in ent, state);
                }
                
            }

            [INLINE(256)]
            public bool HasStatic<T>() where T : unmanaged, IConfigComponentStatic {

                E.IS_ALIVE(this.staticDataEnt);
                var world = this.staticDataEnt.World;
                return Components.Has<T>(world.state, checkEnabled: false, entId: this.staticDataEnt.id, gen: this.staticDataEnt.gen);

            }

            [INLINE(256)]
            public T ReadStatic<T>() where T : unmanaged, IConfigComponentStatic {

                E.IS_ALIVE(this.staticDataEnt);
                var world = this.staticDataEnt.World;
                return Components.Read<T>(world.state, entId: this.staticDataEnt.id, gen: this.staticDataEnt.gen, out _);

            }

            [INLINE(256)]
            public bool TryReadStatic<T>(out T component) where T : unmanaged, IConfigComponentStatic {

                E.IS_ALIVE(this.staticDataEnt);
                var world = this.staticDataEnt.World;
                component = Components.Read<T>(world.state, entId: this.staticDataEnt.id, gen: this.staticDataEnt.gen, out var exists);
                return exists;

            }

        }
        
        public readonly uint id;
        private readonly safe_ptr<UnsafeEntityConfig> baseConfig;
        private readonly Data data;
        private readonly SharedData dataShared;
        private readonly DataInitialize dataInitialize;
        private readonly Aspect aspects;
        private readonly CollectionsData collectionsData;
        private readonly StaticData staticData;

        public bool IsCreated => this.IsValid();

        [INLINE(256)]
        public UnsafeEntityConfig(EntityConfig config, uint id = 0u, Ent staticDataEnt = default, bool autoRegisterConfig = true) {
            
            this.id = id > 0u || autoRegisterConfig == false ? id : EntityConfigRegistry.Register(config, out _);
            this.data = new Data(config.data.components);
            this.dataShared = new SharedData(config.sharedData.components);
            this.dataInitialize = new DataInitialize(config.dataInitialize.components);
            this.aspects = new Aspect(config.aspects);
            this.collectionsData = new CollectionsData(config.collectionsData);
            
            this.baseConfig = default;
            if (config.baseConfig is not null) {
                this.baseConfig = _make(new UnsafeEntityConfig(config.baseConfig, staticDataEnt: staticDataEnt, autoRegisterConfig: false));
            }

            this.staticData = default;
            this.staticData = new StaticData(staticDataEnt, config, this);
            
        }

        [INLINE(256)]
        public bool GetCollectionById(uint id, out safe_ptr<byte> data, out uint length) {
            return this.collectionsData.GetById(id, out data, out length);
        }

        [INLINE(256)]
        public bool TryRead<T>(out T data) where T : unmanaged, IComponent {
            if (this.data.TryRead<T>(out data) == false) {
                if (this.baseConfig.ptr != null) {
                    return this.baseConfig.ptr->TryRead<T>(out data);
                }
                return false;
            }
            return true;
        }

        [INLINE(256)]
        public bool Has<T>() where T : unmanaged, IComponent {
            if (this.data.Has<T>() == false) {
                if (this.baseConfig.ptr != null) {
                    return this.baseConfig.ptr->Has<T>();
                }
                return false;
            }
            return true;
        }

        [INLINE(256)]
        public void Apply(in Ent ent, Config.JoinOptions options = Config.JoinOptions.FullJoin) {
            
            if (this.IsValid() == false) {
                throw new System.Exception();
            }
            
            this.Apply_INTERNAL(in ent, options);

        }

        [INLINE(256)]
        private void Apply_INTERNAL(in Ent ent, Config.JoinOptions options) {

            if (this.baseConfig.ptr != null) {
                this.baseConfig.ptr->Apply_INTERNAL(in ent, options);
            }

            if (options == Config.JoinOptions.FullJoin) {
                ent.Set(new EntityConfigComponent() {
                    id = this.id,
                });
            }

            this.aspects.Apply(in ent);
            this.data.Apply(in this, in ent, options);
            this.staticData.Apply(in this, in ent);
            this.dataShared.Apply(in this, in ent);
            this.dataInitialize.Apply(in this, in ent);
            
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
            if (this.baseConfig.ptr != null) {
                this.baseConfig.ptr->Dispose();
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

        [INLINE(256)]
        public bool TryReadStatic<T>(out T component) where T : unmanaged, IConfigComponentStatic {

            return this.staticData.TryReadStatic(out component);

        }

        internal Ent GetStaticEntity() {
            return this.staticData.staticDataEnt;
        }

    }

}
