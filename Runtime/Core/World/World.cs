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

    }

    public enum WorldMode : byte {

        Logic = 0,
        Visual = 1,

    }

    public unsafe partial struct World : System.IDisposable {

        public bool isCreated => Worlds.IsAlive(this.id);
        public ushort id;
        [NativeDisableUnsafePtrRestriction]
        public State* state;
        public string Name => Worlds.GetWorldName(this.id).ToString();

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
            statePtr->Initialize(statePtr, properties.stateProperties);
            world.state->worldState = WorldState.Initialized;

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
            world.state->worldState = WorldState.Initialized;

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
        public Unity.Jobs.JobHandle Tick(float dt, ushort updateType = 0, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            dependsOn = State.SetWorldState(in this, WorldState.BeginTick, updateType, dependsOn);
            dependsOn = this.TickWithoutWorldState(dt, updateType, dependsOn);
            dependsOn = State.SetWorldState(in this, WorldState.EndTick, updateType, dependsOn);

            return dependsOn;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle TickWithoutWorldState(float dt, ushort updateType, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            Journal.BeginFrame(this.id);

            dependsOn = State.BurstMode(this.state, true, dependsOn);
            dependsOn = Batches.Apply(dependsOn, this.state);
            dependsOn = OneShotTasks.ResolveTasks(this.state, OneShotType.NextTick, updateType, dependsOn);
            {
                dependsOn = State.NextTick(this.state, dependsOn);
                dependsOn = this.TickRootSystemGroup(dt, updateType, dependsOn);
                dependsOn = Batches.Apply(dependsOn, this.state);
            }
            dependsOn = OneShotTasks.ResolveTasks(this.state, OneShotType.CurrentTick, updateType, dependsOn);
            dependsOn = Batches.Apply(dependsOn, this.state);
            dependsOn = State.BurstMode(this.state, false, dependsOn);

            Journal.EndFrame(this.id);

            return dependsOn;

        }

        [INLINE(256)]
        public void Dispose() {
            this.Dispose(default);
        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependsOn) {

            E.IS_CREATED(this);
            if (this.state == null) return dependsOn;

            if (Context.world.state == this.state) Context.world = default;

            dependsOn = this.UnassignRootSystemGroup(dependsOn);
            Worlds.ReleaseWorld(this);
            this.state->Dispose();
            _free(ref this.state);
            this = default;

            return dependsOn;

        }

    }

    public enum WorldState : byte {

        Undefined = 0,
        Initialized,
        BeginTick,
        EndTick,

    }

}