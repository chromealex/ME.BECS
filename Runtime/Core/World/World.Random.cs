namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
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

        [INLINE(256)]
        public void SetSeed(State* statePtr, uint seed) {
            this.data = seed;
        }

        public static RandomData Create(State* statePtr) {
            return new RandomData() { data = 1u };
        }

    }
    
    internal unsafe struct RandomState : System.IDisposable {

        public State* state;
        public Random random;
        
        [INLINE(256)]
        public RandomState(State* state) {
            this.state = state;
            this.state->random.lockIndex.Lock();
            this.random = new Random(this.state->random.data);
        }

        [INLINE(256)]
        public void Dispose() {
            this.state->random.data = this.random.state;
            this.state->random.lockIndex.Unlock();
        }

    }

}