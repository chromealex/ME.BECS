namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe partial struct World : System.IDisposable {

        public bool isCreated => Worlds.IsAlive(this.id);
        public ushort id;
        [NativeDisableUnsafePtrRestriction]
        public State* state;
        public string Name => Worlds.GetWorldName(this.id).ToString();

        [INLINE(256)]
        public static World Create() {
            return World.Create(WorldProperties.Default);
        }

        [INLINE(256)]
        public static World Create(WorldProperties properties) {

            var statePtr = State.CreateDefault(properties.allocatorProperties);
            var world = new World() {
                state = statePtr,
            };
            statePtr->Initialize(statePtr, properties.stateProperties);
            world.state->worldState = WorldState.Initialized;

            Context.Switch(world);
            Worlds.AddWorld(ref world, name: properties.name);
            Context.Switch(world);
            State.BurstMode(world.state, true, default);
            return world;

        }

        [INLINE(256)]
        public static World CreateUninitialized(WorldProperties properties) {
            
            var statePtr = State.CreateDefault(properties.allocatorProperties);
            var world = new World() {
                state = statePtr,
            };
            world.state->worldState = WorldState.Initialized;

            Context.Switch(world);
            Worlds.AddWorld(ref world, name: properties.name, raiseCallback: false);
            Context.Switch(world);
            State.BurstMode(world.state, true, default);
            return world;
            
        }

        [INLINE(256)]
        public Ent NewEnt() {
            return Ent.New(in this);
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Tick(float dt, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            dependsOn = State.SetWorldState(in this, WorldState.BeginTick, dependsOn);
            dependsOn = this.TickWithoutWorldState(dt, dependsOn);
            dependsOn = State.SetWorldState(in this, WorldState.EndTick, dependsOn);

            return dependsOn;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle TickWithoutWorldState(float dt, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            Journal.BeginFrame(Context.world.id);

            dependsOn = State.BurstMode(this.state, true, dependsOn);
            dependsOn = Batches.BurstModeThreadTasks(dependsOn, this.state, true);
            dependsOn = OneShotTasks.ResolveTasks(this.state, OneShotType.NextTick, dependsOn);
            {
                dependsOn = State.NextTick(this.state, dependsOn);
                dependsOn = this.TickRootSystemGroup(dt, dependsOn);
                dependsOn = Batches.Apply(dependsOn, this.state);
            }
            dependsOn = OneShotTasks.ResolveTasks(this.state, OneShotType.CurrentTick, dependsOn);
            dependsOn = Batches.BurstModeThreadTasks(dependsOn, this.state, false);
            dependsOn = State.BurstMode(this.state, false, dependsOn);

            Journal.EndFrame(Context.world.id);

            return dependsOn;

        }

        [INLINE(256)]
        public void Dispose() {
            this.Dispose(default);
        }

        [INLINE(256)]
        public void Dispose(Unity.Jobs.JobHandle dependsOn) {

            E.IS_CREATED(this);
            if (this.state == null) return;

            if (Context.world.state == this.state) Context.world = default;
            
            this.UnassignRootSystemGroup(dependsOn);
            Worlds.ReleaseWorld(this);
            this.state->Dispose();
            _free(ref this.state);
            this = default;

        }

    }

    public enum WorldState : byte {

        Undefined = 0,
        Initialized,
        BeginTick,
        EndTick,

    }

}