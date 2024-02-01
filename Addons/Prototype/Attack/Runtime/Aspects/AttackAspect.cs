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
                this.ent.Set(new AttackTargetComponent() {
                    target = ent,
                });
            } else {
                this.ent.Remove<AttackTargetComponent>();
            }
        }
        
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

    }

    public struct AttackTargetComponent : IComponent {

        public Ent target;

    }
    
    public struct ReloadedComponent : IComponent {}

}