
using Unity.Jobs;

namespace ME.BECS {
    
    using Unity.Profiling;
    using System.Diagnostics;

    public class ProfilerCountersDefinition {

        public readonly unsafe struct Counter<T> where T : unmanaged {

            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute]
            [System.NonSerializedAttribute]
            private readonly System.IntPtr ptr;
            [System.NonSerializedAttribute]
            private readonly byte type;
            
            public Counter(string name, ProfilerCategory category, ProfilerMarkerDataUnit unit) {

                this.type = GetProfilerMarkerDataType();
                this.ptr = Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.CreateMarker(name, category, Unity.Profiling.LowLevel.MarkerFlags.Counter, 1);
                Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.SetMarkerMetadata(this.ptr, 0, null, this.type, (byte)unit);
                
            }

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
                        
            public void Sample(T value) {
                
                var data = new Unity.Profiling.LowLevel.Unsafe.ProfilerMarkerData {
                    Type = this.type,
                    Size = (uint)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>(),
                    Ptr = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref value),
                };
                Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.SingleSampleWithMetadata(this.ptr, 1, &data);
                
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