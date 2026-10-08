namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Defines the identifiers used to select system lifecycle phases.
    /// </summary>
    public static class UpdateType {

        /// <summary>
        /// Any constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort ANY = 0;
        /// <summary>
        /// Update constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort UPDATE = 1;
        /// <summary>
        /// Fixed update constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort FIXED_UPDATE = 2;
        /// <summary>
        /// Late update constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort LATE_UPDATE = 3;
        /// <summary>
        /// Awake constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort AWAKE = 4;
        /// <summary>
        /// Start constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort START = 5;

        /// <summary>
        /// Max constant used by <c>UpdateType</c>.
        /// </summary>
        public const ushort MAX = 6;

    }

    /// <summary>
    /// Defines the supported world mode values.
    /// </summary>
    public enum WorldMode : byte {

        /// <summary>
        /// Logic option for <c>WorldMode</c>.
        /// </summary>
        Logic  = 0,
        /// <summary>
        /// Visual option for <c>WorldMode</c>.
        /// </summary>
        Visual = 1,

    }

    /// <summary>
    /// Owns an ECS simulation state, entity storage and scheduled system work.
    /// </summary>
    [IgnoreProfiler]
    public unsafe partial struct World : System.IDisposable, System.IEquatable<World> {

        /// <summary>
        /// Whether this world identifier is currently registered.
        /// </summary>
        public bool isCreated => Worlds.IsAlive(this.id);
        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public ushort id;
        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public safe_ptr<State> state;
        /// <summary>
        /// Full name used by <c>World</c>.
        /// </summary>
        public string FullName => Worlds.GetWorldName(this.id).ToString();
        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public string Name => Worlds.GetWorldSourceName(this.id).ToString();

        /// <summary>
        /// Current tick used by <c>World</c>.
        /// </summary>
        public ulong CurrentTick => this.state.ptr->tick;

        /// <summary>
        /// Adds end tick handle.
        /// </summary>
        [INLINE(256)]
        public readonly void AddEndTickHandle(Unity.Jobs.JobHandle handle) {
            
            Worlds.AddEndTickHandle(this.id, handle);
            
        }
        
        /// <summary>
        /// Allocates and registers a world; system lifecycle callbacks are invoked separately through the world lifecycle API.
        /// </summary>
        [INLINE(256)]
        public static World Create(bool switchContext = true) {
            return World.Create(WorldProperties.Default, switchContext: switchContext);
        }

        /// <summary>
        /// Allocates and registers a world; system lifecycle callbacks are invoked separately through the world lifecycle API.
        /// </summary>
        [INLINE(256)]
        public static World Create(WorldProperties properties, ushort worldId = 0, bool switchContext = true) {

            var statePtr = State.CreateDefault(properties.allocatorProperties);
            var world = new World() {
                state = statePtr,
            };
            statePtr.ptr->Initialize(statePtr, properties.stateProperties);
            world.state.ptr->WorldState = WorldState.Initialized;

            if (switchContext == true) Context.Switch(world);
            Worlds.AddWorld(ref world, worldId, name: properties.name);
            if (switchContext == true) Context.Switch(world);
            State.BurstMode(world.state, true, default);
            return world;

        }

        /// <summary>
        /// Creates uninitialized.
        /// </summary>
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

        /// <summary>
        /// Creates an entity using the supplied world and creation arguments.
        /// </summary>
        [INLINE(256)]
        public Ent NewEnt() {
            return Ent.New(in this);
        }

        /// <summary>
        /// Creates an entity using the supplied world and creation arguments.
        /// </summary>
        [INLINE(256)]
        public Ent NewEnt<T>() where T : unmanaged, IEntityType {
            return Ent.New<T>(in this);
        }

        /// <summary>
        /// Advances simulation through the requested tick interval.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle Tick(uint deltaTimeMs, ushort updateType = 0, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            dependsOn = State.SetWorldState(in this, WorldState.BeginTick, updateType, deltaTimeMs, dependsOn);
            dependsOn = this.TickWithoutWorldState(deltaTimeMs, updateType, dependsOn);
            dependsOn = State.SetWorldState(in this, WorldState.EndTick, updateType, deltaTimeMs, dependsOn);

            return dependsOn;

        }

        /// <summary>
        /// Advances tick processing without changing the world-state marker.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle TickWithoutWorldState(uint deltaTimeMs, ushort updateType, Unity.Jobs.JobHandle dependsOn = default) {

            E.IS_CREATED(this);
            
            Journal.BeginFrame(this.id);

            dependsOn = State.BurstMode(this.state, true, dependsOn);
            dependsOn = Batches.Apply(dependsOn, this.id, this.state);
            dependsOn = OneShotTasks.ScheduleJobs(this.state, OneShotType.NextTick, updateType, dependsOn);
            {
                if (updateType == UpdateType.FIXED_UPDATE) dependsOn = State.NextTick(this.state, dependsOn);
                dependsOn = this.TickRootSystemGroup(deltaTimeMs, updateType, dependsOn);
                dependsOn = Batches.Apply(dependsOn, in this);
            }
            dependsOn = OneShotTasks.ScheduleJobs(this.state, OneShotType.CurrentTick, updateType, dependsOn);
            dependsOn = Batches.Apply(dependsOn, this.id, this.state);
            dependsOn = State.BurstMode(this.state, false, dependsOn);

            Journal.EndFrame(this.id);

            return Unity.Jobs.JobHandle.CombineDependencies(dependsOn, Worlds.GetEndTickHandle(this.id));

        }

        /// <summary>
        /// Unregisters the world and frees its native state through the world destruction lifecycle.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            this.Dispose(default);
        }

        /// <summary>
        /// Unregisters the world and frees its native state through the world destruction lifecycle.
        /// </summary>
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

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(World other) {
            return this.id == other.id;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is World other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return this.id.GetHashCode();
        }

    }

    /// <summary>
    /// Defines the supported world state values.
    /// </summary>
    public enum WorldState : byte {

        /// <summary>
        /// Undefined option for <c>WorldState</c>.
        /// </summary>
        Undefined   = 0,
        /// <summary>
        /// Initialized option for <c>WorldState</c>.
        /// </summary>
        Initialized = 1,
        /// <summary>
        /// Begin tick option for <c>WorldState</c>.
        /// </summary>
        BeginTick   = 2,
        /// <summary>
        /// End tick option for <c>WorldState</c>.
        /// </summary>
        EndTick     = 3,

    }

}