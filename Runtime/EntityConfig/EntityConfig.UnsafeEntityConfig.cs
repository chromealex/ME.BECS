using Unity.Jobs;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public readonly unsafe struct UnsafeEntityConfig : IIsCreated {

        [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public delegate void MethodCallerDelegate(in UnsafeEntityConfig config, void* component, in Ent ent);
        
        [System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public delegate void MethodMaskCallerDelegate(in UnsafeEntityConfig config, void* component, void* configComponent, void* mask, in Ent ent);

        internal static class MethodComponentMaskCaller<T> where T : unmanaged, IComponentBase {

            [UnityEngine.Scripting.PreserveAttribute]
            [AOT.MonoPInvokeCallbackAttribute(typeof(MethodMaskCallerDelegate))]
            public static void Call(in UnsafeEntityConfig config, void* component, void* configComponent, void* mask, in Ent ent) {

                WorldStaticCallbacks.RaiseConfigComponentMaskCallback<T>(in config, component, configComponent, *(BitArray*)mask, in ent);

            }

        }

        private struct FuncMask {

            public System.IntPtr pointer;
            public System.Runtime.InteropServices.GCHandle handle;

            public bool IsValid() => this.pointer != System.IntPtr.Zero;

            public void Call(in UnsafeEntityConfig config, safe_ptr<byte> comp, safe_ptr<byte> configComp, BitArray* mask, in Ent ent) {
                var del = new Unity.Burst.FunctionPointer<MethodMaskCallerDelegate>(this.pointer);
                del.Invoke(in config, comp.ptr, configComp.ptr, mask, in ent);
            }

            public void Dispose() {
                    
                //if (this.handle.IsAllocated == true) this.handle.Free();
                this = default;

            }

        }

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
                    
                    //if (this.handle.IsAllocated == true) this.handle.Free();
                    this = default;

                }

            }

            private readonly safe_ptr<byte> data;
            private readonly safe_ptr<uint> offsets;
            internal readonly safe_ptr<uint> typeIds;
            private readonly safe_ptr<uint> hashes;
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
                    var data = this.data + this.offsets[i];
                    var groupId = StaticTypes.tracker.Get(typeId);
                    var dataSize = StaticTypes.sizes.Get(typeId);
                    var sharedTypeId = StaticTypes.sharedTypeId.Get(typeId);
                    var hash = this.hashes[i];
                    Batches.SetShared(in ent, groupId, data.ptr, dataSize, typeId, sharedTypeId, state, hash, out var dataPtr);
                    var func = this.functionPointers[i];
                    if (func.IsValid() == true) {
                        func.Call(in config, dataPtr, in ent);
                    }
                }

            }

            [INLINE(256)]
            internal safe_ptr<byte> GetComponentPtr(uint index) {
                return this.data + this.offsets[index];
            }

            [INLINE(256)]
            public void Dispose() {

                for (uint i = 0u; i < this.count; ++i) {
                    this.functionPointers[i].Dispose();
                }

                _free(this.data);
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
                    
                    //if (this.handle.IsAllocated == true) this.handle.Free();
                    this = default;

                }

            }
            
            private readonly safe_ptr<byte> data;
            private readonly safe_ptr<uint> offsets;
            internal readonly safe_ptr<uint> typeIds;
            internal readonly safe_ptr<BitArray> masks;
            private readonly safe_ptr<Func> functionPointers;
            private readonly safe_ptr<FuncMask> functionMaskPointers;
            private readonly uint count;

            internal static class MethodCaller<T> where T : unmanaged, IComponentBase {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void Call(in UnsafeEntityConfig config, void* component, in Ent ent) {

                    WorldStaticCallbacks.RaiseConfigComponentCallback<T>(in config, component, in ent);

                }

            }

            [INLINE(256)]
            public Data(ref MemoryAllocator allocator, IConfigComponent[] components, ComponentsStorageBitMask[] masks) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                this.offsets = _makeArray<uint>(cnt, false);
                this.typeIds = _makeArray<uint>(cnt, false);
                if (masks != null) {
                    this.masks = _makeArray<BitArray>(cnt, false);
                    this.functionMaskPointers = _makeArray<FuncMask>(cnt);
                } else {
                    this.masks = default;
                    this.functionMaskPointers = default;
                }
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
                    if (masks != null) {
                        this.masks[i] = (masks[i].mask != null && masks[i].mask.Length > 1u) ? new BitArray(ref allocator, masks[i].mask) : default;
                    }
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

                    if (masks != null) {
                        var caller = typeof(MethodComponentMaskCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = System.Delegate.CreateDelegate(typeof(MethodMaskCallerDelegate), null, method);
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(del);
                        this.functionMaskPointers[i] = new FuncMask() {
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
                    var groupId = StaticTypes.tracker.Get(typeId);
                    if (this.masks.ptr != null && this.masks[i].Length > 1u) {
                        var dataPtr = Components.GetUnknownType(ent.World.state, typeId, groupId, in ent, out var isNew, default);
                        if (isNew == true) {
                            Batches.Set_INTERNAL(typeId, in ent);
                        }
                        if (this.functionMaskPointers[i].IsValid() == true) {
                            this.functionMaskPointers[i].Call(in config, new safe_ptr<byte>(dataPtr), data, (this.masks.ptr + i), in config.staticData.staticDataEnt);
                        }
                    } else {
                        Batches.Set(in ent, typeId, data.ptr, state);
                    }

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

                if (this.functionMaskPointers.ptr != null) {
                    for (uint i = 0u; i < this.count; ++i) {
                        this.functionMaskPointers[i].Dispose();
                    }
                }

                _free(this.data);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);
                if (this.masks.ptr != null) CutsPool._freeArray(this.masks, this.count);
                CutsPool._freeArray(this.functionPointers, this.count);
                if (this.functionMaskPointers.ptr != null) CutsPool._freeArray(this.functionMaskPointers, this.count);

            }

            [INLINE(256)]
            internal safe_ptr<byte> GetComponentPtr(uint index) {
                return this.data + this.offsets[index];
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
                    
                    //if (this.handle.IsAllocated == true) this.handle.Free();
                    this = default;

                }

            }

            private readonly safe_ptr<ComponentsStorageLink.Item> data;
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
                        _ptrToStruct(component, out T tempData);
                        tempData.OnInitialize(in ent);
                        _structToPtr(ref tempData, component);
                    }

                }

            }
            
            [INLINE(256)]
            public DataInitialize(in UnsafeEntityConfig config, ComponentsStorageLink components) {

                var cnt = (uint)components.items.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }

                this.data = _makeArray<ComponentsStorageLink.Item>(cnt, false);
                this.functionPointers = _makeArray<Func>(cnt, false);
                this.count = cnt;
                
                for (uint i = 0u; i < cnt; ++i) {
                    var item = components.items[i];
                    this.data[i] = item;
                    {
                        var type = GetItemType(config, item);
                        var caller = typeof(MethodCaller<>).MakeGenericType(type);
                        var method = caller.GetMethod("Call", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var del = System.Delegate.CreateDelegate(typeof(MethodCallerDelegate), null, method);
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(del);
                        this.functionPointers[i] = new Func() {
                            pointer = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(del),
                            handle = handle,
                        };
                    }
                }

            }

            [INLINE(256)]
            private static System.Type GetItemType(in UnsafeEntityConfig config, ComponentsStorageLink.Item item) {
                uint typeId = 0u;
                if (item.type == 0) {
                    typeId = config.data.typeIds[item.index];
                } else if (item.type == 1) {
                    typeId = config.dataShared.typeIds[item.index];
                } else if (item.type == 2) {
                    typeId = config.staticData.typeIds[item.index];
                }
                StaticTypesLoadedManaged.allLoadedTypes.TryGetValue(typeId, out var type);
                return type;
            }

            [INLINE(256)]
            private static uint GetTypeId(in UnsafeEntityConfig config, ComponentsStorageLink.Item item) {
                var index = item.index;
                if (item.type == 0) return config.data.typeIds[index];
                if (item.type == 1) return config.dataShared.typeIds[index];
                if (item.type == 2) return config.staticData.typeIds[index];
                return uint.MaxValue;
            }

            [INLINE(256)]
            private static safe_ptr<byte> GetData(in UnsafeEntityConfig config, ComponentsStorageLink.Item item) {
                var index = item.index;
                if (item.type == 0) return config.data.GetComponentPtr(index);
                if (item.type == 1) return config.dataShared.GetComponentPtr(index);
                if (item.type == 2) return (safe_ptr)Components.ReadUnknownType(config.staticData.staticDataEnt.World.state, config.staticData.typeIds[index], entId: config.staticData.staticDataEnt.id, gen: config.staticData.staticDataEnt.gen, out _);
                return default;
            }

            [INLINE(256)]
            public void Apply(in UnsafeEntityConfig config, in Ent ent, Config.JoinOptions options) {

                var state = ent.World.state;
                for (uint i = 0u; i < this.count; ++i) {
                    var typeId = GetTypeId(in config, this.data[i]);
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
                    var data = elemSize == 0u ? new safe_ptr<byte>() : GetData(in config, this.data[i]);
                    this.functionPointers[i].Call(in config, data.ptr, in ent);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                for (uint i = 0u; i < this.count; ++i) {
                    this.functionPointers[i].Dispose();
                }
                
                _free(this.data);
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
                    WorldAspectStorage.Initialize(ent.worldId, this.typeIds[i], this.sizes[i]);
                    UnsafeAspectsStorage.SetAspect(state, in ent, this.typeIds[i]);
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
            internal readonly safe_ptr<uint> typeIds;
            private readonly uint count;

            public StaticData(in Ent ent, EntityConfig sourceConfig, in UnsafeEntityConfig config) {
                
                this.staticDataEnt = ent;

                this.count = (uint)sourceConfig.staticData.components.Length;
                this.typeIds = _makeArray<uint>(this.count, false);
                
                for (int i = 0; i < sourceConfig.staticData.components.Length; ++i) {
                    var comp = sourceConfig.staticData.components[i];
                    var type = comp.GetType();
                    StaticTypesLoadedManaged.typeToId.TryGetValue(type, out var typeId);
                    this.typeIds[i] = typeId;
                    StaticTypesGroups.tracker.TryGetValue(type, out var trackerIndex);
                    var groupId = trackerIndex == 0 ? 0u : StaticTypes.tracker.Get(trackerIndex);
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
                    Batches.Set_INTERNAL(typeId, in ent);
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

            this = default;
            this.id = id > 0u || autoRegisterConfig == false ? id : EntityConfigRegistry.Register(config, out _);
            this.data = new Data(ref staticDataEnt.World.state.ptr->allocator, config.data.components, config.maskable == true ? config.data.masks : null);
            this.dataShared = new SharedData(config.sharedData.components);
            this.aspects = new Aspect(config.aspects);
            this.collectionsData = new CollectionsData(config.collectionsData);
            
            this.baseConfig = default;
            if (config.baseConfig is not null) {
                this.baseConfig = _make(new UnsafeEntityConfig(config.baseConfig, staticDataEnt: staticDataEnt, autoRegisterConfig: false));
            }

            this.staticData = new StaticData(staticDataEnt, config, in this);
            
            this.dataInitialize = new DataInitialize(in this, config.dataInitialize);

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
            this.staticData.Apply(in this, in ent, options);
            this.dataShared.Apply(in this, in ent, options);
            this.dataInitialize.Apply(in this, in ent, options);
            
        }

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

            var q = new Unity.Collections.NativeQueue<UnsafeEntityConfig>(Unity.Collections.Allocator.Temp);
            q.Enqueue(this);
            while (q.Count > 0) {
                var config = q.Dequeue();
                config.aspects.Dispose();
                config.data.Dispose();
                config.dataShared.Dispose();
                config.dataInitialize.Dispose();
                config.collectionsData.Dispose();
                if (config.baseConfig.ptr != null) {
                    q.Enqueue(*config.baseConfig.ptr);
                    _free(config.baseConfig);
                }
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
