using Unity.Jobs;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;
    using static Cuts;

    public readonly unsafe struct UnsafeEntityConfig {

        private readonly struct SharedData<T> where T : class, IConfigComponentShared {

            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* hashes;

            [INLINE(256)]
            public SharedData(T[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                
                this.offsets = _makeArray<uint>(cnt);
                this.typeIds = _makeArray<uint>(cnt);
                this.hashes = _makeArray<uint>(cnt);
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

                for (int i = 0; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    _memcpy((void*)ptr, this.data + this.offsets[i], elemSize);
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    var typeId = this.typeIds[i];
                    var groupId = StaticTypes.groups.Get(typeId);
                    var dataSize = StaticTypes.sizes.Get(typeId);
                    var sharedTypeId = StaticTypes.sharedTypeId.Get(typeId);
                    var hash = this.hashes[i];
                    state->batches.SetShared(ent.id, groupId, data, dataSize, typeId, sharedTypeId, state, hash);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                _free(this.data, Constants.ALLOCATOR_PERSISTENT);
                CutsPool._freeArray(this.hashes, this.count);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);

            }

        }

        private readonly struct Data<T> where T : class {

            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;

            [INLINE(256)]
            public Data(T[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                this.offsets = _makeArray<uint>(cnt);
                this.typeIds = _makeArray<uint>(cnt);
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

                for (int i = 0; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    Cuts._memcpy((void*)ptr, this.data + this.offsets[i], elemSize);
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    var typeId = this.typeIds[i];
                    state->batches.Set(ent.id, ent.gen, typeId, data, state);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                _free(this.data);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);

            }

            [INLINE(256)]
            public bool TryRead<TComponent>(out TComponent data) where TComponent : unmanaged, IComponentStatic {

                data = default;
                var typeId = StaticTypes<TComponent>.typeId;
                for (uint i = 0; i < this.count; ++i) {
                    if (this.typeIds[i] == typeId) {
                        data = *(TComponent*)(this.data + this.offsets[i]);
                        return true;
                    }
                }

                return false;

            }

            [INLINE(256)]
            public bool Has<TComponent>() where TComponent : unmanaged, IComponentStatic {
                
                var typeId = StaticTypes<TComponent>.typeId;
                for (uint i = 0; i < this.count; ++i) {
                    if (this.typeIds[i] == typeId) {
                        return true;
                    }
                }

                return false;
                
            }

        }

        private readonly struct DataInitialize {

            public struct Func {

                public System.IntPtr pointer;
                public System.Runtime.InteropServices.GCHandle handle;

                public void Call(byte* comp, in Ent ent) {
                    var del = new Unity.Burst.FunctionPointer<MethodCallerDelegate>(this.pointer);
                    del.Invoke(comp, in ent);
                }

                public void Dispose() {
                    
                    // TODO: FREE
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
            public delegate void MethodCallerDelegate(void* component, in Ent ent);
            
            internal static class MethodCaller<T> where T : unmanaged, IConfigInitialize {

                [UnityEngine.Scripting.PreserveAttribute]
                [AOT.MonoPInvokeCallbackAttribute(typeof(MethodCallerDelegate))]
                public static void CallNoBurst(void* component, in Ent ent) {

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
                this.offsets = _makeArray<uint>(cnt);
                this.typeIds = _makeArray<uint>(cnt);
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

                for (int i = 0; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    Cuts._memcpy((void*)ptr, this.data + this.offsets[i], elemSize);
                    {
                        var caller = typeof(MethodCaller<>).MakeGenericType(comp.GetType());
                        var method = caller.GetMethod("CallNoBurst");
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

                for (uint i = 0; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    this.functionPointers[i].Call(data, in ent);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                for (uint i = 0; i < this.count; ++i) {
                    this.functionPointers[i].Dispose();
                }
                
                _free(this.data);
                CutsPool._freeArray(this.offsets, this.count);
                CutsPool._freeArray(this.typeIds, this.count);
                CutsPool._freeArray(this.functionPointers, this.count);

            }

        }

        [NativeDisableUnsafePtrRestriction]
        private readonly UnsafeEntityConfig* baseConfig;
        private readonly Data<IConfigComponent> data;
        private readonly SharedData<IConfigComponentShared> dataShared;
        private readonly DataInitialize dataInitialize;
        private readonly uint id;
        private readonly Ent staticDataEnt;

        [INLINE(256)]
        public UnsafeEntityConfig(EntityConfig config, uint id = 0u, Ent staticDataEnt = default) {
            
            this.id = id > 0u ? id : EntityConfigRegistry.Register(config, out _);
            this.data = new Data<IConfigComponent>(config.data.components);
            this.dataShared = new SharedData<IConfigComponentShared>(config.sharedData.components);
            this.dataInitialize = new DataInitialize(config.dataInitialize.components);
            this.staticDataEnt = staticDataEnt;
            var state = staticDataEnt.World.state;
            
            this.baseConfig = null;
            if (config.baseConfig != null) {
                this.baseConfig = _make(new UnsafeEntityConfig(config.baseConfig, staticDataEnt: staticDataEnt));
            }

            for (int i = 0; i < config.staticData.components.Length; ++i) {
                var comp = config.staticData.components[i];
                StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                var ptr = gcHandle.AddrOfPinnedObject();
                state->batches.Set(staticDataEnt.id, staticDataEnt.gen, typeId, (void*)ptr, staticDataEnt.World.state);
                gcHandle.Free();
            }

        }

        [INLINE(256)]
        public void Apply(in Ent ent) {

            if (this.IsValid() == false) {
                throw new System.Exception();
            }
            
            ent.Set(new EntityConfigComponent() {
                id = this.id,
            });

            this.data.Apply(ent);
            this.dataShared.Apply(ent);
            this.dataInitialize.Apply(ent);
            
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

            this.data.Dispose();
            this.dataShared.Dispose();
            this.dataInitialize.Dispose();
            if (this.baseConfig != null) this.baseConfig->Dispose();

        }

        [INLINE(256)]
        public bool IsValid() {
            return this.id > 0u;
        }

        [INLINE(256)]
        public bool HasStatic<T>() where T : unmanaged, IComponentStatic {

            var state = this.staticDataEnt.World.state;
            return state->components.Has<T>(state, this.staticDataEnt.id, this.staticDataEnt.gen, checkEnabled: false);

        }

        [INLINE(256)]
        public T ReadStatic<T>() where T : unmanaged, IComponentStatic {
            
            var state = this.staticDataEnt.World.state;
            return state->components.Read<T>(state, this.staticDataEnt.id, this.staticDataEnt.gen);

        }

    }

}