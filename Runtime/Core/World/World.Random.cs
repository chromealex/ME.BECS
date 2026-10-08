#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary>
    /// Produces random values from the associated deterministic state.
    /// </summary>
    public struct RandomProcessor {

        /// <summary>
        /// Random used by <c>RandomProcessor</c>.
        /// </summary>
        public Random random;
        
        /// <summary>
        /// Initializes <c>RandomProcessor</c> from the supplied seed.
        /// </summary>
        public RandomProcessor(uint seed) {
            var rnd = new Random(seed);
            rnd.NextFloat4(); // process 4 NextState because of seed may be closed to the next one
            this.random = rnd;
        }
        
    }
    
    /// <summary>
    /// Stores deterministic random-generator state.
    /// </summary>
    public struct RandomData {

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public uint data;
        /// <summary>
        /// Index of the synchronization lock used for this entry.
        /// </summary>
        public LockSpinner lockIndex;
        
        /// <summary>
        /// Indicates hash.
        /// </summary>
        public int Hash => Utils.Hash(this.data);

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.data);
            writer.Write(this.lockIndex);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.data);
            reader.Read(ref this.lockIndex);
        }

        /// <summary>
        /// Sets seed.
        /// </summary>
        [INLINE(256)]
        public void SetSeed(safe_ptr<State> statePtr, uint seed) {
            this.data = seed;
        }

        /// <summary>
        /// Creates <c>RandomData</c> using the supplied creation arguments.
        /// </summary>
        public static RandomData Create(safe_ptr<State> statePtr) {
            return new RandomData() { data = 1u };
        }

    }
    
    internal unsafe struct RandomState : System.IDisposable {

        public safe_ptr<State> state;
        public Random random;
        
        [INLINE(256)]
        public RandomState(safe_ptr<State> state) {
            this.state = state;
            this.state.ptr->random.lockIndex.Lock();
            this.random = new Random(this.state.ptr->random.data);
        }

        [INLINE(256)]
        public void Dispose() {
            this.state.ptr->random.data = this.random.state;
            this.state.ptr->random.lockIndex.Unlock();
        }

    }

}