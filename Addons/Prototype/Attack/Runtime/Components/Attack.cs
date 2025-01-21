namespace ME.BECS.Attack {

    public struct AttackComponent : IConfigComponent, IConfigInitialize {

        public static AttackComponent Default => new AttackComponent() {
            sector = Sector.Default,
        };

        public Sector sector;
        public byte ignoreSelf;
        public float reloadTime;
        public float fireTime;
        public float attackTime;
        public Config bulletConfig;
        public ME.BECS.Views.View muzzleView;

        public void OnInitialize(in Ent ent) {
            ent.Set(new AttackRuntimeReloadComponent());
            ent.Set(new AttackRuntimeFireComponent());
        }

    }

    public struct AttackRuntimeReloadComponent : IComponent {

        public float reloadTimer;

    }

    public struct AttackRuntimeFireComponent : IComponent {

        public float fireTimer;

    }

    public struct CanFireWhileMovesTag : IConfigComponent {}

    public struct AttackTargetComponent : IComponent {

        public Ent target;

    }
    
    public struct ReloadedComponent : IComponent {}
    public struct CanFireComponent : IComponent {}
    public struct FireUsedComponent : IComponent {}
    
    public struct OnFireEvent : IComponent {}
    
    public struct RotateToAttackWhileIdleComponent : IConfigComponent {}

    public struct RotateAttackSensorComponent : IConfigComponent {

        public static RotateAttackSensorComponent Default => new RotateAttackSensorComponent() { speedFactor = 1f };
        
        public float speedFactor;

    }
    
    public struct AttackFilterComponent : IConfigComponent {

        public ME.BECS.Units.LayerMask layers;

    }
}