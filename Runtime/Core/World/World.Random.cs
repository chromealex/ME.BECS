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
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Jobs.LowLevel.Unsafe;

    public struct RandomProcessor {

        public Random random;
        
        public RandomProcessor(uint seed) {
            var rnd = new Random(seed);
            rnd.NextFloat4(); // process 4 NextState because of seed may be closed to the next one
            this.random = rnd;
        }
        
    }
    
    public unsafe struct RandomData {

        public uint data;
        public LockSpinner lockIndex;
        
        public int Hash => Utils.Hash(this.data);

        [INLINE(256)]
        public void SetSeed(safe_ptr<State> statePtr, uint seed) {
            this.data = seed;
        }

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