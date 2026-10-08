namespace ME.BECS {
    
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Supplies query with metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsageAttribute(System.AttributeTargets.Field)]
    public class QueryWithAttribute : System.Attribute {}
    
    /// <summary>
    /// Defines the operations required by aspect data.
    /// </summary>
    public interface IAspectData {}

    /// <summary>
    /// Provides typed access to the entity components used for aspect data ptr.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct AspectDataPtr<T> : IAspectData where T : unmanaged, IComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public RefRW<T> value;
        /// <summary>
        /// Value ro used by <c>AspectDataPtr</c>.
        /// </summary>
        public RefRO<T> valueRO;
        
        /// <summary>
        /// Initializes <c>AspectDataPtr</c> from the supplied world.
        /// </summary>
        public AspectDataPtr(in World world) {
            this.value = world.state.ptr->components.GetRW<T>(world.state, world.id);
            this.valueRO = world.state.ptr->components.GetRO<T>(world.state, world.id);
        }

        /// <summary>
        /// Returns the requested entry from aspect data ptr.
        /// </summary>
        [INLINE(256)]
        [SafetyCheck(RefOp.ReadWrite)] public readonly ref T Get(uint entId, ushort gen) {
            return ref this.value.Get(entId, gen);
        }

        /// <summary>
        /// Returns or throw.
        /// </summary>
        [INLINE(256)]
        [SafetyCheck(RefOp.ReadWrite)] public readonly ref T GetOrThrow(uint entId, ushort gen) {
            return ref this.value.GetOrThrow(entId, gen);
        }

        /// <summary>
        /// Reads the requested value from aspect data ptr.
        /// </summary>
        [INLINE(256)]
        [SafetyCheck(RefOp.ReadOnly)] public readonly ref readonly T Read(uint entId, ushort gen) {
            return ref this.valueRO.Read(entId, gen);
        }

    }
    
    /// <summary>
    /// Defines aspect type info loaded managed state and operations.
    /// </summary>
    [IgnoreProfiler]
    public struct AspectTypeInfoLoadedManaged {

        /// <summary>
        /// Loaded types used by <c>AspectTypeInfoLoadedManaged</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        /// <summary>
        /// Type to id used to locate the associated entry.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<System.Type, uint> typeToId = new System.Collections.Generic.Dictionary<System.Type, uint>();

    }
    
    /// <summary>
    /// Stores aspect type info for the associated runtime API.
    /// </summary>
    [IgnoreProfiler]
    public struct AspectTypeInfo {

        /// <summary>
        /// Counter storage accessed by Burst-compiled code.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfo>();
        /// <summary>
        /// Counter tracking the associated quantity.
        /// </summary>
        public static ref uint counter => ref counterBurst.Data;

        /// <summary>
        /// Sizes burst used by <c>AspectTypeInfo</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sizesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10402);
        /// <summary>
        /// Sizes used by <c>AspectTypeInfo</c>.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> sizes => ref sizesBurst.Data;

        /// <summary>
        /// With burst used by <c>AspectTypeInfo</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>> withBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10401);
        /// <summary>
        /// With used by <c>AspectTypeInfo</c>.
        /// </summary>
        public static ref ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>> with => ref withBurst.Data;
        
    }

    /// <summary>
    /// Defines aspect type info ID state and operations.
    /// </summary>
    [IgnoreProfiler]
    public struct AspectTypeInfoId<T> where T : unmanaged, IAspect {

        /// <summary>
        /// Type id burst used by <c>AspectTypeInfoId</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> typeIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoId<T>>();

    }
    
    /// <summary>
    /// Stores aspect type info for the associated runtime API.
    /// </summary>
    [IgnoreProfiler]
    public struct AspectTypeInfo<T> where T : unmanaged, IAspect {

        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        public static ref uint typeId => ref AspectTypeInfoId<T>.typeIdBurst.Data;

        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
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

    /// <summary>
    /// Stores and indexes world aspect entries.
    /// </summary>
    [IgnoreProfiler]
    public struct WorldAspectStorage {

        /// <summary>
        /// Storage containing the associated data.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Internal.Array<UnsafeAspectsStorage>> storage = Unity.Burst.SharedStatic<Internal.Array<UnsafeAspectsStorage>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldAspectStorage>(TAlign<Internal.Array<UnsafeAspectsStorage>>.align, 109L);

        /// <summary>
        /// Adds world.
        /// </summary>
        [INLINE(256)]
        public static void AddWorld(in World world) {
            
            storage.Data.Resize(world.id + 1u);
            storage.Data.Get(world.id) = UnsafeAspectsStorage.Create();

        }

        /// <summary>
        /// Releases the resources registered for the specified world.
        /// </summary>
        [INLINE(256)]
        public static void DisposeWorld(in World world) {

            if (world.id >= storage.Data.Length) return;
            storage.Data.Get(world.id).Dispose();

        }

        /// <summary>
        /// Initializes world aspect storage state from the supplied context.
        /// </summary>
        [INLINE(256)]
        public static ref T Initialize<T>(ushort worldId) where T : unmanaged, IAspect {
            
            return ref InitializeObj<T>(worldId);

        }

        /// <summary>
        /// Initializes obj.
        /// </summary>
        [INLINE(256)]
        public static ref T InitializeObj<T>(ushort worldId) where T : unmanaged, IAspect {
            
            return ref storage.Data.Get(worldId).Initialize<T>();

        }

        /// <summary>
        /// Initializes world aspect storage state from the supplied context.
        /// </summary>
        [INLINE(256)]
        public static safe_ptr Initialize(ushort worldId, uint typeId, uint size) {
            
            return storage.Data.Get(worldId).Initialize(typeId, size);

        }
        
    }

    /// <summary>
    /// Stores and indexes unsafe aspects entries.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct UnsafeAspectsStorage {

        /// <summary>
        /// Defines aspect state and operations for <c>UnsafeAspectsStorage</c>.
        /// </summary>
        public struct Aspect {

            /// <summary>
            /// Constructed aspect used by <c>UnsafeAspectsStorage.Aspect</c>.
            /// </summary>
            public safe_ptr constructedAspect;
            /// <summary>
            /// Spin lock protecting concurrent access to this state.
            /// </summary>
            public LockSpinner lockSpinner;

            /// <summary>
            /// Releases the resources owned by this aspect instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                _free(this.constructedAspect, Constants.ALLOCATOR_PERSISTENT);
            }

        }

        /// <summary>
        /// List storage used by this instance.
        /// </summary>
        public Unity.Collections.NativeArray<Aspect> list;
        
        /// <summary>
        /// Creates <c>UnsafeAspectsStorage</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static UnsafeAspectsStorage Create() {
            var aspectsCount = AspectTypeInfo.counter + 1u;
            var storage = new UnsafeAspectsStorage() {
                list = new Unity.Collections.NativeArray<Aspect>((int)aspectsCount, Constants.ALLOCATOR_PERSISTENT),
            };
            return storage;
        }

        /// <summary>
        /// Releases the resources owned by this unsafe aspects storage instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            foreach (var item in this.list) {
                item.Dispose();
            }
            this.list.Dispose();
        }

        /// <summary>
        /// Initializes unsafe aspects storage state from the supplied context.
        /// </summary>
        [INLINE(256)][CodeGeneratorIgnore]
        public ref T Initialize<T>() where T : unmanaged, IAspect {
            
            var typeId = AspectTypeInfo<T>.typeId;
            return ref *(T*)this.Initialize(typeId, TSize<T>.size).ptr;
            
        }

        /// <summary>
        /// Initializes unsafe aspects storage state from the supplied context.
        /// </summary>
        [INLINE(256)]
        public safe_ptr Initialize(uint typeId, uint size) {
            
            var item = (Aspect*)((byte*)this.list.GetUnsafePtr() + TSize<Aspect>.size * typeId);
            if (item->constructedAspect.ptr == null) {
                item->lockSpinner.Lock();
                if (item->constructedAspect.ptr == null) {
                    item->constructedAspect = _make(size, TAlign<byte>.alignInt, Constants.ALLOCATOR_PERSISTENT);
                }
                item->lockSpinner.Unlock();
            }

            return item->constructedAspect;

        }

        /// <summary>
        /// Sets aspect.
        /// </summary>
        [INLINE(256)]
        public static void SetAspect(safe_ptr<State> state, in Ent ent, uint aspectTypeId) {

            for (uint i = 0u; i < AspectTypeInfo.with.Get(aspectTypeId).Length; ++i) {

                var typeId = AspectTypeInfo.with.Get(aspectTypeId).Get(i);
                if (StaticTypesAutoDestroy.Is(typeId) == true) {
                    AutoDestroyRegistry.Destroy(state, in ent, typeId);
                    AutoDestroyRegistry.Add(state, in ent, typeId);
                }
                var has = Components.HasUnknownType(state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    Components.SetUnknownType(state, typeId, StaticTypes.tracker.Get(typeId), in ent, (void*)StaticTypes.defaultValues.Get(typeId));
                    Batches.Set_INTERNAL(typeId, in ent);
                }

            }

        }

    }

}