using UnityEngine;

namespace ME.BECS {
    
    using Unity.Jobs;

    public class WorldInitializer : MonoBehaviour {

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
        public FeaturesGraph.SystemsGraph featuresGraph;
        protected World world;
        protected JobHandle previousFrameDependsOn;

        protected virtual void Awake() {

            if (this.featuresGraph == null) {
                Debug.LogError("Graph is null");
                return;
            }
            
            this.world = World.Create(this.properties);
            this.featuresGraph.DoAwake(ref this.world);

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

        protected virtual void Update() {

            if (this.world.isCreated == true) {
                this.previousFrameDependsOn.Complete();
                var dependsOn = this.world.Tick(Time.deltaTime);
                this.previousFrameDependsOn = this.OnUpdate(dependsOn);
                
                for (var i = 0; i < this.modules.list.Length; ++i) {
                    var module = this.modules.list[i];
                    if (module.IsEnabled() == false) continue;
                    this.previousFrameDependsOn = module.obj.OnUpdate(this.previousFrameDependsOn);
                }
            }
            
        }

        public virtual JobHandle OnUpdate(JobHandle dependsOn) {
            return dependsOn;
        }

        protected virtual void LateUpdate() {

            if (this.world.isCreated == true) {
                ProfilerCounters.SampleWorld(in this.world);
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