#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Attack {

    public struct AttackComponentGroup {

        public static UnityEngine.Color color = new UnityEngine.Color(0.65f, 0.1f, 0f);

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
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

        public void OnInitialize(in Ent ent) {
            ent.Set(new AttackRuntimeReloadComponent());
            ent.Set(new AttackRuntimeFireComponent());
        }

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackVisualComponent : IConfigComponent {

        public Config bulletConfig;
        public ME.BECS.Views.View muzzleView;

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackTargetsCountComponent : IConfigComponent {

        public uint count;

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackRuntimeReloadComponent : IComponent {

        public tfloat reloadTimer;

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackRuntimeFireComponent : IComponent {

        public tfloat fireTimer;
        public tfloat fireRateTimer;
        public uint fireRateCount;

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct CanFireWhileMovesTag : IConfigComponent {}

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackTargetComponent : IComponent {

        public Ent target;

    }
    
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackTargetsComponent : IComponent {

        public ListAuto<Ent> targets;

    }

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct ReloadedComponent : IComponent {}
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct CanFireComponent : IComponent {}
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct FireUsedComponent : IComponent {}
    
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct OnFireEvent : IComponent {}
    
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct RotateToAttackWhileIdleComponent : IConfigComponent {}

    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct RotateAttackSensorComponent : IConfigComponent {

        public static RotateAttackSensorComponent Default => new RotateAttackSensorComponent() { speedFactor = 1f };
        
        public tfloat speedFactor;

    }
    
    [ComponentGroup(typeof(AttackComponentGroup))]
    public struct AttackFilterComponent : IConfigComponent {

        public ME.BECS.Units.LayerMask layers;

    }
}