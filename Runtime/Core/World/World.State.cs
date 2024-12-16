using Unity.Jobs;

namespace ME.BECS {

    using static Cuts;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Jobs;

    [Unity.Collections.GenerateTestsForBurstCompatibility]
    [BURST(CompileSynchronously = true)]
    public unsafe struct State {

        public MemoryAllocator allocator;
        public Ents entities;
        public Batches batches;
        public OneShotTasks oneShotTasks;
        public Components components;
        public Archetypes archetypes;
        public Queries queries;
        public AspectsStorage aspectsStorage;
        public RandomData random;
        public CollectionsRegistry collectionsRegistry;
        public AutoDestroyRegistry autoDestroyRegistry;
        public ulong tick;
        public WorldState worldState;
        public byte tickCheck;
        public ushort updateType;
        public WorldMode mode;

        public bool IsCreated => this.tick != 0UL;

        [INLINE(256)]
        public static SafePtr<State> Create(byte[] bytes) {
            State st = default;
            var state = st.Deserialize(bytes);
            return _make(state);
        }

        [INLINE(256)]
        public static SafePtr<State> CreateDefault(AllocatorProperties allocatorProperties) {
            var state = new State() {
                allocator = new MemoryAllocator().Initialize(allocatorProperties.sizeInBytesCapacity),
            };
            return _make(in state);
        }

        [INLINE(256)]
        public static SafePtr<State> Clone(SafePtr<State> srcState) {
            var state = _make(new State());
            state.ptr->CopyFrom(in *srcState.ptr);
            return state;
        }

        [INLINE(256)]
        public static SafePtr<State> ClonePrepare(SafePtr<State> srcState) {
            var state = _make(new State());
            state.ptr->CopyFromPrepare(in *srcState.ptr);
            return state;
        }

        [INLINE(256)]
        public static void CloneComplete(SafePtr<State> srcState, SafePtr<State> dstState, int index) {
            dstState.ptr->CopyFromComplete(in *srcState.ptr, index);
        }

        [INLINE(256)]
        public State Initialize(SafePtr<State> statePtr, in StateProperties stateProperties) {
            
            this.aspectsStorage = AspectsStorage.Create(statePtr);
            this.queries = Queries.Create(statePtr, stateProperties.queriesCapacity);
            this.entities = Ents.Create(statePtr, stateProperties.entitiesCapacity);
            this.batches = Batches.Create(statePtr, stateProperties.entitiesCapacity);
            this.oneShotTasks = OneShotTasks.Create(statePtr, stateProperties.oneShotTasksCapacity);
            this.components = Components.Create(statePtr, in stateProperties);
            this.archetypes = Archetypes.Create(statePtr, stateProperties.archetypesCapacity, stateProperties.entitiesCapacity);
            this.random = RandomData.Create(statePtr);
            this.collectionsRegistry = CollectionsRegistry.Create(statePtr, stateProperties.entitiesCapacity);
            this.autoDestroyRegistry = AutoDestroyRegistry.Create(statePtr, stateProperties.entitiesCapacity);
            this.mode = stateProperties.mode;
            return this;

        }

        [BURST(CompileSynchronously = true)]
        private struct SetWorldStateJob : IJobSingle {

            public World world;
            public WorldState worldState;
            public ushort updateType;
            
            public void Execute() {
                if (this.worldState == WorldState.BeginTick) Context.Switch(in this.world);
                this.world.state.ptr->worldState = this.worldState;
                this.world.state.ptr->tickCheck = 1;
                this.world.state.ptr->updateType = this.updateType;
            }

        }

        [BURST(CompileSynchronously = true)]
        private struct NextTickJob : IJobSingle {

            [NativeDisableUnsafePtrRestriction]
            public SafePtr<State> state;
            
            public void Execute() {
                ++this.state.ptr->tick;
            }

        }

        [BURST(CompileSynchronously = true)]
        private struct BurstModeJob : IJobSingle {

            [NativeDisableUnsafePtrRestriction]
            public SafePtr<State> state;
            public bool mode;
            
            public void Execute() {

                this.state.ptr->entities.BurstMode(this.state.ptr->allocator, this.mode);
                this.state.ptr->batches.BurstMode(this.state.ptr->allocator, this.mode);
                this.state.ptr->components.BurstMode(this.state.ptr->allocator, this.mode);
                this.state.ptr->archetypes.BurstMode(this.state.ptr->allocator, this.mode);

            }

        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle SetWorldState(in World world, WorldState worldState, ushort updateType, Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new SetWorldStateJob() {
                world = world,
                worldState = worldState,
                updateType = updateType,
            }.ScheduleSingle(dependsOn);
            return dependsOn;
        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle NextTick(SafePtr<State> state, Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new NextTickJob() {
                state = state,
            }.ScheduleSingle(dependsOn);
            return dependsOn;
        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle BurstMode(SafePtr<State> state, bool mode, Unity.Jobs.JobHandle dependsOn) {
            #if USE_CACHE_PTR
            dependsOn = new BurstModeJob() {
                state = state,
                mode = mode,
            }.ScheduleSingleDeps(dependsOn);
            #endif
            return dependsOn;
        }

        [INLINE(256)]
        public void CopyFrom(in State other) {

            var alloc = this.allocator;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandlers = this.components.handlers;
            var safetyHandlersLock = this.components.handlersLock;
            #endif
            this = other;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.components.handlers = safetyHandlers;
            this.components.handlersLock = safetyHandlersLock;
            this.components.SafetyHandlersCopyFrom(in other.components);
            #endif
            this.allocator = alloc;
            this.allocator.CopyFrom(in other.allocator);

        }

        [INLINE(256)]
        public void CopyFromPrepare(in State other) {

            var alloc = this.allocator;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandlers = this.components.handlers;
            var safetyHandlersLock = this.components.handlersLock;
            #endif
            this = other;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.components.handlers = safetyHandlers;
            this.components.handlersLock = safetyHandlersLock;
            this.components.SafetyHandlersCopyFrom(in other.components);
            #endif
            this.allocator = alloc;
            this.allocator.CopyFromPrepare(in other.allocator);

        }

        [INLINE(256)]
        public void CopyFromComplete(in State other, int index) {

            this.allocator.CopyFromComplete(in other.allocator, index);

        }

        [INLINE(256)]
        public void Dispose() {

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.components.DisposeSafetyHandlers();
            #endif
            this.allocator.Dispose();

        }

    }

}