using UnityEngine;

namespace ME.BECS {
    
    [DefaultExecutionOrder(-10_000)]
    public class WorldInitializer : BaseWorldInitializer {

        public FeaturesGraph.SystemsGraph featuresGraph;
        public FeaturesGraph.SystemsGraph featuresGraphFixedUpdate;
        public FeaturesGraph.SystemsGraph featuresGraphLateUpdate;

        protected override void DoWorldAwake() {
            
            if (this.featuresGraph == null && this.featuresGraphFixedUpdate == null) {
                Logger.Features.Error("Graphs are null");
                return;
            }

            var group = SystemGroup.Create(UpdateType.ANY);
            if (this.featuresGraph != null) group.Add(this.featuresGraph.DoAwake(ref this.world, UpdateType.UPDATE));
            if (this.featuresGraphFixedUpdate != null) group.Add(this.featuresGraphFixedUpdate.DoAwake(ref this.world, UpdateType.FIXED_UPDATE));
            if (this.featuresGraphLateUpdate != null) group.Add(this.featuresGraphLateUpdate.DoAwake(ref this.world, UpdateType.LATE_UPDATE));
            this.world.AssignRootSystemGroup(group);

            base.DoWorldAwake();
            
        }

        public void Update() {

            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.UPDATE, this.previousFrameDependsOn);
            this.previousFrameDependsOn.Complete();

        }

        public void FixedUpdate() {

            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.FIXED_UPDATE, this.previousFrameDependsOn);
            this.previousFrameDependsOn.Complete();
            
        }

        protected override void LateUpdate() {

            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.LATE_UPDATE, this.previousFrameDependsOn);

            if (this.world.isCreated == true) {
                // Update modules
                this.previousFrameDependsOn = this.OnUpdate(this.previousFrameDependsOn);

                for (var i = 0; i < this.modules.list.Length; ++i) {
                    var module = this.modules.list[i];
                    if (module.IsEnabled() == false) continue;
                    this.previousFrameDependsOn = module.obj.OnUpdate(this.previousFrameDependsOn);
                }
            }
            
            base.LateUpdate();

        }

    }

}