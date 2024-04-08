using UnityEngine;

namespace ME.BECS {
    
    using Unity.Jobs;

    [DefaultExecutionOrder(-10_000)]
    public class WorldInitializer : BaseWorldInitializer {

        public FeaturesGraph.SystemsGraph featuresGraph;
        public FeaturesGraph.SystemsGraph featuresGraphFixedUpdate;
        public FeaturesGraph.SystemsGraph featuresGraphLateUpdate;

        protected override void Awake() {
            
            base.Awake();
            
            if (this.featuresGraph == null && this.featuresGraphFixedUpdate == null) {
                Logger.Features.Error("Graphs are null");
                return;
            }

            var group = SystemGroup.Create(UpdateType.ANY);
            if (this.featuresGraph != null) group.Add(this.featuresGraph.DoAwake(ref this.world, UpdateType.UPDATE));
            if (this.featuresGraphFixedUpdate != null) group.Add(this.featuresGraphFixedUpdate.DoAwake(ref this.world, UpdateType.FIXED_UPDATE));
            if (this.featuresGraphLateUpdate != null) group.Add(this.featuresGraphLateUpdate.DoAwake(ref this.world, UpdateType.LATE_UPDATE));
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

        protected override void LateUpdate() {

            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.LATE_UPDATE, this.previousFrameDependsOn);

            base.LateUpdate();

        }

    }

}