
namespace ME.BECS {

    using static Cuts;
    using MemPtr = System.Int64;
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

        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
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
            default(EntityConfig).data.AOTShared<T>();
        }

    }

    public struct StaticTypesStatic<T> where T : unmanaged, IComponentStatic {

        public static void AOT() {
            default(EntityConfig).data.AOTStatic<T>();
        }

    }

    public struct StaticTypesDefaultValue<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<T> value = Unity.Burst.SharedStatic<T>.GetOrCreate<StaticTypesDefaultValue<T>>();

    }

    public struct StaticTypes<T> where T : unmanaged {

        public static ref uint staticTypeId => ref StaticTypesStaticTypeId<T>.value.Data;
        public static ref uint sharedTypeId => ref StaticTypesSharedTypeId<T>.value.Data;
        public static ref bool hasSharedCustomHash => ref StaticTypesSharedCustomHash<T>.value.Data;
        public static ref uint typeId => ref StaticTypesId<T>.value.Data;
        public static ref bool isTag => ref StaticTypesIsTag<T>.value.Data;
        public static ref uint groupId => ref StaticTypesGroupId<T>.value.Data;
        public static ref readonly T defaultValue => ref StaticTypesDefaultValue<T>.value.Data;

        public static void AOT() {
            default(EntityConfig).data.AOT<T>();
        }

        [INLINE(256)]
        public static void Validate(bool isTag) {

            if (typeId == 0u) {
                StaticTypes<T>.typeId = ++StaticTypes.counter;
                StaticTypes<T>.isTag = isTag;
                StaticTypes.sizes.Resize(StaticTypes<T>.typeId + 1u);
                StaticTypes.sizes.Get(StaticTypes<T>.typeId) = isTag == true ? 0u : TSize<T>.size;
                StaticTypes.groups.Resize(StaticTypes<T>.typeId + 1u);
                StaticTypes.groups.Get(StaticTypes<T>.typeId) = groupId;
                StaticTypes<T>.AddTypeToCache();
            }

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
            StaticTypesLoadedManaged.loadedTypes.Add(StaticTypes<T>.typeId, typeof(T));
            StaticTypesLoadedManaged.typeToId.Add(typeof(T), StaticTypes<T>.typeId);
        }

        [Unity.Burst.BurstDiscard]
        public static void AddSharedTypeToCache(bool hasCustomHash) {
            StaticTypesLoadedManaged.loadedSharedTypes.Add(StaticTypes<T>.sharedTypeId, typeof(T));
            StaticTypesLoadedManaged.loadedSharedTypesCustomHash.Add(StaticTypes<T>.typeId, hasCustomHash);
        }

    }

}