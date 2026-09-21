namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using System.Diagnostics;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    using static Cuts;

    public class AllocatorTagInfoAttribute : System.Attribute {}
    
    public struct AllocatorTagInfo {

        public ushort tag;
        public Unity.Collections.FixedString32Bytes name;
        public UnityEngine.Color color;

    }

    public struct AllocatorTagData {

        public AllocatorTagInfo tagInfo;
        public uint componentId;
        public safe_ptr<AllocatorTagData> parent;

    }

    public class AllocatorTagLocks {

        public static readonly SharedStatic<LockSpinner> lck = SharedStatic<LockSpinner>.GetOrCreate<AllocatorTagLocks>();

    }
    
    public class AllocatorTagDummy {

        public static readonly SharedStatic<AllocatorTagData> dummy = SharedStatic<AllocatorTagData>.GetOrCreate<AllocatorTagDummy>();

    }
    
    public struct AllocatorTag : System.IDisposable {

        public static readonly SharedStatic<Internal.Array<AllocatorTagData>> tags = SharedStatic<Internal.Array<AllocatorTagData>>.GetOrCreate<AllocatorTag>();
        
        public AllocatorTag(AllocatorTagInfo tag, uint id = 0u, bool allThreads = false) {
            #if LEAK_DETECTION || LEAK_DETECTION_ALLOCATOR
            AllocatorTagLocks.lck.Data.Lock();
            safe_ptr<AllocatorTagData> parent = default;
            if (Get().tagInfo.tag > 0) {
                parent = _make(Get());
            }
            tags.Data.Resize(JobUtils.ThreadIndex + 1u);
            AllocatorTagLocks.lck.Data.Unlock();
            if (allThreads == true) {
                SetAllThreads(tag, id, parent);
            } else {
                tags.Data.Get(JobUtils.ThreadIndex) = new AllocatorTagData() {
                    tagInfo = tag,
                    componentId = id,
                    parent = parent,
                };
            }
            #endif
        }

        private static void SetAllThreads(AllocatorTagInfo tag, uint id, safe_ptr<AllocatorTagData> parent) {
            for (uint i = 0u; i < JobUtils.ThreadsCount; ++i) {
                tags.Data.Get(JobUtils.ThreadIndex) = new AllocatorTagData() {
                    tagInfo = tag,
                    componentId = id,
                    parent = parent,
                };
            }
        }

        public unsafe void Dispose() {
            #if LEAK_DETECTION || LEAK_DETECTION_ALLOCATOR
            if (Get().parent.ptr != null) {
                tags.Data.Get(JobUtils.ThreadIndex) = *Get().parent.ptr;
            } else {
                tags.Data.Get(JobUtils.ThreadIndex) = default;
            }
            #endif
        }

        public static ref AllocatorTagData Get() {
            #if LEAK_DETECTION || LEAK_DETECTION_ALLOCATOR
            if (JobUtils.ThreadIndex >= tags.Data.Length) return ref AllocatorTagDummy.dummy.Data;
            return ref tags.Data.Get(JobUtils.ThreadIndex);
            #else
            return ref AllocatorTagDummy.dummy.Data;
            #endif
        }

    }
    
    [IgnoreProfiler]
    public unsafe class LeakDetectorData {

        [IgnoreProfiler]
        public readonly struct Key : System.IEquatable<Key> {

            public readonly System.IntPtr ptr;

            [INLINE(256)]
            public Key(void* ptr) {
                this.ptr = (System.IntPtr)ptr;
            }

            [INLINE(256)]
            public bool Equals(Key other) {
                return this.ptr == other.ptr;
            }

            public override bool Equals(object obj) {
                return obj is Key other && this.Equals(other);
            }

            [INLINE(256)]
            public override int GetHashCode() {
                return this.ptr.GetHashCode();
            }

        }

        [IgnoreProfiler]
        public struct Item : System.IEquatable<Item> {

            public System.IntPtr ptr;
            public System.IntPtr hiPtr;
            public readonly MemPtr memPtr;
            public readonly Unity.Collections.Allocator allocator;
            public Unity.Collections.FixedString4096Bytes stackTrace;
            public readonly AllocatorTagData tag;

            public Item(void* ptr, void* hiPtr, MemPtr memPtr, Unity.Collections.Allocator allocator = Unity.Collections.Allocator.None, bool withStackTrace = true, AllocatorTagData tag = default) {
                this = default;
                this.memPtr = memPtr;
                this.tag = tag;
                this.ptr = (System.IntPtr)ptr;
                this.hiPtr = (System.IntPtr)hiPtr;
                this.allocator = allocator;
                if (withStackTrace == true && this.IsTrackableAllocator() == true) this.AddStackTrace();
            }

            public bool IsTrackableAllocator() {
                return (this.allocator == Unity.Collections.Allocator.Domain || this.allocator == Unity.Collections.Allocator.Persistent || this.allocator == Unity.Collections.Allocator.TempJob || this.allocator >= Unity.Collections.Allocator.FirstUserIndex);
            }

            [BURST_DISCARD]
            public void AddStackTrace() {
                var str = UnityEngine.StackTraceUtility.ExtractStackTrace();
                var lines = str.Split('\n');
                str = string.Join("\n", lines, 5, lines.Length - 5);
                if (str.Length < 2000) {
                    this.stackTrace = str;
                    return;
                }
                this.stackTrace = str.Substring(0, 2000);
            }

            public bool Equals(Item other) {
                return this.ptr == other.ptr && this.memPtr == other.memPtr;
            }

            public override bool Equals(object obj) {
                return obj is Item other && this.Equals(other);
            }

            public override int GetHashCode() {
                return this.ptr.GetHashCode() ^ this.memPtr.GetHashCode();
            }

        }
        
        public struct Shard {

            public UnsafeHashMap<Key, Item> tracked;
            public LockSpinner spinner;

        }

        public const int SHARDS_COUNT = 64;
        private const int SHARD_INITIAL_CAPACITY = 1;

        public static readonly SharedStatic<UnsafeList<Shard>> shards = SharedStatic<UnsafeList<Shard>>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<UnsafeList<Shard>>.align, 1L);
        public static readonly SharedStatic<LockSpinner> shardsSpinner = SharedStatic<LockSpinner>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<LockSpinner>.align, 2L);
        public static readonly SharedStatic<Internal.Array<int>> counter = SharedStatic<Internal.Array<int>>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<Internal.Array<int>>.align, 3L);
        public static readonly SharedStatic<LockSpinner> counterSpinner = SharedStatic<LockSpinner>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<LockSpinner>.align, 4L);
        public static readonly SharedStatic<bbool> counterAwait = SharedStatic<bbool>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<bbool>.align, 5L);
        private static readonly SharedStatic<int> shardsInitialized = SharedStatic<int>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<int>.align, 6L);

        [INLINE(256)]
        public static int GetShardIndex(void* ptr) {
            var value = (ulong)ptr;
            value >>= 4;
            value ^= value >> 33;
            value *= 0xff51afd7ed558ccdUL;
            value ^= value >> 33;
            return (int)(value & (SHARDS_COUNT - 1));
        }

        public static void Validate() {
            if (System.Threading.Volatile.Read(ref shardsInitialized.Data) == 1) return;
            shardsSpinner.Data.Lock();
            if (shardsInitialized.Data == 0) {
                var value = new UnsafeList<Shard>(SHARDS_COUNT, Constants.ALLOCATOR_DOMAIN);
                value.Resize(SHARDS_COUNT, Unity.Collections.NativeArrayOptions.ClearMemory);
                shards.Data = value;
                System.Threading.Volatile.Write(ref shardsInitialized.Data, 1);
            }
            shardsSpinner.Data.Unlock();
        }

        [INLINE(256)]
        public static ref Shard GetShard(void* ptr) {
            Validate();
            return ref shards.Data.ElementAt(GetShardIndex(ptr));
        }

        [INLINE(256)]
        public static void Validate(ref Shard shard) {
            if (shard.tracked.IsCreated == false) {
                shard.tracked = new UnsafeHashMap<Key, Item>(SHARD_INITIAL_CAPACITY, Constants.ALLOCATOR_DOMAIN);
            }
        }

    }
    
    public unsafe struct LeakDetector {

        [Conditional(COND.LEAK_DETECTION_COUNTER)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void TrackCount(void* ptr, Unity.Collections.Allocator allocator) {
            if (IsTrackable(allocator) == false) return;
            if (LeakDetectorData.counterAwait.Data == true) return;
            if (LeakDetectorData.counter.Data.IsCreated == false) {
                LeakDetectorData.counterSpinner.Data.Lock();
                if (LeakDetectorData.counter.Data.IsCreated == false) {
                    LeakDetectorData.counterAwait.Data = true;
                    LeakDetectorData.counter.Data.Resize(10);
                    LeakDetectorData.counterAwait.Data = false;
                }
                LeakDetectorData.counterSpinner.Data.Unlock();
            }
            System.Threading.Interlocked.Increment(ref LeakDetectorData.counter.Data.Get((int)allocator));
        }

        [Conditional(COND.LEAK_DETECTION_COUNTER)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void UntrackCount(void* ptr, Unity.Collections.Allocator allocator) {
            if (IsTrackable(allocator) == false) return;
            if (LeakDetectorData.counterAwait.Data == true) return;
            if (LeakDetectorData.counter.Data.IsCreated == false) {
                LeakDetectorData.counterSpinner.Data.Lock();
                if (LeakDetectorData.counter.Data.IsCreated == false) {
                    LeakDetectorData.counterAwait.Data = true;
                    LeakDetectorData.counter.Data.Resize(10);
                    LeakDetectorData.counterAwait.Data = false;
                }
                LeakDetectorData.counterSpinner.Data.Unlock();
            }
            System.Threading.Interlocked.Decrement(ref LeakDetectorData.counter.Data.Get((int)allocator));
        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void TrackAllocator(void* ptr, MemPtr memPtr) {

            var tag = AllocatorTag.Get();
            var item = new LeakDetectorData.Item(ptr, ptr, memPtr, Unity.Collections.Allocator.FirstUserIndex, tag: tag);
            ref var shard = ref LeakDetectorData.GetShard(ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.TryAdd(new LeakDetectorData.Key(ptr), item);
            shard.spinner.Unlock();
            if (result == false) UnityEngine.Debug.LogError($"Pointer {((System.IntPtr)ptr).ToInt64()} is already tracked.");

        }

        [Conditional(COND.LEAK_DETECTION_ALLOCATOR)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void TrackAllocator(safe_ptr ptr, MemPtr memPtr) {

            var tag = AllocatorTag.Get();
            var item = new LeakDetectorData.Item(ptr.ptr, ptr.HiBound, memPtr, Unity.Collections.Allocator.FirstUserIndex, tag: tag);
            ref var shard = ref LeakDetectorData.GetShard(ptr.ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.TryAdd(new LeakDetectorData.Key(ptr.ptr), item);
            shard.spinner.Unlock();
            if (result == false) UnityEngine.Debug.LogError($"Pointer {((System.IntPtr)ptr.ptr).ToInt64()} is already tracked.");

        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Track(void* ptr, Unity.Collections.Allocator allocator) {

            if (IsTrackable(allocator) == false) return;
            var item = new LeakDetectorData.Item(ptr, ptr, default, allocator);
            ref var shard = ref LeakDetectorData.GetShard(ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.TryAdd(new LeakDetectorData.Key(ptr), item);
            shard.spinner.Unlock();
            if (result == false) UnityEngine.Debug.LogError($"Pointer {((System.IntPtr)ptr).ToInt64()} is already tracked.");

        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Track(safe_ptr ptr, Unity.Collections.Allocator allocator) {

            if (IsTrackable(allocator) == false) return;
            var item = new LeakDetectorData.Item(ptr.ptr, ptr.HiBound, default, allocator);
            ref var shard = ref LeakDetectorData.GetShard(ptr.ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.TryAdd(new LeakDetectorData.Key(ptr.ptr), item);
            shard.spinner.Unlock();
            if (result == false) UnityEngine.Debug.LogError($"Pointer {((System.IntPtr)ptr.ptr).ToInt64()} is already tracked.");

        }

        [INLINE(256)]
        private static bool IsTrackable(Unity.Collections.Allocator allocator) {
            if (allocator == Unity.Collections.Allocator.Persistent ||
                allocator == Unity.Collections.Allocator.Domain) return true;
            return false;
        }

        [Conditional(COND.LEAK_DETECTION_ALLOCATOR)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void FreeAllocator(void* ptr, MemPtr memPtr) {

            ref var shard = ref LeakDetectorData.GetShard(ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.Remove(new LeakDetectorData.Key(ptr));
            shard.spinner.Unlock();
            if (result == false) {
                UnityEngine.Debug.LogError($"You are trying to free pointer {((System.IntPtr)ptr).ToInt64()} ({memPtr}) which has been already freed or was never instantiated.");
            }
            
        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Free(safe_ptr ptr, Unity.Collections.Allocator allocator) {

            if (IsTrackable(allocator) == false) return;
            ref var shard = ref LeakDetectorData.GetShard(ptr.ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.Remove(new LeakDetectorData.Key(ptr.ptr));
            shard.spinner.Unlock();
            if (result == false) {
                UnityEngine.Debug.LogError($"You are trying to free pointer {((System.IntPtr)ptr.ptr).ToInt64()} which has been already freed or was never instantiated.");
            }
            
        }

        public static void ClearAllocated() {
            if (LeakDetectorData.counter.Data.IsCreated == true) LeakDetectorData.counter.Data.Dispose();
            LeakDetectorData.Validate();
            for (int i = 0; i < LeakDetectorData.SHARDS_COUNT; ++i) {
                ref var shard = ref LeakDetectorData.shards.Data.ElementAt(i);
                shard.spinner.Lock();
                if (shard.tracked.IsCreated == true) shard.tracked.Clear();
                shard.spinner.Unlock();
            }
        }

        public static void PrintAllocated(Unity.Collections.Allocator allocator) {
            
            if (LeakDetectorData.counter.Data.IsCreated == true) {
                for (int i = 0; i < 10; ++i) {
                    UnityEngine.Debug.Log($"Allocated: {LeakDetectorData.counter.Data.Get(i)} in {(Unity.Collections.Allocator)i}");
                }
            }

            var output = new System.Collections.Generic.List<string>();
            LeakDetectorData.Validate();
            for (int i = 0; i < LeakDetectorData.SHARDS_COUNT; ++i) {
                ref var shard = ref LeakDetectorData.shards.Data.ElementAt(i);
                shard.spinner.Lock();
                if (shard.tracked.IsCreated == true) {
                    foreach (var item in shard.tracked) {
                        if (item.Value.IsTrackableAllocator() == false) continue;
                        if (allocator != Unity.Collections.Allocator.None && allocator != item.Value.allocator) continue;
                        var str = item.Value.stackTrace.ToString();
                        if (str.Contains("UnsafeEntityConfig") == true || str.Contains("ME.BECS.Gen") == true) continue;
                        output.Add($"{item.Value.ptr} - {item.Value.allocator}\n{str}");
                    }
                }
                shard.spinner.Unlock();
            }
            foreach (var str in output) UnityEngine.Debug.Log(str);
            
        }

        [Conditional(COND.LEAK_DETECTION)]
        public static void IsAlive(safe_ptr ptr) {
            
            if (ptr.LowBound == null || ptr.HiBound == null) return;
            var allocationPtr = ptr.LowBound;
            ref var shard = ref LeakDetectorData.GetShard(allocationPtr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.ContainsKey(new LeakDetectorData.Key(allocationPtr));
            shard.spinner.Unlock();
            if (result == false) {
                if (ptr.HiBound != ptr.LowBound) {
                    E.RANGE(ptr.ptr, ptr.LowBound, ptr.HiBound);
                } else {
                    throw new System.Exception($"Pointer {((System.IntPtr)ptr.ptr).ToInt64()} not found.");
                }
                /*var val = ((System.IntPtr)ptr).ToInt64();
                foreach (var item in LeakDetectorData.tracked.Data) {
                    var low = item.ptr.ToInt64();
                    var hi = item.hiPtr.ToInt64();
                    if (val >= low && val <= hi) {
                        result = true;
                        break;
                    }
                }*/
                //throw new System.Exception($"Pointer {((System.IntPtr)ptr.ptr).ToInt64()} not found.");
            }
            
        }

        public static Unity.Collections.FixedString4096Bytes FindStack(safe_ptr ptr) {
            ref var shard = ref LeakDetectorData.GetShard(ptr.ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.TryGetValue(new LeakDetectorData.Key(ptr.ptr), out var value);
            shard.spinner.Unlock();
            return result == true ? value.stackTrace : default;
        }

        public static LeakDetectorData.Item Find(safe_ptr ptr) {
            ref var shard = ref LeakDetectorData.GetShard(ptr.ptr);
            shard.spinner.Lock();
            LeakDetectorData.Validate(ref shard);
            var result = shard.tracked.TryGetValue(new LeakDetectorData.Key(ptr.ptr), out var value);
            shard.spinner.Unlock();
            return result == true ? value : default;
        }

    }

}
