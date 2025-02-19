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
            public Unity.Collections.FixedString4096Bytes stackTrace;

            public Item(void* ptr, void* hiPtr, bool withStackTrace = true) {
                this = default;
                this.ptr = (System.IntPtr)ptr;
                this.hiPtr = (System.IntPtr)hiPtr;
                if (withStackTrace == true) this.AddStackTrace();
            }

            [BURST_DISCARD]
            public void AddStackTrace() {
                var str = UnityEngine.StackTraceUtility.ExtractStackTrace();
                if (str.Length < 1000) {
                    this.stackTrace = str;
                    return;
                }
                this.stackTrace = str.Substring(0, 1000);
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

        public static void Validate() {
            if (tracked.Data.IsCreated == false) tracked.Data = new UnsafeHashSet<Item>(100, Constants.ALLOCATOR_DOMAIN);
        }

    }
    
    public unsafe struct LeakDetector {

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Track(void* ptr) {

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            LeakDetectorData.tracked.Data.Add(new LeakDetectorData.Item(ptr, ptr));
            LeakDetectorData.spinner.Data.Unlock();

        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Track(safe_ptr ptr) {

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            LeakDetectorData.tracked.Data.Add(new LeakDetectorData.Item(ptr.LowBound, ptr.HiBound));
            LeakDetectorData.spinner.Data.Unlock();

        }

        [Conditional(COND.LEAK_DETECTION)]
        [HIDE_CALLSTACK]
        [INLINE(256)]
        public static void Free(void* ptr) {
            
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
        public static void Free(safe_ptr ptr) {

            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            var result = LeakDetectorData.tracked.Data.Remove(new LeakDetectorData.Item(ptr.LowBound, ptr.HiBound));
            LeakDetectorData.spinner.Data.Unlock();
            if (result == false) {
                throw new System.Exception($"You are trying to free pointer {((System.IntPtr)ptr.LowBound).ToInt64()} which has been already freed or was never instantiated.");
            }
            
        }

        [Conditional(COND.LEAK_DETECTION)]
        public static void PrintAllocated() {
            
            LeakDetectorData.spinner.Data.Lock();
            LeakDetectorData.Validate();
            foreach (var item in LeakDetectorData.tracked.Data) {
                var str = item.stackTrace.ToString();
                if (str.Contains("UnsafeEntityConfig") == true || str.Contains("ME.BECS.Gen") == true) continue;
                UnityEngine.Debug.Log($"{item.ptr}\n{str}");
            }
            LeakDetectorData.spinner.Data.Unlock();
            
        }

        [Conditional(COND.LEAK_DETECTION)]
        public static void IsAlive(safe_ptr ptr) {
            
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