using ME.BECS.Transforms;

namespace ME.BECS.Perks {

    using ME.BECS;
    using ME.BECS.Players;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public struct PerksComponent : IComponent {

        public ListAuto<Ent> slots;
        
    }

    public struct IsPerkActiveComponent : IComponent {}
    public struct IsPerkPassiveComponent : IComponent {}
    
    public interface IPerkInitializeComponent : IConfigComponent {

        [INLINE(256)]
        void OnInitialize(in JobInfo jobInfo, in PlayerAspect owner, in PerkAspect perk);

    }
    
    public interface IPerkComponent : IConfigComponent {

        [INLINE(256)]
        void Run(in PerkAspect perk, in Ent target, uint dt);

    }

    public interface IPerkParallelComponent : IPerkComponent {

    }
    
    public struct IsPerkInitializeRequired : IComponent { }

    public struct PerkComponent : IComponent {

        public Ent slot;

    }

    public struct IsPerkUsedComponent : IComponent { }

    public struct ActivatePassivePerksComponent : IConfigComponentStatic, IConfigInitialize {

        public MemArrayAuto<Config> perks;

        public void OnInitialize(in Ent ent) {

            var owner = ent.Read<OwnerComponent>().ent;
            if (owner.IsAlive() == true) {
                for (uint i = 0u; i < this.perks.Length; ++i) {
                    var config = this.perks[i];
                    var perk = PerksUtils.AddPassivePerk(JobInfo.Create(ent.worldId), owner.GetAspect<PlayerAspect>(), config);
                    perk.SetParent(ent);
                }
            }

        }

    }

    public struct DefaultParallelPerkComponent : IPerkParallelComponent {
        
        public void Run(in PerkAspect perk, in Ent target, uint dt) {
            throw new System.NotImplementedException();
        }

    }

    public struct DefaultPerkComponent : IPerkComponent {
        
        public void Run(in PerkAspect perk, in Ent target, uint dt) {
            throw new System.NotImplementedException();
        }

    }

    public struct DefaultPerkInitializerComponent : IPerkInitializeComponent {
        
        public void OnInitialize(in JobInfo jobInfo, in PlayerAspect owner, in PerkAspect perk) {
            throw new System.NotImplementedException();
        }

    }

}