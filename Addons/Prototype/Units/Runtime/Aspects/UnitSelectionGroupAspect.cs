using ME.BECS.Transforms;

namespace ME.BECS.Units {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    /// <summary>
    /// Selection group used for the lists
    /// For unit commands use UnitCommandGroupAspect
    /// </summary>
    public struct UnitSelectionGroupAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<SelectionGroupComponent> groupDataPtr;

        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).units;

        public readonly ref readonly ListAuto<Ent> readUnits => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).units;

        [INLINE(256)]
        public readonly void Add(in UnitAspect unit) => UnitUtils.AddToSelectionGroup(in this, in unit);

        [INLINE(256)]
        public readonly void Remove(in UnitAspect unit) => UnitUtils.RemoveFromSelectionGroup(in this, in unit);

        [INLINE(256)]
        public readonly void RemoveAll() => UnitUtils.DestroySelectionGroup(in this);

        [INLINE(256)]
        public readonly void Destroy() => UnitUtils.DestroySelectionGroup(in this);

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
    public struct UnitSelectionTempGroupAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<SelectionGroupComponent> groupDataPtr;

        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).units;

        public readonly ref readonly ListAuto<Ent> readUnits => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).units;

        [INLINE(256)]
        public readonly void Add(in UnitAspect unit) => this.units.Add(unit.ent);

        [INLINE(256)]
        public readonly void Remove(in UnitAspect unit) => this.units.Remove(unit.ent);

        [INLINE(256)]
        public readonly void Destroy() {
            this.ent.DestroyHierarchy();
        }

    }

}