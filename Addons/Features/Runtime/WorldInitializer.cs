using UnityEngine;

namespace ME.BECS {
    
    using Unity.Jobs;

    [DefaultExecutionOrder(-10_000)]
    public class WorldInitializer : BaseWorldInitializer {

        public FeaturesGraph.SystemsGraph featuresGraph;
        public FeaturesGraph.SystemsGraph featuresGraphFixedUpdate;

        protected override void Awake() {
            
            base.Awake();
            
            if (this.featuresGraph == null && this.featuresGraphFixedUpdate == null) {
                Logger.Features.Error("Graphs are null");
                return;
            }

            var group = SystemGroup.Create(UpdateType.ANY);
            if (this.featuresGraph != null) group.Add(this.featuresGraph.DoAwake(ref this.world, UpdateType.UPDATE));
            if (this.featuresGraphFixedUpdate != null) group.Add(this.featuresGraphFixedUpdate.DoAwake(ref this.world, UpdateType.FIXED_UPDATE));
            this.world.AssignRootSystemGroup(group);

        }

        public void Update() {

            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.UPDATE, this.previousFrameDependsOn);
            
        }

        public void FixedUpdate() {
            
            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.FIXED_UPDATE, this.previousFrameDependsOn);
            
        }

    }

}