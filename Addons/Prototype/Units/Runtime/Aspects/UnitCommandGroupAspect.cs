namespace ME.BECS.Units {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    /// <summary>
    /// Command group used for the special commands
    /// like move, attack, etc
    /// For unit selection use UnitSelectionGroupAspect
    /// </summary>
    public struct UnitCommandGroupAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<CommandGroupComponent> groupDataPtr;

        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).units;
        public readonly ref MemArrayAuto<Ent> targets => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).targets;
        public readonly ref readonly MemArrayAuto<Ent> readTargets => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).targets;
        public readonly ref float volume => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).volume;
        public readonly ref readonly float readVolume => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).volume;

        public readonly void Add(in UnitAspect unit) => UnitUtils.AddToCommandGroup(in this, in unit);

        public bool IsLocked => this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.IsLocked;
        
        [INLINE(256)]
        public void Lock() {

            this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.Lock();

        }

        [INLINE(256)]
        public void Unlock() {
            
            this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.Unlock();
            
        }

    }

}