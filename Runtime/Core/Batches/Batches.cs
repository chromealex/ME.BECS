using Unity.Collections;

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Jobs;
    using System.Runtime.InteropServices;
    using Unity.Jobs.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public static class BatchesExt {

        [INLINE(256)]
        public static void Apply(this ref SystemContext context) {
            context.SetDependency(Batches.Apply(context.dependsOn, in context.world));
        } 

        [INLINE(256)]
        public static JobHandle Apply(this in SystemContext context, JobHandle dependsOn) {
            return Batches.Apply(dependsOn, in context.world);
        }

        [INLINE(256)]
        public static JobHandle Apply(this in World world, JobHandle dependsOn) {
            return Batches.Apply(dependsOn, in world);
        }

    }
    
    
    [IgnoreProfiler]
    [BURST]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe partial struct Batches {

        
        [BURST]
        [INLINE(256)]
        public static void Apply(in World world) {
            Apply(world.id, world.state);
        }

        [BURST]
        [INLINE(256)]
        public static void Apply(ushort worldId, in safe_ptr<State> state) {
            new ApplyDestroyedJob() {
                worldId = worldId,
                state = state,
            }.Execute();
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, ushort worldId, safe_ptr<State> state) {
            var handle2 = new ApplyDestroyedJob() { 
                worldId = worldId,
                state = state,
            }.ScheduleSingle(jobHandle);
            var handle = handle2;
            HandleStorage.lastApplyHandleBurst.Data = JobHandle.CombineDependencies(HandleStorage.lastApplyHandleBurst.Data, handle);
            return handle;
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, in World world) {
            HandleStorage.lastApplyHandleBurst.Data = JobHandle.CombineDependencies(HandleStorage.lastApplyHandleBurst.Data, jobHandle);
            return jobHandle;
        }

    }
    

    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe partial struct Batches {

        [INLINE(256)]
        public static void OnEntityAdd(ushort worldId, uint entId, byte growFactor = 2) {


        }

        [INLINE(256)]
        internal static void Set_INTERNAL(uint typeId, in Ent ent) {
            
            if (ent.IsAlive() == false) return;

            {
                var state = ent.World.state;
                state.ptr->entities.OnAddComponent(state, ent.id, typeId);
                var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->SetBit(state, ent.id, true, typeId);
            }

        }

        [INLINE(256)]
        internal static void Remove_INTERNAL(uint typeId, in Ent ent) {
            
            if (ent.IsAlive() == false) return;
            
            {
                var state = ent.World.state;
                state.ptr->entities.OnRemoveComponent(state, ent.id, typeId);
                var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->SetBit(state, ent.id, false, typeId);
            }
            
        }

    }

}
