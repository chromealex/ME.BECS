namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static class UpdateType {

        public const ushort ANY = 0;
        public const ushort UPDATE = 1;
        public const ushort FIXED_UPDATE = 2;
        public const ushort LATE_UPDATE = 3;

        public const ushort MAX = 4;

    }

    public enum WorldMode : byte {

        Logic  = 0,
        Visual = 1,

    }

    public unsafe partial struct World : System.IDisposable {

        public bool isCreated => Worlds.IsAlive(this.id);
        public ushort id;
        public safe_ptr<State> state;
        public string Name => Worlds.GetWorldName(this.id).ToString();

        public ulong CurrentTick => this.state.ptr->tick;

        [INLINE(256)]
        public readonly void AddEndTickHandle(Unity.Jobs.JobHandle handle) {
            
            Worlds.AddEndTickHandle(this.id, handle);
            
        }
        
        [INLINE(256)]
        public static World Create(bool switchContext = true) {
            return World.Create(WorldProperties.Default, switchContext);
        }

        [INLINE(256)]
        public static World Create(WorldProperties properties, bool switchContext = true) {

            var statePtr = State.CreateDefault(properties.allocatorProperties);
            var world = new World() {
                state = statePtr,
            };
            statePtr.ptr->Initialize(statePtr, properties.stateProperties);
            world.state.ptr->WorldState = WorldState.Initialized;

            if (switchContext == true) Context.Switch(world);
            Worlds.AddWorld(ref world, name: properties.name);
            if (switchContext == true) Context.Switch(world);
            State.BurstMode(world.state, true, default);
            return world;

        }

        [INLINE(256)]
        public static World CreateUninitialized(WorldProperties properties, bool switchContext = true) {
            
            var statePtr = State.CreateDefault(properties.allocatorProperties);
            var world = new World() {
                state = statePtr,
            };
            world.state.ptr->WorldState = WorldState.Initialized;

            if (switchContext == true) Context.Switch(world);
            Worlds.AddWorld(ref world, name: properties.name, raiseCallback: false);
            if (switchContext == true) Context.Switch(world);
            State.BurstMode(world.state, true, default);
            return world;
            
        }

        [INLINE(256)]
        public Ent NewEnt() {
            return Ent.New(in this);
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Tick(uint deltaTimeMs, ushort updateType = 0, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            dependsOn = State.SetWorldState(in this, WorldState.BeginTick, updateType, dependsOn);
            dependsOn = this.TickWithoutWorldState(deltaTimeMs, updateType, dependsOn);
            dependsOn = State.SetWorldState(in this, WorldState.EndTick, updateType, dependsOn);

            return dependsOn;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle TickWithoutWorldState(uint deltaTimeMs, ushort updateType, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            Journal.BeginFrame(this.id);

            dependsOn = State.BurstMode(this.state, true, dependsOn);
            dependsOn = Batches.Apply(dependsOn, this.state);
            dependsOn = OneShotTasks.ScheduleJobs(this.state, OneShotType.NextTick, updateType, dependsOn);
            {
                if (updateType == UpdateType.FIXED_UPDATE) dependsOn = State.NextTick(this.state, dependsOn);
                dependsOn = this.TickRootSystemGroup(deltaTimeMs, updateType, dependsOn);
                dependsOn = Batches.Apply(dependsOn, this.state);
            }
            dependsOn = OneShotTasks.ScheduleJobs(this.state, OneShotType.CurrentTick, updateType, dependsOn);
            dependsOn = Batches.Apply(dependsOn, this.state);
            dependsOn = State.BurstMode(this.state, false, dependsOn);

            Journal.EndFrame(this.id);

            return Unity.Jobs.JobHandle.CombineDependencies(dependsOn, Worlds.GetEndTickHandle(this.id));

        }

        [INLINE(256)]
        public void Dispose() {
            this.Dispose(default);
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependsOn) {

            E.IS_CREATED(this);
            if (this.state.ptr == null) return dependsOn;

            if (Context.world.state.ptr == this.state.ptr) Context.world = default;

            dependsOn = this.UnassignRootSystemGroup(dependsOn);
            Worlds.ReleaseWorld(this);
            this.state.ptr->Dispose();
            _free(ref this.state);
            this = default;

            return dependsOn;

        }

    }

    public enum WorldState : byte {

        Undefined   = 0,
        Initialized = 1,
        BeginTick   = 2,
        EndTick     = 3,

    }

}