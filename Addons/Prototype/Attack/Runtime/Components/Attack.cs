namespace ME.BECS.Attack {

    [System.Serializable]
    public struct Sector {

        public static Sector Default => new Sector() {
            rangeSqr = 0f,
            sector = 360f,
        };

        public float rangeSqr;
        public float minRangeSqr;
        [UnityEngine.RangeAttribute(0f, 360f)]
        public float sector;
        
    }
    
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
    
    public struct OnFireEvent : IComponent {}
    
    public struct RotateToAttackWhileIdleComponent : IConfigComponent {}

}