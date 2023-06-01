namespace ME.BECS {

    using static Cuts;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Jobs;

    [BURST]
    public unsafe struct State {

        public MemoryAllocator allocator;
        public Ents entities;
        public Batches batches;
        public Components components;
        public Archetypes archetypes;
        public Queries queries;
        public AspectsStorage aspectsStorage;
        public ulong tick;
        public WorldState worldState;

        [INLINE(256)]
        public static State* Create(byte[] bytes) {
            State st = default;
            var state = st.Deserialize(bytes);
            return _make(state);
        }

        [INLINE(256)]
        public static State* CreateDefault(AllocatorProperties allocatorProperties) {
            var state = new State() {
                allocator = new MemoryAllocator().Initialize(allocatorProperties.sizeInBytesCapacity),
            };
            return _make(in state);
        }

        [INLINE(256)]
        public static State* Clone(State* srcState) {
            var state = _make(new State());
            state->CopyFrom(in *srcState);
            return state;
        }

        [INLINE(256)]
        public static State* ClonePrepare(State* srcState) {
            var state = _make(new State());
            state->CopyFromPrepare(in *srcState);
            return state;
        }

        [INLINE(256)]
        public static void CloneComplete(State* srcState, State* dstState, int index) {
            dstState->CopyFromComplete(in *srcState, index);
        }

        [INLINE(256)]
        public State Initialize(State* statePtr, in StateProperties stateProperties) {
            
            this.aspectsStorage = AspectsStorage.Create(statePtr);
            this.queries = Queries.Create(statePtr, stateProperties.queriesCapacity);
            this.entities = Ents.Create(statePtr, stateProperties.entitiesCapacity);
            this.batches = Batches.Create(statePtr, stateProperties.entitiesCapacity);
            this.components = Components.Create(statePtr, in stateProperties);
            this.archetypes = Archetypes.Create(statePtr, stateProperties.archetypesCapacity, stateProperties.entitiesCapacity);
            return this;

        }

        [BURST]
        private struct SetWorldStateJob : IJobSingle {

            public World world;
            public WorldState worldState;
            
            public void Execute() {
                if (this.worldState == WorldState.BeginTick) Context.Switch(in this.world);
                this.world.state->worldState = this.worldState;
                //UnityEngine.Debug.Log("SetWorldStateJob: " + this.worldState);
            }

        }

        [BURST]
        private struct NextTickJob : IJobSingle {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            
            public void Execute() {
                ++this.state->tick;
            }

        }

        [BURST]
        private struct BurstModeJob : IJobSingle {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            public bool mode;
            
            public void Execute() {

                this.state->entities.BurstMode(this.state->allocator, this.mode);
                this.state->batches.BurstMode(this.state->allocator, this.mode);
                this.state->components.BurstMode(this.state->allocator, this.mode);
                this.state->archetypes.BurstMode(this.state->allocator, this.mode);

            }

        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle SetWorldState(in World world, WorldState worldState, Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new SetWorldStateJob() {
                world = world,
                worldState = worldState,
            }.ScheduleSingle(dependsOn);
            return dependsOn;
        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle NextTick(State* state, Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new NextTickJob() {
                state = state,
            }.ScheduleSingle(dependsOn);
            return dependsOn;
        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle BurstMode(State* state, bool mode, Unity.Jobs.JobHandle dependsOn) {

            if (dependsOn.IsCompleted == true) {
                new BurstModeJob() {
                    state = state,
                    mode = mode,
                }.Execute();
            } else {
                dependsOn = new BurstModeJob() {
                    state = state,
                    mode = mode,
                }.ScheduleSingle(dependsOn);
            }

            return dependsOn;
        }

        [INLINE(256)]
        public void CopyFrom(in State other) {

            var alloc = this.allocator;
            this = other;
            this.allocator = alloc;
            this.allocator.CopyFrom(other.allocator);

        }

        [INLINE(256)]
        public void CopyFromPrepare(in State other) {

            var alloc = this.allocator;
            this = other;
            this.allocator = alloc;
            this.allocator.CopyFromPrepare(other.allocator);

        }

        [INLINE(256)]
        public void CopyFromComplete(in State other, int index) {

            this.allocator.CopyFromComplete(other.allocator, index);

        }

        [INLINE(256)]
        public void Dispose() {

            this.allocator.Dispose();

        }

    }

}