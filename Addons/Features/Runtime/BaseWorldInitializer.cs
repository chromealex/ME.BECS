using UnityEngine;

namespace ME.BECS {
    
    using Unity.Jobs;

    public abstract class BaseWorldInitializer : MonoBehaviour {

        [System.Serializable]
        public struct Modules {

            public OptionalModule[] list;

            public bool Has<T>() where T : Module {
                for (int i = 0; i < this.list.Length; ++i) {
                    if (this.list[i].IsEnabled() == true && this.list[i].obj is T) return true;
                }
                return false;
            }

            public T Get<T>() where T : Module {
                for (int i = 0; i < this.list.Length; ++i) {
                    if (this.list[i].IsEnabled() == true && this.list[i].obj is T module) return module;
                }
                return null;
            }

        }

        public WorldProperties properties = WorldProperties.Default;
        public Modules modules = new Modules() {
            list = System.Array.Empty<OptionalModule>(),
        };
        public World world;
        protected JobHandle previousFrameDependsOn;
        private static BaseWorldInitializer instance;
        
        public static BaseWorldInitializer GetInstance() => instance;

        public T GetModule<T>() where T : Module {
            
            for (var i = 0; i < this.modules.list.Length; ++i) {
                var module = this.modules.list[i];
                if (module.IsEnabled() == false) continue;
                if (module.obj is T moduleInstance) return moduleInstance;
            }

            return null;

        }
        
        protected virtual void Awake() {

            instance = this;

            this.world = World.Create(this.properties);
            
        }

        protected virtual void Start() {

            if (this.world.isCreated == true) {
                
                for (var i = 0; i < this.modules.list.Length; ++i) {
                    var module = this.modules.list[i];
                    if (module.IsEnabled() == false) continue;
                    module.obj.worldProperties = this.properties;
                    module.obj.OnAwake(ref this.world);
                }

                this.OnAwake();

                this.previousFrameDependsOn = this.world.Awake(this.previousFrameDependsOn);
                
                for (var i = 0; i < this.modules.list.Length; ++i) {
                    var module = this.modules.list[i];
                    if (module.IsEnabled() == false) continue;
                    module.obj.worldProperties = this.properties;
                    this.previousFrameDependsOn = module.obj.OnStart(ref this.world, this.previousFrameDependsOn);
                }
                
                this.previousFrameDependsOn = this.OnStart(this.previousFrameDependsOn);

            }

        }

        public virtual void OnAwake() {
            
        }

        public virtual JobHandle OnStart(JobHandle dependsOn) {
            return dependsOn;
        }

        protected JobHandle DoUpdate(ushort updateType, JobHandle dependsOn) {

            this.previousFrameDependsOn = dependsOn;
            if (this.world.isCreated == true) {
                this.previousFrameDependsOn = this.world.Tick(Time.deltaTime, updateType, this.previousFrameDependsOn);
                if (updateType == UpdateType.UPDATE) {
                    
                    // Update modules
                    this.previousFrameDependsOn = this.OnUpdate(this.previousFrameDependsOn);

                    for (var i = 0; i < this.modules.list.Length; ++i) {
                        var module = this.modules.list[i];
                        if (module.IsEnabled() == false) continue;
                        this.previousFrameDependsOn = module.obj.OnUpdate(this.previousFrameDependsOn);
                    }
                    
                }
            }

            return this.previousFrameDependsOn;

        }

        public virtual JobHandle OnUpdate(JobHandle dependsOn) {
            if (this.world.isCreated == true) {
                ProfilerCounters.Initialize();
                ProfilerCounters.SampleWorldBeginFrame(in this.world);
            }
            return dependsOn;
        }

        protected virtual void LateUpdate() {

            this.previousFrameDependsOn.Complete();

            if (this.world.isCreated == true) {
                ProfilerCounters.SampleWorldEndFrame(in this.world);
            }

            WorldsTempAllocator.Reset();
            
        }

        protected virtual void OnDrawGizmos() {
            
            if (this.world.isCreated == true) {
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = this.world.DrawGizmos(this.previousFrameDependsOn);
            }
            
        }

        protected virtual void OnDestroy() {

            for (var i = 0; i < this.modules.list.Length; ++i) {
                var module = this.modules.list[i];
                if (module.IsEnabled() == false) continue;
                module.obj.OnDestroy();
            }
            
            if (this.world.isCreated == true) {
                this.world.Dispose();
            }
            
        }

    }

}