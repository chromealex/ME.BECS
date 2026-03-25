using Unity.Collections;

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    public partial struct Ent {

        /// <summary>
        /// Create new entity from Context.world
        /// </summary>
        /// <returns></returns>
        [INLINE(256)][CodeGeneratorIgnoreVisited][IgnoreProfiler]
        public static Ent New(in FixedString32Bytes editorName = default) {
            return Ent.New(Context.world.id, default, in editorName);
        }
        
        [INLINE(256)][CodeGeneratorIgnoreVisited][IgnoreProfiler]
        public static Ent New(in JobInfo jobInfo, in FixedString32Bytes editorName = default) {
            return Ent.New(jobInfo.worldId, in jobInfo, in editorName);
        }

        [INLINE(256)][CodeGeneratorIgnoreVisited][IgnoreProfiler]
        public static Ent New(in World world, in JobInfo jobInfo = default, in FixedString32Bytes editorName = default) {
            return Ent.New(world.id, in jobInfo, in editorName);
        }

        [INLINE(256)][CodeGeneratorIgnoreVisited][IgnoreProfiler]
        public static Ent New(in SystemContext systemContext, in JobInfo jobInfo = default, in FixedString32Bytes editorName = default) {
            return Ent.New(systemContext.world.id, in jobInfo, in editorName);
        }

        [INLINE(256)][CodeGeneratorIgnoreVisited][IgnoreProfiler]
        public static Ent New(ushort worldId, in JobInfo jobInfo = default, in FixedString32Bytes editorName = default) {
            return NewEnt_INTERNAL<DefaultEntityType>(worldId, in jobInfo, in editorName);
        }

        [INLINE(256)][CodeGeneratorIgnore][CodeGeneratorIgnoreVisited]
        internal static unsafe Ent NewEnt_INTERNAL<T>(ushort worldId, in JobInfo jobInfo, in FixedString32Bytes editorName = default) where T : unmanaged, IEntityType {
            
            if (JobUtils.IsInParallelJob() == true || jobInfo.itemsPerCall.ptr != null) {
                // Create entity with offset because we are in parallel mode
                // so we need JobInfo struct to be provided
                E.IS_CREATED(jobInfo);
                return New_INTERNAL<T>(worldId, in jobInfo, in editorName);
            } else {
                return New_INTERNAL<T>(worldId, default, in editorName);
            }

        }
        
        [INLINE(256)][CodeGeneratorIgnore][CodeGeneratorIgnoreVisited]
        internal static Ent New_INTERNAL<T>(ushort worldId, in JobInfo jobInfo, in FixedString32Bytes editorName = default) where T : unmanaged, IEntityType {

            return NewWithGroup_INTERNAL(worldId, EntityTypes<T>.id, in jobInfo, in editorName);

        }

        [CodeGeneratorIgnore][CodeGeneratorIgnoreVisited]
        internal static Ent NewWithGroup_INTERNAL(ushort worldId, ushort groupId, in JobInfo jobInfo, in FixedString32Bytes editorName = default) {

            ref readonly var world = ref Worlds.GetWorld(worldId);
            E.IS_IN_TICK(world.state);

            var newEnt = NewWithGroupNoResize_INTERNAL(out var reused, world.state, worldId, groupId, in jobInfo, in editorName);
            if (reused == false) {
                Resize(worldId, world.state, newEnt.id + 1u);
            }
            return newEnt;

        }

        [CodeGeneratorIgnore][CodeGeneratorIgnoreVisited]
        internal static Ent NewWithGroupNoResize_INTERNAL(out bool reused, safe_ptr<State> state, ushort worldId, ushort groupId, in JobInfo jobInfo, in FixedString32Bytes editorName = default) {

            Ent newEnt;
            {
                newEnt = Ents.New(state, worldId, groupId, out reused, in jobInfo);
            }
            #if !ENABLE_BECS_FLAT_QUERIES
            {
                Ents.Lock(state, in newEnt);
                Archetypes.AddEntity(state, newEnt);
                Ents.Unlock(state, in newEnt);
            }
            #endif

            #if UNITY_EDITOR
            EntEditorName.SetEditorName(in newEnt, editorName);
            #endif

            return newEnt;

        }

        [INLINE(256)]
        internal static void Resize(ushort worldId, safe_ptr<State> state, uint length) {
            length = Bitwise.AlignUp(length, Ents.ENTITIES_PER_PAGE);
            Components.OnEntityAdd(state, length);
            Batches.OnEntityAdd(worldId, length);
            CollectionsRegistry.OnEntityAdd(state, length);
            AutoDestroyRegistry.OnEntityAdd(state, length);
        }

    }

}