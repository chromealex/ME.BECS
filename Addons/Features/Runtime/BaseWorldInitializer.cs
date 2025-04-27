using UnityEngine;

namespace ME.BECS {
    
    using Unity.Jobs;
    using Unity.Collections;
    using scg = System.Collections.Generic;

    public static class WorldInitializers {

        private static scg::List<BaseWorldInitializer> list = new scg::List<BaseWorldInitializer>();

        public static BaseWorldInitializer GetByWorldName(FixedString64Bytes worldName) {
            foreach (var item in list) {
                if (item.properties.name == worldName) {
                    return item;
                }
            }
            return null;
        }
        
        public static void Add(BaseWorldInitializer initializer) {
            list.Add(initializer);
        }

        public static void Remove(BaseWorldInitializer initializer) {
            list.Remove(initializer);
        }

    }
    
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

            public void Load() {

                for (int i = 0; i < this.list.Length; ++i) {
                    this.list[i].obj = Object.Instantiate(this.list[i].obj);
                }
                
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

        protected virtual World CreateWorld() => World.Create(this.properties);
        
        protected virtual void Awake() {

            instance = this;
            WorldInitializers.Add(this);

            this.modules.Load();
            this.world = this.CreateWorld();
            this.DoWorldAwake();

        }

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

            this.previousFrameDependsOn = this.world.Awake(this.previousFrameDependsOn);
            
        }

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

        protected virtual void DoWorldStart() {
            
            this.previousFrameDependsOn = this.world.Start(this.previousFrameDependsOn);
            
        }

        public virtual void OnAwake() {
            
        }

        public virtual JobHandle OnStart(JobHandle dependsOn) {
            return dependsOn;
        }

        public virtual uint GetDeltaTimeMs() {
            return (uint)(Time.deltaTime * 1000u);
        }

        protected JobHandle DoUpdate(ushort updateType, JobHandle dependsOn) {

            this.previousFrameDependsOn = dependsOn;
            if (this.world.isCreated == true) {
                this.previousFrameDependsOn = this.world.Tick(this.GetDeltaTimeMs(), updateType, this.previousFrameDependsOn);
            }

            return this.previousFrameDependsOn;

        }

        public virtual JobHandle OnUpdate(JobHandle dependsOn) {
            if (this.world.isCreated == true) {
                ProfilerCounters.Initialize();
                ProfilerCounters.SampleWorldBeginFrame(in this.world);
                dependsOn = this.world.RaiseEvents(dependsOn);
            }
            return dependsOn;
        }

        protected virtual void LateUpdate() {

            this.previousFrameDependsOn.Complete();

            if (this.world.isCreated == true) {
                ProfilerCounters.SampleWorldEndFrame(in this.world);
            }

            this.previousFrameDependsOn.Complete();
            WorldsTempAllocator.Reset(this.world.id);
            
        }

        protected virtual void OnDrawGizmos() {
            
            if (this.world.isCreated == true) {
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = this.world.DrawGizmos(this.previousFrameDependsOn);
            }
            
        }

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