
namespace ME.BECS {

    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public struct StaticTypesGroups {

        public static System.Collections.Generic.Dictionary<System.Type, ushort> groups = new System.Collections.Generic.Dictionary<System.Type, ushort>();
        public static ushort nextGroupId;

    }

    public struct StaticTypesGroupsBurst {

        public static readonly Unity.Burst.SharedStatic<uint> maxIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesGroupsBurst>();
        public static ref uint maxId => ref maxIdBurst.Data;
        
    }

    public struct StaticTypes {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypes>();
        public static ref uint counter => ref counterBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sizesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10201);
        public static ref ME.BECS.Internal.Array<uint> sizes => ref sizesBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> groupsBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10202);
        public static ref ME.BECS.Internal.Array<uint> groups => ref groupsBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sharedTypeIdBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10203);
        public static ref ME.BECS.Internal.Array<uint> sharedTypeId => ref sharedTypeIdBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> staticTypeIdBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10204);
        public static ref ME.BECS.Internal.Array<uint> staticTypeId => ref staticTypeIdBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<System.IntPtr>> defaultValuesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<System.IntPtr>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<System.IntPtr>>.align, 10205);
        public static ref ME.BECS.Internal.Array<System.IntPtr> defaultValues => ref defaultValuesBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> collectionsCountBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<StaticTypes>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10206);
        public static ref ME.BECS.Internal.Array<uint> collectionsCount => ref collectionsCountBurst.Data;

    }

    public struct StaticSharedTypes {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticSharedTypes>();
        public static ref uint counter => ref counterBurst.Data;
        
    }

    public struct StaticStaticTypes {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticStaticTypes>();
        public static ref uint counter => ref counterBurst.Data;
        
    }

    public struct StaticTypesLoadedManaged {

        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> allLoadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedStaticTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<System.Type, uint> typeToId = new System.Collections.Generic.Dictionary<System.Type, uint>();
        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedSharedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<uint, bool> loadedSharedTypesCustomHash = new System.Collections.Generic.Dictionary<uint, bool>();

    }

    public struct SharedStaticDefault<T> where T : unmanaged {

        private static readonly T ptr;
        
        public ref readonly T Data {
            [INLINE(256)]
            get {
                return ref ptr;
            }
        }

    }

    public struct StaticTypesGroupId<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesGroupId<T>>();

    }

    public struct StaticTypesId<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesId<T>>();

    }

    public struct StaticTypesIsTag<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesIsTag<T>>();

    }

    public struct StaticTypesSharedTypeId<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesSharedTypeId<T>>();

    }

    public struct StaticTypesSharedCustomHash<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesSharedCustomHash<T>>();

    }

    public struct StaticTypesStaticTypeId<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticTypesStaticTypeId<T>>();

    }

    public struct StaticTypesShared<T> where T : unmanaged, IComponentShared {

        public static void AOT() {
            
        }

    }

    public struct StaticTypesStatic<T> where T : unmanaged, IConfigComponentStatic {

        public static unsafe void AOT() {
            UnsafeEntityConfig.StaticData.MethodCaller<T>.Call(default, null, default);
        }

    }

    public unsafe struct ConfigInitializeTypes<T> where T : unmanaged, IConfigInitialize {

        public static void AOT() {
            UnsafeEntityConfig.DataInitialize.MethodCaller<T>.Call(default, null, default);
        }

    }

    public struct StaticTypesHasDefaultValue<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<bool> value = Unity.Burst.SharedStatic<bool>.GetOrCreate<StaticTypesHasDefaultValue<T>>();

    }

    public struct StaticDefaultValue<T> where T : unmanaged {

        public static readonly T defaultValue = default;

    }

    public struct StaticTypesDestroyRegistry {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<System.IntPtr>> registry = Unity.Burst.SharedStatic<Internal.Array<System.IntPtr>>.GetOrCreate<StaticTypesDestroyRegistry>();

    }
    
    public struct StaticTypesDestroy<T> where T : unmanaged, IComponentDestroy {

        [INLINE(256)]
        public static unsafe void RegisterAutoDestroy(bool isTag) {

            var typeId = StaticTypes<T>.typeId;
            StaticTypesDestroyRegistry.registry.Data.Resize(typeId + 1);
            StaticTypesDestroyRegistry.registry.Data.Get(typeId) = Unity.Burst.BurstCompiler.CompileFunctionPointer<AutoDestroyRegistry.DestroyDelegate>(AutoDestroyRegistryStatic<T>.Destroy).Value;

        }

    }

    public struct StaticTypesNames<T> {

        public static readonly Unity.Burst.SharedStatic<Unity.Collections.FixedString512Bytes> name = Unity.Burst.SharedStatic<Unity.Collections.FixedString512Bytes>.GetOrCreate<StaticTypesNames<T>>();

    }

    public struct StaticTypesDefault<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<T> defaultValue = Unity.Burst.SharedStatic<T>.GetOrCreate<StaticTypesDefault<T>>();

    }

    public struct StaticTypes<T> where T : unmanaged, IComponentBase {

        private static readonly T defaultZero = default;

        public static ref Unity.Collections.FixedString512Bytes name => ref StaticTypesNames<T>.name.Data;
        public static ref uint staticTypeId => ref StaticTypesStaticTypeId<T>.value.Data;
        public static ref uint sharedTypeId => ref StaticTypesSharedTypeId<T>.value.Data;
        public static ref bool hasSharedCustomHash => ref StaticTypesSharedCustomHash<T>.value.Data;
        public static ref uint typeId => ref StaticTypesId<T>.value.Data;
        public static ref bool isTag => ref StaticTypesIsTag<T>.value.Data;
        public static ref uint groupId => ref StaticTypesGroupId<T>.value.Data;

        public static unsafe ref readonly T defaultValue {
            get {
                if (StaticTypesHasDefaultValue<T>.value.Data == true) {
                    return ref *(T*)StaticTypes.defaultValues.Get(StaticTypes<T>.typeId);
                }

                return ref defaultZero;
            }
        }

        public static unsafe safe_ptr defaultValuePtr {
            get {
                if (StaticTypesHasDefaultValue<T>.value.Data == true) {
                    return (safe_ptr)(T*)StaticTypes.defaultValues.Get(StaticTypes<T>.typeId);
                }

                return default;
            }
        }

        public static unsafe ref T defaultValueGet {
            get {
                if (StaticTypesHasDefaultValue<T>.value.Data == true) {
                    return ref *(T*)StaticTypes.defaultValues.Get(StaticTypes<T>.typeId);
                }

                return ref StaticTypesDefault<T>.defaultValue.Data;
            }
        }

        public static unsafe void AOT() {
            UnsafeEntityConfig.Data.MethodCaller<T>.Call(default, null, default);
        }

        [INLINE(256)]
        public static void SetCollectionsCount(uint count) {

            StaticTypes.collectionsCount.Get(StaticTypes<T>.typeId) = count;
            
        }

        [INLINE(256)]
        public static void Validate(bool isTag) {

            if (typeId == 0u && typeof(T) != typeof(TNull)) {
                StaticTypes<T>.typeId = ++StaticTypes.counter;
                StaticTypes<T>.isTag = isTag;
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

        [INLINE(256)]
        public static unsafe void SetDefaultValue(T data) {

            StaticTypesHasDefaultValue<T>.value.Data = true; 
            var defaultValuePtr = (safe_ptr<T>)_make(TSize<T>.sizeInt, TAlign<T>.alignInt, Constants.ALLOCATOR_DOMAIN);
            *defaultValuePtr.ptr = data;
            StaticTypes.defaultValues.Get(StaticTypes<T>.typeId) = (System.IntPtr)defaultValuePtr.ptr;
            
        }

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

        [INLINE(256)]
        public static void ValidateStatic(bool isTag) {

            Validate(isTag);

            if (sharedTypeId == 0u) {
                StaticTypes<T>.staticTypeId = ++StaticStaticTypes.counter;
                StaticTypes.staticTypeId.Resize(StaticTypes<T>.typeId + 1u);
                StaticTypes.staticTypeId.Get(StaticTypes<T>.typeId) = StaticTypes<T>.staticTypeId;
            }

        }

        [Unity.Burst.BurstDiscard]
        public static void ApplyGroup(System.Type groupType) {

            if (StaticTypesGroups.groups.TryGetValue(groupType, out var groupId) == false) {
                groupId = ++StaticTypesGroups.nextGroupId;
                StaticTypesGroups.groups.Add(groupType, groupId);
            }
            
            StaticTypes<T>.groupId = groupId;
            if (groupId > StaticTypesGroupsBurst.maxId) StaticTypesGroupsBurst.maxId = groupId;

        }

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

        [Unity.Burst.BurstDiscard]
        public static void AddSharedTypeToCache(bool hasCustomHash) {
            StaticTypesLoadedManaged.loadedSharedTypes.Add(StaticTypes<T>.sharedTypeId, typeof(T));
            StaticTypesLoadedManaged.loadedSharedTypesCustomHash.Add(StaticTypes<T>.typeId, hasCustomHash);
        }

    }

}