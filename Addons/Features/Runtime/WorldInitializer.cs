using UnityEngine;

namespace ME.BECS {
    
    [DefaultExecutionOrder(-10_000)]
    public class WorldInitializer : BaseWorldInitializer<WorldInitializer.Graph> {

        [System.Serializable]
        public struct Graph : IGraphInitialize {

            [OptionalGraph]
            public FeaturesGraph.SystemsGraph awake;
            [OptionalGraph]
            public FeaturesGraph.SystemsGraph start;
            [OptionalGraph]
            public FeaturesGraph.SystemsGraph update;
            [OptionalGraph]
            public FeaturesGraph.SystemsGraph fixedUpdate;
            [OptionalGraph]
            public FeaturesGraph.SystemsGraph lateUpdate;

            public void Initialize(ref SystemGroup group, ref World world) {
                if (this.awake != null) group.Add(this.awake.DoAwake(ref world, UpdateType.AWAKE));
                if (this.start != null) group.Add(this.start.DoAwake(ref world, UpdateType.START));
                if (this.update != null) {
                    // add update graph as start and awake if no overrides
                    if (this.awake == null) group.Add(this.update.DoAwake(ref world, UpdateType.AWAKE));
                    if (this.start == null) group.Add(this.update.DoAwake(ref world, UpdateType.START));
                    group.Add(this.update.DoAwake(ref world, UpdateType.UPDATE));
                }

                if (this.fixedUpdate != null) {
                    // add update graph as start and awake if no overrides
                    if (this.awake == null) group.Add(this.fixedUpdate.DoAwake(ref world, UpdateType.AWAKE));
                    if (this.start == null) group.Add(this.fixedUpdate.DoAwake(ref world, UpdateType.START));
                    group.Add(this.fixedUpdate.DoAwake(ref world, UpdateType.FIXED_UPDATE));
                }

                if (this.lateUpdate != null) {
                    // add update graph as start and awake if no overrides
                    if (this.awake == null) group.Add(this.lateUpdate.DoAwake(ref world, UpdateType.AWAKE));
                    if (this.start == null) group.Add(this.lateUpdate.DoAwake(ref world, UpdateType.START));
                    group.Add(this.lateUpdate.DoAwake(ref world, UpdateType.LATE_UPDATE));
                }
            }

        }

        [OptionalGraph]
        public FeaturesGraph.SystemsGraph featuresGraphAwake;
        [OptionalGraph]
        public FeaturesGraph.SystemsGraph featuresGraphStart;
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("featuresGraph")] public FeaturesGraph.SystemsGraph featuresGraphUpdate;
        public FeaturesGraph.SystemsGraph featuresGraphFixedUpdate;
        public FeaturesGraph.SystemsGraph featuresGraphLateUpdate;

        protected override void DoWorldAwake() {
            
            var group = SystemGroup.Create(UpdateType.ANY);
            this.graphs.Initialize(ref group, ref this.world);
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