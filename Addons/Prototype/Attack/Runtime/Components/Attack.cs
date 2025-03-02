#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Attack {

    public struct AttackComponent : IConfigComponent, IConfigInitialize {

        public static AttackComponent Default => new AttackComponent() {
            sector = Sector.Default,
        };

        public Sector sector;
        public byte ignoreSelf;
        public tfloat reloadTime;
        public tfloat fireTime;
        public tfloat attackTime;
        public tfloat rateTime;
        public uint rateCount;
        public Config bulletConfig;
        public ME.BECS.Views.View muzzleView;

        public void OnInitialize(in Ent ent) {
            ent.Set(new AttackRuntimeReloadComponent());
            ent.Set(new AttackRuntimeFireComponent());
        }

    }

    public struct AttackRuntimeReloadComponent : IComponent {

        public tfloat reloadTimer;

    }

    public struct AttackRuntimeFireComponent : IComponent {

        public tfloat fireTimer;
        public tfloat fireRateTimer;
        public uint fireRateCount;

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
        
        public tfloat speedFactor;

    }
    
    public struct AttackFilterComponent : IConfigComponent {

        public ME.BECS.Units.LayerMask layers;

    }
}