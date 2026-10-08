using ME.BECS.Transforms;

namespace ME.BECS.Units {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Selection group used for the lists
    /// For unit commands use UnitCommandGroupAspect
    /// </summary>
    public partial struct UnitSelectionGroupAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for group.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<SelectionGroupComponent> groupDataPtr;

        /// <summary>
        /// Units included in the associated collection or operation.
        /// </summary>
        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).units;

        /// <summary>
        /// Read-only access to units.
        /// </summary>
        public readonly ref readonly ListAuto<Ent> readUnits => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).units;

        /// <summary>
        ///
        /// </summary>
        /// <param name="unit"></param>
        /// <returns>
        /// Amount of units in the group after addition
        /// </returns>
        [INLINE(256)]
        public readonly uint Add(in UnitAspect unit) => UnitUtils.AddToSelectionGroup(in this, in unit);

        /// <summary>
        /// Removes the specified entry from unit selection group aspect.
        /// </summary>
        [INLINE(256)]
        public readonly void Remove(in UnitAspect unit) => UnitUtils.RemoveFromSelectionGroup(in this, in unit);

        /// <summary>
        /// Removes all.
        /// </summary>
        [INLINE(256)]
        public readonly void RemoveAll() => UnitUtils.DestroySelectionGroup(in this);

        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        [INLINE(256)]
        public readonly void Destroy() => UnitUtils.DestroySelectionGroup(in this);

        /// <summary>
        /// Replaces selection-group membership with the supplied temporary selection.
        /// </summary>
        [INLINE(256)]
        public void Replace(in UnitSelectionTempGroupAspect group) {

            // clean up
            for (uint i = 0u; i < this.units.Count; ++i) {
                var unit = this.units[i];
                unit.GetAspect<UnitAspect>().unitSelectionGroup = default;
            }
            this.units.Clear();
            
            // add
            for (uint i = 0u; i < group.units.Count; ++i) {
                var unit = group.units[i];
                this.Add(unit.GetAspect<UnitAspect>());
            }

        }

    }

    /// <summary>
    /// Selection temp group
    /// </summary>
    public partial struct UnitSelectionTempGroupAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for group.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<SelectionGroupComponent> groupDataPtr;

        /// <summary>
        /// Units included in the associated collection or operation.
        /// </summary>
        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).units;

        /// <summary>
        /// Read-only access to units.
        /// </summary>
        public readonly ref readonly ListAuto<Ent> readUnits => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).units;

        /// <summary>
        /// Adds the supplied entry to unit selection temp group aspect.
        /// </summary>
        [INLINE(256)]
        public readonly void Add(in UnitAspect unit) => this.units.Add(unit.ent);

        /// <summary>
        /// Removes the specified entry from unit selection temp group aspect.
        /// </summary>
        [INLINE(256)]
        public readonly void Remove(in UnitAspect unit) => this.units.Remove(unit.ent);

        /// <summary>
        /// Destroys the referenced instance and applies its registered destruction handling.
        /// </summary>
        [INLINE(256)]
        public readonly void Destroy() {
            this.ent.DestroyHierarchy();
        }

    }

}