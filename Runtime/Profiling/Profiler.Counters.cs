
using Unity.Jobs;

namespace ME.BECS {
    
    using Unity.Profiling;
    using System.Diagnostics;

    public class ProfilerCountersDefinition {

        public readonly struct Counter<T> where T : unmanaged {

            private readonly ProfilerCounter<T> count;
            
            public Counter(string name, ProfilerCategory category, ProfilerMarkerDataUnit unit) {

                this.count = new ProfilerCounter<T>(category, name, unit);
                
            }

            public void Sample(T value) {
                
                this.count.Sample(value);
                
            }

        }

        private const string caption = "<b><color=#888>ME.BECS</color></b>";
        private const string categoryAllocatorCaption = "<b><color=#888>ME.BECS</color></b>: Allocator";

        public static readonly Unity.Burst.SharedStatic<Counter<uint>> entitiesCount = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99000);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> componentsSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99001);
        #if !ENABLE_BECS_FLAT_QUERIES
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> batchesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99002);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> archetypesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99003);
        #endif
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> entitiesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99004);
        
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorReserved = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<int>>.align, 99006);
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorUsed = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<int>>.align, 99007);
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorFree = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<int>>.align, 99008);
        
        public static readonly Unity.Burst.SharedStatic<bool> initialized = Unity.Burst.SharedStatic<bool>.GetOrCreate<ProfilerCountersDefinition>(TAlign<Counter<bool>>.align);

        [Conditional("ENABLE_PROFILER")]
        public static void Initialize() {

            if (initialized.Data == true) return;
            initialized.Data = true;
            
            var category = new ProfilerCategory(caption);
            var categoryAllocator = new ProfilerCategory(categoryAllocatorCaption);
            entitiesCount.Data = new Counter<uint>("Entities Count", category, ProfilerMarkerDataUnit.Count);
            componentsSize.Data = new Counter<uint>("Components Size", category, ProfilerMarkerDataUnit.Bytes);
            #if !ENABLE_BECS_FLAT_QUERIES
            batchesSize.Data = new ("Batches Size", category, ProfilerMarkerDataUnit.Bytes);
            archetypesSize.Data = new ("Archetypes Size", category, ProfilerMarkerDataUnit.Bytes);
            #endif
            entitiesSize.Data = new Counter<uint>("Entities Size", category, ProfilerMarkerDataUnit.Bytes);
            
            memoryAllocatorReserved.Data = new Counter<int>("Allocator: Reserved", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
            memoryAllocatorUsed.Data = new Counter<int>("Allocator: Used", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
            memoryAllocatorFree.Data = new Counter<int>("Allocator: Free", categoryAllocator, ProfilerMarkerDataUnit.Bytes);

        }
        
    }

    [Unity.Burst.BurstCompile]
    public static unsafe class ProfilerCounters {

        public static void Initialize() {
            
            ProfilerCountersDefinition.Initialize();
            
        }

        [Conditional("ENABLE_PROFILER")]
        public static void SampleWorldBeginFrame(in World world) {
            
        }

        [Conditional("ENABLE_PROFILER")]
        [Unity.Burst.BurstCompile]
        public static void SampleWorldEndFrame(in World world) {

            if (ProfilerCountersDefinition.initialized.Data == false) return;
            ProfilerCountersDefinition.entitiesCount.Data.Sample(world.state.ptr->entities.EntitiesCount);
            ProfilerCountersDefinition.componentsSize.Data.Sample(Components.GetReservedSizeInBytes(world.state));
            #if !ENABLE_BECS_FLAT_QUERIES
            ProfilerCountersDefinition.archetypesSize.Data.Sample(world.state.ptr->archetypes.GetReservedSizeInBytes(world.state));
            ProfilerCountersDefinition.batchesSize.Data.Sample(Batches.GetReservedSizeInBytes(world.id));
            #endif
            ProfilerCountersDefinition.entitiesSize.Data.Sample(world.state.ptr->entities.GetReservedSizeInBytes(world.state));
            
            world.state.ptr->allocator.GetSize(out var reservedSize, out var usedSize, out var freeSize);
            ProfilerCountersDefinition.memoryAllocatorReserved.Data.Sample((int)reservedSize);
            ProfilerCountersDefinition.memoryAllocatorUsed.Data.Sample((int)usedSize);
            ProfilerCountersDefinition.memoryAllocatorFree.Data.Sample((int)freeSize);
            
        }

    }

}