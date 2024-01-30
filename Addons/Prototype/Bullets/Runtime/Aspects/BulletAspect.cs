namespace ME.BECS.Bullets {

    public struct BulletAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<BulletConfigComponent> bulletConfigDataPtr;
        [QueryWith]
        public AspectDataPtr<BulletRuntimeComponent> bulletRuntimeDataPtr;
        
        public ref BulletConfigComponent config => ref this.bulletConfigDataPtr.Get(this.ent.id, this.ent.gen);
        public ref BulletRuntimeComponent component => ref this.bulletRuntimeDataPtr.Get(this.ent.id, this.ent.gen);

        public bool IsReached {
            get => this.ent.Has<TargetReachedComponent>();
            set => this.ent.Set(new TargetReachedComponent());
        }

    }

}