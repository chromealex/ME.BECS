
using Unity.Jobs;

namespace ME.BECS {
    
    using Unity.Profiling;
    using System.Diagnostics;

    /// <summary>
    /// Defines profiler counters definition state and operations.
    /// </summary>
    public class ProfilerCountersDefinition {

        /// <summary>
        /// Tracks a numeric quantity in the associated execution context.
        /// </summary>
        public readonly unsafe struct Counter<T> where T : unmanaged {

            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute]
            [System.NonSerializedAttribute]
            private readonly System.IntPtr ptr;
            [System.NonSerializedAttribute]
            private readonly byte type;
            
            /// <summary>
            /// Initializes <c>Counter</c> from the supplied name, category, unit.
            /// </summary>
            public Counter(string name, ProfilerCategory category, ProfilerMarkerDataUnit unit) {

                this.type = GetProfilerMarkerDataType();
                this.ptr = Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.CreateMarker(name, category, Unity.Profiling.LowLevel.MarkerFlags.Counter, 1);
                Unity.Profiling.LowLevel.Unsafe.ProfilerUnsafeUtility.SetMarkerMetadata(this.ptr, 0, null, this.type, (byte)unit);
                
            }

            /// <summary>
            /// Returns profiler marker data type.
            /// </summary>
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
                        
            /// <summary>
            /// Reads or evaluates a sample at the requested position.
            /// </summary>
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

        /// <summary>
        /// Entities count for the associated storage.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> entitiesCount = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99000);
        /// <summary>
        /// Components size used by <c>ProfilerCountersDefinition</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> componentsSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99001);
        /// <summary>
        /// Entities size used by <c>ProfilerCountersDefinition</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Counter<uint>> entitiesSize = Unity.Burst.SharedStatic<Counter<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<uint>>.align, 99004);
        
        /// <summary>
        /// Memory allocator reserved used by <c>ProfilerCountersDefinition</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorReserved = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<int>>.align, 99006);
        /// <summary>
        /// Memory allocator used used by <c>ProfilerCountersDefinition</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorUsed = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<int>>.align, 99007);
        /// <summary>
        /// Memory allocator free used by <c>ProfilerCountersDefinition</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<Counter<int>> memoryAllocatorFree = Unity.Burst.SharedStatic<Counter<int>>.GetOrCreatePartiallyUnsafeWithHashCode<ProfilerCountersDefinition>(TAlign<Counter<int>>.align, 99008);
        
        /// <summary>
        /// Whether initialized behavior or state is selected.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<bool> initialized = Unity.Burst.SharedStatic<bool>.GetOrCreate<ProfilerCountersDefinition>(TAlign<Counter<bool>>.align);

        /// <summary>
        /// Initializes profiler counters definition state from the supplied context.
        /// </summary>
        [Conditional("ENABLE_PROFILER")]
        public static void Initialize() {

            if (initialized.Data == true) return;
            initialized.Data = true;
            
            var category = new ProfilerCategory(caption);
            var categoryAllocator = new ProfilerCategory(categoryAllocatorCaption);
            entitiesCount.Data = new Counter<uint>("Entities Count", category, ProfilerMarkerDataUnit.Count);
            componentsSize.Data = new Counter<uint>("Components Size", category, ProfilerMarkerDataUnit.Bytes);
            entitiesSize.Data = new Counter<uint>("Entities Size", category, ProfilerMarkerDataUnit.Bytes);
            
            memoryAllocatorReserved.Data = new Counter<int>("Allocator: Reserved", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
            memoryAllocatorUsed.Data = new Counter<int>("Allocator: Used", categoryAllocator, ProfilerMarkerDataUnit.Bytes);
            memoryAllocatorFree.Data = new Counter<int>("Allocator: Free", categoryAllocator, ProfilerMarkerDataUnit.Bytes);

        }
        
    }

    /// <summary>
    /// Defines profiler counters state and operations.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public static unsafe class ProfilerCounters {

        /// <summary>
        /// Initializes profiler counters state from the supplied context.
        /// </summary>
        public static void Initialize() {
            
            ProfilerCountersDefinition.Initialize();
            
        }

        /// <summary>
        /// Provides the <c>SampleWorldBeginFrame</c> callback; this implementation performs no work.
        /// </summary>
        [Conditional("ENABLE_PROFILER")]
        public static void SampleWorldBeginFrame(in World world) {
            
        }

        /// <summary>
        /// Samples world end frame.
        /// </summary>
        [Conditional("ENABLE_PROFILER")]
        [Unity.Burst.BurstCompile]
        public static void SampleWorldEndFrame(in World world) {

            if (ProfilerCountersDefinition.initialized.Data == false) return;
            ProfilerCountersDefinition.entitiesCount.Data.Sample(world.state.ptr->entities.EntitiesCount);
            ProfilerCountersDefinition.componentsSize.Data.Sample(Components.GetReservedSizeInBytes(world.state));
            ProfilerCountersDefinition.entitiesSize.Data.Sample(world.state.ptr->entities.GetReservedSizeInBytes(world.state));
            
            world.state.ptr->allocator.GetSize(out var reservedSize, out var usedSize, out var freeSize);
            ProfilerCountersDefinition.memoryAllocatorReserved.Data.Sample((int)reservedSize);
            ProfilerCountersDefinition.memoryAllocatorUsed.Data.Sample((int)usedSize);
            ProfilerCountersDefinition.memoryAllocatorFree.Data.Sample((int)freeSize);
            
        }

    }

}