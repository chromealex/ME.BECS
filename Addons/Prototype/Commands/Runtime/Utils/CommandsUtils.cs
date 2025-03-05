namespace ME.BECS.Commands {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Units;
    using Pathfinding;
    using Unity.Collections;

    public static class CommandsUtils {

        /// <summary>
        /// Clear command chain for unit
        /// Add new command
        /// </summary>
        /// <param name="buildGraphSystem"></param>
        /// <param name="unit"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static UnitCommandGroupAspect SetCommand<T>(in BuildGraphSystem buildGraphSystem, in UnitAspect unit, in T data, in JobInfo jobInfo) where T : unmanaged, ICommandComponent {

            // remove from current group
            PathUtils.RemoveUnitFromGroup(in unit);
            // create new group
            var group = UnitUtils.CreateCommandGroup(buildGraphSystem.GetTargetsCapacity(), jobInfo: in jobInfo);
            group.Add(in unit);
            group.ent.Set(data);
            // move unit to target
            //PathUtils.UpdateTarget(in buildGraphSystem, in group, worldPos);
            return group;

        }

        /// <summary>
        /// Clear command chain for all units in selection
        /// Add move command + Add new command
        /// </summary>
        /// <param name="buildGraphSystem"></param>
        /// <param name="selectionGroupAspect"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static UnitCommandGroupAspect SetCommandWithMove<T>(in BuildGraphSystem buildGraphSystem, in UnitSelectionGroupAspect selectionGroupAspect, in T data, in JobInfo jobInfo) where T : unmanaged, ICommandComponent {
            
            var mainGroup = SetCommand(in buildGraphSystem, in selectionGroupAspect, new CommandMove() {
                targetPosition = data.TargetPosition,
            }, jobInfo);
            AddCommand(in buildGraphSystem, in selectionGroupAspect, in data, in jobInfo);
            AddCommand(in buildGraphSystem, in selectionGroupAspect, new CommandMove() {
                targetPosition = data.TargetPosition,
            }, jobInfo);
            return mainGroup;

        }

        /// <summary>
        /// Add command to chain for all units in selection
        /// Add move command + Add new command
        /// </summary>
        /// <param name="buildGraphSystem"></param>
        /// <param name="selectionGroupAspect"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static UnitCommandGroupAspect AddCommandWithMove<T>(in BuildGraphSystem buildGraphSystem, in UnitSelectionGroupAspect selectionGroupAspect, in T data, in JobInfo jobInfo) where T : unmanaged, ICommandComponent {
            
            var mainGroup = AddCommand(in buildGraphSystem, in selectionGroupAspect, new CommandMove() {
                targetPosition = data.TargetPosition,
            }, jobInfo);
            AddCommand(in buildGraphSystem, in selectionGroupAspect, in data, in jobInfo);
            AddCommand(in buildGraphSystem, in selectionGroupAspect, new CommandMove() {
                targetPosition = data.TargetPosition,
            }, jobInfo);
            return mainGroup;

        }

        /// <summary>
        /// Clear command chain for all units in selection
        /// Add new command
        /// </summary>
        /// <param name="buildGraphSystem"></param>
        /// <param name="selectionGroupAspect"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static UnitCommandGroupAspect SetCommand<T>(in BuildGraphSystem buildGraphSystem, in UnitSelectionGroupAspect selectionGroupAspect, in T data, in JobInfo jobInfo) where T : unmanaged, ICommandComponent {
            
            var commandGroup = UnitUtils.CreateCommandGroup(buildGraphSystem.GetTargetsCapacity(), in selectionGroupAspect, in jobInfo);
            commandGroup.ent.Set(data);
            return commandGroup;
            
        }

        /// <summary>
        /// Clear command chain for all units in selection
        /// Add new command
        /// </summary>
        /// <param name="buildGraphSystem"></param>
        /// <param name="selectionGroupAspect"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static UnitCommandGroupAspect SetCommand<T>(in BuildGraphSystem buildGraphSystem, in UnitSelectionTempGroupAspect selectionGroupAspect, in T data, in JobInfo jobInfo) where T : unmanaged, ICommandComponent {
            
            var commandGroup = UnitUtils.CreateCommandGroup(buildGraphSystem.GetTargetsCapacity(), in selectionGroupAspect, in jobInfo);
            commandGroup.ent.Set(data);
            return commandGroup;
            
        }

        /// <summary>
        /// Add command to chain for all units in selection
        /// </summary>
        /// <param name="buildGraphSystem"></param>
        /// <param name="selectionGroupAspect"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static UnitCommandGroupAspect AddCommand<T>(in BuildGraphSystem buildGraphSystem, in UnitSelectionGroupAspect selectionGroupAspect, in T data, in JobInfo jobInfo) where T : unmanaged, ICommandComponent {

            // get all unique command groups for each unit in current selection
            var noChainCommandGroup = UnitUtils.CreateCommandGroup(buildGraphSystem.GetTargetsCapacity(), selectionGroupAspect.units.Count, in jobInfo);
            var uniqueGroups = new NativeHashSet<Ent>((int)selectionGroupAspect.units.Count, Constants.ALLOCATOR_TEMP);
            for (uint i = 0; i < selectionGroupAspect.units.Count; ++i) {
                var unit = selectionGroupAspect.units[i].GetAspect<UnitAspect>();
                uniqueGroups.Add(unit.unitCommandGroup);
                if (unit.unitCommandGroup.IsAlive() == false ||
                    unit.IsPathFollow == false) {
                    noChainCommandGroup.Add(in unit);
                }
            }

            foreach (var group in uniqueGroups) {
                if (group.IsAlive() == true) {
                    // create command group on-demand
                    // when unit has arrived - use chain group to move next
                    var chainCommandGroup = UnitUtils.CreateCommandGroup(buildGraphSystem.GetTargetsCapacity(), selectionGroupAspect.units.Count);
                    chainCommandGroup.ent.Set(data);
                    var groupAspect = group.GetAspect<UnitCommandGroupAspect>();
                    PathUtils.AddChainTarget(groupAspect, chainCommandGroup);
                }
            }

            { // resolve no-chain group - move units now
                if (noChainCommandGroup.units.Count > 0u) {
                    noChainCommandGroup.ent.Set(data);
                    //PathUtils.UpdateTarget(in buildGraphSystem, in commandGroup, in worldPos);
                } else {
                    UnitUtils.DestroyCommandGroup(in noChainCommandGroup);
                }
            }

            uniqueGroups.Dispose();

            return noChainCommandGroup;

        }

    }

}