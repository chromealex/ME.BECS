namespace ME.BECS.Attack {

    public struct AttackAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<AttackComponent> attackDataPtr;
        public AspectDataPtr<AttackTargetComponent> targetDataPtr;

        public ref AttackComponent component => ref this.attackDataPtr.Get(this.ent.id, this.ent.gen);
        public ref float attackRangeSqr => ref this.component.attackRangeSqr;
        
        public Ent target => this.targetDataPtr.Read(this.ent.id, this.ent.gen).target;

        public void SetTarget(Ent ent) {
            if (ent.IsAlive() == true) {
                if (this.ent.Has<CanFireWhileMovesTag>() == true && this.ent.Read<AttackTargetComponent>().target != ent) {
                    this.CanFire = false;
                }
                
                this.ent.Set(new AttackTargetComponent() {
                    target = ent,
                });
            } else {
                this.ent.Remove<AttackTargetComponent>();
            }
        }
        
        public float ReloadProgress => this.component.reloadTimer / this.component.reloadTime;
        public float FireProgress => this.component.fireTimer / this.component.fireTime;

        public bool IsReloaded {
            get => this.ent.Has<ReloadedComponent>();
            set {
                if (value == true) {
                    this.ent.Set(new ReloadedComponent());
                } else {
                    this.component.reloadTimer = 0f;
                    this.ent.Remove<ReloadedComponent>();
                }
            }
        }
        
        public bool CanFire {
            get => this.ent.Has<CanFireComponent>();
            set {
                if (value == true) {
                    this.ent.Set(new CanFireComponent());
                } else {
                    this.component.fireTimer = 0f;
                    this.ent.Remove<CanFireComponent>();
                }
            }
        }

    }

    public struct AttackTargetComponent : IComponent {

        public Ent target;

    }
    
    public struct ReloadedComponent : IComponent {}
    public struct CanFireComponent : IComponent {}

}