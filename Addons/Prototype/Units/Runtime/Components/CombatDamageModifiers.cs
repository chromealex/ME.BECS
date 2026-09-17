using ME.BECS;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
#endif
[assembly: CodeGeneratorInclude(typeof(ME.BECS.Units.CombatDamageModifiers))]
[assembly: CodeGeneratorInclude(typeof(ME.BECS.Units.PlayerDamageModifiers))]
namespace ME.BECS.Units {
    // Optional gameplay-provided modifiers; absence preserves the original damage path.
    public struct PlayerDamageModifiers : IComponent {
        public tfloat fuelAllBonus, fuelExplosionBonus, pvpMultiplier, matchSeconds;
        public uint level;
        public ulong heroShots, super, imprints, sniper, tesla, tank, flame, miner, rocket;
        public ListAuto<Kill> kills;
        public struct Kill { public tfloat ttk; public uint killerLevel, victimLevel; }
    }
    public struct CombatDamageModifiers : IComponent {
        public bool burning, playerUnit, pvpStarted;
        public tfloat pvpStartTime;
        public uint level;
        public byte damageCategory;
        public tfloat shield, shieldRemaining;
        public ListAuto<Mark> marks;
        public struct Mark { public Ent owner; public tfloat bonus, remaining; }
    }
}
