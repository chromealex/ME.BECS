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

    public struct AspectTypeInfoCounter {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoCounter>();

    }

    public struct AspectTypeInfoId<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<uint> typeIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoId<T>>();

    }

    public struct AspectTypeInfoWith<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> withBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfoWith<T>>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10401);

    }

    public struct AspectTypeInfo<T> where T : unmanaged, IAspect {

        public static ref ME.BECS.Internal.Array<uint> with => ref AspectTypeInfoWith<T>.withBurst.Data;
        public static ref uint typeId => ref AspectTypeInfoId<T>.typeIdBurst.Data;

        [INLINE(256)]
        public static void Validate() {

            if (typeId == 0u) {
                typeId = ++AspectTypeInfoCounter.value.Data;
            }
            
        }

    }

    public static unsafe class EntAspectsExt {

        [INLINE(256)]
        public static void Set<T>(in this Ent ent) where T : unmanaged, IAspect {

            var world = ent.World;
            for (uint i = 0u; i < AspectTypeInfo<T>.with.Length; ++i) {

                var typeId = AspectTypeInfo<T>.with.Get(i);
                var has = world.state->components.HasUnknownType(world.state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    world.state->components.SetUnknownType(world.state, typeId, StaticTypes.groups.Get(typeId), in ent, (void*)StaticTypes.defaultValues.Get(typeId));
                    world.state->batches.Set_INTERNAL(typeId, ent.id, world.state);
                }

            }

        }

    }

    public unsafe struct AspectsStorage {

        public struct Aspect {

            public MemPtr constructedAspect;
            public ushort version;

        }

        public MemArray<Aspect> list;
        
        public uint GetReservedSizeInBytes(State* state) {

            if (this.list.isCreated == false) return 0u;

            return this.list.GetReservedSizeInBytes();

        }

        [INLINE(256)]
        public static AspectsStorage Create(State* state) {
            var aspectsCount = AspectTypeInfoCounter.value.Data + 1u;
            var storage = new AspectsStorage() {
                list = new MemArray<Aspect>(ref state->allocator, aspectsCount),
            };
            
            return storage;
        }

        [INLINE(256)]
        public ref T Initialize<T>(State* state) where T : unmanaged, IAspect {
            
            var typeId = AspectTypeInfo<T>.typeId;
            ref var item = ref this.list[state, typeId];
            if (item.constructedAspect.IsValid() == false) {
                item = new Aspect() {
                    constructedAspect = MemoryAllocatorExt.Alloc(ref state->allocator, sizeof(T)),
                    version = state->allocator.version,
                };
            }

            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in item.constructedAspect);

        }

    }

}