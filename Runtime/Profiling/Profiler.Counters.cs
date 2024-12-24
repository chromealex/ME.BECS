
using Unity.Jobs;

namespace ME.BECS {
    
    using Unity.Profiling;
    using System.Diagnostics;

    public class ProfilerCountersDefinition {

        public readonly unsafe struct Counter<T> where T : unmanaged {

            //private readonly ProfilerCounter<T> count;
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute]
            [System.NonSerializedAttribute]
            readonly System.IntPtr m_Ptr;
            [System.NonSerializedAttribute]
            readonly byte m_Type;

            public static byte GetProfilerMarkerDataType() {
                switch (System.Type.GetTypeCode(typeof(T))) {
                    case System.TypeCode.Int32:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int32;

                    case System.TypeCode.UInt32:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt32;

                    case System.TypeCode.Int64:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int64;

                    case System.TypeCode.UInt64:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt64;

                    case System.TypeCode.Single:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.Float;

                    case System.TypeCode.Double:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.Double;

                    case System.TypeCode.String:
                        return (byte)Unity.Profiling.LowLevel.ProfilerMarkerDataType.String16;

                    default:
                        throw new System.ArgumentException($"Type {typeof(T)} is unsupported by ProfilerCounter.");
                }
            }

            public Counter(string name, ProfilerCategory category, ProfilerMarkerDataUnit unit) {

                this.m_Type = GetProfilerMarkerDataType();
                this.m_Ptr = Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.CreateMarker(name, category, Unity.Profiling.LowLevel.MarkerFlags.Counter, 1);
                Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.SetMarkerMetadata(this.m_Ptr, 0, null, this.m_Type, (byte)unit);
                //this.count = new ProfilerCounter<T>(category, name, unit);
                
            }

            public void Sample(T value) {
                
                var data = new Unity.Profiling.LowLevel.Unsafe.ProfilerMarkerData {
                    Type = this.m_Type,
                    Size = (uint)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>(),
                    Ptr = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref value),
                };
                Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.SingleSampleWithMetadata(this.m_Ptr, 1, &data);
                //this.count.Sample(value);
                
            }

        }

        private const string caption = "<b><color=#888>ME.BECS</color></b>";
        private const string categoryNetworkCaption = "<b><color=#888>ME.BECS</color></b>: Network";
        private const string categoryAllocatorCaption = "<b><color=#888>ME.BECS</color></b>: Network";

        public static readonly Unity.Burst.SharedStatic<Counter<uint>> entitiesCount = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99000);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> componentsSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99001);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> batchesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99002);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> archetypesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99003);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> entitiesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99004);
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> aspectStorageSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99005);
        
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorReserved = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99006);
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorUsed = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99007);
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorFree = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99008);
        
        private static bool initialized = false;
        [Conditional("ENABLE_PROFILER")]
        public static void Initialize() {

            if (initialized == true) return;
            initialized = true;
            
            var category = new ProfilerCategory(caption);
            var categoryNetwork = new ProfilerCategory(categoryNetworkCaption);
            var categoryAllocator = new ProfilerCategory(categoryAllocatorCaption);
            entitiesCount.Data = new("Entities Count", category, ProfilerMarkerDataUnit.Count);
            componentsSize.Data = new ("Components Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
            batchesSize.Data = new ("Batches Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
            archetypesSize.Data = new ("Archetypes Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
            entitiesSize.Data = new ("Entities Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
            aspectStorageSize.Data = new ("Aspect Storage Size (bytes)", category, ProfilerMarkerDataUnit.Bytes);
            
            memoryAllocatorReserved.Data = new ("Allocator: Reserved (bytes)", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
            memoryAllocatorUsed.Data = new ("Allocator: Used (bytes)", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
            memoryAllocatorFree.Data = new("Allocator: Free (bytes)", categoryAllocator, ProfilerMarkerDataUnit.Bytes);

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

            var marker = new ProfilerMarker("Profiler::CollectStats");
            marker.Begin();
            
            using (new ProfilerMarker("EntitiesCount").Auto()) {
                ProfilerCountersDefinition.entitiesCount.Data.Sample(world.state.ptr->entities.EntitiesCount);
            }

            using (new ProfilerMarker("Components").Auto()) {
                ProfilerCountersDefinition.componentsSize.Data.Sample(Components.GetReservedSizeInBytes(world.state));
            }
            using (new ProfilerMarker("Archetypes").Auto()) {
                ProfilerCountersDefinition.archetypesSize.Data.Sample(world.state.ptr->archetypes.GetReservedSizeInBytes(world.state));
            }
            using (new ProfilerMarker("EntitiesCount").Auto()) {
                ProfilerCountersDefinition.batchesSize.Data.Sample(Batches.GetReservedSizeInBytes(world.state));
            }
            using (new ProfilerMarker("Entities").Auto()) {
                ProfilerCountersDefinition.entitiesSize.Data.Sample(world.state.ptr->entities.GetReservedSizeInBytes(world.state));
            }
            using (new ProfilerMarker("AspectsStorage").Auto()) {
                ProfilerCountersDefinition.aspectStorageSize.Data.Sample(world.state.ptr->aspectsStorage.GetReservedSizeInBytes(world.state));
            }
            
            using (new ProfilerMarker("Allocator").Auto()) {
                world.state.ptr->allocator.GetSize(out var reservedSize, out var usedSize, out var freeSize);
                ProfilerCountersDefinition.memoryAllocatorReserved.Data.Sample(reservedSize);
                ProfilerCountersDefinition.memoryAllocatorUsed.Data.Sample(usedSize);
                ProfilerCountersDefinition.memoryAllocatorFree.Data.Sample(freeSize);
            }

            marker.End();

        }

    }

}