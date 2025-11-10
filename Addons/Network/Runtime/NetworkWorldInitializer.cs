namespace ME.BECS.Network {

    using ME.BECS.Views;
    
    [UnityEngine.DefaultExecutionOrder(-10_000)]
    public class NetworkWorldInitializer : BaseWorldInitializer<NetworkWorldInitializer.Graph> {

        [System.Serializable]
        public struct Graph : IGraphInitialize {

            [OptionalGraph]
            public FeaturesGraph.SystemsGraph awake;
            [OptionalGraph]
            public FeaturesGraph.SystemsGraph start;
            [OptionalGraph]
            public FeaturesGraph.SystemsGraph update;

            public void Initialize(ref SystemGroup group, ref World world) {
                if (this.awake != null) group.Add(this.awake.DoAwake(ref world, UpdateType.AWAKE));
                if (this.start != null) group.Add(this.start.DoAwake(ref world, UpdateType.START));
                if (this.update != null) {
                    // add update graph as start and awake if no overrides
                    if (this.awake == null) group.Add(this.update.DoAwake(ref world, UpdateType.AWAKE));
                    if (this.start == null) group.Add(this.update.DoAwake(ref world, UpdateType.START));
                    group.Add(this.update.DoAwake(ref world, UpdateType.FIXED_UPDATE));
                }
            }

        }

        [OptionalGraph]
        public FeaturesGraph.SystemsGraph featuresGraphAwake;
        [OptionalGraph]
        public FeaturesGraph.SystemsGraph featuresGraphStart;
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("featuresGraph")] public FeaturesGraph.SystemsGraph featuresGraphUpdate;
        protected NetworkModule networkModule;
        
        protected override void DoWorldAwake() {
            
            var group = SystemGroup.Create(UpdateType.ANY);
            this.graphs.Initialize(ref group, ref this.world);
            if (this.featuresGraphAwake != null) group.Add(this.featuresGraphAwake.DoAwake(ref this.world, UpdateType.AWAKE));
            if (this.featuresGraphStart != null) group.Add(this.featuresGraphStart.DoAwake(ref this.world, UpdateType.START));
            if (this.featuresGraphUpdate != null) {
                // add update graph as start and awake if no overrides
                if (this.featuresGraphAwake == null) group.Add(this.featuresGraphUpdate.DoAwake(ref this.world, UpdateType.AWAKE));
                if (this.featuresGraphStart == null) group.Add(this.featuresGraphUpdate.DoAwake(ref this.world, UpdateType.START));
                group.Add(this.featuresGraphUpdate.DoAwake(ref this.world, UpdateType.FIXED_UPDATE));
            }
            this.world.AssignRootSystemGroup(group);

            this.networkModule = this.modules.Get<NetworkModule>();

            WorldStaticCallbacks.RegisterCallback<ViewsModuleData>(this.ViewsLoad);
            WorldStaticCallbacks.RegisterCallback<ViewsModuleData>(this.OnViewsUpdate, 1);

            this.previousFrameDependsOn = State.SetWorldState(in this.world, WorldState.Initialized, UpdateType.FIXED_UPDATE, 0u, this.previousFrameDependsOn);
            base.DoWorldAwake();
            
        }

        protected override void Start() {

            if (this.world.isCreated == true) {

                this.previousFrameDependsOn = State.SetWorldState(in this.world, WorldState.Initialized, UpdateType.FIXED_UPDATE, 0u, this.previousFrameDependsOn);
                base.Start();

            }

        }

        private unsafe void OnViewsUpdate(ref ViewsModuleData data) {

            if (this.networkModule == null) return;

            if (data.connectedWorld.id != this.world.id) return;

            data.beginFrameState.ptr->timeSinceStart = this.networkModule.GetCurrentTime();
            data.beginFrameState.ptr->state = this.networkModule.GetStartFrameState();

        }

        private unsafe void ViewsLoad(ref ViewsModuleData data) {

            if (this.networkModule == null) return;

            if (data.connectedWorld.id != this.world.id) return;
            
            data.beginFrameState.ptr->tickTime = this.networkModule.properties.tickTime;

        }

        public override Unity.Jobs.JobHandle OnStart(Unity.Jobs.JobHandle dependsOn) {
            
            dependsOn = base.OnStart(dependsOn);
            
            if (this.networkModule != null) {
                
                dependsOn.Complete();
                dependsOn = this.networkModule.Connect(dependsOn);
                
            }

            return dependsOn;

        }

        public virtual void FixedUpdate() {
            if (this.networkModule is null) {
                // Use default initializer behaviour if network module not found as FIXED_UPDATE
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = State.NextTick(this.world.state, this.previousFrameDependsOn);
                this.previousFrameDependsOn = this.DoUpdate(UpdateType.FIXED_UPDATE, this.previousFrameDependsOn);
                this.previousFrameDependsOn.Complete();
            }
        }

        public virtual void Update() {
            
            //this.previousFrameDependsOn = this.DoUpdate(UpdateType.UPDATE, this.previousFrameDependsOn);
            
            if (this.networkModule is null) {
                return;
            }

            // From here there are some code which overrides default world initializer behaviour
            if (this.world.isCreated == true) {
                
                this.previousFrameDependsOn.Complete();
                
                var dt = this.GetDeltaTimeMs();
                // Update logic - depends on tick time
                var handle = this.networkModule.UpdateInitializer(dt, this, this.previousFrameDependsOn, ref this.world);
                handle.Complete();
                
                if (this.networkModule.IsInRollback() == false) {
                    this.RedrawVisual();
                }
                
                this.previousFrameDependsOn.Complete();
                
            }
            
        }

        public void RedrawVisual() {
            // Update visual - once per frame
            this.previousFrameDependsOn = this.OnUpdate(this.previousFrameDependsOn);
            for (var i = 0; i < this.modules.list.Length; ++i) {
                var module = this.modules.list[i];
                if (module.IsEnabled() == false) continue;
                this.previousFrameDependsOn = module.obj.OnUpdate(this.previousFrameDependsOn);
            }
        }

        public void SyncRewind() {
            while (this.world.CurrentTick < this.networkModule.GetTargetTick()) {
                this.Update();
            }
            this.RedrawVisual();
            this.LateUpdate();
        }
        
        /*
        protected override void LateUpdate() {

            if (this.world.isCreated == true) {
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = this.DoUpdate(UpdateType.LATE_UPDATE, this.previousFrameDependsOn);
            }

            base.LateUpdate();

        }*/

        protected override void OnDestroy() {
            
            WorldStaticCallbacks.UnregisterCallback<ViewsModuleData>(this.ViewsLoad);
            WorldStaticCallbacks.UnregisterCallback<ViewsModuleData>(this.OnViewsUpdate, 1);
            
            base.OnDestroy();
            
        }
    }

}