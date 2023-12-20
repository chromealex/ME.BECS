namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using Unity.Jobs.LowLevel.Unsafe;

    public unsafe struct RandomData {

        private MemArray<uint> data;

        [INLINE(256)]
        public uint Get(State* statePtr) {
            return this.data[statePtr, JobsUtility.ThreadIndex];
        }

        [INLINE(256)]
        public void Set(State* statePtr, uint state) {
            this.data[statePtr, JobsUtility.ThreadIndex] = state;
        }
        
        [INLINE(256)]
        public static RandomData Create(State* statePtr) {
            var rnd = new RandomData() {
                data = new MemArray<uint>(ref statePtr->allocator, (uint)JobsUtility.ThreadIndexCount),
            };
            return rnd;
        }

        [INLINE(256)]
        public void SetSeed(State* statePtr, uint seed, bool allThreads) {

            if (allThreads == false) {
                this.data[statePtr, JobsUtility.ThreadIndex] = seed;
            } else {
                for (uint i = 0u; i < this.data.Length; ++i) {
                    this.data[statePtr, i] = seed;
                }
            }

        }

    }
    
    internal unsafe struct RandomState : System.IDisposable {

        public State* state;
        public Random random;
        
        [INLINE(256)]
        public RandomState(State* state) {
            this.state = state;
            this.random = new Random(this.state->random.Get(state));
        }

        [INLINE(256)]
        public void Dispose() {
            this.state->random.Set(this.state, this.random.state);
        }

    }

    public unsafe partial struct World {

        [INLINE(256)]
        public void SetSeed(uint seed, bool allThreads = false) {
            E.RANGE(seed, 1u, uint.MaxValue);
            this.state->random.SetSeed(this.state, seed, allThreads);
        }
        
        [INLINE(256)]
        public float3 GetRandomVector3InSphere(float radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat3() * radius;
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float2 GetRandomVector2InCircle(float radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat2() * radius;
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float2 GetRandomVector2OnCircle(float radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat2Direction() * radius;
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float3 GetRandomVector3OnSphere(float radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat3Direction() * radius;
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float GetRandomValue() {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat();
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float GetRandomValue(float min, float max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat(min, max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float GetRandomValue(float max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat(max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float2 GetRandomVector2() {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat2();
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float2 GetRandomVector2(float2 min, float2 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat2(min, max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float2 GetRandomVector2(float2 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat2(max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float3 GetRandomVector3() {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat3();
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float3 GetRandomVector3(float3 min, float3 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat3(min, max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float3 GetRandomVector3(float3 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat3(max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float4 GetRandomVector4() {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat4();
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float4 GetRandomVector4(float4 min, float4 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat4(min, max);
            rnd.Dispose();
            return result;
        }

        [INLINE(256)]
        public float4 GetRandomVector4(float4 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomState(this.state);
            var result = rnd.random.NextFloat4(max);
            rnd.Dispose();
            return result;
        }

    }

}