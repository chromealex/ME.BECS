namespace ME.BECS.Attack {

    public struct AttackComponent : IConfigComponent, IConfigInitialize {

        public float attackRangeSqr;
        public float reloadTime;
        public float fireTime;
        public float attackTime;
        public Config bulletConfig;
        public ME.BECS.Views.View muzzleView;

        public void OnInitialize(in Ent ent) {
            ent.Set(new AttackRuntimeComponent());
        }

    }

    public struct AttackRuntimeComponent : IComponent {

        public float reloadTimer;
        public float fireTimer;

    }

    public struct CanFireWhileMovesTag : IConfigComponent {}

    public struct AttackTargetComponent : IComponent {

        public Ent target;

    }
    
    public struct ReloadedComponent : IComponent {}
    public struct CanFireComponent : IComponent {}
    public struct FireUsedComponent : IComponent {}
    
    public struct RotateToAttackWhileIdleComponent : IConfigComponent {}

}