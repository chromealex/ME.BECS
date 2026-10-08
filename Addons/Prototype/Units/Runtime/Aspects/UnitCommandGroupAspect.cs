using System;
using ME.BECS.FixedPoint;

namespace ME.BECS.Units {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Command group used for the special commands
    /// like move, attack, etc
    /// For unit selection use UnitSelectionGroupAspect
    /// </summary>
    public partial struct UnitCommandGroupAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for group.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<CommandGroupComponent> groupDataPtr;

        /// <summary>
        /// Units included in the associated collection or operation.
        /// </summary>
        public readonly ref ListAuto<Ent> units => ref this.groupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).units;
        /// <summary>
        /// Read-only access to units.
        /// </summary>
        public readonly ref readonly ListAuto<Ent> readUnits => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).units;
        /// <summary>
        /// Targets considered by the associated operation.
        /// </summary>
        public readonly ref MemArrayAuto<Ent> targets => ref this.groupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).targets;
        /// <summary>
        /// Read-only access to targets.
        /// </summary>
        public readonly ref readonly MemArrayAuto<Ent> readTargets => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).targets;
        /// <summary>
        /// Volume used by the associated bounds calculation.
        /// </summary>
        public readonly ref uint volume => ref this.groupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).volume;
        /// <summary>
        /// Read-only access to volume.
        /// </summary>
        public readonly ref readonly uint readVolume => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).volume;
        /// <summary>
        /// Next chain target used by <c>UnitCommandGroupAspect</c>.
        /// </summary>
        public readonly ref Ent nextChainTarget => ref this.groupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).nextChainTarget;
        /// <summary>
        /// Prev chain target used by <c>UnitCommandGroupAspect</c>.
        /// </summary>
        public readonly ref Ent prevChainTarget => ref this.groupDataPtr.GetOrThrow(this.ent.id, this.ent.gen).prevChainTarget;
        /// <summary>
        /// Read-only access to next chain target.
        /// </summary>
        public readonly ref readonly Ent readNextChainTarget => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).nextChainTarget;
        /// <summary>
        /// Read-only access to prev chain target.
        /// </summary>
        public readonly ref readonly Ent readPrevChainTarget => ref this.groupDataPtr.Read(this.ent.id, this.ent.gen).prevChainTarget;
        
        /// <summary>
        /// Adds the supplied entry to unit command group aspect.
        /// </summary>
        public readonly void Add(in UnitAspect unit) => UnitUtils.AddToCommandGroup(in this, in unit);
        /// <summary>
        /// Adds the supplied entry to unit command group aspect.
        /// </summary>
        public readonly void Add(in Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent> list) => UnitUtils.AddToCommandGroup(in this, in list);

        /// <summary>
        /// Indicates is part of chain.
        /// </summary>
        public bool IsPartOfChain => this.nextChainTarget.IsAlive() == true;
        /// <summary>
        /// Indicates is locked.
        /// </summary>
        public bool IsLocked => this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.IsLocked;

        /// <summary>
        /// Whether this value contains no elements.
        /// </summary>
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

        /// <summary>
        /// Acquires the synchronization lock before accessing protected state.
        /// </summary>
        [INLINE(256)]
        public readonly void Lock() {

            this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.Lock();

        }

        /// <summary>
        /// Releases the synchronization lock after accessing protected state.
        /// </summary>
        [INLINE(256)]
        public readonly void Unlock() {
            
            this.groupDataPtr.Get(this.ent.id, this.ent.gen).lockIndex.Unlock();
            
        }

    }

}