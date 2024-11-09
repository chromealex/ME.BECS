namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public class QueryWithAttribute : System.Attribute {}
    
    public interface IAspectData {}

    public unsafe struct AspectDataPtr<T> : IAspectData where T : unmanaged, IComponent {

        public RefRW<T> value;
        
        public AspectDataPtr(in World world) {
            this.value = world.state->components.GetRW<T>(world.state, world.id);
        }

        [INLINE(256)]
        public readonly ref T Get(uint entId, ushort gen) {
            return ref this.value.Get(entId, gen);
        }

        [INLINE(256)]
        public readonly ref readonly T Read(uint entId, ushort gen) {
            return ref this.value.Read(entId, gen);
        }

    }
    public struct AspectTypeInfoLoadedManaged {

        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<System.Type, uint> typeToId = new System.Collections.Generic.Dictionary<System.Type, uint>();

    }
    
    public struct AspectTypeInfo {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfo>();
        public static ref uint counter => ref counterBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sizesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10402);
        public static ref ME.BECS.Internal.Array<uint> sizes => ref sizesBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>> withBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10401);
        public static ref ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>> with => ref withBurst.Data;
        
    }

    public struct AspectTypeInfoId<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<uint> typeIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoId<T>>();

    }

    public struct AspectTypeInfo<T> where T : unmanaged, IAspect {

        public static ref uint typeId => ref AspectTypeInfoId<T>.typeIdBurst.Data;

        [INLINE(256)]
        public static void Validate() {

            if (typeId == 0u) {
                typeId = ++AspectTypeInfo.counter;
                AspectTypeInfoLoadedManaged.loadedTypes.Add(typeId, typeof(T));
                AspectTypeInfoLoadedManaged.typeToId.Add(typeof(T), typeId);
            }
            
            AspectTypeInfo.sizes.Resize(typeId + 1u);
            AspectTypeInfo.sizes.Get(typeId) = TSize<T>.size;

            AspectTypeInfo.with.Resize(typeId + 1u);

        }

    }

    public unsafe struct AspectsStorage {

        public struct Aspect {

            public MemPtr constructedAspect;
            public LockSpinner lockSpinner;
            public ushort version;

        }

        public MemArray<Aspect> list;
        
        public uint GetReservedSizeInBytes(State* state) {

            if (this.list.IsCreated == false) return 0u;

            return this.list.GetReservedSizeInBytes();

        }

        [INLINE(256)]
        public static AspectsStorage Create(State* state) {
            var aspectsCount = AspectTypeInfo.counter + 1u;
            var storage = new AspectsStorage() {
                list = new MemArray<Aspect>(ref state->allocator, aspectsCount),
            };
            
            return storage;
        }

        [INLINE(256)]
        public ref T Initialize<T>(State* state) where T : unmanaged, IAspect {
            
            var typeId = AspectTypeInfo<T>.typeId;
            return ref *(T*)this.Initialize(state, typeId, TSize<T>.size);
            
        }

        [INLINE(256)]
        public byte* Initialize(State* state, uint typeId, uint size) {
            
            ref var item = ref this.list[state, typeId];
            if (item.constructedAspect.IsValid() == false) {
                item.lockSpinner.Lock();
                if (item.constructedAspect.IsValid() == false) {
                    item.constructedAspect = state->allocator.Alloc(size);
                    item.version = state->allocator.version;
                }
                item.lockSpinner.Unlock();
            }

            return state->allocator.GetUnsafePtr(in item.constructedAspect);

        }

        [INLINE(256)]
        public void SetAspect(State* state, in Ent ent, uint aspectTypeId) {

            for (uint i = 0u; i < AspectTypeInfo.with.Get(aspectTypeId).Length; ++i) {

                var typeId = AspectTypeInfo.with.Get(aspectTypeId).Get(i);
                var has = state->components.HasUnknownType(state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    state->components.SetUnknownType(state, typeId, StaticTypes.groups.Get(typeId), in ent, (void*)StaticTypes.defaultValues.Get(typeId));
                    state->batches.Set_INTERNAL(typeId, in ent, state);
                }

            }

        }

    }

}