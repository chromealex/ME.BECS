using ME.BECS.Transforms;

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

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
        public static void DestroyCommandGroup(in UnitCommandGroupAspect commandGroup) {

            var cmd = commandGroup.prevChainTarget;
            while (cmd.IsAlive() == true) {
                var next = cmd.GetAspect<UnitCommandGroupAspect>().prevChainTarget;
                cmd.DestroyHierarchy();
                cmd = next;
            }
            
            if (commandGroup.ent.IsAlive() == true) commandGroup.ent.DestroyHierarchy();
            
        }

        [INLINE(256)]
        public static uint AddToCommandGroup(in UnitCommandGroupAspect commandGroup, in UnitAspect unit) {

            //E.THREAD_CHECK("AddToCommandGroup");

            RemoveFromCommandGroup(in unit);
            commandGroup.ent.SetTag<IsCommandGroupDirty>(true);
            commandGroup.Lock();
            unit.unitCommandGroup = commandGroup.ent;
            commandGroup.volume += UnitUtils.GetVolume(in unit);
            var idx = commandGroup.units.Add(unit.ent) + 1u;
            commandGroup.Unlock();
            return idx;

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

            //E.THREAD_CHECK("RemoveFromCommandGroup");

            if (unit.unitCommandGroup.IsAlive() == true) {
                var aspect = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
                aspect.Lock();
                aspect.units.Remove(unit.ent);
                aspect.Unlock();
                JobUtils.Decrement(ref aspect.volume, UnitUtils.GetVolume(in unit));
                if (aspect.units.Count == 0u) {
                    // destroy group if it is not a chain group
                    // and parent groups count is zero
                    if (aspect.IsPartOfChain == false &&
                        aspect.IsEmpty == true) {
                        UnitUtils.DestroyCommandGroup(in aspect);
                    }

                    return true;
                }
                unit.unitCommandGroup = default;
            }

            return false;

        }

        [INLINE(256)]
        public static bool SetNextTargetIfAvailable(in UnitAspect unit) {

            var commandGroup = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
            if (commandGroup.nextChainTarget.IsAlive() == true) {
                
                // if next command group is created
                var next = commandGroup.nextChainTarget.GetAspect<UnitCommandGroupAspect>();
                next.Add(in unit);
                // UnityEngine.Debug.Log("Unit " + unit.ent.ToString() + " set cmd group: " + commandGroup.ent.ToString() + " => " + next.ent.ToString());
                return true;

            }
            
            // UnityEngine.Debug.Log("Unit " + unit.ent.ToString() + " has no next group");

            return false;

        }

        /// <summary>
        /// Try to move all units which has no T component from this group to the next one
        /// </summary>
        /// <param name="commandGroup"></param>
        /// <typeparam name="T"></typeparam>
        [INLINE(256)]
        public static void SetNextTargetIfAvailableExcept<T>(in UnitCommandGroupAspect commandGroup) where T : unmanaged, IComponent {

            for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                var unit = commandGroup.readUnits[i];
                if (unit.Has<T>() == false) {
                    SetNextTargetIfAvailable(unit.GetAspect<UnitAspect>());
                }
            }

        }

        /// <summary>
        /// Try to move all units from this group to the next one
        /// </summary>
        /// <param name="commandGroup"></param>
        [INLINE(256)]
        public static void SetNextTargetIfAvailable(in UnitCommandGroupAspect commandGroup) {

            for (uint i = 0u; i < commandGroup.readUnits.Count; ++i) {
                var unit = commandGroup.readUnits[i];
                SetNextTargetIfAvailable(unit.GetAspect<UnitAspect>());
            }

        }

    }

}