using UnityEngine;

namespace ME.BECS {
    
    [DefaultExecutionOrder(-10_000)]
    public class WorldInitializer : BaseWorldInitializer {

        [OptionalGraph]
        public FeaturesGraph.SystemsGraph featuresGraphAwake;
        [OptionalGraph]
        public FeaturesGraph.SystemsGraph featuresGraphStart;
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("featuresGraph")] public FeaturesGraph.SystemsGraph featuresGraphUpdate;
        public FeaturesGraph.SystemsGraph featuresGraphFixedUpdate;
        public FeaturesGraph.SystemsGraph featuresGraphLateUpdate;

        protected override void DoWorldAwake() {
            
            if (this.featuresGraphUpdate == null && this.featuresGraphFixedUpdate == null && this.featuresGraphLateUpdate == null) {
                Logger.Features.Error("Graphs are null");
                return;
            }

            var group = SystemGroup.Create(UpdateType.ANY);
            if (this.featuresGraphAwake != null) group.Add(this.featuresGraphAwake.DoAwake(ref this.world, UpdateType.AWAKE));
            if (this.featuresGraphStart != null) group.Add(this.featuresGraphStart.DoAwake(ref this.world, UpdateType.START));
            if (this.featuresGraphUpdate != null) {
                // add update graph as start and awake if no overrides
                if (this.featuresGraphAwake == null) group.Add(this.featuresGraphUpdate.DoAwake(ref this.world, UpdateType.AWAKE));
                if (this.featuresGraphStart == null) group.Add(this.featuresGraphUpdate.DoAwake(ref this.world, UpdateType.START));
                group.Add(this.featuresGraphUpdate.DoAwake(ref this.world, UpdateType.UPDATE));
            }

            if (this.featuresGraphFixedUpdate != null) {
                // add update graph as start and awake if no overrides
                if (this.featuresGraphAwake == null) group.Add(this.featuresGraphFixedUpdate.DoAwake(ref this.world, UpdateType.AWAKE));
                if (this.featuresGraphStart == null) group.Add(this.featuresGraphFixedUpdate.DoAwake(ref this.world, UpdateType.START));
                group.Add(this.featuresGraphFixedUpdate.DoAwake(ref this.world, UpdateType.FIXED_UPDATE));
            }

            if (this.featuresGraphLateUpdate != null) {
                // add update graph as start and awake if no overrides
                if (this.featuresGraphAwake == null) group.Add(this.featuresGraphLateUpdate.DoAwake(ref this.world, UpdateType.AWAKE));
                if (this.featuresGraphStart == null) group.Add(this.featuresGraphLateUpdate.DoAwake(ref this.world, UpdateType.START));
                group.Add(this.featuresGraphLateUpdate.DoAwake(ref this.world, UpdateType.LATE_UPDATE));
            }
            this.world.AssignRootSystemGroup(group);

            base.DoWorldAwake();
            
        }

        public void Update() {

            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = State.NextTick(this.world.state, this.previousFrameDependsOn);
            
            if (this.featuresGraphUpdate == null) return;
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.UPDATE, this.previousFrameDependsOn);
            this.previousFrameDependsOn.Complete();

        }

        public void FixedUpdate() {

            if (this.featuresGraphFixedUpdate == null) return;
            this.previousFrameDependsOn.Complete();
            this.previousFrameDependsOn = this.DoUpdate(UpdateType.FIXED_UPDATE, this.previousFrameDependsOn);
            this.previousFrameDependsOn.Complete();
            
        }

        protected override void LateUpdate() {

            if (this.featuresGraphLateUpdate != null) {
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = this.DoUpdate(UpdateType.LATE_UPDATE, this.previousFrameDependsOn);
            }

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