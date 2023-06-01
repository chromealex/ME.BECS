namespace ME.BECS {

    using UnityEngine;
    
    public class NetworkWorldInitializer : WorldInitializer {

        protected NetworkModule networkModule;
        
        protected override void Awake() {
            
            base.Awake();

            this.networkModule = this.modules.Get<NetworkModule>();

        }
        
        public override Unity.Jobs.JobHandle OnStart(Unity.Jobs.JobHandle dependsOn) {
            
            dependsOn = base.OnStart(dependsOn);
            
            if (this.networkModule != null) {
                
                dependsOn.Complete();
                dependsOn = this.networkModule.Connect(dependsOn);
                
            }

            return dependsOn;

        }

        protected override void Update() {

            if (this.networkModule is null) {
                // Use default initializer behaviour if network module not found
                base.Update();
                return;
            }
            
            // From here there are some code which overrides default world initializer behaviour
            if (this.world.isCreated == true) {
                
                // Update logic - depends on tick time
                var handle = this.networkModule.UpdateInitializer(this, this.previousFrameDependsOn, ref this.world);
                handle.Complete();
                if (this.networkModule.IsInRollback() == false) {

                    // Update visual - once per frame
                    this.previousFrameDependsOn = this.OnUpdate(this.previousFrameDependsOn);
                    for (var i = 0; i < this.modules.list.Length; ++i) {
                        var module = this.modules.list[i];
                        if (module.IsEnabled() == false) continue;
                        this.previousFrameDependsOn = module.obj.OnUpdate(this.previousFrameDependsOn);
                    }

                }

            }
            
        }

    }

}