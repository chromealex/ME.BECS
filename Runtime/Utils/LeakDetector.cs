namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using System.Diagnostics;

    public unsafe class LeakDetectorData {

        public struct Item : System.IEquatable<Item> {

            public System.IntPtr ptr;
            public System.IntPtr hiPtr;
            public readonly Unity.Collections.Allocator allocator;
            public Unity.Collections.FixedString4096Bytes stackTrace;

            public Item(void* ptr, void* hiPtr, Unity.Collections.Allocator allocator = Unity.Collections.Allocator.None, bool withStackTrace = true) {
                this = default;
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
                if (str.Length < 2000) {
                    this.stackTrace = str;
                    return;
                }
                this.stackTrace = str.Substring(0, 2000);
            }

            public bool Equals(Item other) {
                return this.ptr == other.ptr;
            }

            public override bool Equals(object obj) {
                return obj is Item other && this.Equals(other);
            }

            public override int GetHashCode() {
                return this.ptr.GetHashCode();
            }

        }
        
        public static readonly SharedStatic<UnsafeHashSet<Item>> tracked = SharedStatic<UnsafeHashSet<Item>>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<UnsafeHashSet<Item>>.align, 1L);
        public static readonly SharedStatic<LockSpinner> spinner = SharedStatic<LockSpinner>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<LockSpinner>.align, 2L);
        public static readonly SharedStatic<Internal.Array<int>> counter = SharedStatic<Internal.Array<int>>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<Internal.Array<int>>.align, 3L);
        public static readonly SharedStatic<LockSpinner> counterSpinner = SharedStatic<LockSpinner>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<LockSpinner>.align, 4L);
        public static readonly SharedStatic<bbool> counterAwait = SharedStatic<bbool>.GetOrCreatePartiallyUnsafeWithHashCode<LeakDetectorData>(TAlign<bbool>.align, 5L);

        public static void Validate() {
            if (tracked.Data.IsCreated == false) tracked.Data = new UnsafeHashSet<Item>(100, Constants.ALLOCATOR_DOMAIN);
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
        public static void TrackAllocator(void* ptr) {

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            LeakDetectorData.tracked.Data.Add(new LeakDetectorData.Item(ptr, ptr, Unity.Collections.Allocator.FirstUserIndex));
            LeakDetectorData.spinner.Data.Unlock();

        }

        [Conditional(COND.LEAK_DETECTION_ALLOCATOR)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void TrackAllocator(safe_ptr ptr) {

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            LeakDetectorData.tracked.Data.Add(new LeakDetectorData.Item(ptr.ptr, ptr.HiBound, Unity.Collections.Allocator.FirstUserIndex));
            LeakDetectorData.spinner.Data.Unlock();

        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Track(void* ptr, Unity.Collections.Allocator allocator) {

            if (IsTrackable(allocator) == false) return;
            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            LeakDetectorData.tracked.Data.Add(new LeakDetectorData.Item(ptr, ptr, allocator));
            LeakDetectorData.spinner.Data.Unlock();

        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Track(safe_ptr ptr, Unity.Collections.Allocator allocator) {

            if (IsTrackable(allocator) == false) return;
            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            LeakDetectorData.tracked.Data.Add(new LeakDetectorData.Item(ptr.ptr, ptr.HiBound, allocator));
            LeakDetectorData.spinner.Data.Unlock();

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
        public static void FreeAllocator(void* ptr) {

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            var result = LeakDetectorData.tracked.Data.Remove(new LeakDetectorData.Item(ptr, ptr));
            LeakDetectorData.spinner.Data.Unlock();
            if (result == false) {
                throw new System.Exception($"You are trying to free pointer {((System.IntPtr)ptr).ToInt64()} which has been already freed or was never instantiated.");
            }
            
        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Free(safe_ptr ptr, Unity.Collections.Allocator allocator) {

            if (IsTrackable(allocator) == false) return;
            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            var result = LeakDetectorData.tracked.Data.Remove(new LeakDetectorData.Item(ptr.ptr, ptr.HiBound));
            LeakDetectorData.spinner.Data.Unlock();
            if (result == false) {
                throw new System.Exception($"You are trying to free pointer {((System.IntPtr)ptr.ptr).ToInt64()} which has been already freed or was never instantiated.");
            }
            
        }

        public static void ClearAllocated() {
            LeakDetectorData.counter.Data.Dispose();
            LeakDetectorData.tracked.Data.Clear();
        }

        public static void PrintAllocated(Unity.Collections.Allocator allocator) {
            
            if (LeakDetectorData.counter.Data.IsCreated == true) {
                for (int i = 0; i < 10; ++i) {
                    UnityEngine.Debug.Log($"Allocated: {LeakDetectorData.counter.Data.Get(i)} in {(Unity.Collections.Allocator)i}");
                }
            }

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            foreach (var item in LeakDetectorData.tracked.Data) {
                if (item.IsTrackableAllocator() == false) continue;
                if (allocator != Unity.Collections.Allocator.None && allocator != item.allocator) continue; 
                var str = item.stackTrace.ToString();
                if (str.Contains("UnsafeEntityConfig") == true || str.Contains("ME.BECS.Gen") == true) continue;
                UnityEngine.Debug.Log($"{item.ptr} - {item.allocator}\n{str}");
            }
            LeakDetectorData.spinner.Data.Unlock();
            
        }

        [Conditional(COND.LEAK_DETECTION)]
        public static void IsAlive(safe_ptr ptr) {
            
            if (ptr.LowBound == null || ptr.HiBound == null) return;
            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            var result = LeakDetectorData.tracked.Data.Contains(new LeakDetectorData.Item(ptr.ptr, ptr.HiBound, withStackTrace: false));
            LeakDetectorData.spinner.Data.Unlock();
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

    }

}