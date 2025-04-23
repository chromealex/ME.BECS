namespace ME.BECS.Bullets {

    public struct BulletAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<BulletConfigComponent> bulletConfigDataPtr;
        [QueryWith]
        public AspectDataPtr<BulletRuntimeComponent> bulletRuntimeDataPtr;
        
        public readonly ref BulletConfigComponent config => ref this.bulletConfigDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly BulletConfigComponent readConfig => ref this.bulletConfigDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref BulletRuntimeComponent component => ref this.bulletRuntimeDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly BulletRuntimeComponent readComponent => ref this.bulletRuntimeDataPtr.Read(this.ent.id, this.ent.gen);
        public uint damage {
            get => this.ent.Has<DamageOverrideComponent>() ? this.ent.Read<DamageOverrideComponent>().damage : this.readConfig.damage;
            set => this.ent.Get<DamageOverrideComponent>().damage = value;
        }

        public bool IsReached {
            get => this.ent.Has<TargetReachedComponent>();
            set => this.ent.Set(new TargetReachedComponent());
        }

    }

}