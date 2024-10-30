namespace ME.BECS.Network {

    using ME.BECS.Views;
    
    [UnityEngine.DefaultExecutionOrder(-10_000)]
    public class NetworkWorldInitializer : BaseWorldInitializer {

        public FeaturesGraph.SystemsGraph featuresGraph;
        protected NetworkModule networkModule;
        
        protected override void Awake() {
            
            base.Awake();
            
            if (this.featuresGraph == null) {
                Logger.Features.Error("Graph is null");
                return;
            }

            var group = SystemGroup.Create(UpdateType.ANY);
            if (this.featuresGraph != null) group.Add(this.featuresGraph.DoAwake(ref this.world, UpdateType.FIXED_UPDATE));
            this.world.AssignRootSystemGroup(group);

            this.networkModule = this.modules.Get<NetworkModule>();

            WorldStaticCallbacks.RegisterCallback<ViewsModuleData>(this.ViewsLoad);
            WorldStaticCallbacks.RegisterCallback<ViewsModuleData>(this.OnViewsUpdate, 1);

        }
        
        protected override void Start() {

            if (this.world.isCreated == true) {

                this.previousFrameDependsOn = State.SetWorldState(in this.world, WorldState.BeginTick,
                    UpdateType.FIXED_UPDATE, this.previousFrameDependsOn);
                base.Start();

            }

        }

        private unsafe void OnViewsUpdate(ref ViewsModuleData data) {

            if (this.networkModule == null) return;

            data.beginFrameState->timeSinceStart = this.networkModule.GetCurrentTime();
            data.beginFrameState->state = this.networkModule.GetStartFrameState();

        }

        private unsafe void ViewsLoad(ref ViewsModuleData data) {

            if (this.networkModule == null) return;
            
            data.beginFrameState->tickTime = this.networkModule.properties.tickTime;

        }

        public override Unity.Jobs.JobHandle OnStart(Unity.Jobs.JobHandle dependsOn) {
            
            dependsOn = base.OnStart(dependsOn);
            
            if (this.networkModule != null) {
                
                dependsOn.Complete();
                dependsOn = this.networkModule.Connect(dependsOn);
                
            }

            return dependsOn;

        }

        public void FixedUpdate() {
            if (this.networkModule is null) {
                // Use default initializer behaviour if network module not found as FIXED_UPDATE
                this.previousFrameDependsOn = this.DoUpdate(UpdateType.FIXED_UPDATE, this.previousFrameDependsOn);
            }
        }

        public void Update() {
            
            //this.previousFrameDependsOn = this.DoUpdate(UpdateType.UPDATE, this.previousFrameDependsOn);
            
            if (this.networkModule is null) {
                return;
            }
            
            // From here there are some code which overrides default world initializer behaviour
            if (this.world.isCreated == true) {

                var dt = UnityEngine.Time.deltaTime;
                // Update logic - depends on tick time
                var handle = this.networkModule.UpdateInitializer(dt, this, this.previousFrameDependsOn, ref this.world);
                handle.Complete();
                
                if (this.networkModule.IsInRollback() == false) {

                    this.previousFrameDependsOn = this.world.RaiseEvents(this.previousFrameDependsOn);
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

        /*
        protected override void LateUpdate() {

            if (this.world.isCreated == true) {
                this.previousFrameDependsOn.Complete();
                this.previousFrameDependsOn = this.DoUpdate(UpdateType.LATE_UPDATE, this.previousFrameDependsOn);
            }

            base.LateUpdate();

        }*/

    }

}