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
        public readonly ref readonly ListAuto<Ent> readUnits => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).units;
        public readonly ref MemArrayAuto<Ent> targets => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).targets;
        public readonly ref readonly MemArrayAuto<Ent> readTargets => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).targets;
        public readonly ref uint volume => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).volume;
        public readonly ref readonly uint readVolume => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).volume;
        public readonly ref Ent nextChainTarget => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).nextChainTarget;
        public readonly ref Ent prevChainTarget => ref this.groupDataPtr.Get(this.ent.id, this.ent.gen).prevChainTarget;
        
        public readonly void Add(in UnitAspect unit) => UnitUtils.AddToCommandGroup(in this, in unit);

        public bool IsPartOfChain => this.nextChainTarget.IsAlive() == true;
        public bool IsLocked => this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.IsLocked;

        public bool IsEmpty {
            [INLINE(256)]
            get {
                if (this.prevChainTarget.IsAlive() == false) return true;
                var chain = this.prevChainTarget.GetAspect<UnitCommandGroupAspect>();
                while (chain.IsAlive() == true) {
                    if (chain.readUnits.Count > 0u) return false;
                    if (chain.prevChainTarget.IsAlive() == false) break;
                    chain = chain.prevChainTarget.GetAspect<UnitCommandGroupAspect>();
                }
                    
                return true;
            }
        }
        
        [INLINE(256)]
        public readonly void Lock() {

            this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.Lock();

        }

        [INLINE(256)]
        public readonly void Unlock() {
            
            this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.Unlock();
            
        }

    }

}