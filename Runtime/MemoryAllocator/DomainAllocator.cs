namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections;
    using Unity.Burst;
    using AOT;

    [BURST]
    public unsafe struct DomainAllocator : AllocatorManager.IAllocator {
        
        private AllocatorManager.AllocatorHandle handle;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK
        private Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<System.IntPtr> pointers;
        private Spinner spinner;
        #endif

        public void Initialize(int capacity) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.spinner = default;
            this.pointers = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<System.IntPtr>(capacity, Allocator.Persistent);
            #endif
            this.handle = Allocator.Persistent;
        }
        
        public void Dispose() {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.spinner.Acquire();
            AllocatorManager.Block block = default;
            block.Range.Allocator = this.handle;
            foreach (var ptr in this.pointers) {
                block.Range.Pointer = ptr;
                this.handle.Try(ref block);
            }
            this.pointers.Dispose();
            this.spinner.Release();
            #endif
            this = default;
        }

        public int Try(ref AllocatorManager.Block block) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            var alloc = false;
            if (block.Range.Pointer == System.IntPtr.Zero) { // Allocate
                alloc = true;
            } else if (block.Bytes == 0) { // Free
                alloc = false;
                this.spinner.Acquire();
                var contains = this.pointers.Contains(block.Range.Pointer);
                this.spinner.Release();
                if (contains == false) {
                    UnityEngine.Debug.LogError("You are trying to free a non-existing memory.");
                    return -1;
                }
            }
            #endif
            block.Range.Allocator = this.handle;
            var result = this.handle.Try(ref block);
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            if (alloc == true) {
                this.spinner.Acquire();
                this.pointers.Add(block.Range.Pointer);
                this.spinner.Release();
            } else {
                this.spinner.Acquire();
                this.pointers.Remove(block.Range.Pointer);
                this.spinner.Release();
            }
            #endif
            return result;
        }

        [ExcludeFromBurstCompatTesting("Uses managed delegate")]
        public AllocatorManager.TryFunction Function => Try;
        public AllocatorManager.AllocatorHandle Handle {
            get => this.handle;
            set => this.handle = value;
        }
        public Allocator ToAllocator => this.handle.ToAllocator;
        public bool IsCustomAllocator => true;

        [BURST]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        internal static int Try(System.IntPtr state, ref AllocatorManager.Block block) {
            unsafe { return ((DomainAllocator*)state)->Try(ref block); }
        }
        
    }

}