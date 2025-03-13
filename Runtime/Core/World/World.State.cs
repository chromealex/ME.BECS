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
        public byte state;
        public byte tickCheck;
        public ushort updateType;

        public WorldState WorldState {
            get {
                if ((this.state & (1 << 0)) != 0) return WorldState.Initialized;
                if ((this.state & (1 << 1)) != 0) return WorldState.BeginTick;
                if ((this.state & (1 << 2)) != 0) return WorldState.EndTick;
                return default;
            }
            set {
                this.state = (byte)(this.state & ~(1 << 0));
                this.state = (byte)(this.state & ~(1 << 1));
                this.state = (byte)(this.state & ~(1 << 2));
                if (value != default) this.state = (byte)(this.state | (1 << ((int)value - 1)));
            }
        }
        public WorldMode Mode {
            get {
                if ((this.state & (1 << 5)) != 0) return WorldMode.Visual;
                return default;
            }
            set {
                this.state = (byte)(this.state & ~(1 << 4));
                this.state = (byte)(this.state & ~(1 << 5));
                if (value != default) this.state = (byte)(this.state | (1 << (4 + (int)value)));
            }
        }

        public bool IsCreated => this.tick != 0UL;

        public int Hash => Utils.Hash(this.entities.Hash, this.components.Hash, this.random.Hash, this.tick);

        [INLINE(256)]
        public static safe_ptr<State> Create(byte[] bytes) {
            State st = default;
            var state = st.Deserialize(bytes);
            return _make(state);
        }

        [INLINE(256)]
        public static safe_ptr<State> CreateDefault(AllocatorProperties allocatorProperties) {
            var state = new State() {
                allocator = new MemoryAllocator().Initialize(allocatorProperties.sizeInBytesCapacity),
            };
            return _makeDefault(in state);
        }

        [INLINE(256)]
        public static safe_ptr<State> Clone(safe_ptr<State> srcState) {
            var state = _make(new State());
            state.ptr->CopyFrom(in *srcState.ptr);
            return state;
        }

        [INLINE(256)]
        public static safe_ptr<State> ClonePrepare(safe_ptr<State> srcState) {
            var state = _make(new State());
            state.ptr->CopyFromPrepare(in *srcState.ptr);
            return state;
        }

        [INLINE(256)]
        public static void CloneComplete(safe_ptr<State> srcState, safe_ptr<State> dstState, int index) {
            dstState.ptr->CopyFromComplete(in *srcState.ptr, index);
        }

        [INLINE(256)]
        public State Initialize(safe_ptr<State> statePtr, in StateProperties stateProperties) {
            
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
            this.Mode = stateProperties.mode;
            return this;

        }

        [BURST(CompileSynchronously = true)]
        private struct SetWorldStateJob : IJobSingle {

            public World world;
            public WorldState worldState;
            public ushort updateType;
            
            public void Execute() {
                if (this.worldState == WorldState.BeginTick) Context.Switch(in this.world);
                this.world.state.ptr->WorldState = this.worldState;
                this.world.state.ptr->tickCheck = 1;
                this.world.state.ptr->updateType = this.updateType;
            }

        }

        [BURST(CompileSynchronously = true)]
        private struct NextTickJob : IJobSingle {

            public safe_ptr<State> state;
            
            public void Execute() {
                ++this.state.ptr->tick;
            }

        }

        [BURST(CompileSynchronously = true)]
        private struct BurstModeJob : IJobSingle {

            public safe_ptr<State> state;
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
        public static Unity.Jobs.JobHandle NextTick(safe_ptr<State> state, Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new NextTickJob() {
                state = state,
            }.ScheduleSingle(dependsOn);
            return dependsOn;
        }

        [INLINE(256)]
        public static Unity.Jobs.JobHandle BurstMode(safe_ptr<State> state, bool mode, Unity.Jobs.JobHandle dependsOn) {
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