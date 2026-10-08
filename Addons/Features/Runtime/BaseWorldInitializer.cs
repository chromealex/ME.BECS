using UnityEngine;

namespace ME.BECS {
    
    using Unity.Jobs;
    using Unity.Collections;
    using scg = System.Collections.Generic;

    /// <summary>
    /// Coordinates initialization of world s.
    /// </summary>
    public static class WorldInitializers {

        private static readonly scg::List<BaseWorldInitializer> list = new scg::List<BaseWorldInitializer>();

        /// <summary>
        /// Returns by world name.
        /// </summary>
        public static BaseWorldInitializer GetByWorldName(FixedString64Bytes worldName) {
            foreach (var item in list) {
                if (item != null && item.properties.name == worldName) {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns by world name.
        /// </summary>
        public static BaseWorldInitializer GetByWorldName(string worldName) {
            foreach (var item in list) {
                if (item != null && item.properties.name.ToString() == worldName) {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Adds the supplied entry to world initializers.
        /// </summary>
        public static void Add(BaseWorldInitializer initializer) {
            list.Add(initializer);
        }

        /// <summary>
        /// Removes the specified entry from world initializers.
        /// </summary>
        public static void Remove(BaseWorldInitializer initializer) {
            list.Remove(initializer);
        }

    }

    /// <summary>
    /// Defines the operations required by graph initialize.
    /// </summary>
    public interface IGraphInitialize {

        /// <summary>
        /// Initializes i graph initialize state from the supplied context.
        /// </summary>
        void Initialize(ref SystemGroup group, ref World world);

    }

    /// <summary>
    /// Coordinates initialization of base world.
    /// </summary>
    public abstract class BaseWorldInitializer<T> : BaseWorldInitializer where T : IGraphInitialize {

        /// <summary>
        /// Defines graphs state and operations for <c>BaseWorldInitializer</c>.
        /// </summary>
        [System.Serializable]
        public struct Graphs {

            /// <summary>
            /// Entries stored by this container.
            /// </summary>
            public T[] items;

            /// <summary>
            /// Initializes graphs state from the supplied context.
            /// </summary>
            public void Initialize(ref SystemGroup group, ref World world) {
                foreach (var graph in this.items) {
                    graph.Initialize(ref group, ref world);
                }
            }

        }

        /// <summary>
        /// Graphs used by <c>BaseWorldInitializer</c>.
        /// </summary>
        public Graphs graphs;
        
    }
    
    /// <summary>
    /// Coordinates initialization of base world.
    /// </summary>
    public abstract class BaseWorldInitializer : MonoBehaviour {

        /// <summary>
        /// Defines modules state and operations for <c>BaseWorldInitializer</c>.
        /// </summary>
        [System.Serializable]
        public struct Modules {

            /// <summary>
            /// List storage used by this instance.
            /// </summary>
            public OptionalModule[] list;

            /// <summary>
            /// Tests whether the requested entry is present.
            /// </summary>
            public bool Has<T>() where T : Module {
                for (int i = 0; i < this.list.Length; ++i) {
                    if (this.list[i].IsEnabled() == true && this.list[i].obj is T) return true;
                }
                return false;
            }

            /// <summary>
            /// Returns the requested entry from modules.
            /// </summary>
            public T Get<T>() where T : Module {
                for (int i = 0; i < this.list.Length; ++i) {
                    if (this.list[i].IsEnabled() == true && this.list[i].obj is T module) return module;
                }
                return null;
            }

            /// <summary>
            /// Loads the registered data required by this operation.
            /// </summary>
            public void Load() {

                for (int i = 0; i < this.list.Length; ++i) {
                    this.list[i].obj = Object.Instantiate(this.list[i].obj);
                }
                
            }
            
        }

        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public WorldProperties properties = WorldProperties.Default;
        /// <summary>
        /// Modules used by <c>BaseWorldInitializer</c>.
        /// </summary>
        public Modules modules = new Modules() {
            list = System.Array.Empty<OptionalModule>(),
        };
        /// <summary>
        /// World used by the containing operation.
        /// </summary>
        public World world;
        /// <summary>
        /// Previous frame depends on used by <c>BaseWorldInitializer</c>.
        /// </summary>
        protected JobHandle previousFrameDependsOn;
        private static BaseWorldInitializer instance;
        
        /// <summary>
        /// Returns instance.
        /// </summary>
        public static BaseWorldInitializer GetInstance() => instance;

        /// <summary>
        /// Returns module.
        /// </summary>
        public T GetModule<T>() where T : Module {
            
            for (var i = 0; i < this.modules.list.Length; ++i) {
                var module = this.modules.list[i];
                if (module.IsEnabled() == false) continue;
                if (module.obj is T moduleInstance) return moduleInstance;
            }

            return null;

        }

        /// <summary>
        /// Creates world.
        /// </summary>
        protected virtual World CreateWorld() => World.Create(this.properties, this.worldId);

        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        protected virtual ushort worldId => 0;
        
        /// <summary>
        /// Runs initialization for the associated lifecycle.
        /// </summary>
        protected virtual void Awake() {

            instance = this;
            WorldInitializers.Add(this);

            this.modules.Load();
            this.world = this.CreateWorld();
            this.DoWorldAwake();

        }

        /// <summary>
        /// Invokes world initialization through the configured lifecycle handler.
        /// </summary>
        protected virtual void DoWorldAwake() {
            
            Context.Switch(in this.world);
            this.previousFrameDependsOn.Complete();
            for (var i = 0; i < this.modules.list.Length; ++i) {
                var module = this.modules.list[i];
                if (module.IsEnabled() == false) continue;
                module.obj.worldProperties = this.properties;
                this.previousFrameDependsOn.Complete();
                module.obj.OnAwake(ref this.world);
            }

            this.OnAwake();

            this.previousFrameDependsOn = this.world.Awake(this.previousFrameDependsOn, UpdateType.AWAKE);
            
        }

        /// <summary>
        /// Starts base world initializer processing for the supplied context.
        /// </summary>
        protected virtual void Start() {

            if (this.world.isCreated == true) {
                
                this.previousFrameDependsOn.Complete();
                Context.Switch(in this.world);
                for (var i = 0; i < this.modules.list.Length; ++i) {
                    var module = this.modules.list[i];
                    if (module.IsEnabled() == false) continue;
                    module.obj.worldProperties = this.properties;
                    this.previousFrameDependsOn.Complete();
                    this.previousFrameDependsOn = module.obj.OnStart(ref this.world, this.previousFrameDependsOn);
                }
                
                this.previousFrameDependsOn = this.OnStart(this.previousFrameDependsOn);

                this.DoWorldStart();

                this.previousFrameDependsOn.Complete();

            }

        }

        /// <summary>
        /// Invokes world start.
        /// </summary>
        protected virtual void DoWorldStart() {
            
            this.previousFrameDependsOn = this.world.Start(this.previousFrameDependsOn, UpdateType.START);
            
        }

        /// <summary>
        /// Provides the <c>OnAwake</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void OnAwake() {
            
        }

        /// <summary>
        /// Starts base world initializer processing for the supplied context.
        /// </summary>
        public virtual JobHandle OnStart(JobHandle dependsOn) {
            return dependsOn;
        }

        /// <summary>
        /// Returns delta time ms.
        /// </summary>
        public virtual uint GetDeltaTimeMs() {
            return (uint)(Time.deltaTime * 1000u);
        }

        /// <summary>
        /// Invokes update.
        /// </summary>
        protected JobHandle DoUpdate(ushort updateType, JobHandle dependsOn) {

            this.previousFrameDependsOn = dependsOn;
            if (this.world.isCreated == true) {
                this.previousFrameDependsOn = this.world.Tick(this.GetDeltaTimeMs(), updateType, this.previousFrameDependsOn);
            }

            return this.previousFrameDependsOn;

        }

        /// <summary>
        /// Updates base world initializer using the current inputs and execution context.
        /// </summary>
        public virtual JobHandle OnUpdate(JobHandle dependsOn) {
            if (this.world.isCreated == true) {
                ProfilerCounters.Initialize();
                ProfilerCounters.SampleWorldBeginFrame(in this.world);
                dependsOn = this.world.RaiseEvents(dependsOn);
            }
            return dependsOn;
        }

        /// <summary>
        /// Runs the late-update phase for the associated state.
        /// </summary>
        protected virtual void LateUpdate() {

            this.previousFrameDependsOn.Complete();

            if (this.world.isCreated == true) {
                ProfilerCounters.SampleWorldEndFrame(in this.world);
            }

            this.previousFrameDependsOn.Complete();
            WorldsTempAllocator.Reset(this.world.id);
            
        }

        /// <summary>
        /// Draws diagnostic geometry for the associated state.
        /// </summary>
        protected virtual void OnDrawGizmos() {
            
            if (this.world.isCreated == true) {
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = this.world.DrawGizmos(this.previousFrameDependsOn);
            }
            
        }

        /// <summary>
        /// Releases base world initializer state at the end of its owning lifecycle.
        /// </summary>
        protected virtual void OnDestroy() {

            this.previousFrameDependsOn.Complete();
            
            for (var i = 0; i < this.modules.list.Length; ++i) {
                var module = this.modules.list[i];
                if (module.IsEnabled() == false) continue;
                module.obj.DoDestroy();
            }
            
            if (this.world.isCreated == true) {
                this.world.Dispose();
            }
            
            WorldInitializers.Remove(this);
            
        }

    }

}