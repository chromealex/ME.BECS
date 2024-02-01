using ME.BECS.Transforms;

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public static partial class UnitUtils {

        [INLINE(256)]
        public static UnitCommandGroupAspect CreateCommandGroup(uint targetsCapacity, uint capacity = 10u) {

            var ent = Ent.New();
            var aspect = ent.GetOrCreateAspect<UnitCommandGroupAspect>();
            aspect.units = new ListAuto<Ent>(in ent, capacity);
            aspect.targets = new MemArrayAuto<Ent>(in ent, targetsCapacity);
            return aspect;

        }

        [INLINE(256)]
        public static UnitCommandGroupAspect CreateCommandGroup(uint targetsCapacity, in UnitSelectionGroupAspect selectionGroup) {

            var ent = Ent.New();
            var aspect = ent.GetOrCreateAspect<UnitCommandGroupAspect>();
            aspect.units = new ListAuto<Ent>(in ent, selectionGroup.units.Count);
            aspect.targets = new MemArrayAuto<Ent>(in ent, targetsCapacity);
            {
                for (uint i = 0; i < selectionGroup.units.Count; ++i) {
                    var unit = selectionGroup.units[i];
                    aspect.Add(unit.GetAspect<UnitAspect>());
                }
            }
            return aspect;

        }

        [INLINE(256)]
        public static void DestroyCommandGroup(in Ent commandGroup) {
            
            if (commandGroup.IsAlive() == true) commandGroup.Destroy();
            
        }

        [INLINE(256)]
        public static void DestroyCommandGroup(in UnitCommandGroupAspect commandGroup) {
            
            if (commandGroup.ent.IsAlive() == true) commandGroup.ent.Destroy();
            
        }

        [INLINE(256)]
        public static uint AddToCommandGroup(in UnitCommandGroupAspect commandGroup, in UnitAspect unit) {

            E.THREAD_CHECK("AddToCommandGroup");

            RemoveFromCommandGroup(in unit);
            unit.unitCommandGroup = commandGroup.ent;
            commandGroup.volume += UnitUtils.GetVolume(in unit);
            return commandGroup.units.Add(unit.ent) + 1u;

        }

        [INLINE(256)]
        public static bool WillRemoveCommandGroup(in UnitAspect unit) {
            if (unit.unitCommandGroup.IsAlive() == true) {
                var aspect = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
                return aspect.units.Count == 1u;
            }
            return false;
        }

        [INLINE(256)]
        public static bool RemoveFromCommandGroup(in UnitAspect unit) {

            E.THREAD_CHECK("RemoveFromCommandGroup");

            if (unit.unitCommandGroup.IsAlive() == true) {
                var aspect = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
                aspect.units.Remove(unit.ent);
                if (aspect.units.Count == 0u) {
                    // destroy group
                    aspect.ent.DestroyHierarchy();
                    return true;
                } else {
                    JobUtils.Decrement(ref aspect.volume, UnitUtils.GetVolume(in unit));
                }
                unit.unitCommandGroup = default;
            }

            return false;

        }

    }

}