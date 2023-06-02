
namespace ME.BECS {
    
    using Unity.Profiling;
    using System.Diagnostics;

    public static unsafe class ProfilerCounters {
        
        [System.Serializable]
        public struct ProfilerCounterData {

            public string m_Category;
            public string m_Name;

            public ProfilerCounterData(ProfilerCategory category, string name) {
                this.m_Category = category.Name;
                this.m_Name = name;
            }

        }

        public readonly struct Counter<T> where T : unmanaged {

            private readonly ProfilerCounterData data;
            private readonly ProfilerCounter<T> count;
            
            public Counter(string name, ProfilerCategory category, ProfilerMarkerDataUnit unit) {

                this.data = new ProfilerCounterData(category, name);
                this.count = new ProfilerCounter<T>(category, this.data.m_Name, unit);

            }

            public readonly void Sample(T value) {
                
                this.count.Sample(value);
                
            }

        }

        public static readonly string caption = "<b><color=#888>ME.BECS</color></b>";
        private static readonly ProfilerCategory category = new ProfilerCategory(caption);
        private static readonly ProfilerCategory categoryNetwork = new ProfilerCategory($"{caption}: Network");
        private static readonly ProfilerCategory categoryAllocator = new ProfilerCategory($"{caption} Allocator");

        public static readonly Counter<uint> entitiesCount = new ("Entities Count", category, ProfilerMarkerDataUnit.Count);
        
        public static readonly Counter<uint> componentsSize = new ("Components Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
        public static readonly Counter<uint> batchesSize = new ("Batches Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
        public static readonly Counter<uint> archetypesSize = new ("Archetypes Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
        public static readonly Counter<uint> entitiesSize = new ("Entities Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
        public static readonly Counter<uint> aspectStorageSize = new ("Aspect Storage Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
        
        public static readonly Counter<int> memoryAllocatorReserved = new ("Allocator: Reserved (bytes)", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
        public static readonly Counter<int> memoryAllocatorUsed = new ("Allocator: Used (bytes)", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
        public static readonly Counter<int> memoryAllocatorFree = new ("Allocator: Free (bytes)", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
        
        [Conditional("ENABLE_PROFILER")]
        public static void SampleWorld(in World world) {
            
            entitiesCount.Sample(world.state->entities.EntitiesCount);
            
            componentsSize.Sample(world.state->components.GetReservedSizeInBytes(world.state));
            batchesSize.Sample(world.state->archetypes.GetReservedSizeInBytes(world.state));
            archetypesSize.Sample(world.state->batches.GetReservedSizeInBytes(world.state));
            entitiesSize.Sample(world.state->entities.GetReservedSizeInBytes(world.state));
            aspectStorageSize.Sample(world.state->aspectsStorage.GetReservedSizeInBytes(world.state));
            
            memoryAllocatorReserved.Sample(world.state->allocator.GetReservedSize());
            memoryAllocatorUsed.Sample(world.state->allocator.GetUsedSize());
            memoryAllocatorFree.Sample(world.state->allocator.GetFreeSize());

        }

    }

}