namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections;
    using Unity.Burst;
    using AOT;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Defines domain allocator state and operations.
    /// </summary>
    [IgnoreProfiler]
    [BURST]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct DomainAllocator : AllocatorManager.IAllocator {
        
        private AllocatorManager.AllocatorHandle handle;
        #if MEMORY_ALLOCATOR_BOUNDS_CHECK
        private Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<System.IntPtr> pointers;
        private Spinner spinner;
        #endif

        /// <summary>
        /// Initializes domain allocator state from the supplied context.
        /// </summary>
        public void Initialize(int capacity) {
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            this.spinner = default;
            this.pointers = new Unity.Collections.LowLevel.Unsafe.UnsafeHashSet<System.IntPtr>(capacity, Allocator.Persistent);
            #endif
            this.handle = Allocator.Persistent;
        }
        
        /// <summary>
        /// Releases the resources owned by this domain allocator instance.
        /// </summary>
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

        /// <summary>
        /// Attempts to  and reports whether the operation succeeded.
        /// </summary>
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

        /// <summary>
        /// Function used by <c>DomainAllocator</c>.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Uses managed delegate")]
        public AllocatorManager.TryFunction Function => Try;
        /// <summary>
        /// Handle used by <c>DomainAllocator</c>.
        /// </summary>
        public AllocatorManager.AllocatorHandle Handle {
            get => this.handle;
            set => this.handle = value;
        }
        /// <summary>
        /// Gets to allocator; this implementation returns <c>this.handle.ToAllocator</c>.
        /// </summary>
        public Allocator ToAllocator => this.handle.ToAllocator;
        /// <summary>
        /// Gets is custom allocator; this implementation returns <c>true</c>.
        /// </summary>
        public bool IsCustomAllocator => true;

        [BURST]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        internal static int Try(System.IntPtr state, ref AllocatorManager.Block block) {
            unsafe { return ((DomainAllocator*)state)->Try(ref block); }
        }
        
    }

}