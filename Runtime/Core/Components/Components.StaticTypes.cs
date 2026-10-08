
namespace ME.BECS {

    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides helper operations for static.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticUtils {

        private const uint FILL_SIZE = 2048 * 10;

        /// <summary>
        /// Zero used by <c>StaticUtils</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<safe_ptr> zero = Unity.Burst.SharedStatic<safe_ptr>.GetOrCreate<StaticUtils>();

        /// <summary>
        /// Initializes static utils state from the supplied context.
        /// </summary>
        public static void Initialize() {

            zero.Data = _calloc(FILL_SIZE);

        }

    }

    /// <summary>
    /// Caches groups metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesGroups {

        /// <summary>
        /// Groups used to partition the associated entries.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<System.Type, ushort> groups = new System.Collections.Generic.Dictionary<System.Type, ushort>();
        /// <summary>
        /// Next group id used to locate the associated entry.
        /// </summary>
        public static ushort nextGroupId;

        /// <summary>
        /// Change tracker used to decide whether processing is required.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<System.Type, ushort> tracker = new System.Collections.Generic.Dictionary<System.Type, ushort>();
        /// <summary>
        /// Next track id used to locate the associated entry.
        /// </summary>
        public static ushort nextTrackId;

    }

    /// <summary>
    /// Caches groups burst metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesGroupsBurst {

        /// <summary>
        /// Maximum ID burst.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> maxIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesGroupsBurst>();
        /// <summary>
        /// Maximum ID.
        /// </summary>
        public static ref uint maxId => ref maxIdBurst.Data;
        
    }

    /// <summary>
    /// Caches tracked burst metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesTrackedBurst {

        /// <summary>
        /// Maximum ID burst.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> maxIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesTrackedBurst>();
        /// <summary>
        /// Maximum ID.
        /// </summary>
        public static ref uint maxId => ref maxIdBurst.Data;
        
    }

    /// <summary>
    /// Caches  metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypes {

        /// <summary>
        /// Counter storage accessed by Burst-compiled code.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypes>();
        /// <summary>
        /// Counter tracking the associated quantity.
        /// </summary>
        public static ref uint counter => ref counterBurst.Data;

        /// <summary>
        /// Sizes burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sizesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10201);
        /// <summary>
        /// Sizes used by <c>StaticTypes</c>.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> sizes => ref sizesBurst.Data;

        /// <summary>
        /// Groups burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> groupsBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10202);
        /// <summary>
        /// Groups used to partition the associated entries.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> groups => ref groupsBurst.Data;

        /// <summary>
        /// Shared type id burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sharedTypeIdBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10203);
        /// <summary>
        /// Shared type id used to locate the associated entry.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> sharedTypeId => ref sharedTypeIdBurst.Data;

        /// <summary>
        /// Static type id burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> staticTypeIdBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10204);
        /// <summary>
        /// Static type id used to locate the associated entry.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> staticTypeId => ref staticTypeIdBurst.Data;

        /// <summary>
        /// Default values burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<System.IntPtr>> defaultValuesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<System.IntPtr>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<System.IntPtr>>.align, 10205);
        /// <summary>
        /// Default values used by <c>StaticTypes</c>.
        /// </summary>
        public static ref ME.BECS.Internal.Array<System.IntPtr> defaultValues => ref defaultValuesBurst.Data;

        /// <summary>
        /// Collections count burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> collectionsCountBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10206);
        /// <summary>
        /// Collections count for the associated storage.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> collectionsCount => ref collectionsCountBurst.Data;

        /// <summary>
        /// Tracker burst used by <c>StaticTypes</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> trackerBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10207);
        /// <summary>
        /// Change tracker used to decide whether processing is required.
        /// </summary>
        public static ref ME.BECS.Internal.Array<uint> tracker => ref trackerBurst.Data;

        /// <summary>
        /// Provides the <c>Dispose</c> callback; this implementation performs no work.
        /// </summary>
        public static void Dispose() {
        }
        
        /// <summary>
        /// Sets tracker.
        /// </summary>
        public static void SetTracker(uint count) {
            tracker.Resize(StaticTypes.counter + 1u);
        }

        /// <summary>
        /// Tests the entity or type static-state flag.
        /// </summary>
        public static bool IsStatic(uint id) {
            if (id >= StaticTypes.staticTypeId.Length) return false;
            return StaticTypes.staticTypeId.Get(id) > 0u;
        }

    }

    /// <summary>
    /// Defines static shared types state and operations.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticSharedTypes {

        /// <summary>
        /// Counter storage accessed by Burst-compiled code.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticSharedTypes>();
        /// <summary>
        /// Counter tracking the associated quantity.
        /// </summary>
        public static ref uint counter => ref counterBurst.Data;
        
    }

    /// <summary>
    /// Defines static static types state and operations.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticStaticTypes {

        /// <summary>
        /// Counter storage accessed by Burst-compiled code.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticStaticTypes>();
        /// <summary>
        /// Counter tracking the associated quantity.
        /// </summary>
        public static ref uint counter => ref counterBurst.Data;
        
    }

    /// <summary>
    /// Caches loaded managed metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesLoadedManaged {

        /// <summary>
        /// All loaded types used by <c>StaticTypesLoadedManaged</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> allLoadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        /// <summary>
        /// Loaded types used by <c>StaticTypesLoadedManaged</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        /// <summary>
        /// Loaded static types used by <c>StaticTypesLoadedManaged</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedStaticTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        /// <summary>
        /// Type to id used to locate the associated entry.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<System.Type, uint> typeToId = new System.Collections.Generic.Dictionary<System.Type, uint>();
        /// <summary>
        /// Loaded shared types used by <c>StaticTypesLoadedManaged</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedSharedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        /// <summary>
        /// Loaded shared types custom hash used by <c>StaticTypesLoadedManaged</c>.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<uint, bool> loadedSharedTypesCustomHash = new System.Collections.Generic.Dictionary<uint, bool>();

    }

    /// <summary>
    /// Defines shared static default state and operations.
    /// </summary>
    [IgnoreProfiler]
    public struct SharedStaticDefault<T> where T : unmanaged {

        private static readonly T ptr;
        
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public ref readonly T Data {
            [INLINE(256)]
            get => ref ptr;
        }

    }

    /// <summary>
    /// Caches group ID metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesGroupId<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesGroupId<T>>();

    }

    /// <summary>
    /// Caches track ID metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesTrackId<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesTrackId<T>>();

    }

    /// <summary>
    /// Caches ID metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesId<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesId<T>>();

    }

    /// <summary>
    /// Marks entities with the static types is state.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesIsTag<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesIsTag<T>>();

    }

    /// <summary>
    /// Caches is static metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesIsStatic<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesIsStatic<T>>();

    }

    /// <summary>
    /// Caches shared type ID metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesSharedTypeId<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesSharedTypeId<T>>();

    }

    /// <summary>
    /// Caches shared custom hash metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesSharedCustomHash<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesSharedCustomHash<T>>();

    }

    /// <summary>
    /// Caches static type ID metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesStaticTypeId<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesStaticTypeId<T>>();

    }

    /// <summary>
    /// Caches shared metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesShared<T> where T : unmanaged, IComponentShared {

        /// <summary>
        /// Provides the <c>AOT</c> callback; this implementation performs no work.
        /// </summary>
        public static void AOT() {
        }

    }

    /// <summary>
    /// Caches static metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesStatic<T> where T : unmanaged, IConfigComponentStatic {

        /// <summary>
        /// References generic code paths required by ahead-of-time compilation.
        /// </summary>
        public static unsafe void AOT() {
            UnsafeEntityConfig.StaticData.MethodCaller<T>.Call(default, null, default);
        }

    }

    /// <summary>
    /// Tracks initialization callbacks required by configuration components.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct ConfigInitializeTypes<T> where T : unmanaged, IConfigInitialize {

        /// <summary>
        /// References generic code paths required by ahead-of-time compilation.
        /// </summary>
        public static void AOT() {
            UnsafeEntityConfig.DataInitialize.MethodCaller<T>.Call(default, null, default);
        }

    }

    /// <summary>
    /// Caches has default value metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesHasDefaultValue<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesHasDefaultValue<T>>();

    }

    /// <summary>
    /// Defines static default value state and operations.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticDefaultValue<T> where T : unmanaged {

        /// <summary>
        /// Value used when no explicit value is supplied.
        /// </summary>
        public static readonly T defaultValue = default;

    }

    /// <summary>
    /// Caches destroy registry metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesDestroyRegistry {

        /// <summary>
        /// Registry used to resolve the associated identifiers.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Internal.Array<Unity.Burst.FunctionPointer<AutoDestroyRegistry.DestroyDelegate>>> registry = Unity.Burst.SharedStatic<Internal.Array<Unity.Burst.FunctionPointer<AutoDestroyRegistry.DestroyDelegate>>>.GetOrCreate<StaticTypesDestroyRegistry>();

    }

    /// <summary>
    /// Caches auto destroy metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesAutoDestroy<T> {

        /// <summary>
        /// Registry used to resolve the associated identifiers.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<bool> registry = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesAutoDestroy<T>>();

    }

    /// <summary>
    /// Caches auto destroy metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesAutoDestroy {

        /// <summary>
        /// Registry used to resolve the associated identifiers.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Internal.Array<bbool>> registry = Unity.Burst.SharedStatic<Internal.Array<bbool>>.GetOrCreate<StaticTypesAutoDestroy>();

        /// <summary>
        /// Tests whether the value matches the requested type or condition.
        /// </summary>
        [INLINE(256)]
        public static bool Is(uint typeId) {
            if (typeId >= registry.Data.Length) return false;
            return registry.Data.Get(typeId);
        }

    }

    /// <summary>
    /// Caches names metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesNames<T> {

        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Unity.Collections.FixedString512Bytes> name = Unity.Burst.SharedStatic<Unity.Collections.FixedString512Bytes>.GetOrCreate<StaticTypesNames<T>>();

    }

    /// <summary>
    /// Caches default metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypesDefault<T> where T : unmanaged {

        /// <summary>
        /// Value used when no explicit value is supplied.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<T> defaultValue = Unity.Burst.SharedStatic<T>.GetOrCreate<StaticTypesDefault<T>>();

    }

    /// <summary>
    /// Caches  metadata for registered component types.
    /// </summary>
    [IgnoreProfiler]
    public struct StaticTypes<T> where T : unmanaged, IComponentBase {

        private static readonly T defaultZero = default;

        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public static ref Unity.Collections.FixedString512Bytes name => ref StaticTypesNames<T>.name.Data;
        /// <summary>
        /// Static type id used to locate the associated entry.
        /// </summary>
        public static ref uint staticTypeId => ref StaticTypesStaticTypeId<T>.value.Data;
        /// <summary>
        /// Shared type id used to locate the associated entry.
        /// </summary>
        public static ref uint sharedTypeId => ref StaticTypesSharedTypeId<T>.value.Data;
        /// <summary>
        /// Indicates has shared custom hash.
        /// </summary>
        public static ref bool hasSharedCustomHash => ref StaticTypesSharedCustomHash<T>.value.Data;
        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        public static ref uint typeId => ref StaticTypesId<T>.value.Data;
        /// <summary>
        /// Indicates is tag.
        /// </summary>
        public static ref bool isTag => ref StaticTypesIsTag<T>.value.Data;
        /// <summary>
        /// Indicates is static.
        /// </summary>
        public static ref bool isStatic => ref StaticTypesIsStatic<T>.value.Data;
        /// <summary>
        /// Group id used to locate the associated entry.
        /// </summary>
        public static ref uint groupId => ref StaticTypesGroupId<T>.value.Data;
        /// <summary>
        /// Tracker index used to locate the associated entry.
        /// </summary>
        public static ref uint trackerIndex => ref StaticTypesTrackId<T>.value.Data;
        /// <summary>
        /// Indicates is tracked.
        /// </summary>
        public static bool isTracked => StaticTypes.tracker.Get(StaticTypes<T>.typeId) > 0u;

        /// <summary>
        /// Value used when no explicit value is supplied.
        /// </summary>
        public static unsafe ref readonly T defaultValue {
            get {
                if (StaticTypesHasDefaultValue<T>.value.Data == true) {
                    return ref *(T*)StaticTypes.defaultValues.Get(StaticTypes<T>.typeId);
                }

                return ref defaultZero;
            }
        }

        /// <summary>
        /// Native pointer or typed storage accessor for default value.
        /// </summary>
        public static unsafe safe_ptr defaultValuePtr {
            get {
                if (StaticTypesHasDefaultValue<T>.value.Data == true) {
                    return (safe_ptr)(T*)StaticTypes.defaultValues.Get(StaticTypes<T>.typeId);
                }

                return default;
            }
        }

        /// <summary>
        /// Default value get used by <c>StaticTypes</c>.
        /// </summary>
        public static unsafe ref T defaultValueGet {
            get {
                if (StaticTypesHasDefaultValue<T>.value.Data == true) {
                    return ref *(T*)StaticTypes.defaultValues.Get(StaticTypes<T>.typeId);
                }

                return ref StaticTypesDefault<T>.defaultValue.Data;
            }
        }

        /// <summary>
        /// References generic code paths required by ahead-of-time compilation.
        /// </summary>
        public static unsafe void AOT() {
            UnsafeEntityConfig.Data.MethodCaller<T>.Call(default, null, default);
            UnsafeEntityConfig.MethodComponentMaskCaller<T>.Call(default, null, null, default, default);
        }

        /// <summary>
        /// Sets collections count.
        /// </summary>
        [INLINE(256)]
        public static void SetCollectionsCount(uint count) {

            StaticTypes.collectionsCount.Get(StaticTypes<T>.typeId) = count;
            
        }

        /// <summary>
        /// Tracks version.
        /// </summary>
        [INLINE(256)]
        public static void TrackVersion() {

            if (StaticTypes<T>.trackerIndex == 0u) {
                StaticTypes<T>.trackerIndex = ++StaticTypesTrackedBurst.maxId;
                StaticTypes.tracker.Get(StaticTypes<T>.typeId) = StaticTypes<T>.trackerIndex;
            }
            
            if (StaticTypesGroups.tracker.TryGetValue(typeof(T), out var groupId) == false) {
                groupId = ++StaticTypesGroups.nextTrackId;
                StaticTypesGroups.tracker.Add(typeof(T), groupId);
            }

        }

        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        [INLINE(256)]
        public static void Validate(bool isTag, bool isStatic = false) {

            if (typeId == 0u && typeof(T) != typeof(TNull)) {
                StaticTypes<T>.typeId = ++StaticTypes.counter;
                StaticTypes<T>.isTag = isTag;
                StaticTypes<T>.isStatic = isStatic;
                var typeId = (StaticTypes<T>.typeId + 1u);// * 2u;
                StaticTypes.sizes.Resize(typeId);
                StaticTypes.sizes.Get(StaticTypes<T>.typeId) = isTag == true ? 0u : TSize<T>.size;
                StaticTypes.groups.Resize(typeId);
                StaticTypes.groups.Get(StaticTypes<T>.typeId) = groupId;
                StaticTypes.defaultValues.Resize(typeId);
                StaticTypesNames<T>.name.Data = typeof(T).Name;
                StaticTypes<T>.AddTypeToCache();
            }

        }

        /// <summary>
        /// Sets default value.
        /// </summary>
        [INLINE(256)]
        public static unsafe void SetDefaultValue(T data) {

            StaticTypesHasDefaultValue<T>.value.Data = true;
            var defaultValuePtr = (safe_ptr<T>)_make(TSize<T>.sizeInt, TAlign<T>.alignInt, Constants.ALLOCATOR_DOMAIN);
            *defaultValuePtr.ptr = data;
            StaticTypes.defaultValues.Resize(StaticTypes<T>.typeId + 1u);
            StaticTypes.defaultValues.Get(StaticTypes<T>.typeId) = (System.IntPtr)defaultValuePtr.ptr;
            
        }

        /// <summary>
        /// Validates shared.
        /// </summary>
        [INLINE(256)]
        public static void ValidateShared(bool isTag, bool hasCustomHash) {

            Validate(isTag);

            if (sharedTypeId == 0u) {
                StaticTypes<T>.sharedTypeId = ++StaticSharedTypes.counter;
                StaticTypes<T>.hasSharedCustomHash = hasCustomHash;
                StaticTypes.sharedTypeId.Resize(StaticTypes<T>.typeId + 1u);
                StaticTypes.sharedTypeId.Get(StaticTypes<T>.typeId) = StaticTypes<T>.sharedTypeId;
                StaticTypes<T>.AddSharedTypeToCache(hasCustomHash);
            }

        }

        /// <summary>
        /// Validates static.
        /// </summary>
        [INLINE(256)]
        public static void ValidateStatic(bool isTag) {

            Validate(isTag);

            if (sharedTypeId == 0u) {
                StaticTypes<T>.staticTypeId = ++StaticStaticTypes.counter;
                StaticTypes.staticTypeId.Resize(StaticTypes<T>.typeId + 1u);
                StaticTypes.staticTypeId.Get(StaticTypes<T>.typeId) = StaticTypes<T>.staticTypeId;
            }

        }

        /// <summary>
        /// Applies group.
        /// </summary>
        [Unity.Burst.BurstDiscard]
        public static void ApplyGroup(System.Type groupType) {

            if (StaticTypesGroups.groups.TryGetValue(groupType, out var groupId) == false) {
                groupId = ++StaticTypesGroups.nextGroupId;
                StaticTypesGroups.groups.Add(groupType, groupId);
            }
            
            StaticTypes<T>.groupId = groupId;
            if (groupId > StaticTypesGroupsBurst.maxId) StaticTypesGroupsBurst.maxId = groupId;

        }

        /// <summary>
        /// Adds type to cache.
        /// </summary>
        [Unity.Burst.BurstDiscard]
        public static void AddTypeToCache() {
            if (typeof(IConfigComponentStatic).IsAssignableFrom(typeof(T)) == true) {
                StaticTypesLoadedManaged.loadedStaticTypes.Add(StaticTypes<T>.typeId, typeof(T));
            } else {
                StaticTypesLoadedManaged.loadedTypes.Add(StaticTypes<T>.typeId, typeof(T));
            }
            StaticTypesLoadedManaged.allLoadedTypes.Add(StaticTypes<T>.typeId, typeof(T));
            StaticTypesLoadedManaged.typeToId.Add(typeof(T), StaticTypes<T>.typeId);
        }

        /// <summary>
        /// Adds shared type to cache.
        /// </summary>
        [Unity.Burst.BurstDiscard]
        public static void AddSharedTypeToCache(bool hasCustomHash) {
            StaticTypesLoadedManaged.loadedSharedTypes.Add(StaticTypes<T>.sharedTypeId, typeof(T));
            StaticTypesLoadedManaged.loadedSharedTypesCustomHash.Add(StaticTypes<T>.typeId, hasCustomHash);
        }

    }

}